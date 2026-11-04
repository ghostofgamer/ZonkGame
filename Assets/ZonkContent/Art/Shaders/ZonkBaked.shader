// Комната с запечённым светом: только текстура, без расчёта света. Дёшево для телефонов.
// _ZonkBakedDarken — глобальное затемнение от LightingDirector (BakedTint профиля): ночной вариант локации на той же модели.
Shader "Zonk/Baked"
{
    Properties
    {
        _BaseMap("Запечённая текстура", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

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
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            fixed4 _ZonkBakedDarken;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
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
