// Eyeballs of Rise of Eros characters.
// Stands in for the game's "Pinkcore/Heros/Eye".  The eye is composed in the eyeball's UV
// disc (centre = looking direction): sclera texture, iris texture inside the iris radius,
// the pupil enlarged through the mask's G channel, a limbal ring, a little parallax, and one smooth
// highlight for cornea and sclera.  The exact formulas of the game are unknown; the layout
// constants below were measured from the game's own textures.
Shader "ROE/Eye"
{
    Properties
    {
        [Header(Eye Albedo)] [Space(5)]
        _ScleraAlbedoTex ("Sclera Albedo Map", 2D) = "white" {}
        _ScleraColor ("Sclera Color", Color) = (0.95,0.95,0.95,1)
        _ScleraSizeInv ("Sclera Size (INV)", Range(0.5, 2)) = 1
        _IrisAlbedoTex ("Iris Albedo Map", 2D) = "white" {}
        _IrisMaskTex ("Iris Mask Map (R iris, G pupil)", 2D) = "white" {}
        _IrisColor ("Iris Color", Color) = (1,1,1,1)
        _PupilMaxSizeInv ("Pupil Max Size (INV)", Range(0.35, 0.5)) = 0.4
        _PupilMinSizeInv ("Pupil Min Size (INV)", Range(0.75, 2)) = 1
        _PupilSizeScale ("Pupil Size Scale", Range(0, 1)) = 0
        _IrisLimbusScaleInv ("Iris Size (INV)", Range(0.5, 2)) = 1
        _LimbusPivot ("Limbus Pivot", Range(0, 1)) = 0.3
        _LimbusSizeSclera ("Limbal Ring Size Sclera", Range(0, 1)) = 1
        _LimbusSizeIris ("Limbal Ring Size Iris", Range(0, 1)) = 1
        _LimbusFadeRatio ("Limbal Fade Ratio", Range(1, 5)) = 1
        _LimbusPivotWidth ("Limbus Pivot Width", Range(0, 1)) = 0.5
        _LimbusColor ("Limbus Color", Color) = (0.2,0,0,1)

        [Header(Cornea Parallax)] [Space(5)]
        _IrisParallax ("Parallax Effect", Range(0, 0.1)) = 0.03

        [Header(Iris Illumination)] [Space(5)]
        [Toggle(_EMISSION)] _EnableEmission ("Iris Emission", Float) = 0
        [HDR] _IrisIllumColor ("Iris Emission Color", Color) = (0,0,0,1)

        [Header(Lighting Surface)] [Space(5)]
        _EyeBumpMap ("Sclera Bump Map", 2D) = "bump" {}
        _BumpStrength ("Sclera Bump Strength", Range(0, 1)) = 1
        _IrisClearCoatMetallic ("Iris ClearCoat Metallic", Range(0, 1)) = 1
        _ScleraMetallic ("Sclera Metallic", Range(0, 1)) = 0.1
        _IrisClearCoatSmoothness ("Iris ClearCoat Smoothness", Range(0, 1)) = 0.6
        _IrisClearCoatMask ("Iris ClearCoat Mask", Range(0, 1)) = 0.3
        _ScleraSmoothness ("Sclera Smoothness", Range(0, 1)) = 0.1
        _EyeOcclusionMap ("Eye Occlusion Map", 2D) = "white" {}
        _IrisOcclusionAmt ("Iris Occlusion Strength", Range(0, 1)) = 0
        _ScleraOcclusionAmt ("Sclera Occlusion Strength", Range(0, 1)) = 0
        _SpecularTermMax ("Specular Term Max", Range(0, 100)) = 10

        [Header(Specular Shape)] [Space(5)]
        _DirectHighlightSizeMultiplier ("Direct Highlight Size", Range(-1, 1)) = 0
        _DirectHighlightSharpen ("Direct Highlight Sharpness", Range(0, 1)) = 0
        _DirectHighlightThreshold ("Direct Highlight Specular Clipping Threshold", Range(0.01, 0.99)) = 0.9

        [Header(Ours)] [Space(5)]
        _PupilBoost ("Pupil Boost", Range(0.5, 3)) = 1.5
        _LimbusStrength ("Limbal Ring Strength", Range(0, 1)) = 0.75
        _DiffuseWrap ("Diffuse Wrap", Range(0, 1)) = 0.4
        _EnvReflection ("Reflection Strength", Range(0, 2)) = 1

        [Header(Other)] [Space(5)]
        [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows ("Receive Shadows", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "RoeCore.hlsl"

        TEXTURE2D(_ScleraAlbedoTex);    SAMPLER(sampler_ScleraAlbedoTex);
        TEXTURE2D(_IrisAlbedoTex);      SAMPLER(sampler_IrisAlbedoTex);
        TEXTURE2D(_IrisMaskTex);        SAMPLER(sampler_IrisMaskTex);
        TEXTURE2D(_EyeBumpMap);         SAMPLER(sampler_EyeBumpMap);
        TEXTURE2D(_EyeOcclusionMap);    SAMPLER(sampler_EyeOcclusionMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _ScleraAlbedoTex_ST;
            float4 _IrisAlbedoTex_ST;
            float4 _IrisMaskTex_ST;
            float4 _EyeBumpMap_ST;
            float4 _EyeOcclusionMap_ST;
            half4 _ScleraColor;
            half4 _IrisColor;
            half4 _LimbusColor;
            half4 _IrisIllumColor;
            half _ScleraSizeInv;
            half _PupilMaxSizeInv;
            half _PupilMinSizeInv;
            half _PupilSizeScale;
            half _IrisLimbusScaleInv;
            half _LimbusPivot;
            half _LimbusSizeSclera;
            half _LimbusSizeIris;
            half _LimbusFadeRatio;
            half _LimbusPivotWidth;
            half _IrisParallax;
            half _BumpStrength;
            half _IrisClearCoatMetallic;
            half _ScleraMetallic;
            half _IrisClearCoatSmoothness;
            half _IrisClearCoatMask;
            half _ScleraSmoothness;
            half _IrisOcclusionAmt;
            half _ScleraOcclusionAmt;
            half _SpecularTermMax;
            half _DirectHighlightSizeMultiplier;
            half _DirectHighlightSharpen;
            half _DirectHighlightThreshold;
            half _PupilBoost;
            half _LimbusStrength;
            half _DiffuseWrap;
            half _EnvReflection;
        CBUFFER_END

        void RoeClip(float2 uv, float4 positionCS) { }
        void RoeShadowClip(float2 uv) { }
        #define ROE_SHADOW_DEPTH_BIAS 1.0
        #define ROE_SHADOW_NORMAL_BIAS 1.0

        // Radius of the iris disc in the game's shared iris mask texture, and of the iris in
        // the iris albedo textures (fractions of the texture width; measured).
        #define ROE_MASK_IRIS_RADIUS 0.137
        #define ROE_TEX_IRIS_RADIUS 0.25
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RoeVert
            #pragma fragment Frag

            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _EMISSION

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
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
                half3 normalWS;          // bumpy on the sclera, smooth on the cornea
                half occlusion;
            };

            half3 RoeShadeLight(RoeSurface s, InputData inputData, Light light)
            {
                half3 L = light.direction;
                half NdotL = dot(s.normalWS, L);
                half wrapped = saturate((NdotL + _DiffuseWrap) / (1.0 + _DiffuseWrap));
                half3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation);

                half3 color = s.brdf.diffuse * wrapped;
                half spec = min(DirectBRDFSpecular(s.brdf, s.normalWS, L, inputData.viewDirectionWS), _SpecularTermMax);
                color += s.brdf.specular * spec * saturate(NdotL);
                return color * radiance * s.occlusion;
            }

            #include "RoeLightLoop.hlsl"

            half4 Frag(RoeVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Tangent frame from screen-space derivatives of position and UV: the eyeballs'
                // vertex tangents are not reliable, and a broken frame turns the whole eye black.
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 N = normalize(input.normalWS);
                float3 dp1 = ddx(input.positionWS);
                float3 dp2 = ddy(input.positionWS);
                float2 duv1 = ddx(input.uv);
                float2 duv2 = ddy(input.uv);
                float3 dp2perp = cross(dp2, N);
                float3 dp1perp = cross(N, dp1);
                float3 T = dp2perp * duv1.x + dp1perp * duv2.x;
                float3 B = dp2perp * duv1.y + dp1perp * duv2.y;
                float invmax = rsqrt(max(max(dot(T, T), dot(B, B)), 1e-20));
                half3x3 tangentToWorld = half3x3(T * invmax, B * invmax, N);
                half3 viewDirTS = half3(dot(viewDirWS, tangentToWorld[0]), dot(viewDirWS, tangentToWorld[1]), dot(viewDirWS, N));

                float2 c = input.uv - 0.5;
                float irisRadius = ROE_MASK_IRIS_RADIUS / _IrisLimbusScaleInv;      // in mesh UV
                float r = length(c) / irisRadius;                                   // 1 = iris edge
                half irisW = 1.0 - smoothstep(0.95, 1.04, r);

                // sclera
                float2 scleraUV = c * _ScleraSizeInv + 0.5;
                half3 sclera = SAMPLE_TEXTURE2D(_ScleraAlbedoTex, sampler_ScleraAlbedoTex, scleraUV).rgb * _ScleraColor.rgb;

                // iris, seen through the cornea: shift the lookup against the view direction
                float2 parallax = viewDirTS.xy / max(viewDirTS.z, 0.35) * (_IrisParallax * irisRadius * 2.0);
                float2 ci = c - parallax * irisW;

                // The iris textures carry only a tiny pupil (12% of the iris).  The centre of the
                // texture is magnified, fading to no magnification at the iris rim, so the pupil
                // gets its size without moving the rim.  The material's pupil controls give the
                // magnification; _PupilBoost is ours (the game's exact falloff is unknown).
                half pupilInv = lerp(_PupilMinSizeInv, _PupilMaxSizeInv, _PupilSizeScale) / max(_PupilBoost, 0.01);
                float ri = saturate(length(ci) / irisRadius);
                float2 irisUV = ci * ((ROE_TEX_IRIS_RADIUS / irisRadius) * lerp(pupilInv, 1.0, ri)) + 0.5;
                half3 iris = SAMPLE_TEXTURE2D(_IrisAlbedoTex, sampler_IrisAlbedoTex, irisUV).rgb * _IrisColor.rgb;

                // limbal ring: dark band straddling the iris edge
                half ringIn = smoothstep(1.0 - _LimbusSizeIris * 0.6, 1.0, r);
                half ringOut = 1.0 - smoothstep(1.0, 1.0 + _LimbusSizeSclera * 0.6, r);
                half ring = pow(saturate(ringIn * ringOut), 1.0 / max(_LimbusFadeRatio, 1.0)) * _LimbusStrength;

                half3 albedo = lerp(sclera, iris, irisW);
                albedo = lerp(albedo, albedo * _LimbusColor.rgb, ring);

                half ao = SAMPLE_TEXTURE2D(_EyeOcclusionMap, sampler_EyeOcclusionMap, input.uv).r;
                half occlusion = LerpWhiteTo(ao, lerp(_ScleraOcclusionAmt, _IrisOcclusionAmt, irisW));

                half3 bumpTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_EyeBumpMap, sampler_EyeBumpMap, scleraUV), _BumpStrength);
                half3 normalTS = normalize(lerp(bumpTS, half3(0, 0, 1), irisW));

                InputData inputData;
                RoeInitInputData(input, tangentToWorld, normalTS, inputData);

                half smoothness = lerp(_ScleraSmoothness, _IrisClearCoatSmoothness, irisW * _IrisClearCoatMask);
                half metallic = lerp(_ScleraMetallic, _IrisClearCoatMetallic, irisW * _IrisClearCoatMask) * 0.2;
                half alpha = 1.0;

                RoeSurface s;
                InitializeBRDFData(albedo, metallic, half3(0, 0, 0), smoothness, alpha, s.brdf);
                s.normalWS = inputData.normalWS;
                s.occlusion = occlusion;

                AmbientOcclusionFactor aoFactor;
                aoFactor.directAmbientOcclusion = 1.0;
                aoFactor.indirectAmbientOcclusion = occlusion;

                half3 color = RoeEnvironment(s.brdf, inputData, inputData.bakedGI, occlusion, _EnvReflection, true);
                color += RoeDirectLighting(s, inputData, aoFactor);

            #if defined(_EMISSION)
                color += iris * _IrisIllumColor.rgb * irisW;
            #endif

                color = MixFog(color, inputData.fogCoord);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RoeDepthVert
            #pragma fragment RoeDepthFrag
            #pragma multi_compile_instancing
            #include "RoePasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RoeDepthVert
            #pragma fragment RoeDepthNormalsFrag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #include "RoePasses.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
