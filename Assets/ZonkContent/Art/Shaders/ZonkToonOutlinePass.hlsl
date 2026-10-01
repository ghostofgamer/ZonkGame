#ifndef ZONK_TOON_OUTLINE_INCLUDED
#define ZONK_TOON_OUTLINE_INCLUDED

// Контур: объект ещё раз, только задние грани, раздвинутые по нормали в экранном пространстве.
// Толщина одна и та же в пикселях на любом расстоянии и разрешении (задаётся для высоты экрана 1080).

struct OutlineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct OutlineVaryings
{
    float4 positionCS : SV_POSITION;
    half fogFactor : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

OutlineVaryings OutlineVertex(OutlineAttributes input)
{
    OutlineVaryings output = (OutlineVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float width = _ZonkToonOutlineWidth * _OutlineScale * _ZonkToonBlend;
    if (width <= 0.0)
    {
        // Контур выключен: вершина за пределами экрана, треугольник отбрасывается до растеризации.
        output.positionCS = float4(2.0, 2.0, 2.0, 1.0);
        return output;
    }

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float4 positionCS = TransformWorldToHClip(positionWS);
    float3 normalCS = TransformWorldToHClipDir(TransformObjectToWorldNormal(input.normalOS));
    float2 dir = normalCS.xy;
    float len = length(dir);
    dir = len > 1e-5 ? dir / len : float2(0.0, 0.0);

    float pixels = width * _ScreenParams.y / 1080.0;
    positionCS.xy += dir * (pixels * 2.0 / _ScreenParams.xy) * positionCS.w;

    output.positionCS = positionCS;
    output.fogFactor = ComputeFogFactor(positionCS.z);
    return output;
}

half4 OutlineFragment(OutlineVaryings input) : SV_Target
{
    return half4(MixFog(_ZonkToonOutlineColor.rgb, input.fogFactor), 1.0h);
}

#endif
