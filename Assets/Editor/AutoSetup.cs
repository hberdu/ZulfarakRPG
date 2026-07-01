using UnityEditor;
using UnityEngine;
using System.IO;

namespace ZulfarakRPG
{
    // Runs automatically the first time Unity opens this project.
    // Calls both wizards, then removes the trigger flag so it never runs again.
    [InitializeOnLoad]
    public static class AutoSetup
    {
        private static readonly string DoneFlag =
            Path.Combine(Application.dataPath, "../.zulfarak_setup_done");

        static AutoSetup()
        {
            if (File.Exists(DoneFlag)) return;

            // Defer until Editor is fully initialized (not during domain reload)
            EditorApplication.delayCall += RunSetup;
        }

        private static void RunSetup()
        {
            if (File.Exists(DoneFlag)) return;

            // EditorSceneManager.NewScene cannot run during Play mode
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Debug.Log("[ZulfarakRPG] Primeira abertura detectada — rodando setup automático...");

            try
            {
                ZulfarakSetupWizard.SetupAll();
                PixelArtGenerator.GenerateAll();           // generate UI elements (UIFrame, UIButton)
                CharacterSpriteImporter.ImportAll();       // import Wizard, Archer, Soldier from full pack
                ZulfarakTextureImporter.ImportAll();       // copy & import GameAssets textures
                SceneSetupWizard.SetupAllScenes();

                File.WriteAllText(DoneFlag, System.DateTime.Now.ToString());
                Debug.Log("[ZulfarakRPG] ✓ Setup completo! Abra a cena Bootstrap e aperte Play.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ZulfarakRPG] Erro no setup automático: {e.Message}\n{e.StackTrace}");
                Debug.LogWarning("[ZulfarakRPG] Tente manualmente: Tools > ZulfarakRPG > Setup All Assets e depois Setup All Scenes");
            }
        }

        // Reset flag if user wants to run again
        [MenuItem("Tools/ZulfarakRPG/Resetar Setup (rodar novamente)")]
        public static void ResetSetup()
        {
            if (File.Exists(DoneFlag)) File.Delete(DoneFlag);
            Debug.Log("[ZulfarakRPG] Setup resetado. Feche e reabra o projeto para rodar novamente.");
        }
    }
}
