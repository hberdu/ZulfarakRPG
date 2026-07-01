using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Small "Convidar amigo" icon shown in Zulfarak next to the world-map icon.
    // Hover shows a tooltip; clicking pops the Steam friend-invite overlay
    // scoped to the player's current lobby (creating one on demand). Auto-
    // spawns at scene load so no scene editing is required.
    public class InviteFriendIcon : MonoBehaviour
    {
        public float  bobAmplitude = 0.05f;
        public float  bobSpeed     = 1.5f;
        public string tooltipText  = "Convidar amigo da Steam";

        // ── Auto-spawn ────────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Zulfarak") return;
            if (Object.FindAnyObjectByType<InviteFriendIcon>() != null) return;
            // Sit just to the right of the WorldMapIcon (0.65, 0.55) so the
            // two "system" buttons line up in the upper-left HUD area.
            SpawnAt(new Vector3(1.05f, 0.55f, 0f));
        }

        public static InviteFriendIcon SpawnAt(Vector3 worldPos)
        {
            var go = new GameObject("InviteFriendIcon");
            go.transform.position   = worldPos;
            go.transform.localScale = Vector3.one * 0.55f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeIconSprite();
            sr.sortingOrder = 11;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius    = 0.30f;
            return go.AddComponent<InviteFriendIcon>();
        }

        // ── Runtime ────────────────────────────────────────────────────────
        SpriteRenderer  _sr;
        CircleCollider2D _col;
        Camera           _cam;
        Vector3          _basePos;
        GameObject       _tooltipRoot;
        bool             _hovering;

        void Awake()
        {
            _sr      = GetComponent<SpriteRenderer>();
            _col     = GetComponent<CircleCollider2D>();
            _basePos = transform.position;
            BuildTooltip();
        }

        void Update()
        {
            float t = Time.time;
            transform.position = _basePos + new Vector3(0f,
                Mathf.Sin(t * bobSpeed) * bobAmplitude, 0f);

            if (WorldMapPanel.IsOpen)
            {
                if (_hovering)
                {
                    _hovering = false;
                    if (_tooltipRoot != null) _tooltipRoot.SetActive(false);
                }
                return;
            }

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
                SteamLobbyManager.Instance?.EnsureLobby();
                FriendsListPopup.Show();
            }
        }

        void BuildTooltip()
        {
            if (string.IsNullOrEmpty(tooltipText)) return;
            _tooltipRoot = new GameObject("Tooltip");
            _tooltipRoot.transform.SetParent(transform, false);
            _tooltipRoot.transform.localPosition = new Vector3(0f, 0.32f, -0.3f);

            float w = tooltipText.Length * 0.030f + 0.10f;
            var bgGO = new GameObject("Bg");
            bgGO.transform.SetParent(_tooltipRoot.transform, false);
            bgGO.transform.localScale = new Vector3(w, 0.07f, 1f);
            var bgSr = bgGO.AddComponent<SpriteRenderer>();
            bgSr.sprite       = WhitePixel();
            bgSr.color        = new Color(0f, 0f, 0f, 0.92f);
            bgSr.sortingOrder = 12;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(_tooltipRoot.transform, false);
            labelGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            var tmp = labelGO.AddComponent<TextMeshPro>();
            tmp.text      = tooltipText;
            tmp.fontSize  = 0.45f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            var mr = labelGO.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 14;
            _tooltipRoot.SetActive(false);
        }

        // ── Procedural sprite ─────────────────────────────────────────────
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

        // Pixel-art "envelope + plus" icon — instantly reads as "invite".
        static Sprite _iconSprite;
        static Sprite MakeIconSprite()
        {
            if (_iconSprite != null) return _iconSprite;
            const int W = 36, H = 32;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    t.SetPixel(x, y, Color.clear);

            var paper   = new Color(0.94f, 0.92f, 0.82f, 1f);
            var paperDk = new Color(0.78f, 0.74f, 0.60f, 1f);
            var border  = new Color(0.30f, 0.22f, 0.08f, 1f);
            var fold    = new Color(0.60f, 0.50f, 0.30f, 1f);
            var plus    = new Color(0.20f, 0.82f, 0.40f, 1f);
            var plusDk  = new Color(0.10f, 0.48f, 0.22f, 1f);

            // Envelope body (rectangle 2..25, 5..24)
            for (int y = 5; y <= 24; y++)
                for (int x = 2; x <= 25; x++)
                    t.SetPixel(x, y, paper);
            // Border
            for (int x = 2; x <= 25; x++) { t.SetPixel(x, 5, border); t.SetPixel(x, 24, border); }
            for (int y = 5; y <= 24; y++) { t.SetPixel(2, y, border); t.SetPixel(25, y, border); }
            // Envelope flap (V shape)
            for (int i = 0; i <= 11; i++)
            {
                t.SetPixel(3 + i, 23 - i, fold);
                t.SetPixel(25 - i, 23 - i, fold);
            }
            // Paper shading at bottom
            for (int x = 3; x <= 24; x++) t.SetPixel(x, 6, paperDk);

            // "Plus" badge — bigger circle bottom-right
            void Disk(int cx, int cy, int r, Color c)
            {
                for (int y = -r; y <= r; y++)
                    for (int x = -r; x <= r; x++)
                        if (x * x + y * y <= r * r) t.SetPixel(cx + x, cy + y, c);
            }
            // Badge at (29, 7): outer ring r=6, inner r=5
            Disk(29, 7, 6, plusDk);
            Disk(29, 7, 5, plus);
            // Plus glyph — 3-pixel thick arms
            for (int d = -2; d <= 2; d++) { t.SetPixel(29, 7 + d, paper); t.SetPixel(29 + d, 7, paper); }

            t.Apply();
            _iconSprite = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            return _iconSprite;
        }
    }
}
