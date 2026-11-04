// Качание запечённых деталей (листья пальм, купол зонта): вершины ходят в шейдере, без анимации и расчёта света.
// Чем выше вершина над _SwayBase, тем больше размах. Текстура та же, что у предметов. Затемнение — как у Zonk/Baked.
Shader "Zonk/BakedSway"
{
    Properties
    {
        _BaseMap("Запечённая текстура", 2D) = "white" {}
        _SwayAmount("Размах на верху, метры", Float) = 0.05
        _SwaySpeed("Скорость", Float) = 1.1
        _SwayBase("Высота без качания", Float) = -1.6
        _SwayRange("Высота полного размаха над основанием", Float) = 5
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
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _BaseMap;
            fixed4 _ZonkBakedDarken;
            float _SwayAmount, _SwaySpeed, _SwayBase, _SwayRange;

            v2f vert(appdata v)
            {
                v2f o;
                float4 world = mul(unity_ObjectToWorld, v.vertex);
                float weight = saturate((world.y - _SwayBase) / _SwayRange);
                float phase = world.x * 0.9 + world.z * 0.7;
                float wave = sin(_Time.y * _SwaySpeed + phase) + 0.4 * sin(_Time.y * _SwaySpeed * 2.3 + phase * 1.7);
                world.x += wave * _SwayAmount * weight;
                world.z += wave * _SwayAmount * 0.6 * weight;
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
