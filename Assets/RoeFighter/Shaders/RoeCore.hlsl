// Shared code of the ROE character shaders (URP 17, forward / forward+).
// The game's own shaders ("Pinkcore/Heros/...") cannot be recovered from the build, only their
// property lists.  These shaders keep the game's property names, so the game's materials work
// as they are; the lighting maths is our own reconstruction.
#ifndef ROE_CORE_INCLUDED
#define ROE_CORE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"

struct RoeAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 texcoord   : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct RoeVaryings
{
    float2 uv         : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS   : TEXCOORD2;
    half4  tangentWS  : TEXCOORD3;    // xyz: tangent, w: sign
    half   fogFactor  : TEXCOORD4;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord : TEXCOORD5;
#endif
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

RoeVaryings RoeVert(RoeAttributes input)
{
    RoeVaryings output = (RoeVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.uv = input.texcoord;
    output.positionWS = vertexInput.positionWS;
    output.normalWS = normalInput.normalWS;
    real sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);

    half fogFactor = 0;
#if !defined(_FOG_FRAGMENT)
    fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
#endif
    output.fogFactor = fogFactor;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif
    output.positionCS = vertexInput.positionCS;
    return output;
}

// Tangent frame of the pixel.  Back faces of two-sided meshes (hair cards, cloth) get the
// mirrored normal, the tangent and bitangent stay.
half3x3 RoeTangentToWorld(RoeVaryings input, bool frontFace)
{
    float3 n = normalize(input.normalWS);
    float3 t = input.tangentWS.xyz;
    if (dot(t, t) < 1e-6)   // mesh without usable tangents: any frame is better than NaN
        t = normalize(cross(n, abs(n.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0)));
    float3 b = input.tangentWS.w * cross(n, t);
    if (!frontFace)
        n = -n;
    return half3x3(t, b, n);
}

void RoeInitInputData(RoeVaryings input, half3x3 tangentToWorld, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.tangentToWorld = tangentToWorld;
    inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif

    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
    inputData.vertexLighting = half3(0, 0, 0);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.bakedGI = SampleSH(inputData.normalWS);   // characters are dynamic: light probes / ambient only
    inputData.shadowMask = half4(1, 1, 1, 1);
}

// The GGX highlight of URP's Lit shader for one light, without the diffuse part.
half3 RoeSpecularGGX(BRDFData brdf, half3 normalWS, half3 lightDirWS, half3 viewDirWS)
{
    return brdf.specular * DirectBRDFSpecular(brdf, normalWS, lightDirWS, viewDirWS);
}

// Ambient diffuse + reflections, like URP's GlobalIllumination(), with two extra controls:
// fresnelScale scales the grazing-angle boost, reflections = false drops the reflection probe.
half3 RoeEnvironment(BRDFData brdf, InputData inputData, half3 diffuseNormalGI, half occlusion,
                     half fresnelScale, bool reflections)
{
    half3 reflectVector = reflect(-inputData.viewDirectionWS, inputData.normalWS);
    half NoV = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
    half fresnelTerm = Pow4(1.0 - NoV) * fresnelScale;

    half3 color = diffuseNormalGI * brdf.diffuse;
    if (reflections)
    {
        half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, inputData.positionWS,
            brdf.perceptualRoughness, half(1.0), inputData.normalizedScreenSpaceUV);
        color += indirectSpecular * EnvironmentBRDFSpecular(brdf, fresnelTerm);
    }
    return color * occlusion;
}

// Screen-door fade the game uses (_IGNOpacity): 1 = solid, 0 = gone.
void RoeDitherClip(float4 positionCS, half opacity)
{
    if (opacity < 0.999)
        clip(opacity - InterleavedGradientNoise(positionCS.xy, 0) - 0.001);
}

// Alpha-to-coverage friendly cut-out: sharpen alpha around the threshold so MSAA can
// anti-alias the edge; without MSAA it behaves like a plain alpha test.
half RoeCutoutAlpha(half alpha, half cutoff)
{
    half sharpened = (alpha - cutoff) / max(fwidth(alpha), 0.0001) + 0.5;
    clip(alpha - cutoff * 0.5);    // far below the threshold: always drop
    return saturate(sharpened);
}

#endif
