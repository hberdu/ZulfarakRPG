using System.Collections.Generic;
using UnityEngine;

namespace ZulfarakRPG
{
    // MU Online-style loot: when a monster dies, each item it dropped — and its gold — pops out
    // of the corpse, arcs down onto the ground line, rests just long enough to be seen, then
    // flies to the hero and is swept up. No label, no waiting for the player to walk over it.
    //
    // The loot is ALREADY in the bag by the time this spawns: the backend grants it when it
    // validates the kill (CombatService.RegisterMonsterKillAsync) and the client applies the
    // authoritative inventory snapshot that comes back. So this is purely the on-screen half of
    // the drop, and the auto-pickup can never lose anything.
    public class ItemDropWorld : MonoBehaviour
    {
        const float FallDuration     = 0.40f;   // corpse → ground arc
        const float RestOnGround     = 0.35f;   // beat on the floor so the drop is readable
        const float PickupDuration    = 0.30f;  // ground → hero sweep

        SpriteRenderer _icon;
        LootChest      _chest;    // where this drop is headed once it lifts off
        bool           _spin;     // gold coins rotate on their vertical axis
        Vector3        _from, _to, _pickupFrom;
        float          _t, _age;
        bool           _collecting;

        static readonly Color GoldColor = new Color(1f, 0.84f, 0.20f, 1f);

        // Gold rides the SAME flow as items — MU drops coins on the floor, it doesn't float a
        // number over the corpse. Its coin keeps the size it already had.
        public static void SpawnGold(Vector3 worldPos, long amount)
        {
            if (amount <= 0) return;
            Build(worldPos, GoldColor, null, spin: true, scale: 0.16f);
        }

        public static void Spawn(Vector3 worldPos, string itemId, int quantity)
        {
            if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0) return;

            var data = ItemDatabase.Instance != null ? ItemDatabase.Instance.Get(itemId) : null;
            Color color = data != null ? data.RarityColor : Color.white;
            // Items read noticeably bigger than the gold coin — the drop IS the announcement now
            // that there's no name tag under it.
            Build(worldPos, color, ResolveSprite(data), spin: false, scale: 0.44f);
        }

        static void Build(Vector3 worldPos, Color color, Sprite art, bool spin, float scale)
        {
            var go = new GameObject(spin ? "GoldDrop" : "ItemDrop");
            var drop = go.AddComponent<ItemDropWorld>();

            // Land just beside the corpse so a multi-drop kill doesn't stack into one pile.
            float groundY = GroundAlignUtil.FindGroundTopY();
            drop._from = worldPos + new Vector3(0f, 0.35f, -0.3f);
            drop._to   = new Vector3(worldPos.x + Random.Range(-0.55f, 0.55f), groundY + 0.06f, -0.3f);
            go.transform.position = drop._from;

            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(go.transform, false);
            iconGO.transform.localScale = Vector3.one * scale;
            drop._icon = iconGO.AddComponent<SpriteRenderer>();
            drop._icon.sprite       = art ?? (spin ? CoinSprite() : GemSprite());
            drop._icon.color        = art != null ? Color.white : color;
            drop._icon.sortingOrder = 18;
            drop._spin = spin;
        }

        void Update()
        {
            _age += Time.deltaTime;

            // Coins keep turning the whole time, like MU's spinning drops.
            if (_spin && _icon != null)
                _icon.transform.localRotation = Quaternion.Euler(0f, _age * 360f, 0f);

            if (_collecting) { TickCollect(); return; }

            // Arc out of the corpse and settle on the ground.
            if (_t < 1f)
            {
                _t = Mathf.Min(1f, _t + Time.deltaTime / FallDuration);
                float e = 1f - (1f - _t) * (1f - _t);                 // ease-out
                var p = Vector3.Lerp(_from, _to, e);
                p.y += Mathf.Sin(_t * Mathf.PI) * 0.28f;              // hop
                transform.position = p;
                return;
            }

            // Resting on the floor: a short readable beat, then it goes to the hero on its own.
            transform.position = _to + new Vector3(0f, Mathf.Sin(Time.time * 2.6f) * 0.025f, 0f);
            if (_age >= FallDuration + RestOnGround) Collect();
        }

        // Sweep the drop into the chest. Nothing is granted here — the bag already holds it — so
        // this is only the pickup flourish.
        public void Collect()
        {
            if (_collecting) return;
            _collecting = true;
            _t = 0f;
            _pickupFrom = transform.position;
            // Summon (or reuse) the chest on the HUD rail and tell it something is on the way, so
            // it throws its lid open before this drop actually gets there.
            _chest = LootChest.Ensure();
            if (_chest != null) _chest.NotifyIncoming();
        }

        void TickCollect()
        {
            _t += Time.deltaTime / PickupDuration;
            if (_t >= 1f)
            {
                if (_chest != null) _chest.NotifyReceived();   // last one in → the lid can shut
                Destroy(gameObject);
                return;
            }

            // Into the chest's mouth (or straight up if the chest is somehow gone).
            Vector3 target = _chest != null ? _chest.MouthWorldPos : _pickupFrom + Vector3.up * 0.5f;

            float e = _t * _t;                                   // ease-in: drifts off, then darts
            var p = Vector3.Lerp(_pickupFrom, target, e);
            p.y += Mathf.Sin(_t * Mathf.PI) * 0.22f;             // little arc over the lip
            transform.position = p;

            if (_icon != null)
            {
                var c = _icon.color; c.a = 1f - e * e; _icon.color = c;
                _icon.transform.localScale = Vector3.Lerp(_icon.transform.localScale,
                                                          _icon.transform.localScale * 0.6f,
                                                          Time.deltaTime * 6f);
            }
        }

        // ── Art ───────────────────────────────────────────────────────────────
        static readonly Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>();

        // The item's own Sprite if it has one, else its pack PNG loaded through IconLibrary
        // (the same art the inventory window blits), else null → the procedural gem.
        static Sprite ResolveSprite(ItemData data)
        {
            if (data == null) return null;
            if (data.icon != null) return data.icon;
            if (string.IsNullOrWhiteSpace(data.iconPath)) return null;
            if (_iconCache.TryGetValue(data.iconPath, out var cached)) return cached;

            Sprite sp = null;
            var tex = IconLibrary.Tex(data.iconPath);
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            _iconCache[data.iconPath] = sp;
            return sp;
        }

        // Gold coin — a filled disc with a darker rim, spun on its Y axis by Update.
        static Sprite _coin;
        static Sprite CoinSprite()
        {
            if (_coin != null) return _coin;
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 1f) { t.SetPixel(x, y, Color.clear); continue; }
                    float shade = d > 0.72f ? 0.70f : 1f;      // bevelled rim
                    t.SetPixel(x, y, new Color(shade, shade, shade, 1f));
                }
            t.Apply();
            _coin = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _coin;
        }

        // Small faceted gem used when an item has no icon art — tinted by rarity.
        static Sprite _gem;
        static Sprite GemSprite()
        {
            if (_gem != null) return _gem;
            const int N = 16;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    // Diamond mask with a lighter top-left facet.
                    float d = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                    if (d > c) { t.SetPixel(x, y, Color.clear); continue; }
                    float shade = (x < c && y > c) ? 1f : (d > c - 2f ? 0.55f : 0.82f);
                    t.SetPixel(x, y, new Color(shade, shade, shade, 1f));
                }
            t.Apply();
            _gem = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _gem;
        }
    }
}
