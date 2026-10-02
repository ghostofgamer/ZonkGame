using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Zonk.Editor
{
    /// <summary>
    /// Перед сборкой очищает накопленные данные динамических шрифтов TextMeshPro (атлас букв, которые редактор успел
    /// нарисовать во время игры): в игре атлас всё равно строится заново из файла шрифта, а накопленный попадал в сборку
    /// (~1.7 МБ у Noto Sans). Шрифт остаётся динамическим — нужны любые буквы любого языка (имена в игре вдвоём).
    /// </summary>
    public sealed class DynamicFontCleaner : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Clean();
        }

        [MenuItem("Zonk/Build/Clear Dynamic Font Atlases")]
        public static void Clean()
        {
            var count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/ZonkContent" }))
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
                    continue;

                font.ClearFontAssetData(true);
                EditorUtility.SetDirty(font);
                count++;
            }

            if (count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[Zonk] Dynamic font atlases cleared: {count}");
            }
        }
    }
}
