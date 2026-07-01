using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Bottom-left HUD: the character portrait frame ("face") from the GandalfHardcore
    // HP-bar art, followed by four square action buttons of the same dimension.
    // The old round HP orb (red liquid meter) is removed — health is tracked via the
    // world-space bar over the character's head, not here.
    public class PlayerHud : MonoBehaviour
    {
        static PlayerHud _instance;
        Canvas _canvas;

        const float SIZE   = 40f;   // square edge of face + each button
        const float MARGIN = 8f;    // gap from screen edge
        const float GAP    = 4f;    // gap between squares

        // Four action-button labels + their tap handlers (placeholder popups for now).
        static readonly (string label, string title, string body)[] Buttons = new []
        {
            ("Inv",  "Inventário",  "Inventário em construção.\n\nEm breve você poderá gerenciar seus itens aqui."),
            ("Per",  "Personagem",  "Ficha do personagem em construção.\n\nAtributos, equipamentos e progresso ficarão aqui."),
            ("Hab",  "Habilidades", "Árvore de habilidades em construção.\n\nInvista pontos de talento para evoluir sua classe."),
            ("Mis",  "Missões",     "Quadro de missões em construção.\n\nAceite tarefas e acompanhe recompensas aqui."),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool gameplay = scene.name == "Zulfarak" || scene.name == "Dungeon";
            if (_instance == null && gameplay) Build();
            if (_instance != null) _instance._canvas.enabled = gameplay;
        }

        static void Build()
        {
            var root = new GameObject("PlayerHud");
            DontDestroyOnLoad(root);
            _instance = root.AddComponent<PlayerHud>();

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;                         // below native popups (800)
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            root.AddComponent<GraphicRaycaster>();
            _instance._canvas = canvas;

            // Bar container: one row = face + four square buttons.
            var bar = new GameObject("HudBar", typeof(RectTransform));
            bar.transform.SetParent(canvas.transform, false);
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0f, 0f);
            brt.anchoredPosition = new Vector2(MARGIN, MARGIN);
            brt.sizeDelta        = new Vector2(SIZE * 5 + GAP * 4, SIZE);

            // Face square (the character-portrait frame, no HP orb inside).
            BuildFace(brt, 0f);

            // Four square action buttons to the RIGHT of the face.
            for (int i = 0; i < Buttons.Length; i++)
            {
                float x = (i + 1) * (SIZE + GAP);
                var (label, title, body) = Buttons[i];
                BuildButton(brt, x, label, title, body);
            }
        }

        static void BuildFace(Transform parent, float xOffset)
        {
            var go = new GameObject("Face", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(xOffset, 0f);
            rt.sizeDelta        = new Vector2(SIZE, SIZE);

            // Dark backing so the frame has a solid base regardless of desktop behind.
            var bg = go.AddComponent<Image>();
            bg.color         = new Color(0.08f, 0.06f, 0.05f, 0.95f);
            bg.raycastTarget = false;

            // Portrait frame sprite (left 64×64 of the HpBarFrame — the ring / face plate).
            var frame = RingSprite();
            if (frame != null)
            {
                var fg = new GameObject("Frame", typeof(RectTransform));
                fg.transform.SetParent(go.transform, false);
                var frt = (RectTransform)fg.transform;
                frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
                frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
                var img = fg.AddComponent<Image>();
                img.sprite        = frame;
                img.color         = Color.white;
                img.raycastTarget = false;
            }
        }

        static void BuildButton(Transform parent, float xOffset, string label, string title, string body)
        {
            var go = new GameObject($"Btn_{label}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(xOffset, 0f);
            rt.sizeDelta        = new Vector2(SIZE, SIZE);

            // Gold border + dark inner panel — matches the tooltip theme.
            var border = go.AddComponent<Image>();
            border.color         = new Color(0.85f, 0.65f, 0.20f, 1f);
            border.raycastTarget = true;

            var innerGO = new GameObject("Inner", typeof(RectTransform));
            innerGO.transform.SetParent(go.transform, false);
            var irt = (RectTransform)innerGO.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2( 2f,  2f);
            irt.offsetMax = new Vector2(-2f, -2f);
            var inner = innerGO.AddComponent<Image>();
            inner.color         = new Color(0.10f, 0.08f, 0.06f, 0.95f);
            inner.raycastTarget = false;

            // Centered label (3-letter cue) so we don't depend on TMP font assets here.
            var txtGO = new GameObject("Label", typeof(RectTransform));
            txtGO.transform.SetParent(innerGO.transform, false);
            var trt = (RectTransform)txtGO.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var txt = txtGO.AddComponent<Text>();
            txt.text     = label;
            txt.font     = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 14;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color    = new Color(1f, 0.93f, 0.72f, 1f);
            txt.raycastTarget = false;

            // Click handler: opens a themed popup as a placeholder for each system.
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = border;
            btn.onClick.AddListener(() => NPCMenuUI.Show(title, body));
        }

        // Frame is 116×64: the portrait ring occupies the left 64px. Cropping to that
        // square isolates the "face" plate without the meter housing beside it.
        static Sprite _ring;
        static Sprite RingSprite()
        {
            if (_ring != null) return _ring;
            var tex = Resources.Load<Texture2D>("HpBarFrame");
            if (tex == null) return null;
            _ring = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
            return _ring;
        }
    }
}
