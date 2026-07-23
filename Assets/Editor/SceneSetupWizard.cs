using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using ZulfarakRPG;

// Tools > ZulfarakRPG > Relink Player Class Attacks
// The one-off scene/boss setup wizards (Setup All Scenes, Rebuild Necromancer Boss,
// Rebuild Character Creation, Setup Phase 2/3/4) were removed as obsolete. Only the
// class-attack relinker survives, since it re-applies sprite wiring after a sprite import.
public static partial class SceneSetupWizard
{
    private const string SceneFolder = "Assets/Scenes";

    // Merge multiple sprite arrays into one (for chaining attack animations)
    private static Sprite[] MergeSprites(params Sprite[][] arrays)
    {
        var result = new System.Collections.Generic.List<Sprite>();
        foreach (var arr in arrays)
            if (arr != null) foreach (var s in arr) if (s != null) result.Add(s);
        return result.ToArray();
    }

    // Assigns every class's sprite arrays onto a PlayerController2D: idle/walk/death/hurt,
    // the legacy merged attack strip (kept as a fallback), the per-variant attack
    // animations that drive the alternate-attack combo, and the mage/archer projectile
    // art. Shared by the city + dungeon scene setup and the "Relink" menu item.
    private static void WirePlayerClassSprites(PlayerController2D pc)
    {
        // Soldier (Warrior) — three swings cycled per attack.
        pc.soldierIdleFrames    = CharacterSpriteImporter.GetFrames("Soldier", "Idle");
        pc.soldierWalkFrames    = CharacterSpriteImporter.GetFrames("Soldier", "Walk");
        pc.soldierAttack1Frames = CharacterSpriteImporter.GetFrames("Soldier", "Attack01");
        pc.soldierAttack2Frames = CharacterSpriteImporter.GetFrames("Soldier", "Attack02");
        pc.soldierAttack3Frames = CharacterSpriteImporter.GetFrames("Soldier", "Attack03");
        pc.soldierAttackFrames  = MergeSprites(pc.soldierAttack1Frames, pc.soldierAttack2Frames);
        pc.soldierDeathFrames   = CharacterSpriteImporter.GetFrames("Soldier", "Death");
        pc.soldierHurtFrames    = CharacterSpriteImporter.GetFrames("Soldier", "Hurt");

        // Wizard (Mage) — two spells alternated, each with its own projectile sheet.
        pc.wizardIdleFrames     = CharacterSpriteImporter.GetFrames("Wizard", "Idle");
        pc.wizardWalkFrames     = CharacterSpriteImporter.GetFrames("Wizard", "Walk");
        pc.wizardAttack1Frames  = CharacterSpriteImporter.GetFrames("Wizard", "Attack01");
        pc.wizardAttack2Frames  = CharacterSpriteImporter.GetFrames("Wizard", "Attack02");
        pc.wizardAttackFrames   = MergeSprites(pc.wizardAttack1Frames, pc.wizardAttack2Frames);
        pc.wizardMagic1Frames   = CharacterSpriteImporter.GetFrames("Wizard", "Magic1");
        pc.wizardMagic2Frames   = CharacterSpriteImporter.GetFrames("Wizard", "Magic2");
        pc.wizardDeathFrames    = CharacterSpriteImporter.GetFrames("Wizard", "DEATH");
        pc.wizardHurtFrames     = CharacterSpriteImporter.GetFrames("Wizard", "Hurt");

        // Archer — two draws alternated; a set of arrows cycled per shot.
        pc.archerIdleFrames     = CharacterSpriteImporter.GetFrames("Archer", "Idle");
        pc.archerWalkFrames     = CharacterSpriteImporter.GetFrames("Archer", "Walk");
        pc.archerAttack1Frames  = CharacterSpriteImporter.GetFrames("Archer", "Attack01");
        pc.archerAttack2Frames  = CharacterSpriteImporter.GetFrames("Archer", "Attack02");
        pc.archerAttackFrames   = MergeSprites(pc.archerAttack1Frames, pc.archerAttack2Frames);
        pc.archerDeathFrames    = CharacterSpriteImporter.GetFrames("Archer", "Death");
        pc.archerHurtFrames     = CharacterSpriteImporter.GetFrames("Archer", "Hurt");
        pc.arrowVariantSprites  = MergeSprites(
            CharacterSpriteImporter.GetFrames("Archer", "Arrow1"),
            CharacterSpriteImporter.GetFrames("Archer", "Arrow2"),
            CharacterSpriteImporter.GetFrames("Archer", "Arrow3"));

        EditorUtility.SetDirty(pc);
    }

    // Tools > ZulfarakRPG > Relink Player Class Attacks
    // Re-applies the class sprite/attack/projectile wiring onto the PlayerController2D in
    // the Zulfarak and Dungeon scenes WITHOUT regenerating them, so alternate attacks and
    // the new projectiles light up after running "Import Character Sprites". Preserves any
    // other manual scene edits.
    [MenuItem("Tools/ZulfarakRPG/Relink Player Class Attacks")]
    public static void RelinkPlayerClassAttacks()
    {
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        foreach (var sceneName in new[] { "Zulfarak", "Dungeon" })
        {
            string path = SceneFolder + "/" + sceneName + ".unity";
            if (!File.Exists(path)) { Debug.LogWarning($"[ZulfarakRPG] Cena não encontrada: {path}"); continue; }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var pc = Object.FindAnyObjectByType<PlayerController2D>();
            if (pc == null) { Debug.LogWarning($"[ZulfarakRPG] PlayerController2D não encontrado em {sceneName}."); continue; }
            WirePlayerClassSprites(pc);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ZulfarakRPG] Ataques alternativos religados ao jogador em {sceneName}.");
        }
        AssetDatabase.Refresh();
    }
}
