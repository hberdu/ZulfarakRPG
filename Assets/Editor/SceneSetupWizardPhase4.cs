using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ZulfarakRPG;

// Phase 4-1 (Cursed Cemetery) — a darker continuation of phase 3: near-black eroded ground,
// dark distant mountains, gravestones + a mausoleum along the path, bigger/darker rocks,
// werewolf packs + the Alpha Werewolf boss. Reuses SceneSetupWizard's partial helpers and the
// phase 2/3 helpers (MakeGameplayCamera, AddDragStrip, MakeTiledGround, PlaceMountains, ...).
public static partial class SceneSetupWizard
{
    // Very dark, slightly cold tint so the cemetery reads as night.
    static readonly Color MountainsDark = new Color(0.30f, 0.32f, 0.42f);
    static readonly Color RockDark      = new Color(0.45f, 0.46f, 0.52f);

    [MenuItem("Tools/ZulfarakRPG/Setup Phase 4-1 (Cursed Cemetery)")]
    public static void SetupPhase4()
    {
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        SetupCamp4Scene();
        SetupDungeon4Scene();
        AddSceneToBuild("Camp_4_1");
        AddSceneToBuild("Dungeon_4_1");
        EditorUtility.DisplayDialog("Fase 4-1",
            "Camp_4_1 e Dungeon_4_1 criadas.\n\nImporte o sprite 'Werewolf' (Import Character Sprites) se sair invisivel.", "OK");
    }

    private static void SetupCamp4Scene()
    {
        const float ORTHO = 0.75f, CAM_X = 2.5f, GROUND_TOP = -0.344f, GROUND_CY = -0.494f;
        const float GROUND_W = 5.0f, GROUND_H = 0.30f, SPAWN_Y = GROUND_TOP - 0.80f;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeGameplayCamera(CAM_X, ORTHO, new Color(0.02f, 0.02f, 0.03f));   // near-black night sky
        AddDragStrip();
        MakeTiledGround(CAM_X, GROUND_CY, GROUND_W, GROUND_H, "ground_dark");
        // Distant mountains come from the layered BackgroundLayers backdrop now (no pixel-art prop).

        // Cemetery dressing — bigger/darker rocks, gravestones, a mausoleum, wagon + campfire.
        PlaceDecoration("Rock_L", EnsureProp(ZulfArt + "rock_big.png"), RockDark, 0.55f, GROUND_TOP, 2.6f, -6);
        PlaceDecoration("Rock_R", EnsureProp(ZulfArt + "rock_big.png"), RockDark, 4.55f, GROUND_TOP, 2.4f, -6);
        PlaceDecoration("Mausoleum", EnsureProp(ZulfArt + "mausoleum.png"), Color.white, 4.05f, GROUND_TOP, 2.6f, -5);
        PlaceDecoration("Grave1", EnsureProp(ZulfArt + "gravestone.png"), Color.white, 1.15f, GROUND_TOP, 2.0f, -3);
        PlaceDecoration("Grave2", EnsureProp(ZulfArt + "gravestone.png"), Color.white, 3.05f, GROUND_TOP, 1.8f, -3);
        PlaceDecoration("Wagon", EnsureProp(ZulfArt + "wagon.png"), Color.white, 3.55f, GROUND_TOP, 3.0f, -4);
        CreateCampfire(new Vector3(1.9f, GROUND_TOP, 0f));
        ScatterGroundDetail(0.5f, 4.6f, GROUND_TOP, 41,
            new[] { "rock_small.png", "rock_big.png", "gravestone.png" }, new Color(0.42f, 0.44f, 0.50f));

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

        var portalGO = CreatePortalGO("DungeonPortal", "Dungeon_4_1",
            new Vector3(4.50f, GROUND_TOP + 0.45f, 0f), openOnStart: true);
        portalGO.GetComponent<Portal2D>().tooltipText = "4-1";
        EditorUtility.SetDirty(portalGO.GetComponent<Portal2D>());

        Save(scene, "Camp_4_1");
    }

    private static void SetupDungeon4Scene()
    {
        const float ORTHO = 0.75f, CAM_X = 2.5f, GROUND_TOP = -0.313f, GROUND_CY = -0.463f;
        const float GROUND_H = 0.30f, SPAWN_Y = GROUND_TOP - 0.80f;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeGameplayCamera(CAM_X, ORTHO, new Color(0.015f, 0.015f, 0.025f));
        AddDragStrip();
        MakeTiledGround(CAM_X + 1.0f, GROUND_CY, 7.0f, GROUND_H, "ground_dark");
        // Distant mountains come from the layered BackgroundLayers backdrop now (no pixel-art prop).

        // Graves + a mausoleum + big dark rocks scattered along the path.
        PlaceDecoration("Grave1", EnsureProp(ZulfArt + "gravestone.png"), Color.white, 1.2f, GROUND_TOP, 1.9f, -3);
        PlaceDecoration("Mausoleum", EnsureProp(ZulfArt + "mausoleum.png"), Color.white, 2.6f, GROUND_TOP, 2.4f, -5);
        PlaceDecoration("Grave2", EnsureProp(ZulfArt + "gravestone.png"), Color.white, 4.3f, GROUND_TOP, 1.8f, -3);
        // Clean arena (no path props) — matches the first dungeon; only the layered backdrop shows.
        ScatterGroundDetail(0.5f, 7.0f, GROUND_TOP, 42,
            new[] { "rock_small.png", "rock_med.png", "gravestone.png" }, new Color(0.40f, 0.42f, 0.48f));

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

        var exitPortalGO = CreatePortalGO("ExitPortal", "Camp_4_1",
            new Vector3(0.50f, GROUND_TOP + 0.45f, 0f), openOnStart: false);
        var exitPortal = exitPortalGO.GetComponent<Portal2D>();

        Directory.CreateDirectory(Application.dataPath + "/../Assets/Prefabs");
        var wolfScene = CreateEnemyGO("Werewolf", "Werewolf", boss: false);
        var wolfPrefab = PrefabUtility.SaveAsPrefabAsset(wolfScene, "Assets/Prefabs/Werewolf.prefab");
        Object.DestroyImmediate(wolfScene);
        var alphaScene = CreateAlphaWerewolfGO("AlphaWerewolf");
        var alphaPrefab = PrefabUtility.SaveAsPrefabAsset(alphaScene, "Assets/Prefabs/AlphaWerewolfBoss.prefab");
        Object.DestroyImmediate(alphaScene);

        var wmGO = new GameObject("WaveManager");
        var wm = wmGO.AddComponent<WaveManager>();
        wm.skeletonPrefab = wolfPrefab;
        wm.armoredSkeletonPrefab = wolfPrefab;
        wm.necromancerPrefab = alphaPrefab;   // wave-10 boss slot = Alpha Werewolf
        wm.spawnPoints = spawnPts;
        wm.exitPortal = exitPortal;
        wm.runScrollSpeed = 4.0f;
        wm.runScrollDistance = 12.0f;

        var hudCanvas = CreateCanvas("HUDCanvas");
        hudCanvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(400, 120);
        hudCanvas.GetComponent<Canvas>().sortingOrder = 10;
        wm.clearText = MakeHudText(hudCanvas, "ClearText", "CLEAR", 64, new Color(1f, 0.92f, 0.15f));
        wm.defeatText = MakeHudText(hudCanvas, "DefeatText", "DEFEAT", 56, new Color(0.85f, 0.15f, 0.15f));
        wm.bossText = MakeHudText(hudCanvas, "BossText", "ALPHA WEREWOLF", 48, new Color(0.90f, 0.15f, 0.15f));
        EditorUtility.SetDirty(wm);

        Save(scene, "Dungeon_4_1");
    }

    private static GameObject CreateAlphaWerewolfGO(string name)
    {
        var go = new GameObject(name);
        go.transform.localScale = new Vector3(2.8f, 2.8f, 1f);   // bigger/fiercer than the pack
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

        var boss = go.AddComponent<AlphaWerewolfBoss>();
        boss.idleFrames = CharacterSpriteImporter.GetFrames("Werewolf", "Idle");
        boss.walkFrames = CharacterSpriteImporter.GetFrames("Werewolf", "Walk");
        boss.attackFrames = CharacterSpriteImporter.MergeFrames(
            CharacterSpriteImporter.GetFrames("Werewolf", "Attack01"),
            CharacterSpriteImporter.GetFrames("Werewolf", "Attack02"));
        boss.deathFrames = CharacterSpriteImporter.GetFrames("Werewolf", "Death");
        boss.hurtFrames = CharacterSpriteImporter.GetFrames("Werewolf", "Hurt");
        boss.maxHealth = 2200f; boss.attackDamage = 360f;   // fallback; server catalog overrides
        boss.moveSpeed = 3.6f; boss.attackCooldown = 1.5f;  // fast, aggressive alpha
        boss.sceneBoundsMinX = 0.45f; boss.sceneBoundsMaxX = 4.55f;
        EditorUtility.SetDirty(boss);
        return go;
    }
}
