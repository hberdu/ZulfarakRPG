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
        // One EXCLUSIVE set per depth band, nearest first. Nothing is shared between bands, so a
        // screen never shows the same silhouette twice and each band contrasts with the ones behind
        // it. Bands 0-2 are ground matter (vegetation, rock, ore, ruin); bands 3-4 are the far
        // structural skyline (towers, keeps, mountains) and are drawn hue-stripped + faded.
        public static string[][] PhasePropLayers(string scene)
        {
            switch (Phase(scene))
            {
                case 1: return new[] {
                    new[] { "FernClump",    "ToadstoolRing", "RootKnot",      "MossBoulder"   },
                    new[] { "BrambleBush",  "FallenTrunk",   "WildBerryBush", "MossyLog"      },
                    new[] { "RuinArch",     "BrokenColumn",  "IvyStone",      "StandingStone" },
                    new[] { "OldWatchpost", "ThatchCottage", "WoodPalisade",  "ChapelSpire"   },
                    new[] { "GreenKeep",    "ConiferRidge",  "MistMountain",  "FarHamlet"     },
                };
                case 2: return new[] {
                    // NOT "DryThorn" — that name is a phase-2 TREE (PhaseTreeNames), and a piece
                    // may only ever live in one bucket or the screen repeats it.
                    new[] { "SunSkull",     "ScrubTuft",     "RedPebbleHeap", "CrackedSlab"   },
                    new[] { "RustOreVein",  "BoneShard",     "SandstoneRock", "CactusPatch"   },
                    new[] { "CanyonSpire",  "TuskArch",      "RuinedPillar",  "SkullPike"     },
                    new[] { "MesaWatchpost","OrcLonghouse",  "StakeWall",     "ObsidianTower" },
                    new[] { "CanyonFort",   "ButteRidge",    "RedMountain",   "FarMesa"       },
                };
                case 3: return new[] {
                    new[] { "Cattails",     "LilyPatch",     "PeatMound",     "SwampReeds"    },
                    new[] { "GlowShrooms",  "SunkenLog",     "SlimePool",     "VineSnare"     },
                    new[] { "SwampSnag",    "DrownedArch",   "MireIdol",      "RottenPost"    },
                    new[] { "StiltHut",     "MireWatchpost", "SunkenChapel",  "BogGate"       },
                    new[] { "DrownedCitadel","FogRidge",     "MarshMountain", "FarBluff"      },
                };
                case 4: return new[] {
                    new[] { "FrostTuft",    "SnowDrift",     "IceShard",      "FrozenBramble" },
                    new[] { "SnowCairn",    "IceOreVein",    "BuriedStone",   "DeadHedge"     },
                    new[] { "CryptObelisk", "GraveCluster",  "BrokenTomb",    "IronFence"     },
                    new[] { "FrostChapel",  "SnowWatchpost", "CryptGate",     "BellSpire"     },
                    new[] { "GlacierKeep",  "FrostRidge",    "DistantPeak",   "FarSnowMount"  },
                };
                default: return new string[0][];
            }
        }

        // Flat pool of the same pieces — used by the dungeon parallax rows, which do their own
        // depth banding and just need every biome piece in one list.
        public static string[] PhasePropNames(string scene)
        {
            var layers = PhasePropLayers(scene);
            var flat   = new List<string>();
            foreach (var l in layers) flat.AddRange(l);
            return flat.ToArray();
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
                    // Harmonised like the props — foliage is the loudest thing on screen, so it is
                    // the first place an off-palette colour shows.
                    SwapTree(sr, Harmonize(trees[rng.Next(trees.Length)],
                                           MapScenery.Phase(sceneName)));
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
        // How many receding depth bands the open field is split into (0 = nearest).
        const int DepthLayers = 5;

        // Scatters the biome pieces across DepthLayers receding bands so the flat side-on strip
        // reads as a TILTED field seen slightly from above (TaskbarHero-style): the farther a band
        // is, the HIGHER up the strip it sits, the SMALLER it draws, the further back it sorts and
        // the more it fades out. Every piece is seated by its alpha-trimmed bottom so it actually
        // touches the ground instead of floating or sinking into it.
        void ScatterHubProps(Sprite[] props, System.Random rng)
        {
            float g = GroundAlignUtil.FindGroundTopY();

            // Each band draws ONLY from its own authored set (PhasePropLayers) — no piece is shared
            // between bands, so a screen never repeats a silhouette and every band contrasts with
            // the ones behind it. Falls back to the flat pool if a band's art isn't in yet.
            var buckets = MapScenery.PhasePropLayers(sceneName);

            for (int layer = 0; layer < DepthLayers; layer++)
            {
                var slice = layer < buckets.Length ? LoadTrees(buckets[layer]) : new Sprite[0];
                if (slice.Length == 0) slice = props;
                if (slice.Length == 0) continue;
                int sliceCursor = 0;
                float t = layer / (float)(DepthLayers - 1);          // 0 = nearest, 1 = farthest
                // EVERY band seats on the STANDING LINE — one straight row, no altitude drift and
                // never on top of the backdrop band. Depth is carried by SIZE and FADE alone.
                const float rise = 0f;
                float baseS = Mathf.Lerp(0.72f, 0.20f, t);           // strong size falloff, all small
                float alpha = Mathf.Lerp(1f,    0.28f, t);           // far bands fade well back
                int   sort  = -4 - layer * 3;                        // -4..-16, all above ground(-20/-19)

                // Farther bands are denser (smaller pieces, packed tighter) like a real horizon —
                // but keep the gaps WIDE overall: the play area is only ~5 world units, so short
                // gaps here put ~50 pieces on screen and the strip turned into visual soup.
                float minGap = Mathf.Lerp(1.70f, 0.85f, t);
                float maxGap = Mathf.Lerp(2.70f, 1.45f, t);

                float x = MapBounds.MinX + 0.05f + (float)rng.NextDouble() * (0.30f + t * 0.5f);
                while (x < MapBounds.MaxX - 0.1f)
                {
                    // Walk the slice in order (not random) so a piece only reappears after every
                    // other piece in this band has been used — no side-by-side duplicates.
                    var sprite = slice[sliceCursor % slice.Length];
                    sliceCursor++;
                    // The see-through bands carry NO hue — only shading — so they read as distance
                    // haze instead of competing with the hero for the eye. The near bands keep
                    // their colour but get pulled onto the map's palette.
                    sprite = layer >= DepthLayers - 2
                           ? Grayscale(sprite)
                           : Harmonize(sprite, MapScenery.Phase(sceneName));
                    float scale = baseS + ((float)rng.NextDouble() - 0.5f) * 0.10f * baseS;

                    var go = new GameObject($"HubProp_L{layer}_{sprite.name}");
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite       = sprite;
                    sr.sortingOrder = sort;
                    sr.color        = new Color(1f, 1f, 1f, alpha);

                    go.transform.position = new Vector3(x, 0f, 0f);   // SeatOnGround keeps X, sets Y
                    SeatOnGround(go.transform, sprite, g + rise, scale);
                    x += Mathf.Lerp(minGap, maxGap, (float)rng.NextDouble());
                }
            }
        }

        // Recoloured copies of a sprite, cached by (sprite, variant). CityDecor textures are
        // imported readable (ZulfarakArtPostprocessor) so these are plain pixel copies — no extra
        // shader to keep out of the build's strip list. Falls back to the original if it's locked.
        static readonly Dictionary<(Sprite, int), Sprite> _tintCache =
            new Dictionary<(Sprite, int), Sprite>();

        // Hue anchor per phase, in degrees — the colour the whole map is built around.
        // 1 forest sage-green, 2 canyon rust-tan, 3 swamp olive, 4 snow pale-blue.
        static readonly float[] _phaseHue = { 100f, 100f, 25f, 90f, 205f };

        // Strips hue entirely: the far bands read as shading only.
        static Sprite Grayscale(Sprite src) => Recolour(src, 0, (h, s) => (h, 0f));

        // Pulls every pixel toward the map's anchor hue and caps saturation, so no piece — least of
        // all a bright-red mushroom or a neon slime — sits outside its scenario's palette. Value is
        // untouched, so the art keeps its own shading and silhouette.
        static Sprite Harmonize(Sprite src, int phase)
        {
            float anchor = _phaseHue[Mathf.Clamp(phase, 0, 4)];
            return Recolour(src, phase + 1, (h, s) =>
            {
                // Shortest way round the wheel, so red (350) pulls toward 25 the short way.
                float d = Mathf.Repeat(anchor - h + 180f, 360f) - 180f;
                return (Mathf.Repeat(h + d * 0.55f, 360f), Mathf.Min(s, 0.42f));
            });
        }

        static Sprite Recolour(Sprite src, int variant, System.Func<float, float, (float, float)> map)
        {
            if (src == null) return null;
            var key = (src, variant);
            if (_tintCache.TryGetValue(key, out var hit)) return hit;

            Sprite result = src;
            try
            {
                var r  = src.textureRect;
                int w  = (int)r.width, h = (int)r.height;
                var px = src.texture.GetPixels((int)r.x, (int)r.y, w, h);
                for (int i = 0; i < px.Length; i++)
                {
                    if (px[i].a < 0.004f) continue;
                    Color.RGBToHSV(px[i], out float hh, out float ss, out float vv);
                    var (nh, ns) = map(hh * 360f, ss);
                    var c = Color.HSVToRGB(nh / 360f, ns, vv);
                    px[i] = new Color(c.r, c.g, c.b, px[i].a);
                }
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                tex.SetPixels(px);
                tex.Apply();
                result = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                                       src.pixelsPerUnit);
                result.name = src.name + "_v" + variant;
            }
            catch { /* not readable — keep the original rather than lose the prop */ }

            _tintCache[key] = result;
            return result;
        }

        // Places `t` so the sprite's VISIBLE (alpha-trimmed) bottom rests exactly on groundY at the
        // given uniform scale. Using the raw sprite bounds instead leaves transparent padding under
        // the art, which is what made pieces hover — or sink, when the padding was negative space
        // the artist filled. Every scenery piece goes through here so they all touch the floor.
        static void SeatOnGround(Transform t, Sprite sprite, float groundY, float scale)
        {
            var ab = SpriteAlphaBounds.Get(sprite);
            t.localScale = new Vector3(scale, scale, 1f);
            t.position   = new Vector3(t.position.x, groundY - ab.bottomFromBottom * scale, 0f);
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

                // RE-SEAT after the swap. Matching the old on-screen HEIGHT is not enough: each
                // piece of art has a different amount of transparent padding under it, so keeping
                // the old transform left the new trunk hanging above the line or sunk into it
                // ("árvores enterradas"). Drop it back onto the ground by its alpha-trimmed bottom.
                var ab = SpriteAlphaBounds.Get(next);
                var p  = sr.transform.position;
                p.y = GroundAlignUtil.FindGroundTopY() - ab.bottomFromBottom * k;
                sr.transform.position = p;

                // Baked trees must also clear the floor's sorting, same as the scattered props.
                if (sr.sortingOrder <= -19) sr.sortingOrder = -6;
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
        // any baked sprite still using it is removed. None of the current biome pieces match
        // these fragments — check a new name against this list before adding it.
        static bool IsGandalfLeftover(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            s = s.ToLowerInvariant();
            // CAREFUL with bare Contains here — these run against EVERY sprite in the scene.
            // "ores" used to be matched loosely and silently destroyed anything containing it,
            // including "f-ores-t": the ground tiles (city_forest_body / dungeon_forest_body) and
            // the ForestShrine prop vanished ~3 frames after GroundDressing built them. Match the
            // Gandalf "Ores" prop at a name boundary instead.
            return s.Contains("largetent") || s.Contains("smalltent") || s.Contains("large tent")
                || s.Contains("small tent") || s.Contains("torch") || s.Contains("cooking")
                || s.Contains("garden") || s.Contains("tall grass") || s.Contains("tallgrass")
                || s == "ores" || s.StartsWith("ores") || s.Contains("_ores") || s.Contains(" ores");
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
