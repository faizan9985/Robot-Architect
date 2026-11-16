// Real-world occlusion for Remix Table content (ROOM mode, Meta Quest environment depth via AR Foundation's
// AR Shader Occlusion). Returns 1 where the content is in front of the real world, fading to 0 within a few cm
// behind a real surface (your hand, a chair, the desk edge), so the ribbons look physically present.
// Globals are set by ARShaderOcclusion (_EnvironmentDepth*, _NdcLinearConversionParameters, _IsOcclusionOn)
// and RemixRoomScanner (_RemixTrackingFromWorld: world -> XR tracking space, where the depth matrices live).
#ifndef REMIX_OCCLUSION_INCLUDED
#define REMIX_OCCLUSION_INCLUDED

// Eye index for the depth texture slice (0 outside stereo variants).
#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    #define REMIX_EYE_INDEX unity_StereoEyeIndex
#else
    #define REMIX_EYE_INDEX 0
#endif

#if defined(XR_HARD_OCCLUSION) || defined(XR_SOFT_OCCLUSION)
TEXTURE2D_ARRAY(_EnvironmentDepthTexture);
SAMPLER(sampler_EnvironmentDepthTexture);
float4x4 _EnvironmentDepthProjectionMatrices[2];
float4 _NdcLinearConversionParameters;
float4x4 _RemixTrackingFromWorld;
int _IsOcclusionOn;

float RemixOcclusion(float3 positionWS, uint eye)
{
    if (_IsOcclusionOn == 0) return 1.0;
    float3 positionTS = mul(_RemixTrackingFromWorld, float4(positionWS, 1.0)).xyz;
    float4 depthSpace = mul(_EnvironmentDepthProjectionMatrices[eye], float4(positionTS, 1.0));
    if (depthSpace.w <= 0.0) return 1.0;
    float2 uv = (depthSpace.xy / depthSpace.w + 1.0) * 0.5;
    if (any(uv < 0.0) || any(uv > 1.0)) return 1.0;
    float environmentNdc = SAMPLE_TEXTURE2D_ARRAY_LOD(_EnvironmentDepthTexture, sampler_EnvironmentDepthTexture, uv, eye, 0).r * 2.0 - 1.0;
    float environmentLinear = _NdcLinearConversionParameters.x / (environmentNdc + _NdcLinearConversionParameters.y);
    // Soft edge: fully visible 2 cm in front of the real surface, gone 2 cm behind it.
    return saturate((environmentLinear - depthSpace.w) / 0.04 + 0.5);
}
#else
float RemixOcclusion(float3 positionWS, uint eye) { return 1.0; }
#endif

#endif
