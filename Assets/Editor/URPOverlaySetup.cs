using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZulfarakRPG;

// Tools > ZulfarakRPG > Setup URP Overlay Pipeline
// Creates a UniversalRenderPipelineAsset + UniversalRendererData with the
// AlphaMaskFeature registered, then assigns them to GraphicsSettings and every
// QualitySettings level. Without this the AlphaMaskFeature (which writes
// alpha=0 for magenta pixels) never runs and the overlay window stays pink.
public static class URPOverlaySetup
{
    const string SettingsDir   = "Assets/Settings";
    const string PipelinePath  = "Assets/Settings/ZulfarakURP.asset";
    const string RendererPath  = "Assets/Settings/ZulfarakRenderer.asset";

    [MenuItem("Tools/ZulfarakRPG/Setup URP Overlay Pipeline")]
    public static void Setup()
    {
        Directory.CreateDirectory(SettingsDir);

        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, RendererPath);
        }

        if (!HasAlphaMaskFeature(renderer))
        {
            var feature = ScriptableObject.CreateInstance<AlphaMaskFeature>();
            feature.name = "AlphaMaskFeature";
            var shader = Shader.Find("Hidden/ZulfarakRPG/AlphaFromMagenta");
            if (shader != null)
            {
                var so = new SerializedObject(feature);
                var prop = so.FindProperty("maskShader");
                if (prop != null) { prop.objectReferenceValue = shader; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            EditorUtility.SetDirty(renderer);
        }

        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
        }
        else
        {
            SetRendererOnPipeline(pipeline, renderer);
        }

        AssetDatabase.SaveAssets();

        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline         = pipeline;
        int count = QualitySettings.count;
        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < count; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = pipeline;
        }
        QualitySettings.SetQualityLevel(current, false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ZulfarakRPG] URP overlay pipeline configured. Restart Play mode to apply.");
    }

    // Revert to Built-in Render Pipeline. Use this if the generated URP asset
    // breaks rendering (e.g. sprites going invisible because the renderer was
    // created without its default opaque/transparent passes). The magenta chroma
    // key still works at the OS compositor layer without URP.
    [MenuItem("Tools/ZulfarakRPG/Disable URP Overlay Pipeline (Revert to Built-in)")]
    public static void Disable()
    {
        GraphicsSettings.defaultRenderPipeline = null;
        QualitySettings.renderPipeline         = null;
        int count = QualitySettings.count;
        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < count; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = null;
        }
        QualitySettings.SetQualityLevel(current, false);

        // Physically delete the broken pipeline assets so nothing can reference
        // them. Built-in RP takes over once these are gone and the references
        // above are cleared.
        if (AssetDatabase.LoadAssetAtPath<Object>(PipelinePath) != null)
            AssetDatabase.DeleteAsset(PipelinePath);
        if (AssetDatabase.LoadAssetAtPath<Object>(RendererPath) != null)
            AssetDatabase.DeleteAsset(RendererPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ZulfarakRPG] URP overlay pipeline DISABLED + assets deleted. Using Built-in RP. Restart Play mode.");
    }

    static bool HasAlphaMaskFeature(UniversalRendererData renderer)
    {
        foreach (var f in renderer.rendererFeatures)
            if (f is AlphaMaskFeature) return true;
        return false;
    }

    // UniversalRenderPipelineAsset stores its renderer list privately; use reflection
    // to overwrite element 0 when the pipeline already exists.
    static void SetRendererOnPipeline(UniversalRenderPipelineAsset pipeline, ScriptableRendererData renderer)
    {
        var field = typeof(UniversalRenderPipelineAsset).GetField(
            "m_RendererDataList",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(pipeline) is ScriptableRendererData[] arr && arr.Length > 0)
        {
            arr[0] = renderer;
            EditorUtility.SetDirty(pipeline);
        }
    }
}
