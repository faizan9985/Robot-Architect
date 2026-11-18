// Main menu background: wide, faint stem ribbons arcing around the listener (RemixMenuAura).
// Vertex color = stem color and strength; uv.y runs -1..1 across the ribbon. The fragment gives each ribbon a
// soft gaussian body, a thin brighter core and very fine drifting scanlines, so it reads as light, not as a
// solid band. Premultiplied output like RibbonEnergy / RoomGlow, so it also works over passthrough.
Shader "RemixTable/MenuAura"
{
    Properties
    {
        _Intensity("Intensity", Float) = 1
        _Softness("Body Softness", Float) = 2.6
        _Core("Core Strength", Float) = 0.55
        _ScanDensity("Scanline Density", Float) = 90
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
            float _Intensity, _Softness, _Core, _ScanDensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float y = input.uv.y;
                float body = exp(-y * y * _Softness);
                float core = exp(-y * y * 60.0) * _Core;
                float scan = 0.82 + 0.18 * sin((input.uv.x * _ScanDensity - _Time.y * 0.6) * 6.2831853);
                float a = saturate(input.color.a * (body * scan + core) * _Intensity);
                // Core drifts toward white a little so the hue stays readable at low alpha.
                half3 rgb = lerp(input.color.rgb, 1.0.xxx, core * 0.35);
                return half4(rgb * a, a * 0.6);
            }
            ENDHLSL
        }
    }
}
