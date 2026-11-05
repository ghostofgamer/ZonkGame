// Лёгкий ветер в запечённых листьях пальм: вершины ходят в шейдере, без анимации и расчёта света.
// Цвет вершин задан в Tools/Blender/zonk_beach.py: R сколько гнётся (у ствола 0, на конце листа 1), G своя фаза листа,
// B дрожь кончиков листочков. Текстура та же, что у предметов. Затемнение — как у Zonk/Baked.
Shader "Zonk/BakedSway"
{
    Properties
    {
        _BaseMap("Запечённая текстура", 2D) = "white" {}
        _Bend("Размах конца листа, метры", Float) = 0.13
        _WindSpeed("Скорость", Float) = 1.15
        _Gust("Сила порывов (0 — ровный ветер, 1 — временами стихает)", Range(0, 1)) = 0.45
        _Flutter("Дрожь кончиков листочков, метры", Float) = 0.022
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _BaseMap;
            fixed4 _ZonkBakedDarken;
            float _Bend, _WindSpeed, _Gust, _Flutter;

            v2f vert(appdata v)
            {
                v2f o;
                float4 world = mul(unity_ObjectToWorld, v.vertex);
                float t = _Time.y * _WindSpeed;
                float phase = v.color.g * 6.283;

                // Лист гнётся целиком: у всех его вершин одна фаза, поэтому не рвётся и не рябит. Порыв — общий на крону.
                float gust = 1.0 - _Gust * (0.5 + 0.5 * sin(t * 0.31 + world.x * 0.08));
                float wave = (sin(t + phase) + 0.4 * sin(t * 2.3 + phase * 1.7)) * gust * _Bend * v.color.r;
                world.y += wave;
                world.x += wave * 0.7;
                world.z += wave * 0.35;

                // Кончики листочков дрожат каждый по-своему.
                world.y += sin(t * 6.0 + phase * 3.0 + world.x * 5.0 + world.z * 4.0) * _Flutter * v.color.b * gust;

                o.vertex = mul(UNITY_MATRIX_VP, world);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = tex2D(_BaseMap, i.uv);
                color.rgb *= 1.0 - _ZonkBakedDarken.rgb;
                color.a = 1.0;
                return color;
            }
            ENDCG
        }
    }
}
