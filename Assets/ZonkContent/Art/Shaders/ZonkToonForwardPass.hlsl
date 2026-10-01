#ifndef ZONK_TOON_FORWARD_INCLUDED
#define ZONK_TOON_FORWARD_INCLUDED

// Основной проход: ступенчатый свет (солнце и лампы), блик, кромка по силуэту, растр точками в тенях.
// Всё считается в материале, без постобработки: дёшево для телефонов и браузера.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half fogFactor : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings ToonVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.normalWS = normal.normalWS;
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.fogFactor = ComputeFogFactor(position.positionCS.z);
    return output;
}

half ToonStep(half value, half threshold)
{
    half soft = max(_ZonkToonBands.z, 0.001h);
    return smoothstep(threshold - soft, threshold + soft, value);
}

// Свет 0..1 -> множитель освещения: три ступени (тень, полутень, свет); при _ZonkToonBlend = 0 — как есть.
half3 ToonRamp(half light)
{
    half3 toon = lerp(_ZonkToonShadowTint.rgb, _ZonkToonMidTint.rgb, ToonStep(light, _ZonkToonBands.x));
    toon = lerp(toon, half3(1.0h, 1.0h, 1.0h), ToonStep(light, _ZonkToonBands.y));
    return lerp(light.xxx, toon, _ZonkToonBlend);
}

// Лампы: три уровня вместо плавного спада — пятна света, как в комиксе.
half ToonLevels(half light)
{
    half toon = (ToonStep(light, 0.12h) + ToonStep(light, 0.4h) + ToonStep(light, 0.7h)) * (1.0h / 3.0h);
    return lerp(light, toon, _ZonkToonBlend);
}

// Растр: сетка точек в пикселях экрана, радиус точки растёт с тенью.
half HalftoneDots(float2 pixel, half light)
{
    half start = max(_ZonkToonBands.w, 0.001h);
    half shadow = saturate((start - light) / start);
    half radius = _ZonkToonDots.y * shadow;
    float cell = max(_ZonkToonDots.x * _ScreenParams.y / 1080.0, 2.0);
    float s, c;
    sincos(_ZonkToonDots.w, s, c);
    float2 grid = float2(c * pixel.x - s * pixel.y, s * pixel.x + c * pixel.y) / cell;
    float d = length(frac(grid) - 0.5);
    float aa = max(fwidth(d), 0.0001);
    half dotMask = 1.0h - smoothstep(radius - aa, radius + aa, d);
    return dotMask * step(0.001h, radius) * _ZonkToonDots.z * _ZonkToonBlend;
}

half4 ToonFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 albedo = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)) * _BaseColor;
#if defined(_ALPHATEST_ON)
    clip(albedo.a - _Cutoff);
#endif

    half3 normalWS = normalize(input.normalWS);
    half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    half4 shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);

    // Солнце: ступени света.
    Light mainLight = GetMainLight(inputData.shadowCoord, input.positionWS, shadowMask);
    half light = saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
    half3 lighting = mainLight.color * ToonRamp(light);

    // Блик: одна резкая ступень, у металла сильнее и цвета металла.
    half3 halfDir = SafeNormalize(mainLight.direction + viewWS);
    half spec = pow(saturate(dot(normalWS, halfDir)), exp2(10.0h * _Smoothness + 1.0h)) * light;
    half specStrength = _ZonkToonSpec.y * _Smoothness * lerp(0.25h, 1.0h, _Metallic);
    half3 specular = mainLight.color * lerp(spec, ToonStep(spec, _ZonkToonSpec.x), _ZonkToonBlend) * specStrength;

#if defined(_ADDITIONAL_LIGHTS)
    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light lamp = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
        half lampLight = saturate(dot(normalWS, lamp.direction)) * lamp.distanceAttenuation * lamp.shadowAttenuation;
        lighting += lamp.color * ToonLevels(lampLight);
    LIGHT_LOOP_END
#endif

    half3 ambient = SampleSH(normalWS);
    half3 color = albedo.rgb * (lighting + ambient) + specular * lerp(half3(1.0h, 1.0h, 1.0h), albedo.rgb, _Metallic);

    // Кромка по силуэту: отделяет стакан и кости от фона.
    half rim = 1.0h - saturate(dot(normalWS, viewWS));
    half rimMask = smoothstep(_ZonkToonRim.x - 0.04h, _ZonkToonRim.x + 0.04h, rim)
        * lerp(_ZonkToonRim.z, 1.0h, ToonStep(light, _ZonkToonBands.x));
    color += _ZonkToonRimColor.rgb * rimMask * _ZonkToonRim.y * _RimScale * _ZonkToonBlend;

    // Растр точками в тенях.
    half dots = HalftoneDots(input.positionCS.xy, light);
    color = lerp(color, color * _ZonkToonDotColor.rgb, dots);

    color += _EmissionColor.rgb;
    color = MixFog(color, input.fogFactor);
    return half4(color, albedo.a);
}

#endif
