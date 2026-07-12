using System;
using TMPro;
using UnityEngine;

namespace ZulfarakRPG
{
    // Hover shows a black tooltip balloon above the object; click invokes onClick.
    // Used by chest + NPCs in the city. Mirrors Portal2D's tooltip pattern so
    // the city's interactive elements look consistent.
    [RequireComponent(typeof(Collider2D))]
    public class Interactable2D : MonoBehaviour
    {
        public string     tooltipText;
        public Action     onClick;
        public Vector2    tooltipOffset = new Vector2(0f, 0.30f);

        // Optional serializable popup. Action delegates can't be saved to the
        // scene, so NPCs placed at design-time use these string fields to drive
        // MenuPopupWindow without needing a per-NPC script.
        public string popupTitle;
        [TextArea(3, 8)] public string popupBody;

        GameObject _tooltipRoot;
        Collider2D _col;
        Camera     _cam;

        void Start()
        {
            _col = GetComponent<Collider2D>();
            _cam = Camera.main;
            if (!string.IsNullOrEmpty(tooltipText)) BuildTooltip();
        }

        void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            Vector3 mw = _cam.ScreenToWorldPoint(Input.mousePosition);
            mw.z = transform.position.z;

            bool hovering = _col != null && _col.OverlapPoint(mw);
            if (_tooltipRoot != null && _tooltipRoot.activeSelf != hovering)
                _tooltipRoot.SetActive(hovering);

            if (hovering && Input.GetMouseButtonDown(0))
            {
                onClick?.Invoke();
                if (!string.IsNullOrEmpty(popupTitle) || !string.IsNullOrEmpty(popupBody))
                    NPCMenuUI.Show(popupTitle ?? "", popupBody ?? "");
            }
        }

        void BuildTooltip()
        {
            _tooltipRoot = new GameObject("Tooltip");
            _tooltipRoot.transform.SetParent(transform, false);
            _tooltipRoot.transform.localPosition = new Vector3(tooltipOffset.x, tooltipOffset.y, -0.3f);

            float w = tooltipText.Length * 0.032f + 0.16f;
            const float h = 0.085f;

            // Drop shadow (offset, soft black) for depth.
            var shadow = MakePanel("Shadow", new Vector3(0.015f, -0.018f, 0.02f),
                                   new Vector3(w, h, 1f), new Color(0f, 0f, 0f, 0.35f), 10);

            // Grey border panel (slightly larger), then a dark inner panel on top.
            MakePanel("Border", new Vector3(0f, 0f, 0f),
                      new Vector3(w + 0.03f, h + 0.03f, 1f), new Color(0.45f, 0.45f, 0.50f, 1f), 11);
            MakePanel("Bg", new Vector3(0f, 0f, -0.05f),
                      new Vector3(w, h, 1f), new Color(0.06f, 0.05f, 0.04f, 0.96f), 12);

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(_tooltipRoot.transform, false);
            labelGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            var tmp = labelGO.AddComponent<TextMeshPro>();
            tmp.text      = tooltipText;
            tmp.fontSize  = 0.45f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = new Color(0.92f, 0.92f, 0.95f, 1f);   // neutral grey-white text
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableWordWrapping = false;
            var mr = labelGO.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 14;

            _tooltipRoot.SetActive(false);
        }

        SpriteRenderer MakePanel(string name, Vector3 localPos, Vector3 scale, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_tooltipRoot.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = WhitePixel();
            sr.color        = color;
            sr.sortingOrder = order;
            return sr;
        }

        static Sprite _white;
        static Sprite WhitePixel()
        {
            if (_white != null) return _white;
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.SetPixel(0, 0, Color.white);
            t.Apply();
            _white = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _white;
        }
    }
}
