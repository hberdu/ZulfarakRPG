using UnityEngine;

namespace ZulfarakRPG
{
    // City NPC whose sprite + tooltip is chosen at runtime to match the player's class.
    // All masters wear "roupas pretas" (black tint) and live in the same city slot.
    [RequireComponent(typeof(SpriteRenderer), typeof(Interactable2D))]
    public class ClassMasterNPC : MonoBehaviour
    {
        public Sprite[] warriorIdleFrames;
        public Sprite[] mageIdleFrames;
        public Sprite[] archerIdleFrames;

        SpriteRenderer _sr;
        Sprite[]       _frames;
        int            _i;

        void Start()
        {
            _sr = GetComponent<SpriteRenderer>();
            var classType = PlayerManager.Instance?.Data?.classType ?? ClassType.Warrior;
            string label;
            // Class-specific tint — the mage sprite has a vivid purple cloak that
            // pokes through the default cool-grey tint, so darken it further to
            // sit at the same visual weight as the archer/blacksmith.
            Color tint = new Color(0.42f, 0.42f, 0.50f, 1f);
            switch (classType)
            {
                case ClassType.Mage:
                    _frames = mageIdleFrames;
                    label = "Mestre dos Magos";
                    tint  = new Color(0.32f, 0.30f, 0.36f, 1f);
                    break;
                case ClassType.Archer:
                    _frames = archerIdleFrames;
                    label = "Mestre Arqueiro";
                    // Sooty/leather tone matching the blacksmith.
                    tint  = new Color(0.62f, 0.50f, 0.38f, 1f);
                    break;
                default:
                    _frames = warriorIdleFrames;
                    label = "Mestre Guerreiro";
                    break;
            }
            if (_frames != null && _frames.Length > 0) _sr.sprite = _frames[0];
            _sr.color = tint;
            // Static at its authored Y (Kael's ground line) with only its trigger collider,
            // so it rests correctly AND doesn't physically block the player from walking past
            // to the portal. (Interactable2D uses OverlapPoint — no Rigidbody needed.)
            GetComponent<Interactable2D>().tooltipText = label;
            GetComponent<Interactable2D>().onClick = () =>
                NPCMenuUI.Show(label, "Árvore de habilidades em construção.\n\nEm breve você poderá investir pontos de talento aqui para evoluir sua classe.");

            // Small name tag above the head, matching the WorldHealthBar style.
            NameTag.Attach(_sr, label, yOffsetWorld: 0.62f);
        }

        void Update()
        {
            if (_frames == null || _frames.Length == 0 || _sr == null) return;
            _i = (int)(Time.time * 6f) % _frames.Length;
            _sr.sprite = _frames[_i];
        }
    }
}
