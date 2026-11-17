Shader "RemixTable/VocalsHolographic"
{
    Properties
    {
        _Halo("Halo", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination blend", Float) = 0
        [Toggle] _ZWrite("Depth write", Float) = 1
        [HideInInspector] _PrimaryLinear("Primary Linear", Vector) = (0,0,0,1)
        [HideInInspector] _DeepLinear("Shadow Linear", Vector) = (0,0,0,1)
        [HideInInspector] _HighlightLinear("Highlight Linear", Vector) = (0,0,0,1)
        [HideInInspector] _EdgeLinear("Contour Linear", Vector) = (0,0,0,1)
        [HideInInspector] _LowpassLinear("Low-pass Linear", Vector) = (0,0,0,1)
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
            #pragma shader_feature_local _ _STEM_PALETTE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 edge:TEXCOORD1; float4 automation:TEXCOORD2; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half intensity:TEXCOORD1; float height:TEXCOORD2; float4 automation:TEXCOORD3; float localY:TEXCOORD4; float localX:TEXCOORD5; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
            float _Halo;
            float4 _PrimaryLinear, _DeepLinear, _HighlightLinear, _EdgeLinear, _LowpassLinear;
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
                output.localX=input.positionOS.x;
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
                // The approved Vocals constants remain the exact default path.
                // Other stems receive CPU-decoded Linear vectors, without per-fragment color conversion.
                #if defined(_STEM_PALETTE)
                    primary=_PrimaryLinear.rgb;deep=_DeepLinear.rgb;
                    highlight=_HighlightLinear.rgb;edge=_EdgeLinear.rgb;
                #endif
                float y=input.uv.y;
                float lowpass=saturate(-input.automation.x),highpass=saturate(input.automation.x);
                if(_Halo>.5)
                {
                    float falloff=pow(saturate(1-y),2.2-lowpass*.7);
                    return half4(lerp(deep,primary,.45)*1.5,saturate(input.intensity*.42*falloff*(1-lowpass*.5+highpass*.25)));
                }
                half3 body=lerp(deep,primary,.85*smoothstep(0,1,y));
                body=lerp(body,highlight,.12*smoothstep(.65,1,y));
                // Filter is a parameter-driven metaphor, not measured audio-frequency content.
                // Broad 65mm bands read across even a narrow envelope at seated distance.
                half3 lowViolet=SRGBToLinear(half3(.49,.20,.94));
                #if defined(_STEM_PALETTE)
                    lowViolet=_LowpassLinear.rgb;
                #endif
                float broadBand=.5+.5*cos(input.localX*96.66439);
                body=lerp(body,lowViolet*(.08+1.25*broadBand),lowpass*.9);
                // High-pass adds thin diagonal streaks, distinct from horizontal Pitch harmonics.
                float filterPhase=input.localX*(48+64*highpass)+input.localY*35;
                float filterFootprint=fwidth(filterPhase);
                float streakDistance=abs(frac(filterPhase+.5)-.5);
                float streakMask=1-smoothstep(.035,.035+max(filterFootprint*.7,.001),streakDistance);
                float streakResolved=1-smoothstep(.35,.8,filterFootprint);
                body=lerp(body,lerp(primary,edge,.38)*1.15,streakMask*streakResolved*highpass*.85);
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
                half3 contourColor=lerp(edge,lerp(lowViolet,primary,.25),lowpass*.65);
                body=lerp(body,contourColor*1.15*(1+highpass*.45),rim*.8*(1-lowpass*.55));
                body*=1-lowpass*.48;
                return half4(body*input.intensity,1);
            }
            ENDHLSL
        }
    }
}
