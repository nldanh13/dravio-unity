// Dravio - seabed caustics (Ocean) for URP 17. Replaces URP Particles/Unlit on the caustics quad.
// Mobile friendly: 2 samples of the existing T_ENV_FX_Caustics alpha mask, additive, no decal,
// no depth texture, no procedural Voronoi.
//   layer A = _BaseMap_ST (tiling + offset written by EnvironmentScrollingFx, as before)
//   layer B = same mask, scaled, rotated and scrolled in the shader (_Time), so it moves every frame
//   result = min(A, B) * _MinBoost + max(A, B) * _MaxKeep
// The min term brightens where the two networks cross (moving "cells" like Voronoi caustics);
// the max term keeps the authored lines, so average brightness stays close to the old single layer
// and the tiling repeat of one layer is broken up.
// _BaseColor (rgb tint, a = day/night alpha) is still driven by EnvironmentScrollingFx.
Shader "Dravio/Environment/Caustics"
{
    Properties
    {
        _BaseMap ("Caustics Mask (alpha, repeat)", 2D) = "white" {}
        _BaseColor ("Tint (a = strength)", Color) = (0.8,1,1,0.11)
        _ScaleB ("Layer B Scale", Float) = 1.31
        _RotateB ("Layer B Rotation (rad)", Float) = 0.52
        _ScrollB ("Layer B Scroll (uv/s)", Vector) = (-0.021, 0.015, 0, 0)
        _MinBoost ("Crossing Boost", Range(0,4)) = 1.6
        _MaxKeep ("Line Keep", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _ScaleB;
                float _RotateB;
                float4 _ScrollB;
                half _MinBoost;
                half _MaxKeep;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv         : TEXCOORD0; // xy = layer A, zw = layer B
                half   fogFactor  : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float2 tiled = input.uv * _BaseMap_ST.xy;
                o.uv.xy = tiled + _BaseMap_ST.zw;
                float s, c;
                sincos(_RotateB, s, c);
                float2 b = tiled * _ScaleB + float2(0.37, 0.21);
                o.uv.zw = float2(b.x * c - b.y * s, b.x * s + b.y * c) + _Time.y * _ScrollB.xy;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy).a;
                half b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.zw).a;
                half k = saturate(min(a, b) * _MinBoost + max(a, b) * _MaxKeep);
                half3 col = MixFogColor(_BaseColor.rgb, half3(0, 0, 0), input.fogFactor); // additive fades out in fog
                return half4(col, k * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
