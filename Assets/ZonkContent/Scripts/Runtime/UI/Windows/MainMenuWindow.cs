using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zenject;
using Zonk.Configs;
using Zonk.Progress;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    public enum MainMenuChoice
    {
        Campaign,
        HotSeat,
        Shop,
        Settings,
        Rules,
        Quests,

        /// <summary>Особое предложение (стартовый набор): магазин сразу на нём.</summary>
        Offer,
        Leaderboards,
        Tower,
        EndlessRun,
    }

    /// <summary>
    /// Главное меню поверх стола: стол в текущей теме и есть витрина. Кошелёк — WalletView в префабе.
    /// Под кошельком: награды за рекламу (кнопка на каждую строку GameConfig.MenuAdOffers: монеты, энергия) и особое
    /// предложение (набор с Featured), пока оно не куплено.
    /// </summary>
    public sealed class MainMenuWindow : UiWindow
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private UiButtonView _campaign;
        [SerializeField] private UiButtonView _hotSeat;
        [SerializeField] private UiButtonView _shop;
        [SerializeField] private UiButtonView _settings;
        [SerializeField] private UiButtonView _rules;
        [SerializeField] private UiButtonView _quests;

        [Tooltip("Значок на кнопке заданий: есть награда, которую можно забрать")]
        [SerializeField] private GameObject _questsBadge;

        [SerializeField] private RectTransform _adOffers;
        [SerializeField] private UiButtonView _adOfferTemplate;
        [SerializeField] private UiButtonView _offer;
        [SerializeField] private UiButtonView _leaderboards;

        [Tooltip("Башня (пусто — кнопки нет: старый префаб)")]
        [SerializeField] private UiButtonView _tower;

        [Tooltip("«Бесконечный забег» (пусто — кнопки нет: старый префаб)")]
        [SerializeField] private UiButtonView _endlessRun;

        private readonly Choice<MainMenuChoice> _choice = new Choice<MainMenuChoice>();
        private IQuestService _questService;
        private MenuAdRewards _adRewards;
        private readonly System.Collections.Generic.List<(MenuAdOffer offer, UiButtonView button)> _adButtons =
            new System.Collections.Generic.List<(MenuAdOffer, UiButtonView)>();
        private ContentDatabase _content;
        private IInventory _inventory;
        private UiKit _kit;
        private ShopService _shopService;
        private PlayerStats _stats;
        private bool _busy;

        /// <summary>Набор, который предлагает кнопка особого предложения.</summary>
        public ThemeSetConfig Offer { get; private set; }

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, UiButtonView campaign, UiButtonView hotSeat, UiButtonView shop,
            UiButtonView settings, UiButtonView rules, UiButtonView quests, GameObject questsBadge, RectTransform adOffers,
            UiButtonView adOfferTemplate, UiButtonView offer, UiButtonView leaderboards, UiButtonView tower = null,
            UiButtonView endlessRun = null)
        {
            _tower = tower;
            _endlessRun = endlessRun;
            _leaderboards = leaderboards;
            _quests = quests;
            _questsBadge = questsBadge;
            _title = title;
            _campaign = campaign;
            _hotSeat = hotSeat;
            _shop = shop;
            _settings = settings;
            _rules = rules;
            _adOffers = adOffers;
            _adOfferTemplate = adOfferTemplate;
            _offer = offer;
        }
#endif

        [Inject]
        public void Construct(IQuestService quests, MenuAdRewards adRewards, ContentDatabase content, IInventory inventory, UiKit kit,
            ShopService shop, PlayerStats stats)
        {
            _stats = stats;
            _shopService = shop;
            _questService = quests;
            _adRewards = adRewards;
            _content = content;
            _inventory = inventory;
            _kit = kit;
        }

        private void Awake()
        {
            _campaign.OnClick(() => _choice.Set(MainMenuChoice.Campaign));
            _hotSeat.OnClick(() => _choice.Set(MainMenuChoice.HotSeat));
            _shop.OnClick(() => _choice.Set(MainMenuChoice.Shop));
            _settings.OnClick(() => _choice.Set(MainMenuChoice.Settings));
            _rules.OnClick(() => _choice.Set(MainMenuChoice.Rules));
            if (_quests != null)
                _quests.OnClick(() => _choice.Set(MainMenuChoice.Quests));
            if (_offer != null)
                _offer.OnClick(() => _choice.Set(MainMenuChoice.Offer));
            if (_leaderboards != null)
                _leaderboards.OnClick(() => _choice.Set(MainMenuChoice.Leaderboards));
            if (_tower != null)
                _tower.OnClick(() => _choice.Set(MainMenuChoice.Tower));
            if (_endlessRun != null)
                _endlessRun.OnClick(() => _choice.Set(MainMenuChoice.EndlessRun));
        }

        protected override void OnShowing()
        {
            _title.text = T("game.title");
            _campaign.SetText(T("menu.campaign"));
            _hotSeat.SetText(T("menu.hotseat"));
            _shop.SetText(T("menu.shop"));
            _settings.SetText(T("menu.settings"));
            if (_quests != null)
                _quests.SetText(T("menu.quests"));
            if (_tower != null)
                _tower.SetText(T("mode.tower"));
            if (_endlessRun != null)
                _endlessRun.SetText(T("mode.endlessRun"));
            if (_questsBadge != null)
                _questsBadge.SetActive(_questService != null && _questService.HasClaimable);

            // Рекорды — только где у площадки есть таблицы (Яндекс; на VK и RuStore их нет).
            if (_leaderboards != null)
            {
                _leaderboards.SetVisible(_stats != null && _stats.IsLeaderboardAvailable);
                _leaderboards.SetText(T("menu.leaderboards"));
            }

            Offer = FindOffer();
            if (_offer != null)
            {
                _offer.SetVisible(Offer != null);
                if (Offer != null)
                    _offer.SetText(T("menu.offer", T(Offer.NameKey)));
            }

            BuildAdOffers();
            RefreshAdCoinsLoopAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>Готовность рекламы меняется со временем (на VK — предзагрузка): кнопка появляется, когда реклама готова.</summary>
        private async UniTaskVoid RefreshAdCoinsLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: ct).SuppressCancellationThrow())
                    return;
                if (!_busy)
                    RefreshAdCoins();
            }
        }

        public UniTask<MainMenuChoice> WaitChoiceAsync(CancellationToken ct)
        {
            return _choice.WaitAsync(ct);
        }

        private ThemeSetConfig FindOffer()
        {
            // Особое предложение продаётся за деньги: без покупок на площадке (VK) кнопки нет.
            if (_content == null || _shopService == null || !_shopService.IsPurchaseAvailable)
                return null;

            var themes = _content.All<ThemeSetConfig>().FindAll(t => t.Featured && !_inventory.IsOwned(t));
            themes.Sort((a, b) => a.Order.CompareTo(b.Order));
            return themes.Count > 0 ? themes[0] : null;
        }

        private void BuildAdOffers()
        {
            if (_adOffers == null || _adOfferTemplate == null || _adRewards == null)
                return;

            _adOfferTemplate.gameObject.SetActive(false);
            foreach (var pair in _adButtons)
                Destroy(pair.button.gameObject);
            _adButtons.Clear();
            _adShownState.Clear();

            foreach (var offer in _adRewards.Offers)
            {
                if (offer == null)
                    continue;

                var captured = offer;
                var button = _adOfferTemplate.Spawn(_adOffers);
                button.OnClick(() => ClaimAdOfferAsync(captured).Forget());
                _adButtons.Add((offer, button));
            }

            RefreshAdCoins();
        }

        // Что сейчас написано на кнопках рекламы: текст пересобирается, только если поменялось состояние.
        private readonly System.Collections.Generic.List<int> _adShownState = new System.Collections.Generic.List<int>();

        private void RefreshAdCoins()
        {
            for (var index = 0; index < _adButtons.Count; index++)
            {
                var (offer, button) = _adButtons[index];
                // Кнопка всегда на месте; если нажать нельзя — на ней написано почему.
                button.SetVisible(_adRewards.IsShown(offer));
                var left = _adRewards.Left(offer);
                var kind = left <= 0 ? 0 : !_adRewards.IsAdReady ? 1 : offer.PerDay <= 0 ? 2 : 3;
                var key = kind == 0 ? "menu.adOfferTomorrow"
                    : kind == 1 ? "menu.adOfferLoading"
                    : kind == 2 ? "menu.adOfferUnlimited"
                    : "menu.adOffer";

                // Состояние кнопки одним числом: вид текста и остаток просмотров.
                var state = kind * 100000 + Mathf.Min(left, 99999);
                while (_adShownState.Count <= index)
                    _adShownState.Add(int.MinValue);
                if (_adShownState[index] != state)
                {
                    _adShownState[index] = state;
                    var currency = offer.Currency != null ? T(offer.Currency.NameKey) : string.Empty;
                    button.SetText(T(key, offer.Amount, currency, left));
                }
                button.Interactable = _adRewards.CanOffer(offer) && !_busy;
            }
        }

        private async UniTaskVoid ClaimAdOfferAsync(MenuAdOffer offer)
        {
            if (_busy)
                return;

            _busy = true;
            try
            {
                var lifetime = this.GetCancellationTokenOnDestroy();
                var coins = await _adRewards.ClaimAsync(offer, lifetime);
                if (this == null)
                    return;

                if (coins > 0)
                    Toast.ShowAsync(_kit, transform, "+" + coins + " " + T(offer.Currency.NameKey), UiColors.Gold, 0.8f, lifetime).Forget();
                RefreshAdCoins();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _busy = false;
            }
        }
    }
}
