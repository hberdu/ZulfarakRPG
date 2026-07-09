using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ZulfarakRPG
{
    // Runtime-built HUD strip showing dungeon progress by waves. Sits along the bottom
    // of the taskbar-thin gameplay window, right of the inventory button. The frame is
    // the horizontal papyrus banner from the Pixel RPG UI Pack (9-sliced so it stretches
    // to any width) and the purple fill grows inside it as waves are cleared. A small
    // yellow triangle pins itself to the END of the fill so the "player is here" marker
    // is always the leading edge of progress.
    public class DungeonProgressBar : MonoBehaviour
    {
        // Layout metrics (screen pixels; the canvas is ConstantPixelSize so these stay
        // pixel-perfect at any DPI).
        private const float BannerW  = 460f;
        private const float BannerH  = 32f;
        // Inside padding — keeps the fill/ticks/marker inside the banner's built-in
        // decorative border (rolled papyrus edges).
        private const float PadLeft  = 16f;
        private const float PadRight = 16f;
        private const float PadTop   = 8f;
        private const float PadBot   = 8f;

        private RectTransform _fill;
        private RectTransform _marker;
        private TextMeshProUGUI _label;
        private int _totalWaves = 10;

        public static DungeonProgressBar Create(Canvas canvas, int totalWaves)
        {
            var root = new GameObject("DungeonProgressBar", typeof(RectTransform));
            var canvasComp = root.AddComponent<Canvas>();
            canvasComp.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvasComp.sortingOrder = 690;                     // just under the HUD buttons (700)
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var bar = root.AddComponent<DungeonProgressBar>();
            bar._totalWaves = Mathf.Max(1, totalWaves);
            bar.Build();
            bar.SetWave(0);
            return bar;
        }

        void Build()
        {
            // Anchor bottom-left, starting just to the right of the inventory button.
            float xStart = PlayerHud.ButtonColumnX + PlayerHud.ButtonSize + 4f;
            float yBase  = PlayerHud.ButtonColumnBottomY;

            var holder = new GameObject("Holder", typeof(RectTransform));
            var hrt = (RectTransform)holder.transform;
            hrt.SetParent(transform, false);
            hrt.anchorMin = hrt.anchorMax = new Vector2(0f, 0f);
            hrt.pivot     = new Vector2(0f, 0f);
            hrt.anchoredPosition = new Vector2(xStart, yBase);
            hrt.sizeDelta = new Vector2(BannerW, BannerH);

            // ── Banner background (9-sliced papyrus scroll from the RPG UI Pack) ──
            var bannerGO = new GameObject("Banner", typeof(RectTransform));
            bannerGO.transform.SetParent(holder.transform, false);
            var brt = (RectTransform)bannerGO.transform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            var banner = bannerGO.AddComponent<Image>();
            banner.sprite         = RpgUiSprites.BannerHorizontal();
            banner.type           = Image.Type.Sliced;
            banner.pixelsPerUnitMultiplier = 1f;
            banner.raycastTarget  = false;

            // ── Dark fill trough sitting INSIDE the banner (parent of fill/ticks/marker) ──
            var bg = MakeImage("BG", holder.transform, new Color(0.10f, 0.05f, 0.08f, 0.85f));
            var bgRt = (RectTransform)bg.transform;
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = new Vector2( PadLeft,  PadBot);
            bgRt.offsetMax = new Vector2(-PadRight, -PadTop);

            // Fill (left-anchored so width = progress, driven via anchorMax in SetWave).
            var fill = MakeImage("Fill", bg.transform, new Color(0.62f, 0.18f, 0.75f, 0.95f));
            _fill = (RectTransform)fill.transform;
            _fill.anchorMin = new Vector2(0f, 0f);
            _fill.anchorMax = new Vector2(0f, 1f);
            _fill.pivot     = new Vector2(0f, 0.5f);
            _fill.anchoredPosition = Vector2.zero;
            _fill.sizeDelta = Vector2.zero;

            // Wave segment ticks anchored at fractional positions so they follow the
            // BG's actual width (no BarW dependency).
            for (int i = 1; i < _totalWaves; i++)
            {
                float t = i / (float)_totalWaves;
                var tick = MakeImage($"Tick{i}", bg.transform, new Color(0.92f, 0.75f, 0.30f, 0.65f));
                var tr = (RectTransform)tick.transform;
                tr.anchorMin = new Vector2(t, 0f);
                tr.anchorMax = new Vector2(t, 1f);
                tr.pivot = new Vector2(0.5f, 0.5f);
                tr.anchoredPosition = Vector2.zero;
                tr.sizeDelta = new Vector2(1f, 0f);
            }

            // Boss skull at the very right end of the fill journey.
            var skull = MakeImage("Skull", bg.transform, Color.white);
            skull.sprite = BuildSkullSprite();
            var skRt = (RectTransform)skull.transform;
            skRt.anchorMin = skRt.anchorMax = new Vector2(1f, 0.5f);
            skRt.pivot = new Vector2(1f, 0.5f);
            skRt.anchoredPosition = new Vector2(-2f, 0f);
            skRt.sizeDelta = new Vector2(9f, 9f);

            // Player-position marker: yellow triangle pinned to the END of the fill.
            var markGO = new GameObject("PlayerMarker", typeof(RectTransform));
            markGO.transform.SetParent(bg.transform, false);
            _marker = (RectTransform)markGO.transform;
            _marker.anchorMin = _marker.anchorMax = new Vector2(0f, 1f);
            _marker.pivot = new Vector2(0.5f, 0f);
            _marker.anchoredPosition = new Vector2(0f, -1f);
            _marker.sizeDelta = new Vector2(6f, 5f);
            var markImg = markGO.AddComponent<Image>();
            markImg.sprite        = BuildDownArrowSprite();
            markImg.color         = new Color(1f, 0.90f, 0.30f, 1f);
            markImg.raycastTarget = false;

            // "Wave X/10" label sits over the banner centre in the papyrus tint.
            var lgo = new GameObject("Label", typeof(RectTransform));
            var lrt = (RectTransform)lgo.transform;
            lrt.SetParent(bg.transform, false);
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            _label = lgo.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 9f;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = new Color(1f, 0.98f, 0.82f, 0.95f);
            _label.raycastTarget = false;
            _label.fontStyle = FontStyles.Bold;
        }

        // completedWaves = waves already cleared (0.._totalWaves)
        public void SetWave(int completedWaves)
        {
            int done = Mathf.Clamp(completedWaves, 0, _totalWaves);
            float frac = done / (float)_totalWaves;
            if (_fill)
            {
                _fill.anchorMax = new Vector2(frac, 1f);
                _fill.sizeDelta = Vector2.zero;
            }
            if (_marker)
                _marker.anchorMin = _marker.anchorMax = new Vector2(frac, 1f);
            if (_label)
                _label.text = done >= _totalWaves
                    ? "COMPLETO!"
                    : $"Wave {Mathf.Min(done + 1, _totalWaves)}/{_totalWaves}";
        }

        static Image MakeImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Sprite BuildSkullSprite()
        {
            string[] rows = {
                "..XXXXX..",
                ".XXXXXXX.",
                "XXXXXXXXX",
                "XX.XXX.XX",
                "XX.XXX.XX",
                "XXXXXXXXX",
                ".XX.X.XX.",
                ".XXXXXXX.",
                "..X.X.X..",
            };
            int size = 9;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var bone = new Color(0.92f, 0.90f, 0.82f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, size - 1 - y, rows[y][x] == 'X' ? bone : Color.clear);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 9f);
        }

        static Sprite _downArrow;
        static Sprite BuildDownArrowSprite()
        {
            if (_downArrow != null) return _downArrow;
            const int W = 7, H = 5;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    tex.SetPixel(x, y, Color.clear);
            for (int row = 0; row < H; row++)
            {
                int y = H - 1 - row;
                int half = (H - 1 - row);
                for (int x = -half; x <= half; x++)
                    tex.SetPixel((W / 2) + x, y, Color.white);
            }
            tex.Apply();
            _downArrow = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 100f);
            return _downArrow;
        }
    }
}
