Shader "Hidden/ZulfarakRPG/AlphaFromMagenta"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    float4 FragAlphaMask(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord.xy;
        float4 c  = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

        // Magenta = R high, G low, B high. Anything matching the camera clear becomes
        // alpha 0 so DWM (DwmExtendFrameIntoClientArea) composites it as transparent.
        float isMagenta = step(0.85, c.r) * step(c.g, 0.15) * step(0.85, c.b);
        float alpha     = 1.0 - isMagenta;
        return float4(c.rgb, alpha);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ZulfarakAlphaMask"
            ZWrite Off ZTest Always Cull Off
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment FragAlphaMask
            ENDHLSL
        }
    }
}
