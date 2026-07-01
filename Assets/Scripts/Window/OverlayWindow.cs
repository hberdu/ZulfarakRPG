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

        // Fully-transparent clear colour for the gameplay window. The camera clears
        // to alpha 0, and DwmExtendFrameIntoClientArea (all-negative "sheet of glass"
        // margins) tells Windows to composite every alpha-0 pixel as a see-through
        // hole over the live desktop. Sprites/text/HUD (alpha 1) stay opaque.
        // This is the Built-in Render Pipeline path — no URP renderer feature needed,
        // and it works with Unity's DirectX swapchain (unlike LWA_COLORKEY, which a
        // DX flip-model swapchain bypasses entirely → that's why magenta stayed pink).
        public static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0f);

        // ── Public settings ───────────────────────────────────────────────────
        public int windowWidth  = 400;
        public int windowHeight = 240;

        public static OverlayWindow Instance { get; private set; }
        public static int WinX { get; private set; } = 40;
        public static int WinY { get; private set; } = 40;

        static IntPtr _hwnd = IntPtr.Zero;

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
            Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
            ApplyOverlay();
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
            ApplyOverlay();
        }

        void OnEnable()  => UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode _)
        {
            bool gameplay = scene.name == "Zulfarak" || scene.name == "Dungeon";
            int newW = gameplay ? 400 : 380;
            int newH = gameplay ? 120 : 640;
            bool sizeChanged = newW != windowWidth || newH != windowHeight;
            windowWidth  = newW;
            windowHeight = newH;
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
            // Only call SetResolution when the size actually changed — otherwise Windows
            // briefly hides/repositions the window on every scene load (visible flicker
            // when entering the dungeon via the portal).
            if (sizeChanged) Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
            // Same idea for the WinAPI side: SetWindowPos with SWP_FRAMECHANGED forces
            // a visible reframe even at the same size, so only do it on a real resize.
            // The borderless/topmost style flags are still re-applied silently below.
            ApplyOverlay(repositionWindow: sizeChanged);
            MenuPopupWindow.Reposition();
#endif
        }

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

            // Hide from taskbar/Alt+Tab. NO WS_EX_LAYERED — the color-key it enables
            // is bypassed by Unity's DirectX flip-model swapchain. We use DWM per-pixel
            // alpha instead (below), which composites the framebuffer's alpha channel.
            int ex = GetWindowLong(_hwnd, GWL_EXSTYLE);
            ex |= WS_EX_TOOLWINDOW;
            SetWindowLong(_hwnd, GWL_EXSTYLE, ex);

            if (repositionWindow)
            {
                SetWindowPos(_hwnd, HWND_TOPMOST,
                    WinX, WinY, windowWidth, windowHeight,
                    SWP_SHOWWINDOW | SWP_FRAMECHANGED);
            }

            // "Sheet of glass": all-negative margins extend the DWM frame across the
            // whole client area, so Windows blends the window using the framebuffer's
            // per-pixel alpha. Camera clears to alpha 0 → those pixels show the desktop.
            var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(_hwnd, ref margins);
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
            MenuPopupWindow.Reposition();
#endif
        }

        public static bool GetOsCursorPos(out POINT p)
        {
            return GetCursorPos(out p);
        }

        // Re-apply WS_EX_TOOLWINDOW every frame in the build. Unity sometimes resets
        // window styles after the initial application, so this keeps the overlay sticky.
        void Update()
        {
#if !UNITY_EDITOR
            if (_hwnd == IntPtr.Zero) _hwnd = Process.GetCurrentProcess().MainWindowHandle;
            if (_hwnd == IntPtr.Zero) return;
            int ex = GetWindowLong(_hwnd, GWL_EXSTYLE);
            if ((ex & WS_EX_TOOLWINDOW) == 0)
            {
                ex |= WS_EX_TOOLWINDOW;
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
