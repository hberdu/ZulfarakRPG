using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace ZulfarakRPG
{
    // WoW-style party frame at the top-LEFT: one row per LOBBY member — a face portrait, the Steam
    // name, and an HP bar. Appears only when you're in a lobby with at least one other member; the
    // leader's row is gold-framed. Non-lobby players sharing the scene never appear here (and their
    // world HP bar/name are hidden — see RemotePlayer.RefreshLobbyVisibility).
    public class PartyFrameUI : MonoBehaviour
    {
        static PartyFrameUI _instance;
        RectTransform _root;
        readonly List<Row> _rows = new List<Row>();
        string _sig = "";

        class Row
        {
            public string steamId;
            public GameObject go;
            public Image portrait, hpFill;
            public TextMeshProUGUI name;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("PartyFrameUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PartyFrameUI>();
        }

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            gameObject.AddComponent<GraphicRaycaster>();   // needed for portrait drag-to-reorder

            var rootGO = new GameObject("PartyRoot", typeof(RectTransform));
            _root = rootGO.GetComponent<RectTransform>();
            _root.SetParent(transform, false);
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0f, 1f);   // top-left
            _root.anchoredPosition = new Vector2(6f, -6f);
        }

        const float RowH = 28f, RowW = 126f, Gap = 3f;

        void Update()
        {
            var lm = SteamLobbyManager.Instance;
            bool grouped = lm != null && lm.InLobby && lm.MemberSteamIds.Count >= 2;
            if (_root != null && _root.gameObject.activeSelf != grouped) _root.gameObject.SetActive(grouped);
            if (!grouped) return;

            // Rows follow the PARTY AGGRO ORDER (drag-sorted), not raw lobby order.
            var order = PartyOrder.Get();
            string sig = string.Join(",", order) + "|" + lm.LeaderSteamId;
            if (sig != _sig) { Rebuild(order, lm.LeaderSteamId); _sig = sig; }
            foreach (var row in _rows) UpdateRow(row);
        }

        void Rebuild(List<string> order, string leaderId)
        {
            foreach (var r in _rows) if (r.go != null) Destroy(r.go);
            _rows.Clear();
            for (int i = 0; i < order.Count; i++)
                _rows.Add(MakeRow(order[i], order[i] == leaderId, i));
        }

        Row MakeRow(string steamId, bool leader, int index)
        {
            var row = new Row { steamId = steamId };
            var rt = NewRect("PartyRow", _root, new Vector2(0, -index * (RowH + Gap)), new Vector2(RowW, RowH));
            row.go = rt.gameObject;

            AddImage(rt, "Bg", new Color(0.05f, 0.04f, 0.06f, 0.74f), Vector2.zero, new Vector2(RowW, RowH));

            float pf = RowH - 4f;
            AddImage(rt, "PortraitFrame", leader ? new Color(0.95f, 0.78f, 0.30f, 1f) : new Color(0.48f, 0.48f, 0.54f, 1f),
                     new Vector2(2, -2), new Vector2(pf, pf));
            AddImage(rt, "PortraitBg", new Color(0.03f, 0.03f, 0.04f, 1f), new Vector2(4, -4), new Vector2(pf - 4, pf - 4));
            row.portrait = AddImage(rt, "Portrait", Color.white, new Vector2(4, -4), new Vector2(pf - 4, pf - 4));
            row.portrait.sprite = null; row.portrait.preserveAspect = true;

            // Aggro-order number (1..N) badge on the portrait's top-left corner.
            AddImage(rt, "NumBg", new Color(0f, 0f, 0f, 0.72f), new Vector2(2, -2), new Vector2(10, 10))
                .raycastTarget = false;
            var numRT = NewRect("Num", rt, new Vector2(2, -2), new Vector2(10, 10));
            var num = numRT.gameObject.AddComponent<TextMeshProUGUI>();
            num.text = (index + 1).ToString();
            num.fontSize = 8; num.fontStyle = FontStyles.Bold;
            num.color = new Color(1f, 0.92f, 0.42f, 1f);
            num.alignment = TextAlignmentOptions.Center;
            num.raycastTarget = false;

            float textX = pf + 5f;
            var nameRT = NewRect("Name", rt, new Vector2(textX, -2), new Vector2(RowW - textX - 3, 12));
            row.name = nameRT.gameObject.AddComponent<TextMeshProUGUI>();
            row.name.fontSize = 10; row.name.fontStyle = FontStyles.Bold;
            row.name.color = leader ? new Color(1f, 0.9f, 0.55f) : Color.white;
            row.name.enableWordWrapping = false; row.name.overflowMode = TextOverflowModes.Ellipsis;
            row.name.alignment = TextAlignmentOptions.Left;

            // Thin minimalist HP bar sat just under the name.
            float barX = textX, barW = (RowW - textX - 4), barH = 4f, barY = -18f;
            AddImage(rt, "HpBg", new Color(0.14f, 0.03f, 0.03f, 1f), new Vector2(barX, barY), new Vector2(barW, barH));
            row.hpFill = AddImage(rt, "HpFill", new Color(0.28f, 0.82f, 0.30f, 1f), new Vector2(barX, barY), new Vector2(barW, barH));
            row.hpFill.type = Image.Type.Filled; row.hpFill.fillMethod = Image.FillMethod.Horizontal;
            row.hpFill.fillOrigin = 0; row.hpFill.fillAmount = 1f;

            // Drag the row to change its aggro-order slot.
            var drag = rt.gameObject.AddComponent<PartyRowDrag>();
            drag.steamId = steamId;
            drag.pitch   = RowH + Gap;

            UpdateRow(row);
            return row;
        }

        // Drag-to-reorder: while dragging, the row follows the pointer vertically; on drop it
        // snaps into the nearest slot and the party aggro order is updated + synced.
        class PartyRowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public string steamId;
            public float  pitch = 42f;   // RowH + Gap
            RectTransform _rt;

            void Awake() => _rt = (RectTransform)transform;

            public void OnBeginDrag(PointerEventData e)
                => transform.SetAsLastSibling();   // render on top of the other rows while dragging

            public void OnDrag(PointerEventData e)
            {
                var p = _rt.anchoredPosition;
                p.y += e.delta.y;               // ConstantPixelSize canvas → 1 UI unit = 1 screen px
                _rt.anchoredPosition = p;
            }

            public void OnEndDrag(PointerEventData e)
            {
                var order  = PartyOrder.Get();
                int oldIdx = order.IndexOf(steamId);
                if (oldIdx < 0) return;
                int newIdx = Mathf.Clamp(Mathf.RoundToInt(-_rt.anchoredPosition.y / Mathf.Max(1f, pitch)),
                                         0, order.Count - 1);
                if (newIdx != oldIdx)
                {
                    order.RemoveAt(oldIdx);
                    order.Insert(newIdx, steamId);
                    PartyOrder.Set(order);   // syncs to partners; the frame rebuilds next Update
                }
                // Restore this row's slot until the rebuild lands (avoids a 1-frame jump).
                var p = _rt.anchoredPosition; p.y = -oldIdx * pitch; _rt.anchoredPosition = p;
            }
        }

        void UpdateRow(Row row)
        {
            string myId = SteamIntegration.Instance?.SteamId;
            string nm; ClassType cls; float hp; Sprite portrait;

            if (row.steamId == myId)
            {
                var d  = PlayerManager.Instance != null ? PlayerManager.Instance.Data : null;
                var pc = Object.FindAnyObjectByType<PlayerController2D>();
                nm  = !string.IsNullOrEmpty(d?.playerName) ? d.playerName : (SteamIntegration.Instance?.SteamName ?? "Você");
                cls = d?.classType ?? ClassType.Warrior;
                hp  = pc != null ? pc.HealthFraction : 1f;
                portrait = pc != null ? (pc.PortraitSprite ?? pc.IdleSpriteForClass(cls)) : null;
            }
            else
            {
                var rp  = MultiplayerSync.Instance?.GetRemote(row.steamId);
                var bot = BotPlayer.Get(row.steamId);
                var pc  = Object.FindAnyObjectByType<PlayerController2D>();
                if (rp != null)
                {
                    nm = rp.PlayerName; cls = rp.ClassType; hp = rp.HpFraction;
                    portrait = rp.PortraitSprite ?? (pc != null ? pc.IdleSpriteForClass(cls) : null);
                }
                else if (bot != null)
                {
                    nm = bot.PlayerName; cls = bot.ClassType; hp = bot.HpFraction;
                    portrait = bot.PortraitSprite ?? (pc != null ? pc.IdleSpriteForClass(cls) : null);
                }
                else { nm = "Aliado"; cls = ClassType.Warrior; hp = 1f; portrait = pc != null ? pc.IdleSpriteForClass(cls) : null; }
            }

            if (row.name != null) row.name.text = nm;
            if (row.hpFill != null) row.hpFill.fillAmount = Mathf.Clamp01(hp);
            if (row.portrait != null)
            {
                var face = HeadPortrait(portrait);
                if (row.portrait.sprite != face) row.portrait.sprite = face;
                row.portrait.enabled = face != null;
            }
        }

        // ── Head-crop portrait (the character's face) ─────────────────────────
        static readonly Dictionary<Sprite, Sprite> _headCache = new Dictionary<Sprite, Sprite>();
        static Sprite HeadPortrait(Sprite src)
        {
            if (src == null) return null;
            if (_headCache.TryGetValue(src, out var cached)) return cached;

            Sprite result = src;
            var tex = src.texture;
            if (tex != null && tex.isReadable)
            {
                var ab  = SpriteAlphaBounds.Get(src);
                float ppu = Mathf.Max(1f, src.pixelsPerUnit);
                var r = src.textureRect;
                float topPx = ab.topFromBottom * ppu, botPx = ab.bottomFromBottom * ppu;
                float visH  = topPx - botPx;
                if (visH > 6f)
                {
                    float headH = visH * 0.82f;                     // head + torso — show MORE of the character
                    float headW = Mathf.Min(ab.width * ppu * 1.25f, headH);
                    float cx    = ab.centerXFromLeft * ppu;
                    float x = Mathf.Clamp(r.x + cx - headW * 0.5f, r.x, r.x + r.width - 1f);
                    float y = Mathf.Clamp(r.y + topPx - headH,     r.y, r.y + r.height - 1f);
                    float w = Mathf.Min(headW, r.x + r.width  - x);
                    float h = Mathf.Min(headH, r.y + r.height - y);
                    if (w > 2f && h > 2f)
                        result = Sprite.Create(tex, new Rect(x, y, w, h), new Vector2(0.5f, 0.5f), ppu);
                }
            }
            _headCache[src] = result;
            return result;
        }

        // ── UI helpers ────────────────────────────────────────────────────────
        static RectTransform NewRect(string name, Transform parent, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return rt;
        }

        static Image AddImage(Transform parent, string name, Color color, Vector2 anchoredPos, Vector2 size)
        {
            var rt = NewRect(name, parent, anchoredPos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = WhiteSprite();
            img.color  = color;
            return img;
        }

        static Sprite _white;
        static Sprite WhiteSprite()
        {
            if (_white != null) return _white;
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            t.SetPixel(0, 0, Color.white); t.Apply();
            _white = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _white;
        }
    }
}
