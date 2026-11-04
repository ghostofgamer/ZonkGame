// Затемнение обучения: весь экран темнее, вокруг цели светлая рамка со скруглением (у точки на столе это круг) и пульсирующий контур.
// Считается по UV картинки без спрайта: центр, размер и скругление ставит TutorialTipWindow, остальные числа — в материале.
// Дополнительные круги (несколько костей сразу): _ExtraCircles, xy — центр в UV, z — радиус в долях высоты.
Shader "Zonk/UiSpotlight"
{
    Properties
    {
        [PerRendererData] _MainTex("Не используется (нужно UI)", 2D) = "white" {}
        _DimColor("Затемнение", Color) = (0.02, 0.015, 0.03, 0.6)
        _RingColor("Кольцо", Color) = (1, 0.78, 0.18, 1)
        _Center("Центр круга, UV", Vector) = (0.5, 0.5, 0, 0)
        _HalfSize("Половина размера рамки, доля высоты", Vector) = (0.15, 0.15, 0, 0)
        _Corner("Скругление, доля высоты", Float) = 0.15
        _Aspect("Ширина / высота картинки", Float) = 1.78
        _Soft("Мягкость края, доля высоты", Float) = 0.004
        _RingWidth("Толщина кольца, доля высоты", Float) = 0.003
        _PulseSize("Размах пульса, доля высоты", Float) = 0.006
        _PulseSpeed("Скорость пульса", Float) = 4
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "CanUseSpriteAtlas" = "False" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
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
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            fixed4 _DimColor;
            fixed4 _RingColor;
            float4 _Center;
            float4 _HalfSize;
            float _Corner;
            float _Aspect;
            float _Soft;
            float _RingWidth;
            float _PulseSize;
            float _PulseSpeed;
            float4 _ExtraCircles[5];
            float _ExtraCount;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Расстояние в долях высоты: круг остаётся кругом на любом экране.
                float2 delta = (i.uv - _Center.xy) * float2(_Aspect, 1.0);
                float pulse = (sin(_Time.y * _PulseSpeed) * 0.5 + 0.5) * _PulseSize;
                float corner = min(_Corner, min(_HalfSize.x, _HalfSize.y));
                float2 box = abs(delta) - _HalfSize.xy + corner;
                float distance = length(max(box, 0.0)) + min(max(box.x, box.y), 0.0) - corner - pulse;

                int extraCount = (int)_ExtraCount;
                for (int n = 0; n < extraCount; n++)
                {
                    float2 toCircle = (i.uv - _ExtraCircles[n].xy) * float2(_Aspect, 1.0);
                    distance = min(distance, length(toCircle) - _ExtraCircles[n].z - pulse);
                }

                float dim = smoothstep(-_Soft, _Soft, distance);
                float ring = 1.0 - smoothstep(_RingWidth, _RingWidth + _Soft, abs(distance));

                fixed4 color = _DimColor;
                color.a *= dim;
                color = lerp(color, _RingColor, ring);
                color.a *= i.color.a;
                return color;
            }
            ENDCG
        }
    }
}
