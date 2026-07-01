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
                    break;
                default:
                    _frames = warriorIdleFrames;
                    label = "Mestre Guerreiro";
                    break;
            }
            if (_frames != null && _frames.Length > 0) _sr.sprite = _frames[0];
            _sr.color = tint;
            // Gravity + non-trigger foot collider settles the master onto the shared
            // GroundFloor at the same height as Kael (whose scene-authored Y already
            // matches the physics rest point). No manual snap needed.
            EnsurePhysicsFoot();
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

        // Attach a Dynamic rigidbody with gravity + a non-trigger foot collider so the
        // master falls onto GroundFloor and rests at Kael's height. A separate trigger
        // collider (already on the GO) is kept for Interactable2D hover/click detection.
        void EnsurePhysicsFoot()
        {
            var rb = GetComponent<Rigidbody2D>() ?? gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType    = RigidbodyType2D.Dynamic;
            rb.gravityScale = 3f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;

            // Reuse the authored trigger collider dimensions for the physics foot
            // (same size/offset Kael uses → same rest height).
            var trig = GetComponent<BoxCollider2D>();
            var foot = gameObject.AddComponent<BoxCollider2D>();
            foot.isTrigger = false;
            foot.size   = trig != null ? trig.size   : new Vector2(0.3f, 0.2f);
            foot.offset = trig != null ? trig.offset : new Vector2(0f,   0.5f);
        }
    }
}
