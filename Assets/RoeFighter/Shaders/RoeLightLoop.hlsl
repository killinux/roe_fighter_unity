// Runs over the main light and the additional lights the way URP's UniversalFragmentPBR does
// (forward and forward+), calling the shader's own
//     half3 RoeShadeLight(RoeSurface s, InputData inputData, Light light)
// which must be defined before this file is included.
#ifndef ROE_LIGHT_LOOP_INCLUDED
#define ROE_LIGHT_LOOP_INCLUDED

half3 RoeDirectLighting(RoeSurface s, InputData inputData, AmbientOcclusionFactor aoFactor)
{
    half4 shadowMask = CalculateShadowMask(inputData);
    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);

    half3 color = half3(0, 0, 0);
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        color += RoeShadeLight(s, inputData, mainLight);
    }

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            color += RoeShadeLight(s, inputData, light);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            color += RoeShadeLight(s, inputData, light);
        }
    LIGHT_LOOP_END
#endif

    return color;
}

#endif
