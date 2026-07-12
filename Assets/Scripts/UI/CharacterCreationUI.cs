using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // "Seleção de Herói": the three classes stand around the campfire. Hovering a hero previews its
    // name + description and plays a little attack loop; CLICKING a hero starts the game right away.
    // There is no name field any more — the character reuses the player's Steam persona name.
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

        void Start()
        {
            _idle   = new[] { mageIdleFrames,   warriorIdleFrames,   archerIdleFrames };
            _attack = new[] { mageAttackFrames, warriorAttackFrames, archerAttackFrames };

            int n = classButtons != null ? Mathf.Min(classButtons.Length, 3) : 0;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                if (classButtons[i] == null) continue;
                classButtons[i].onClick.AddListener(() => StartAs((ClassType)idx));   // click = start
                AddHover(classButtons[i].gameObject, () => Preview(idx));             // hover = preview
                ShowIdle(idx, 0);
                SetBorder(idx, false);
            }
            Preview(1);   // Warrior previewed by default
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
                SetBorder(i, i == idx);
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
