using UnityEditor;
using Zonk.Table;

namespace Zonk.Editor
{
    /// <summary>
    /// Проверка обучения в редакторе: с галочкой Zonk/Debug/Replay Tutorial подсказки показываются каждый раз,
    /// даже уже увиденные (сохранение игрока не меняется). В сборке игры не действует.
    /// </summary>
    public static class TutorialDebugMenu
    {
        private const string MenuPath = "Zonk/Debug/Replay Tutorial";

        [MenuItem(MenuPath, priority = 40)]
        private static void Toggle()
        {
            var value = !EditorPrefs.GetBool(TutorialDirector.ReplayEditorKey, false);
            EditorPrefs.SetBool(TutorialDirector.ReplayEditorKey, value);
            Menu.SetChecked(MenuPath, value);
        }

        [MenuItem(MenuPath, true)]
        private static bool Validate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(TutorialDirector.ReplayEditorKey, false));
            return true;
        }
    }
}
