using System;
using System.Collections.Generic;
using System.Threading;
using Base.Services.Monetization;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zenject;
using Zonk.Configs;
using Zonk.Progress;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>
    /// Сезонный путь: название и сколько осталось, ступень и полоса очков, лента ступеней (деталь Parts/SeasonStep):
    /// сверху бесплатные награды, снизу платные. Пройденную забирают нажатием; платную — по пропуску, на площадке без
    /// покупок — за рекламу или даром позже (ISeasonPass.PremiumAccessFor). Кнопка покупки пропуска — где есть покупки.
    /// Лента строится при открытии (редко: память выделять можно).
    /// </summary>
    public sealed class SeasonWindow : UiWindow
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _timer;
        [SerializeField] private TMP_Text _stepText;
        [SerializeField] private RectTransform _fill;
        [SerializeField] private TMP_Text _freeLabel;
        [SerializeField] private TMP_Text _premiumLabel;
        [SerializeField] private RectTransform _track;
        [SerializeField] private SeasonStepView _stepTemplate;
        [SerializeField] private TMP_Text _passInfo;
        [SerializeField] private UiButtonView _buy;
        [SerializeField] private UiButtonView _back;

        private ISeasonPass _season;
        private IPurchaseFlow _purchases;
        private UiKit _kit;
        private UiConfig _ui;
        private UiPalette _palette;
        private readonly List<SeasonStepView> _steps = new List<SeasonStepView>();
        private string _price;
        private bool _busy;

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, TMP_Text timer, TMP_Text stepText, RectTransform fill, TMP_Text freeLabel,
            TMP_Text premiumLabel, RectTransform track, SeasonStepView stepTemplate, TMP_Text passInfo, UiButtonView buy,
            UiButtonView back)
        {
            _title = title;
            _timer = timer;
            _stepText = stepText;
            _fill = fill;
            _freeLabel = freeLabel;
            _premiumLabel = premiumLabel;
            _track = track;
            _stepTemplate = stepTemplate;
            _passInfo = passInfo;
            _buy = buy;
            _back = back;
        }
#endif

        [Inject]
        public void Construct(ISeasonPass season, IPurchaseFlow purchases, UiKit kit, GameConfig config)
        {
            _season = season;
            _purchases = purchases;
            _kit = kit;
            _ui = config != null ? config.Ui : null;
            _palette = _ui != null ? _ui.Palette : new UiPalette();
        }

        private void Awake()
        {
            if (_stepTemplate.gameObject.scene.IsValid())
                _stepTemplate.gameObject.SetActive(false);
            _back.OnClick(RequestClose);
            _buy.OnClick(() => BuyAsync(this.GetCancellationTokenOnDestroy()).Forget());
        }

        protected override void OnShowing()
        {
            _back.SetText(T("ui.back"));
            if (_freeLabel != null)
                _freeLabel.text = T("season.free");
            if (_premiumLabel != null)
                _premiumLabel.text = T("season.premium");
            BuildSteps();
            Refresh();
            LoadPriceAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void BuildSteps()
        {
            foreach (var step in _steps)
                Destroy(step.gameObject);
            _steps.Clear();

            var season = _season.Current;
            if (season == null)
                return;

            for (var i = 0; i < season.Steps.Count; i++)
            {
                var view = Instantiate(_stepTemplate, _track);
                view.gameObject.SetActive(true);
                var index = i;
                view.FreeClicked += () => ClaimFree(index);
                view.PremiumClicked += () => ClaimPremiumAsync(index, this.GetCancellationTokenOnDestroy()).Forget();
                _steps.Add(view);
            }
        }

        private void Refresh()
        {
            var season = _season.Current;
            if (season == null)
            {
                _title.text = T("season.none");
                _timer.text = string.Empty;
                _stepText.text = string.Empty;
                SetFill(0f);
                _passInfo.text = string.Empty;
                _buy.SetVisible(false);
                return;
            }

            _title.text = T(season.NameKey);
            var left = _season.Left;
            _timer.text = T("season.left", Math.Max(0, left.Days), Math.Max(0, left.Hours));
            var step = _season.Step;
            _stepText.text = T("season.step", step, season.Steps.Count);
            _season.StepProgress(out var into, out var perStep);
            SetFill(step >= season.Steps.Count ? 1f : (float)into / perStep);

            for (var i = 0; i < _steps.Count && i < season.Steps.Count; i++)
                PaintStep(_steps[i], season, i, step);

            if (_season.HasPass)
                _passInfo.text = T("season.passOwned");
            else if (_purchases.IsAvailable && !string.IsNullOrEmpty(season.PassProductId))
                _passInfo.text = T("season.passPitch");
            else
                _passInfo.text = T("season.passFallback", season.PremiumFreeAfterSteps);

            _buy.SetVisible(_season.CanBuyPass);
            _buy.SetText(string.IsNullOrEmpty(_price) ? T("season.buy") : T("season.buyPrice", _price));
            _buy.Interactable = !_busy;
        }

        private void PaintStep(SeasonStepView view, SeasonConfig season, int index, int reached)
        {
            var data = season.Steps[index];
            view.SetNumber((index + 1).ToString(), index < reached ? _palette.Gold : _palette.TextMuted);

            var freeState = !HasRewards(data.Free) ? SeasonCellState.Empty
                : _season.IsClaimedFree(index) ? SeasonCellState.Claimed
                : index < reached ? SeasonCellState.Claimable
                : SeasonCellState.Locked;
            view.SetFree(IconOf(data.Free), TextOf(data.Free, freeState), freeState, ColorOf(freeState));

            SeasonCellState premiumState;
            if (!HasRewards(data.Premium))
                premiumState = SeasonCellState.Empty;
            else if (_season.IsClaimedPremium(index))
                premiumState = SeasonCellState.Claimed;
            else if (index >= reached)
                premiumState = SeasonCellState.Locked;
            else
            {
                switch (_season.PremiumAccessFor(index))
                {
                    case PremiumAccess.Pass:
                    case PremiumAccess.Free:
                        premiumState = SeasonCellState.Claimable;
                        break;
                    case PremiumAccess.Ad:
                        premiumState = SeasonCellState.ForAd;
                        break;
                    case PremiumAccess.NeedsPass:
                        premiumState = SeasonCellState.NeedsPass;
                        break;
                    default:
                        premiumState = SeasonCellState.Locked;
                        break;
                }
            }

            view.SetPremium(IconOf(data.Premium), TextOf(data.Premium, premiumState), premiumState, ColorOf(premiumState));
        }

        private void ClaimFree(int index)
        {
            if (_busy)
                return;

            var granted = _season.ClaimFree(index);
            if (granted != null)
                ShowGranted(granted);
            Refresh();
        }

        private async UniTaskVoid ClaimPremiumAsync(int index, CancellationToken ct)
        {
            if (_busy)
                return;

            if (_season.PremiumAccessFor(index) == PremiumAccess.NeedsPass)
            {
                await BuyAsync(ct);
                return;
            }

            _busy = true;
            try
            {
                var granted = await _season.ClaimPremiumAsync(index, ct);
                if (granted != null)
                    ShowGranted(granted);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _busy = false;
                if (this != null)
                    Refresh();
            }
        }

        private async UniTask BuyAsync(CancellationToken ct)
        {
            if (_busy || !_season.CanBuyPass)
                return;

            _busy = true;
            Refresh();
            try
            {
                if (await _season.BuyPassAsync(ct) && this != null)
                    Toast.ShowAsync(_kit, transform, T("season.passBought"), _palette.Gold, 1.2f, ct).Forget();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _busy = false;
                if (this != null)
                    Refresh();
            }
        }

        private async UniTaskVoid LoadPriceAsync(CancellationToken ct)
        {
            var season = _season.Current;
            if (season == null || string.IsNullOrEmpty(season.PassProductId) || !_purchases.IsAvailable)
                return;

            try
            {
                foreach (var product in await _purchases.GetProductsAsync(ct))
                {
                    if (product.Id == season.PassProductId)
                        _price = product.PriceFormatted;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (this != null)
                Refresh();
        }

        private void ShowGranted(List<GrantedReward> granted)
        {
            var text = RewardNames.Granted(granted, T);
            if (!string.IsNullOrEmpty(text))
                Toast.ShowAsync(_kit, transform, text, _palette.Gold, 1f, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private Sprite IconOf(List<Reward> rewards)
        {
            if (!HasRewards(rewards))
                return null;

            foreach (var reward in rewards)
            {
                if (reward is ContentReward content && content.Item is CosmeticItemConfig cosmetic && cosmetic.Icon != null)
                    return cosmetic.Icon;
            }

            return _ui != null ? _ui.CoinSprite : null;
        }

        private string TextOf(List<Reward> rewards, SeasonCellState state)
        {
            switch (state)
            {
                case SeasonCellState.Empty:
                    return string.Empty;
                case SeasonCellState.Claimed:
                    return T("season.claimed");
                case SeasonCellState.ForAd:
                    return T("season.forAd");
                case SeasonCellState.NeedsPass:
                    return T("season.needsPass");
                default:
                    return RewardNames.List(rewards, T);
            }
        }

        private Color ColorOf(SeasonCellState state)
        {
            switch (state)
            {
                case SeasonCellState.Claimable: return _palette.Good;
                case SeasonCellState.ForAd: return _palette.Bank;
                case SeasonCellState.NeedsPass: return _palette.RarityLegendary * 0.6f;
                case SeasonCellState.Claimed: return _palette.PanelLight;
                case SeasonCellState.Empty: return Color.clear;
                default: return _palette.ButtonMuted;
            }
        }

        private void SetFill(float fraction)
        {
            if (_fill == null)
                return;
            var max = _fill.anchorMax;
            max.x = Mathf.Clamp01(fraction);
            _fill.anchorMax = max;
        }

        private static bool HasRewards(List<Reward> rewards)
        {
            return rewards != null && rewards.Count > 0;
        }
    }
}
