using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Вес сборки (02.10.2026, замер WebGL для Яндекса: стартовая загрузка ~19 МБ). Данные постобработки URP по умолчанию
    /// тащат в сборку 10 текстур зернистости плёнки (~2.5 МБ), а зернистость в игре не используется: рендеры получают
    /// облегчённую копию без них (Assets/Settings/PostProcessData_Lite.asset). Остальное (заставка Unity, демо Zenject,
    /// шрифт LiberationSans) убрано из проекта; шрифты надписей — FixUiFonts.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string LitePostProcessPath = "Assets/Settings/PostProcessData_Lite.asset";
        private static readonly string[] RendererPaths = { "Assets/Settings/Mobile_Renderer.asset", "Assets/Settings/PC_Renderer.asset" };

        private static void SlimPostProcessData()
        {
            var lite = AssetDatabase.LoadAssetAtPath<PostProcessData>(LitePostProcessPath);
            foreach (var rendererPath in RendererPaths)
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                if (renderer == null)
                    continue;

                var serialized = new SerializedObject(renderer);
                var property = serialized.FindProperty("postProcessData");
                if (property == null)
                    continue;

                if (lite == null)
                {
                    var source = property.objectReferenceValue as PostProcessData;
                    if (source == null)
                        return;

                    lite = Object.Instantiate(source);
                    var liteSerialized = new SerializedObject(lite);
                    var grain = liteSerialized.FindProperty("textures.filmGrainTex");
                    if (grain != null && grain.isArray)
                        grain.arraySize = 0;
                    liteSerialized.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.CreateAsset(lite, LitePostProcessPath);
                }

                if (property.objectReferenceValue == lite)
                    continue;

                property.objectReferenceValue = lite;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(renderer);
            }
        }
    }
}
