using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ZulfarakRPG;

// Phase 3-1 (Slime Settlement) — eroded gray terrain + distant mountains, slimes + Giant Slime.
// Reuses SceneSetupWizard's private helpers (partial class) and the Phase-2 helpers.
public static partial class SceneSetupWizard
{
    [MenuItem("Tools/ZulfarakRPG/Setup Phase 3-1 (Slime Settlement)")]
    public static void SetupPhase3()
    {
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        SetupCamp3Scene();
        SetupDungeon3Scene();
        AddSceneToBuild("Camp_3_1");
        AddSceneToBuild("Dungeon_3_1");
        EditorUtility.DisplayDialog("Fase 3-1",
            "Camp_3_1 e Dungeon_3_1 criadas.\n\nImporte o sprite 'Slime' (Import Character Sprites) se sair invisivel.", "OK");
    }

    private static void SetupCamp3Scene()
    {
        const float ORTHO = 0.75f, CAM_X = 2.5f, GROUND_TOP = -0.344f, GROUND_CY = -0.494f;
        const float GROUND_W = 5.0f, GROUND_H = 0.30f, SPAWN_Y = GROUND_TOP - 0.80f;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeGameplayCamera(CAM_X, ORTHO, new Color(0.05f, 0.055f, 0.07f));
        AddDragStrip();
        MakeTiledGround(CAM_X, GROUND_CY, GROUND_W, GROUND_H, "ground_eroded");
        PlaceMountains(CAM_X, GROUND_TOP);

        // Fewer, darker rocks + low bushes; wagon + campfire, no buildings.
        PlaceDecoration("Rock_L", EnsureProp(ZulfArt + "rock_big.png"), new Color(0.7f, 0.7f, 0.75f), 0.6f, GROUND_TOP, 2.0f, -6);
        PlaceDecoration("Rock_R", EnsureProp(ZulfArt + "rock_med.png"), new Color(0.7f, 0.7f, 0.75f), 4.4f, GROUND_TOP, 1.8f, -4);
        PlaceDecoration("Rock_S", EnsureProp(ZulfArt + "rock_small.png"), new Color(0.7f, 0.7f, 0.75f), 3.6f, GROUND_TOP, 1.8f, -2);
        PlaceDecoration("Bush", EnsureProp(ZulfArt + "bush.png"), new Color(0.7f, 0.75f, 0.65f), 1.1f, GROUND_TOP, 1.6f, -2);
        PlaceDecoration("Wagon", EnsureProp(ZulfArt + "wagon.png"), Color.white, 3.15f, GROUND_TOP, 3.0f, -4);
        CreateCampfire(new Vector3(1.9f, GROUND_TOP, 0f));
        ScatterGroundDetail(0.5f, 4.6f, GROUND_TOP, 31,
            new[] { "rock_small.png", "rock_med.png", "bush.png" }, new Color(0.68f, 0.70f, 0.74f));

        var playerGO = CreatePlayerGO(SPAWN_Y);
        playerGO.transform.position = new Vector3(0.7f, SPAWN_Y, 0f);
        var pc = playerGO.GetComponent<PlayerController2D>();
        WirePlayerClassSprites(pc);
        pc.sceneBoundsMinX = 0.45f; pc.sceneBoundsMaxX = 4.55f;
        EditorUtility.SetDirty(pc);
        AddWall("WallLeft", new Vector3(0.15f, 0f, 0f), new Vector2(0.2f, 3.2f));
        AddWall("WallRight", new Vector3(4.85f, 0f, 0f), new Vector2(0.2f, 3.2f));
        CreateKaelNPC(new Vector3(1.35f, SPAWN_Y, 0f), GROUND_TOP);
        CreateClassMaster(new Vector3(2.45f, SPAWN_Y, 0f), GROUND_TOP);

        var portalGO = CreatePortalGO("DungeonPortal", "Dungeon_3_1",
            new Vector3(4.50f, GROUND_TOP + 0.45f, 0f), openOnStart: true);
        portalGO.GetComponent<Portal2D>().tooltipText = "3-1";
        EditorUtility.SetDirty(portalGO.GetComponent<Portal2D>());

        Save(scene, "Camp_3_1");
    }

    private static void SetupDungeon3Scene()
    {
        const float ORTHO = 0.75f, CAM_X = 2.5f, GROUND_TOP = -0.313f, GROUND_CY = -0.463f;
        const float GROUND_H = 0.30f, SPAWN_Y = GROUND_TOP - 0.80f;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeGameplayCamera(CAM_X, ORTHO, new Color(0.04f, 0.045f, 0.06f));
        AddDragStrip();
        MakeTiledGround(CAM_X + 1.0f, GROUND_CY, 7.0f, GROUND_H, "ground_eroded");
        PlaceMountains(CAM_X, GROUND_TOP);

        PlaceDecoration("PathRock1", EnsureProp(ZulfArt + "rock_small.png"), new Color(0.7f, 0.7f, 0.75f), 1.3f, GROUND_TOP, 1.6f, -3);
        PlaceDecoration("PathRock2", EnsureProp(ZulfArt + "rock_med.png"), new Color(0.7f, 0.7f, 0.75f), 3.1f, GROUND_TOP, 1.7f, -4);
        PlaceDecoration("PathRock3", EnsureProp(ZulfArt + "rock_small.png"), new Color(0.7f, 0.7f, 0.75f), 6.0f, GROUND_TOP, 1.6f, -3);
        ScatterGroundDetail(0.5f, 7.0f, GROUND_TOP, 32,
            new[] { "rock_small.png", "rock_med.png", "bush.png" }, new Color(0.66f, 0.68f, 0.72f));

        var playerGO = CreatePlayerGO(SPAWN_Y);
        playerGO.transform.position = new Vector3(0.7f, SPAWN_Y, 0f);
        var pc = playerGO.GetComponent<PlayerController2D>();
        WirePlayerClassSprites(pc);
        pc.sceneBoundsMinX = 0.45f; pc.sceneBoundsMaxX = 4.55f;
        EditorUtility.SetDirty(pc);
        AddWall("WallLeft", new Vector3(0.15f, 0f, 0f), new Vector2(0.2f, 3.2f));
        AddWall("WallRight", new Vector3(7.10f, 0f, 0f), new Vector2(0.2f, 3.2f));

        var spawnsRoot = new GameObject("SpawnPoints");
        var spawnPts = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            var sp = new GameObject($"SpawnPoint_{i}");
            sp.transform.SetParent(spawnsRoot.transform, false);
            sp.transform.position = new Vector3(5.2f + i * 0.35f, SPAWN_Y, 0f);
            spawnPts[i] = sp.transform;
        }

        var exitPortalGO = CreatePortalGO("ExitPortal", "Camp_3_1",
            new Vector3(0.50f, GROUND_TOP + 0.45f, 0f), openOnStart: false);
        var exitPortal = exitPortalGO.GetComponent<Portal2D>();

        Directory.CreateDirectory(Application.dataPath + "/../Assets/Prefabs");
        var slimeScene = CreateEnemyGO("Slime", "Slime", boss: false);
        var slimePrefab = PrefabUtility.SaveAsPrefabAsset(slimeScene, "Assets/Prefabs/Slime.prefab");
        Object.DestroyImmediate(slimeScene);
        var giantScene = CreateGiantSlimeGO("GiantSlime");
        var giantPrefab = PrefabUtility.SaveAsPrefabAsset(giantScene, "Assets/Prefabs/GiantSlimeBoss.prefab");
        Object.DestroyImmediate(giantScene);

        var wmGO = new GameObject("WaveManager");
        var wm = wmGO.AddComponent<WaveManager>();
        wm.skeletonPrefab = slimePrefab;
        wm.armoredSkeletonPrefab = slimePrefab;
        wm.necromancerPrefab = giantPrefab;
        wm.spawnPoints = spawnPts;
        wm.exitPortal = exitPortal;
        wm.runScrollSpeed = 4.0f;
        wm.runScrollDistance = 12.0f;

        var hudCanvas = CreateCanvas("HUDCanvas");
        hudCanvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(400, 120);
        hudCanvas.GetComponent<Canvas>().sortingOrder = 10;
        wm.clearText = MakeHudText(hudCanvas, "ClearText", "CLEAR", 64, new Color(1f, 0.92f, 0.15f));
        wm.defeatText = MakeHudText(hudCanvas, "DefeatText", "DEFEAT", 56, new Color(0.85f, 0.15f, 0.15f));
        wm.bossText = MakeHudText(hudCanvas, "BossText", "BOSS", 64, new Color(0.90f, 0.15f, 0.15f));
        EditorUtility.SetDirty(wm);

        Save(scene, "Dungeon_3_1");
    }

    private static GameObject CreateGiantSlimeGO(string name)
    {
        var go = new GameObject(name);
        go.transform.localScale = new Vector3(2.8f, 2.8f, 1f);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f; rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        go.AddComponent<SpriteRenderer>().sortingOrder = 1;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.30f, CHAR_HEIGHT);
        col.offset = new Vector2(0f, FEET_OFFSET + CHAR_HEIGHT * 0.5f);
        var hpGO = new GameObject("HealthBar");
        hpGO.transform.SetParent(go.transform, false);
        hpGO.transform.localPosition = new Vector3(0f, FEET_OFFSET + CHAR_HEIGHT + 0.02f, -0.1f);
        hpGO.AddComponent<WorldHealthBar>().barHeight = 0.025f;

        var boss = go.AddComponent<GiantSlimeBoss>();
        boss.idleFrames = CharacterSpriteImporter.GetFrames("Slime", "Idle");
        boss.walkFrames = CharacterSpriteImporter.GetFrames("Slime", "Walk");
        boss.attackFrames = CharacterSpriteImporter.MergeFrames(
            CharacterSpriteImporter.GetFrames("Slime", "Attack01"),
            CharacterSpriteImporter.GetFrames("Slime", "Attack02"));
        boss.deathFrames = CharacterSpriteImporter.GetFrames("Slime", "Death");
        boss.hurtFrames = CharacterSpriteImporter.GetFrames("Slime", "Hurt");
        boss.maxHealth = 1925f; boss.attackDamage = 275f;
        boss.moveSpeed = 1.8f; boss.attackCooldown = 2.0f;
        boss.sceneBoundsMinX = 0.45f; boss.sceneBoundsMaxX = 4.55f;
        EditorUtility.SetDirty(boss);
        return go;
    }

    // ── Shared scene helpers (used by phase 2 & 3) ────────────────────────────────────────
    private static void MakeGameplayCamera(float camX, float ortho, Color bg)
    {
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = bg;
        cam.orthographic = true; cam.orthographicSize = ortho;
        cam.allowHDR = false; cam.allowMSAA = false;
        camGO.transform.position = new Vector3(camX, 0f, -10f);
        camGO.tag = "MainCamera"; camGO.AddComponent<AudioListener>();
        AddEventSystem();
    }

    private static void AddDragStrip()
    {
        var dragCanvas = CreateCanvas("DragCanvas");
        dragCanvas.GetComponent<Canvas>().sortingOrder = 100;
        CreatePanel(dragCanvas, "DragArea", Color.clear, Vec(0, 0.88f), Vec(1, 1)).AddComponent<DragWindow>();
    }

    private static void MakeTiledGround(float cx, float cy, float w, float h, string art)
    {
        // TaskbarHero platform: tile only horizontally (single row) with the tile's native
        // height so the lit surface sits at the top and the body extends down — never a
        // vertically-cropped mid-slice. Physics floor stays exactly at (cy, h).
        var spr    = EnsureProp(ZulfArt + art + ".png", bottomPivot: false);
        float tileH = spr != null ? spr.bounds.size.y : h;   // native tile height (world units)
        float top   = cy + h * 0.5f;                          // surface line
        var ground = new GameObject("Ground");
        ground.transform.position = new Vector3(cx, top - tileH * 0.5f, 0f);
        var gsr = ground.AddComponent<SpriteRenderer>();
        gsr.sprite       = spr;
        gsr.drawMode     = SpriteDrawMode.Tiled;
        gsr.size         = new Vector2(w, tileH);
        gsr.sortingOrder = -5;
        var gcol = ground.AddComponent<BoxCollider2D>();
        gcol.size   = new Vector2(w, h);
        gcol.offset = new Vector2(0f, cy - (top - tileH * 0.5f));  // keep the physics floor put
    }

    // Distant mountain range behind everything (far, hazy). tint defaults to the phase-3
    // hazy blue; phase 4 passes a darker tint for its night/cemetery mood.
    private static void PlaceMountains(float camX, float groundTop, Color? tint = null)
    {
        var spr = EnsureProp(ZulfArt + "mountains.png", bottomPivot: true);
        if (spr == null) return;
        var go = new GameObject("Mountains");
        float scale = 12f;
        var ab = ZulfarakRPG.SpriteAlphaBounds.Get(spr);
        go.transform.position = new Vector3(camX, groundTop - ab.bottomFromBottom * scale + 0.15f, 0f);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spr;
        sr.color = tint ?? new Color(0.75f, 0.78f, 0.88f);  // hazy distance tint
        sr.sortingOrder = -12;
    }

    // Sprinkles extra small ground props (pebbles/tufts/graves) across [x0,x1] to make the
    // scene read denser and more detailed. Uses the existing Zulfarak sprites at small scales,
    // tinted + pushed to back sorting orders so they sit behind the action as scatter.
    private static void ScatterGroundDetail(float x0, float x1, float groundTop, int seed, string[] props, Color tint)
    {
        var rng = new System.Random(seed);
        int count = 8;
        for (int i = 0; i < count; i++)
        {
            float t = (i + (float)rng.NextDouble() * 0.7f) / count;
            float x = Mathf.Lerp(x0, x1, Mathf.Clamp01(t));
            string p = props[rng.Next(props.Length)];
            float scale = 0.6f + (float)rng.NextDouble() * 0.7f;
            var jitter = 0.88f + (float)rng.NextDouble() * 0.24f;
            var col = new Color(tint.r * jitter, tint.g * jitter, tint.b * jitter, 1f);
            PlaceDecoration($"Scatter_{i}", EnsureProp(ZulfArt + p), col, x, groundTop, scale, -2 - rng.Next(3));
        }
    }

    private static void AddSceneToBuild(string name)
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        string path = SceneFolder + "/" + name + ".unity";
        if (list.TrueForAll(s => s.path != path))
            list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
