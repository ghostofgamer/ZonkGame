using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Защита ручных правок от генератора. Префаб окна или сцена с меткой ZonkLocked не удаляется и не пересоздаётся
    /// ни в Rebuild UI Prefabs, ни в Build Everything / Rebuild Scenes: генератор пишет в консоль, что пропустил её.
    /// Поставить или снять: выделить ассет в Project, меню Zonk/Setup/Lock or Unlock Selected (или ПКМ → Zonk).
    /// Метку видно внизу инспектора ассета (Asset Labels).
    /// </summary>
    public static partial class ZonkSetup
    {
        public const string LockLabel = "ZonkLocked";

        [MenuItem("Zonk/Setup/Lock or Unlock Selected", priority = 10)]
        [MenuItem("Assets/Zonk/Lock or Unlock (protect from generator)", priority = 2000)]
        public static void ToggleLockSelected()
        {
            foreach (var asset in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(asset);
                if (string.IsNullOrEmpty(path))
                    continue;

                var labels = AssetDatabase.GetLabels(asset).ToList();
                var locked = labels.Remove(LockLabel);
                if (!locked)
                    labels.Add(LockLabel);

                AssetDatabase.SetLabels(asset, labels.ToArray());
                Debug.Log($"[Zonk] {path}: {(locked ? "unlocked, the generator may rebuild it" : "locked, the generator will not touch it")}");
            }
        }

        [MenuItem("Zonk/Setup/Lock or Unlock Selected", true)]
        [MenuItem("Assets/Zonk/Lock or Unlock (protect from generator)", true)]
        private static bool CanToggleLock()
        {
            return Selection.objects.Any(o => !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(o)));
        }

        [MenuItem("Zonk/Setup/Show Locked", priority = 11)]
        public static void ShowLocked()
        {
            var paths = AssetDatabase.FindAssets("l:" + LockLabel, new[] { Root }).Select(AssetDatabase.GUIDToAssetPath).ToList();
            Debug.Log(paths.Count == 0
                ? "[Zonk] Nothing is locked"
                : "[Zonk] Locked (the generator skips them):\n" + string.Join("\n", paths));
        }

        /// <summary>Пересоздать только выделенные окна (не заблокированные), сцены не трогаются.</summary>
        [MenuItem("Zonk/Setup/Rebuild Selected UI Prefabs", priority = 3)]
        public static void RebuildSelectedUiPrefabs()
        {
            var names = SelectedWindows();
            if (names.Count == 0)
            {
                EditorUtility.DisplayDialog("Zonk", "Выделите в Project префабы окон из Prefabs/UI.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Zonk", "Пересоздать из генератора: " + string.Join(", ", names) +
                                                     "?\nОкна со снимком вернутся к сохранённому виду, заблокированные пропускаются.",
                    "Пересоздать", "Отмена"))
                return;

            foreach (var name in names)
                ResetWindow(name);

            var content = BuildContent(BuildArt());
            BuildWindows(content.Ui);
            AssetDatabase.SaveAssets();
            UiWindowRegistry.Rebuild();
        }

        public static bool IsLocked(string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            return asset != null && AssetDatabase.GetLabels(asset).Contains(LockLabel);
        }

        /// <summary>Удалить ассет для пересоздания, если он не заблокирован. true — удалён.</summary>
        private static bool DeleteUnlessLocked(string path)
        {
            if (IsLocked(path))
            {
                Debug.Log($"[Zonk] {path} is locked ({LockLabel}): kept as is");
                return false;
            }

            return AssetDatabase.DeleteAsset(path);
        }
    }
}
