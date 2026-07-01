using System.Collections.Generic;
using UnityEngine;

namespace ZulfarakRPG
{
    // A horizontally-scrolling background layer with procedural item placement.
    // WaveManager calls Scroll(dx) during the run-to-next-wave transition.
    // Items that drift past the left despawn edge are destroyed; new items are
    // spawned to the right of the rightmost existing item to keep the screen full.
    public class ParallaxLayer : MonoBehaviour
    {
        [Header("Parallax")]
        // 1.0 = scrolls at full requested speed (close layer).
        // 0.4 = scrolls slower (far background).
        public float speedFactor = 1.0f;

        [Header("Pool")]
        public Sprite[] sprites;
        public Color    tint      = Color.white;
        public float    minScale  = 0.30f;
        public float    maxScale  = 0.60f;
        public int      sortOrder = -8;

        [Header("Spawn")]
        public float minSpacing = 1.5f;
        public float maxSpacing = 3.0f;
        // Ground Y where BottomCenter-pivoted sprites sit
        public float groundY    = -0.313f;
        // Optional per-item Y jitter so the layer isn't perfectly flat
        public float yJitter    = 0f;

        private float _rightmostX;
        private float _leftDespawn;
        private float _rightSpawn;
        private readonly List<Transform> _items = new List<Transform>();
        private System.Random _rng;

        void Start()
        {
            // Seed from name+position so each layer has a distinct, deterministic pattern.
            int seed = (name.GetHashCode() * 397) ^ transform.position.GetHashCode();
            _rng = new System.Random(seed);
            var cam = Camera.main;
            float camHalfW = (cam != null) ? cam.orthographicSize * cam.aspect : 2.5f;
            float camX     = (cam != null) ? cam.transform.position.x         : 2.5f;
            _leftDespawn   = camX - camHalfW - 1.0f;
            _rightSpawn    = camX + camHalfW + 1.2f;
            _rightmostX    = camX - camHalfW - 0.5f;
            // Pre-populate so the layer is dressed up from the very first frame
            int guard = 0;
            while (_rightmostX < _rightSpawn && guard++ < 64) SpawnNext();
        }

        // Called by WaveManager during inter-wave run. amount = positive scroll distance.
        public void Scroll(float amount)
        {
            if (sprites == null || sprites.Length == 0) return;
            float dx = amount * speedFactor;
            if (dx == 0f) return;

            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var t = _items[i];
                if (t == null) { _items.RemoveAt(i); continue; }
                t.position += new Vector3(-dx, 0f, 0f);
                if (t.position.x < _leftDespawn)
                {
                    Destroy(t.gameObject);
                    _items.RemoveAt(i);
                }
            }

            _rightmostX -= dx;
            int guard = 0;
            while (_rightmostX < _rightSpawn && guard++ < 64) SpawnNext();
        }

        void SpawnNext()
        {
            if (sprites == null || sprites.Length == 0) return;
            float spacing = Mathf.Lerp(minSpacing, maxSpacing, (float)_rng.NextDouble());
            _rightmostX += spacing;

            var sprite = sprites[_rng.Next(sprites.Length)];
            if (sprite == null) return;

            var go = new GameObject("ParallaxItem");
            go.transform.SetParent(transform, true);
            float scale = Mathf.Lerp(minScale, maxScale, (float)_rng.NextDouble());
            // Offset Y so the VISIBLE art bottom (not the transparent sprite-rect bottom)
            // touches groundY — prevents items from looking like they're floating.
            var ab = SpriteAlphaBounds.Get(sprite);
            float jy = yJitter > 0f ? ((float)_rng.NextDouble() * 2f - 1f) * yJitter : 0f;
            float yOffset = -ab.bottomFromBottom * scale;
            go.transform.position   = new Vector3(_rightmostX, groundY + jy + yOffset, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = sprite;
            sr.color        = tint;
            sr.sortingOrder = sortOrder;
            _items.Add(go.transform);
        }
    }
}
