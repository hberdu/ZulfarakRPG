using TMPro;
using UnityEngine;

namespace ZulfarakRPG
{
    // Taskbar-Hero style cooldown row floating just above the hero: one small cell per
    // EQUIPPED skill (max 2). Each cell shows the skill's colour (violet = dano, verde =
    // cura) with a dark overlay that fills the cell while on cooldown and empties as it
    // becomes ready, plus the remaining seconds. Follows the player in world space.
    public class SkillCooldownHUD : MonoBehaviour
    {
        const float CellSize = 0.30f;   // world units
        const float Gap      = 0.08f;
        const float HeightAboveHead = 0.22f;

        SkillAutoCaster _caster;
        SpriteRenderer  _playerSr;
        Cell[] _cells;

        class Cell
        {
            public GameObject root;
            public SpriteRenderer baseSr;
            public SpriteRenderer overlaySr;
            public TextMeshPro label;
        }

        public static void Attach(SkillAutoCaster caster)
        {
            if (caster == null) return;
            var go = new GameObject("SkillCooldownHUD");
            var hud = go.AddComponent<SkillCooldownHUD>();
            hud._caster   = caster;
            hud._playerSr = caster.GetComponent<SpriteRenderer>();
            hud.Build();
        }

        void Build()
        {
            _cells = new Cell[SkillManager.MaxEquipped];
            for (int i = 0; i < _cells.Length; i++)
            {
                var root = new GameObject($"Cell{i}");
                root.transform.SetParent(transform, false);

                MakeSquare(root.transform, "Outline", new Color(0f, 0f, 0f, 0.85f), CellSize + 0.05f, 30);
                var baseSr = MakeSquare(root.transform, "Base", Color.white, CellSize, 31);
                var overlay = MakeSquare(root.transform, "Cooldown", new Color(0.02f, 0.02f, 0.04f, 0.66f), CellSize, 32);

                var lblGo = new GameObject("Sec");
                lblGo.transform.SetParent(root.transform, false);
                lblGo.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                var lbl = lblGo.AddComponent<TextMeshPro>();
                lbl.fontSize  = 1.3f;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.color     = Color.white;
                lbl.fontStyle = FontStyles.Bold;
                lbl.enableWordWrapping = false;
                var rt = lbl.rectTransform; rt.sizeDelta = new Vector2(CellSize * 4f, CellSize * 4f);
                if (GameFont.Tmp != null) lbl.font = GameFont.Tmp;
                var mat = lbl.fontMaterial;
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                lbl.UpdateMeshPadding();
                var mr = lblGo.GetComponent<MeshRenderer>(); if (mr != null) mr.sortingOrder = 33;

                _cells[i] = new Cell { root = root, baseSr = baseSr, overlaySr = overlay, label = lbl };
            }
        }

        void LateUpdate()
        {
            if (_caster == null || _playerSr == null) { HideAll(); return; }

            var active = _caster.Active;
            int n = Mathf.Min(active.Count, _cells.Length);
            if (n == 0) { HideAll(); return; }

            // Anchor above the visible sprite (robust to the small foot collider).
            Bounds b = _playerSr.bounds;
            float centerX = b.center.x;
            float topY    = b.max.y + HeightAboveHead;
            float rowW    = n * CellSize + (n - 1) * Gap;
            float startX  = centerX - rowW * 0.5f + CellSize * 0.5f;

            for (int i = 0; i < _cells.Length; i++)
            {
                var cell = _cells[i];
                if (i >= n) { cell.root.SetActive(false); continue; }
                cell.root.SetActive(true);

                var a = active[i];
                float x = startX + i * (CellSize + Gap);
                cell.root.transform.position = new Vector3(x, topY + CellSize * 0.5f, 0f);

                // Skill colour by element (matches the cast VFX).
                cell.baseSr.color = SkillAutoCaster.SkillFxColor(a.def);

                float frac = a.total > 0f ? Mathf.Clamp01(a.remaining / a.total) : 0f;
                // Cooldown overlay fills from the bottom while cooling, empties when ready.
                if (frac > 0.001f)
                {
                    cell.overlaySr.enabled = true;
                    var t = cell.overlaySr.transform;
                    t.localScale    = new Vector3(CellSize, CellSize * frac, 1f);
                    t.localPosition = new Vector3(0f, CellSize * (frac - 1f) * 0.5f, -0.02f);
                    cell.label.gameObject.SetActive(true);
                    cell.label.text = Mathf.CeilToInt(a.remaining).ToString();
                }
                else
                {
                    cell.overlaySr.enabled = false;
                    cell.label.gameObject.SetActive(false);
                }
            }
        }

        void HideAll()
        {
            if (_cells == null) return;
            foreach (var c in _cells) if (c != null && c.root != null) c.root.SetActive(false);
        }

        SpriteRenderer MakeSquare(Transform parent, string name, Color color, float size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, order == 30 ? 0.01f : 0f);
            go.transform.localScale    = new Vector3(size, size, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = Pixel();
            sr.color        = color;
            sr.sortingOrder = order;
            return sr;
        }

        static Sprite _pixel;
        static Sprite Pixel()
        {
            if (_pixel != null) return _pixel;
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            t.SetPixel(0, 0, Color.white); t.Apply();
            _pixel = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _pixel;
        }
    }
}
