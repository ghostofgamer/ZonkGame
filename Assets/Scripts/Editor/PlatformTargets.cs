using System;
using System.Collections.Generic;
using System.Linq;
using Base.Platform;
using UnityEditor;
using UnityEditor.Build;

namespace Base.Editor
{
    /// <summary>Соответствие площадки целевой платформе Unity, шаблону WebGL и папке плагинов.</summary>
    public static class PlatformTargets
    {
        public const string PlatformRoot = "Assets/Scripts/Platform";

        public static BuildTarget BuildTargetFor(PlatformId platform)
        {
            switch (platform)
            {
                case PlatformId.Yandex:
                case PlatformId.VKPlay:
                case PlatformId.VKGames:
                    return BuildTarget.WebGL;
                case PlatformId.RuStore:
                    return BuildTarget.Android;
                default:
                    return EditorUserBuildSettings.activeBuildTarget;
            }
        }

        public static NamedBuildTarget NamedBuildTargetFor(BuildTarget target)
        {
            return NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
        }

        /// <summary>Имя WebGL-шаблона в формате PlayerSettings.WebGL.template.</summary>
        public static string WebGLTemplateFor(PlatformId platform)
        {
            switch (platform)
            {
                case PlatformId.Yandex: return "PROJECT:Yandex";
                case PlatformId.VKPlay: return "PROJECT:VKPlay";
                case PlatformId.VKGames: return "PROJECT:VKGames";
                default: return "APPLICATION:Default";
            }
        }

        /// <summary>
        /// Сжатие WebGL-сборки. Яндекс отдаёт .br с заголовком Content-Encoding: br, это проверено.
        /// Хостинг VK отдаёт .br как binary/octet-stream без заголовка (проверено 11.09.2026),
        /// и загрузчик Unity падает с "Unable to parse". Поэтому для VK и для непроверенных хостингов
        /// gzip с распаковкой в браузере (Decompression Fallback): Unity рекомендует gzip для этого режима,
        /// распаковка Brotli на JavaScript медленная.
        /// </summary>
        public static WebGLCompressionFormat WebGLCompressionFor(PlatformId platform)
        {
            return platform == PlatformId.Yandex ? WebGLCompressionFormat.Brotli : WebGLCompressionFormat.Gzip;
        }

        /// <summary>Распаковывать ли сборку в браузере, если сервер не прислал Content-Encoding.</summary>
        public static bool WebGLDecompressionFallbackFor(PlatformId platform)
        {
            return platform != PlatformId.Yandex;
        }

        /// <summary>Папка с нативными плагинами площадки (.jslib, .aar). Null для Stub.</summary>
        public static string PluginsFolderFor(PlatformId platform)
        {
            switch (platform)
            {
                case PlatformId.Yandex: return PlatformRoot + "/Yandex/Plugins";
                case PlatformId.VKPlay: return PlatformRoot + "/VKPlay/Plugins";
                case PlatformId.RuStore: return PlatformRoot + "/RuStore/Plugins";
                case PlatformId.VKGames: return PlatformRoot + "/VKGames/Plugins";
                default: return null;
            }
        }

        public static readonly PlatformId[] SdkPlatforms =
        {
            PlatformId.Yandex, PlatformId.VKPlay, PlatformId.RuStore, PlatformId.VKGames,
        };

        /// <summary>Определяет площадку по списку defines. Бросает исключение, если задано больше одного BASE_*.</summary>
        public static PlatformId ParseDefines(IEnumerable<string> defines)
        {
            var found = SdkPlatforms.Where(p => defines.Contains(PlatformDefines.For(p))).ToArray();
            if (found.Length > 1)
                throw new InvalidOperationException(
                    "More than one platform define is set: " + string.Join(", ", found.Select(PlatformDefines.For)) +
                    ". Use menu Base/Platform to pick exactly one.");
            return found.Length == 1 ? found[0] : PlatformId.Stub;
        }

        public static List<string> GetDefines(NamedBuildTarget target)
        {
            return PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .Where(d => d.Length > 0)
                .ToList();
        }
    }
}
