using UnityEditor;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Пятно света на стол (02.10.2026): в тёмных локациях стол освещён из темноты прожектором из точки лампы
    /// (LightingProfileConfig.TableLight). 05.10: прожектор у лампы, круг света на всю столешницу. Раньше лампа в 3.6 над столом почти не доставала до стола (свет слабеет
    /// с квадратом расстояния), и подвал был чёрным. Применяется к профилю, пока у него TableLight = 0: ручная правка остаётся.
    /// Заодно: в «Доме в полночь» дождь шёл внутри комнаты — остаются гроза и молнии.
    /// </summary>
    public static partial class ZonkSetup
    {
        // Сила — яркость на сукне в центре; круг света, затухание и высота у всех по умолчанию профиля (весь стол).
        private static readonly (string Profile, float Power)[] TableLights =
        {
            ("Lighting_Basement", 1.4f),
            ("Lighting_TavernNight", 1.3f),
            ("Lighting_HomeMidnight", 1.2f),
            ("Lighting_Ship", 1f),
            ("Lighting_Tavern", 0.9f),
            ("Lighting_Attic", 0.7f),
        };

        private static void TuneTableLights()
        {
            // Узор лампы-клетки рисовался с Mathf.SmoothStep (серая каша): перерисовать один раз на месте — .meta и ссылки прежние.
            const string cookieMarker = "Library/Zonk_CageCookie_v2";
            var cookiePath = Textures + "/Cookie_Cage.png";
            if (!System.IO.File.Exists(cookieMarker) && System.IO.File.Exists(cookiePath))
            {
                var cookie = PaintCageCookie();
                System.IO.File.WriteAllBytes(cookiePath, cookie.EncodeToPNG());
                Object.DestroyImmediate(cookie);
                AssetDatabase.ImportAsset(cookiePath);
                System.IO.File.WriteAllText(cookieMarker, "redrawn");
            }

            foreach (var (name, power) in TableLights)
            {
                var profile = AssetDatabase.LoadAssetAtPath<LightingProfileConfig>(ConfigsFolder + "/Lighting/" + name + ".asset");
                if (profile == null || profile.TableLight > 0f)
                    continue;

                profile.TableLight = power;
                if (name == "Lighting_HomeMidnight")
                    profile.Effects.RemoveAll(e => e is RainEffect);
                EditorUtility.SetDirty(profile);
            }

            TuneLampMotion();
        }

        // Качание ламп и мерцание огня по локациям (05.10.2026): наклон X/Z (°), период (с), дрожь; мерцание — доля и скорость
        // (0 — без мерцания). Сильнее всего качает корабль, ярче всего мерцает подвал (дрожащая лампочка); улица — без эффектов.
        private static readonly (string Profile, Vector2 Sway, float Period, float Jitter, float Flicker, float Speed)[] LampMotion =
        {
            ("Lighting_Ship", new Vector2(15f, 10f), 6f, 0.1f, 0.08f, 1.3f),
            ("Lighting_Basement", new Vector2(1.5f, 1f), 5f, 0.3f, 0.12f, 2.5f),
            ("Lighting_Attic", new Vector2(1.5f, 1.2f), 5.5f, 0.25f, 0.06f, 1.2f),
            ("Lighting_TavernNight", new Vector2(1f, 0.7f), 6f, 0.25f, 0.1f, 1.5f),
            ("Lighting_Tavern", new Vector2(0.8f, 0.6f), 6f, 0.25f, 0.08f, 1.5f),
            ("Lighting_HomeMidnight", new Vector2(0.8f, 0.6f), 6.5f, 0.2f, 0.05f, 1f),
            ("Lighting_Home", new Vector2(0.4f, 0.3f), 7f, 0.2f, 0f, 0f),
        };

        /// <summary>Добавляет эффекты один раз (метка в Library): удалённый руками эффект не возвращается.</summary>
        private static void TuneLampMotion()
        {
            const string marker = "Library/Zonk_LampMotion_v1";
            if (System.IO.File.Exists(marker))
                return;

            foreach (var (name, sway, period, jitter, flicker, speed) in LampMotion)
            {
                var profile = AssetDatabase.LoadAssetAtPath<LightingProfileConfig>(ConfigsFolder + "/Lighting/" + name + ".asset");
                if (profile == null)
                    continue;

                if (!profile.Effects.Exists(e => e is LampSwayEffect))
                    profile.Effects.Add(new LampSwayEffect { Amplitude = sway, Period = period, Jitter = jitter });
                if (flicker > 0f && !profile.Effects.Exists(e => e is LightFlickerEffect))
                    profile.Effects.Add(new LightFlickerEffect { Amount = flicker, Speed = speed });
                EditorUtility.SetDirty(profile);
            }

            System.IO.File.WriteAllText(marker, "added");
        }
    }
}
