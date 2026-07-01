using UnityEngine;
using UnityEngine.UI;

namespace ZulfarakRPG
{
    // Plays frame-by-frame pixel art animations on a UI Image.
    // Sprites must be an array of frames in order.
    [RequireComponent(typeof(Image))]
    public class PixelSpriteAnimator : MonoBehaviour
    {
        [System.Serializable]
        public class AnimClip
        {
            public string    name;
            public Sprite[]  frames;
            public float     fps    = 8f;
            public bool      loop   = true;
        }

        public AnimClip[] clips;
        public string     defaultClip = "Idle";

        private Image    _image;
        private AnimClip _current;
        private int      _frame;
        private float    _timer;

        void Awake() => _image = GetComponent<Image>();

        void Start() => Play(defaultClip);

        void Update()
        {
            if (_current == null || _current.frames == null || _current.frames.Length == 0) return;

            _timer += Time.deltaTime;
            float interval = 1f / Mathf.Max(1f, _current.fps);

            if (_timer >= interval)
            {
                _timer -= interval;
                _frame++;

                if (_frame >= _current.frames.Length)
                {
                    if (_current.loop)
                        _frame = 0;
                    else
                    {
                        _frame = _current.frames.Length - 1;
                        return;
                    }
                }

                if (_current.frames[_frame] != null)
                    _image.sprite = _current.frames[_frame];
            }
        }

        public void Play(string clipName)
        {
            if (_current != null && _current.name == clipName) return;

            foreach (var c in clips)
            {
                if (c.name != clipName) continue;
                _current = c;
                _frame   = 0;
                _timer   = 0f;
                if (_image != null && c.frames.Length > 0 && c.frames[0] != null)
                    _image.sprite = c.frames[0];
                return;
            }
        }

        // Play once then revert to Idle
        public void PlayOnce(string clipName, string returnClip = "Idle")
        {
            StartCoroutine(PlayOnceRoutine(clipName, returnClip));
        }

        private System.Collections.IEnumerator PlayOnceRoutine(string clipName, string returnClip)
        {
            AnimClip clip = null;
            foreach (var c in clips) if (c.name == clipName) { clip = c; break; }
            if (clip == null) yield break;

            bool wasLoop = clip.loop;
            clip.loop = false;
            Play(clipName);

            float duration = clip.frames.Length / Mathf.Max(1f, clip.fps);
            yield return new WaitForSeconds(duration);

            clip.loop = wasLoop;
            Play(returnClip);
        }
    }
}
