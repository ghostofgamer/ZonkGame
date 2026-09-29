using System.Collections.Generic;
using System.Threading;
using Base.Platform;
using Cysharp.Threading.Tasks;
using Zonk.Configs;
using Zonk.Presentation;
using Zonk.Progress;
using Zonk.UI;

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

        /// <summary>Показать предмет поверх экипировки (локация главы на время партии).</summary>
        public void Override(CosmeticItemConfig item)
        {
            if (item != null)
                _table.Stage.Apply(item);
        }
    }

    public sealed class MenuState : ITableState
    {
        private readonly UiKit _kit;
        private readonly TableView _table;
        private readonly IWallet _wallet;
        private readonly GameConfig _config;
        private readonly StageDresser _dresser;
        private readonly IPlatformService _platform;
        private readonly LoadingScreenHolder _loading;
        private bool _first = true;

        public MenuState(UiKit kit, TableView table, IWallet wallet, GameConfig config, StageDresser dresser,
            IPlatformService platform, LoadingScreenHolder loading)
        {
            _kit = kit;
            _table = table;
            _wallet = wallet;
            _config = config;
            _dresser = dresser;
            _platform = platform;
            _loading = loading;
        }

        public string Id => TableStateIds.Menu;

        public async UniTask<string> RunAsync(CancellationToken ct)
        {
            _dresser.ApplyEquipped();
            if (_first)
            {
                _first = false;
                _table.Camera.SnapTo(CameraShots.Menu);
            }
            else
            {
                _table.Camera.MoveToAsync(CameraShots.Menu, 0.8f, ct).Forget();
            }

            _platform.NotifyGameplayStop();

            // Первый показ меню: экран загрузки из Bootstrap уходит, когда стол уже одет и камера на месте.
            var loading = _loading.HideAsync(ct);

            using (var screen = new MainMenuScreen(_kit, _table.UiRoot, _wallet, _config))
            {
                await loading;
                var choice = await screen.RunAsync(ct);
                _table.Sound.Play(Sfx.Click);
                switch (choice)
                {
                    case MainMenuChoice.Campaign: return TableStateIds.Campaign;
                    case MainMenuChoice.HotSeat: return TableStateIds.HotSeat;
                    case MainMenuChoice.Shop: return TableStateIds.Shop;
                    case MainMenuChoice.Rules: return TableStateIds.Rules;
                    default: return TableStateIds.Settings;
                }
            }
        }
    }

    public sealed class SettingsState : ITableState
    {
        private readonly UiKit _kit;
        private readonly TableView _table;
        private readonly IGameSettings _settings;

        public SettingsState(UiKit kit, TableView table, IGameSettings settings)
        {
            _kit = kit;
            _table = table;
            _settings = settings;
        }

        public string Id => TableStateIds.Settings;

        public async UniTask<string> RunAsync(CancellationToken ct)
        {
            using (var screen = new SettingsScreen(_kit, _table.UiRoot, _settings))
                await screen.RunAsync(ct);
            return TableStateIds.Menu;
        }
    }

    public sealed class ShopState : ITableState
    {
        private readonly UiKit _kit;
        private readonly TableView _table;
        private readonly ContentDatabase _content;
        private readonly IInventory _inventory;
        private readonly ILoadout _loadout;
        private readonly ShopService _shop;
        private readonly IWallet _wallet;
        private readonly GameConfig _config;
        private readonly MatchPresenter _presenter;

        public ShopState(UiKit kit, TableView table, ContentDatabase content, IInventory inventory, ILoadout loadout,
            ShopService shop, IWallet wallet, GameConfig config, MatchPresenter presenter)
        {
            _kit = kit;
            _table = table;
            _content = content;
            _inventory = inventory;
            _loadout = loadout;
            _shop = shop;
            _wallet = wallet;
            _config = config;
            _presenter = presenter;
        }

        public string Id => TableStateIds.Shop;

        public async UniTask<string> RunAsync(CancellationToken ct)
        {
            using (var screen = new ShopScreen(_kit, _table.UiRoot, _content, _inventory, _loadout, _shop, _wallet, _config, _table, _presenter))
                await screen.RunAsync(ct);
            return TableStateIds.Menu;
        }
    }

    /// <summary>Открытые игроку кости и скины: для экранов выбора.</summary>
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
            var result = _content.All<DieConfig>().FindAll(d => _inventory.IsOwned(d) && (allowSpecial || !d.IsSpecial));
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

    /// <summary>Книга правил поверх стола. Закрытие по кнопке окна, затем обратно в меню.</summary>
    public sealed class RulesState : ITableState
    {
        private readonly IUiService _ui;

        public RulesState(IUiService ui)
        {
            _ui = ui;
        }

        public string Id => TableStateIds.Rules;

        public async UniTask<string> RunAsync(CancellationToken ct)
        {
            var window = await _ui.OpenAsync<Zonk.UI.Rules.RulesWindow>(ct);
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
}
