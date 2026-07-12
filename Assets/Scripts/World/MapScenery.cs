using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Themed background trees per map, chosen by each sprite's OWN natural colour (no tinting):
    //   phase 1 (Zulfarak + first Dungeon) = red/orange autumn trees (Tree3 / Birch2 / Birch1),
    //   phases 2-3 = green (Tree1/2, Willow, Pine, Flowering), phase 4 (werewolf map) = the icy
    //   light-blue tree (Tree4). Phase 4's ground also gets an icy blanket so it reads as snow.
    // Trees are scattered RANDOMLY across three parallax depth rows. Runtime hook so it works on the
    // baked scenes without a rebuild (same pattern as BackgroundLayers / CampSceneHooks).
    public static class MapScenery
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!MapBounds.IsGameplayScene(scene.name)) return;
            // Delayed builder: let BackgroundLayers (strips ParallaxLayers) and GroundDressing (builds
            // the floor) run first, so our tree rows survive the strip and seat on the real ground.
            new GameObject("MapSceneryBuilder").AddComponent<MapSceneryBuilder>().sceneName = scene.name;
        }

        // Phase = digit after the first '_' (Camp_2_1 / Dungeon_3_1 → 2 / 3); the first city
        // ("Zulfarak") and first dungeon ("Dungeon") have no digit → phase 1.
        public static int Phase(string n)
        {
            int us = n.IndexOf('_');
            if (us >= 0 && us + 1 < n.Length && char.IsDigit(n[us + 1])) return n[us + 1] - '0';
            return 1;
        }

        // Tree sprite NAMES picked for each phase by their OWN colour (sampled averages):
        //   red/orange autumn: Tree3 (164,75,43) · Birch2 (153,91,49) · Birch1 (autumn yellow)
        //   green: Tree1/Tree2/Willow/Pine/Flowering ·  icy light-blue: Tree4 (144,169,189).
        public static string[] PhaseTreeNames(string scene)
        {
            switch (Phase(scene))
            {
                case 1:  return new[] { "Tree3", "Birch2", "Birch1" };
                case 4:  return new[] { "Tree4" };
                default: return new[] { "Tree1", "Tree2", "WeepingWillow1", "LargePineTree", "FloweringTree" };
            }
        }

        // Icy snow tone for phase 4's ground — same light-blue family as its Tree4.
        public static readonly Color SnowColor = new Color(0.72f, 0.82f, 0.92f, 0.55f);

        public static bool IsTreeSprite(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            s = s.ToLowerInvariant();
            return s.Contains("tree") || s.Contains("pine") || s.Contains("birch")
                || s.Contains("willow") || s.Contains("flower");
        }

        public static bool IsStatueSprite(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            s = s.ToLowerInvariant();
            return s.Contains("statue") || s.Contains("angel");
        }
    }

    public class MapSceneryBuilder : MonoBehaviour
    {
        public string sceneName;

        IEnumerator Start()
        {
            yield return null; yield return null; yield return null;

            var trees = LoadTrees(MapScenery.PhaseTreeNames(sceneName));
            var rng   = new System.Random(sceneName.GetHashCode());

            // Re-theme baked decorations: statues removed; existing trees are SWAPPED (not tinted) to
            // a random phase-coloured tree, keeping their on-screen height.
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                var spr = sr.sprite; if (spr == null) continue;
                if (MapScenery.IsStatueSprite(spr.name)) { Destroy(sr.gameObject); continue; }
                if (MapScenery.IsTreeSprite(spr.name) && trees.Length > 0)
                    SwapTree(sr, trees[rng.Next(trees.Length)]);
            }

            // Phase 4: lay an icy blanket over the ground so it reads as snow (same blue as the trees).
            if (MapScenery.Phase(sceneName) == 4)
                BuildSnowGround();

            // Themed background tree line — three depth rows, each scattering the phase trees randomly
            // (ParallaxLayer picks per-item from `sprites`, seeded per row → disordered across layers).
            // Skip the hub city (its own trees were swapped above). Dungeons register the rows with the
            // WaveManager so they scroll during the inter-wave march.
            if (sceneName != "Zulfarak" && trees.Length > 0)
            {
                float g = GroundAlignUtil.FindGroundTopY();
                var far  = Row("TreeRow_Far",  trees, 0.24f, 0.38f, -24, g, 0.30f, 2.0f, 3.4f);
                var mid  = Row("TreeRow_Mid",  trees, 0.36f, 0.55f, -18, g, 0.48f, 2.4f, 3.9f);
                var near = Row("TreeRow_Near", trees, 0.50f, 0.72f, -12, g, 0.65f, 2.6f, 4.3f);
                if (WaveManager.Instance != null)
                    WaveManager.Instance.parallaxLayers = new[] { far, mid, near };
            }
            Destroy(gameObject);
        }

        // Swap a baked tree's sprite for `next`, preserving its visible height (different tree art has
        // different pixel dims). Colour reset to white so the sprite's own colour shows (no paint).
        static void SwapTree(SpriteRenderer sr, Sprite next)
        {
            if (next == null || sr.sprite == null) return;
            float oldH = sr.sprite.bounds.size.y * Mathf.Abs(sr.transform.localScale.y);
            sr.sprite = next;
            sr.color  = Color.white;
            float newH = next.bounds.size.y;
            if (newH > 1e-4f)
            {
                float k  = oldH / newH;
                var   ls = sr.transform.localScale;
                float sx = ls.x >= 0f ? 1f : -1f;
                sr.transform.localScale = new Vector3(sx * k, k, ls.z);
            }
        }

        void BuildSnowGround()
        {
            float g = GroundAlignUtil.FindGroundTopY();
            var go = new GameObject("SnowGround");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = WhitePixel();
            sr.color        = MapScenery.SnowColor;
            sr.sortingOrder = -5;   // above the ground platform (≤ -6), behind the characters
            const float height = 1.4f;
            float width = MapBounds.Width + 2f;
            go.transform.position   = new Vector3(MapBounds.CenterX, g + 0.05f - height * 0.5f, 0f);
            go.transform.localScale = new Vector3(width, height, 1f);
        }

        static ParallaxLayer Row(string name, Sprite[] sprites, float minS, float maxS,
                                 int sort, float groundY, float speed, float minSp, float maxSp)
        {
            var go = new GameObject(name);
            var pl = go.AddComponent<ParallaxLayer>();
            pl.sprites = sprites; pl.tint = Color.white;   // NO tint — colour comes from the chosen sprite
            pl.sortOrder = sort;
            pl.minScale = minS; pl.maxScale = maxS; pl.groundY = groundY;
            pl.speedFactor = speed; pl.minSpacing = minSp; pl.maxSpacing = maxSp;
            return pl;
        }

        static Sprite[] LoadTrees(string[] names)
        {
            var list = new List<Sprite>();
            foreach (var n in names) { var s = Resources.Load<Sprite>("CityDecor/" + n); if (s != null) list.Add(s); }
            return list.ToArray();
        }

        static Sprite _white;
        static Sprite WhitePixel()
        {
            if (_white != null) return _white;
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            t.SetPixel(0, 0, Color.white); t.Apply();
            _white = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _white;
        }
    }
}
