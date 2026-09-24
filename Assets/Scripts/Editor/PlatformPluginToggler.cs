using System.Collections.Generic;
using Base.Platform;
using UnityEditor;

namespace Base.Editor
{
    /// <summary>
    /// Включает нативные плагины (.jslib, .aar, .jar) только для активной площадки.
    /// Нужно, потому что Unity собирает в WebGL все .jslib проекта независимо от defines,
    /// а два web-SDK в одном билде конфликтуют.
    /// </summary>
    public static class PlatformPluginToggler
    {
        public static void Apply(PlatformId active)
        {
            foreach (var platform in PlatformTargets.SdkPlatforms)
            {
                var enabled = platform == active;
                var target = PlatformTargets.BuildTargetFor(platform);

                foreach (var importer in FindPlugins(platform))
                {
                    var changed = false;

                    if (importer.GetCompatibleWithAnyPlatform())
                    {
                        importer.SetCompatibleWithAnyPlatform(false);
                        changed = true;
                    }

                    if (importer.GetCompatibleWithEditor())
                    {
                        importer.SetCompatibleWithEditor(false);
                        changed = true;
                    }

                    if (importer.GetCompatibleWithPlatform(target) != enabled)
                    {
                        importer.SetCompatibleWithPlatform(target, enabled);
                        changed = true;
                    }

                    if (changed)
                        importer.SaveAndReimport();
                }
            }
        }

        /// <summary>Список плагинов, чьё состояние не соответствует активной площадке.</summary>
        public static List<string> FindMismatches(PlatformId active)
        {
            var result = new List<string>();

            foreach (var platform in PlatformTargets.SdkPlatforms)
            {
                var enabled = platform == active;
                var target = PlatformTargets.BuildTargetFor(platform);

                foreach (var importer in FindPlugins(platform))
                {
                    if (importer.GetCompatibleWithAnyPlatform() || importer.GetCompatibleWithPlatform(target) != enabled)
                        result.Add(importer.assetPath);
                }
            }

            return result;
        }

        private static IEnumerable<PluginImporter> FindPlugins(PlatformId platform)
        {
            var folder = PlatformTargets.PluginsFolderFor(platform);
            if (folder == null || !AssetDatabase.IsValidFolder(folder))
                yield break;

            foreach (var guid in AssetDatabase.FindAssets("", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is PluginImporter importer)
                    yield return importer;
            }
        }
    }
}
