// Dravio U1-ENV - stylized pond water for URP 17. Mobile friendly: no depth or
// opaque texture, no reflections. Two scrolling samples of one painted ripple
// texture, shore foam driven by vertex colour alpha (0 = shore, 1 = centre).
// U1-ENV.3: up to 4 expanding ripple rings (_DravioRipples, set by EnvironmentWater
// when a dragon drinks or splashes) and a gust push of the ripple pattern (_DravioWind).
// Both globals are zero by default, which gives the U1-ENV.2 look.
Shader "Dravio/Environment/Painterly Water"
{
    Properties
    {
        _BaseMap ("Ripple Texture", 2D) = "white" {}
        _ShallowColor ("Shallow Colour", Color) = (0.42,0.78,0.74,1)
        _DeepColor ("Deep Colour", Color) = (0.13,0.42,0.50,1)
        _FoamColor ("Shore Foam", Color) = (0.93,0.98,0.92,1)
        _FoamWidth ("Foam Width", Range(0,1)) = 0.28
        _Opacity ("Centre Opacity", Range(0,1)) = 0.82
        _ShoreOpacity ("Shore Opacity", Range(0,1)) = 0.55
        _ScrollA ("Scroll A (xy)", Vector) = (0.025, 0.012, 0, 0)
        _ScrollB ("Scroll B (xy)", Vector) = (-0.018, 0.02, 0, 0)
        _SparkleStrength ("Sun Glint", Range(0,2)) = 0.6
        _BrightPond ("Bright Fantasy pond", Range(0,1)) = 0
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                half _FoamWidth;
                half _Opacity;
                half _ShoreOpacity;
                float4 _ScrollA;
                float4 _ScrollB;
                half _SparkleStrength;
                half _BrightPond;
            CBUFFER_END

            float4 _DravioWind;
            float4 _DravioRipples[4];   // xy = world xz, z = start time (_Time.y), w = strength
            float4 _DravioRippleParams; // x = ring speed m/s, y = ring width m, z = lifetime s

            half DravioRipples(float2 xz)
            {
                float speed = _DravioRippleParams.x > 0 ? _DravioRippleParams.x : 0.9;
                float width = _DravioRippleParams.y > 0 ? _DravioRippleParams.y : 0.12;
                float life = _DravioRippleParams.z > 0 ? _DravioRippleParams.z : 2.2;
                half sum = 0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float4 r = _DravioRipples[i];
                    float age = _Time.y - r.z;
                    if (r.w <= 0 || age < 0 || age > life) continue;
                    float d = distance(xz, r.xy);
                    float x = (d - age * speed) / width;
                    float fade = (1 - age / life);
                    sum += exp(-x * x) * fade * fade * r.w;
                }
                return saturate(sum);
            }

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
                float4 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
                half   shore      : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                float2 uv = TRANSFORM_TEX(input.uv, _BaseMap);
                // Gusts push the ripple pattern downwind (offset, not speed, so no pops).
                float2 push = _DravioWind.zw * saturate(_DravioWind.y) * 0.05;
                o.uv.xy = uv + _Time.y * _ScrollA.xy + push;
                o.uv.zw = uv * 1.37 + float2(0.31, 0.17) + _Time.y * _ScrollB.xy + push * 0.6;
                o.shore = input.color.a;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy).rgb;
                half3 b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.zw).rgb;
                half ripple = dot(a * b, half3(0.333h, 0.333h, 0.333h)) * 2.0h;

                half depth = saturate(input.shore);
                half3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth);
                water *= 0.75h + 0.5h * ripple;

                Light light = GetMainLight();
                half lit = saturate(light.direction.y) * 0.6h + 0.4h;
                water *= light.color * lit + SampleSH(half3(0,1,0)) * 0.35h;

                half glint = saturate((ripple - 0.62h) * 6.0h) * _SparkleStrength;
                if (_BrightPond > 0.0h)
                {
                    // Reuse the two existing ripple samples for a gently perturbed
                    // surface normal; no reflection probe or camera texture required.
                    half3 n = normalize(half3((a.r - 0.5h) * 0.38h, 1.0h,
                                               (b.r - 0.5h) * 0.38h));
                    half3 v = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                    half3 h = SafeNormalize(light.direction + v);
                    half spec = smoothstep(0.82h, 0.98h, saturate(dot(n,h)))
                              * smoothstep(0.38h, 0.85h, ripple) * _SparkleStrength
                              * saturate(light.direction.y);
                    glint = lerp(glint, spec, _BrightPond);
                    half fresnel = pow(1.0h - saturate(dot(n,v)), 3.0h);
                    water += light.color * fresnel * 0.13h * _BrightPond;
                }
                water += light.color * glint * 0.5h;

                half rings = DravioRipples(input.positionWS.xz);
                water += light.color * rings * 0.35h + rings * 0.08h;

                half foam = 1.0h - smoothstep(0.0h, _FoamWidth, depth + (ripple - 0.5h) * 0.18h);
                if (_BrightPond > 0.0h)
                {
                    half crest = 1.0h - smoothstep(0.0h, max(_FoamWidth * 0.46h, 0.001h),
                                                    depth + (ripple - 0.5h) * 0.11h);
                    foam = lerp(foam, saturate(foam * 0.32h + crest * (0.44h + 0.22h * saturate(ripple))), _BrightPond);
                }
                water = lerp(water, _FoamColor.rgb, foam * 0.85h);

                half alpha = lerp(_ShoreOpacity, _Opacity, depth);
                alpha = max(alpha, max(foam * 0.9h, rings * 0.85h));
                water = MixFog(water, input.fogFactor);
                return half4(water, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
