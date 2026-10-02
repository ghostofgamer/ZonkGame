using UnityEditor;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Пятно света на стол (02.10.2026): в тёмных локациях стол освещён из темноты прожектором из точки лампы
    /// (LightingProfileConfig.TableLight). Раньше лампа в 3.6 над столом почти не доставала до стола (свет слабеет
    /// с квадратом расстояния), и подвал был чёрным. Применяется к профилю, пока у него TableLight = 0: ручная правка остаётся.
    /// Заодно: в «Доме в полночь» дождь шёл внутри комнаты — остаются гроза и молнии.
    /// </summary>
    public static partial class ZonkSetup
    {
        private static readonly (string Profile, float Power, float Angle)[] TableLights =
        {
            ("Lighting_Basement", 14f, 85f),
            ("Lighting_TavernNight", 10f, 95f),
            ("Lighting_HomeMidnight", 9f, 95f),
            ("Lighting_Ship", 6f, 100f),
            ("Lighting_Tavern", 5f, 100f),
            ("Lighting_Attic", 4f, 100f),
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

            foreach (var (name, power, angle) in TableLights)
            {
                var profile = AssetDatabase.LoadAssetAtPath<LightingProfileConfig>(ConfigsFolder + "/Lighting/" + name + ".asset");
                if (profile == null || profile.TableLight > 0f)
                    continue;

                profile.TableLight = power;
                profile.TableLightAngle = angle;
                if (name == "Lighting_HomeMidnight")
                    profile.Effects.RemoveAll(e => e is RainEffect);
                EditorUtility.SetDirty(profile);
            }
        }
    }
}
