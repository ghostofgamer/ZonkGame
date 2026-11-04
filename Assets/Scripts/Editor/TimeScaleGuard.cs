using UnityEditor;
using UnityEngine;

namespace Base.Editor
{
    /// <summary>
    /// Выход из Play на паузе (Time.timeScale = 0) оставляет ноль в редакторе, и следующее сохранение пишет его в
    /// ProjectSettings/TimeManager.asset. Тогда окна работают, а всё на игровом времени стоит: игра «зависает».
    /// Страж возвращает 1 при загрузке редактора, после выхода из Play и перед входом в Play.
    /// Сборку с другим значением останавливает PlatformBuildPreprocessor.
    /// </summary>
    [InitializeOnLoad]
    public static class TimeScaleGuard
    {
        static TimeScaleGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                Restore();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.ExitingEditMode)
                Restore();
        }

        private static void Restore()
        {
            if (Mathf.Approximately(Time.timeScale, 1f))
                return;

            Debug.LogWarning("[Base] Time Scale was " + Time.timeScale + ", restored to 1");
            Time.timeScale = 1f;
        }
    }
}
