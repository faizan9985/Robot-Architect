Shader "RemixTable/RibbonEnergy"
{
    Properties { _BaseColor("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float4 edge:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float3 edge:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.color=input.color*_BaseColor;
                output.uv=input.uv;
                output.edge=input.edge.xyz;
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                float y=input.uv.y;
                float lower=input.edge.x,upper=input.edge.y;
                float aa=max(fwidth(y),.001);
                float core=smoothstep(lower-aa,lower+aa,y)*(1-smoothstep(upper-aa,upper+aa,y));
                float rim=exp2(-40*min(abs(y-lower),abs(y-upper)));
                float halo=smoothstep(0,max(lower,.001),y)*(1-smoothstep(min(upper,.999),1,y));
                float alpha=saturate(core*.55+rim*.4+halo*input.edge.z*.2);
                half3 radiance=input.color.rgb*(.35+rim*.8+halo*input.edge.z*.2);
                return half4(radiance*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
