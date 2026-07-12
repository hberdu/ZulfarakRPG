using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using ZulfarakRPG;

// Phase 2-1 (Orc Camp) — builds the settlement scene + its dungeon, reusing the private
// helpers of SceneSetupWizard (partial class). Props come from Assets/Art/Zulfarak (the
// project's exclusive pixel art); enemy sprites come from the Tiny RPG pack via
// CharacterSpriteImporter (import "Orc" and "Orc rider" first).
public static partial class SceneSetupWizard
{
    const string ZulfArt = "Assets/Art/Zulfarak/";

    [MenuItem("Tools/ZulfarakRPG/Setup Phase 2-1 (Orc Camp)")]
    public static void SetupPhase2()
    {
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        SetupCamp2Scene();
        SetupDungeon2Scene();
        AddPhase2ToBuildSettings();
        EditorUtility.DisplayDialog("Fase 2-1",
            "Camp_2_1 e Dungeon_2_1 criadas + adicionadas ao Build Settings.\n\n" +
            "Importe os sprites 'Orc' e 'Orc rider' (Import Character Sprites) se os inimigos sairem invisiveis.\n" +
            "Ligue o teleporte 2-1 no mapa (WorldMapPopup).", "OK");
    }

    // ── Settlement (Camp 2-1): NPCs + wagon + campfire, no buildings/tents ────────────────
    private static void SetupCamp2Scene()
    {
        const float ORTHO = 0.75f, CAM_X = 2.5f, GROUND_TOP = -0.344f, GROUND_CY = -0.494f;
        const float GROUND_W = 5.0f, GROUND_H = 0.30f, SPAWN_Y = GROUND_TOP - 0.80f;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.06f, 0.08f);
        cam.orthographic = true; cam.orthographicSize = ORTHO;
        cam.allowHDR = false; cam.allowMSAA = false;
        camGO.transform.position = new Vector3(CAM_X, 0f, -10f);
        camGO.tag = "MainCamera"; camGO.AddComponent<AudioListener>();

        AddEventSystem();
        var dragCanvas = CreateCanvas("DragCanvas");
        dragCanvas.GetComponent<Canvas>().sortingOrder = 100;
        CreatePanel(dragCanvas, "DragArea", Color.clear, Vec(0, 0.88f), Vec(1, 1)).AddComponent<DragWindow>();

        // Rocky ground (TaskbarHero flat platform, tiled horizontally).
        MakeTiledGround(CAM_X, GROUND_CY, GROUND_W, GROUND_H, "ground_rocky");

        // Camp dressing — rocks (varied sizes), bushes, wagon, animated campfire. No tents/houses.
        PlaceDecoration("Rock_L", EnsureProp(ZulfArt + "rock_big.png"), Color.white, 0.55f, GROUND_TOP, 2.2f, -6);
        PlaceDecoration("Rock_R", EnsureProp(ZulfArt + "rock_big.png"), Color.white, 4.55f, GROUND_TOP, 2.0f, -6);
        PlaceDecoration("Rock_M", EnsureProp(ZulfArt + "rock_med.png"), Color.white, 3.70f, GROUND_TOP, 2.0f, -3);
        PlaceDecoration("Rock_S1", EnsureProp(ZulfArt + "rock_small.png"), Color.white, 1.35f, GROUND_TOP, 2.0f, -2);
        PlaceDecoration("Rock_S2", EnsureProp(ZulfArt + "rock_small.png"), Color.white, 2.95f, GROUND_TOP, 1.6f, -2);
        PlaceDecoration("Bush_L", EnsureProp(ZulfArt + "bush.png"), Color.white, 0.95f, GROUND_TOP, 1.8f, -2);
        PlaceDecoration("Bush_R", EnsureProp(ZulfArt + "bush.png"), Color.white, 4.15f, GROUND_TOP, 1.6f, -2);
        PlaceDecoration("Wagon", EnsureProp(ZulfArt + "wagon.png"), Color.white, 3.30f, GROUND_TOP, 3.0f, -4);
        CreateCampfire(new Vector3(1.95f, GROUND_TOP, 0f));
        ScatterGroundDetail(0.5f, 4.6f, GROUND_TOP, 21,
            new[] { "rock_small.png", "rock_med.png", "bush.png" }, new Color(0.92f, 0.92f, 0.95f));

        // Player + walls + NPCs (reuse city helpers).
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

        // Dungeon portal → Dungeon_2_1, tooltip "2-1".
        var portalGO = CreatePortalGO("DungeonPortal", "Dungeon_2_1",
            new Vector3(4.50f, GROUND_TOP + 0.45f, 0f), openOnStart: true);
        var portal = portalGO.GetComponent<Portal2D>();
        portal.tooltipText = "2-1";
        EditorUtility.SetDirty(portal);

        Save(scene, "Camp_2_1");
    }

    // ── Dungeon 2-1: waves of orcs + Orc Rider boss, extends the camp visual ──────────────
    private static void SetupDungeon2Scene()
    {
        const float ORTHO = 0.75f, CAM_X = 2.5f, GROUND_TOP = -0.313f, GROUND_CY = -0.463f;
        const float GROUND_H = 0.30f, SPAWN_Y = GROUND_TOP - 0.80f;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
        cam.orthographic = true; cam.orthographicSize = ORTHO;
        cam.allowHDR = false; cam.allowMSAA = false;
        camGO.transform.position = new Vector3(CAM_X, 0f, -10f);
        camGO.tag = "MainCamera"; camGO.AddComponent<AudioListener>();

        AddEventSystem();
        var dragCanvas = CreateCanvas("DragCanvas");
        dragCanvas.GetComponent<Canvas>().sortingOrder = 100;
        CreatePanel(dragCanvas, "DragArea", Color.clear, Vec(0, 0.88f), Vec(1, 1)).AddComponent<DragWindow>();

        MakeTiledGround(CAM_X + 1.0f, GROUND_CY, 7.0f, GROUND_H, "ground_rocky");

        // Scattered rocks along the path (props on the ground behind the fight).
        // Clean arena (no path props) — matches the first dungeon; only the layered backdrop shows.
        ScatterGroundDetail(0.5f, 7.0f, GROUND_TOP, 22,
            new[] { "rock_small.png", "rock_med.png", "bush.png" }, new Color(0.9f, 0.9f, 0.94f));

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

        var exitPortalGO = CreatePortalGO("ExitPortal", "Camp_2_1",
            new Vector3(0.50f, GROUND_TOP + 0.45f, 0f), openOnStart: false);
        var exitPortal = exitPortalGO.GetComponent<Portal2D>();

        Directory.CreateDirectory(Application.dataPath + "/../Assets/Prefabs");
        var orcScene = CreateEnemyGO("Orc", "Orc", boss: false);
        var orcPrefab = PrefabUtility.SaveAsPrefabAsset(orcScene, "Assets/Prefabs/Orc.prefab");
        Object.DestroyImmediate(orcScene);
        var riderScene = CreateOrcRiderGO("OrcRider");
        var riderPrefab = PrefabUtility.SaveAsPrefabAsset(riderScene, "Assets/Prefabs/OrcRiderBoss.prefab");
        Object.DestroyImmediate(riderScene);

        var wmGO = new GameObject("WaveManager");
        var wm = wmGO.AddComponent<WaveManager>();
        wm.skeletonPrefab = orcPrefab;          // regular wave enemy
        wm.armoredSkeletonPrefab = orcPrefab;   // (same roster — server has one 'orc')
        wm.necromancerPrefab = riderPrefab;     // wave-10 boss slot = Orc Rider
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
        wm.waveAnnounceText = null;
        EditorUtility.SetDirty(wm);

        Save(scene, "Dungeon_2_1");
    }

    // ── Enemy builders (mirror CreateSkeletonGO / CreateNecromancerGO) ────────────────────
    private static GameObject CreateEnemyGO(string name, string spriteChar, bool boss)
    {
        var go = new GameObject(name);
        go.transform.localScale = new Vector3(2f, 2f, 1f);
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

        var enemy = go.AddComponent<SkeletonEnemy>();
        enemy.idleFrames = CharacterSpriteImporter.GetFrames(spriteChar, "Idle");
        enemy.walkFrames = CharacterSpriteImporter.GetFrames(spriteChar, "Walk");
        enemy.attackFrames = CharacterSpriteImporter.MergeFrames(
            CharacterSpriteImporter.GetFrames(spriteChar, "Attack01"),
            CharacterSpriteImporter.GetFrames(spriteChar, "Attack02"));
        enemy.deathFrames = CharacterSpriteImporter.GetFrames(spriteChar, "Death");
        enemy.hurtFrames = CharacterSpriteImporter.GetFrames(spriteChar, "Hurt");
        enemy.maxHealth = 550f; enemy.attackDamage = 250f;  // fallback; server catalog overrides
        enemy.moveSpeed = 1.7f;                             // slower general pace
        enemy.sceneBoundsMinX = 0.45f; enemy.sceneBoundsMaxX = 4.55f;
        EditorUtility.SetDirty(enemy);
        return go;
    }

    private static GameObject CreateOrcRiderGO(string name)
    {
        var go = new GameObject(name);
        go.transform.localScale = new Vector3(2.6f, 2.6f, 1f);
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

        var boss = go.AddComponent<OrcRiderBoss>();
        boss.idleFrames = CharacterSpriteImporter.GetFrames("Orc rider", "Idle");
        boss.walkFrames = CharacterSpriteImporter.GetFrames("Orc rider", "Walk");
        boss.attackFrames = CharacterSpriteImporter.MergeFrames(
            CharacterSpriteImporter.GetFrames("Orc rider", "Attack01"),
            CharacterSpriteImporter.GetFrames("Orc rider", "Attack02"));
        boss.deathFrames = CharacterSpriteImporter.GetFrames("Orc rider", "Death");
        boss.hurtFrames = CharacterSpriteImporter.GetFrames("Orc rider", "Hurt");
        boss.maxHealth = 1760f; boss.attackDamage = 325f;   // fallback; server overrides
        boss.moveSpeed = 3.0f; boss.attackCooldown = 1.8f;
        boss.sceneBoundsMinX = 0.45f; boss.sceneBoundsMaxX = 4.55f;
        EditorUtility.SetDirty(boss);
        return go;
    }

    // ── Animated campfire (4-frame spritesheet cycled by SimpleIdleAnim) ──────────────────
    private static void CreateCampfire(Vector3 groundPos)
    {
        const float CAMPFIRE_SCALE = 1.3f;   // smaller than the hero (was 2×, too big)
        var frames = LoadCampfireFrames();
        var go = new GameObject("Campfire");
        go.transform.localScale = new Vector3(CAMPFIRE_SCALE, CAMPFIRE_SCALE, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 0;
        if (frames.Length > 0)
        {
            sr.sprite = frames[0];
            var ab = ZulfarakRPG.SpriteAlphaBounds.Get(frames[0]);
            go.transform.position = new Vector3(groundPos.x, groundPos.y - ab.bottomFromBottom * CAMPFIRE_SCALE, 0f);
            var anim = go.AddComponent<SimpleIdleAnim>();
            anim.frames = frames; anim.fps = 8f;
        }
        else go.transform.position = groundPos;
    }

    private static Sprite[] LoadCampfireFrames()
    {
        const string p = ZulfArt + "campfire.png";
        var ti = AssetImporter.GetAtPath(p) as TextureImporter;
        if (ti != null && ti.spriteImportMode != SpriteImportMode.Multiple)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.spritePixelsPerUnit = 100;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            var meta = new List<SpriteMetaData>();
            for (int i = 0; i < 4; i++)
                meta.Add(new SpriteMetaData
                {
                    name = "campfire_" + i,
                    rect = new Rect(i * 24, 0, 24, 24),
                    pivot = new Vector2(0.5f, 0f),
                    alignment = (int)SpriteAlignment.BottomCenter
                });
            ti.spritesheet = meta.ToArray();
            EditorUtility.SetDirty(ti);
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>()
            .OrderBy(s => s.name).ToArray();
    }

    // Loads a Zulfarak prop sprite, setting a sane pixel-art import (PPU 100, point filter,
    // bottom-center pivot for ground props so PlaceDecoration seats it on the floor).
    private static Sprite EnsureProp(string path, bool bottomPivot = true)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null)
        {
            bool dirty = false;
            if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
            if (ti.spritePixelsPerUnit != 100) { ti.spritePixelsPerUnit = 100; dirty = true; }
            if (ti.filterMode != FilterMode.Point) { ti.filterMode = FilterMode.Point; dirty = true; }
            var want = bottomPivot ? SpriteAlignment.BottomCenter : SpriteAlignment.Center;
            if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            // FullRect mesh so ground sprites render correctly under SpriteDrawMode.Tiled.
            var tset = new TextureImporterSettings();
            ti.ReadTextureSettings(tset);
            if (tset.spriteMeshType != SpriteMeshType.FullRect) { tset.spriteMeshType = SpriteMeshType.FullRect; ti.SetTextureSettings(tset); dirty = true; }
            var so = new SerializedObject(ti);
            var align = so.FindProperty("m_Alignment");
            if (align != null && align.intValue != (int)want) { align.intValue = (int)want; so.ApplyModifiedProperties(); dirty = true; }
            if (dirty) ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static TextMeshProUGUI MakeHudText(GameObject canvas, string name, string text, int size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(canvas.transform, false);
        SetAnchors(go.AddComponent<RectTransform>(), Vec(0, 0.3f), Vec(1, 0.7f));
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = color;
        go.SetActive(false);
        return tmp;
    }

    private static void AddPhase2ToBuildSettings()
    {
        var list = EditorBuildSettings.scenes.ToList();
        void AddIfMissing(string path)
        {
            if (list.All(s => s.path != path))
                list.Add(new EditorBuildSettingsScene(path, true));
        }
        AddIfMissing(SceneFolder + "/Camp_2_1.unity");
        AddIfMissing(SceneFolder + "/Dungeon_2_1.unity");
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log("[ZulfarakRPG] Camp_2_1 + Dungeon_2_1 no Build Settings.");
    }
}
