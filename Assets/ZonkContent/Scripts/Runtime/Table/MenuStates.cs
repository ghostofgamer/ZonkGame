using System.Collections.Generic;
using System.Threading;
using Base.Platform;
using Cysharp.Threading.Tasks;
using Zonk.Configs;
using Zonk.Presentation;
using Zonk.Progress;
using Zonk.UI;
using Zonk.UI.Windows;

namespace Zonk.Table
{
    /// <summary>Надевает экипировку игрока на стол: каждый слот косметики.</summary>
    public sealed class StageDresser
    {
        private readonly ContentDatabase _content;
        private readonly ILoadout _loadout;
        private readonly TableView _table;

        public StageDresser(ContentDatabase content, ILoadout loadout, TableView table)
        {
            _content = content;
            _loadout = loadout;
            _table = table;
        }

        public void ApplyEquipped()
        {
            foreach (var slot in _content.All<CosmeticSlotConfig>())
                _table.Stage.Apply(slot, _loadout.GetEquipped(slot));
        }

        /// <summary>Надеть экипировку и дождаться, пока модели загрузятся (префабы грузятся по требованию).</summary>
        public UniTask ApplyEquippedAsync(CancellationToken ct)
        {
            ApplyEquipped();
            return _table.Stage.WhenReadyAsync(ct);
        }

        /// <summary>Показать предмет поверх экипировки (локация главы на время партии).</summary>
        public void Override(CosmeticItemConfig item)
        {
            if (item != null)
                _table.Stage.Apply(item);
        }

        /// <summary>Показать предмет поверх экипировки и дождаться его модели.</summary>
        public UniTask OverrideAsync(CosmeticItemConfig item, CancellationToken ct)
        {
            Override(item);
            return _table.Stage.WhenReadyAsync(ct);
        }
    }

    /// <summary>Что показать в магазине сразу при открытии (особое предложение из меню). Одноразово: магазин забирает.</summary>
    public sealed class ShopFocus
    {
        public ContentConfig Item;
    }

    public sealed class MenuState : ITableState
    {
        private readonly IUiService _ui;
        private readonly TableView _table;
        private readonly StageDresser _dresser;
        private readonly IPlatformService _platform;
        private readonly LoadingScreenHolder _loading;
        private readonly ShopFocus _focus;
        private readonly ShopService _shop;
        private readonly TutorialDirector _tutorial;
        private bool _first = true;

        public MenuState(IUiService ui, TableView table, StageDresser dresser, IPlatformService platform, LoadingScreenHolder loading,
            ShopFocus focus, ShopService shop, TutorialDirector tutorial)
        {
            _tutorial = tutorial;
            _focus = focus;
            _shop = shop;
            _ui = ui;
            _table = table;
            _dresser = dresser;
            _platform = platform;
            _loading = loading;
        }

        public string Id => TableStateIds.Menu;

        public async UniTask<string> RunAsync(CancellationToken ct)
        {
            if (_first)
            {
                // Первый показ: экран загрузки ещё на месте — дождаться моделей стола, чтобы он ушёл уже с одетым столом.
                await _dresser.ApplyEquippedAsync(ct);
                _first = false;
                _table.Camera.SnapTo(CameraShots.Menu);
            }
            else
            {
                _dresser.ApplyEquipped();
                _table.Camera.MoveToAsync(CameraShots.Menu, 0.8f, ct).Forget();
            }

            _platform.NotifyGameplayStop();

            // Оплаченное, но не выданное (пакеты монет, награды наборов) — до показа кошелька.
            _shop.SyncPurchases();

            // Первый показ меню: экран загрузки из Bootstrap уходит, когда стол уже одет и камера на месте.
            var loading = _loading.HideAsync(ct);
            var menu = await _ui.OpenAsync<MainMenuWindow>(ct);
            await loading;
            _tutorial.Show(TutorialTrigger.MainMenu);

            MainMenuChoice choice;
            try
            {
                choice = await menu.WaitChoiceAsync(ct);
                _focus.Item = choice == MainMenuChoice.Offer ? menu.Offer : null;
                _table.Sound.Play(Sfx.Click);
            }
            finally
            {
                _ui.CloseAsync(menu, CancellationToken.None).Forget();
                _tutorial.Hide();
            }

            switch (choice)
            {
                case MainMenuChoice.Campaign: return TableStateIds.Campaign;
                case MainMenuChoice.HotSeat: return TableStateIds.HotSeat;
                case MainMenuChoice.Shop: return TableStateIds.Shop;
                case MainMenuChoice.Rules: return TableStateIds.Rules;
                case MainMenuChoice.Quests: return TableStateIds.Quests;
                case MainMenuChoice.Offer: return TableStateIds.Shop;
                case MainMenuChoice.Leaderboards: return TableStateIds.Leaderboards;
                case MainMenuChoice.Tower: return TableStateIds.Tower;
                case MainMenuChoice.EndlessRun: return TableStateIds.EndlessRun;
                case MainMenuChoice.Profile: return TableStateIds.Profile;
                case MainMenuChoice.Chest: return TableStateIds.Chest;
                case MainMenuChoice.Season: return TableStateIds.Season;
                default: return TableStateIds.Settings;
            }
        }
    }

    public sealed class ShopState : ITableState
    {
        private readonly IUiService _ui;
        private readonly ShopFocus _focus;

        public ShopState(IUiService ui, ShopFocus focus)
        {
            _ui = ui;
            _focus = focus;
        }

        public string Id => TableStateIds.Shop;

        public async UniTask<string> RunAsync(CancellationToken ct)
        {
            var focus = _focus.Item;
            _focus.Item = null;
            var window = await _ui.OpenAsync<ShopWindow>(ct);
            try
            {
                await window.RunAsync(ct, focus);
            }
            finally
            {
                await _ui.CloseAsync(window, CancellationToken.None);
            }

            return TableStateIds.Menu;
        }
    }

    public sealed class OwnedContent
    {
        private readonly ContentDatabase _content;
        private readonly IInventory _inventory;
        private readonly GameConfig _config;

        public OwnedContent(ContentDatabase content, IInventory inventory, GameConfig config)
        {
            _content = content;
            _inventory = inventory;
            _config = config;
        }

        /// <summary>Обычная кость первой, дальше особые по имени ассета.</summary>
        public List<DieConfig> Dice(bool allowSpecial = true)
        {
            var result = _content.All<DieConfig>().FindAll(d => !d.RunOnly && _inventory.IsOwned(d) && (allowSpecial || !d.IsSpecial));
            result.Sort((a, b) =>
            {
                if (a == _config.StandardDie) return -1;
                if (b == _config.StandardDie) return 1;
                return string.CompareOrdinal(a.name, b.name);
            });
            return result;
        }

        public List<CosmeticItemConfig> Items(string slotId)
        {
            var result = _content.All<CosmeticItemConfig>().FindAll(i => i.Slot != null && i.Slot.Id == slotId && _inventory.IsOwned(i));
            result.Sort((a, b) => a.Order.CompareTo(b.Order));
            return result;
        }
    }

    /// <summary>
    /// Окно поверх стола, которое закрывается кнопкой окна (WaitCloseRequestAsync), затем обратно в меню.
    /// Новое такое окно — наследник с ID состояния и строка в TableInstaller.
    /// </summary>
    public abstract class ModalWindowState<TWindow> : ITableState where TWindow : UiWindow
    {
        private readonly IUiService _ui;

        protected ModalWindowState(IUiService ui)
        {
            _ui = ui;
        }

        public abstract string Id { get; }

        public async UniTask<string> RunAsync(CancellationToken ct)
        {
            var window = await _ui.OpenAsync<TWindow>(ct);
            try
            {
                await window.WaitCloseRequestAsync(ct);
            }
            finally
            {
                await _ui.CloseAsync(window, CancellationToken.None);
            }

            return TableStateIds.Menu;
        }
    }

    public sealed class SettingsState : ModalWindowState<SettingsWindow>
    {
        public SettingsState(IUiService ui) : base(ui)
        {
        }

        public override string Id => TableStateIds.Settings;
    }

    /// <summary>Книга правил поверх стола.</summary>
    public sealed class RulesState : ModalWindowState<Zonk.UI.Rules.RulesWindow>
    {
        public RulesState(IUiService ui) : base(ui)
        {
        }

        public override string Id => TableStateIds.Rules;
    }

    /// <summary>Задания дня и недели поверх стола.</summary>
    public sealed class QuestsState : ModalWindowState<QuestsWindow>
    {
        public QuestsState(IUiService ui) : base(ui)
        {
        }

        public override string Id => TableStateIds.Quests;
    }

    /// <summary>Рекорды (таблицы лидеров) поверх стола.</summary>
    public sealed class LeaderboardsState : ModalWindowState<LeaderboardWindow>
    {
        public LeaderboardsState(IUiService ui) : base(ui)
        {
        }

        public override string Id => TableStateIds.Leaderboards;
    }

    /// <summary>Открытие сундуков поверх стола.</summary>
    public sealed class ChestState : ModalWindowState<ChestWindow>
    {
        public ChestState(IUiService ui) : base(ui)
        {
        }

        public override string Id => TableStateIds.Chest;
    }

    /// <summary>Сезонный путь поверх стола.</summary>
    public sealed class SeasonState : ModalWindowState<SeasonWindow>
    {
        public SeasonState(IUiService ui) : base(ui)
        {
        }

        public override string Id => TableStateIds.Season;
    }

    /// <summary>Профиль игрока (статистика) поверх стола.</summary>
    public sealed class ProfileState : ModalWindowState<ProfileWindow>
    {
        public ProfileState(IUiService ui) : base(ui)
        {
        }

        public override string Id => TableStateIds.Profile;
    }
}
