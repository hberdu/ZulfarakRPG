using UnityEngine;

namespace ZulfarakRPG
{
    // Plays a sprite-sheet idle loop at a fixed FPS. Used by static city NPCs that
    // don't need full PlayerController2D state.
    [RequireComponent(typeof(SpriteRenderer))]
    public class SimpleIdleAnim : MonoBehaviour
    {
        public Sprite[] frames;
        public float    fps = 6f;
        // Optional floating name tag (same style/height as the other city NPCs).
        // Leave empty for NPCs that attach their own tag or need none.
        public string   nameLabel;

        SpriteRenderer _sr;

        void Awake() { _sr = GetComponent<SpriteRenderer>(); }

        void Start()
        {
            if (!string.IsNullOrEmpty(nameLabel) && _sr != null)
                NameTag.Attach(_sr, nameLabel, yOffsetWorld: 0.62f);
        }

        void Update()
        {
            if (frames == null || frames.Length == 0 || _sr == null) return;
            int i = (int)(Time.time * fps) % frames.Length;
            _sr.sprite = frames[i];
        }
    }
}
