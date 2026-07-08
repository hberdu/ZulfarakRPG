using UnityEngine;

namespace ZulfarakRPG
{
    // City NPC whose sprite + tooltip is chosen at runtime to match the player's class.
    // Each master is recoloured (white hair + class-distinct robes via NPCRecolor) so
    // they read as elders clearly different from the player, and share the city slot.
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
            // Every master is a WHITE-HAIRED elder whose robes are a distinct colour
            // from the player's outfit (NPCRecolor bleaches the hair and hue-shifts the
            // clothes). clothesHue is the master's robe hue; tintFallback is used only
            // when the source textures aren't CPU-readable (flat multiply, can't whiten).
            float clothesHue;
            Color tintFallback;
            Sprite[] source;
            switch (classType)
            {
                case ClassType.Mage:
                    source = mageIdleFrames;
                    label = "Mestre dos Magos";
                    clothesHue   = 0.52f;                              // teal robes
                    tintFallback = new Color(0.45f, 0.75f, 0.80f, 1f);
                    break;
                case ClassType.Archer:
                    source = archerIdleFrames;
                    label = "Mestre Arqueiro";
                    clothesHue   = 0.33f;                              // forest-green garb
                    tintFallback = new Color(0.45f, 0.72f, 0.42f, 1f);
                    break;
                default:
                    source = warriorIdleFrames;
                    label = "Mestre Guerreiro";
                    clothesHue   = 0.02f;                              // crimson armour
                    tintFallback = new Color(0.80f, 0.42f, 0.42f, 1f);
                    break;
            }

            // Bake the white-hair + recoloured-clothes frames. If the swap couldn't read
            // the textures it returns the originals unchanged → apply the flat tint so the
            // master is at least a different colour from the player.
            _frames = NPCRecolor.Recolor(source, clothesHue);
            // Recolor returns a fresh array but leaves individual frames untouched when a
            // texture can't be read — so success is when the baked frame differs from the
            // source sprite. Only then is the sprite already coloured (no tint needed).
            bool recolored = _frames != null && source != null && _frames.Length > 0
                             && source.Length > 0 && _frames[0] != source[0];
            _sr.color = recolored ? Color.white : tintFallback;
            if (_frames != null && _frames.Length > 0) _sr.sprite = _frames[0];
            // Static at its authored Y (Kael's ground line) with only its trigger collider,
            // so it rests correctly AND doesn't physically block the player from walking past
            // to the portal. (Interactable2D uses OverlapPoint — no Rigidbody needed.)
            GetComponent<Interactable2D>().tooltipText = label;
            GetComponent<Interactable2D>().onClick = () => SkillTreePopup.Show();
            // No floating name tag — the name shows on hover (Interactable2D tooltip).
        }

        void Update()
        {
            if (_frames == null || _frames.Length == 0 || _sr == null) return;
            _i = (int)(Time.time * 6f) % _frames.Length;
            _sr.sprite = _frames[_i];
        }
    }
}
