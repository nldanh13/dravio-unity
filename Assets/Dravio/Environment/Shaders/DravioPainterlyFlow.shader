// Dravio VIS.4b (Issue #91) - stylized flowing water (waterfall curtain, stream ribbon) for URP 17.
// Separate from "Painterly Water": the pond shader, its shore foam (vertex A) and the drink /
// splash ripple contract (_DravioRipples) are untouched.
// Mobile friendly: no depth or opaque texture, no reflections, one pass, 3 texture samples.
//   * _BaseMap  = waterfall silk RGBA (VIS9), _StreamMap = stream ripples RGBA (VIS9), both repeat.
//   * vertex colour A selects the surface: 1 = waterfall (silk + foam band), 0 = stream.
//     vertex colour RGB multiplies the colour (white in the VIS3d meshes).
//   * Water flows toward -v (authored glTF v = 0 at the top / stream start; Blender/Unity flip v).
//   * _FoamMap = 4x4 flipbook (16 frames, row-major from top-left, clamp) drawn in a band at
//     the bottom of the waterfall (uv.y < _FoamBand), each horizontal tile on its own frame.
// Cull Off by default: the VIS3d waterfall curtain is single-sided and faces away from the
// Origin camera in its source orientation.
Shader "Dravio/Environment/Painterly Flow"
{
    Properties
    {
        _BaseMap ("Waterfall Silk (RGBA, repeat)", 2D) = "white" {}
        _StreamMap ("Stream Ripples (RGBA, repeat)", 2D) = "white" {}
        [NoScaleOffset] _FoamMap ("Foam Flipbook 4x4 (RGBA, clamp)", 2D) = "black" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _FallSpeed ("Waterfall Speed (v/s)", Float) = 0.9
        _StreamSpeed ("Stream Speed (v/s)", Float) = 0.22
        _StreamTiling ("Stream Tiling (uv)", Vector) = (1, 1, 0, 0)
        _Opacity ("Opacity", Range(0,1.5)) = 1
        _EdgeSoftness ("Side Edge Softness (u)", Range(0,0.5)) = 0.08
        _FoamBand ("Foam Band (v from bottom)", Range(0.01,1)) = 0.22
        _FoamTiling ("Foam Tiles Across", Float) = 3
        _FoamFps ("Foam Frames / s", Float) = 16
        _FoamStrength ("Foam Strength", Range(0,2)) = 1
        _LightInfluence ("Light Influence", Range(0,1)) = 0.55
        _Brightness ("Brightness", Range(0,2)) = 1.08
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_StreamMap); SAMPLER(sampler_StreamMap);
            TEXTURE2D(_FoamMap);   SAMPLER(sampler_FoamMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _StreamMap_ST;
                half4 _Tint;
                float _FallSpeed;
                float _StreamSpeed;
                float4 _StreamTiling;
                half _Opacity;
                half _EdgeSoftness;
                half _FoamBand;
                float _FoamTiling;
                float _FoamFps;
                half _FoamStrength;
                half _LightInfluence;
                half _Brightness;
                half _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uvFlow     : TEXCOORD0; // xy = waterfall sample, zw = stream sample
                float2 uv         : TEXCOORD1; // authored uv (foam band, side edges)
                half4  color      : TEXCOORD2;
                half3  normalWS   : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                // Sampling at uv + t*s moves the painted pattern toward -v (downstream).
                o.uvFlow.xy = TRANSFORM_TEX(input.uv, _BaseMap) + float2(0, _Time.y * _FallSpeed);
                o.uvFlow.zw = TRANSFORM_TEX(input.uv * _StreamTiling.xy, _StreamMap) + float2(0, _Time.y * _StreamSpeed);
                o.uv = input.uv;
                o.color = input.color;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 SampleFoam(float2 uv)
            {
                float tiles = max(_FoamTiling, 1.0);
                float across = uv.x * tiles;
                float tile = floor(across);
                // Each tile runs the 16-frame loop with its own phase so splashes do not pulse in sync.
                float frame = floor(fmod(_Time.y * _FoamFps + tile * 5.0, 16.0));
                float col = fmod(frame, 4.0);
                float row = floor(frame / 4.0);              // 0 = top row of the sheet
                float2 local = float2(frac(across), saturate(uv.y / max(_FoamBand, 0.01)));
                float2 sheet = float2((col + local.x) * 0.25, 1.0 - (row + 1.0 - local.y) * 0.25);
                return SAMPLE_TEXTURE2D(_FoamMap, sampler_FoamMap, sheet);
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half fall = saturate(input.color.a);
                half4 silk = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uvFlow.xy);
                half4 ripple = SAMPLE_TEXTURE2D(_StreamMap, sampler_StreamMap, input.uvFlow.zw);
                half4 water = lerp(ripple, silk, fall);
                water.rgb *= _Tint.rgb * input.color.rgb;

                if (fall > 0.0h && input.uv.y < _FoamBand)
                {
                    half4 foam = SampleFoam(input.uv) * (_FoamStrength * fall);
                    water.rgb = lerp(water.rgb, foam.rgb, saturate(foam.a));
                    water.a = max(water.a, saturate(foam.a));
                }

                Light light = GetMainLight();
                half3 n = normalize(input.normalWS) * IS_FRONT_VFACE(cullFace, 1.0h, -1.0h);
                half wrap = saturate(dot(n, light.direction) * 0.5h + 0.5h);
                half3 lit = light.color * wrap + SampleSH(n) * 0.5h;
                water.rgb *= lerp(half3(1, 1, 1), lit, _LightInfluence) * _Brightness;

                half edge = _EdgeSoftness > 0.0h
                    ? smoothstep(0.0h, _EdgeSoftness, input.uv.x) * smoothstep(0.0h, _EdgeSoftness, 1.0h - input.uv.x)
                    : 1.0h;
                half alpha = saturate(water.a * _Opacity * _Tint.a * lerp(1.0h, edge, fall));
                water.rgb = MixFog(water.rgb, input.fogFactor);
                return half4(water.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
