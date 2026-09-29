using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zenject;
using Zonk.Configs;
using Zonk.Presentation;
using Zonk.Progress;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>
    /// Магазин прямо на столе. Вкладка = слот косметики: камера летит к его ракурсу (CameraShotId),
    /// выбранный предмет сразу примеряется в сцене, стиль броска показывается рукой со стаканом.
    /// При выходе всё возвращается к надетому. Вкладки, карточки и кнопки цены — копии шаблонов из префаба.
    /// </summary>
    public sealed class ShopWindow : UiWindow
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private RectTransform _tabs;
        [SerializeField] private UiButtonView _tabTemplate;
        [SerializeField] private UiButtonView _back;
        [SerializeField] private RectTransform _items;
        [SerializeField] private UiButtonView _cardTemplate;
        [SerializeField] private TMP_Text _itemName;
        [SerializeField] private RectTransform _details;
        [SerializeField] private UiButtonView _detailButtonTemplate;
        [SerializeField] private TMP_Text _detailTextTemplate;

        private readonly Choice<bool> _close = new Choice<bool>();
        private ContentDatabase _content;
        private IInventory _inventory;
        private ILoadout _loadout;
        private ShopService _shop;
        private TableView _table;
        private MatchPresenter _presenter;
        private UiKit _kit;
        private List<CosmeticSlotConfig> _slots;
        private List<ThemeSetConfig> _themes;
        private List<DieConfig> _shopDice;
        private List<CoinPackConfig> _coinPacks;
        private bool _coinsTab;
        private ContentConfig _current;
        private bool _diceTab;
        private CosmeticSlotConfig _slot;
        private CancellationToken _ct;
        private CancellationTokenSource _previewCts;
        private UniTask _previewTask = UniTask.CompletedTask;
        private IDieMastery _mastery;
        private GameConfig _config;

        [Inject]
        public void Construct(ContentDatabase content, IInventory inventory, ILoadout loadout, ShopService shop, TableView table,
            MatchPresenter presenter, UiKit kit, IDieMastery mastery, GameConfig config)
        {
            _mastery = mastery;
            _config = config;
            _content = content;
            _inventory = inventory;
            _loadout = loadout;
            _shop = shop;
            _table = table;
            _presenter = presenter;
            _kit = kit;
        }

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, RectTransform tabs, UiButtonView tabTemplate, UiButtonView back, RectTransform items,
            UiButtonView cardTemplate, TMP_Text itemName, RectTransform details, UiButtonView detailButtonTemplate,
            TMP_Text detailTextTemplate)
        {
            _title = title;
            _tabs = tabs;
            _tabTemplate = tabTemplate;
            _back = back;
            _items = items;
            _cardTemplate = cardTemplate;
            _itemName = itemName;
            _details = details;
            _detailButtonTemplate = detailButtonTemplate;
            _detailTextTemplate = detailTextTemplate;
        }
#endif

        private void Awake()
        {
            _tabTemplate.gameObject.SetActive(false);
            _cardTemplate.gameObject.SetActive(false);
            _detailButtonTemplate.gameObject.SetActive(false);
            _detailTextTemplate.gameObject.SetActive(false);
            _back.OnClick(() => _close.Set(true));
        }

        private void OnDestroy()
        {
            _previewCts?.Cancel();
            _previewCts?.Dispose();
        }

        protected override void OnShowing()
        {
            _back.SetText(T("ui.back"));
            _slots = _content.All<CosmeticSlotConfig>().FindAll(s => s.ShowInShop);
            _slots.Sort((a, b) => a.Order.CompareTo(b.Order));
            _themes = _content.All<ThemeSetConfig>();
            _themes.Sort((a, b) => a.Order.CompareTo(b.Order));
            _coinPacks = _content.All<CoinPackConfig>();
            _coinPacks.Sort((a, b) => a.Order.CompareTo(b.Order));

            foreach (var slot in _slots)
            {
                var captured = slot;
                var tab = _tabTemplate.Spawn(_tabs);
                tab.SetText(T(slot.NameKey));
                tab.OnClick(() => SelectSlot(captured));
            }

            // Вкладка особых костей: за монеты, рекламу, покупку или победу над соперником.
            _shopDice = _content.All<DieConfig>().FindAll(d => d.IsSpecial);
            _shopDice.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id));
            if (_shopDice.Count > 0)
            {
                var diceTab = _tabTemplate.Spawn(_tabs);
                diceTab.SetText(T("shop.dice"));
                diceTab.OnClick(SelectDice);
            }

            if (_themes.Count > 0)
            {
                var themes = _tabTemplate.Spawn(_tabs);
                themes.SetText(T("shop.themes"));
                themes.OnClick(() => SelectSlot(null));
            }

            // Пакеты монет за деньги: только там, где на площадке есть покупки.
            if (_coinPacks.Count > 0 && _shop.IsPurchaseAvailable)
            {
                var coinsTab = _tabTemplate.Spawn(_tabs);
                coinsTab.SetText(T("shop.coins"));
                coinsTab.OnClick(SelectCoins);
            }

            _back.transform.SetAsLastSibling();
        }

        /// <param name="focus">Что показать сразу: набор (особое предложение из меню), кость или предмет.</param>
        public async UniTask RunAsync(CancellationToken ct, ContentConfig focus = null)
        {
            _ct = ct;
            switch (focus)
            {
                case ThemeSetConfig theme:
                    SelectSlot(null);
                    SelectItem(theme);
                    break;
                case DieConfig _:
                    SelectDice();
                    SelectItem(focus);
                    break;
                case CosmeticItemConfig item when item.Slot != null:
                    SelectSlot(item.Slot);
                    SelectItem(item);
                    break;
                default:
                    if (_slots.Count > 0)
                        SelectSlot(_slots[0]);
                    break;
            }

            // Цены площадки («99 RUB») приходят позже: кнопки покупки перерисовываются.
            LoadPricesAsync().Forget();

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
            _diceTab = false;
            _coinsTab = false;
            _title.text = slot != null ? T(slot.NameKey) : T("shop.themes");

            var shot = slot != null ? slot.CameraShotId : CameraShots.Menu;
            _table.Camera.MoveToAsync(shot, 0.6f, _ct).Forget();
            ShowDiceShowcase(slot != null && slot.Applier is DiceSkinApplier);

            Clear(_items, _cardTemplate.transform);

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
                var card = _cardTemplate.Spawn(_items);
                card.SetText(T(entry.NameKey) + "\n" + T(equipped ? equippedKey : owned ? "shop.owned" : "shop.forSale"));
                card.SetColor(equipped ? UiColors.ButtonAccent : owned ? UiColors.Button : UiColors.ButtonMuted);
                card.OnClick(() => SelectItem(captured));
            }

            SelectItem(slot != null ? (ContentConfig)_loadout.GetEquipped(slot) : entries.Count > 0 ? entries[0] : null);
        }

        /// <summary>Вкладка особых костей: камера на лоток, в нём шесть костей в виде выбранной.</summary>
        private void SelectDice()
        {
            RestoreEquipped();
            _slot = null;
            _diceTab = true;
            _coinsTab = false;
            _title.text = T("shop.dice");
            _table.Camera.MoveToAsync("shop_dice", 0.6f, _ct).Forget();

            Clear(_items, _cardTemplate.transform);
            foreach (var die in _shopDice)
            {
                var captured = die;
                var owned = _inventory.IsOwned(die);
                var card = _cardTemplate.Spawn(_items);
                card.SetText(T(die.NameKey) + "\n" + T(owned ? "shop.owned" : "shop.forSale"));
                card.SetColor(owned ? UiColors.Button : UiColors.ButtonMuted);
                card.OnClick(() => SelectItem(captured));
            }

            SelectItem(_shopDice.Count > 0 ? _shopDice[0] : null);
        }

        /// <summary>Перерисовать текущую вкладку (после покупки или смены отметки).</summary>
        private void Reselect()
        {
            if (_coinsTab)
                SelectCoins();
            else if (_diceTab)
                SelectDice();
            else
                SelectSlot(_slot);
        }

        /// <summary>Вкладка пакетов монет за деньги: камера на меню, карточки пакетов.</summary>
        private void SelectCoins()
        {
            RestoreEquipped();
            _slot = null;
            _diceTab = false;
            _coinsTab = true;
            _title.text = T("shop.coins");
            _table.Camera.MoveToAsync(CameraShots.Menu, 0.6f, _ct).Forget();

            Clear(_items, _cardTemplate.transform);
            foreach (var pack in _coinPacks)
            {
                var captured = pack;
                var card = _cardTemplate.Spawn(_items);
                card.SetText(pack.Amount + "\n" + (pack.BonusPercent > 0 ? T("shop.bonus", pack.BonusPercent) : T(pack.NameKey)));
                card.SetColor(pack.BonusPercent > 0 ? UiColors.ButtonAccent : UiColors.Button);
                card.OnClick(() => SelectItem(captured));
            }

            SelectItem(_coinPacks.Count > 0 ? _coinPacks[0] : null);
        }

        private async UniTaskVoid LoadPricesAsync()
        {
            try
            {
                await _shop.LoadPricesAsync(this.GetCancellationTokenOnDestroy());
            }
            catch (System.OperationCanceledException)
            {
                return;
            }

            if (this != null && _current != null)
                SelectItem(_current);
        }

        private void SelectItem(ContentConfig item)
        {
            Clear(_details, _detailButtonTemplate.transform, _detailTextTemplate.transform);
            _itemName.text = item != null ? T(item.NameKey) : string.Empty;
            if (item == null)
                return;
            _current = item;

            if (item is CoinPackConfig pack)
            {
                DetailText(T("shop.coinPack", pack.Amount, pack.Currency != null ? T(pack.Currency.NameKey) : string.Empty), UiColors.Text);
                DetailButton(RealPriceText(pack.ProductId), UiColors.ButtonAccent, () => BuyCoinPackAsync(pack).Forget());
                return;
            }

            if (item is ThemeSetConfig set)
            {
                if (!string.IsNullOrEmpty(set.DescriptionKey))
                    DetailText(T(set.DescriptionKey), UiColors.Text);
                var contents = SetContents(set);
                if (contents.Length > 0)
                    DetailText(T("shop.setContents", contents), UiColors.TextMuted);
            }

            Preview(item);

            if (item is DieConfig described && !string.IsNullOrEmpty(described.DescriptionKey))
                DetailText(T(described.DescriptionKey), UiColors.Text);

            if (_inventory.IsOwned(item))
            {
                if (item is CosmeticItemConfig multi && multi.Slot != null && multi.Slot.MultiSelect)
                {
                    // Мультивыбор: отметить или снять. Последний отмеченный снять нельзя.
                    var selected = _loadout.IsEquipped(multi);
                    var toggle = DetailButton(T(selected ? "shop.deselect" : "shop.select"),
                        selected ? UiColors.ButtonMuted : UiColors.ButtonAccent, () =>
                        {
                            _loadout.Toggle(multi);
                            _table.Sound.Play(Sfx.Click);
                            SelectSlot(_slot);
                            SelectItem(multi);
                        });
                    toggle.Interactable = !(selected && _loadout.GetEquippedSet(multi.Slot).Count <= 1);
                    DetailText(T("shop.multiHint"), UiColors.TextMuted);
                }
                else if (item is CosmeticItemConfig cosmetic)
                {
                    var equipped = _loadout.GetEquipped(cosmetic.Slot) == cosmetic;
                    var button = DetailButton(T(equipped ? "shop.equipped" : "shop.equip"), UiColors.ButtonAccent, () =>
                    {
                        _loadout.Equip(cosmetic);
                        _table.Sound.Play(Sfx.Click);
                        SelectSlot(_slot);
                    });
                    button.Interactable = !equipped;
                }
                else
                {
                    DetailText(T(item is DieConfig ? "shop.dieOwned" : "shop.owned"), UiColors.Good);
                    if (item is DieConfig mastered)
                        DetailText(MasteryText(mastered), UiColors.TextMuted);
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
                    DetailText(T(string.IsNullOrEmpty(progress.HintKey) ? "shop.fromProgress" : progress.HintKey), UiColors.TextMuted);
                    continue;
                }

                if (!_shop.IsOptionAvailable(option))
                    continue;

                var captured = option;
                DetailButton(OptionText(item, option), UiColors.Button, () => BuyAsync(item, captured).Forget());
            }
        }

        /// <summary>Мастерство кости: уровень и очки до следующего уровня.</summary>
        private string MasteryText(DieConfig die)
        {
            var level = _mastery.GetLevel(die);
            var points = _mastery.GetPoints(die);
            var current = _mastery.GetLevelInfo(level);
            var next = _mastery.GetLevelInfo(level + 1);
            var name = current != null ? T(current.NameKey) : T("mastery.none");
            return next != null
                ? T("mastery.info", name, points, next.Points)
                : T("mastery.max", name, points);
        }

        private UiButtonView DetailButton(string text, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var button = _detailButtonTemplate.Spawn(_details);
            button.SetText(text);
            button.SetColor(color);
            button.OnClick(onClick);
            return button;
        }

        private void DetailText(string text, Color color)
        {
            var label = Instantiate(_detailTextTemplate, _details);
            label.gameObject.SetActive(true);
            label.text = text;
            label.color = color;
        }

        private string OptionText(ContentConfig item, PriceOption option)
        {
            switch (option)
            {
                case CurrencyPriceOption currency:
                    return T("shop.buyFor", currency.Amount, currency.Currency != null ? T(currency.Currency.NameKey) : string.Empty);
                case RewardedAdPriceOption ads:
                    return T("shop.watchAds", _inventory.GetAdProgress(item), ads.AdsRequired);
                case PurchasePriceOption purchase:
                    return RealPriceText(purchase.ProductId);
                default:
                    return option.GetType().Name;
            }
        }

        /// <summary>«Купить за 99 RUB», пока цена из каталога площадки не пришла — «Купить».</summary>
        private string RealPriceText(string productId)
        {
            var price = _shop.PriceText(productId);
            return string.IsNullOrEmpty(price) ? T("shop.buyReal") : T("shop.buyForReal", price);
        }

        /// <summary>Что внутри набора: предметы, кости, разовые награды.</summary>
        private string SetContents(ThemeSetConfig set)
        {
            var parts = new List<string>();
            foreach (var item in set.Items)
            {
                if (item != null)
                    parts.Add(T(item.NameKey));
            }

            foreach (var die in set.Dice)
            {
                if (die != null)
                    parts.Add(T(die.NameKey));
            }

            foreach (var reward in set.Rewards)
            {
                if (reward is CurrencyReward currency && currency.Currency != null)
                    parts.Add(currency.Amount + " " + T(currency.Currency.NameKey));
            }

            return string.Join(", ", parts);
        }

        private async UniTaskVoid BuyCoinPackAsync(CoinPackConfig pack)
        {
            var result = await _shop.BuyCoinPackAsync(pack, _ct);
            if (this == null)
                return;

            var lifetime = this.GetCancellationTokenOnDestroy();
            if (result == AcquireResult.Acquired)
            {
                _table.Sound.Play(Sfx.Coin);
                Toast.ShowAsync(_kit, (RectTransform)transform, T("shop.coinsAdded", pack.Amount), UiColors.Good, 0.9f, lifetime).Forget();
            }
            else if (result == AcquireResult.Unavailable)
            {
                Toast.ShowAsync(_kit, (RectTransform)transform, T("shop.unavailable"), UiColors.Bad, 0.8f, lifetime).Forget();
            }
        }

        private async UniTaskVoid BuyAsync(ContentConfig item, PriceOption option)
        {
            var result = await _shop.AcquireAsync(item, option, _ct);
            if (this == null)
                return;

            var root = (RectTransform)transform;
            var lifetime = this.GetCancellationTokenOnDestroy();
            switch (result)
            {
                case AcquireResult.Acquired:
                    _table.Sound.Play(Sfx.Coin);
                    if (item is CosmeticItemConfig cosmetic)
                        _loadout.Equip(cosmetic);
                    Toast.ShowAsync(_kit, root, T("shop.acquired"), UiColors.Good, 0.8f, lifetime).Forget();
                    break;
                case AcquireResult.NotEnoughCurrency:
                    Toast.ShowAsync(_kit, root, T("shop.notEnough"), UiColors.Bad, 0.8f, lifetime).Forget();
                    break;
                case AcquireResult.Unavailable:
                    Toast.ShowAsync(_kit, root, T("shop.unavailable"), UiColors.Bad, 0.8f, lifetime).Forget();
                    break;
            }

            Reselect();
            if (result != AcquireResult.Acquired)
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
                case DieConfig die:
                    ShowDiceShowcase(true, die);
                    break;
                case ThemeSetConfig theme:
                    foreach (var themeItem in theme.Items)
                        _table.Stage.Apply(themeItem);
                    break;
            }
        }

        /// <summary>
        /// Рука берёт стакан и трясёт его выбранным стилем. Новый выбор прерывает прошлый показ, но начинается только
        /// после того, как прошлый вернул стакан на место: два показа одновременно уводили стакан вместе с рукой под стол.
        /// </summary>
        private void PreviewRollStyle(RollStyleConfig style)
        {
            _previewCts?.Cancel();
            _previewCts?.Dispose();
            _previewCts = CancellationTokenSource.CreateLinkedTokenSource(_ct, this.GetCancellationTokenOnDestroy());
            _previewTask = RunPreviewAsync(_previewTask, style, _previewCts.Token).Preserve();
            _previewTask.Forget();
        }

        private async UniTask RunPreviewAsync(UniTask previous, RollStyleConfig style, CancellationToken ct)
        {
            await previous.SuppressCancellationThrow();
            if (ct.IsCancellationRequested)
                return;

            await _presenter.PreviewShakeAsync(style, ct).SuppressCancellationThrow();
        }

        private void RestoreEquipped()
        {
            foreach (var slot in _content.All<CosmeticSlotConfig>())
                _table.Stage.Apply(slot, _loadout.GetEquipped(slot));
            ShowDiceShowcase(false);
        }

        /// <summary>На вкладке скинов кости лежат рядом в лотке, гранями 1..6.</summary>
        private void ShowDiceShowcase(bool show, DieConfig look = null)
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
                if (look != null)
                {
                    var level = _inventory.IsOwned(look) ? _mastery.GetLevel(look) : 0;
                    var info = _mastery.GetLevelInfo(level);
                    die.SetLook(look.LookMesh, DiceSetView.LookFor(look, level));
                    die.SetMarker(look.MarkerColor);
                    die.SetGlow(info != null ? info.Glow : 1f);
                }

                die.SetVisible(true);
            }
        }

        private static void Clear(Transform container, params Transform[] keep)
        {
            foreach (Transform child in container)
            {
                if (System.Array.IndexOf(keep, child) < 0)
                    Destroy(child.gameObject);
            }
        }
    }
}
