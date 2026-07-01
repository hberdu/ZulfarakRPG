using UnityEngine;
using UnityEditor;
using System.IO;

// Tools > ZulfarakRPG > Generate Pixel Art
// Creates all game sprites programmatically in pixel art style.
public static class PixelArtGenerator
{
    private const string Folder = "Assets/Art/PixelArt";

    [MenuItem("Tools/ZulfarakRPG/Generate Pixel Art")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(Application.dataPath + "/../" + Folder);

        GenerateSprite("Mage",     DrawMage,    32, 64);
        GenerateSprite("Warrior",  DrawWarrior, 32, 64);
        GenerateSprite("Archer",   DrawArcher,  32, 64);
        GenerateBackground();
        GenerateUIFrame();
        GenerateButtonSprite();
        GenerateCityBackground();
        GenerateDungeonBackground();
        GenerateGroundSprite();
        GenerateDungeonGround();
        GeneratePortalSprite();

        AssetDatabase.Refresh();
        Debug.Log("[ZulfarakRPG] Pixel art gerado em " + Folder);
    }

    // ── Palette ─────────────────────────────────────────────────────────────
    static Color32 CLEAR       = new Color32(0,   0,   0,   0);
    static Color32 BLACK       = new Color32(15,  10,  5,   255);
    static Color32 SKIN        = new Color32(220, 175, 130, 255);
    static Color32 SKIN_SH     = new Color32(175, 130, 90,  255);
    static Color32 EYE         = new Color32(30,  20,  10,  255);
    static Color32 HAIR        = new Color32(70,  45,  15,  255);
    static Color32 BOOT        = new Color32(55,  35,  15,  255);
    static Color32 BOOT_DK     = new Color32(30,  18,  5,   255);

    // Mage
    static Color32 M_HAT_DK    = new Color32(12,  10,  60,  255);
    static Color32 M_HAT_MID   = new Color32(22,  20,  100, 255);
    static Color32 M_BRIM      = new Color32(200, 195, 220, 255);
    static Color32 M_ROBE      = new Color32(40,  45,  135, 255);
    static Color32 M_ROBE_DK   = new Color32(22,  24,  80,  255);
    static Color32 M_ROBE_HL   = new Color32(70,  80,  180, 255);
    static Color32 M_STAFF     = new Color32(120, 80,  35,  255);
    static Color32 M_ORB       = new Color32(100, 190, 255, 255);
    static Color32 M_ORB_HL    = new Color32(200, 240, 255, 255);
    static Color32 M_ORB_DK    = new Color32(50,  120, 200, 255);

    // Warrior
    static Color32 W_HELM      = new Color32(155, 160, 170, 255);
    static Color32 W_HELM_DK   = new Color32(85,  90,  100, 255);
    static Color32 W_HELM_HL   = new Color32(210, 215, 225, 255);
    static Color32 W_ARM_R     = new Color32(165, 28,  28,  255);
    static Color32 W_ARM_DK    = new Color32(90,  12,  12,  255);
    static Color32 W_ARM_HL    = new Color32(200, 60,  60,  255);
    static Color32 W_STEEL     = new Color32(165, 170, 180, 255);
    static Color32 W_STEEL_DK  = new Color32(90,  95,  105, 255);
    static Color32 W_STEEL_HL  = new Color32(215, 220, 230, 255);
    static Color32 W_SHIELD_B  = new Color32(35,  70,  150, 255);
    static Color32 W_SHIELD_DK = new Color32(20,  40,  90,  255);
    static Color32 W_GOLD      = new Color32(200, 165, 50,  255);

    // Archer
    static Color32 A_GREEN     = new Color32(38,  95,  42,  255);
    static Color32 A_GREEN_DK  = new Color32(22,  58,  25,  255);
    static Color32 A_GREEN_HL  = new Color32(65,  130, 70,  255);
    static Color32 A_HOOD      = new Color32(28,  72,  32,  255);
    static Color32 A_BOW       = new Color32(135, 88,  28,  255);
    static Color32 A_BOW_HL    = new Color32(180, 135, 65,  255);
    static Color32 A_STRING    = new Color32(215, 205, 165, 255);
    static Color32 A_ARROW     = new Color32(175, 135, 75,  255);
    static Color32 A_TIP       = new Color32(155, 158, 170, 255);

    // ── Helpers ─────────────────────────────────────────────────────────────
    static Texture2D _tex;

    static void Px(int x, int y, Color32 c)
    {
        if (x < 0 || x >= _tex.width || y < 0 || y >= _tex.height) return;
        _tex.SetPixel(x, y, c);
    }

    static void Rect(int x, int y, int w, int h, Color32 c)
    {
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                Px(x + dx, y + dy, c);
    }

    static void Column(int x, int y0, int y1, Color32 c)
    {
        for (int y = y0; y <= y1; y++) Px(x, y, c);
    }

    static void Row(int y, int x0, int x1, Color32 c)
    {
        for (int x = x0; x <= x1; x++) Px(x, y, c);
    }

    // Upward-pointing triangle with apex at (cx, yTop), base at yBottom
    static void Triangle(int cx, int yBottom, int yTop, Color32 c)
    {
        int h = yTop - yBottom;
        for (int dy = 0; dy <= h; dy++)
        {
            float progress = (float)dy / Mathf.Max(1, h);
            int halfW = Mathf.Max(1, Mathf.RoundToInt((1f - progress) * 5));
            Row(yBottom + dy, cx - halfW, cx + halfW, c);
        }
    }

    static void Circle(int cx, int cy, int r, Color32 c)
    {
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
                if (dx * dx + dy * dy <= r * r + r / 2)
                    Px(cx + dx, cy + dy, c);
    }

    // ── Mage sprite (32x64) ─────────────────────────────────────────────────
    static void DrawMage(Texture2D tex)
    {
        _tex = tex;

        // Boots (y 0-6)
        Rect(9, 0, 6, 7, BOOT);  Rect(9, 0, 2, 7, BOOT_DK);
        Rect(17, 0, 6, 7, BOOT); Rect(17, 0, 2, 7, BOOT_DK);

        // Robe hem swirl (y 7-15)
        Rect(7, 7, 18, 9, M_ROBE);
        for (int x = 7; x < 25; x += 3) Px(x, 7, M_ROBE_HL);  // hem shine
        Rect(7, 7, 3, 9, M_ROBE_DK);   // left shadow
        Rect(22, 7, 3, 9, M_ROBE_DK);  // right shadow

        // Robe body (y 16-40)
        Rect(8, 16, 16, 25, M_ROBE);
        Rect(8, 16, 3, 25, M_ROBE_DK);  // left shadow
        Rect(21, 16, 3, 25, M_ROBE_DK); // right shadow
        Column(9, 16, 40, M_ROBE_HL);   // left highlight

        // Belt (y 27)
        Row(27, 9, 22, M_ROBE_DK);
        Px(15, 27, M_ORB_DK); Px(16, 27, M_ORB_DK); // buckle

        // Staff (x 24-25, y 8-55)
        Column(24, 8, 55, M_STAFF);
        Column(25, 8, 55, M_STAFF);

        // Orb at staff tip (y 52-60)
        Circle(24, 57, 5, M_ORB_DK);
        Circle(24, 57, 4, M_ORB);
        Circle(24, 58, 2, M_ORB_HL);
        Px(23, 59, M_ORB_HL); Px(24, 60, M_ORB_HL); // glow flare

        // Neck (y 41-43)
        Rect(13, 41, 6, 3, SKIN);

        // Face (y 44-52)
        Rect(11, 44, 10, 9, SKIN);
        Rect(11, 44, 2, 9, SKIN_SH); // left face shadow
        // Eyes
        Px(13, 50, EYE); Px(14, 50, EYE);  // left eye
        Px(18, 50, EYE); Px(19, 50, EYE);  // right eye
        Px(13, 51, EYE);                    // pupil shadow
        Px(18, 51, EYE);
        // Brows
        Row(52, 12, 15, HAIR);
        Row(52, 17, 20, HAIR);
        // Mouth (slight smile)
        Px(14, 45, SKIN_SH); Px(15, 44, SKIN_SH); Px(16, 44, SKIN_SH); Px(17, 45, SKIN_SH);
        // Goatee
        Px(14, 44, HAIR); Px(15, 44, HAIR); Px(16, 44, HAIR);
        Px(15, 43, HAIR);

        // Hat brim (y 53-55, wide)
        Rect(7, 53, 18, 3, M_BRIM);
        Row(53, 7, 24, BLACK);           // bottom outline
        Row(55, 8, 23, M_BRIM);          // top shine

        // Hat cone (y 56-63)
        for (int y = 56; y <= 63; y++)
        {
            int progress = y - 56;
            int hw = Mathf.Max(1, 6 - progress);
            Row(y, 16 - hw, 16 + hw, progress < 4 ? M_HAT_MID : M_HAT_DK);
        }
        // Hat highlight stripe
        for (int y = 56; y <= 63; y++) Px(16, y, M_HAT_MID);
        Px(15, 63, M_HAT_MID);

        // Left arm holding staff
        Rect(6, 24, 3, 8, M_ROBE_DK);  // sleeve
        Rect(6, 24, 1, 8, M_ROBE);     // sleeve highlight
        Rect(6, 22, 2, 3, SKIN);        // hand
    }

    // ── Warrior sprite (32x64) ─────────────────────────────────────────────
    static void DrawWarrior(Texture2D tex)
    {
        _tex = tex;

        // Boots (y 0-7)
        Rect(8, 0, 6, 8, BOOT_DK);
        Rect(18, 0, 6, 8, BOOT_DK);
        Rect(9, 1, 4, 7, BOOT);

        // Leg armor (y 8-22)
        Rect(8, 8, 6, 15, W_STEEL);
        Rect(18, 8, 6, 15, W_STEEL);
        Rect(8, 8, 2, 15, W_STEEL_DK);
        Rect(22, 8, 2, 15, W_STEEL_DK);
        Column(9, 8, 22, W_STEEL_HL);  // leg highlight
        Column(19, 8, 22, W_STEEL_HL);

        // Skirt armor (y 23-33)
        Rect(7, 23, 18, 11, W_ARM_R);
        Rect(7, 23, 3, 11, W_ARM_DK);   // left shadow
        Rect(22, 23, 3, 11, W_ARM_DK);  // right shadow
        // Plate divisions
        Column(13, 23, 33, W_ARM_DK);
        Column(18, 23, 33, W_ARM_DK);
        Row(23, 7, 24, W_ARM_DK);       // top rim gold
        Row(23, 7, 24, W_GOLD);

        // Chest armor (y 34-47)
        Rect(7, 34, 18, 14, W_ARM_R);
        Rect(7, 34, 3, 14, W_ARM_DK);   // left shadow
        Rect(22, 34, 3, 14, W_ARM_DK);  // right shadow
        // Chest plate
        Rect(12, 37, 8, 8, W_STEEL);
        Rect(12, 37, 2, 8, W_STEEL_DK); // plate shadow
        Row(44, 10, 21, W_GOLD);         // shoulder rim
        // Shoulder pads
        Rect(5, 42, 4, 6, W_STEEL);
        Rect(23, 42, 4, 6, W_STEEL);
        Rect(5, 42, 1, 6, W_STEEL_DK);

        // Shield (left, y 22-44)
        Rect(2, 22, 6, 23, W_SHIELD_B);
        Rect(2, 22, 1, 23, W_GOLD);     // left rim
        Rect(7, 22, 1, 23, W_GOLD);     // right rim
        Row(22, 2, 7, W_GOLD);          // top rim
        Row(44, 2, 7, W_GOLD);          // bottom rim
        Circle(5, 33, 2, W_GOLD);       // boss
        Px(5, 33, W_STEEL_HL);

        // Sword (right, y 16-52)
        for (int i = 0; i < 36; i++)
        {
            int sx = 24 + (i > 20 ? 1 : 0);
            int sy = 52 - i;
            Px(sx,     sy, W_STEEL_HL);
            Px(sx + 1, sy, W_STEEL);
        }
        // Crossguard
        Rect(23, 36, 6, 2, W_GOLD);
        // Pommel
        Circle(25, 52, 2, W_GOLD);

        // Neck (y 48-50)
        Rect(13, 48, 6, 3, SKIN);

        // Face visible under visor (y 51-53)
        Rect(11, 51, 10, 3, SKIN);
        Px(13, 52, EYE); Px(14, 52, EYE);   // determined eyes
        Px(18, 52, EYE); Px(19, 52, EYE);

        // Helmet (y 54-63)
        Rect(9, 54, 14, 10, W_HELM);
        Rect(9, 54, 2, 10, W_HELM_DK);     // left shadow
        Rect(21, 54, 2, 10, W_HELM_DK);    // right shadow
        Column(10, 54, 63, W_HELM_HL);      // highlight
        // Visor slit
        Rect(11, 57, 10, 3, BLACK);
        Row(58, 11, 20, W_HELM_DK);         // visor bar
        Px(11, 57, W_HELM_DK); Px(20, 57, W_HELM_DK); // visor sides
        // Cheek guards
        Rect(9, 54, 3, 6, W_HELM_DK);
        Rect(20, 54, 3, 6, W_HELM_DK);
        // Crest / plume
        Rect(14, 62, 4, 2, W_ARM_R);
        Px(14, 63, W_ARM_HL); Px(17, 63, W_ARM_HL);
        // Helmet rim gold
        Row(54, 9, 22, W_GOLD);
    }

    // ── Archer sprite (32x64) ──────────────────────────────────────────────
    static void DrawArcher(Texture2D tex)
    {
        _tex = tex;

        // Boots (y 0-6)
        Rect(10, 0, 5, 7, BOOT);
        Rect(17, 0, 5, 7, BOOT);
        Rect(10, 0, 1, 7, BOOT_DK);
        Rect(21, 0, 1, 7, BOOT_DK);

        // Legs (y 7-20)
        Rect(10, 7, 5, 14, A_GREEN_DK);
        Rect(17, 7, 5, 14, A_GREEN_DK);
        Column(10, 7, 20, A_GREEN);
        Column(17, 7, 20, A_GREEN);

        // Cloak body (y 21-44)
        Rect(8, 21, 16, 24, A_GREEN);
        Rect(8, 21, 3, 24, A_GREEN_DK);     // left shadow
        Rect(21, 21, 3, 24, A_GREEN_DK);    // right shadow
        Column(9, 21, 44, A_GREEN_HL);       // highlight
        // Cloak split at bottom
        Rect(14, 21, 4, 10, A_GREEN_DK);
        // Belt
        Row(35, 9, 22, A_GREEN_DK);
        Px(15, 35, A_BOW); Px(16, 35, A_BOW); // buckle

        // Quiver (right side, y 25-44)
        Rect(23, 25, 5, 20, A_BOW);
        Rect(23, 25, 1, 20, A_BOW_HL);
        // Arrow tips sticking up
        Px(25, 44, A_TIP); Px(26, 44, A_TIP);
        Px(24, 43, A_TIP); Px(27, 43, A_TIP);

        // Bow (left, y 12-52)
        // Left curve
        for (int y = 12; y <= 52; y++)
        {
            float t = (float)(y - 12) / 40f;
            float curve = 4f * t * (1f - t); // parabola
            int bx = 5 - Mathf.RoundToInt(curve * 3.5f);
            Px(bx,     y, A_BOW);
            Px(bx + 1, y, A_BOW_HL);
        }
        // Bow tips
        Row(12, 5, 7, A_BOW); Row(52, 5, 7, A_BOW);
        // Bowstring
        Column(9, 12, 52, A_STRING);
        // Notched arrow
        Row(32, 9, 20, A_ARROW);
        Px(21, 32, A_TIP); Px(22, 32, A_TIP); // arrowhead
        Px(23, 32, A_TIP);
        // Arrow fletching (back end)
        Px(9, 31, A_GREEN); Px(8, 31, A_GREEN);
        Px(9, 33, A_GREEN); Px(8, 33, A_GREEN);

        // Left arm
        Rect(6, 26, 3, 7, A_GREEN_DK);
        Rect(6, 26, 1, 7, A_GREEN);
        Rect(6, 30, 2, 4, SKIN);  // hand gripping bow

        // Neck (y 45-47)
        Rect(13, 45, 6, 3, SKIN);

        // Face (y 48-55)
        Rect(12, 48, 8, 8, SKIN);
        Rect(12, 48, 2, 8, SKIN_SH);  // side shadow
        // Eyes (sharp, focused)
        Px(13, 53, EYE); Px(14, 53, EYE);
        Px(18, 53, EYE); Px(19, 53, EYE);
        Px(14, 54, EYE); Px(18, 54, EYE);     // squinting
        // Furrowed brow
        Row(55, 13, 15, HAIR);
        Row(55, 17, 19, HAIR);
        // Mouth (firm)
        Row(49, 15, 17, SKIN_SH);

        // Hood sides (y 48-63)
        Rect(9, 48, 3, 16, A_HOOD);   // left side
        Rect(20, 48, 3, 16, A_HOOD);  // right side
        // Hood top triangle (y 56-63)
        for (int y = 56; y <= 63; y++)
        {
            int progress = y - 56;
            int hw = 5 - progress / 2;
            if (hw >= 0)
                Row(y, 16 - hw, 16 + hw, A_HOOD);
        }
        // Hood shadow
        Row(48, 9, 11, A_GREEN_DK);
        Row(48, 20, 22, A_GREEN_DK);
        Column(9, 48, 63, A_GREEN_DK);
        Column(22, 48, 63, A_GREEN_DK);
        // Hood highlight
        Column(10, 56, 63, A_GREEN);
    }

    // ── Pixel art background (128x72, desert city) ────────────────────────
    static void GenerateBackground()
    {
        var tex = new Texture2D(128, 72, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        _tex = tex;

        Color32 SKY_TOP   = new Color32(8,   5,   20,  255);
        Color32 SKY_HOR   = new Color32(55,  28,  8,   255);
        Color32 SAND_LITE  = new Color32(185, 145, 72,  255);
        Color32 SAND_DRK   = new Color32(130, 95,  42,  255);
        Color32 BLDG       = new Color32(105, 78,  32,  255);
        Color32 BLDG_SH    = new Color32(62,  45,  16,  255);
        Color32 DOME_BLU   = new Color32(28,  75,  115, 255);
        Color32 DOME_LT    = new Color32(45,  110, 160, 255);
        Color32 WIN        = new Color32(205, 165, 50,  255);
        Color32 WIN_DK     = new Color32(140, 110, 25,  255);
        Color32 STAR       = new Color32(235, 225, 185, 255);

        // Sky gradient
        for (int y = 0; y < 72; y++)
            for (int x = 0; x < 128; x++)
            {
                float t = (float)y / 71f;
                Color c = Color.Lerp((Color)SKY_TOP, (Color)SKY_HOR, Mathf.Pow(t, 0.4f));
                tex.SetPixel(x, y, c);
            }

        // Stars
        int[] sx = { 3, 12, 22, 35, 48, 58, 72, 88, 102, 112, 120 };
        int[] sy = { 65, 60, 68, 55, 70, 62, 67, 58, 65,  71,  62  };
        for (int i = 0; i < sx.Length; i++) Px(sx[i], sy[i], STAR);

        // --- Buildings ---
        // Left mosque complex (x 3-28)
        Rect(3, 40, 22, 25, BLDG);
        Rect(3, 40, 3, 25, BLDG_SH);   // shadow
        Rect(22, 40, 4, 25, BLDG_SH);
        // Main dome
        for (int dy = 0; dy <= 12; dy++)
        {
            float t = (float)dy / 12f;
            int hw = Mathf.RoundToInt((1f - (1f - t) * (1f - t)) * 9);
            Row(39 + dy, 7 + (9 - hw), 7 + 9 + hw, dy > 8 ? DOME_LT : DOME_BLU);
        }
        Px(12, 51, DOME_LT); Px(13, 51, DOME_LT); // dome highlight
        // Minaret (x 24-27)
        Rect(24, 40, 4, 32, BLDG_SH);
        Rect(24, 40, 1, 32, BLDG);
        Px(25, 71, WIN); Px(26, 71, WIN); // minaret tip
        // Windows
        Rect(5,  44, 3, 4, WIN);  Rect(5,  52, 3, 4, WIN);
        Rect(14, 44, 3, 4, WIN);  Rect(14, 52, 3, 4, WIN);
        Rect(5,  44, 1, 4, WIN_DK); Rect(14, 44, 1, 4, WIN_DK);

        // Center buildings (x 40-78)
        Rect(40, 40, 36, 20, BLDG);
        Rect(40, 40, 4, 20, BLDG_SH);
        Rect(72, 40, 4, 20, BLDG_SH);
        // Pointed arches on windows
        for (int wx = 44; wx < 72; wx += 8)
        {
            Rect(wx, 42, 4, 6, WIN_DK);
            Px(wx + 1, 47, WIN); Px(wx + 2, 47, WIN); // arch top
            Px(wx, 46, WIN);     Px(wx + 3, 46, WIN);
        }
        // Upper balcony
        Row(58, 40, 75, BLDG_SH);
        Row(59, 40, 75, BLDG);

        // Right fort/tower (x 90-122)
        Rect(90, 40, 30, 22, BLDG);
        Rect(90, 40, 4, 22, BLDG_SH);
        // Battlements
        for (int bx = 90; bx < 120; bx += 6)
            Rect(bx, 61, 4, 4, BLDG);
        // Gate arch
        Rect(102, 40, 8, 14, BLDG_SH);
        Px(105, 53, BLDG); Px(106, 53, BLDG); // arch key
        // Torches
        Px(100, 50, WIN); Px(101, 51, WIN);
        Px(110, 50, WIN); Px(111, 51, WIN);

        // Sand ground (y 0-39 from bottom = y 0-39 display)
        for (int y = 0; y < 40; y++)
        {
            float t = (float)y / 39f;
            Color c = Color.Lerp((Color)SAND_DRK, (Color)SAND_LITE, t * 0.5f);
            for (int x = 0; x < 128; x++) tex.SetPixel(x, y, c);
        }
        // Dune ripple texture
        for (int y = 2; y < 35; y++)
            for (int x = 0; x < 128; x++)
                if ((x + y * 3) % 7 == 0) tex.SetPixel(x, y, SAND_DRK);

        tex.Apply();
        SaveSprite(tex, "ZulfarakBG", false);
    }

    // ── UI Frame (9-sliced gold border) ───────────────────────────────────
    static void GenerateUIFrame()
    {
        int w = 16, h = 16;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        _tex = tex;

        Color32 DARK  = new Color32(20,  13,  4,  255);
        Color32 GOLD1 = new Color32(180, 140, 45, 255);
        Color32 GOLD2 = new Color32(230, 190, 70, 255);
        Color32 MID   = new Color32(80,  58,  18, 255);

        Rect(0, 0, w, h, DARK);

        // Outer rim
        Row(0, 0, w-1, GOLD1);    Row(h-1, 0, w-1, GOLD1);
        Column(0, 0, h-1, GOLD1); Column(w-1, 0, h-1, GOLD1);
        // Inner rim
        Row(2, 2, w-3, MID);      Row(h-3, 2, w-3, MID);
        Column(2, 2, h-3, MID);   Column(w-3, 2, h-3, MID);
        // Corner gems
        Px(1, 1, GOLD2); Px(w-2, 1, GOLD2);
        Px(1, h-2, GOLD2); Px(w-2, h-2, GOLD2);

        tex.Apply();

        string name = "UIFrame";
        byte[] png = tex.EncodeToPNG();
        string path = Application.dataPath + "/../" + Folder + "/" + name + ".png";
        File.WriteAllBytes(path, png);

        string assetPath = Folder + "/" + name + ".png";
        AssetDatabase.ImportAsset(assetPath);
        var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp != null)
        {
            imp.textureType       = TextureImporterType.Sprite;
            imp.spriteImportMode  = SpriteImportMode.Single;
            imp.filterMode        = FilterMode.Point;
            imp.spriteBorder      = new Vector4(4, 4, 4, 4); // 9-slice
            imp.textureCompression= TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
    }

    // ── Button sprite (9-sliced, golden border) ───────────────────────────
    static void GenerateButtonSprite()
    {
        int w = 16, h = 10;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        _tex = tex;

        Color32 BASE  = new Color32(85,  55,  15,  255);
        Color32 LITE  = new Color32(130, 88,  25,  255);
        Color32 DARK2 = new Color32(45,  28,  7,   255);
        Color32 GOLD  = new Color32(200, 162, 50,  255);

        Rect(0, 0, w, h, BASE);
        Row(h-1, 0, w-1, GOLD);   Row(0, 0, w-1, DARK2);
        Column(0, 0, h-1, GOLD);  Column(w-1, 0, h-1, DARK2);
        Row(h-2, 1, w-2, LITE);   // highlight near top

        tex.Apply();

        string name = "UIButton";
        byte[] png = tex.EncodeToPNG();
        string path = Application.dataPath + "/../" + Folder + "/" + name + ".png";
        File.WriteAllBytes(path, png);

        string assetPath = Folder + "/" + name + ".png";
        AssetDatabase.ImportAsset(assetPath);
        var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp != null)
        {
            imp.textureType       = TextureImporterType.Sprite;
            imp.spriteImportMode  = SpriteImportMode.Single;
            imp.filterMode        = FilterMode.Point;
            imp.spriteBorder      = new Vector4(4, 3, 4, 3);
            imp.textureCompression= TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
    }

    // ── City Background (wide panorama, desert ruins at night) ────────────
    static void GenerateCityBackground()
    {
        // 480x240 — matches game window resolution, pixel art city silhouette
        int W = 480, H = 240;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        _tex = tex;

        Color32 SKY_TOP  = new Color32(4,   2,   12,  255); // near black
        Color32 SKY_HOR  = new Color32(30,  12,  6,   255); // dark amber horizon
        Color32 SAND_DK  = new Color32(80,  55,  20,  255);
        Color32 SAND_LT  = new Color32(130, 95,  42,  255);
        Color32 BLDG_FAR = new Color32(35,  25,  10,  255); // very dark far buildings
        Color32 BLDG_MID = new Color32(55,  38,  14,  255); // mid buildings
        Color32 BLDG_NEAR= new Color32(80,  58,  20,  255); // near buildings
        Color32 WIN      = new Color32(195, 145, 45,  255);
        Color32 TORCH    = new Color32(240, 160, 40,  255);
        Color32 STAR_C   = new Color32(230, 220, 180, 255);

        // Sky gradient
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float t = (float)y / (H - 1);
                tex.SetPixel(x, y, Color.Lerp((Color)SKY_HOR, (Color)SKY_TOP, Mathf.Pow(t, 0.55f)));
            }

        // Stars
        var rng = new System.Random(99);
        for (int i = 0; i < 80; i++)
        {
            int sx = rng.Next(0, W), sy = H / 2 + rng.Next(0, H / 2);
            float b = 0.5f + (float)rng.NextDouble() * 0.5f;
            var sc = new Color32((byte)(STAR_C.r * b), (byte)(STAR_C.g * b), (byte)(STAR_C.b * b), 200);
            tex.SetPixel(sx, sy, sc);
        }

        // Far background: pyramid silhouettes (left + right)
        DrawPyramid(tex, 30,  60, 55, BLDG_FAR);
        DrawPyramid(tex, 380, 45, 40, BLDG_FAR);

        // Mid layer: ruined walls with arches (several sections)
        // Left section
        Rect(10, 70, 90, 100, BLDG_MID);
        Rect(10, 70, 8,  100, new Color32(30,20,8,255));  // left shadow
        // Windows
        for (int wy = 80; wy < 150; wy += 22) for (int wx = 20; wx < 90; wx += 18)
            { Rect(wx,wy,8,12,WIN); Rect(wx,wy+11,4,3,BLDG_MID); } // arch window
        // Column pair
        Rect(100,70,10,100,BLDG_MID); Rect(115,70,10,100,BLDG_MID);
        // Top crenellations
        for (int cx = 10; cx < 130; cx += 12) Rect(cx,168,8,8,BLDG_MID);

        // Center: large gate/arch
        Rect(185,60,110,110,BLDG_NEAR);
        Rect(185,60,10,110,new Color32(40,28,10,255)); // shadow
        // Gate arch opening
        Rect(215,60,50,70,SKY_HOR);   // arch interior
        for (int gy = 128; gy < 132; gy++) Rect(215,gy,50,1,BLDG_NEAR); // arch keystone
        // Pillars flanking gate
        Rect(185,60,18,115,BLDG_MID); Rect(277,60,18,115,BLDG_MID);
        // Torch flames on pillars
        tex.SetPixel(194, 165, TORCH); tex.SetPixel(195,166, TORCH); tex.SetPixel(193,166, TORCH);
        tex.SetPixel(286, 165, TORCH); tex.SetPixel(285,166, TORCH); tex.SetPixel(287,166, TORCH);
        // Top battlements center
        for (int bx = 185; bx < 295; bx += 14) Rect(bx,170,9,12,BLDG_MID);

        // Right section: tower
        Rect(345,55,80,115,BLDG_MID);
        Rect(345,55,8,115,new Color32(30,20,8,255));
        for (int wy = 70; wy < 145; wy += 22)
        {
            Rect(355,wy,10,14,new Color32(25,18,8,255)); // window slot dark
            Rect(356,wy,8,12,WIN);
        }
        for (int bx = 345; bx < 425; bx += 10) Rect(bx,168,7,10,BLDG_MID);

        // Ground: sandy stone
        int groundY = 65;
        for (int y = 0; y < groundY; y++)
            for (int x = 0; x < W; x++)
            {
                float t = (float)y / groundY;
                tex.SetPixel(x, y, Color.Lerp((Color)SAND_DK, (Color)SAND_LT, t * 0.6f));
            }
        // Ground texture pattern
        for (int y = 2; y < groundY - 2; y++)
            for (int x = 0; x < W; x++)
                if ((x * 3 + y * 5) % 11 == 0) tex.SetPixel(x, y, SAND_DK);
        // Ground top edge
        for (int x = 0; x < W; x++) tex.SetPixel(x, groundY, new Color32(100,72,28,255));

        tex.Apply();
        SaveRaw(tex, "CityBG");
    }

    // ── Dungeon Background (dark stone crypt) ─────────────────────────────
    static void GenerateDungeonBackground()
    {
        int W = 480, H = 240;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        _tex = tex;

        Color32 BG   = new Color32(6,  4,  8,  255);
        Color32 WALL = new Color32(28, 20, 32, 255);
        Color32 WALL_DK = new Color32(15, 10, 18, 255);
        Color32 STONE= new Color32(45, 32, 52, 255);
        Color32 TORCH= new Color32(240,160,40, 255);
        Color32 CRACK= new Color32(12, 8, 15, 255);
        Color32 FLOOR= new Color32(20, 14, 24, 255);
        Color32 FLOOR_LT = new Color32(35,24,40,255);
        Color32 RED_GLOW = new Color32(80,8,8,130);

        // Base background
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                tex.SetPixel(x, y, (y > 70) ? WALL : BG);

        // Top ceiling stone blocks
        for (int bx = 0; bx < W; bx += 32)
        {
            Rect(bx, H-20, 32, 20, WALL);
            Rect(bx, H-20, 1, 20, WALL_DK);
            Rect(bx, H-1, 32, 1, STONE);
        }

        // Background stone wall pattern
        for (int row = 0; row < 3; row++)
        {
            int ry = H - 22 - row * 18;
            int offset = (row % 2 == 0) ? 0 : 16;
            for (int bx = -offset; bx < W; bx += 32)
            {
                Rect(bx, ry-16, 30, 16, WALL);
                Rect(bx, ry-16, 1, 16, WALL_DK);
                if (ry - 16 >= 0) tex.SetPixel(bx, ry-1, STONE);
            }
        }

        // Skull decorations on wall
        for (int sx = 60; sx < W - 60; sx += 120)
        {
            int sy = H - 35;
            // Simple skull silhouette
            Rect(sx-4, sy-8, 9, 8, STONE);      // head
            Rect(sx-2, sy-11,5, 3, STONE);       // forehead
            tex.SetPixel(sx-2, sy-5, CRACK);  tex.SetPixel(sx-1, sy-5, CRACK); // left eye
            tex.SetPixel(sx+1, sy-5, CRACK);  tex.SetPixel(sx+2, sy-5, CRACK); // right eye
            Rect(sx-2, sy-2, 5, 2, CRACK);       // mouth
        }

        // Torch sconces on walls
        for (int tx = 80; tx < W; tx += 140)
        {
            int ty = H - 48;
            Rect(tx-2, ty, 5, 8, new Color32(60,40,20,255)); // holder
            // Flame glow
            tex.SetPixel(tx, ty+8, TORCH);
            tex.SetPixel(tx-1, ty+9, TORCH); tex.SetPixel(tx, ty+9, TORCH); tex.SetPixel(tx+1, ty+9, TORCH);
            tex.SetPixel(tx, ty+10, new Color32(255,210,80,200));
            // Glow spread
            for (int gy = -8; gy <= 8; gy++)
                for (int gx = -8; gx <= 8; gx++)
                    if (gx*gx+gy*gy < 40)
                    {
                        int px2 = tx+gx, py2 = ty+9+gy;
                        if (px2>=0&&px2<W&&py2>=0&&py2<H)
                        {
                            var ex = tex.GetPixel(px2, py2);
                            tex.SetPixel(px2, py2, Color.Lerp(ex, new Color(0.8f,0.5f,0.1f,0.18f), 0.3f));
                        }
                    }
        }

        // Floor
        int floorY = 70;
        for (int y = 0; y < floorY; y++)
            for (int x = 0; x < W; x++)
            {
                float t = (float)y/floorY;
                tex.SetPixel(x, y, Color.Lerp((Color)FLOOR, (Color)FLOOR_LT, t * 0.5f));
            }
        // Floor stone tile lines
        for (int y = 5; y < floorY; y += 16)
            for (int x = 0; x < W; x++) tex.SetPixel(x, y, FLOOR_LT);
        for (int x = 0; x < W; x += 32)
            for (int y = 0; y < floorY; y++) if (y%16>2) tex.SetPixel(x, y, WALL_DK);

        // Subtle blood stains
        var bloodRng = new System.Random(77);
        for (int i = 0; i < 5; i++)
        {
            int bx = bloodRng.Next(40, W-40), by = bloodRng.Next(5, 40);
            for (int dy = -5; dy <= 5; dy++)
                for (int dx = -8; dx <= 8; dx++)
                    if (dx*dx/4+dy*dy < 18 && bx+dx>=0&&bx+dx<W&&by+dy>=0)
                        tex.SetPixel(bx+dx, by+dy, Color.Lerp(tex.GetPixel(bx+dx,by+dy), (Color)RED_GLOW, 0.35f));
        }

        tex.Apply();
        SaveRaw(tex, "DungeonBG");
    }

    // ── Ground tile (city stone ground) ──────────────────────────────────
    static void GenerateGroundSprite()
    {
        int W = 64, H = 24;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        _tex = tex;

        Color32 TOP   = new Color32(130, 95,  42,  255);
        Color32 FACE  = new Color32(105, 75,  30,  255);
        Color32 DARK  = new Color32(68,  48,  16,  255);
        Color32 LINE  = new Color32(80,  58,  20,  255);
        Color32 HL    = new Color32(165, 125, 58,  255);

        Rect(0, 0,   W, H,   FACE);   // main face
        Rect(0, H-5, W, 5,   TOP);    // top cap
        Rect(0, H-5, W, 1,   HL);     // highlight at top edge
        Rect(0, H-6, W, 1,   LINE);   // seam
        Rect(0, 0,   W, 3,   DARK);   // bottom shadow
        // Vertical mortar lines
        for (int x = 0; x < W; x += 16)
            for (int y = 3; y < H-5; y++) tex.SetPixel(x, y, DARK);
        // Horizontal crack
        for (int x = 0; x < W; x++) if (x%4!=0) tex.SetPixel(x, H/2, LINE);

        tex.Apply();
        SaveRaw(tex, "GroundCity");
    }

    // ── Dungeon ground tile ───────────────────────────────────────────────
    static void GenerateDungeonGround()
    {
        int W = 64, H = 24;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        _tex = tex;

        Color32 TOP  = new Color32(30,  20,  38,  255);
        Color32 FACE = new Color32(22,  14,  28,  255);
        Color32 DARK = new Color32(12,  8,   16,  255);
        Color32 LINE = new Color32(18,  12,  22,  255);
        Color32 HL   = new Color32(50,  35,  60,  255);

        Rect(0, 0,   W, H,   FACE);
        Rect(0, H-5, W, 5,   TOP);
        Rect(0, H-5, W, 1,   HL);
        Rect(0, H-6, W, 1,   LINE);
        Rect(0, 0,   W, 3,   DARK);
        for (int x = 0; x < W; x += 16)
            for (int y = 3; y < H-5; y++) tex.SetPixel(x, y, DARK);
        for (int x = 0; x < W; x++) if (x%6!=0) tex.SetPixel(x, H/2, LINE);

        tex.Apply();
        SaveRaw(tex, "GroundDungeon");
    }

    // ── Portal sprite (glowing oval doorway) ─────────────────────────────
    static void GeneratePortalSprite()
    {
        int W = 64, H = 96;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        _tex = tex;

        // Clear
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) tex.SetPixel(x, y, Color.clear);

        int cx = W/2, cy = H/2;
        int rx = 22, ry = 40;  // ellipse radii

        // Outer glow (large, translucent)
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float ex = (float)(x-cx)/rx, ey = (float)(y-cy)/ry;
                float d = ex*ex + ey*ey;
                if (d < 2.5f)
                {
                    float a = Mathf.Clamp01(1f - d/2.5f) * 0.35f;
                    tex.SetPixel(x, y, new Color(0.4f, 0.2f, 0.9f, a));
                }
            }
        // Inner portal fill
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float ex = (float)(x-cx)/rx, ey = (float)(y-cy)/ry;
                float d = ex*ex + ey*ey;
                if (d < 1f)
                {
                    float a = Mathf.Clamp01(1f - d) * 0.75f;
                    float swirl = Mathf.Sin((x-cx)*0.4f + (y-cy)*0.4f) * 0.5f + 0.5f;
                    tex.SetPixel(x, y, new Color(0.3f + swirl*0.2f, 0.05f, 0.8f + swirl*0.2f, a));
                }
            }
        // Rim (bright purple/white ring)
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float ex = (float)(x-cx)/rx, ey = (float)(y-cy)/ry;
                float d = ex*ex + ey*ey;
                if (d >= 0.88f && d < 1.05f)
                    tex.SetPixel(x, y, new Color(0.75f, 0.5f, 1.0f, 0.90f));
            }

        tex.Apply();

        string name = "Portal";
        byte[] png  = tex.EncodeToPNG();
        string path = Application.dataPath + "/../" + Folder + "/" + name + ".png";
        File.WriteAllBytes(path, png);
        string assetPath = Folder + "/" + name + ".png";
        AssetDatabase.ImportAsset(assetPath);
        var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp != null)
        {
            imp.textureType       = TextureImporterType.Sprite;
            imp.spriteImportMode  = SpriteImportMode.Single;
            imp.filterMode        = FilterMode.Point;
            imp.textureCompression= TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
    }

    // ── Pyramid helper for city background ───────────────────────────────
    static void DrawPyramid(Texture2D tex, int baseCx, int baseY, int halfW, Color32 c)
    {
        int height = halfW;
        for (int dy = 0; dy <= height; dy++)
        {
            float t = (float)dy / height;
            int w = Mathf.RoundToInt(halfW * (1f - t));
            for (int x = baseCx - w; x <= baseCx + w; x++)
                if (x >= 0 && x < tex.width && baseY + dy < tex.height)
                    tex.SetPixel(x, baseY + dy, c);
        }
    }

    static void SaveRaw(Texture2D tex, string name)
    {
        byte[] png  = tex.EncodeToPNG();
        string path = Application.dataPath + "/../" + Folder + "/" + name + ".png";
        File.WriteAllBytes(path, png);
        string assetPath = Folder + "/" + name + ".png";
        AssetDatabase.ImportAsset(assetPath);
        var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp == null) return;
        imp.textureType        = TextureImporterType.Sprite;
        imp.spriteImportMode   = SpriteImportMode.Single;
        imp.filterMode         = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.maxTextureSize     = 512;
        imp.SaveAndReimport();
    }

    // ── Shared save ───────────────────────────────────────────────────────
    static void GenerateSprite(string name, System.Action<Texture2D> draw, int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] blank = new Color[w * h];
        for (int i = 0; i < blank.Length; i++) blank[i] = Color.clear;
        tex.SetPixels(blank);

        draw(tex);
        tex.Apply();
        SaveSprite(tex, name, true);
    }

    static void SaveSprite(Texture2D tex, string name, bool isCharacter)
    {
        byte[] png = tex.EncodeToPNG();
        string path = Application.dataPath + "/../" + Folder + "/" + name + ".png";
        File.WriteAllBytes(path, png);

        string assetPath = Folder + "/" + name + ".png";
        AssetDatabase.ImportAsset(assetPath);
        var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp == null) return;
        imp.textureType       = TextureImporterType.Sprite;
        imp.spriteImportMode  = SpriteImportMode.Single;
        imp.filterMode        = FilterMode.Point;
        imp.textureCompression= TextureImporterCompression.Uncompressed;
        if (isCharacter) imp.maxTextureSize = 64;
        imp.SaveAndReimport();
    }
}
