using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ZulfarakRPG
{
    // Runtime-built HUD strip (top-center) showing dungeon progress by waves.
    // Fill grows per cleared wave; a small skull at the right end marks the boss.
    public class DungeonProgressBar : MonoBehaviour
    {
        private const float BarW = 150f;
        private const float BarH = 7f;

        private RectTransform _fill;
        private TextMeshProUGUI _label;
        private int _totalWaves = 10;

        public static DungeonProgressBar Create(Canvas canvas, int totalWaves)
        {
            if (canvas == null)
            {
                canvas = Object.FindAnyObjectByType<Canvas>();
                if (canvas == null) return null;
            }

            var root = new GameObject("DungeonProgressBar", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -6f);
            rt.sizeDelta = new Vector2(BarW + 18f, BarH + 12f);

            var bar = root.AddComponent<DungeonProgressBar>();
            bar._totalWaves = Mathf.Max(1, totalWaves);
            bar.Build();
            bar.SetWave(0);
            return bar;
        }

        void Build()
        {
            // Background trough
            var bg = MakeImage("BG", transform, new Color(0f, 0f, 0f, 0.55f));
            var bgRt = (RectTransform)bg.transform;
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
            bgRt.anchoredPosition = new Vector2(-8f, 0f);
            bgRt.sizeDelta = new Vector2(BarW + 4f, BarH + 4f);

            // Fill (left-anchored so width = progress)
            var fill = MakeImage("Fill", bg.transform, new Color(0.62f, 0.18f, 0.75f, 0.95f));
            _fill = (RectTransform)fill.transform;
            _fill.anchorMin = new Vector2(0f, 0.5f);
            _fill.anchorMax = new Vector2(0f, 0.5f);
            _fill.pivot = new Vector2(0f, 0.5f);
            _fill.anchoredPosition = new Vector2(2f, 0f);
            _fill.sizeDelta = new Vector2(0f, BarH);

            // Wave segment ticks
            for (int i = 1; i < _totalWaves; i++)
            {
                var tick = MakeImage($"Tick{i}", bg.transform, new Color(0f, 0f, 0f, 0.6f));
                var tr = (RectTransform)tick.transform;
                tr.anchorMin = tr.anchorMax = new Vector2(0f, 0.5f);
                tr.pivot = new Vector2(0.5f, 0.5f);
                tr.anchoredPosition = new Vector2(2f + BarW * i / (float)_totalWaves, 0f);
                tr.sizeDelta = new Vector2(1f, BarH);
            }

            // Boss skull at the right end
            var skull = MakeImage("Skull", transform, Color.white);
            skull.sprite = BuildSkullSprite();
            var skRt = (RectTransform)skull.transform;
            skRt.anchorMin = skRt.anchorMax = new Vector2(1f, 0.5f);
            skRt.pivot = new Vector2(1f, 0.5f);
            skRt.anchoredPosition = new Vector2(0f, 0f);
            skRt.sizeDelta = new Vector2(13f, 13f);

            // "Wave X/10" label under the bar
            var lgo = new GameObject("Label", typeof(RectTransform));
            var lrt = (RectTransform)lgo.transform;
            lrt.SetParent(transform, false);
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(-8f, 1f);
            lrt.sizeDelta = new Vector2(BarW, 12f);
            _label = lgo.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 9f;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = new Color(1f, 1f, 1f, 0.85f);
            _label.raycastTarget = false;
        }

        // completedWaves = waves already cleared (0.._totalWaves)
        public void SetWave(int completedWaves)
        {
            int done = Mathf.Clamp(completedWaves, 0, _totalWaves);
            if (_fill)  _fill.sizeDelta = new Vector2(BarW * done / (float)_totalWaves, BarH);
            if (_label) _label.text = done >= _totalWaves
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

        // Tiny procedural 9x9 pixel skull so no sprite asset is required.
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
    }
}
