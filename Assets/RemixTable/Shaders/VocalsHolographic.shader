Shader "RemixTable/VocalsHolographic"
{
    Properties
    {
        _Halo("Halo", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination blend", Float) = 0
        [Toggle] _ZWrite("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "RenderType"="Opaque" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 edge:TEXCOORD1; float4 automation:TEXCOORD2; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half intensity:TEXCOORD1; float height:TEXCOORD2; float4 automation:TEXCOORD3; float localY:TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
            float _Halo;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;output.intensity=input.color.r;
                output.height=input.edge.w;
                output.automation=input.automation;output.localY=input.positionOS.y;
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Palette specified in sRGB, explicitly decoded for the project's Linear workflow.
                half3 primary=SRGBToLinear(half3(.705882,.603922,1));
                half3 deep=SRGBToLinear(half3(.392157,.254902,.784314));
                half3 highlight=SRGBToLinear(half3(.909804,.839216,1));
                half3 edge=SRGBToLinear(half3(.941176,.874510,1));
                float y=input.uv.y;
                float lowpass=saturate(-input.automation.x),highpass=saturate(input.automation.x);
                if(_Halo>.5)
                {
                    float falloff=pow(saturate(1-y),2.2-lowpass*.7);
                    return half4(lerp(deep,primary,.45)*1.5,saturate(input.intensity*.42*falloff*(1-lowpass*.5+highpass*.25)));
                }
                half3 body=lerp(deep,primary,.85*smoothstep(0,1,y));
                body=lerp(body,highlight,.12*smoothstep(.65,1,y));
                // Baseline-anchored harmonics: no wall-clock phase or silhouette displacement.
                float phase=input.localY*input.automation.z;
                float footprint=fwidth(phase);
                float harmonicLine=1-smoothstep(.07,.07+max(footprint*.7,.001),abs(frac(phase+.5)-.5));
                float resolved=1-smoothstep(.35,.8,footprint);
                body=lerp(body,highlight*1.15,harmonicLine*resolved*input.automation.w*.65*(1-lowpass*.6));
                // A 0.45mm contour with derivative anti-aliasing, capped on silent sections.
                float uvPixel=max(fwidth(y),.0001);
                float width=min(.12,.00045/max(input.height,.0001));
                float distanceToEdge=min(y,1-y);
                float rim=1-smoothstep(width,width+uvPixel*.65*(1+lowpass*1.8-highpass*.45),distanceToEdge);
                body=lerp(body,edge*1.15*(1+highpass*.45),rim*.8*(1-lowpass*.55));
                body*=1-lowpass*.48;
                return half4(body*input.intensity,1);
            }
            ENDHLSL
        }
    }
}
