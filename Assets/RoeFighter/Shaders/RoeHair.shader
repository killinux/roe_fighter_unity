// Hair cards of Rise of Eros characters.
// Stands in for the game's "Pinkcore/Heros/KajiyaKayHair" (and SimpleLit/Hair): grey strand
// texture tinted by _BaseColor, strand occlusion from the AM map, two shifted Kajiya-Kay
// highlights.  Drawn as an anti-aliased cut-out (alpha to coverage) so it writes depth and
// works with shadows, SSAO and depth of field.
Shader "ROE/Hair"
{
    Properties
    {
        [Enum(Opaque, 0, Transparent, 1)] _Surface ("Surface Type (unused, kept for the game's materials)", Float) = 0

        [Header(Base)] [Space(5)]
        _BaseMap ("Albedo (grey strands, alpha)", 2D) = "white" {}
        _BaseColor ("Hair Color", Color) = (1,1,1,1)
        [Toggle(_ALPHATEST_ON)] _EnableAlphaTest ("Alpha Test", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Cast Shadow and Occlusion)] [Space(5)]
        _CastShadowDepthBias ("Depth Bias (x light bias)", Range(0, 30)) = 1
        _CastShadowNormalBias ("Normal Bias (x light bias)", Range(0, 30)) = 0.5
        [Toggle(HAIR_AM)] _EnableAM ("AM map (R occlusion, G cast-shadow mask)", Float) = 0
        _CastShadowMaskCutoff ("Cast Shadow Mask Cutoff", Range(0, 0.95)) = 0.95
        _OcclusionMaskMap ("AM Map", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1

        [Header(Shift Noise)] [Space(5)]
        [Toggle(SHIFTNOISEMAP)] _EnableShiftNoise ("Shift Noise Map", Float) = 0
        _ShiftNoiseMap ("Shift Noise Map", 2D) = "black" {}

        [Header(Primary Highlight)] [Space(5)]
        _PrimaryShift ("Primary Shift", Range(-1, 1)) = 0
        _PrimaryNoiseStrength ("Primary Noise Strength", Range(0, 10)) = 3
        _PrimaryShrink ("Primary Shrink (tightness)", Range(1, 30)) = 10
        _PrimarySpecularColor ("Primary Specular Color", Color) = (0.66,0.66,0.66,1)

        [Header(Secondary Highlight)] [Space(5)]
        _SecondaryShift ("Secondary Shift", Range(-1, 1)) = 0
        _SecondaryNoiseStrength ("Secondary Noise Strength", Range(0, 10)) = 3
        _SecondaryShrink ("Secondary Shrink (tightness)", Range(1, 30)) = 10
        _SecondarySpecularColor ("Secondary Specular Color", Color) = (0.33,0.33,0.33,1)

        [Header(Ours)] [Space(5)]
        _SpecularScale ("Highlight Strength", Range(0, 2)) = 0.1
        _DiffuseWrap ("Diffuse Wrap", Range(0, 1)) = 0.5

        [Header(Normal)] [Space(5)]
        [Toggle(_NORMALMAP)] _EnableNormalMap ("Normal Map", Float) = 0
        _BumpScale ("Scale", Float) = 1
        _BumpMap ("Normal Map", 2D) = "bump" {}

        [Header(Other)] [Space(5)]
        [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows ("Receive Shadows", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Culling", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #include "RoeCore.hlsl"

        TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
        TEXTURE2D(_OcclusionMaskMap);   SAMPLER(sampler_OcclusionMaskMap);
        TEXTURE2D(_ShiftNoiseMap);      SAMPLER(sampler_ShiftNoiseMap);
        TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _OcclusionMaskMap_ST;
            float4 _ShiftNoiseMap_ST;
            float4 _BumpMap_ST;
            half4 _BaseColor;
            half4 _PrimarySpecularColor;
            half4 _SecondarySpecularColor;
            half _Surface;
            half _Cutoff;
            half _CastShadowDepthBias;
            half _CastShadowNormalBias;
            half _CastShadowMaskCutoff;
            half _OcclusionStrength;
            half _PrimaryShift;
            half _PrimaryNoiseStrength;
            half _PrimaryShrink;
            half _SecondaryShift;
            half _SecondaryNoiseStrength;
            half _SecondaryShrink;
            half _SpecularScale;
            half _DiffuseWrap;
            half _BumpScale;
        CBUFFER_END

        half RoeHairAlpha(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
        }

        void RoeClip(float2 uv, float4 positionCS)
        {
        #if defined(_ALPHATEST_ON)
            clip(RoeHairAlpha(uv) - _Cutoff);
        #endif
        }

        void RoeShadowClip(float2 uv)
        {
        #if defined(_ALPHATEST_ON)
            clip(RoeHairAlpha(uv) - _Cutoff);
        #endif
        #if defined(HAIR_AM)
            // only the dense inner cards cast shadows, loose strands do not
            clip(SAMPLE_TEXTURE2D(_OcclusionMaskMap, sampler_OcclusionMaskMap, uv).g - _CastShadowMaskCutoff);
        #endif
        }

        #define ROE_SHADOW_DEPTH_BIAS _CastShadowDepthBias
        #define ROE_SHADOW_NORMAL_BIAS _CastShadowNormalBias
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull [_Cull]
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RoeVert
            #pragma fragment Frag

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment HAIR_AM
            #pragma shader_feature_local_fragment SHIFTNOISEMAP

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            struct RoeSurface
            {
                half3 albedo;
                half3 normalWS;
                half3 strandPrimary;     // strand direction shifted towards the normal
                half3 strandSecondary;
                half occlusion;
            };

            half RoeStrandSpecular(half3 T, half3 V, half3 L, half exponent)
            {
                half3 H = SafeNormalize(L + V);
                half dotTH = dot(T, H);
                half sinTH = sqrt(max(0.0, 1.0 - dotTH * dotTH));
                half dirAtten = smoothstep(-1.0, 0.0, dotTH);
                return dirAtten * pow(sinTH, exponent);
            }

            half3 RoeShadeLight(RoeSurface s, InputData inputData, Light light)
            {
                half3 L = light.direction;
                half3 V = inputData.viewDirectionWS;
                half NdotL = dot(s.normalWS, L);
                half wrapped = saturate((NdotL + _DiffuseWrap) / (1.0 + _DiffuseWrap));
                half3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation);

                half3 color = s.albedo * wrapped;

                half3 spec = _PrimarySpecularColor.rgb * RoeStrandSpecular(s.strandPrimary, V, L, _PrimaryShrink * _PrimaryShrink);
                spec += _SecondarySpecularColor.rgb * s.albedo * RoeStrandSpecular(s.strandSecondary, V, L, _SecondaryShrink * _SecondaryShrink);
                color += spec * (_SpecularScale * saturate(NdotL * 2.0 + 0.5));

                return color * radiance * s.occlusion;
            }

            #define ROE_SURFACE_DIFFUSE(s) ((s).albedo)
            #include "RoeLightLoop.hlsl"

            half4 Frag(RoeVaryings input, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half alpha = tex.a * _BaseColor.a;
            #if defined(_ALPHATEST_ON)
                alpha = RoeCutoutAlpha(alpha, _Cutoff);
            #else
                alpha = 1.0;
            #endif

                half3 normalTS = half3(0, 0, 1);
            #if defined(_NORMALMAP)
                normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv * _BumpMap_ST.xy + _BumpMap_ST.zw), _BumpScale);
            #endif

                half3x3 tangentToWorld = RoeTangentToWorld(input, IS_FRONT_VFACE(frontFace, true, false));
                InputData inputData;
                RoeInitInputData(input, tangentToWorld, normalTS, inputData);

                RoeSurface s;
                s.albedo = tex.rgb * _BaseColor.rgb;
                s.normalWS = inputData.normalWS;
                s.occlusion = 1.0;
            #if defined(HAIR_AM)
                half am = SAMPLE_TEXTURE2D(_OcclusionMaskMap, sampler_OcclusionMaskMap, uv).r;
                s.occlusion = LerpWhiteTo(am, _OcclusionStrength);
            #endif

                half noise = 0.0;
            #if defined(SHIFTNOISEMAP)
                noise = SAMPLE_TEXTURE2D(_ShiftNoiseMap, sampler_ShiftNoiseMap, uv * _ShiftNoiseMap_ST.xy + _ShiftNoiseMap_ST.zw).r - 0.5;
            #endif
                // strands run along V of the card's UV = the bitangent
                half3 strand = normalize(tangentToWorld[1]);
                s.strandPrimary = normalize(strand + s.normalWS * (_PrimaryShift + noise * _PrimaryNoiseStrength));
                s.strandSecondary = normalize(strand + s.normalWS * (_SecondaryShift + noise * _SecondaryNoiseStrength));

                AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, s.occlusion);
                half3 color = inputData.bakedGI * s.albedo * aoFactor.indirectAmbientOcclusion;
                color += RoeDirectLighting(s, inputData, aoFactor);

                color = MixFog(color, inputData.fogCoord);
                return half4(color, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RoeShadowVert
            #pragma fragment RoeShadowFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment HAIR_AM
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "RoePasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RoeDepthVert
            #pragma fragment RoeDepthFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "RoePasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RoeDepthVert
            #pragma fragment RoeDepthNormalsFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #include "RoePasses.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
