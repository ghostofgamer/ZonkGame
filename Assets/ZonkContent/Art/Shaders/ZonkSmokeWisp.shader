// Лёгкий дымок: плоскость с бегущим шумом. Две выборки одной текстуры, без частиц. Для камина, свечей, трубки.
// Шум тот же, что у дымной тени (Art/Textures/Smoke/Shadow_Noise). Все числа — в материале.
Shader "Zonk/SmokeWisp"
{
    Properties
    {
        _Noise("Шум", 2D) = "gray" {}
        _Color("Цвет дыма", Color) = (0.2, 0.18, 0.17, 0.6)
        _Scroll("Скорость вверх", Float) = 0.18
        _Scale("Размер клубов", Float) = 0.55
        _Thin("Истончение к верху", Range(0, 1)) = 0.75
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "SmokeWisp"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Noise); SAMPLER(sampler_Noise);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Scroll;
                float _Scale;
                half _Thin;
                float4 _Noise_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y * _Scroll;
                half bend = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, float2(uv.x * 0.5, uv.y * _Scale - time)).r - 0.5;
                float2 q = float2(uv.x + bend * 0.45 * uv.y, uv.y * _Scale - time * 1.6);
                half puffs = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, float2(q.x * 1.3, q.y)).b;

                // Столб по центру сужается к верху, снизу и сверху растворяется.
                half width = lerp(0.42, 0.18, uv.y);
                half column = 1.0 - smoothstep(width * 0.4, width, abs(uv.x + bend * 0.45 * uv.y - 0.5));
                half fade = smoothstep(0.0, 0.14, uv.y) * (1.0 - smoothstep(1.0 - _Thin, 1.0, uv.y));
                half alpha = column * fade * smoothstep(0.35, 0.72, puffs) * _Color.a;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
