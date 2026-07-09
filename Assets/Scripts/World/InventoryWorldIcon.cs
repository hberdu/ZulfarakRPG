using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // World-space inventory icon. The auto-spawn hook was removed — the inventory
    // popup is now opened from the pixel-art HUD button (and still the `I` shortcut),
    // so no floating icon appears in the city. Class kept so manual callers still work.
    public class InventoryWorldIcon : MonoBehaviour
    {
        public float bobAmplitude = 0.05f;
        public float bobSpeed = 1.6f;
        public string tooltipText = "Inventario";

        public static InventoryWorldIcon SpawnAt(Vector3 worldPos)
        {
            var go = new GameObject("InventoryWorldIcon");
            go.transform.position = worldPos;
            go.transform.localScale = Vector3.one * 0.55f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadInventoryIconSprite() ?? MakeFallbackIconSprite();
            sr.sortingOrder = 11;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.30f;

            return go.AddComponent<InventoryWorldIcon>();
        }

        SpriteRenderer _sr;
        CircleCollider2D _col;
        Camera _cam;
        Vector3 _basePos;
        GameObject _tooltipRoot;
        bool _hovering;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _col = GetComponent<CircleCollider2D>();
            _basePos = transform.position;
            BuildTooltip();
        }

        void Update()
        {
            float t = Time.time;
            transform.position = _basePos + new Vector3(0f, Mathf.Sin(t * bobSpeed) * bobAmplitude, 0f);

            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            Vector3 mw = _cam.ScreenToWorldPoint(Input.mousePosition);
            mw.z = transform.position.z;
            bool inside = _col != null && _col.OverlapPoint(mw);
            if (inside != _hovering)
            {
                _hovering = inside;
                if (_tooltipRoot != null) _tooltipRoot.SetActive(_hovering);
            }

            if (_hovering && Input.GetMouseButtonDown(0))
            {
                InventoryPopupWindow.Toggle();
            }
        }

        void BuildTooltip()
        {
            if (string.IsNullOrEmpty(tooltipText)) return;
            _tooltipRoot = new GameObject("Tooltip");
            _tooltipRoot.transform.SetParent(transform, false);
            _tooltipRoot.transform.localPosition = new Vector3(0f, 0.32f, -0.3f);

            float w = tooltipText.Length * 0.03f + 0.10f;
            var bgGO = new GameObject("Bg");
            bgGO.transform.SetParent(_tooltipRoot.transform, false);
            bgGO.transform.localScale = new Vector3(w, 0.07f, 1f);
            var bgSr = bgGO.AddComponent<SpriteRenderer>();
            bgSr.sprite = WhitePixel();
            bgSr.color = new Color(0f, 0f, 0f, 0.92f);
            bgSr.sortingOrder = 12;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(_tooltipRoot.transform, false);
            labelGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            var tmp = labelGO.AddComponent<TextMeshPro>();
            tmp.text = tooltipText;
            tmp.fontSize = 0.45f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            var mr = labelGO.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 14;
            _tooltipRoot.SetActive(false);
        }

        static Sprite _loadedInventoryIcon;
        static Sprite LoadInventoryIconSprite()
        {
            if (_loadedInventoryIcon != null) return _loadedInventoryIcon;
            // StreamingAssets (bundled with the build) so the icon loads on any machine —
            // Application.dataPath/Itens only exists in the Editor, not in a player build.
            var path = Path.Combine(Application.streamingAssetsPath, "Icons", "ItemMall-Icon(Static).png");
            if (!File.Exists(path))
            {
                return null;
            }

            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            if (!tex.LoadImage(bytes))
            {
                Object.Destroy(tex);
                return null;
            }

            _loadedInventoryIcon = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return _loadedInventoryIcon;
        }

        static Sprite _fallback;
        static Sprite MakeFallbackIconSprite()
        {
            if (_fallback != null) return _fallback;
            const int W = 32, H = 32;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    t.SetPixel(x, y, Color.clear);

            var border = new Color(0.30f, 0.22f, 0.08f, 1f);
            var fill = new Color(0.94f, 0.85f, 0.58f, 1f);
            for (int y = 7; y <= 24; y++)
                for (int x = 6; x <= 25; x++)
                    t.SetPixel(x, y, fill);
            for (int x = 6; x <= 25; x++) { t.SetPixel(x, 7, border); t.SetPixel(x, 24, border); }
            for (int y = 7; y <= 24; y++) { t.SetPixel(6, y, border); t.SetPixel(25, y, border); }
            for (int y = 10; y <= 13; y++)
                for (int x = 9; x <= 22; x++)
                    t.SetPixel(x, y, border);
            t.Apply();

            _fallback = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            return _fallback;
        }

        static Sprite _whitePixel;
        static Sprite WhitePixel()
        {
            if (_whitePixel != null) return _whitePixel;
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.SetPixel(0, 0, Color.white);
            t.Apply();
            _whitePixel = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _whitePixel;
        }
    }
}
