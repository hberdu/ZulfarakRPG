using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using TMPro;
using System.IO;
using System.Linq;
using ZulfarakRPG;

// Tools > ZulfarakRPG > Setup All Scenes
// Creates Bootstrap, CharacterCreation and Zulfarak scenes with all GameObjects,
// components and cross-references wired up. Run AFTER "Setup All Assets".
public static class SceneSetupWizard
{
    private const string SceneFolder = "Assets/Scenes";

    [MenuItem("Tools/ZulfarakRPG/Setup All Scenes")]
    public static void SetupAllScenes()
    {
        Directory.CreateDirectory(Application.dataPath + "/../" + SceneFolder);

        // WS_EX_LAYERED + LWA_COLORKEY transparency only works on BitBlt swap chains;
        // DXGI Flip Model (Unity's default) silently ignores it. Disabling here so the
        // next build picks up a layered-window-friendly swap chain.
        PlayerSettings.useFlipModelSwapchain = false;

        // Inject the AlphaMaskFeature into all UniversalRendererData assets so URP
        // rewrites the swapchain alpha channel based on the magenta clear color.
        EnsureAlphaMaskFeatureOnUrpRenderers();

        SetupBootstrapScene();
        SetupCharacterCreationScene();
        SetupZulfarakScene();
        SetupDungeonScene();
        SetupBuildSettings();

        AssetDatabase.Refresh();
        Debug.Log("[ZulfarakRPG] Todas as cenas criadas com sucesso!");
    }

    // Rebuilds ONLY the Necromancer boss prefab (from the current sprites/stats) and
    // rewires the existing Dungeon scene's WaveManager — without regenerating the whole
    // scene, so manual scene edits are preserved. Run AFTER "Import Character Sprites".
    [MenuItem("Tools/ZulfarakRPG/Rebuild Necromancer Boss")]
    public static void RebuildNecromancerBoss()
    {
        var skelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SkeletonEnemy.prefab");
        if (skelPrefab == null)
        {
            Debug.LogError("[ZulfarakRPG] Assets/Prefabs/SkeletonEnemy.prefab não encontrado. " +
                           "Rode 'Import Character Sprites' e 'Setup All Scenes' pelo menos uma vez antes.");
            return;
        }

        var necroFrames = CharacterSpriteImporter.GetFrames("Necromancer", "Idle");
        if (necroFrames == null || necroFrames.Length == 0)
            Debug.LogWarning("[ZulfarakRPG] Sprites do Necromancer não encontradas — rode 'Import Character Sprites' primeiro, senão o boss fica invisível.");

        Directory.CreateDirectory(Application.dataPath + "/../Assets/Prefabs");
        var necroScene  = CreateNecromancerGO("NecromancerBoss", skelPrefab);
        var necroPrefab = PrefabUtility.SaveAsPrefabAsset(necroScene, "Assets/Prefabs/NecromancerBoss.prefab");
        Object.DestroyImmediate(necroScene);

        // Give the user a chance to save any open work, then rewire the Dungeon scene.
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        string dungeonPath = SceneFolder + "/Dungeon.unity";
        var scene = EditorSceneManager.OpenScene(dungeonPath, OpenSceneMode.Single);
        var wm = Object.FindAnyObjectByType<WaveManager>();
        if (wm != null)
        {
            wm.necromancerPrefab = necroPrefab;
            EditorUtility.SetDirty(wm);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ZulfarakRPG] NecromancerBoss.prefab reconstruído e religado ao WaveManager da cena Dungeon.");
        }
        else
        {
            Debug.LogError("[ZulfarakRPG] WaveManager não encontrado na cena Dungeon — o prefab foi salvo, mas religue manualmente o campo 'Necromancer Prefab'.");
        }

        AssetDatabase.Refresh();
    }

    // Injects an `AlphaMaskFeature` instance into every UniversalRendererData asset in
    // the project so URP rewrites the swap-chain alpha channel based on the magenta
    // clear color. Combined with OverlayWindow's DwmExtendFrameIntoClientArea trick,
    // magenta pixels become transparent and the desktop is visible behind the game.
    private static void EnsureAlphaMaskFeatureOnUrpRenderers()
    {
        var guids = AssetDatabase.FindAssets("t:UniversalRendererData");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null) continue;
            if (data.rendererFeatures.Any(f => f is AlphaMaskFeature)) continue;

            var feature = ScriptableObject.CreateInstance<AlphaMaskFeature>();
            feature.name = "AlphaMaskFeature";
            data.rendererFeatures.Add(feature);
            AssetDatabase.AddObjectToAsset(feature, data);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log("[ZulfarakRPG] AlphaMaskFeature added to URP renderer: " + path);
        }
    }

    // ══════════════════════════════════════════════════════════
    // BOOTSTRAP
    // ══════════════════════════════════════════════════════════
    private static void SetupBootstrapScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera (Bootstrap has none normally, but Input System needs it) ──
        var camGO = new GameObject("Main Camera");
        camGO.AddComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        AddEventSystem();

        // ── Managers root ──────────────────────────────────────
        var managers = new GameObject("Managers");

        managers.AddComponent<GameBootstrap>();
        managers.AddComponent<OverlayWindow>();  // overlay + always-on-top

        // Individual manager objects (GameBootstrap also creates them at runtime,
        // but having them in scene lets the Inspector expose their fields)
        Add<SteamIntegration>(managers,   "SteamIntegration");
        Add<PlayerManager>(managers,      "PlayerManager");
        Add<GuildManager>(managers,       "GuildManager");

        var nm = Add<NetworkManager>(managers, "NetworkManager");
        nm.serverUrl = "ws://localhost:3000";

        Add<LobbyManager>(managers,  "LobbyManager");
        Add<IdleCombat>(managers,    "IdleCombat");

        var mm = Add<MissionManager>(managers, "MissionManager");

        // Wire MissionManager with the two MissionData assets created by ZulfarakSetupWizard
        var missions = new System.Collections.Generic.List<MissionData>();
        foreach (var guid in AssetDatabase.FindAssets("t:MissionData", new[] { "Assets/ScriptableObjects/Missions" }))
            missions.Add(AssetDatabase.LoadAssetAtPath<MissionData>(AssetDatabase.GUIDToAssetPath(guid)));
        mm.allMissions = missions.ToArray();

        EditorUtility.SetDirty(mm);

        Save(scene, "Bootstrap");
    }

    // ══════════════════════════════════════════════════════════
    // CHARACTER CREATION  (Mu Online style: class + name only)
    // ══════════════════════════════════════════════════════════
    private static void SetupCharacterCreationScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera ────────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        var cam   = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.02f, 0.04f); // near-black with violet hint
        cam.orthographic    = true;
        camGO.tag           = "MainCamera";
        camGO.AddComponent<AudioListener>();

        AddEventSystem();

        var canvas = CreateCanvas("Canvas_CharacterCreation");

        // ══════════════════════════════════════════════════════════
        // BACKGROUND — dark gothic atmosphere
        // ══════════════════════════════════════════════════════════
        var bg = CreatePanel(canvas, "Background", new Color(0.04f, 0.02f, 0.04f, 1f), Vec(0, 0), Vec(1, 1));
        bg.GetComponent<Image>().raycastTarget = false;

        // Subtle vignette (dark edges)
        var vig = CreatePanel(bg, "Vignette", new Color(0f, 0f, 0f, 0.55f), Vec(0, 0), Vec(1, 1));
        vig.GetComponent<Image>().raycastTarget = false;

        // Star field (scattered across upper two-thirds)
        var rng = new System.Random(42);
        for (int si = 0; si < 55; si++)
        {
            float sx = (float)rng.NextDouble();
            float sy = 0.15f + (float)rng.NextDouble() * 0.85f;
            float ss = (float)rng.NextDouble() * 0.006f + 0.002f;
            var st = new GameObject("Star" + si);
            st.transform.SetParent(bg.transform, false);
            SetAnchors(st.AddComponent<RectTransform>(),
                new Vector2(sx, sy), new Vector2(sx + ss * 0.6f, sy + ss));
            float br = 0.45f + (float)rng.NextDouble() * 0.55f;
            var si2 = st.AddComponent<Image>();
            si2.color = new Color(br, br * 0.88f, br * 0.70f, 0.75f);
            si2.raycastTarget = false;
        }

        // Atmospheric horizon glow
        CreatePanel(bg, "HorizonGlow", new Color(0.40f, 0.15f, 0.05f, 0.30f), Vec(0, 0.10f), Vec(1, 0.18f))
            .GetComponent<Image>().raycastTarget = false;

        // Top gold rule
        CreatePanel(canvas, "TopRule", new Color(0.65f, 0.48f, 0.12f, 0.85f), Vec(0, 0.975f), Vec(1, 0.982f))
            .GetComponent<Image>().raycastTarget = false;
        // Bottom gold rule
        CreatePanel(canvas, "BotRule", new Color(0.65f, 0.48f, 0.12f, 0.85f), Vec(0, 0.010f), Vec(1, 0.017f))
            .GetComponent<Image>().raycastTarget = false;

        // ══════════════════════════════════════════════════════════
        // TITLE — anchored at very top
        // ══════════════════════════════════════════════════════════
        // Gold header band
        CreatePanel(canvas, "TitleBand", new Color(0.08f, 0.05f, 0.02f, 0.92f), Vec(0, 0.930f), Vec(1, 0.975f))
            .GetComponent<Image>().raycastTarget = false;
        CreatePanel(canvas, "TitleLineTop", new Color(0.70f, 0.52f, 0.14f, 0.90f), Vec(0.02f, 0.969f), Vec(0.98f, 0.975f))
            .GetComponent<Image>().raycastTarget = false;
        CreatePanel(canvas, "TitleLineBot", new Color(0.70f, 0.52f, 0.14f, 0.90f), Vec(0.02f, 0.930f), Vec(0.98f, 0.934f))
            .GetComponent<Image>().raycastTarget = false;

        var titleTmp = CreateTMP(canvas, "TitleText", "CRIAR PERSONAGEM", 32, FontStyle.Bold)
            .GetComponent<TextMeshProUGUI>();
        titleTmp.color = new Color(0.97f, 0.88f, 0.50f);
        titleTmp.alignment = TextAlignmentOptions.Center;
        Anchor(titleTmp.gameObject, Vec(0.05f, 0.930f), Vec(0.95f, 0.972f), Vector2.zero);

        var subTmp = CreateTMP(canvas, "SubTitle", "Escolha sua classe e comece sua jornada", 13, FontStyle.Normal)
            .GetComponent<TextMeshProUGUI>();
        subTmp.color = new Color(0.70f, 0.58f, 0.35f);
        subTmp.alignment = TextAlignmentOptions.Center;
        Anchor(subTmp.gameObject, Vec(0.05f, 0.905f), Vec(0.95f, 0.930f), Vector2.zero);

        // ══════════════════════════════════════════════════════════
        // CHARACTER SPRITES — load from full pack
        // ══════════════════════════════════════════════════════════
        // Idle frames for static display and animation
        var wizIdle    = CharacterSpriteImporter.GetFrames("Wizard",  "Idle");
        var soldierIdle= CharacterSpriteImporter.GetFrames("Soldier", "Idle");
        var archerIdle = CharacterSpriteImporter.GetFrames("Archer",  "Idle");

        // Attack frames for selection animation (chain multiple attacks)
        var wizAttack    = MergeSprites(
            CharacterSpriteImporter.GetFrames("Wizard",  "Attack01"),
            CharacterSpriteImporter.GetFrames("Wizard",  "Attack02"));
        var soldierAttack = MergeSprites(
            CharacterSpriteImporter.GetFrames("Soldier", "Attack01"),
            CharacterSpriteImporter.GetFrames("Soldier", "Attack02"),
            CharacterSpriteImporter.GetFrames("Soldier", "Attack03"));
        var archerAttack  = MergeSprites(
            CharacterSpriteImporter.GetFrames("Archer",  "Attack01"),
            CharacterSpriteImporter.GetFrames("Archer",  "Attack02"));

        // Initial portrait sprite (idle frame 0 or null)
        Sprite[] initialSprites = {
            wizIdle.Length    > 0 ? wizIdle[0]     : null,
            soldierIdle.Length> 0 ? soldierIdle[0] : null,
            archerIdle.Length > 0 ? archerIdle[0]  : null,
        };

        // UI frame sprites
        Sprite uiFrameSpr = LoadPixelArtSprite("UIFrame");
        Sprite uiBtnSpr   = LoadPixelArtSprite("UIButton");

        // ══════════════════════════════════════════════════════════
        // CLASS CARDS — large dark portrait cards
        // ══════════════════════════════════════════════════════════
        float[] cardXMin  = { 0.015f, 0.345f, 0.675f };
        float   cardW     = 0.315f;

        string[] classNames = { "MAGO",    "GUERREIRO", "ARQUEIRO" };
        string[] classRoles = { "Arcano",  "Combate",   "À Distância" };
        string[] classDescs = {
            "Mestre das artes arcanas. Lança feitiços destruidores de fogo, gelo e raio.",
            "Campeão das batalhas. Força bruta e armadura pesada para proteger aliados.",
            "Fantasma das sombras. Elimina inimigos antes de serem vistos ou ouvidos.",
        };

        // Class color accent (border/glow color when selected)
        Color[] classAccent = {
            new Color(0.30f, 0.45f, 0.90f, 1.0f),   // sapphire blue  (Mage)
            new Color(0.85f, 0.12f, 0.12f, 1.0f),   // blood crimson  (Warrior)
            new Color(0.15f, 0.72f, 0.28f, 1.0f),   // emerald green  (Archer)
        };
        // Card base background
        Color[] cardBg = {
            new Color(0.04f, 0.04f, 0.10f, 0.98f),
            new Color(0.10f, 0.03f, 0.03f, 0.98f),
            new Color(0.03f, 0.08f, 0.04f, 0.98f),
        };
        // Accent tint inside portrait area
        Color[] portraitTint = {
            new Color(0.08f, 0.08f, 0.18f, 1.0f),
            new Color(0.16f, 0.05f, 0.04f, 1.0f),
            new Color(0.04f, 0.12f, 0.06f, 1.0f),
        };

        var cardButtons  = new Button[3];
        var cardBorders  = new Image[3];
        var cardArts     = new Image[3];
        var cardNameTxts = new TextMeshProUGUI[3];
        var cardDescTxts = new TextMeshProUGUI[3];

        for (int i = 0; i < 3; i++)
        {
            // ── Card root ──────────────────────────────────────
            var card = new GameObject($"ClassCard_{classNames[i]}");
            card.transform.SetParent(canvas.transform, false);
            var cardRect = card.AddComponent<RectTransform>();
            SetAnchors(cardRect, Vec(cardXMin[i], 0.155f), Vec(cardXMin[i] + cardW, 0.900f));
            var cardBgImg = card.AddComponent<Image>();
            cardBgImg.color = cardBg[i];
            cardButtons[i] = card.AddComponent<Button>();

            // ── Selection glow border (rendered first = behind everything) ──
            var selBorder = new GameObject("SelectionBorder");
            selBorder.transform.SetParent(card.transform, false);
            SetAnchors(selBorder.AddComponent<RectTransform>(), Vec(-0.02f, -0.008f), Vec(1.02f, 1.008f));
            var borderImg = selBorder.AddComponent<Image>();
            borderImg.color        = new Color(classAccent[i].r, classAccent[i].g, classAccent[i].b, 0f); // invisible until selected
            borderImg.raycastTarget= false;
            cardBorders[i] = borderImg;

            // ── Portrait background (tinted inner area) ───────
            var portBg = new GameObject("PortraitBg");
            portBg.transform.SetParent(card.transform, false);
            SetAnchors(portBg.AddComponent<RectTransform>(), Vec(0, 0.27f), Vec(1, 1f));
            portBg.AddComponent<Image>().color = portraitTint[i];
            portBg.GetComponent<Image>().raycastTarget = false;

            // ── Character portrait image (fills portrait area) ──
            var artGO  = new GameObject("ArtImage");
            artGO.transform.SetParent(card.transform, false);
            var artRect= artGO.AddComponent<RectTransform>();
            // Fill portrait area — anchored to card, leaves room below for name
            SetAnchors(artRect, Vec(0.02f, 0.28f), Vec(0.98f, 0.99f));
            var artImg = artGO.AddComponent<Image>();
            if (initialSprites[i] != null)
            {
                artImg.sprite         = initialSprites[i];
                artImg.preserveAspect = true;
                artImg.color          = Color.white;
            }
            else
            {
                // No sprite yet — show class color placeholder
                artImg.color = classAccent[i] * new Color(0.3f, 0.3f, 0.3f, 0.5f);
            }
            artImg.raycastTarget = false;
            cardArts[i] = artImg;

            // ── Class color top accent bar ─────────────────────
            var topBar = new GameObject("TopAccent");
            topBar.transform.SetParent(card.transform, false);
            SetAnchors(topBar.AddComponent<RectTransform>(), Vec(0, 0.975f), Vec(1, 1f));
            topBar.AddComponent<Image>().color = classAccent[i];
            topBar.GetComponent<Image>().raycastTarget = false;

            // ── Name nameplate (semi-transparent bar between portrait and description) ──
            var nameBand = new GameObject("NameBand");
            nameBand.transform.SetParent(card.transform, false);
            SetAnchors(nameBand.AddComponent<RectTransform>(), Vec(0, 0.245f), Vec(1, 0.285f));
            nameBand.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
            nameBand.GetComponent<Image>().raycastTarget = false;

            // Class role label (small, above class name)
            var roleTmp = CreateTMP(card, "ClassRole", classRoles[i].ToUpper(), 9, FontStyle.Normal)
                .GetComponent<TextMeshProUGUI>();
            roleTmp.color = new Color(classAccent[i].r, classAccent[i].g, classAccent[i].b, 0.85f);
            roleTmp.alignment = TextAlignmentOptions.Center;
            Anchor(roleTmp.gameObject, Vec(0.02f, 0.268f), Vec(0.98f, 0.296f), Vector2.zero);

            // Main class name
            var nameTmp = CreateTMP(card, "ClassName", classNames[i], 22, FontStyle.Bold)
                .GetComponent<TextMeshProUGUI>();
            nameTmp.color = new Color(0.97f, 0.88f, 0.52f);
            nameTmp.alignment = TextAlignmentOptions.Center;
            Anchor(nameTmp.gameObject, Vec(0.02f, 0.220f), Vec(0.98f, 0.268f), Vector2.zero);
            cardNameTxts[i] = nameTmp;

            // Thin accent line between name and description
            var divLine = new GameObject("Divider");
            divLine.transform.SetParent(card.transform, false);
            SetAnchors(divLine.AddComponent<RectTransform>(), Vec(0.08f, 0.216f), Vec(0.92f, 0.221f));
            divLine.AddComponent<Image>().color = new Color(classAccent[i].r, classAccent[i].g, classAccent[i].b, 0.50f);
            divLine.GetComponent<Image>().raycastTarget = false;

            // Description text
            var descTmp = CreateTMP(card, "ClassDesc", classDescs[i], 9, FontStyle.Normal)
                .GetComponent<TextMeshProUGUI>();
            descTmp.color     = new Color(0.68f, 0.60f, 0.42f);
            descTmp.alignment = TextAlignmentOptions.Center;
            Anchor(descTmp.gameObject, Vec(0.04f, 0.01f), Vec(0.96f, 0.215f), Vector2.zero);
            cardDescTxts[i] = descTmp;

            // ── UIFrame pixel border (outermost, rendered last = on top) ─
            if (uiFrameSpr != null)
            {
                var frameGO  = new GameObject("PixelFrame");
                frameGO.transform.SetParent(card.transform, false);
                SetAnchors(frameGO.AddComponent<RectTransform>(), Vec(0, 0), Vec(1, 1));
                var frameImg = frameGO.AddComponent<Image>();
                frameImg.sprite       = uiFrameSpr;
                frameImg.type         = Image.Type.Sliced;
                frameImg.color        = new Color(0.45f, 0.32f, 0.10f, 0.60f);
                frameImg.raycastTarget= false;
            }
        }

        // ══════════════════════════════════════════════════════════
        // NAME INPUT + CONFIRM
        // ══════════════════════════════════════════════════════════
        // Section label
        CreatePanel(canvas, "InputDivTop", new Color(0.55f, 0.40f, 0.10f, 0.60f), Vec(0.05f, 0.148f), Vec(0.95f, 0.153f))
            .GetComponent<Image>().raycastTarget = false;

        var lblTmp = CreateTMP(canvas, "NameLabel", "◈  NOME DO PERSONAGEM  ◈", 12, FontStyle.Bold)
            .GetComponent<TextMeshProUGUI>();
        lblTmp.color = new Color(0.82f, 0.68f, 0.35f);
        lblTmp.alignment = TextAlignmentOptions.Center;
        Anchor(lblTmp.gameObject, Vec(0.15f, 0.128f), Vec(0.85f, 0.152f), Vector2.zero);

        // Input field
        var nameInputGO = CreateInputField(canvas, "NameInput", "Digite seu nome...",
            Vec(0.12f, 0.065f), Vec(0.88f, 0.125f));
        var nameInput = nameInputGO.GetComponent<TMP_InputField>();
        nameInput.characterLimit = 24;

        if (uiFrameSpr != null)
        {
            var ifFrm = new GameObject("InputFrame");
            ifFrm.transform.SetParent(nameInputGO.transform, false);
            SetAnchors(ifFrm.AddComponent<RectTransform>(), Vec(0, 0), Vec(1, 1));
            var ifImg = ifFrm.AddComponent<Image>();
            ifImg.sprite = uiFrameSpr; ifImg.type = Image.Type.Sliced;
            ifImg.color = new Color(0.55f, 0.40f, 0.12f, 0.65f);
            ifImg.raycastTarget = false;
        }

        // Error text
        var errTmp = CreateTMP(canvas, "ErrorText", "", 11, FontStyle.Normal)
            .GetComponent<TextMeshProUGUI>();
        errTmp.color = new Color(1f, 0.30f, 0.30f);
        Anchor(errTmp.gameObject, Vec(0.10f, 0.038f), Vec(0.90f, 0.065f), Vector2.zero);

        // Confirm button
        var confirmGO = CreateButton(canvas, "ConfirmButton", "CRIAR PERSONAGEM", new Color(0.50f, 0.32f, 0.07f));
        Anchor(confirmGO, Vec(0.20f, 0.005f), Vec(0.80f, 0.040f), Vector2.zero);
        if (uiBtnSpr != null)
        {
            var cbi = confirmGO.GetComponent<Image>();
            cbi.sprite = uiBtnSpr; cbi.type = Image.Type.Sliced;
        }

        // ══════════════════════════════════════════════════════════
        // WIRE CharacterCreationUI
        // ══════════════════════════════════════════════════════════
        var ui = canvas.AddComponent<CharacterCreationUI>();
        ui.classButtons          = cardButtons;
        ui.classArtImages        = cardArts;
        ui.classSelectionBorders = cardBorders;
        ui.classNameTexts        = cardNameTxts;
        ui.classDescTexts        = cardDescTxts;
        ui.nameInput             = nameInput;
        ui.confirmButton         = confirmGO.GetComponent<Button>();
        ui.confirmErrorText      = errTmp;

        // Animation frames
        ui.mageIdleFrames    = wizIdle;
        ui.warriorIdleFrames = soldierIdle;
        ui.archerIdleFrames  = archerIdle;
        ui.mageAttackFrames    = wizAttack;
        ui.warriorAttackFrames = soldierAttack;
        ui.archerAttackFrames  = archerAttack;

        EditorUtility.SetDirty(ui);
        Save(scene, "CharacterCreation");
    }

    // Merge multiple sprite arrays into one (for chaining attack animations)
    private static Sprite[] MergeSprites(params Sprite[][] arrays)
    {
        var result = new System.Collections.Generic.List<Sprite>();
        foreach (var arr in arrays)
            if (arr != null) foreach (var s in arr) if (s != null) result.Add(s);
        return result.ToArray();
    }

    // ══════════════════════════════════════════════════════════
    // ZULFARAK — compact single-screen city (fits 400×120 window)
    // ══════════════════════════════════════════════════════════
    private static void SetupZulfarakScene()
    {
        // Window 400×120; ortho=0.75 → camera = 5×1.5 world units (aspect 3.333).
        // BG image is 480×240 (4.8×2.4 wu). We stretch it to fit the camera EXACTLY:
        //   BG_SCX = 5/4.8     → 5.0 wide
        //   BG_SCY = 1.5/2.4   → 1.5 tall (vertically compressed; pixel art tolerates this)
        // CityBG sand horizon is at y=65 of 240 → world y = -0.75 + (65/240)*1.5 = -0.344.
        // Sprites: BottomCenter pivot, 100×100px @100PPU=1.0 unit; FEET_OFFSET=0.30 → col.bottom=+0.325.
        const float ORTHO       = 0.75f;
        const float CAM_X       = 2.5f;
        const float CAM_Y       = 0f;
        const float GROUND_TOP  = -0.344f;
        const float GROUND_CY   = -0.494f;
        const float GROUND_W    = 5.0f;
        const float GROUND_H    = 0.30f;
        const float SPAWN_Y     = GROUND_TOP - 0.80f;   // = -1.144 — collider bottom on ground (2× char scale, FEET_OFFSET=0.40)
        const float BG_SCX      = 5.0f / 4.8f;  // 1.0417 — fills camera width
        const float BG_SCY      = 1.5f / 2.4f;  // 0.625  — fills camera height exactly

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera (FIXED — entire city visible at once) ───────────────
        var camGO = new GameObject("Main Camera");
        var cam   = camGO.AddComponent<Camera>();
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = new Color(0.04f, 0.02f, 0.06f); // dark backdrop
        cam.orthographic     = true;
        cam.orthographicSize = ORTHO;
        cam.allowHDR         = false;
        cam.allowMSAA        = false;
        camGO.transform.position = new Vector3(CAM_X, CAM_Y, -10f);
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();

        AddEventSystem();

        // Drag canvas (transparent top-left strip = drag handle for overlay window)
        var dragCanvas = CreateCanvas("DragCanvas");
        dragCanvas.GetComponent<Canvas>().sortingOrder = 100;
        var dragImg = CreatePanel(dragCanvas, "DragArea", Color.clear, Vec(0, 0.88f), Vec(1, 1));
        dragImg.AddComponent<DragWindow>();

        // ── Background SKIPPED — overlay aesthetic: only the ground platform and
        //     decoration silhouettes show; the rest is transparent in build mode
        //     (OverlayWindow chroma-keys the camera clear color to the desktop).

        // ── Ground — tiled sprite + physics (visually aligns with CityBG sand) ─
        var groundSpr = LoadPixelArtSprite("GroundCity");
        var ground    = new GameObject("Ground");
        ground.transform.position = new Vector3(CAM_X, GROUND_CY, 0f);
        var gsr = ground.AddComponent<SpriteRenderer>();
        gsr.sprite       = groundSpr;
        gsr.drawMode     = SpriteDrawMode.Tiled;
        gsr.size         = new Vector2(GROUND_W, GROUND_H);
        gsr.color        = new Color(0.92f, 0.80f, 0.58f);
        gsr.sortingOrder = -5;
        var gcol    = ground.AddComponent<BoxCollider2D>();
        gcol.size   = new Vector2(GROUND_W, GROUND_H);
        gcol.offset = Vector2.zero;

        // ── City decoration silhouettes (sit on the ground, render BEHIND the player) ──────
        // ── City decoration silhouettes — ZUL'FARRAK desert ruins ('cause sand pyramids slap) ───────────────────────────────────────────────────────────────────────────────────────
        // 3 visual layers: huge silhouettes far back, stepped pyramids + dunes in mid, gate columns + props up close.
        Color cFar  = new Color(0.18f, 0.13f, 0.06f, 1f);  // far jagged dune/mountain silhouettes
        Color cMid  = new Color(0.42f, 0.30f, 0.13f, 1f);  // stepped pyramids + sand dunes
        Color cGate = new Color(0.60f, 0.45f, 0.22f, 1f);  // sandstone gate, arch, columns
        Color cNear = new Color(0.75f, 0.58f, 0.30f, 1f);  // foreground props sitting on the sand
        // Far back — dune/mountain silhouettes flanking a distant pyramid
        PlaceDecoration("Dune_FarL", ZulfarakTextureImporter.Load("TX Plant.png",  "plant_bush3"),      cFar,  0.35f, GROUND_TOP, 1.40f, -10);
        PlaceDecoration("Pyramid_C", ZulfarakTextureImporter.Load("TX Struct.png", "struct_building3"), cFar,  2.50f, GROUND_TOP, 0.78f, -10);
        PlaceDecoration("Dune_FarR", ZulfarakTextureImporter.Load("TX Plant.png",  "plant_bush3"),      cFar,  4.75f, GROUND_TOP, 1.40f, -10);
        // Mid — stepped sandstone pyramids on the sides
        PlaceDecoration("Pyramid_L", ZulfarakTextureImporter.Load("TX Struct.png", "struct_building1"), cMid,  1.05f, GROUND_TOP, 0.55f, -8);
        PlaceDecoration("Pyramid_R", ZulfarakTextureImporter.Load("TX Struct.png", "struct_building2"), cMid,  4.00f, GROUND_TOP, 0.50f, -8);
        PlaceDecoration("Dune_NearL",ZulfarakTextureImporter.Load("TX Plant.png",  "plant_bush2"),      cMid,  1.85f, GROUND_TOP, 0.55f, -8);
        PlaceDecoration("Dune_NearR",ZulfarakTextureImporter.Load("TX Plant.png",  "plant_bush2"),      cMid,  3.20f, GROUND_TOP, 0.55f, -8);
        // Mid-near — gate arch + flanking stone columns (the entrance to the city centre)
        PlaceDecoration("Gate_Arch", ZulfarakTextureImporter.Load("TX Struct.png", "struct_arch_top"), cGate, 2.50f, GROUND_TOP, 0.50f, -6);
        PlaceDecoration("Column_L",  ZulfarakTextureImporter.Load("TX Props.png",  "prop_column"),     cGate, 1.55f, GROUND_TOP, 0.45f, -6);
        PlaceDecoration("Column_R",  ZulfarakTextureImporter.Load("TX Props.png",  "prop_column"),     cGate, 3.45f, GROUND_TOP, 0.45f, -6);
        // Foreground — vases, tablet and statue scattered on the sand
        PlaceDecoration("Vase_L",    ZulfarakTextureImporter.Load("TX Props.png",  "prop_vase_lg"),    cNear, 1.15f, GROUND_TOP, 0.28f, -2);
        PlaceDecoration("Tablet",    ZulfarakTextureImporter.Load("TX Props.png",  "prop_tablet"),     cNear, 2.10f, GROUND_TOP, 0.28f, -2);
        PlaceDecoration("Statue",    ZulfarakTextureImporter.Load("TX Props.png",  "prop_statue"),     cNear, 3.05f, GROUND_TOP, 0.30f, -2);
        PlaceDecoration("Vase_R",    ZulfarakTextureImporter.Load("TX Props.png",  "prop_vase_sm"),    cNear, 3.85f, GROUND_TOP, 0.26f, -2);

        // ── Player (left side) ─────────────────────────────────────────────
        var playerGO = CreatePlayerGO(SPAWN_Y);
        playerGO.transform.position = new Vector3(0.7f, SPAWN_Y, 0f);
        var pc = playerGO.GetComponent<PlayerController2D>();
        pc.soldierIdleFrames   = CharacterSpriteImporter.GetFrames("Soldier", "Idle");
        pc.soldierWalkFrames   = CharacterSpriteImporter.GetFrames("Soldier", "Walk");
        pc.soldierAttackFrames = MergeSprites(
            CharacterSpriteImporter.GetFrames("Soldier", "Attack01"),
            CharacterSpriteImporter.GetFrames("Soldier", "Attack02"));
        pc.wizardIdleFrames    = CharacterSpriteImporter.GetFrames("Wizard", "Idle");
        pc.wizardWalkFrames    = CharacterSpriteImporter.GetFrames("Wizard", "Walk");
        pc.wizardAttackFrames  = MergeSprites(
            CharacterSpriteImporter.GetFrames("Wizard", "Attack01"),
            CharacterSpriteImporter.GetFrames("Wizard", "Attack02"));
        pc.archerIdleFrames    = CharacterSpriteImporter.GetFrames("Archer", "Idle");
        pc.archerWalkFrames    = CharacterSpriteImporter.GetFrames("Archer", "Walk");
        pc.archerAttackFrames  = MergeSprites(
            CharacterSpriteImporter.GetFrames("Archer", "Attack01"),
            CharacterSpriteImporter.GetFrames("Archer", "Attack02"));
        pc.soldierDeathFrames  = CharacterSpriteImporter.GetFrames("Soldier", "Death");
        pc.wizardDeathFrames   = CharacterSpriteImporter.GetFrames("Wizard",  "DEATH");
        pc.archerDeathFrames   = CharacterSpriteImporter.GetFrames("Archer",  "Death");
        pc.soldierHurtFrames   = CharacterSpriteImporter.GetFrames("Soldier", "Hurt");
        pc.wizardHurtFrames    = CharacterSpriteImporter.GetFrames("Wizard",  "Hurt");
        pc.archerHurtFrames    = CharacterSpriteImporter.GetFrames("Archer",  "Hurt");
        pc.sceneBoundsMinX = 0.45f;
        pc.sceneBoundsMaxX = 4.55f;
        EditorUtility.SetDirty(pc);

        // ── Invisible boundary walls (clamp player to camera viewport) ─────
        AddWall("WallLeft",  new Vector3(0.15f, 0f, 0f), new Vector2(0.2f, 3.2f));
        AddWall("WallRight", new Vector3(4.85f, 0f, 0f), new Vector2(0.2f, 3.2f));

        // ── Portal to Dungeon (right side, always open, tagged "1-1" = dungeon 1 / phase 1) ─────
        var dungeonPortalGO = CreatePortalGO("DungeonPortal", "Dungeon",
                       new Vector3(4.50f, GROUND_TOP + 0.45f, 0f), openOnStart: true);
        var dungeonPortal   = dungeonPortalGO.GetComponent<Portal2D>();
        dungeonPortal.tooltipText = "1-1";
        EditorUtility.SetDirty(dungeonPortal);

        // ── City interactables: chest + Kael (rune master) + class-specific skill master ──
        CreateChest(new Vector3(3.85f, GROUND_TOP, 0f), GROUND_TOP);
        CreateKaelNPC(new Vector3(1.55f, SPAWN_Y, 0f), GROUND_TOP);
        CreateClassMaster(new Vector3(2.55f, SPAWN_Y, 0f), GROUND_TOP);

        Save(scene, "Zulfarak");
    }

    static void CreateChest(Vector3 pos, float groundY)
    {
        var go = new GameObject("Chest");
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        var spr = ZulfarakTextureImporter.Load("TX Props.png", "prop_chest")
               ?? ZulfarakTextureImporter.Load("TX Props.png", "prop_vase_lg");
        if (spr == null)
        {
            Debug.LogWarning("[ZulfarakRPG] Chest sprite failed to load — skipping (would have been an invisible collider blocking the player).");
            Object.DestroyImmediate(go);
            return;
        }
        sr.sprite       = spr;
        sr.color        = new Color(0.78f, 0.55f, 0.20f);
        sr.sortingOrder = 0;
        var ab = ZulfarakRPG.SpriteAlphaBounds.Get(spr);
        go.transform.position = new Vector3(pos.x, pos.y - ab.bottomFromBottom * 0.45f, pos.z);
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.6f, 0.5f);
        col.isTrigger = true;  // interaction-only; player walks through
        var inter = go.AddComponent<Interactable2D>();
        inter.tooltipText = "Baú";
        inter.tooltipOffset = new Vector2(0f, 0.20f);
        inter.onClick = () => ZulfarakRPG.NPCMenuUI.Show("Baú", "Inventário em construção.\n\nEm breve você poderá guardar e organizar itens aqui.");
        var snap = go.AddComponent<ZulfarakRPG.GroundSnap>();
        snap.groundY = groundY;
    }

    static void CreateKaelNPC(Vector3 pos, float groundY)
    {
        var go = new GameObject("Kael_NPC");
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(2f, 2f, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        var frames = CharacterSpriteImporter.GetFrames("Wizard", "Idle");
        if (frames != null && frames.Length > 0) sr.sprite = frames[0];
        sr.color        = new Color(0.85f, 0.85f, 0.95f); // pale (white-bearded mage)
        sr.sortingOrder = 1;
        var col = go.AddComponent<BoxCollider2D>();
        col.size   = new Vector2(0.30f, 0.20f);
        col.offset = new Vector2(0f, 0.50f);
        col.isTrigger = true;  // interaction-only; player walks through
        var inter = go.AddComponent<Interactable2D>();
        inter.tooltipText = "Kael";
        inter.tooltipOffset = new Vector2(0f, 0.85f);
        inter.popupTitle = "Kael";
        inter.popupBody  = "Texto de teste — popup do Kael funcionando.\n\nClique ou pressione ESC para fechar.";
        var anim = go.AddComponent<SimpleIdleAnim>();
        anim.frames = frames;
        anim.fps    = 6f;
        // Intentionally no GroundSnap: SPAWN_Y matches the player's grounded Y;
        // alpha-based snap kept finding shadow/cloak bottoms and flying the NPC up-screen.
    }

    static void CreateClassMaster(Vector3 pos, float groundY)
    {
        var go = new GameObject("ClassMaster_NPC");
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(2f, 2f, 1f);
        go.AddComponent<SpriteRenderer>().sortingOrder = 1;
        var col = go.AddComponent<BoxCollider2D>();
        col.size   = new Vector2(0.30f, 0.20f);
        col.offset = new Vector2(0f, 0.50f);
        col.isTrigger = true;  // interaction-only; player walks through
        go.AddComponent<Interactable2D>().tooltipOffset = new Vector2(0f, 0.85f);
        var master = go.AddComponent<ClassMasterNPC>();
        master.warriorIdleFrames = CharacterSpriteImporter.GetFrames("Soldier", "Idle");
        master.mageIdleFrames    = CharacterSpriteImporter.GetFrames("Wizard",  "Idle");
        master.archerIdleFrames  = CharacterSpriteImporter.GetFrames("Archer",  "Idle");
        // Intentionally no GroundSnap: SPAWN_Y matches the player's grounded Y.
    }

    // ══════════════════════════════════════════════════════════
    // DUNGEON — 2D wave combat (single-screen, ortho=0.75)
    // ══════════════════════════════════════════════════════════
    private static void SetupDungeonScene()
    {
        // Window 400×120; ortho=0.75 → camera 5×1.5 world units.
        // BG image 480×240 stretched to fit camera EXACTLY (5×1.5).
        // DungeonBG floor at y=70 of 240 → world y = -0.75 + (70/240)*1.5 = -0.3125.
        const float ORTHO       = 0.75f;
        const float CAM_X       = 2.5f;
        const float CAM_Y       = 0f;
        const float GROUND_TOP  = -0.313f;
        const float GROUND_CY   = -0.463f;
        const float GROUND_W    = 5.0f;
        const float GROUND_H    = 0.30f;
        const float SPAWN_Y     = GROUND_TOP - 0.80f;   // = -1.113 — collider bottom on ground (2× char scale, FEET_OFFSET=0.40)
        const float BG_SCX      = 5.0f / 4.8f;
        const float BG_SCY      = 1.5f / 2.4f;  // 0.625 — fills camera height exactly

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera (FIXED — entire arena visible at once) ────────────
        var camGO = new GameObject("Main Camera");
        var cam   = camGO.AddComponent<Camera>();
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = new Color(0.02f, 0.01f, 0.05f);
        cam.orthographic     = true;
        cam.orthographicSize = ORTHO;
        cam.allowHDR         = false;  // HDR swap chain has no alpha — breaks DWM transparency
        cam.allowMSAA        = false;  // MSAA blends sprite edges with the clear color
        camGO.transform.position = new Vector3(CAM_X, CAM_Y, -10f);
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();

        AddEventSystem();

        // Top-strip drag handle (matches city scene)
        var dragCanvas = CreateCanvas("DragCanvas");
        dragCanvas.GetComponent<Canvas>().sortingOrder = 100;
        var dragArea = CreatePanel(dragCanvas, "DragArea", Color.clear, Vec(0, 0.88f), Vec(1, 1));
        dragArea.AddComponent<DragWindow>();

        // ── Background SKIPPED — chroma-key transparency in build (see OverlayWindow).
        //     The ground tile + decoration silhouettes are the only visuals.

        // ── Ground (extends past the right edge so enemies spawning off-screen have a floor) ─
        var dungGroundSpr = LoadPixelArtSprite("GroundDungeon");
        var ground = new GameObject("Ground");
        // Center shifted +1 to the right so the colider/sprite covers x = 0..7 (camera view 0..5 + spawn lane 5..7).
        ground.transform.position = new Vector3(CAM_X + 1.0f, GROUND_CY, 0f);
        var gsr2 = ground.AddComponent<SpriteRenderer>();
        gsr2.sprite       = dungGroundSpr;
        gsr2.drawMode     = SpriteDrawMode.Tiled;
        gsr2.size         = new Vector2(7.0f, GROUND_H);
        gsr2.color        = new Color(0.22f, 0.16f, 0.30f);
        gsr2.sortingOrder = -5;
        var gcol    = ground.AddComponent<BoxCollider2D>();
        gcol.size   = new Vector2(7.0f, GROUND_H);
        gcol.offset = Vector2.zero;

        // ── Dungeon parallax — THREE layers behind the ground, procedurally re-spawned ──────────
        // All layers spawn items on the SAME GROUND_TOP line, the wizard's PlaceDecoration /
        // ParallaxLayer.SpawnNext call SpriteAlphaBounds.Get(...) to seat the VISIBLE art on
        // the ground (no more floating). Each layer scrolls at its own speed.

        // FAR layer (slowest, deepest) — castle/dune silhouettes.
        var farLayerGO = new GameObject("ParallaxFar");
        var farPL = farLayerGO.AddComponent<ParallaxLayer>();
        farPL.speedFactor = 0.30f;
        farPL.tint        = new Color(0.05f, 0.04f, 0.08f, 1f);
        farPL.sprites = new[] {
            ZulfarakTextureImporter.Load("TX Struct.png", "struct_building1"),
            ZulfarakTextureImporter.Load("TX Struct.png", "struct_building3"),
            ZulfarakTextureImporter.Load("TX Struct.png", "struct_building2"),
            ZulfarakTextureImporter.Load("TX Plant.png",  "plant_bush3"),
        };
        farPL.minScale   = 0.55f;
        farPL.maxScale   = 0.80f;
        farPL.minSpacing = 1.8f;
        farPL.maxSpacing = 3.0f;
        farPL.sortOrder  = -10;
        farPL.groundY    = GROUND_TOP;

        // MID layer — stone walls + small structures, half-speed.
        var midLayerGO = new GameObject("ParallaxMid");
        var midPL = midLayerGO.AddComponent<ParallaxLayer>();
        midPL.speedFactor = 0.65f;
        midPL.tint        = new Color(0.10f, 0.08f, 0.14f, 1f);
        midPL.sprites = new[] {
            ZulfarakTextureImporter.Load("TX Tileset Wall.png", "wall_bldg1"),
            ZulfarakTextureImporter.Load("TX Tileset Wall.png", "wall_bldg3"),
            ZulfarakTextureImporter.Load("TX Tileset Wall.png", "wall_wide"),
            ZulfarakTextureImporter.Load("TX Struct.png",       "struct_arch_sm"),
            ZulfarakTextureImporter.Load("TX Props.png",        "prop_column"),
        };
        midPL.minScale   = 0.40f;
        midPL.maxScale   = 0.55f;
        midPL.minSpacing = 1.3f;
        midPL.maxSpacing = 2.4f;
        midPL.sortOrder  = -8;
        midPL.groundY    = GROUND_TOP;

        // NEAR layer (full speed, foreground silhouettes) — dead trees + rocks at base.
        var nearLayerGO = new GameObject("ParallaxNear");
        var nearPL = nearLayerGO.AddComponent<ParallaxLayer>();
        nearPL.speedFactor = 1.0f;
        nearPL.tint        = new Color(0.17f, 0.14f, 0.21f, 1f);
        nearPL.sprites = new[] {
            ZulfarakTextureImporter.Load("TX Plant.png", "plant_tree1"),
            ZulfarakTextureImporter.Load("TX Plant.png", "plant_tree2"),
            ZulfarakTextureImporter.Load("TX Plant.png", "plant_tree3"),
            ZulfarakTextureImporter.Load("TX Props.png", "prop_rock_lg"),
            ZulfarakTextureImporter.Load("TX Props.png", "prop_rock_sm"),
            ZulfarakTextureImporter.Load("TX Props.png", "prop_pebbles"),
        };
        nearPL.minScale   = 0.32f;
        nearPL.maxScale   = 0.50f;
        nearPL.minSpacing = 0.9f;
        nearPL.maxSpacing = 1.9f;
        nearPL.sortOrder  = -6;
        nearPL.groundY    = GROUND_TOP;

        // ── Player (left side of arena) ───────────────────────────────
        var playerGO = CreatePlayerGO(SPAWN_Y);
        playerGO.transform.position = new Vector3(0.7f, SPAWN_Y, 0f);
        var pc = playerGO.GetComponent<PlayerController2D>();
        pc.soldierIdleFrames   = CharacterSpriteImporter.GetFrames("Soldier", "Idle");
        pc.soldierWalkFrames   = CharacterSpriteImporter.GetFrames("Soldier", "Walk");
        pc.soldierAttackFrames = MergeSprites(
            CharacterSpriteImporter.GetFrames("Soldier", "Attack01"),
            CharacterSpriteImporter.GetFrames("Soldier", "Attack02"));
        pc.wizardIdleFrames    = CharacterSpriteImporter.GetFrames("Wizard", "Idle");
        pc.wizardWalkFrames    = CharacterSpriteImporter.GetFrames("Wizard", "Walk");
        pc.wizardAttackFrames  = MergeSprites(
            CharacterSpriteImporter.GetFrames("Wizard", "Attack01"),
            CharacterSpriteImporter.GetFrames("Wizard", "Attack02"));
        pc.archerIdleFrames    = CharacterSpriteImporter.GetFrames("Archer", "Idle");
        pc.archerWalkFrames    = CharacterSpriteImporter.GetFrames("Archer", "Walk");
        pc.archerAttackFrames  = MergeSprites(
            CharacterSpriteImporter.GetFrames("Archer", "Attack01"),
            CharacterSpriteImporter.GetFrames("Archer", "Attack02"));
        pc.soldierDeathFrames  = CharacterSpriteImporter.GetFrames("Soldier", "Death");
        pc.wizardDeathFrames   = CharacterSpriteImporter.GetFrames("Wizard",  "DEATH");
        pc.archerDeathFrames   = CharacterSpriteImporter.GetFrames("Archer",  "Death");
        pc.soldierHurtFrames   = CharacterSpriteImporter.GetFrames("Soldier", "Hurt");
        pc.wizardHurtFrames    = CharacterSpriteImporter.GetFrames("Wizard",  "Hurt");
        pc.archerHurtFrames    = CharacterSpriteImporter.GetFrames("Archer",  "Hurt");
        pc.sceneBoundsMinX = 0.45f;
        pc.sceneBoundsMaxX = 4.55f;
        EditorUtility.SetDirty(pc);

        // ── Invisible boundary walls ──────────────────────────────────────
        AddWall("WallLeft",  new Vector3(0.15f, 0f, 0f), new Vector2(0.2f, 3.2f));
        AddWall("WallRight", new Vector3(7.10f, 0f, 0f), new Vector2(0.2f, 3.2f));

        // ── Spawn points (right edge / just off-screen, enemies walk left) ──
        var spawnsRoot = new GameObject("SpawnPoints");
        var spawnPts   = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            var sp = new GameObject($"SpawnPoint_{i}");
            sp.transform.SetParent(spawnsRoot.transform, false);
            sp.transform.position = new Vector3(5.2f + i * 0.35f, SPAWN_Y, 0f);
            spawnPts[i] = sp.transform;
        }

        // ── Exit portal (starts CLOSED — left side, opens after all waves clear) ─
        var exitPortalGO = CreatePortalGO("ExitPortal", "Zulfarak",
                                           new Vector3(0.50f, GROUND_TOP + 0.45f, 0f), openOnStart: false);
        var exitPortal   = exitPortalGO.GetComponent<Portal2D>();

        // ── Enemy prefabs ─────────────────────────────────────────────
        Directory.CreateDirectory(Application.dataPath + "/../Assets/Prefabs");

        var skelScene = CreateSkeletonGO("SkeletonEnemy", false);
        var skelPrefab = PrefabUtility.SaveAsPrefabAsset(skelScene, "Assets/Prefabs/SkeletonEnemy.prefab");
        Object.DestroyImmediate(skelScene);

        var armScene = CreateSkeletonGO("ArmoredSkeletonEnemy", true);
        var armPrefab = PrefabUtility.SaveAsPrefabAsset(armScene, "Assets/Prefabs/ArmoredSkeletonEnemy.prefab");
        Object.DestroyImmediate(armScene);

        var necroScene = CreateNecromancerGO("NecromancerBoss", skelPrefab);
        var necroPrefab = PrefabUtility.SaveAsPrefabAsset(necroScene, "Assets/Prefabs/NecromancerBoss.prefab");
        Object.DestroyImmediate(necroScene);

        // ── WaveManager ───────────────────────────────────────────────
        var wmGO = new GameObject("WaveManager");
        var wm   = wmGO.AddComponent<WaveManager>();
        wm.skeletonPrefab        = skelPrefab;
        wm.armoredSkeletonPrefab = armPrefab;
        wm.necromancerPrefab     = necroPrefab;
        wm.spawnPoints           = spawnPts;
        wm.exitPortal            = exitPortal;
        wm.parallaxLayers        = new[] { farPL, midPL, nearPL };
        wm.runScrollSpeed        = 4.0f;
        wm.runScrollDistance     = 12.0f;

        // ── HUD canvas (Screen Space Overlay — only CLEAR and DEFEAT, no wave banner) ──────
        var hudCanvas = CreateCanvas("HUDCanvas");
        var hudScaler = hudCanvas.GetComponent<CanvasScaler>();
        hudScaler.referenceResolution = new Vector2(400, 120);
        hudCanvas.GetComponent<Canvas>().sortingOrder = 10;

        var clearGO = new GameObject("ClearText");
        clearGO.transform.SetParent(hudCanvas.transform, false);
        SetAnchors(clearGO.AddComponent<RectTransform>(), Vec(0, 0.3f), Vec(1, 0.7f));
        var clearTMP = clearGO.AddComponent<TextMeshProUGUI>();
        clearTMP.text      = "CLEAR";
        clearTMP.fontSize  = 64;
        clearTMP.fontStyle = TMPro.FontStyles.Bold;
        clearTMP.alignment = TextAlignmentOptions.Center;
        clearTMP.color     = new Color(1f, 0.92f, 0.15f);
        clearGO.SetActive(false);

        var defeatGO = new GameObject("DefeatText");
        defeatGO.transform.SetParent(hudCanvas.transform, false);
        SetAnchors(defeatGO.AddComponent<RectTransform>(), Vec(0, 0.3f), Vec(1, 0.7f));
        var defeatTMP = defeatGO.AddComponent<TextMeshProUGUI>();
        defeatTMP.text      = "DEFEAT";
        defeatTMP.fontSize  = 56;
        defeatTMP.fontStyle = TMPro.FontStyles.Bold;
        defeatTMP.alignment = TextAlignmentOptions.Center;
        defeatTMP.color     = new Color(0.85f, 0.15f, 0.15f);
        defeatGO.SetActive(false);

        var bossGO = new GameObject("BossText");
        bossGO.transform.SetParent(hudCanvas.transform, false);
        SetAnchors(bossGO.AddComponent<RectTransform>(), Vec(0, 0.3f), Vec(1, 0.7f));
        var bossTMP = bossGO.AddComponent<TextMeshProUGUI>();
        bossTMP.text      = "BOSS";
        bossTMP.fontSize  = 64;
        bossTMP.fontStyle = TMPro.FontStyles.Bold;
        bossTMP.alignment = TextAlignmentOptions.Center;
        bossTMP.color     = new Color(0.90f, 0.15f, 0.15f);
        bossGO.SetActive(false);

        wm.clearText        = clearTMP;
        wm.defeatText       = defeatTMP;
        wm.bossText         = bossTMP;
        wm.waveAnnounceText = null;  // intentionally no wave banner
        EditorUtility.SetDirty(wm);

        Save(scene, "Dungeon");
    }

    // ── 2D helpers (shared by Zulfarak + Dungeon) ─────────────────────────

    // FEET_OFFSET: Y of the VISIBLE feet in sprite local space (0..1 of frame height).
    // CHAR_HEIGHT: vertical extent of the visible character in sprite local space.
    // Together they define the BoxCollider2D: bottom = FEET_OFFSET, top = FEET_OFFSET + CHAR_HEIGHT.
    // Critically, the collider matches the visible art, so WorldHealthBar can anchor the HP bar
    // to the collider TOP (= visible head top).
    const float FEET_OFFSET = 0.40f;
    const float CHAR_HEIGHT = 0.20f;

    private static GameObject CreatePlayerGO(float startY)
    {
        var go = new GameObject("Player");
        go.tag = "Player";

        // 2× scale — character takes more screen real-estate. SPAWN_Y formula uses
        // FEET_OFFSET * 2 so collider bottom still rests on GROUND_TOP after scaling.
        go.transform.localScale = new Vector3(2f, 2f, 1f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.constraints  = RigidbodyConstraints2D.FreezeRotation;

        // Collider matches the VISIBLE character footprint exactly so WorldHealthBar
        // can anchor to collider.bounds.max.y = visible head top.
        var col = go.AddComponent<BoxCollider2D>();
        col.size   = new Vector2(0.30f, CHAR_HEIGHT);
        col.offset = new Vector2(0f, FEET_OFFSET + CHAR_HEIGHT * 0.5f);

        go.AddComponent<SpriteRenderer>().sortingOrder = 1;
        go.transform.position = new Vector3(0.6f, startY, 0f);

        // HealthBar sits just above the character's head. PlayerController2D.Start() calls
        // WorldHealthBar.AttachAbove(_sr, ...) at runtime to set the GREEN color, the exact
        // visible-width sizing (alpha pixel scan), and the tight head-hugging Y — values here
        // are only Editor placeholders.
        var hpGO = new GameObject("HealthBar");
        hpGO.transform.SetParent(go.transform, false);
        hpGO.transform.localPosition = new Vector3(0f, FEET_OFFSET + CHAR_HEIGHT + 0.02f, -0.1f);  // local Y just above collider top
        var whb = hpGO.AddComponent<WorldHealthBar>();
        whb.barHeight = 0.025f;

        go.AddComponent<PlayerController2D>();
        return go;
    }

    private static GameObject CreateSkeletonGO(string name, bool isArmored)
    {
        var go = new GameObject(name);

        // 2× scale to match the player size.
        go.transform.localScale = new Vector3(2f, 2f, 1f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.constraints  = RigidbodyConstraints2D.FreezeRotation;

        go.AddComponent<SpriteRenderer>().sortingOrder = 1;

        // Collider matches the VISIBLE skeleton footprint (see CreatePlayerGO).
        var col = go.AddComponent<BoxCollider2D>();
        col.size   = new Vector2(0.30f, CHAR_HEIGHT);
        col.offset = new Vector2(0f, FEET_OFFSET + CHAR_HEIGHT * 0.5f);

        var hpGO = new GameObject("HealthBar");
        hpGO.transform.SetParent(go.transform, false);
        hpGO.transform.localPosition = new Vector3(0f, FEET_OFFSET + CHAR_HEIGHT + 0.02f, -0.1f);  // local Y just above collider top
        var whb2 = hpGO.AddComponent<WorldHealthBar>();
        whb2.barHeight = 0.025f;

        var enemy = go.AddComponent<SkeletonEnemy>();
        string cn = isArmored ? "ArmoredSkeleton" : "Skeleton";
        enemy.idleFrames   = CharacterSpriteImporter.GetFrames(cn, "Idle");
        enemy.walkFrames   = CharacterSpriteImporter.GetFrames(cn, "Walk");
        enemy.attackFrames = CharacterSpriteImporter.MergeFrames(
            CharacterSpriteImporter.GetFrames(cn, "Attack01"),
            CharacterSpriteImporter.GetFrames(cn, "Attack02"));
        enemy.deathFrames  = CharacterSpriteImporter.GetFrames(cn, "Death");
        enemy.hurtFrames   = CharacterSpriteImporter.GetFrames(cn, "Hurt");
        enemy.maxHealth    = isArmored ? 90f : 50f;
        enemy.attackDamage = isArmored ? 14f : 8f;
        enemy.sceneBoundsMinX = 0.45f;
        enemy.sceneBoundsMaxX = 4.55f;
        EditorUtility.SetDirty(enemy);
        return go;
    }

    private static GameObject CreateNecromancerGO(string name, GameObject minionPrefab)
    {
        var go = new GameObject(name);

        // Slightly bigger than regular enemies so the boss reads as a boss.
        go.transform.localScale = new Vector3(2.6f, 2.6f, 1f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.constraints  = RigidbodyConstraints2D.FreezeRotation;

        go.AddComponent<SpriteRenderer>().sortingOrder = 1;

        var col = go.AddComponent<BoxCollider2D>();
        col.size   = new Vector2(0.30f, CHAR_HEIGHT);
        col.offset = new Vector2(0f, FEET_OFFSET + CHAR_HEIGHT * 0.5f);

        var hpGO = new GameObject("HealthBar");
        hpGO.transform.SetParent(go.transform, false);
        hpGO.transform.localPosition = new Vector3(0f, FEET_OFFSET + CHAR_HEIGHT + 0.02f, -0.1f);
        var whb = hpGO.AddComponent<WorldHealthBar>();
        whb.barHeight = 0.025f;

        var boss = go.AddComponent<NecromancerBoss>();
        boss.idleFrames   = CharacterSpriteImporter.GetFrames("Necromancer", "Idle");
        boss.walkFrames   = CharacterSpriteImporter.GetFrames("Necromancer", "Walk");
        // Ranged caster: the ATTACK animation is the magic cast (Attack02); the boss
        // hurls the separate Magic(projectile) bolt instead of a melee swing.
        boss.attackFrames = CharacterSpriteImporter.GetFrames("Necromancer", "Attack02");
        boss.deathFrames  = CharacterSpriteImporter.GetFrames("Necromancer", "Death");
        boss.hurtFrames   = CharacterSpriteImporter.GetFrames("Necromancer", "Hurt");
        boss.summonFrames = CharacterSpriteImporter.GetFrames("Necromancer", "Summon");
        boss.magicBoltFrames = CharacterSpriteImporter.GetFrames("Necromancer", "MagicBolt");
        boss.minionPrefab = minionPrefab;
        boss.minionsPerSummon = 5;
        boss.maxHealth      = 1200f;   // 3× tankier boss
        boss.attackDamage   = 42f;     // 3× harder-hitting bolts
        boss.attackRange    = 6f;      // ranged (cast AI ignores melee range)
        boss.castMinDistance = 2.4f;   // kites back if the player closes in
        boss.moveSpeed      = 2.2f;
        boss.attackCooldown = 2.2f;
        boss.sceneBoundsMinX = 0.45f;
        boss.sceneBoundsMaxX = 4.55f;
        EditorUtility.SetDirty(boss);
        return go;
    }

    private static GameObject CreatePortalGO(string name, string dest, Vector3 pos, bool openOnStart)
    {
        var go = new GameObject(name);
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

        var portalSpr = LoadPixelArtSprite("Portal");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = portalSpr;
        sr.sortingOrder = 2;

        var cirCol = go.AddComponent<CircleCollider2D>();
        cirCol.isTrigger = true;
        cirCol.radius    = 0.6f;

        var portal = go.AddComponent<Portal2D>();
        portal.destinationScene = dest;
        portal.openOnStart      = openOnStart;
        portal.glowSprite       = sr;
        EditorUtility.SetDirty(portal);
        return go;
    }

    private static void AddWall(string name, Vector3 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
    }

    private static void PlaceSprite(string name, Sprite spr, Color tint, Vector3 pos, Vector3 scale)
    {
        if (spr == null) return;
        var go = new GameObject(name);
        go.transform.position   = pos;
        go.transform.localScale = scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = spr;
        sr.color        = tint;
        sr.sortingOrder = Mathf.RoundToInt((10 - pos.z) * 0.5f);
    }

    // Drops a decorative sprite onto the ground (BottomCenter pivot) with an explicit
    // sortingOrder so the caller controls layering relative to the player (sortOrder=1).
    // Negative sortOrder = behind player; positive = in front. The Y is auto-shifted via
    // SpriteAlphaBounds so the VISIBLE art bottom (not the transparent sprite rect bottom)
    // touches groundTopY — no more floating decorations.
    private static void PlaceDecoration(string name, Sprite spr, Color tint,
                                        float x, float groundTopY, float scale, int sortOrder)
    {
        if (spr == null) return;
        var go = new GameObject(name);
        var ab = ZulfarakRPG.SpriteAlphaBounds.Get(spr);
        float yOffset = -ab.bottomFromBottom * scale;
        go.transform.position   = new Vector3(x, groundTopY + yOffset, 0f);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = spr;
        sr.color        = tint;
        sr.sortingOrder = sortOrder;
    }

    // ══════════════════════════════════════════════════════════
    // BUILD SETTINGS
    // ══════════════════════════════════════════════════════════
    private static void SetupBuildSettings()
    {
        var scenes = new[]
        {
            new EditorBuildSettingsScene(SceneFolder + "/Bootstrap.unity",         true),
            new EditorBuildSettingsScene(SceneFolder + "/CharacterCreation.unity", true),
            new EditorBuildSettingsScene(SceneFolder + "/Zulfarak.unity",          true),
            new EditorBuildSettingsScene(SceneFolder + "/Dungeon.unity",           true),
        };
        EditorBuildSettings.scenes = scenes;
        Debug.Log("[ZulfarakRPG] Build Settings atualizadas com 4 cenas.");
    }

    // ══════════════════════════════════════════════════════════
    // PREFABS
    // ══════════════════════════════════════════════════════════
    private static GameObject CreateSubclassPrefab()
    {
        const string path = "Assets/Prefabs/SubclassButton.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var go  = CreateButton(null, "SubclassButton", "Subclasse", new Color(0.3f, 0.2f, 0.08f));
        go.transform.SetParent(null);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 60);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static GameObject CreateMissionCardPrefab()
    {
        const string path = "Assets/Prefabs/MissionCard.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var card = new GameObject("MissionCard");
        var rect = card.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 80);
        var bg = card.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.10f, 0.05f);

        var label = CreateTMP(card, "MissionLabel", "Missão", 13, FontStyle.Normal);
        label.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.TopLeft;
        Anchor(label, Vec(0.02f, 0.05f), Vec(0.75f, 0.95f), Vector2.zero);

        var btn = CreateButton(card, "StartButton", "Iniciar", new Color(0.5f, 0.35f, 0.1f));
        Anchor(btn, Vec(0.76f, 0.1f), Vec(0.98f, 0.9f), Vector2.zero);

        var prefab = PrefabUtility.SaveAsPrefabAsset(card, path);
        Object.DestroyImmediate(card);
        return prefab;
    }

    private static GameObject CreateMemberEntryPrefab()
    {
        const string path = "Assets/Prefabs/MemberEntry.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var entry = new GameObject("MemberEntry");
        entry.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 40);
        entry.AddComponent<Image>().color = new Color(0.1f, 0.07f, 0.03f, 0.8f);

        var label = CreateTMP(entry, "MemberLabel", "SteamID", 13, FontStyle.Normal);
        Anchor(label, Vec(0.02f, 0.05f), Vec(0.98f, 0.95f), Vector2.zero);

        var prefab = PrefabUtility.SaveAsPrefabAsset(entry, path);
        Object.DestroyImmediate(entry);
        return prefab;
    }

    // ══════════════════════════════════════════════════════════
    // UI HELPERS
    // ══════════════════════════════════════════════════════════
    private static GameObject CreateCanvas(string name)
    {
        var go     = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution  = new Vector2(1280, 720);
        scaler.matchWidthOrHeight   = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    private static GameObject CreatePanel(GameObject parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        if (parent != null) go.transform.SetParent(parent.transform, false);
        SetAnchors(rect, anchorMin, anchorMax);
        var img  = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private static GameObject CreateTMP(GameObject parent, string name, string text, int fontSize, FontStyle style)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        if (parent != null) go.transform.SetParent(parent.transform, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax  = Vector2.zero;
        var tmp  = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.fontStyle = style == FontStyle.Bold   ? FontStyles.Bold :
                        style == FontStyle.Italic ? FontStyles.Italic : FontStyles.Normal;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return go;
    }

    private static GameObject CreateButton(GameObject parent, string name, string label, Color bgColor)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        if (parent != null) go.transform.SetParent(parent.transform, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax  = Vector2.zero;
        var img  = go.AddComponent<Image>();
        img.color = bgColor;
        var btn  = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = bgColor * 1.3f;
        colors.pressedColor     = bgColor * 0.7f;
        btn.colors = colors;
        var txtGO = CreateTMP(go, "Label", label, 14, FontStyle.Bold);
        Anchor(txtGO, Vec(0, 0), Vec(1, 1), Vector2.zero);
        return go;
    }

    private static GameObject CreateInputField(GameObject parent, string name, string placeholder, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        if (parent != null) go.transform.SetParent(parent.transform, false);
        SetAnchors(rect, anchorMin, anchorMax);
        var img  = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.16f, 0.10f);
        var field = go.AddComponent<TMP_InputField>();
        var phGO  = CreateTMP(go, "Placeholder", placeholder, 13, FontStyle.Italic);
        phGO.GetComponent<TextMeshProUGUI>().color = new Color(0.55f, 0.45f, 0.25f);
        var txtGO = CreateTMP(go, "Text", "", 14, FontStyle.Normal);
        field.textViewport  = rect;
        field.textComponent = txtGO.GetComponent<TMP_Text>();
        field.placeholder   = phGO.GetComponent<TMP_Text>();
        return go;
    }

    private static GameObject CreateScrollView(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        go.transform.SetParent(parent.transform, false);
        SetAnchors(rect, anchorMin, anchorMax);
        go.AddComponent<Image>().color = new Color(0, 0, 0, 0.2f);
        var scroll = go.AddComponent<ScrollRect>();

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(go.transform, false);
        var vpRect = viewport.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero; vpRect.offsetMax  = Vector2.zero;
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var cRect = content.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0, 1); cRect.anchorMax = new Vector2(1, 1);
        cRect.pivot     = new Vector2(0.5f, 1);
        cRect.offsetMin = Vector2.zero; cRect.offsetMax = Vector2.zero;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing           = 4;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport     = vpRect;
        scroll.content      = cRect;
        scroll.horizontal   = false;
        scroll.vertical     = true;
        scroll.scrollSensitivity = 20f;

        return go;
    }

    private static GameObject CreateButtonRow(GameObject parent, string name, string[] labels, Vector2 anchorMin, Vector2 anchorMax)
    {
        var row  = new GameObject(name);
        var rect = row.AddComponent<RectTransform>();
        row.transform.SetParent(parent.transform, false);
        SetAnchors(rect, anchorMin, anchorMax);
        var hlg  = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4; hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

        foreach (var lbl in labels)
            CreateButton(row, lbl.Replace(" ", "") + "Btn", lbl, new Color(0.3f, 0.2f, 0.07f));

        return row;
    }

    private static void StyleSlider(Slider slider, Color fillColor)
    {
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(slider.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax  = Vector2.zero;
        bgGO.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f);

        var fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(slider.transform, false);
        var faRect = fillAreaGO.AddComponent<RectTransform>();
        faRect.anchorMin = Vector2.zero; faRect.anchorMax = Vector2.one;
        faRect.offsetMin = Vector2.zero; faRect.offsetMax  = Vector2.zero;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fRect = fillGO.AddComponent<RectTransform>();
        fRect.anchorMin = Vector2.zero; fRect.anchorMax = new Vector2(0, 1);
        fRect.offsetMin = Vector2.zero; fRect.offsetMax  = Vector2.zero;
        fillGO.AddComponent<Image>().color = fillColor;

        slider.fillRect    = fRect;
        slider.handleRect  = null;
        slider.targetGraphic = null;
        slider.transition  = Selectable.Transition.None;
    }

    private static void AddEventSystem()
    {
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();
    }

    private static T Add<T>(GameObject parent, string childName) where T : MonoBehaviour
    {
        var go = new GameObject(childName);
        go.transform.SetParent(parent.transform);
        return go.AddComponent<T>();
    }

    private static void Anchor(GameObject go, Vector2 min, Vector2 max, Vector2 pivot)
    {
        var r = go.GetComponent<RectTransform>();
        if (r == null) return;
        SetAnchors(r, min, max);
        if (pivot != Vector2.zero) r.pivot = pivot;
    }

    private static void SetAnchors(RectTransform r, Vector2 min, Vector2 max)
    {
        r.anchorMin = min; r.anchorMax = max;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    private static void SetColor(GameObject go, Color c)
    {
        var img = go.GetComponent<Image>();
        if (img != null) img.color = c;
    }

    private static Button[] GetButtons(GameObject row)
    {
        var btns = new System.Collections.Generic.List<Button>();
        foreach (Transform child in row.transform)
        {
            var b = child.GetComponent<Button>();
            if (b != null) btns.Add(b);
        }
        return btns.ToArray();
    }

    private static Vector2 Vec(float x, float y) => new Vector2(x, y);

    // Loads a sprite from an asset; pass null spriteName for single-sprite files
    private static Sprite LoadSprite(string assetPath, string spriteName)
    {
        if (spriteName == null)
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        var all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (var a in all)
            if (a is Sprite s && s.name == spriteName) return s;
        return null;
    }

    // Loads a sprite from Assets/Art/PixelArt/  (generated by PixelArtGenerator)
    private static Sprite LoadPixelArtSprite(string name) =>
        AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PixelArt/" + name + ".png");

    // Adds a UIFrame 9-sliced pixel art border as the last child of parent
    private static void AddPixelBorder(GameObject parent, Color tint)
    {
        var frame = LoadPixelArtSprite("UIFrame");
        if (frame == null) return;
        var go   = new GameObject("PixelBorder");
        go.transform.SetParent(parent.transform, false);
        SetAnchors(go.AddComponent<RectTransform>(), Vec(0, 0), Vec(1, 1));
        var img  = go.AddComponent<Image>();
        img.sprite       = frame;
        img.type         = Image.Type.Sliced;
        img.color        = tint;
        img.raycastTarget= false;
    }

    // Adds a horizontal gold pixel-art divider strip inside parent
    private static void AddGoldDivider(GameObject parent, float yNorm, float thickness = 0.007f)
    {
        var go = new GameObject("GoldDivider");
        go.transform.SetParent(parent.transform, false);
        SetAnchors(go.AddComponent<RectTransform>(),
            new Vector2(0.02f, yNorm), new Vector2(0.98f, yNorm + thickness));
        go.AddComponent<Image>().color = new Color(0.65f, 0.48f, 0.12f, 0.75f);
    }

    private static GameObject CreateCitySprite(GameObject parent, string name, Sprite sprite, Color tint, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        go.transform.SetParent(parent.transform, false);
        SetAnchors(rect, anchorMin, anchorMax);
        var img  = go.AddComponent<Image>();
        if (sprite != null)
        {
            img.sprite         = sprite;
            img.color          = tint;
            img.preserveAspect = true;
            img.type           = Image.Type.Simple;
        }
        else
        {
            // Fallback: dim silhouette so layout is visible even without assets
            img.color = tint * new Color(0.25f, 0.25f, 0.25f, 0.60f);
        }
        img.raycastTarget = false;
        return go;
    }

    private static void Save(UnityEngine.SceneManagement.Scene scene, string name)
    {
        string path = SceneFolder + "/" + name + ".unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[ZulfarakRPG] Cena salva: {path}");
    }
}
