using UnityEditor;
using UnityEngine;
using Zenject;
using Zonk.Configs;
using Zonk.Progress;

namespace Zonk.Editor
{
    /// <summary>
    /// Проверка игры в редакторе: меню Zonk/Tools/Cheats. «Открыть всё» — галочка, работает и без игры (вещи магазина,
    /// главы, соперники, режимы открыты; сохранение не меняется). Остальное — во время игры (Play): монеты, энергия,
    /// сундуки, уровень — пишутся в сохранение, как настоящие награды. В сборке игры этого меню нет.
    /// </summary>
    public static class CheatsMenu
    {
        private const string Root = "Zonk/Tools/Cheats/";
        private const string UnlockAllPath = Root + "Unlock Everything";

        [MenuItem(UnlockAllPath, priority = 0)]
        private static void ToggleUnlockAll()
        {
            var value = !EditorPrefs.GetBool(DebugCheats.UnlockAllKey, false);
            EditorPrefs.SetBool(DebugCheats.UnlockAllKey, value);
            Menu.SetChecked(UnlockAllPath, value);
            Debug.Log("[Cheats] Unlock Everything: " + (value ? "ON" : "OFF") + " (reopen the window or menu to refresh)");
        }

        [MenuItem(UnlockAllPath, true)]
        private static bool ValidateUnlockAll()
        {
            Menu.SetChecked(UnlockAllPath, EditorPrefs.GetBool(DebugCheats.UnlockAllKey, false));
            return true;
        }

        [MenuItem(Root + "+10000 Coins", priority = 20)]
        private static void AddCoins()
        {
            if (TryResolve(out IWallet wallet, out var config) && config.Coins != null)
                wallet.Add(config.Coins, 10000);
        }

        [MenuItem(Root + "Refill Energy", priority = 21)]
        private static void RefillEnergy()
        {
            if (TryResolve(out IWallet wallet, out var config) && config.Energy != null)
            {
                var missing = wallet.CapOf(config.Energy) - wallet.Get(config.Energy);
                if (missing > 0)
                    wallet.Add(config.Energy, missing);
            }
        }

        [MenuItem(Root + "+5 Chests", priority = 22)]
        private static void AddChests()
        {
            if (!TryResolve(out IChestService chests, out _))
                return;

            for (var i = 0; i < 5 * chests.WinsPerChest; i++)
                chests.AddWin();
        }

        [MenuItem(Root + "+1 Player Level", priority = 23)]
        private static void LevelUp()
        {
            if (!TryResolve(out IPlayerLevel level, out _))
                return;

            level.GetProgress(level.Xp, out _, out var into, out var toNext);
            level.AddXp(toNext - into);
        }

        [MenuItem(Root + "+10 Player Levels", priority = 24)]
        private static void TenLevels()
        {
            for (var i = 0; i < 10; i++)
                LevelUp();
        }

        [MenuItem(Root + "+10000 Coins", true)]
        [MenuItem(Root + "Refill Energy", true)]
        [MenuItem(Root + "+5 Chests", true)]
        [MenuItem(Root + "+1 Player Level", true)]
        [MenuItem(Root + "+10 Player Levels", true)]
        private static bool ValidatePlaying()
        {
            return Application.isPlaying;
        }

        /// <summary>Сервис игры из ProjectContext (только во время игры).</summary>
        private static bool TryResolve<T>(out T service, out GameConfig config) where T : class
        {
            service = null;
            config = null;
            if (!Application.isPlaying || !ProjectContext.HasInstance)
                return false;

            var container = ProjectContext.Instance.Container;
            service = container.TryResolve<T>();
            config = container.TryResolve<GameConfig>();
            if (service == null || config == null)
            {
                Debug.LogWarning("[Cheats] Game services are not ready yet");
                return false;
            }

            return true;
        }
    }
}
