using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZulfarakRPG
{
    // Makes the Unity window borderless, always-on-top and draggable (build only).
    public class OverlayWindow : MonoBehaviour
    {
        // ── WinAPI ────────────────────────────────────────────────────────────
        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")] static extern int    GetWindowLong(IntPtr h, int n);
        [DllImport("user32.dll")] static extern int    SetWindowLong(IntPtr h, int n, int v);
        [DllImport("user32.dll")] static extern bool   SetWindowPos(IntPtr h, IntPtr z, int x, int y, int cx, int cy, uint f);
        [DllImport("user32.dll")] static extern bool   GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern bool   SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
        [DllImport("user32.dll")] static extern int    SetWindowCompositionAttribute(IntPtr h, ref WindowCompositionAttributeData data);
        [DllImport("dwmapi.dll")] static extern int    DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);
        [DllImport("dwmapi.dll")] static extern int    DwmEnableBlurBehindWindow(IntPtr hWnd, ref DWM_BLURBEHIND pBlurBehind);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)]
        struct MARGINS { public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight; }
        [StructLayout(LayoutKind.Sequential)]
        struct DWM_BLURBEHIND { public uint dwFlags; public bool fEnable; public IntPtr hRgnBlur; public bool fTransitionOnMaximized; }
        [StructLayout(LayoutKind.Sequential)]
        struct WindowCompositionAttributeData { public int Attribute; public IntPtr Data; public int SizeOfData; }
        [StructLayout(LayoutKind.Sequential)]
        struct AccentPolicy { public uint AccentState; public uint AccentFlags; public uint GradientColor; public uint AnimationId; }

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const int  GWL_STYLE       = -16;
        const int  GWL_EXSTYLE     = -20;
        const int  WS_BORDER       = 0x00800000;
        const int  WS_CAPTION      = 0x00C00000;
        const int  WS_THICKFRAME   = 0x00040000;
        const int  WS_SYSMENU      = 0x00080000;
        const int  WS_MINIMIZEBOX  = 0x00020000;
        const int  WS_MAXIMIZEBOX  = 0x00010000;
        const int  WS_EX_LAYERED   = 0x00080000;
        const int  WS_EX_TOOLWINDOW= 0x00000080;  // hides the window from the taskbar and Alt+Tab
        const uint LWA_COLORKEY    = 0x00000001;
        const uint DWM_BB_ENABLE   = 0x00000001;
        const int  WCA_ACCENT_POLICY = 19;
        const uint ACCENT_ENABLE_TRANSPARENTGRADIENT = 2;
        const uint SWP_SHOWWINDOW  = 0x0040;
        const uint SWP_FRAMECHANGED= 0x0020;

        // Transparency retired: this is now a normal OPAQUE window. The gameplay camera clears
        // straight to this forest-sky colour (no magenta key, no per-pixel layered push, no
        // desktop see-through). Tune to taste.
        public static readonly Color BackgroundColor = new Color(0.53f, 0.71f, 0.63f, 1f);

        // ── Public settings ───────────────────────────────────────────────────
        // 400 px wide keeps the strip compact (TaskbarHero-style) instead of a very wide
        // band. The character's on-screen size is unaffected — it depends only on the
        // strip HEIGHT and GameplayCamOrtho, not the width (which just controls how much
        // world is visible left/right). CameraFollow2D scrolls to keep the hero centred.
        public int windowWidth  = 960;
        public int windowHeight = 260;

        // Orthographic half-height applied to Camera.main during gameplay. The CITY and the
        // DUNGEON share this exact zoom so the world reads at the same scale in both.
        // Tuned for the thin 960×260 TaskbarHero-style strip: ortho 1.0 crops the view to a
        // narrow band that fits the ground + full character height with little sky. camY sits
        // the ground near the bottom of the strip. Tweak in-editor.
        public const float GameplayCamOrtho = 1.1f;
        public const float GameplayCamY     = 0.55f;

        public static OverlayWindow Instance { get; private set; }
        public static int WinX { get; private set; } = 40;
        public static int WinY { get; private set; } = 40;

        // True while a left-drag is actively moving the overlay window (past the click
        // threshold). Lets gameplay ignore the press so a drag never doubles as a click.
        public static bool IsDraggingWindow { get; private set; }

        // Pixels the cursor must travel while the button is held before the press counts
        // as a window drag (below this, it stays a normal gameplay/HUD click).
        const int DragThreshold = 5;

        static IntPtr _hwnd = IntPtr.Zero;
#if !UNITY_EDITOR
        Coroutine _resizeCo;
        bool _overlayActive;
        bool _pressActive;
        bool _draggingAnywhere;
        int _dragStartWinX;
        int _dragStartWinY;
        int _dragStartCursorX;
        int _dragStartCursorY;
#endif

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            WinX = PlayerPrefs.GetInt("overlay_x", 40);
            WinY = PlayerPrefs.GetInt("overlay_y", 40);

#if !UNITY_EDITOR
            // Force windowed mode — fullscreen breaks DWM transparency.
            Screen.fullScreen = false;
#endif
        }

        void Start()
        {
#if !UNITY_EDITOR
            if (Instance == this) StartCoroutine(DelayedApply());
#endif
        }

        // Awake runs before the Win32 window is fully created; wait two frames so
        // Process.MainWindowHandle is valid before we call SetWindowLong.
        IEnumerator DelayedApply()
        {
            yield return null;
            yield return null;
            ApplyWindowModeForScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        void OnEnable()  => UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode _)
        {
            ApplyWindowModeForScene(scene.name);
        }

        void ApplyWindowModeForScene(string sceneName)
        {
            // Bootstrap boots at the gameplay resolution so the loading screen and
            // the game share the exact same window size (no resize on scene swap).
            bool gameplay = sceneName == "Zulfarak" || sceneName == "Dungeon" || sceneName == "Bootstrap"
                            || sceneName.StartsWith("Camp_") || sceneName.StartsWith("Dungeon_");
            int newW = gameplay ? 960 : 380;
            int newH = gameplay ? 260 : 640;
            windowWidth = newW;
            windowHeight = newH;
            if (gameplay && Camera.main != null)
            {
                // Shrink the visible world vertically so the ground reads as a thin
                // taskbar-style strip. Scene assets store 0.75 as their baked ortho;
                // we override it every scene load so any Camera.main lands here.
                Camera.main.orthographic     = true;
                Camera.main.orthographicSize = GameplayCamOrtho;
                // Opaque forest-sky clear (works in the editor game view too, not just the build).
                Camera.main.clearFlags      = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = BackgroundColor;
                // Nudge the view up so the crop lands mostly on empty sky above the
                // character — CameraFollow2D owns the runtime Y, so route through it
                // when present; otherwise write the position directly.
                var follow = Camera.main.GetComponent<CameraFollow2D>();
                if (follow != null) follow.fixedY = GameplayCamY;
                else
                {
                    var p = Camera.main.transform.position;
                    p.y = GameplayCamY;
                    Camera.main.transform.position = p;
                }
            }
#if !UNITY_EDITOR
            if (gameplay && Camera.main != null)
            {
                // clearFlags MUST be SolidColor so the camera clears the background to
                // alpha 0; Skybox/DepthOnly would leave opaque pixels DWM can't see through.
                Camera.main.clearFlags      = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = BackgroundColor;
                Camera.main.allowHDR        = false;
                Camera.main.allowMSAA       = false;
            }
            _overlayActive = true;
            // Apply the borderless/topmost style + DWM glass immediately (no reposition
            // here — it would fight the resize below).
            ApplyOverlay(repositionWindow: false);
            // ALWAYS drive the resolution + a hard SetWindowPos pin — even when Screen
            // already reports the target size. On boot the OS window can still be at a
            // DPI-scaled rect (bigger than the backbuffer), which zoomed the game AND
            // desynced mouse hit-testing so the HUD buttons weren't clickable until a
            // manual drag re-pinned the rect. Gating this on `sizeChanged` skipped the pin
            // in exactly that case; now it runs unconditionally and re-pins a few times to
            // beat the deferred Screen.SetResolution / DPI settling.
            Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
            if (_resizeCo != null) StopCoroutine(_resizeCo);
            _resizeCo = StartCoroutine(LockWindowSizeAfterResolution());
#endif
        }

#if !UNITY_EDITOR
        IEnumerator LockWindowSizeAfterResolution()
        {
            // Let Unity apply the deferred Screen.SetResolution before we pin the size.
            yield return null;
            yield return null;
            ApplyOverlay(repositionWindow: true);
            MenuPopupWindow.Reposition();
            // Re-pin a few times over the first ~0.4s: DPI scaling and Unity's window
            // settling can otherwise leave the rect oversized until the user drags it.
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForSecondsRealtime(0.1f);
                ApplyOverlay(repositionWindow: true);
            }
            MenuPopupWindow.Reposition();
            _resizeCo = null;
        }
#endif

        void ApplyOverlay(bool repositionWindow = true)
        {
            // Process MainWindowHandle is more reliable than GetActiveWindow at startup.
            _hwnd = Process.GetCurrentProcess().MainWindowHandle;
            if (_hwnd == IntPtr.Zero) _hwnd = GetActiveWindow();
            if (_hwnd == IntPtr.Zero) return;

            // Remove title bar + all borders
            int style = GetWindowLong(_hwnd, GWL_STYLE);
            style &= ~(WS_BORDER | WS_CAPTION | WS_THICKFRAME |
                       WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
            SetWindowLong(_hwnd, GWL_STYLE, style);

            // Opaque window: WS_EX_TOOLWINDOW still hides us from taskbar/Alt+Tab, but
            // WS_EX_LAYERED is cleared — transparency is retired, so the normal DX-rendered
            // (opaque) content shows directly. No LayeredWindowRenderer push.
            int ex = GetWindowLong(_hwnd, GWL_EXSTYLE);
            ex |= WS_EX_TOOLWINDOW;
            ex &= ~WS_EX_LAYERED;
            SetWindowLong(_hwnd, GWL_EXSTYLE, ex);

            if (repositionWindow)
            {
                SetWindowPos(_hwnd, HWND_TOPMOST,
                    WinX, WinY, windowWidth, windowHeight,
                    SWP_SHOWWINDOW | SWP_FRAMECHANGED);
            }
        }

        public static void MoveWindowTo(int x, int y)
        {
            WinX = Mathf.Max(0, x);
            WinY = Mathf.Max(0, y);
            PlayerPrefs.SetInt("overlay_x", WinX);
            PlayerPrefs.SetInt("overlay_y", WinY);
#if !UNITY_EDITOR
            if (_hwnd == IntPtr.Zero) return;
            if (Instance == null) return;
            SetWindowPos(_hwnd, HWND_TOPMOST,
                WinX, WinY, Instance.windowWidth, Instance.windowHeight,
                SWP_SHOWWINDOW);
            // Keep every top popup glued to the game strip while it's dragged.
            MenuPopupWindow.Reposition();
            WorldMapPopup.Reposition();
            FriendsListPopup.Reposition();
            InventoryPopupWindow.Reposition();
            SkillTreePopup.Reposition();
#endif
        }

        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int nCmdShow);
        const int SW_MINIMIZE = 6;

        // Minimize the overlay to the taskbar (used by the HUD's minimize button).
        public static void Minimize()
        {
#if !UNITY_EDITOR
            if (_hwnd != IntPtr.Zero) ShowWindow(_hwnd, SW_MINIMIZE);
#endif
        }

        // Close the game (HUD close button).
        public static void QuitGame()
        {
            Application.Quit();
        }

        public static bool GetOsCursorPos(out POINT p)
        {
            return GetCursorPos(out p);
        }

        // Re-apply WS_EX_TOOLWINDOW every frame in the build. Unity sometimes resets
        // window styles after the initial application, so this keeps the overlay sticky.
        void Update()
        {
            // Consume a pending map teleport (set by WorldMapPopup's native click handler):
            // play the horse-gallop cutscene, which fades + loads the target scene itself.
            if (WorldMapPopup.PendingScene != null)
            {
                var s = WorldMapPopup.PendingScene;
                WorldMapPopup.PendingScene = null;
                HorseCutscene.Play(s);
                return;
            }
#if !UNITY_EDITOR
            if (_hwnd == IntPtr.Zero) _hwnd = Process.GetCurrentProcess().MainWindowHandle;
            if (_hwnd == IntPtr.Zero) return;
            if (!_overlayActive) return;

            // Left-drag anywhere in the window moves the borderless overlay — in every
            // scene. A small movement threshold separates a genuine drag from a gameplay
            // click (walk-to / HUD button), so pure clicks still reach the game unchanged.
            if (Input.GetMouseButtonDown(0) && GetCursorPos(out var start))
            {
                _pressActive = true;
                _draggingAnywhere = false;
                _dragStartCursorX = start.X;
                _dragStartCursorY = start.Y;
                _dragStartWinX = WinX;
                _dragStartWinY = WinY;
            }
            else if (_pressActive && Input.GetMouseButton(0) && GetCursorPos(out var now))
            {
                int dx = now.X - _dragStartCursorX;
                int dy = now.Y - _dragStartCursorY;
                if (!_draggingAnywhere && (Mathf.Abs(dx) > DragThreshold || Mathf.Abs(dy) > DragThreshold))
                {
                    _draggingAnywhere = true;
                    IsDraggingWindow = true;
                    // The mouse-down may have queued a click-to-move; undo it now that we
                    // know the press is a window drag.
                    var pc = UnityEngine.Object.FindAnyObjectByType<PlayerController2D>();
                    if (pc != null) pc.CancelClickTarget();
                }
                if (_draggingAnywhere)
                    MoveWindowTo(_dragStartWinX + dx, _dragStartWinY + dy);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _pressActive = false;
                _draggingAnywhere = false;
                IsDraggingWindow = false;
            }

            // Keep the taskbar-hiding sticky (Unity occasionally resets the ex-style) and make
            // sure WS_EX_LAYERED never sneaks back — the window must stay opaque.
            int ex = GetWindowLong(_hwnd, GWL_EXSTYLE);
            if ((ex & WS_EX_TOOLWINDOW) == 0 || (ex & WS_EX_LAYERED) != 0)
            {
                ex = (ex | WS_EX_TOOLWINDOW) & ~WS_EX_LAYERED;
                SetWindowLong(_hwnd, GWL_EXSTYLE, ex);
            }
#endif
        }

        void OnApplicationQuit()
        {
            PlayerPrefs.Save();
        }
    }
}
