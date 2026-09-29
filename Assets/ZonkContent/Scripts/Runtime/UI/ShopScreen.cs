using TMPro;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zonk.Configs;
using Zonk.Presentation;
using Zonk.Progress;

namespace Zonk.UI
{
    /// <summary>
    /// Магазин прямо на столе. Вкладка = слот косметики: камера летит к его ракурсу (CameraShotId),
    /// выбранный предмет сразу примеряется в сцене. При выходе всё возвращается к надетому.
    /// Вкладки и предметы строятся из конфигов: новый слот или предмет появляется здесь без кода.
    /// </summary>
    public sealed class ShopScreen : UiScreen
    {
        private readonly Choice<bool> _close = new Choice<bool>();
        private readonly ContentDatabase _content;
        private readonly IInventory _inventory;
        private readonly ILoadout _loadout;
        private readonly ShopService _shop;
        private readonly TableView _table;
        private readonly List<CosmeticSlotConfig> _slots;
        private readonly List<ThemeSetConfig> _themes;
        private readonly RectTransform _items;
        private readonly RectTransform _details;
        private readonly TMP_Text _title;

        private CosmeticSlotConfig _slot;
        private CancellationToken _ct;
        private readonly MatchPresenter _presenter;
        private CancellationTokenSource _previewCts;

        public ShopScreen(UiKit kit, RectTransform parent, ContentDatabase content, IInventory inventory, ILoadout loadout,
            ShopService shop, IWallet wallet, GameConfig config, TableView table, MatchPresenter presenter) : base(kit, parent)
        {
            _content = content;
            _inventory = inventory;
            _loadout = loadout;
            _shop = shop;
            _table = table;
            _presenter = presenter;

            _slots = content.All<CosmeticSlotConfig>().FindAll(s => s.ShowInShop);
            _slots.Sort((a, b) => a.Order.CompareTo(b.Order));
            _themes = content.All<ThemeSetConfig>();

            var bottom = kit.Panel("Shop", Root, UiColors.Panel);
            var bottomRect = bottom.rectTransform;
            bottomRect.anchorMin = new Vector2(0f, 0f);
            bottomRect.anchorMax = new Vector2(1f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.sizeDelta = new Vector2(0, 330);
            var column = kit.Column(bottom.transform, 10, 16);
            UiKit.Stretch((RectTransform)column.transform);

            var tabs = kit.Row(column.transform, 8);
            UiKit.Size(tabs, -1, 64);
            foreach (var slot in _slots)
            {
                var captured = slot;
                kit.Button(tabs.transform, kit.T(slot.NameKey), () => SelectSlot(captured), UiColors.ButtonMuted, 24);
            }

            if (_themes.Count > 0)
                kit.Button(tabs.transform, kit.T("shop.themes"), () => SelectSlot(null), UiColors.ButtonMuted, 24);

            kit.Button(tabs.transform, kit.T("ui.back"), () => _close.Set(true), UiColors.Button, 24);

            var body = kit.Row(column.transform, 16);
            UiKit.Size(body, -1, 220);
            // Предметы — горизонтальная прокрутка: сколько бы их ни было, ряд не вылезает за края панели.
            var itemsList = kit.HorizontalList(body.transform, 10, out _items);
            UiKit.Size(itemsList, -1, -1, 3);

            var details = kit.Column(body.transform, 8, 8);
            _details = (RectTransform)details.transform;
            UiKit.Size(details, 520, -1, 0);

            _title = kit.Label(Root, string.Empty, 40, TextAnchor.MiddleCenter, UiColors.Gold);
            UiKit.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(900, 80), new Vector2(0, -24));

            WalletBar.Create(kit, Root, wallet, config);
        }

        public async UniTask RunAsync(CancellationToken ct)
        {
            _ct = ct;
            if (_slots.Count > 0)
                SelectSlot(_slots[0]);

            try
            {
                await _close.WaitAsync(ct);
            }
            finally
            {
                RestoreEquipped();
            }
        }

        private void SelectSlot(CosmeticSlotConfig slot)
        {
            RestoreEquipped();
            _slot = slot;
            _title.text = slot != null ? Kit.T(slot.NameKey) : Kit.T("shop.themes");

            var shot = slot != null ? slot.CameraShotId : CameraShots.Menu;
            _table.Camera.MoveToAsync(shot, 0.6f, _ct).Forget();
            ShowDiceShowcase(slot != null && slot.Applier is DiceSkinApplier);

            foreach (Transform child in _items)
                Object.Destroy(child.gameObject);

            var entries = new List<ContentConfig>();
            if (slot != null)
            {
                var items = _content.All<CosmeticItemConfig>().FindAll(i => i.Slot == slot && (!i.HiddenInShop || _inventory.IsOwned(i)));
                items.Sort((a, b) => a.Order.CompareTo(b.Order));
                entries.AddRange(items);
            }
            else
            {
                entries.AddRange(_themes);
            }

            foreach (var entry in entries)
            {
                var captured = entry;
                var owned = _inventory.IsOwned(entry);
                var equipped = entry is CosmeticItemConfig cosmetic && _loadout.IsEquipped(cosmetic);
                var equippedKey = slot != null && slot.MultiSelect ? "shop.selected" : "shop.equipped";
                var label = Kit.T(entry.NameKey) + "\n" + Kit.T(equipped ? equippedKey : owned ? "shop.owned" : "shop.forSale");
                var button = Kit.Button(_items, label, () => SelectItem(captured),
                    equipped ? UiColors.ButtonAccent : owned ? UiColors.Button : UiColors.ButtonMuted, 22);
                UiKit.Size(button.Background, 200, -1);
            }

            SelectItem(slot != null ? (ContentConfig)_loadout.GetEquipped(slot) : entries.Count > 0 ? entries[0] : null);
        }

        private void SelectItem(ContentConfig item)
        {
            foreach (Transform child in _details)
                Object.Destroy(child.gameObject);

            if (item == null)
                return;

            Preview(item);
            UiKit.Size(Kit.Label(_details, Kit.T(item.NameKey), 30, TextAnchor.MiddleLeft, UiColors.Gold), -1, 44);

            if (_inventory.IsOwned(item))
            {
                if (item is CosmeticItemConfig multi && multi.Slot != null && multi.Slot.MultiSelect)
                {
                    // Мультивыбор: отметить или снять. Последний отмеченный снять нельзя.
                    var selected = _loadout.IsEquipped(multi);
                    var last = selected && _loadout.GetEquippedSet(multi.Slot).Count <= 1;
                    var toggle = Kit.Button(_details, Kit.T(selected ? "shop.deselect" : "shop.select"), () =>
                    {
                        _loadout.Toggle(multi);
                        _table.Sound.Play(Sfx.Click);
                        SelectSlot(_slot);
                        SelectItem(multi);
                    }, selected ? UiColors.ButtonMuted : UiColors.ButtonAccent);
                    toggle.Interactable = !last;
                    Kit.Label(_details, Kit.T("shop.multiHint"), 20, TextAnchor.UpperLeft, UiColors.TextMuted);
                }
                else if (item is CosmeticItemConfig cosmetic)
                {
                    var equipped = _loadout.GetEquipped(cosmetic.Slot) == cosmetic;
                    var button = Kit.Button(_details, Kit.T(equipped ? "shop.equipped" : "shop.equip"), () =>
                    {
                        _loadout.Equip(cosmetic);
                        _table.Sound.Play(Sfx.Click);
                        SelectSlot(_slot);
                    }, UiColors.ButtonAccent);
                    button.Interactable = !equipped;
                }
                else
                {
                    Kit.Label(_details, Kit.T("shop.owned"), 26, TextAnchor.MiddleLeft, UiColors.Good);
                }

                return;
            }

            var price = Pricing.PriceOf(item);
            if (price == null)
                return;

            foreach (var option in price.Options)
            {
                if (option is ProgressPriceOption progress)
                {
                    Kit.Label(_details, Kit.T(string.IsNullOrEmpty(progress.HintKey) ? "shop.fromProgress" : progress.HintKey), 22,
                        TextAnchor.MiddleLeft, UiColors.TextMuted);
                    continue;
                }

                if (!_shop.IsOptionAvailable(option))
                    continue;

                var captured = option;
                Kit.Button(_details, OptionText(item, option), () => BuyAsync(item, captured).Forget(), UiColors.Button, 24);
            }
        }

        private string OptionText(ContentConfig item, PriceOption option)
        {
            switch (option)
            {
                case CurrencyPriceOption currency:
                    return Kit.T("shop.buyFor", currency.Amount, currency.Currency != null ? Kit.T(currency.Currency.NameKey) : string.Empty);
                case RewardedAdPriceOption ads:
                    return Kit.T("shop.watchAds", _inventory.GetAdProgress(item), ads.AdsRequired);
                case PurchasePriceOption _:
                    return Kit.T("shop.buyReal");
                default:
                    return option.GetType().Name;
            }
        }

        private async UniTaskVoid BuyAsync(ContentConfig item, PriceOption option)
        {
            var result = await _shop.AcquireAsync(item, option, _ct);
            switch (result)
            {
                case AcquireResult.Acquired:
                    _table.Sound.Play(Sfx.Coin);
                    if (item is CosmeticItemConfig cosmetic)
                        _loadout.Equip(cosmetic);
                    Toast.ShowAsync(Kit, Root, Kit.T("shop.acquired"), UiColors.Good, 0.8f, LifetimeToken).Forget();
                    break;
                case AcquireResult.NotEnoughCurrency:
                    Toast.ShowAsync(Kit, Root, Kit.T("shop.notEnough"), UiColors.Bad, 0.8f, LifetimeToken).Forget();
                    break;
                case AcquireResult.Unavailable:
                    Toast.ShowAsync(Kit, Root, Kit.T("shop.unavailable"), UiColors.Bad, 0.8f, LifetimeToken).Forget();
                    break;
            }

            if (Root != null)
                SelectSlot(_slot);
            if (Root != null && result != AcquireResult.Acquired)
                SelectItem(item);
        }

        private void Preview(ContentConfig item)
        {
            switch (item)
            {
                case CosmeticItemConfig style when style.Payload is RollStylePayload rollStyle:
                    PreviewRollStyle(rollStyle.Style);
                    break;
                case CosmeticItemConfig cosmetic:
                    _table.Stage.Apply(cosmetic);
                    break;
                case ThemeSetConfig theme:
                    foreach (var themeItem in theme.Items)
                        _table.Stage.Apply(themeItem);
                    break;
            }
        }

        /// <summary>Рука берёт стакан и трясёт его выбранным стилем. Новый выбор прерывает прошлый показ.</summary>
        private void PreviewRollStyle(RollStyleConfig style)
        {
            _previewCts?.Cancel();
            _previewCts?.Dispose();
            _previewCts = CancellationTokenSource.CreateLinkedTokenSource(_ct, LifetimeToken);
            _presenter.PreviewShakeAsync(style, _previewCts.Token).SuppressCancellationThrow().Forget();
        }

        private void RestoreEquipped()
        {
            foreach (var slot in _content.All<CosmeticSlotConfig>())
                _table.Stage.Apply(slot, _loadout.GetEquipped(slot));
            ShowDiceShowcase(false);
        }

        /// <summary>На вкладке скинов кости лежат рядом в лотке, гранями 1..6.</summary>
        private void ShowDiceShowcase(bool show)
        {
            var dice = _table.Dice;
            if (!show)
            {
                dice.SetVisible(false);
                return;
            }

            dice.SetLoadout(null);
            var yaw = _table.Tray.transform.eulerAngles.y;
            for (var i = 0; i < dice.Dice.Count; i++)
            {
                var die = dice[i];
                die.SetFace(i + 1, Quaternion.identity);
                die.transform.SetPositionAndRotation(_table.Tray.RowPosition(i, dice.Dice.Count), die.RootRotationForFaceUp(yaw));
                die.SetVisible(true);
            }
        }
    }
}
