using System.IO;
using System.Linq;
using Base.Platform;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Base.Editor
{
    /// <summary>
    /// Сборка под площадку одной командой: переключает площадку и запускает BuildPipeline.
    /// Меню Base/Build или из командной строки:
    ///   Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod Base.Editor.BuildScript.BuildYandex
    ///   Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod Base.Editor.BuildScript.BuildVKGames
    ///   Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod Base.Editor.BuildScript.BuildVKPlay
    ///   Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod Base.Editor.BuildScript.BuildRuStore
    /// Результат кладётся в Builds/&lt;площадка&gt;.
    /// </summary>
    public static class BuildScript
    {
        [MenuItem("Base/Build/Yandex Games (WebGL)", priority = 0)]
        public static void BuildYandex() => Build(PlatformId.Yandex);

        [MenuItem("Base/Build/VK Games (WebGL)", priority = 1)]
        public static void BuildVKGames() => Build(PlatformId.VKGames);

        [MenuItem("Base/Build/VK Play (WebGL)", priority = 2)]
        public static void BuildVKPlay() => Build(PlatformId.VKPlay);

        [MenuItem("Base/Build/RuStore (Android APK)", priority = 3)]
        public static void BuildRuStore() => Build(PlatformId.RuStore);

        /// <summary>Сборка из меню или CLI. В batchmode завершает Unity с кодом результата.</summary>
        public static void Build(PlatformId platform)
        {
            var ok = TryBuild(platform);
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Сборка без выхода из Unity: для цепочек вроде "собрать и выложить".</summary>
        public static bool TryBuild(PlatformId platform)
        {
            PlatformSwitcher.Apply(platform);

            var target = PlatformTargets.BuildTargetFor(platform);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Base] No scenes enabled in Build Settings");
                return false;
            }

            var location = target == BuildTarget.Android
                ? $"Builds/{platform}/{ProjectIdentity.BuildFileName}.apk"
                : $"Builds/{platform}";

            if (target == BuildTarget.Android)
                EditorUserBuildSettings.buildAppBundle = false;

            // Папка WebGL-сборки заливается на хостинг целиком (выкладка VK упаковывает её всю),
            // поэтому файлы прошлой сборки, например .br после смены сжатия, удаляем заранее.
            if (target == BuildTarget.WebGL && Directory.Exists(location))
                FileUtil.DeleteFileOrDirectory(location);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                locationPathName = location,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Base] Build {platform} succeeded: {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime:mm\\:ss})");
                return true;
            }

            Debug.LogError($"[Base] Build {platform} finished with {summary.result}: {summary.totalErrors} errors");
            return false;
        }
    }
}
