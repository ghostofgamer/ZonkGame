// Соперник-тень: тёмное ядро, расходящееся в дым, и живые глаза. Одна плоскость, без частиц и скелета.
// Форма — поле расстояний в текстуре (Shadow_Body), движение дыма — бегущий шум (Shadow_Noise), руки считаются здесь же:
// они тянутся к рукавам 3D-рук (_WristL, _WristR ставит OpponentAvatarView). 4 чтения текстур на пиксель.
// Единицы p: голова в нуле, радиус около 0.5. Tools/Blender/zonk_smoke.py рисует текстуры в этих же единицах.
Shader "Zonk/ShadowSmoke"
{
    Properties
    {
        [MainColor] _BaseColor("Оттенок края дыма (BodyColor соперника)", Color) = (0.25, 0.25, 0.3, 1)
        _Mask("Форма фигуры", 2D) = "white" {}
        _Noise("Шум", 2D) = "gray" {}
        _CoreColor("Цвет ядра", Color) = (0.018, 0.014, 0.026, 1)
        _EdgeBrightness("Яркость края дыма", Range(0, 4)) = 1.7
        _Scroll("Скорость дыма вверх", Float) = 0.32
        _Warp("Изгиб фигуры", Float) = 0.75
        _Puffs("Рваность края", Float) = 0.85
        _Haze("Плотность дымки", Range(0, 1)) = 0.72
        _Wisps("Плотность струек", Range(0, 1)) = 0.6
        _HeadFirm("Чёткость головы (меньше — чётче)", Range(0, 1)) = 0.28
        _ArmWidth("Толщина рук", Float) = 0.23
        _ShoulderL("Плечо левое", Vector) = (-1.15, -1.05, 0, 0)
        _ShoulderR("Плечо правое", Vector) = (1.15, -1.05, 0, 0)
        _WristL("Запястье левое", Vector) = (-2.2, -0.6, 0, 0)
        _WristR("Запястье правое", Vector) = (2.2, -0.6, 0, 0)
        [HDR] _EyeColor("Цвет глаз", Color) = (1, 0.74, 0.15, 1)
        _EyeGap("Расстояние глаз от центра", Float) = 0.21
        _EyeHeight("Высота глаз", Float) = 0.04
        _EyeSize("Размер глаз", Float) = 1
        _EyeOpen("Открытость глаз", Float) = 1
        _EyeAngle("Наклон глаз (злость)", Float) = 0.25
        _EyeShift("Сдвиг взгляда", Vector) = (0, 0, 0, 0)
        _BlinkEvery("Моргать раз в секунд", Float) = 4.3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ShadowSmoke"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Границы формы в текстуре и кодировка расстояния: как в zonk_smoke.py.
            #define MASK_X 3.0
            #define MASK_Y_MIN -3.5
            #define MASK_Y_SPAN 5.0
            #define DIST_MIN -1.0
            #define DIST_SPAN 3.0
            // Плоскость в единицах p: ширина и низ. Размер плоскости в мире задан так, что единица p равна 0.9 метра.
            #define QUAD_W 7.2
            #define QUAD_H 5.0
            #define QUAD_Y_CENTER -1.0
            // Шум: одна плитка на столько единиц p.
            #define NOISE_TILE 8.0

            TEXTURE2D(_Mask); SAMPLER(sampler_Mask);
            TEXTURE2D(_Noise); SAMPLER(sampler_Noise);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _CoreColor;
                half _EdgeBrightness;
                float _Scroll;
                float _Warp;
                float _Puffs;
                half _Haze;
                half _Wisps;
                half _HeadFirm;
                float _ArmWidth;
                float4 _ShoulderL;
                float4 _ShoulderR;
                float4 _WristL;
                float4 _WristR;
                half4 _EyeColor;
                float _EyeGap;
                float _EyeHeight;
                float _EyeSize;
                float _EyeOpen;
                float _EyeAngle;
                float4 _EyeShift;
                float _BlinkEvery;
                float4 _Mask_ST;
                float4 _Noise_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 p : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.p = float2(input.positionOS.x * QUAD_W, input.positionOS.y * QUAD_H + QUAD_Y_CENTER);
                return output;
            }

            float Segment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / max(dot(ba, ba), 0.0001));
                return length(pa - ba * h);
            }

            float SmoothMin(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5 * (b - a) / k);
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.p;
                float time = _Time.y;
                float2 up = float2(0.0, -time * _Scroll / NOISE_TILE);

                // Голова держит форму, низ расходится в дым.
                float loose = lerp(_HeadFirm, 1.0, saturate((-0.35 - p.y) / 0.9));

                half4 warp = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, p * 1.1 / NOISE_TILE + up);
                float2 q = p + (warp.rg - 0.5) * _Warp * loose;

                float2 maskUv = float2(q.x / (MASK_X * 2.0) + 0.5, (q.y - MASK_Y_MIN) / MASK_Y_SPAN);
                float d = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, maskUv).r * DIST_SPAN + DIST_MIN;
                d = SmoothMin(d, Segment(q, _ShoulderL.xy, _WristL.xy) - _ArmWidth, 0.3);
                d = SmoothMin(d, Segment(q, _ShoulderR.xy, _WristR.xy) - _ArmWidth, 0.3);

                half4 fine = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, (p * 2.6 + warp.rg) / NOISE_TILE + up * 1.7);
                float e = d + (fine.b - 0.5) * _Puffs * loose;

                half core = 1.0 - smoothstep(-0.22, 0.16, e);
                half haze = (1.0 - smoothstep(-0.05, 0.7, e)) * smoothstep(0.22, 0.6, fine.b + 0.1);
                half ridge = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, float2(p.x * 1.7, p.y * 0.7) / NOISE_TILE + up * 1.5).a;
                half wisps = pow(ridge, 14.0) * (1.0 - smoothstep(0.1, 0.85, d)) * _Wisps;

                half alpha = max(core, max(haze * _Haze, wisps));
                half3 edge = _BaseColor.rgb * _EdgeBrightness;
                half3 color = lerp(edge, _CoreColor.rgb, smoothstep(0.1, 0.8, alpha));

                // Глаза: миндаль с ореолом, иногда моргают. Настроение задают _EyeOpen и _EyeAngle.
                float blinkTime = fmod(time, _BlinkEvery) - 0.1;
                float blink = smoothstep(0.0, 0.05, abs(blinkTime) - 0.06);
                float open = max(0.04, 0.08 + 0.92 * blink * _EyeOpen);
                float2 eye = float2(abs(p.x - _EyeShift.x) - _EyeGap, (p.y - _EyeHeight - _EyeShift.y) / open);
                eye.x += eye.y * _EyeAngle;
                float radius = length(eye * float2(1.0, 1.9)) / max(_EyeSize, 0.01);
                half eyeCore = 1.0 - smoothstep(0.05, 0.085, radius);
                half glow = exp(-radius * radius * 34.0) * 0.75 * blink;
                color = lerp(color, _EyeColor.rgb, saturate(glow));
                color = lerp(color, half3(1.0, 0.97, 0.85), eyeCore);
                alpha = max(alpha, max(eyeCore, glow));

                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
