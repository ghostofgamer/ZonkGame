using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zonk.UI;

namespace Zonk.Editor
{
    /// <summary>
    /// Список окон UiConfig.Windows собирается сам: все префабы с компонентом UiWindow в папке Prefabs/UI.
    /// Новое окно = новый префаб в этой папке, руками в список его добавлять не нужно.
    /// Экран загрузки в список не входит: он живёт в сцене Bootstrap, а не открывается через IUiService.
    /// </summary>
    public sealed class UiWindowRegistry : AssetPostprocessor
    {
        public const string WindowsFolder = ContentDatabaseBuilder.GameFolder + "/Prefabs/UI";
        private static bool _scheduled;

        [MenuItem("Zonk/Content/Rebuild UI Windows", priority = 12)]
        public static void Rebuild()
        {
            var config = ContentDatabaseBuilder.FindFirst<UiConfig>();
            if (config == null)
                return;

            var windows = new List<UiWindow>();
            foreach (var prefabGuid in AssetDatabase.FindAssets("t:Prefab", new[] { WindowsFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(prefabGuid));
                var window = prefab != null ? prefab.GetComponent<UiWindow>() : null;
                if (window != null && !(window is LoadingScreen))
                    windows.Add(window);
            }

            windows.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (config.Windows.SequenceEqual(windows))
                return;

            config.Windows = windows;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
            Debug.Log($"[UI] Windows registered: {string.Join(", ", windows.Select(w => w.name))}");
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (_scheduled || !imported.Concat(deleted).Concat(moved).Any(p => p.StartsWith(WindowsFolder) && p.EndsWith(".prefab")))
                return;

            _scheduled = true;
            EditorApplication.delayCall += () =>
            {
                _scheduled = false;
                Rebuild();
            };
        }
    }
}
