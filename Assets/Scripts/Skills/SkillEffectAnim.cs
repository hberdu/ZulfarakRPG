using System.Collections.Generic;
using UnityEngine;

namespace ZulfarakRPG
{
    // Plays a PixelEffect skill sheet (sliced cols×rows) as a one-shot animation at a
    // world position — the magic visual for a cast. Frames are cached per sheet. Falls
    // back to a coloured ring (SkillCastFX) if the sheet can't be loaded.
    public class SkillEffectAnim : MonoBehaviour
    {
        static readonly Dictionary<string, Sprite[]> _cache = new Dictionary<string, Sprite[]>();

        Sprite[] _frames;
        SpriteRenderer _sr;
        float _t;
        int _i;
        const float FrameDur = 0.055f;

        public static void Spawn(Vector3 pos, int sheet, int cols, int rows, Color fallback, float worldSize)
        {
            var frames = GetFrames(sheet, cols, rows);
            if (frames == null || frames.Length == 0)
            {
                Debug.LogWarning($"[SkillEffectAnim] Sheet {sheet} nao carregou ({IconPaths.SkillFx(sheet)}) — usando anel.");
                SkillCastFX.Spawn(pos, fallback);
                return;
            }

            var go = new GameObject("SkillEffectAnim");
            go.transform.position = pos + new Vector3(0f, 0.35f, -1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = frames[0];
            sr.sortingOrder = 200;   // well above gameplay sprites
            // Match the player's sorting layer so it isn't hidden behind gameplay layers.
            var pl = Object.FindAnyObjectByType<PlayerController2D>();
            var plSr = pl != null ? pl.GetComponent<SpriteRenderer>() : null;
            if (plSr != null) sr.sortingLayerID = plSr.sortingLayerID;

            float spriteH = frames[0].bounds.size.y;
            float scale = spriteH > 0.001f ? worldSize / spriteH : 1f;
            go.transform.localScale = Vector3.one * scale;

            var a = go.AddComponent<SkillEffectAnim>();
            a._frames = frames;
            a._sr = sr;
        }

        static Sprite[] GetFrames(int sheet, int cols, int rows)
        {
            string key = $"{sheet}:{cols}x{rows}";
            if (_cache.TryGetValue(key, out var f)) return f;

            Sprite[] arr = null;
            var tex = IconLibrary.Tex(IconPaths.SkillFx(sheet));
            if (tex != null && cols > 0 && rows > 0)
            {
                int fw = tex.width / cols;
                int fh = tex.height / rows;
                if (fw > 0 && fh > 0)
                {
                    var list = new List<Sprite>(cols * rows);
                    // Row-major, top→bottom (texture rows are bottom-up, so invert Y).
                    for (int r = 0; r < rows; r++)
                        for (int c = 0; c < cols; c++)
                        {
                            var rect = new Rect(c * fw, tex.height - (r + 1) * fh, fw, fh);
                            list.Add(Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 16f));
                        }
                    arr = list.ToArray();
                }
            }
            _cache[key] = arr;
            return arr;
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_t < FrameDur) return;
            _t -= FrameDur;
            _i++;
            if (_frames == null || _i >= _frames.Length) { Destroy(gameObject); return; }
            _sr.sprite = _frames[_i];
        }
    }
}
