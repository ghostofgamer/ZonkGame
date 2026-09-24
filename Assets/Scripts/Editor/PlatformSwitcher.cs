using System.Linq;
using Base.Platform;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Base.Editor
{
    /// <summary>
    /// Переключение проекта на площадку: активная платформа сборки, scripting defines,
    /// WebGL-шаблон и включение только нужных нативных плагинов.
    /// Меню Base/Platform.
    /// </summary>
    public static class PlatformSwitcher
    {
        [MenuItem("Base/Platform/Stub (no SDK)", priority = 0)]
        public static void SwitchToStub() => Apply(PlatformId.Stub);

        [MenuItem("Base/Platform/Yandex Games (WebGL)", priority = 1)]
        public static void SwitchToYandex() => Apply(PlatformId.Yandex);

        [MenuItem("Base/Platform/VK Games (WebGL)", priority = 2)]
        public static void SwitchToVKGames() => Apply(PlatformId.VKGames);

        [MenuItem("Base/Platform/VK Play (WebGL)", priority = 3)]
        public static void SwitchToVKPlay() => Apply(PlatformId.VKPlay);

        [MenuItem("Base/Platform/RuStore (Android)", priority = 4)]
        public static void SwitchToRuStore() => Apply(PlatformId.RuStore);

        [MenuItem("Base/Platform/Show Current", priority = 20)]
        public static void ShowCurrent()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var platform = CurrentFor(target);
            Debug.Log($"[Base] Active build target: {target}, platform: {platform}, WebGL template: {PlayerSettings.WebGL.template}, " +
                      $"compression: {PlayerSettings.WebGL.compressionFormat}, decompression fallback: {PlayerSettings.WebGL.decompressionFallback}");
        }

        /// <summary>Площадка, заданная defines для указанной целевой платформы.</summary>
        public static PlatformId CurrentFor(BuildTarget target)
        {
            var named = PlatformTargets.NamedBuildTargetFor(target);
            return PlatformTargets.ParseDefines(PlatformTargets.GetDefines(named));
        }

        public static void Apply(PlatformId platform)
        {
            var target = PlatformTargets.BuildTargetFor(platform);
            var group = BuildPipeline.GetBuildTargetGroup(target);

            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                Debug.Log($"[Base] Switching active build target to {target}");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
                {
                    Debug.LogError($"[Base] Cannot switch to {target}. Is the platform module installed in Unity Hub?");
                    return;
                }
            }

            // Defines ставим на всех целевых платформах, чтобы не осталось "хвоста" от прошлой площадки.
            SetDefines(NamedBuildTarget.WebGL, target == BuildTarget.WebGL ? platform : PlatformId.Stub);
            SetDefines(NamedBuildTarget.Android, target == BuildTarget.Android ? platform : PlatformId.Stub);
            SetDefines(NamedBuildTarget.Standalone, PlatformId.Stub);

            ProjectIdentity.Apply();
            PlayerSettings.WebGL.template = PlatformTargets.WebGLTemplateFor(platform);
            PlayerSettings.WebGL.compressionFormat = PlatformTargets.WebGLCompressionFor(platform);
            PlayerSettings.WebGL.decompressionFallback = PlatformTargets.WebGLDecompressionFallbackFor(platform);
            PlatformPluginToggler.Apply(platform);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Base] Platform switched to {platform} (build target {target})");
        }

        private static void SetDefines(NamedBuildTarget named, PlatformId platform)
        {
            var defines = PlatformTargets.GetDefines(named);
            defines.RemoveAll(d => PlatformDefines.All.Contains(d));

            var define = PlatformDefines.For(platform);
            if (define != null)
                defines.Add(define);

            PlayerSettings.SetScriptingDefineSymbols(named, string.Join(";", defines));
        }
    }
}
