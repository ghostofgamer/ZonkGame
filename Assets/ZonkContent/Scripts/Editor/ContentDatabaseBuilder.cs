using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.Editor
{
    /// <summary>
    /// Собирает ContentDatabase из всех ассетов ContentConfig в папке игры. Запускается сам при создании,
    /// удалении или переносе конфигов, и вручную: Zonk/Content/Rebuild Database.
    /// </summary>
    public sealed class ContentDatabaseBuilder : AssetPostprocessor
    {
        public const string GameFolder = "Assets/ZonkContent";
        private static bool _scheduled;

        [MenuItem("Zonk/Content/Rebuild Database", priority = 10)]
        public static void Rebuild()
        {
            var database = FindDatabase();
            if (database == null)
            {
                Debug.LogWarning("[Content] ContentDatabase asset not found. Run Zonk/Setup/Build Everything");
                return;
            }

            var items = AssetDatabase.FindAssets("t:" + nameof(ContentConfig), new[] { GameFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ContentConfig>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item != null)
                .OrderBy(item => item.GetType().Name)
                .ThenBy(item => item.Id)
                .ToList();

            if (database.Items.SequenceEqual(items))
                return;

            database.EditorSetItems(items);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssetIfDirty(database);
            Debug.Log($"[Content] Database rebuilt: {items.Count} items");
        }

        public static ContentDatabase FindDatabase() => FindFirst<ContentDatabase>();

        /// <summary>Первый ассет типа T в папке игры (или null): единственные ассеты вроде GameConfig и UiConfig.</summary>
        public static T FindFirst<T>() where T : Object
        {
            var guid = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { GameFolder }).FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (_scheduled || !Touches(imported) && !Touches(deleted) && !Touches(moved))
                return;

            // Пересборка после импорта: сразу во время импорта ассеты ещё не все загружены.
            _scheduled = true;
            EditorApplication.delayCall += () =>
            {
                _scheduled = false;
                Rebuild();
            };
        }

        private static bool Touches(IEnumerable<string> paths)
        {
            return paths.Any(p => p.StartsWith(GameFolder) && p.EndsWith(".asset"));
        }
    }
}
