// Dravio U1-ENV - stylized pond water for URP 17. Mobile friendly: no depth or
// opaque texture, no reflections. Two scrolling samples of one painted ripple
// texture, shore foam driven by vertex colour alpha (0 = shore, 1 = centre).
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
                o.uv.xy = uv + _Time.y * _ScrollA.xy;
                o.uv.zw = uv * 1.37 + float2(0.31, 0.17) + _Time.y * _ScrollB.xy;
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
                water += light.color * glint * 0.5h;

                half foam = 1.0h - smoothstep(0.0h, _FoamWidth, depth + (ripple - 0.5h) * 0.18h);
                water = lerp(water, _FoamColor.rgb, foam * 0.85h);

                half alpha = lerp(_ShoreOpacity, _Opacity, depth);
                alpha = max(alpha, foam * 0.9h);
                water = MixFog(water, input.fogFactor);
                return half4(water, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
