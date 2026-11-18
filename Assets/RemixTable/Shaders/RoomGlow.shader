// ROOM mode light painted onto the user's real walls, floor and desk (drawn over passthrough).
// One quad per surface; uv spans the surface and _Size gives its size in meters, so edges and grid
// stay a constant real-world width on any wall.
//   _Shape 0: panel  - glowing rim, faint grid, reveal sweep and beat ripples
//   _Shape 1: ring   - mount ring where a stem orb sits on a wall
//   _Shape 2: meter  - VU strip along the desk edge, filled to _Fill
// Premultiplied output matching RibbonEnergy, so it composites over the passthrough underlay the same way.
Shader "RemixTable/RoomGlow"
{
    Properties
    {
        _BaseColor("Color", Color) = (0.3,0.8,1,1)
        _Intensity("Intensity", Float) = 1
        _Size("Size (m)", Vector) = (1,1,0,0)
        _Shape("Shape", Float) = 0
        _Fill("Fill", Float) = 1
        _Level("Level", Float) = 0
        _EdgeWidth("Edge Width (m)", Float) = 0.035
        _GridSpacing("Grid Spacing (m)", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Intensity, _Shape, _Fill, _Level, _EdgeWidth, _GridSpacing;
            float4 _Size;
            CBUFFER_END
            // Shared by every room surface, set once per frame by RemixRoomVisuals.
            float4 _RoomReveal;        // xyz origin, w radius (m) of the reveal sweep
            float4 _RoomPulseOrigin;   // xyz origin of beat ripples, w ripple speed (m/s)
            float4 _RoomPulseAges;     // seconds since each of the last four beats (large = none)
            float4 _RoomPulseStrength; // strength of each of those beats
            float _RoomIdle;           // 1 during the reveal, settles to a calm resting glow

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.uv = input.uv;
                return output;
            }

            float Ripples(float3 positionWS)
            {
                float d = distance(positionWS, _RoomPulseOrigin.xyz);
                float speed = max(_RoomPulseOrigin.w, 0.01);
                float4 radius = _RoomPulseAges * speed;
                float4 ring = exp2(-abs(d - radius) * 14.0) * exp2(-_RoomPulseAges * 2.2) * _RoomPulseStrength;
                return dot(ring, 1.0);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 size = max(_Size.xy, 0.01);
                float2 m = input.uv * size;                       // meters across the surface
                float edgeDistance = min(min(m.x, size.x - m.x), min(m.y, size.y - m.y));
                float aa = max(fwidth(edgeDistance), 1e-4);
                float intensity;

                if (_Shape > 1.5)
                {
                    // Meter: a bar filled left to right, bright leading edge, dim unlit track.
                    float filled = 1.0 - smoothstep(_Fill - fwidth(input.uv.x), _Fill, input.uv.x);
                    float head = exp2(-abs(input.uv.x - _Fill) * size.x * 60.0) * step(0.02, _Fill);
                    float border = 1.0 - smoothstep(0.0, 0.004 + aa, edgeDistance);
                    intensity = filled * 0.85 + head * 0.9 + border * 0.25 + 0.06;
                }
                else if (_Shape > 0.5)
                {
                    // Ring: two concentric circles that breathe with the stem's level.
                    float r = length(input.uv - 0.5) * 2.0;           // 0 center .. 1 rim
                    float ra = max(fwidth(r), 1e-3);
                    float outer = 1.0 - smoothstep(0.0, ra * 2.0, abs(r - 0.92));
                    float inner = 1.0 - smoothstep(0.0, ra * 2.0, abs(r - (0.55 + 0.25 * _Level)));
                    float glow = exp2(-abs(r - 0.92) * 9.0) * 0.35 * (1.0 - step(1.0, r));
                    intensity = outer * 0.9 + inner * (0.35 + _Level) + glow;
                }
                else
                {
                    // Panel: rim light, a faint meter grid, the reveal front and beat ripples.
                    float rim = 1.0 - smoothstep(0.0, _EdgeWidth + aa, edgeDistance);
                    rim *= rim;
                    float2 cell = abs(frac(m / _GridSpacing + 0.5) - 0.5) * _GridSpacing;
                    float2 gaa = max(fwidth(m), 1e-4);
                    float grid = 1.0 - smoothstep(0.0, gaa.x * 1.5, min(cell.x, cell.y));
                    float d = distance(input.positionWS, _RoomReveal.xyz);
                    float revealed = 1.0 - smoothstep(_RoomReveal.w - 0.25, _RoomReveal.w, d);
                    float front = exp2(-abs(d - _RoomReveal.w) * 10.0) * (1.0 - step(_RoomReveal.w, d - 0.6));
                    float idle = lerp(0.55, 1.0, saturate(_RoomIdle));
                    intensity = revealed * (rim * 0.75 * idle + grid * 0.10 * _RoomIdle + 0.025 * idle + _Level * (0.08 + rim * 0.6))
                              + front * 0.9 + revealed * Ripples(input.positionWS) * (0.35 + rim * 0.5);
                }

                intensity = saturate(intensity * _Intensity);
                return half4(_BaseColor.rgb * intensity, intensity * 0.85);
            }
            ENDHLSL
        }
    }
}
