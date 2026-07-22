using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Themed background scenery per map: 12+ ORIGINAL generated pieces per phase (trees + props,
    // Resources/CityDecor) alternate through the parallax depth rows in dungeons and scatter as
    // fixed props in the hubs, with fake open-field depth (distant = higher + smaller). The old
    // Gandalf pack is retired — baked leftovers are destroyed or swapped on load.
    // Runtime hook so it works on the baked scenes without a rebuild (same pattern as
    // BackgroundLayers / CampSceneHooks).
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

        // TREE sprite names per phase — ORIGINAL generated art (the Gandalf pack is retired).
        // These are the swap targets for baked trees and the backbone of the parallax rows.
        public static string[] PhaseTreeNames(string scene)
        {
            switch (Phase(scene))
            {
                case 1:  return new[] { "AutumnOak", "AutumnMaple", "GnarledElm" };
                case 2:  return new[] { "DesertAcacia", "DryThorn" };
                case 3:  return new[] { "BogAlder", "RottenStump" };
                case 4:  return new[] { "FrostFir", "BareHawthorn" };
                default: return new string[0];
            }
        }

        // Biome PROPS (generated pixel-art, Resources/CityDecor): 12+ pieces per phase counting
        // the trees above; all alternate through the distant parallax rows. Names deliberately
        // avoid tree/pine/birch/willow/flower/statue/angel so the swap/destroy passes skip them.
        public static string[] PhasePropNames(string scene)
        {
            switch (Phase(scene))
            {
                case 1:  return new[] { "RuinArch", "ShroomCluster", "MossyLog", "ForestShrine",
                                        "StoneWell", "TraderTent", "HayCart", "FernClump",
                                        "MossBoulder", "RavenPerch" };
                case 2:  return new[] { "OrcTotem", "CanyonSpire", "BoneHeap", "OrcBanner",
                                        "OrcHut", "WarDrum", "SkullPike", "TuskArch",
                                        "CampCauldron", "ScrapPile" };
                case 3:  return new[] { "SwampSnag", "GlowShrooms", "SlimePool", "Cattails",
                                        "SwampHut", "VineSnare", "FrogTotem", "MireLantern",
                                        "PeatMound", "BubbleGeyser" };
                case 4:  return new[] { "GraveGuardian", "CryptObelisk", "IronFence", "LanternPost",
                                        "TombVault", "GraveCluster", "DeadHedge", "SnowCairn",
                                        "WraithLight", "BellShrine" };
                default: return new string[0];
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
            var props = LoadTrees(MapScenery.PhasePropNames(sceneName));
            var rng   = new System.Random(sceneName.GetHashCode());

            // Re-theme baked decorations: statues and leftover Gandalf-pack props removed; existing
            // trees are SWAPPED (not tinted) to a random phase tree, keeping their on-screen height.
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                var spr = sr.sprite; if (spr == null) continue;
                // Vase_/Column_ belong to SceneGrounder (it re-skins them with the new art on its
                // own 3-frame delay — never race-destroy them here).
                bool grounderOwned = sr.gameObject.name.StartsWith("Vase_")
                                  || sr.gameObject.name.StartsWith("Column_");
                if (!grounderOwned && (MapScenery.IsStatueSprite(spr.name) || IsGandalfLeftover(spr.name)))
                    { Destroy(sr.gameObject); continue; }
                if (MapScenery.IsTreeSprite(spr.name) && trees.Length > 0)
                    SwapTree(sr, trees[rng.Next(trees.Length)]);
            }

            // Phase 4: lay an icy blanket over the ground so it reads as snow (same blue as the
            // trees) — skipped once the real pixel-art snow tile exists (GroundDressing loads it).
            if (MapScenery.Phase(sceneName) == 4
                && Resources.Load<Texture2D>("Ground/ground_snow") == null)
                BuildSnowGround();

            // Themed background tree line — three depth rows, each scattering the phase trees randomly.
            // ONLY in dungeons: they scroll during the inter-wave march, so overlapping rows read as
            // depth. Static hubs (city + orc/slime camps) skipped these rows, which just piled trees
            // on top of each other in place — those keep their (re-themed) baked trees instead.
            var mixed = Mix(trees, props);
            if (MapBounds.IsDungeonScene(sceneName) && mixed.Length > 0)
            {
                float g = GroundAlignUtil.FindGroundTopY();
                // All rows alternate through the full 12-piece biome pool. The per-row ground
                // offsets place distant rows HIGHER on the open-field band (fake depth, matching
                // the TaskbarHero-style diagonal ground the path art draws).
                var far  = Row("PropRow_Far",  mixed, 0.24f, 0.38f, -24, g + 0.10f, 0.30f, 1.6f, 2.8f, 0.02f);
                var mid  = Row("PropRow_Mid",  mixed, 0.36f, 0.55f, -18, g + 0.06f, 0.48f, 2.0f, 3.4f, 0.02f);
                var near = Row("PropRow_Near", mixed, 0.50f, 0.72f, -12, g + 0.02f, 0.65f, 2.4f, 4.0f, 0.01f);
                var rows = new List<ParallaxLayer> { far, mid, near };
                if (props.Length > 0)
                    rows.Add(Row("PropRow_Fg", props, 0.52f, 0.85f, -10, g + 0.01f, 0.74f, 4.2f, 7.0f, 0f));
                if (WaveManager.Instance != null)
                    WaveManager.Instance.parallaxLayers = rows.ToArray();
            }
            else if (!MapBounds.IsDungeonScene(sceneName) && props.Length > 0)
            {
                // Static hubs (city + camps): scatter a handful of fixed biome props behind the
                // NPC/prop layer, seated on the real ground line. Never in dungeons — fixed props
                // would break the scroll illusion there.
                ScatterHubProps(props, rng);
            }
            Destroy(gameObject);
        }

        static Sprite[] Mix(Sprite[] a, Sprite[] b)
        {
            if (b.Length == 0) return a;
            var list = new List<Sprite>(a);
            list.AddRange(b);
            return list.ToArray();
        }

        // Fixed biome props for the static hubs. Deterministic per scene (seeded rng), spread
        // across the shared playable width, seated with the same alpha-aware maths as
        // ParallaxLayer so the visible art bottom touches the ground line.
        void ScatterHubProps(Sprite[] props, System.Random rng)
        {
            float g = GroundAlignUtil.FindGroundTopY();
            float x = MapBounds.MinX + 0.05f + (float)rng.NextDouble() * 0.25f;
            int i = 0;
            while (x < MapBounds.MaxX - 0.1f && i < 9)
            {
                var sprite = props[rng.Next(props.Length)];
                // Depth on the open-field band: farther props sit higher, draw smaller and
                // behind — same fake perspective as the dungeon rows.
                float depth = (float)rng.NextDouble() * 0.09f;
                float scale = Mathf.Lerp(0.62f, 0.38f, depth / 0.09f)
                            + ((float)rng.NextDouble() - 0.5f) * 0.08f;
                var ab = SpriteAlphaBounds.Get(sprite);
                var go = new GameObject("HubProp_" + sprite.name);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite       = sprite;
                sr.sortingOrder = -7 - Mathf.RoundToInt(depth * 30f);   // -7..-11, behind wagon(-4)
                go.transform.position   = new Vector3(x, g + depth - ab.bottomFromBottom * scale, 0f);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                x += Mathf.Lerp(0.5f, 0.9f, (float)rng.NextDouble());
                i++;
            }
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
                                 int sort, float groundY, float speed, float minSp, float maxSp,
                                 float jitter = 0f)
        {
            var go = new GameObject(name);
            var pl = go.AddComponent<ParallaxLayer>();
            pl.sprites = sprites; pl.tint = Color.white;   // NO tint — colour comes from the chosen sprite
            pl.sortOrder = sort;
            pl.minScale = minS; pl.maxScale = maxS; pl.groundY = groundY;
            pl.speedFactor = speed; pl.minSpacing = minSp; pl.maxSpacing = maxSp;
            pl.yJitter = jitter;
            return pl;
        }

        // Gandalf-pack leftovers that neither the tree swap nor the statue pass catches —
        // torches, cooking spots, garden decor, ore piles, tall grass. The pack is retired;
        // any baked sprite still using it is removed. (TraderTent/HayCart are OUR art and do
        // not match these fragments.)
        static bool IsGandalfLeftover(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            s = s.ToLowerInvariant();
            return s.Contains("largetent") || s.Contains("smalltent") || s.Contains("large tent")
                || s.Contains("small tent") || s.Contains("torch") || s.Contains("cooking")
                || s.Contains("garden") || s.Contains("tall grass") || s.Contains("tallgrass")
                || s.Contains("ores");
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
