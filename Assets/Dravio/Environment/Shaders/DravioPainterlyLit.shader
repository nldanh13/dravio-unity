// Dravio U1-ENV - stylized "painterly" lit shader for the Origin Sanctuary kit.
// URP 17 (Unity 6). Mobile friendly: main light only, SH ambient, no normal maps,
// one texture sample (+1 emission). SRP Batcher compatible (all material
// properties live in UnityPerMaterial).
//   * soft two-tone light ramp with a cool painted shadow tint
//   * vertex colour RGB = painted AO, vertex colour A = wind weight / path mask
//   * optional alpha clip (foliage cards) and pulsing emission (runes, glow caps)
//   * U1-ENV.3 globals (set by the Living Environment, all zero by default so the
//     U1-ENV.2 look is unchanged when no Living Environment is in the scene):
//       _DravioWind   x = extra wind strength, y = gust 0..1, zw = gust direction (xz)
//       _DravioLiving x = night glow 0..1 (emission boost), y = ambient floor cut 0..1
Shader "Dravio/Environment/Painterly Lit"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        [NoScaleOffset] _EmissionMap ("Emission Map", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionPulse ("Emission Pulse", Range(0,1)) = 0.35
        _PulseSpeed ("Pulse Speed", Float) = 1.3

        _ShadowTint ("Shadow Tint", Color) = (0.62,0.66,0.86,1)
        _RampThreshold ("Ramp Threshold", Range(-1,1)) = 0.05
        _RampSoftness ("Ramp Softness", Range(0.01,1)) = 0.32
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 1
        _AmbientFloor ("Ambient Floor", Color) = (0.24,0.26,0.30,1)
        _VertexAO ("Vertex AO Strength", Range(0,1)) = 1
        _RimColor ("Rim Color", Color) = (1,0.92,0.74,1)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.12

        _WindStrength ("Wind Strength (x vertex A)", Range(0,0.5)) = 0
        _WindSpeed ("Wind Speed", Float) = 1.6
        _PathColor ("Path Colour (x vertex A)", Color) = (0.62,0.49,0.33,1)
        _PathBlend ("Path Blend", Range(0,1)) = 0

        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.45
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap);  SAMPLER(sampler_EmissionMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half _EmissionPulse;
            half _PulseSpeed;
            half4 _ShadowTint;
            half _RampThreshold;
            half _RampSoftness;
            half _AmbientStrength;
            half4 _AmbientFloor;
            half _VertexAO;
            half4 _RimColor;
            half _RimStrength;
            half _WindStrength;
            half _WindSpeed;
            half4 _PathColor;
            half _PathBlend;
            half _Cutoff;
        CBUFFER_END

        // Globals (outside UnityPerMaterial, so the SRP Batcher is unaffected).
        float4 _DravioWind;
        half4 _DravioLiving;

        float3 DravioWind(float3 positionWS, half weight)
        {
            float gust = saturate(_DravioWind.y);
            float w = weight * _WindStrength * max(0.0, 1.0 + _DravioWind.x + gust * 0.8);
            float phase = _Time.y * _WindSpeed + positionWS.x * 0.6 + positionWS.z * 0.45;
            positionWS.x += sin(phase) * w;
            positionWS.z += cos(phase * 0.83) * w * 0.6;
            // Gusts lean foliage downwind (amplitude only, so no phase pops).
            float lean = weight * _WindStrength * gust * (0.75 + 0.25 * sin(phase * 0.37));
            positionWS.xz += _DravioWind.zw * lean;
            return positionWS;
        }

        half DravioAlpha(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = DravioWind(TransformObjectToWorld(input.positionOS.xyz), input.color.a);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.color = input.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                    clip(albedo.a - _Cutoff);
                #endif

                // Painted ambient occlusion from vertex colour, then the earth path blend.
                albedo.rgb *= lerp(half3(1,1,1), input.color.rgb, _VertexAO);
                half luma = dot(albedo.rgb, half3(0.299, 0.587, 0.114));
                half3 pathCol = _PathColor.rgb * (0.7 + 0.9 * luma);
                albedo.rgb = lerp(albedo.rgb, pathCol, saturate(input.color.a * _PathBlend));

                half3 n = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light light = GetMainLight(shadowCoord);
                half ndl = dot(n, light.direction);
                half ramp = smoothstep(_RampThreshold - _RampSoftness, _RampThreshold + _RampSoftness, ndl);
                ramp *= light.shadowAttenuation * light.distanceAttenuation;

                half3 ambient = max(SampleSH(n), _AmbientFloor.rgb * (1.0h - saturate(_DravioLiving.y))) * _AmbientStrength;
                half3 lighting = light.color * ramp + ambient * lerp(_ShadowTint.rgb, half3(1,1,1), ramp);

                half3 viewDir = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0h - saturate(dot(n, viewDir)), 3.0h) * _RimStrength * ramp;

                half3 color = albedo.rgb * lighting + _RimColor.rgb * rim;

                half pulse = 1.0h - _EmissionPulse * 0.5h
                           + _EmissionPulse * 0.5h * sin(_Time.y * _PulseSpeed + input.positionWS.x * 0.7 + input.positionWS.z * 0.5);
                half glow = 1.0h + saturate(_DravioLiving.x) * 1.6h;
                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb * pulse * glow;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

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
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                float3 positionWS = DravioWind(TransformObjectToWorld(input.positionOS.xyz), input.color.a);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(DravioAlpha(input.uv) - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = DravioWind(TransformObjectToWorld(input.positionOS.xyz), input.color.a);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(DravioAlpha(input.uv) - _Cutoff);
                #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
