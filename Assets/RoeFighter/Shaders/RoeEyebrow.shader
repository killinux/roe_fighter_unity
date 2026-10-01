// Eyebrow and eyelash cards of Rise of Eros characters (alpha blended over the face).
// Stands in for the game's "Pinkcore/Heros/Eyebrow".
Shader "ROE/Eyebrow"
{
    Properties
    {
        [Header(Base)] [Space(5)]
        [NoScaleOffset] _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)

        [Header(Cast Shadow)] [Space(5)]
        _CastShadowDepthBias ("Depth Bias", Range(0, 30)) = 1
        _CastShadowNormalBias ("Normal Bias", Range(0, 30)) = 0.5

        [Toggle(_OCCLUSIONMAP)] _EnableOcclusion ("Occlusion", Float) = 0
        _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1

        [Header(Other)] [Space(5)]
        [Toggle(_ALPHAPREMULTIPLY_ON)] _EnablePremultiplyAlpha ("Premultiply Alpha", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Culling", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RoeVert
            #pragma fragment Frag

            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "RoeCore.hlsl"

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_OcclusionMap);   SAMPLER(sampler_OcclusionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _OcclusionMap_ST;
                half4 _BaseColor;
                half _CastShadowDepthBias;
                half _CastShadowNormalBias;
                half _OcclusionStrength;
            CBUFFER_END

            half4 Frag(RoeVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half occlusion = 1.0;
            #if defined(_OCCLUSIONMAP)
                occlusion = LerpWhiteTo(SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).g, _OcclusionStrength);
            #endif

                half3 normalWS = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half wrapped = saturate(dot(normalWS, mainLight.direction) * 0.5 + 0.5);
                half3 light = SampleSH(normalWS)
                            + mainLight.color * (mainLight.distanceAttenuation * lerp(0.5, 1.0, mainLight.shadowAttenuation) * wrapped);

                half3 color = tex.rgb * light * occlusion;
            #if defined(_ALPHAPREMULTIPLY_ON)
                // The game's material combines this with a SrcAlpha blend: colour x alpha^2 over
                // skin x (1 - alpha), which darkens soft edges - brows and lashes read denser.
                color *= tex.a;
            #endif
                half fog = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                color = MixFog(color, fog);
                return half4(color, tex.a);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
