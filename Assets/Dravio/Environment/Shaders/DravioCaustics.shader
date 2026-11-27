// Dravio - seabed caustics (Ocean) for URP 17. Replaces URP Particles/Unlit on the caustics quad.
// Mobile friendly: 4 samples of the T_ENV_FX_Caustics mask (photon-traced caustic web, see
// scripts/art/environment/zone_textures.py fx_caustics), additive, no decal, no depth texture.
//   * 2 samples of mip 5 (8x8 texels = smooth blobs) make low-frequency noise n1, n2 that
//     (a) warp the UVs so the cells wobble and morph instead of sliding rigidly, and
//     (b) modulate brightness in broad patches, like light through an uneven surface.
//   * layer A = warped uv * _BaseMap_ST (offset still written by EnvironmentScrollingFx);
//     layer B = same, scaled/rotated and scrolled in the shader.
//   * result = A * (1 - _Mix) + min(A, B) * _Mix * 2  (crossings flare, the web stays connected).
// _BaseColor (rgb tint, a = day/night alpha) is still driven by EnvironmentScrollingFx.
Shader "Dravio/Environment/Caustics"
{
    Properties
    {
        _BaseMap ("Caustics Mask (alpha, repeat)", 2D) = "white" {}
        _BaseColor ("Tint (a = strength)", Color) = (0.8,1,1,0.11)
        _ScaleB ("Layer B Scale", Float) = 1.31
        _RotateB ("Layer B Rotation (rad)", Float) = 0.52
        _ScrollB ("Layer B Scroll (uv/s)", Vector) = (-0.015, 0.011, 0, 0)
        _Mix ("Crossing Mix", Range(0,1)) = 0.35
        _Warp ("Wobble (uv)", Range(0,0.2)) = 0.05
        _WarpSpeed ("Wobble Speed", Float) = 1
        _Patches ("Bright Patches", Range(0,1)) = 0.5
        _NoiseRange ("Mip 5 Range (min, max)", Vector) = (0.02, 0.38, 0, 0)
        _Intensity ("Intensity", Range(0,6)) = 3
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
                half _Mix;
                half _Warp;
                float _WarpSpeed;
                half _Patches;
                float4 _NoiseRange;
                half _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0; // tiled uv (no offset)
                half   fogFactor  : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv * _BaseMap_ST.xy;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half Noise(float2 uv)
            {
                half n = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 5).a;
                return saturate((n - _NoiseRange.x) / max(_NoiseRange.y - _NoiseRange.x, 1e-3));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y * _WarpSpeed;
                float2 uv = input.uv;
                half n1 = Noise(uv * 0.37 + float2(0.031, -0.019) * t);
                half n2 = Noise(uv * 0.29 + float2(0.41, 0.17) + float2(-0.023, 0.027) * t);
                float2 ua = uv + float2(n1 - n2, n1 + n2 - 1.0h) * _Warp;

                float s, c;
                sincos(_RotateB, s, c);
                float2 b = ua * _ScaleB + float2(0.37, 0.21);
                float2 ub = float2(b.x * c - b.y * s, b.x * s + b.y * c) + _Time.y * _ScrollB.xy;

                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, ua + _BaseMap_ST.zw).a;
                half bb = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, ub).a;
                half k = saturate(a * (1.0h - _Mix) + min(a, bb) * _Mix * 2.0h);

                half m = saturate((n1 * n2 - 0.10h) / 0.40h);
                m = lerp(1.0h, smoothstep(0.0h, 1.0h, m), _Patches);

                half3 col = MixFogColor(_BaseColor.rgb, half3(0, 0, 0), input.fogFactor); // additive fades out in fog
                return half4(col, saturate(k * m * _Intensity * _BaseColor.a));
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
