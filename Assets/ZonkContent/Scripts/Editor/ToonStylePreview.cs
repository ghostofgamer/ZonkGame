using UnityEditor;
using Zonk.Configs;
using Zonk.Presentation;

namespace Zonk.Editor
{
    /// <summary>
    /// Стиль Zonk/Toon в редакторе без запуска игры: окно сцены и превью показывают тот же вид, что в игре
    /// (на уровне High). Правка ToonStyleConfig в инспекторе видна сразу. В игре стиль выставляет ToonStyleService.
    /// </summary>
    [InitializeOnLoad]
    public static class ToonStylePreview
    {
        private const string GameConfigPath = "Assets/ZonkContent/Configs/Game/GameConfig.asset";

        static ToonStylePreview()
        {
            ToonStyleConfig.EditorChanged += OnChanged;
            EditorApplication.delayCall += Apply;
        }

        private static void OnChanged(ToonStyleConfig style)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                Apply();
        }

        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            ToonStyleService.Apply(config != null ? config.ToonStyle : null, Base.Services.Quality.QualityTier.High);
            SceneView.RepaintAll();
        }
    }
}
