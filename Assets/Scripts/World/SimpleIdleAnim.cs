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

        SpriteRenderer _sr;

        void Awake() { _sr = GetComponent<SpriteRenderer>(); }

        void Update()
        {
            if (frames == null || frames.Length == 0 || _sr == null) return;
            int i = (int)(Time.time * fps) % frames.Length;
            _sr.sprite = frames[i];
        }
    }
}
