using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Снимки окон: свой вид окна (иконки, расположение, картинки) сохраняется, и пересоздание окон возвращает к нему,
    /// а не к вёрстке генератора. Снимок — копия файла префаба в папке UiSnapshots в корне проекта (вне Assets:
    /// Unity его не импортирует, в сборку он не попадает, в git хранится). При восстановлении файл префаба заменяется
    /// снимком, .meta остаётся, поэтому ссылки на окно не ломаются.
    ///
    /// Порядок для окна при Rebuild UI Prefabs: заблокированное (ZonkLocked) не трогается; есть снимок —
    /// восстанавливается из снимка; иначе строится генератором.
    /// </summary>
    public static partial class ZonkSetup
    {
        public const string SnapshotsFolderName = "UiSnapshots";

        private static string SnapshotsFolder =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, SnapshotsFolderName);

        private static string SnapshotPath(string windowName) => Path.Combine(SnapshotsFolder, windowName + ".prefab");

        private static string WindowAssetPath(string windowName) => Prefabs + "/UI/" + windowName + ".prefab";

        public static bool HasSnapshot(string windowName) => File.Exists(SnapshotPath(windowName));

        [MenuItem("Zonk/Setup/Save UI Snapshot", priority = 5)]
        public static void SaveSnapshotsMenu()
        {
            var names = SelectedWindows();
            if (names.Count == 0)
            {
                if (!EditorUtility.DisplayDialog("Zonk", "Окна не выделены. Сохранить текущий вид всех окон?", "Все окна", "Отмена"))
                    return;
                names = WindowPrefabs.Where(n => File.Exists(WindowAssetPath(n))).ToList();
            }

            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(SnapshotsFolder);
            foreach (var name in names)
            {
                File.Copy(WindowAssetPath(name), SnapshotPath(name), true);
                Debug.Log($"[Zonk] UI snapshot saved: {name}. Rebuild UI Prefabs will restore this look");
            }
        }

        [MenuItem("Zonk/Setup/Restore UI from Snapshots", priority = 6)]
        public static void RestoreSnapshotsMenu()
        {
            var names = SelectedWindows();
            if (names.Count == 0)
                names = WindowPrefabs.Where(HasSnapshot).ToList();

            if (names.Count == 0)
            {
                EditorUtility.DisplayDialog("Zonk", "Снимков окон нет. Сначала Zonk/Setup/Save UI Snapshot.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Zonk", "Вернуть окна к сохранённому виду: " + string.Join(", ", names) + "?",
                    "Вернуть", "Отмена"))
                return;

            foreach (var name in names)
                RestoreSnapshot(name);
            UiWindowRegistry.Rebuild();
        }

        [MenuItem("Zonk/Setup/Forget UI Snapshot of Selected", priority = 7)]
        public static void ForgetSnapshotsMenu()
        {
            var names = SelectedWindows().Where(HasSnapshot).ToList();
            if (names.Count == 0)
            {
                EditorUtility.DisplayDialog("Zonk", "Выделите в Prefabs/UI окна, у которых есть снимок.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Zonk", "Удалить снимки: " + string.Join(", ", names) +
                                                     "?\nПосле этого Rebuild UI Prefabs построит их генератором.", "Удалить", "Отмена"))
                return;

            foreach (var name in names)
                File.Delete(SnapshotPath(name));
        }

        /// <summary>Заменить префаб окна снимком. Заблокированное окно не трогается. true — восстановлено.</summary>
        private static bool RestoreSnapshot(string windowName)
        {
            var assetPath = WindowAssetPath(windowName);
            if (!HasSnapshot(windowName))
                return false;

            if (IsLocked(assetPath))
            {
                Debug.Log($"[Zonk] {assetPath} is locked ({LockLabel}): kept as is");
                return true;
            }

            File.Copy(SnapshotPath(windowName), assetPath, true);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[Zonk] {windowName} restored from UI snapshot");
            return true;
        }

        /// <summary>Подготовить окно к пересборке: снимок — восстановить, иначе удалить, чтобы построил генератор.</summary>
        private static void ResetWindow(string windowName)
        {
            if (!RestoreSnapshot(windowName))
                DeleteUnlessLocked(WindowAssetPath(windowName));
        }

        private static List<string> SelectedWindows()
        {
            return Selection.objects.Select(AssetDatabase.GetAssetPath)
                .Where(p => p.StartsWith(Prefabs + "/UI/") && p.EndsWith(".prefab"))
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => WindowPrefabs.Contains(n)).ToList();
        }
    }
}
