#ifndef ZONK_TOON_INPUT_INCLUDED
#define ZONK_TOON_INPUT_INCLUDED

// Свойства материала Zonk/Toon (общие для всех проходов, совместимо с SRP Batcher)
// и глобальный стиль игры, который выставляет ToonStyleService из ToonStyleConfig.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseMap_TexelSize;
    half4 _BaseColor;
    half4 _EmissionColor;
    half _Cutoff;
    half _Surface;
    half _Smoothness;
    half _Metallic;
    half _OutlineScale;
    half _RimScale;
    UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

// Стиль. Если сервис их ещё не выставил, все значения 0: обычный плавный свет без точек и контура.
half _ZonkToonBlend;            // 1 — комикс, 0 — плавный свет
half4 _ZonkToonShadowTint;      // цвет освещения в тени
half4 _ZonkToonMidTint;         // цвет освещения в полутени
half4 _ZonkToonBands;           // x порог тени, y порог света, z мягкость ступени, w с какого света начинаются точки
half4 _ZonkToonDotColor;
half4 _ZonkToonDots;            // x шаг сетки (пиксели при высоте 1080), y наибольший радиус, z сила, w угол (радианы)
half4 _ZonkToonRimColor;
half4 _ZonkToonRim;             // x порог, y сила, z доля в тени
half4 _ZonkToonSpec;            // x порог блика, y сила
half4 _ZonkToonOutlineColor;
half _ZonkToonOutlineWidth;     // пиксели при высоте экрана 1080; 0 — контура нет

#endif
