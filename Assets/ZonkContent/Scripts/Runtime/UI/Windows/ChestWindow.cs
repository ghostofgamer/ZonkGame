using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Zonk.Configs;
using Zonk.Progress;
using Zonk.UI.Views;
using Zonk.Utils;

namespace Zonk.UI.Windows
{
    /// <summary>
    /// Открытие сундуков: сундук трясётся, вспыхивает цветом редкости, показывается награда (вещь или монеты).
    /// «Открыть» — пока есть сундуки, сколько их и сколько побед до следующего — подписью. Закрывается «Назад».
    /// Тайминги анимации — поля префаба.
    /// </summary>
    public sealed class ChestWindow : UiWindow
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private Image _chest;
        [SerializeField] private Image _glow;
        [SerializeField] private Image _prize;
        [SerializeField] private TMP_Text _result;
        [SerializeField] private TMP_Text _count;
        [SerializeField] private UiButtonView _open;
        [SerializeField] private UiButtonView _back;

        [Header("Анимация")]
        [SerializeField] private float _shakeDuration = 0.8f;
        [SerializeField] private float _shakeAngle = 12f;
        [SerializeField] private float _revealDuration = 0.35f;

        private IChestService _chests;
        private UiConfig _ui;
        private ChestConfig _config;
        private bool _busy;

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, Image chest, Image glow, Image prize, TMP_Text result, TMP_Text count,
            UiButtonView open, UiButtonView back)
        {
            _title = title;
            _chest = chest;
            _glow = glow;
            _prize = prize;
            _result = result;
            _count = count;
            _open = open;
            _back = back;
        }
#endif

        [Inject]
        public void Construct(IChestService chests, GameConfig config)
        {
            _chests = chests;
            _ui = config != null ? config.Ui : null;
            _config = config != null ? config.Chest : null;
        }

        private void Awake()
        {
            _open.OnClick(() => OpenAsync(this.GetCancellationTokenOnDestroy()).Forget());
            _back.OnClick(RequestClose);
        }

        protected override void OnShowing()
        {
            _title.text = T("chest.title");
            _back.SetText(T("ui.back"));
            if (_config != null && _config.Icon != null)
                _chest.sprite = _config.Icon;
            SetPrize(null, Color.clear);
            _result.text = string.Empty;
            Refresh();
        }

        private void Refresh()
        {
            var ready = _chests.Ready;
            _open.SetText(T("chest.open"));
            _open.SetVisible(ready > 0);
            _open.Interactable = !_busy;
            _count.text = ready > 0
                ? T("chest.ready", ready)
                : T("chest.next", _chests.WinsPerChest - _chests.Wins);
        }

        private async UniTaskVoid OpenAsync(CancellationToken ct)
        {
            if (_busy || _chests.Ready <= 0)
                return;

            _busy = true;
            Refresh();
            try
            {
                SetPrize(null, Color.clear);
                _result.text = string.Empty;

                // Тряска: сундук качается всё сильнее, потом замирает.
                var rect = _chest.rectTransform;
                await DOTween.To(() => 0f, t => rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * 40f) * _shakeAngle * t), 1f,
                    _shakeDuration).SetEase(Ease.Linear).AwaitAsync(ct);
                rect.localEulerAngles = Vector3.zero;

                var opening = _chests.Open();
                if (opening == null)
                    return;

                ShowPrize(opening.Value);
                var scale = _prize.rectTransform;
                await DOTween.To(() => 0f, t => scale.localScale = Vector3.one * t, 1f, _revealDuration).SetEase(Ease.OutBack).AwaitAsync(ct);
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

        private void ShowPrize(ChestOpening opening)
        {
            var palette = _ui != null ? _ui.Palette : new UiPalette();
            var reward = opening.Reward;
            if (reward.Item != null)
            {
                var color = palette.RarityColor(opening.Rarity);
                var sprite = reward.Item is CosmeticItemConfig cosmetic ? cosmetic.Icon : null;
                SetPrize(sprite, color);
                var rarity = T("rarity." + opening.Rarity.ToString().ToLowerInvariant());
                _result.text = T(opening.Pity ? "chest.gotPity" : "chest.gotItem", RewardNames.Describe(reward.Item, T), rarity);
                _result.color = color;
                return;
            }

            SetPrize(_ui != null ? _ui.CoinSprite : null, palette.Gold);
            _result.text = "+" + reward.Amount + " " + (reward.Currency != null ? T(reward.Currency.NameKey) : string.Empty);
            _result.color = palette.Gold;
        }

        private void SetPrize(Sprite sprite, Color glow)
        {
            _prize.sprite = sprite;
            _prize.enabled = sprite != null;
            _prize.rectTransform.localScale = Vector3.one;
            if (_glow != null)
            {
                glow.a = glow.a > 0f ? 0.55f : 0f;
                _glow.color = glow;
            }
        }
    }
}
