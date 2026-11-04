// Мультяшная вода: плавный цвет от мелководья к глубине, по ней ползут две сетки светлых ячеек, у кромки густая рваная пена,
// вдоль берега ходит тонкая волна, прилив двигает край по песку. Без расчёта света и отражений: две выборки одной маленькой
// текстуры и немного математики, рассчитано на слабые телефоны. Формула та же, что в Tools/Blender/zonk_water.py: там можно
// посмотреть кадр без Unity. Цвета и скорости — в материале M_Water. Затемнение ночных вариантов — как у Zonk/Baked.
Shader "Zonk/Water"
{
    Properties
    {
        _Cells("Ячейки (Water_Cells)", 2D) = "black" {}

        [Header(Colors)]
        _Shallow("Мелководье", Color) = (0.46, 0.93, 0.86, 1)
        _Middle("Средняя глубина", Color) = (0.13, 0.72, 0.83, 1)
        _Deep("Глубина", Color) = (0.05, 0.42, 0.74, 1)
        _Far("Даль", Color) = (0.42, 0.74, 0.90, 1)
        _Lines("Линии ячеек", Color) = (0.80, 0.98, 0.97, 1)
        _Foam("Пена", Color) = (1, 1, 0.98, 1)
        _WetSand("Мокрый песок", Color) = (0.45, 0.33, 0.21, 0.55)

        [Header(Shore)]
        _ShoreZ("Линия берега, z", Float) = 5.6
        _TideSize("Размах прилива, метры", Float) = 0.45
        _TideSpeed("Скорость прилива", Float) = 0.5
        _WetWidth("Ширина мокрого песка, метры", Float) = 1.2
        _SurfWidth("Ширина пены у кромки, метры", Float) = 0.55

        [Header(Cells)]
        _ScaleA("Размер первой сетки", Float) = 0.3
        _ScaleB("Размер второй сетки", Float) = 0.19
        _LineFade("Линии гаснут к, метры от берега", Float) = 14
        _WaveSpeed("Скорость волны у берега", Float) = 0.17
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 world : TEXCOORD0;
            };

            sampler2D _Cells;
            fixed4 _Shallow, _Middle, _Deep, _Far, _Lines, _Foam, _WetSand;
            float _ShoreZ, _TideSize, _TideSpeed, _WetWidth, _SurfWidth;
            float _ScaleA, _ScaleB, _LineFade, _WaveSpeed;
            fixed4 _ZonkBakedDarken;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float x = i.world.x;
                float z = i.world.y;
                float swing = sin(x * 0.55 + t * 0.25) * 0.28 + sin(x * 1.35 - t * 0.2) * 0.13;
                float tide = sin(t * _TideSpeed) * _TideSize;
                float depth = z - _ShoreZ + swing * 0.6;
                float edge = z - _ShoreZ - tide + swing;

                float a = tex2D(_Cells, float2(x * _ScaleA + t * 0.018, z * _ScaleA + t * 0.03)).r;
                float b = tex2D(_Cells, float2(x * _ScaleB - t * 0.024 + 0.37, z * _ScaleB + t * 0.014 + 0.61)).r;

                fixed3 color = lerp(_Shallow.rgb, _Middle.rgb, smoothstep(0.0, 2.6, depth));
                color = lerp(color, _Deep.rgb, smoothstep(2.0, 9.0, depth));

                // Светлые линии ячеек: вдали слабеют, чтобы не рябило.
                float lines = saturate(smoothstep(0.34, 0.52, a) * 0.65 + smoothstep(0.40, 0.62, b) * 0.4);
                lines *= 0.85 - 0.75 * smoothstep(2.0, _LineFade, depth);
                color = lerp(color, _Lines.rgb, lines);

                // Волна: тонкая рваная линия пены отходит от берега и гаснет.
                float wavePhase = frac(t * _WaveSpeed);
                float waveAt = 0.5 + wavePhase * 3.0;
                float ragged = (a + b) * 0.25;
                float wave = (1.0 - smoothstep(0.04, 0.17, abs(depth - waveAt + ragged))) * sin(wavePhase * 3.14159);
                color = lerp(color, _Foam.rgb, wave * (1.0 - smoothstep(0.12, 0.28, b) * 0.6));

                // Прибой у кромки: густая рваная пена.
                float surfWidth = _SurfWidth + 0.22 * sin(x * 2.3 + t * 0.6) * sin(x * 0.9 - t * 0.3);
                float surf = 1.0 - smoothstep(surfWidth - 0.05, surfWidth + 0.04, edge + a * 0.42);
                color = lerp(color, _Foam.rgb, surf);

                color = lerp(color, _Far.rgb, smoothstep(24.0, 60.0, depth));

                // За кромкой вода прозрачна, виден песок, ближе к воде он мокрый.
                float alpha = smoothstep(-0.05, 0.05, edge);
                float wetEdge = z - _ShoreZ + _TideSize + sin(x * 0.55 + t * 0.1) * 0.2;
                float wet = smoothstep(-_WetWidth, -0.1, wetEdge) * _WetSand.a;
                color = lerp(_WetSand.rgb, color, alpha);
                color *= 1.0 - _ZonkBakedDarken.rgb;
                return fixed4(color, saturate(alpha + wet));
            }
            ENDCG
        }
    }
}
