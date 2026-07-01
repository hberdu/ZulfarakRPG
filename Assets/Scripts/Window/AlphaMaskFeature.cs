using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace ZulfarakRPG
{
    // URP Renderer Feature for Unity 6 / URP 17+ (RenderGraph API).
    // After post-processing, we blit the camera color through a material that detects
    // magenta pixels (the cleared background) and rewrites their alpha to 0; every
    // other pixel keeps alpha 1. Combined with OverlayWindow's DwmExtendFrameIntoClientArea
    // call, magenta becomes a true transparent hole over the desktop.
    public class AlphaMaskFeature : ScriptableRendererFeature
    {
        class AlphaMaskPass : ScriptableRenderPass
        {
            public Material material;

            public AlphaMaskPass()
            {
                renderPassEvent             = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var source = resourceData.activeColorTexture;
                if (!source.IsValid()) return;

                var desc = renderGraph.GetTextureDesc(source);
                desc.name            = "_AlphaMaskTemp";
                desc.depthBufferBits = 0;
                var dest = renderGraph.CreateTexture(desc);

                var blitParams = new RenderGraphUtils.BlitMaterialParameters(source, dest, material, 0);
                renderGraph.AddBlitPass(blitParams, passName: "ZulfarakAlphaMask");

                // Swap the camera color so subsequent passes (and the final present) see
                // the alpha-fixed texture rather than the original.
                resourceData.cameraColor = dest;
            }
        }

        public Shader maskShader;
        Material      _material;
        AlphaMaskPass _pass;

        public override void Create()
        {
            if (maskShader == null) maskShader = Shader.Find("Hidden/ZulfarakRPG/AlphaFromMagenta");
            if (maskShader == null) return;
            _material = CoreUtils.CreateEngineMaterial(maskShader);
            _pass     = new AlphaMaskPass { material = _material };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass != null) renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
        }
    }
}
