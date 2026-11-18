Shader "RemixTable/SpatialHolographic"
{
    Properties
    {
        _Halo("Outer halo",Float)=0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source blend",Float)=1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination blend",Float)=0
        [Toggle] _ZWrite("Depth write",Float)=1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=2
        _CoreIntensity("Core intensity",Float)=1
        _RimIntensity("Rim intensity",Float)=.7
        _HaloIntensity("Halo intensity",Float)=.35
        _HaloExpansion("Halo expansion",Float)=1.14
        [HideInInspector] _PrimaryLinear("Primary Linear",Vector)=(0,0,0,1)
        [HideInInspector] _DeepLinear("Shadow Linear",Vector)=(0,0,0,1)
        [HideInInspector] _HighlightLinear("Highlight Linear",Vector)=(0,0,0,1)
        [HideInInspector] _EdgeLinear("Edge Linear",Vector)=(0,0,0,1)
        [HideInInspector] _LowpassLinear("Unused palette compatibility",Vector)=(0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "RenderType"="Opaque" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            CBUFFER_START(UnityPerMaterial)
            float _Halo,_CoreIntensity,_RimIntensity,_HaloIntensity,_HaloExpansion;
            float4 _PrimaryLinear,_DeepLinear,_HighlightLinear,_EdgeLinear,_LowpassLinear;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 position=input.positionOS.xyz*(_Halo>.5?_HaloExpansion:1);
                output.positionWS=TransformObjectToWorld(position);output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal=normalize(input.normalWS),view=normalize(GetWorldSpaceViewDir(input.positionWS));
                float facing=saturate(dot(normal,view));
                if(_Halo>.5)
                {
                    // Analytic outer ring: no translucent full-fill haze over the core.
                    float projected=sqrt(saturate(1-facing*facing));
                    float inner=rcp(max(_HaloExpansion,1.001));
                    float ring=saturate((projected-inner)/max(1-inner,.001));
                    float alpha=saturate(smoothstep(0,.12,ring)*pow(1-ring,2.2)*_HaloIntensity);
                    clip(alpha-.003);
                    return half4(lerp(_DeepLinear.rgb,_PrimaryLinear.rgb,.78)*1.45,alpha);
                }
                float3 lightDirection=normalize(float3(-.35,.65,-.70));
                float shade=saturate(dot(normal,lightDirection)*.5+.5);
                half3 body=lerp(_DeepLinear.rgb,_PrimaryLinear.rgb,.08+.84*shade*shade)*(.58+.42*shade);
                float3 halfDirection=lightDirection+view;
                halfDirection*=rsqrt(max(dot(halfDirection,halfDirection),.0001));
                float softHighlight=pow(saturate(dot(normal,halfDirection)),12);
                body=lerp(body,_HighlightLinear.rgb,softHighlight*.14)*_CoreIntensity;
                float rim=pow(1-facing,3);
                body+=lerp(_PrimaryLinear.rgb,_EdgeLinear.rgb,.22)*rim*.35*_RimIntensity;
                // Compress radiance uniformly rather than clipping channels toward white.
                body/=max(1,max(body.r,max(body.g,body.b))/1.2);
                return half4(body,1);
            }
            ENDHLSL
        }
    }
}
