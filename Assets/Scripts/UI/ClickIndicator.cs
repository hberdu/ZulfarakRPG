using System.Collections;
using UnityEngine;

namespace ZulfarakRPG
{
    // Expanding golden ring shown at the clicked world position.
    // Spawned in code — no prefab required.
    public class ClickIndicator : MonoBehaviour
    {
        public static void SpawnAt(Vector3 worldPos)
        {
            worldPos.z = -0.2f;
            new GameObject("ClickIndicator").AddComponent<ClickIndicator>()
                .transform.position = worldPos;
        }

        void Start() => StartCoroutine(Animate());

        IEnumerator Animate()
        {
            var sr          = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite       = BuildRingSprite(10);
            sr.color        = new Color(1f, 0.85f, 0.25f, 1f);
            sr.sortingOrder = 25;

            const float duration = 0.40f;
            const float maxScale = 0.55f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                transform.localScale = Vector3.one * (maxScale * t);
                var c = sr.color; c.a = 1f - t; sr.color = c;
                elapsed += Time.deltaTime;
                yield return null;
            }
            Destroy(gameObject);
        }

        static Sprite BuildRingSprite(int radius)
        {
            int size = radius * 2 + 2;
            var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            float cx = size * 0.5f, cy = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx + 0.5f, dy = y - cy + 0.5f;
                float d  = Mathf.Sqrt(dx * dx + dy * dy);
                float a  = Mathf.Clamp01(1.8f - Mathf.Abs(d - radius));
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                                 new Vector2(0.5f, 0.5f), size);
        }
    }
}
