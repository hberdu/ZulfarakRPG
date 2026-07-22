using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // "Seleção de Herói": the three classes stand around the campfire. Hovering a hero previews its
    // name + description and plays a little attack loop; CLICKING a hero selects it and reveals the
    // CRIAR PERSONAGEM button, which actually creates the character and starts the game.
    // There is no name field — the character reuses the player's Steam persona name, and each Steam
    // account holds a single character (GET/PUT /api/character/me).
    public class CharacterCreationUI : MonoBehaviour
    {
        [Header("Class figures (index 0=Mage, 1=Warrior, 2=Archer)")]
        public Button[]          classButtons;
        public Image[]           classArtImages;
        public Image[]           classSelectionBorders;   // optional highlight ring per class

        [Header("Shared info panel")]
        public TextMeshProUGUI   infoNameText;
        public TextMeshProUGUI   infoDescText;

        [Header("Idle / Attack frames per class")]
        public Sprite[] mageIdleFrames,   warriorIdleFrames,   archerIdleFrames;
        public Sprite[] mageAttackFrames, warriorAttackFrames, archerAttackFrames;

        static readonly string[] Names = { "Mago", "Guerreiro", "Arqueiro" };
        static readonly string[] Descs =
        {
            "Mestre arcano. Lança feitiços de fogo, gelo e raio à distância.",
            "Lutador corpo-a-corpo tanque, com forte defesa e escudo.",
            "Atirador ágil que elimina inimigos de longe com flechas.",
        };
        static readonly Color BorderOn  = new Color(0.95f, 0.78f, 0.25f, 1f);
        static readonly Color BorderOff = new Color(1f, 1f, 1f, 0f);
        const float IdleFps = 1f / 7f, AttackFps = 1f / 11f;

        Sprite[][]  _idle, _attack;
        Coroutine[] _anim = new Coroutine[3];
        int _preview = -1;
        int _selected = -1;
        GameObject _createButton;
        bool _creating;

        void Start()
        {
            _idle   = new[] { mageIdleFrames,   warriorIdleFrames,   archerIdleFrames };
            _attack = new[] { mageAttackFrames, warriorAttackFrames, archerAttackFrames };

            BuildBackdropArt();
            BuildCreateButton();

            int n = classButtons != null ? Mathf.Min(classButtons.Length, 3) : 0;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                if (classButtons[i] == null) continue;
                classButtons[i].onClick.AddListener(() => Select(idx));    // click = select
                AddHover(classButtons[i].gameObject, () => Preview(idx));  // hover = preview
                ShowIdle(idx, 0);
                SetBorder(idx, false);
            }
            Preview(1);   // Warrior previewed by default

            var sub = GameObject.Find("SubTitle");
            if (sub != null && sub.TryGetComponent(out TextMeshProUGUI subTmp))
                subTmp.text = "Selecione um herói e clique em CRIAR PERSONAGEM";
        }

        // Pixel-art night-clearing backdrop (Resources/UI/CampfireBg) slotted just above the flat
        // "Background" panel but under its vignette/stars, so the authored atmosphere still layers.
        void BuildBackdropArt()
        {
            var sprite = Resources.Load<Sprite>("UI/CampfireBg");
            if (sprite == null) return;
            var bg = GameObject.Find("Background");
            var parent = bg != null ? bg.transform : transform;
            var go = new GameObject("BackdropArt", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.SetAsFirstSibling();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
        }

        // "CRIAR PERSONAGEM" button (bottom-right, clear of the campfire) — hidden until a class
        // is selected. Built in code so the wizard-baked scene needs no rebuild.
        void BuildCreateButton()
        {
            var go = new GameObject("CreateButton", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = new Vector2(0.76f, 0.055f);
            rt.anchorMax = new Vector2(0.985f, 0.175f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.10f, 0.06f, 0.02f, 0.96f);

            // Thin gold frame behind the panel (slightly larger).
            var frame = new GameObject("Frame", typeof(RectTransform));
            var frt = (RectTransform)frame.transform;
            frt.SetParent(rt, false);
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(-2f, -2f); frt.offsetMax = new Vector2(2f, 2f);
            var fimg = frame.AddComponent<Image>();
            fimg.color = BorderOn;
            fimg.raycastTarget = false;
            frame.transform.SetAsFirstSibling();

            var label = new GameObject("Label", typeof(RectTransform));
            var lrt = (RectTransform)label.transform;
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var tmp = label.AddComponent<TextMeshProUGUI>();
            if (GameFont.Tmp != null) tmp.font = GameFont.Tmp;
            tmp.text = "CRIAR PERSONAGEM";
            tmp.fontSize = 13f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.97f, 0.88f, 0.50f);
            tmp.raycastTarget = false;

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                if (_selected >= 0 && !_creating) { _creating = true; StartAs((ClassType)_selected); }
            });

            _createButton = go;
            go.SetActive(false);
        }

        void Select(int idx)
        {
            _selected = idx;
            _preview  = -1;      // force Preview to re-apply borders/animation
            Preview(idx);
            if (_createButton != null) _createButton.SetActive(true);
        }

        void OnDestroy()
        {
            for (int i = 0; i < 3; i++) if (_anim[i] != null) StopCoroutine(_anim[i]);
        }

        static void AddHover(GameObject go, System.Action onEnter)
        {
            var trg = go.GetComponent<EventTrigger>();
            if (trg == null) trg = go.AddComponent<EventTrigger>();
            var e = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            e.callback.AddListener(_ => onEnter());
            trg.triggers.Add(e);
        }

        void Preview(int idx)
        {
            if (idx == _preview) return;
            _preview = idx;
            if (infoNameText) infoNameText.text = Names[idx];
            if (infoDescText) infoDescText.text = Descs[idx];
            for (int i = 0; i < 3; i++)
            {
                SetBorder(i, i == idx || i == _selected);   // selection ring survives hovers
                if (_anim[i] != null) { StopCoroutine(_anim[i]); _anim[i] = null; }
                if (i == idx) _anim[i] = StartCoroutine(AnimateSelected(i));
                else ShowIdle(i, 0);
            }
        }

        void SetBorder(int i, bool on)
        {
            if (classSelectionBorders != null && classSelectionBorders.Length > i && classSelectionBorders[i] != null)
                classSelectionBorders[i].color = on ? BorderOn : BorderOff;
        }

        // Create the character with the player's STEAM name (no manual name entry) and start.
        void StartAs(ClassType cls)
        {
            SubclassType sub = cls switch
            {
                ClassType.Mage    => SubclassType.FireMage,
                ClassType.Warrior => SubclassType.Berserker,
                ClassType.Archer  => SubclassType.Hunter,
                _                 => SubclassType.Berserker,
            };

            var steam = SteamIntegration.Instance;
            string steamName = steam != null ? steam.SteamName : null;
            if (string.IsNullOrWhiteSpace(steamName)) steamName = "Herói";

            var data = new PlayerData
            {
                steamId      = steam != null ? steam.SteamId : "",
                playerName   = steamName,          // reuse the Steam persona name
                classType    = cls,
                subclassType = sub,
            };
            PlayerManager.Instance?.CreateNewCharacter(data);
            SceneManager.LoadScene("Zulfarak");
        }

        IEnumerator AnimateSelected(int idx)
        {
            var img    = (classArtImages != null && classArtImages.Length > idx) ? classArtImages[idx] : null;
            var idle   = _idle?[idx];
            var attack = _attack?[idx];

            if (idle != null && idle.Length > 0)
                foreach (var f in idle) { if (img) img.sprite = f; yield return new WaitForSeconds(IdleFps); }

            while (true)
            {
                if (attack != null && attack.Length > 0)
                {
                    foreach (var f in attack) { if (img) img.sprite = f; yield return new WaitForSeconds(AttackFps); }
                    yield return new WaitForSeconds(0.35f);
                }
                if (idle != null && idle.Length > 0)
                    foreach (var f in idle) { if (img) img.sprite = f; yield return new WaitForSeconds(IdleFps); }
            }
        }

        void ShowIdle(int idx, int frame)
        {
            if (classArtImages == null || classArtImages.Length <= idx || classArtImages[idx] == null) return;
            var frames = _idle?[idx];
            if (frames != null && frames.Length > frame) classArtImages[idx].sprite = frames[frame];
        }
    }
}
