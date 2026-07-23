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

            // Prefer a DECOUPLED, pixel-edited master sheet (Resources/Masters/*) — the master's
            // new gear (cloak/weapon) lives there so it never touches the idle art it otherwise
            // SHARES with the player. Used as-is (no recolor). Falls back to the shared inspector
            // frames + runtime recolor when no edited sheet exists for this class.
            string masterRes = classType == ClassType.Mage   ? "Masters/WizardMaster-Idle"
                             : classType == ClassType.Archer ? "Masters/ArcherMaster-Idle"
                             :                                  "Masters/SoldierMaster-Idle";
            _frames = LoadMasterSheet(masterRes);
            if (_frames != null)
            {
                _sr.color = Color.white;
            }
            else
            {
                _frames = NPCRecolor.Recolor(source, clothesHue);
                bool recolored = _frames != null && source != null && _frames.Length > 0
                                 && source.Length > 0 && _frames[0] != source[0];
                _sr.color = recolored ? Color.white : tintFallback;
            }
            if (_frames != null && _frames.Length > 0) _sr.sprite = _frames[0];
            // Static at its authored Y (Kael's ground line) with only its trigger collider,
            // so it rests correctly AND doesn't physically block the player from walking past
            // to the portal. (Interactable2D uses OverlapPoint — no Rigidbody needed.)
            GetComponent<Interactable2D>().tooltipText = label;
            GetComponent<Interactable2D>().onClick = () => SkillTreePopup.Show();
            // No floating name tag — the name shows on hover (Interactable2D tooltip).
        }

        // Loads a 600x100 pixel-edited master sheet from Resources as 6 bottom-centre frames
        // (pivot 0.5,0 — matches the authored idle sub-sprites so it seats at the same line).
        static Sprite[] LoadMasterSheet(string res)
        {
            var tex = Resources.Load<Texture2D>(res);
            if (tex == null) return null;
            const int fw = 100;
            int n = Mathf.Max(1, tex.width / fw);
            var a = new Sprite[n];
            for (int i = 0; i < n; i++)
                a[i] = Sprite.Create(tex, new Rect(i * fw, 0, fw, tex.height), new Vector2(0.5f, 0f), 100f);
            return a;
        }

        void Update()
        {
            if (_frames == null || _frames.Length == 0 || _sr == null) return;
            _i = (int)(Time.time * 6f) % _frames.Length;
            _sr.sprite = _frames[_i];
        }
    }
}
