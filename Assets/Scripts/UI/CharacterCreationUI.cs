using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    public class CharacterCreationUI : MonoBehaviour
    {
        [Header("Class Cards (Mage / Warrior / Archer)")]
        public Button[]            classButtons;
        public Image[]             classArtImages;
        public Image[]             classSelectionBorders;
        public TextMeshProUGUI[]   classNameTexts;
        public TextMeshProUGUI[]   classDescTexts;

        [Header("Name Input")]
        public TMP_InputField  nameInput;
        public Button          confirmButton;
        public TextMeshProUGUI confirmErrorText;

        [Header("Portrait Animation — Idle Frames per Class")]
        public Sprite[] mageIdleFrames;
        public Sprite[] warriorIdleFrames;
        public Sprite[] archerIdleFrames;

        [Header("Portrait Animation — Attack Frames per Class")]
        public Sprite[] mageAttackFrames;
        public Sprite[] warriorAttackFrames;
        public Sprite[] archerAttackFrames;

        // Selected-border colors
        static readonly Color BorderSelected   = new Color(0.92f, 0.75f, 0.18f, 1f);
        static readonly Color BorderUnselected = new Color(0.15f, 0.10f, 0.05f, 0.35f);

        // Animation speed (seconds per frame)
        const float IdleFps   = 1f / 7f;
        const float AttackFps = 1f / 11f;

        private ClassType    _selected = ClassType.Warrior;
        private Coroutine[]  _anim     = new Coroutine[3];
        private Sprite[][]   _idle;
        private Sprite[][]   _attack;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            _idle   = new[] { mageIdleFrames,   warriorIdleFrames,   archerIdleFrames };
            _attack = new[] { mageAttackFrames,  warriorAttackFrames, archerAttackFrames };

            for (int i = 0; i < classButtons.Length; i++)
            {
                int idx = i;
                classButtons[i]?.onClick.AddListener(() => SelectClass((ClassType)idx));
            }

            confirmButton?.onClick.AddListener(Confirm);

            // Set initial idle frames
            for (int i = 0; i < 3; i++)
                ShowIdle(i, frame: 0);

            SelectClass(ClassType.Warrior);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < 3; i++)
                if (_anim[i] != null) StopCoroutine(_anim[i]);
        }

        // ── Class selection ───────────────────────────────────────────────────

        private void SelectClass(ClassType type)
        {
            _selected = type;

            for (int i = 0; i < 3; i++)
            {
                bool isSelected = ((int)type == i);

                // Border highlight
                if (classSelectionBorders.Length > i && classSelectionBorders[i] != null)
                    classSelectionBorders[i].color = isSelected ? BorderSelected : BorderUnselected;

                // Stop previous animation
                if (_anim[i] != null) { StopCoroutine(_anim[i]); _anim[i] = null; }

                if (isSelected)
                    _anim[i] = StartCoroutine(AnimateSelected(i));
                else
                    ShowIdle(i, frame: 0);
            }
        }

        // ── Coroutines ────────────────────────────────────────────────────────

        private IEnumerator AnimateSelected(int idx)
        {
            var img     = (classArtImages.Length > idx) ? classArtImages[idx] : null;
            var idle    = _idle?[idx];
            var attack  = _attack?[idx];

            // Brief idle cycle before attack
            if (idle != null && idle.Length > 0)
                foreach (var f in idle)
                {
                    if (img) img.sprite = f;
                    yield return new WaitForSeconds(IdleFps);
                }

            while (true)
            {
                // Attack burst
                if (attack != null && attack.Length > 0)
                {
                    foreach (var f in attack)
                    {
                        if (img) img.sprite = f;
                        yield return new WaitForSeconds(AttackFps);
                    }
                    yield return new WaitForSeconds(0.35f);
                }

                // Recovery idle
                if (idle != null && idle.Length > 0)
                    foreach (var f in idle)
                    {
                        if (img) img.sprite = f;
                        yield return new WaitForSeconds(IdleFps);
                    }
            }
        }

        private void ShowIdle(int idx, int frame)
        {
            if (classArtImages.Length <= idx || classArtImages[idx] == null) return;
            var frames = _idle?[idx];
            if (frames != null && frames.Length > frame)
                classArtImages[idx].sprite = frames[frame];
        }

        // ── Confirm ───────────────────────────────────────────────────────────

        private void Confirm()
        {
            if (confirmErrorText) confirmErrorText.text = "";

            string charName = nameInput != null ? nameInput.text.Trim() : "";
            if (string.IsNullOrWhiteSpace(charName))
            {
                if (confirmErrorText) confirmErrorText.text = "Digite um nome para o personagem.";
                return;
            }

            SubclassType defaultSub = _selected switch
            {
                ClassType.Mage    => SubclassType.FireMage,
                ClassType.Warrior => SubclassType.Berserker,
                ClassType.Archer  => SubclassType.Hunter,
                _                 => SubclassType.Berserker
            };

            var data = new PlayerData
            {
                steamId      = SteamIntegration.Instance != null ? SteamIntegration.Instance.SteamId : default,
                playerName   = charName,
                classType    = _selected,
                subclassType = defaultSub
            };

            PlayerManager.Instance?.CreateNewCharacter(data);
            SceneManager.LoadScene("Zulfarak");
        }
    }
}
