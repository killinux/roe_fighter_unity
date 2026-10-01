// Skin, face and teeth of Rise of Eros characters.
// Stands in for the game's "Pinkcore/Heros/Skin": pre-integrated skin (the game's own LUT,
// indexed by N.L and the curvature stored in the MGAC alpha), a softened normal for diffuse,
// a pore detail normal, and back-light translucency on thin parts.
Shader "ROE/Skin"
{
    Properties
    {
        [Enum(Opaque, 0, Transparent, 1)] _Surface ("Surface Type", Float) = 0

        [Header(Base)] [Space(5)]
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        [Toggle(_ALPHATEST_ON)] _EnableAlphaTest ("Alpha Test", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Cast Shadow)] [Space(5)]
        _CastShadowDepthBias ("Depth Bias (x light bias)", Range(0, 30)) = 1
        _CastShadowNormalBias ("Normal Bias (x light bias)", Range(0, 30)) = 0.5

        [Header(Skin)] [Space(5)]
        [NoScaleOffset] _SkinLutMap ("Skin LUT (u: N.L, v: curvature)", 2D) = "white" {}
        _LutCurveStrength ("Curvature Strength", Range(0, 1)) = 0.5
        _TranslucentCurveStrength ("Translucent Curvature Strength", Range(0, 1)) = 0.5
        _TranslucentColor ("Translucent Color", Color) = (1,1,1,1)
        _TranslucentPower ("Translucent Power", Range(0, 5)) = 1
        _TranslucentScale ("Translucent Scale (ours)", Range(0, 4)) = 1
        _CurvatureScale ("Scattering Width (ours)", Range(0, 1)) = 0.45

        [Header(Metallic Gloss Occlusion Curvature)] [Space(5)]
        [NoScaleOffset] _MetallicGlossMap ("MGAC (R metallic, G gloss, B occlusion, A curvature)", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1

        [Header(Normal)] [Space(5)]
        [NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Scale", Range(0, 1)) = 1
        _BumpBias ("Diffuse Normal Mip Bias", Range(0, 20)) = 0
        _DetailNormalMap ("Detail Normal Map", 2D) = "bump" {}
        _DetailBumpScale ("Detail Bump Scale", Range(0, 1)) = 1

        [Header(Emission)] [Space(5)]
        [Toggle(_EMISSION)] _EnableEmission ("Emission", Float) = 0
        [HDR] _EmissionColor ("Color", Color) = (0,0,0,1)
        _EmissionMap ("Emission", 2D) = "white" {}

        [Header(Other)] [Space(5)]
        [Toggle(_ALPHAPREMULTIPLY_ON)] _EnablePremultiplyAlpha ("Premultiply Alpha", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Culling", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "RoeCore.hlsl"

        TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);
        TEXTURE2D(_DetailNormalMap);    SAMPLER(sampler_DetailNormalMap);
        TEXTURE2D(_EmissionMap);        SAMPLER(sampler_EmissionMap);
        TEXTURE2D(_SkinLutMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _DetailNormalMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half4 _TranslucentColor;
            half _Surface;
            half _Cutoff;
            half _CastShadowDepthBias;
            half _CastShadowNormalBias;
            half _LutCurveStrength;
            half _TranslucentCurveStrength;
            half _TranslucentPower;
            half _TranslucentScale;
            half _CurvatureScale;
            half _Metallic;
            half _Smoothness;
            half _OcclusionStrength;
            half _BumpScale;
            half _BumpBias;
            half _DetailBumpScale;
        CBUFFER_END

        void RoeClip(float2 uv, float4 positionCS)
        {
        #if defined(_ALPHATEST_ON)
            clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a - _Cutoff);
        #endif
        }

        void RoeShadowClip(float2 uv)
        {
        #if defined(_ALPHATEST_ON)
            clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a - _Cutoff);
        #endif
        }

        #define ROE_SHADOW_DEPTH_BIAS _CastShadowDepthBias
        #define ROE_SHADOW_NORMAL_BIAS _CastShadowNormalBias
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RoeVert
            #pragma fragment Frag

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _EMISSION

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            struct RoeSurface
            {
                BRDFData brdf;
                half3 normalWS;          // sharp normal (with pores): highlights
                half3 diffuseNormalWS;   // softened normal: scattering
                half curvature;          // 0 flat .. 1 thin / tightly curved
                half translucency;       // how much back light gets through
            };

            half3 RoeShadeLight(RoeSurface s, InputData inputData, Light light)
            {
                half3 L = light.direction;
                half3 lightColor = light.color * light.distanceAttenuation;

                // Pre-integrated scattering: the LUT replaces max(N.L, 0); the red bleed past the
                // terminator grows with curvature.
                half NdotLd = dot(s.diffuseNormalWS, L);
                half3 lut = SAMPLE_TEXTURE2D_LOD(_SkinLutMap, sampler_LinearClamp,
                                                 float2(NdotLd * 0.5 + 0.5, s.curvature), 0).rgb;
                half3 color = s.brdf.diffuse * lut * (lightColor * light.shadowAttenuation);

                half NdotL = saturate(dot(s.normalWS, L));
                color += RoeSpecularGGX(s.brdf, s.normalWS, L, inputData.viewDirectionWS)
                         * (lightColor * (light.shadowAttenuation * NdotL));

                // Light coming through thin parts (ears, nostrils, fingers) from behind.
                half3 H = normalize(L + s.diffuseNormalWS * 0.4);
                half through = pow(saturate(dot(inputData.viewDirectionWS, -H)), max(_TranslucentPower, 0.05) * 4.0);
                // do not let the shadow map kill it completely: the light is behind the surface anyway
                half shadow = lerp(0.3, 1.0, light.shadowAttenuation);
                color += s.brdf.albedo * _TranslucentColor.rgb * lightColor * (through * s.translucency * shadow);
                return color;
            }

            #include "RoeLightLoop.hlsl"

            half4 Frag(RoeVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                half alpha = albedoAlpha.a;
            #if defined(_ALPHATEST_ON)
                clip(alpha - _Cutoff);
            #endif

                half4 mgac = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);
                half metallic = mgac.r * _Metallic;
                half smoothness = mgac.g * _Smoothness;
                half occlusion = LerpWhiteTo(mgac.b, _OcclusionStrength);

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                half3 softTS = UnpackNormalScale(SAMPLE_TEXTURE2D_BIAS(_BumpMap, sampler_BumpMap, uv, _BumpBias), _BumpScale);
                float2 detailUV = uv * _DetailNormalMap_ST.xy + _DetailNormalMap_ST.zw;
                half3 detailTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, detailUV), _DetailBumpScale);
                normalTS = BlendNormalRNM(normalTS, detailTS);

                half3x3 tangentToWorld = RoeTangentToWorld(input, true);
                InputData inputData;
                RoeInitInputData(input, tangentToWorld, normalTS, inputData);

                RoeSurface s;
                InitializeBRDFData(albedoAlpha.rgb, metallic, half3(0, 0, 0), smoothness, alpha, s.brdf);
                s.normalWS = inputData.normalWS;
                s.diffuseNormalWS = NormalizeNormalPerPixel(TransformTangentToWorld(softTS, tangentToWorld));
                s.curvature = saturate(mgac.a * _LutCurveStrength * _CurvatureScale);
                s.translucency = saturate(mgac.a * _TranslucentCurveStrength) * _TranslucentScale;

                AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, occlusion);
                // ambient from the softened normal, no grazing boost on skin
                half3 ambient = SampleSH(s.diffuseNormalWS);
                half3 color = RoeEnvironment(s.brdf, inputData, ambient, aoFactor.indirectAmbientOcclusion, 0.35, true);
                color += RoeDirectLighting(s, inputData, aoFactor);

            #if defined(_EMISSION)
                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor.rgb;
            #endif

                color = MixFog(color, inputData.fogCoord);
                return half4(color, OutputAlpha(alpha, IsSurfaceTypeTransparent(_Surface)));
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
