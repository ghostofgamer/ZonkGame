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

        /// <summary>Разрыв в очках при мгновенном конце партии: поражение покажет «Не хватило N очков».</summary>
        public const int ForcedMargin = 350;

        /// <summary>Мгновенная победа (true) или поражение текущей партии. Слушает MatchRunner, пока партия идёт.</summary>
        public static event System.Action<bool> MatchResultRequested;

        public static void RequestMatchResult(bool localWins)
        {
            MatchResultRequested?.Invoke(localWins);
        }
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
