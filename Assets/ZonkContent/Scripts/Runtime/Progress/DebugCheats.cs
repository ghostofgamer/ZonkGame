namespace Zonk.Progress
{
    /// <summary>
    /// Проверка игры в редакторе (меню Zonk/Tools/Cheats): «Открыть всё» — все вещи магазина, главы, соперники и режимы
    /// считаются открытыми, сохранение игрока не меняется (выключил — всё как было). В сборке игры всегда выключено.
    /// </summary>
    public static class DebugCheats
    {
#if UNITY_EDITOR
        public const string UnlockAllKey = "Zonk.Cheats.UnlockAll";
#endif

        public static bool UnlockAll
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool(UnlockAllKey, false);
#else
                return false;
#endif
            }
        }
    }
}
