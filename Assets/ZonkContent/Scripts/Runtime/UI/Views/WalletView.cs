using System;
using System.Threading;
using Base.Core.Localization;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;
using Zonk.Configs;
using Zonk.Progress;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Монеты и энергия с таймером восстановления. Часть префабов окон, обновляется сама, пока видна.
    /// Когда монет становится больше, из точки последнего нажатия в кошелёк летят монетки, а число
    /// «накручивается» — так любая награда (победа, задание, реклама, покупка) видна без кода в месте выдачи.
    /// </summary>
    public sealed class WalletView : MonoBehaviour
    {
        private const float FlightDuration = 0.55f;
        private const float CoinInterval = 0.05f;
        private const float CountDuration = 0.8f;
        private const int MaxFlyingCoins = 10;

        [SerializeField] private TMP_Text _coins;
        [SerializeField] private TMP_Text _energy;

        private IWallet _wallet;
        private GameConfig _config;
        private ILocalization _localization;
        private UiKit _kit;
        private CancellationTokenSource _cts;
        private Transform _flightLayer;

        // Что показано сейчас: текст пересобирается, только если число изменилось (без мусора и перестройки меша
        // TextMeshPro каждые полсекунды).
        private int _shownCoins = int.MinValue;
        private int _shownEnergy = int.MinValue;
        private int _shownRegenSeconds = int.MinValue;
        private string _shownEnergyTemplate;
        private string _energyHead;
        private readonly NumberTemplate _coinsText = new NumberTemplate();
        private readonly char[] _energyBuffer = new char[128];

        // Накрутка монет: к чему идёт число и идёт ли анимация.
        private int _targetCoins;
        private bool _counting;
        private bool _refreshing;

        [Inject]
        public void Construct(IWallet wallet, GameConfig config, ILocalization localization, [InjectOptional] UiKit kit)
        {
            _wallet = wallet;
            _config = config;
            _localization = localization;
            _kit = kit;
        }

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text coins, TMP_Text energy)
        {
            _coins = coins;
            _energy = energy;
        }
#endif

        private void OnEnable()
        {
            _cts = new CancellationTokenSource();
            var canvas = GetComponentInParent<Canvas>();
            _flightLayer = canvas != null ? canvas.rootCanvas.transform : transform.parent;

            if (_wallet != null)
                _wallet.Changed += OnWalletChanged;
            UpdateAsync(_cts.Token).Forget();
        }

        private void OnDisable()
        {
            if (_wallet != null)
                _wallet.Changed -= OnWalletChanged;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _counting = false;
        }

        private void OnWalletChanged(CurrencyConfig currency)
        {
            Refresh();
        }

        private async UniTaskVoid UpdateAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                Refresh();
                if (await UniTask.Delay(500, true, cancellationToken: ct).SuppressCancellationThrow())
                    return;
            }
        }

        private void Refresh()
        {
            // Wallet.Get может сам сообщить об изменении (восстановление энергии): без повторного входа.
            if (_refreshing || _wallet == null || _config == null || _localization == null)
                return;

            _refreshing = true;
            try
            {
                RefreshCoins();
                RefreshEnergy();
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void RefreshCoins()
        {
            var coins = _wallet.Get(_config.Coins);
            if (_counting)
            {
                _targetCoins = coins;
                return;
            }

            if (coins == _shownCoins)
                return;

            // Прибавка при открытом кошельке — полёт монет и накрутка; первое значение и траты — сразу.
            if (_shownCoins != int.MinValue && coins > _shownCoins && _kit != null && _cts != null)
            {
                _targetCoins = coins;
                GainAsync(_shownCoins, _cts.Token).Forget();
                return;
            }

            SetCoins(coins);
        }

        private void RefreshEnergy()
        {
            var energyAmount = _wallet.Get(_config.Energy);
            var next = _wallet.TimeToNextRegen(_config.Energy);
            var regenSeconds = next.HasValue ? (int)next.Value.TotalSeconds : -1;
            var template = _localization.Get("ui.energy");
            if (energyAmount == _shownEnergy && regenSeconds == _shownRegenSeconds && ReferenceEquals(template, _shownEnergyTemplate))
                return;

            // «Энергия: 3/5» собирается, только когда меняется запас (или язык); таймер — каждую секунду, без строк.
            if (energyAmount != _shownEnergy || !ReferenceEquals(template, _shownEnergyTemplate))
                _energyHead = UiFormat.Format(_localization, "ui.energy", energyAmount, _config.Energy != null ? _config.Energy.RegenCap : 0);

            _shownEnergy = energyAmount;
            _shownRegenSeconds = regenSeconds;
            _shownEnergyTemplate = template;
            if (regenSeconds < 0)
                _energy.text = _energyHead;
            else
                SetEnergyWithTimer(regenSeconds);
        }

        /// <summary>«Энергия: 3/5  4:07» в общий буфер символов: таймер тикает раз в секунду без выделения памяти.</summary>
        private void SetEnergyWithTimer(int seconds)
        {
            var length = 0;
            for (var i = 0; i < _energyHead.Length && length < _energyBuffer.Length - 16; i++)
                _energyBuffer[length++] = _energyHead[i];

            _energyBuffer[length++] = ' ';
            _energyBuffer[length++] = ' ';
            length = AppendNumber(seconds / 60, length);
            _energyBuffer[length++] = ':';
            _energyBuffer[length++] = (char)('0' + seconds % 60 / 10);
            _energyBuffer[length++] = (char)('0' + seconds % 10);
            _energy.SetCharArray(_energyBuffer, 0, length);
        }

        private int AppendNumber(int value, int length)
        {
            var start = length;
            do
            {
                _energyBuffer[length++] = (char)('0' + value % 10);
                value /= 10;
            } while (value > 0 && length < _energyBuffer.Length - 3);

            // Цифры записаны с конца: развернуть.
            for (int a = start, b = length - 1; a < b; a++, b--)
                (_energyBuffer[a], _energyBuffer[b]) = (_energyBuffer[b], _energyBuffer[a]);
            return length;
        }

        private void SetCoins(int coins)
        {
            if (coins == _shownCoins)
                return;

            _shownCoins = coins;
            // Во время накрутки меняется каждый кадр: число без сборки строки.
            _coinsText.Set(_coins, _localization.Get("ui.coins"), coins);
        }

        /// <summary>
        /// Показать прибавку, выданную до открытия окна (награды за партию): число откатывается на amount назад
        /// и накручивается с полётом монет.
        /// </summary>
        public void PlayGain(int amount)
        {
            if (amount <= 0 || _kit == null || _cts == null || _wallet == null || _config == null || _counting)
                return;

            var current = _wallet.Get(_config.Coins);
            _shownCoins = int.MinValue;
            SetCoins(current - amount);
            _targetCoins = current;
            GainAsync(current - amount, _cts.Token).Forget();
        }

        /// <summary>Монетки летят от точки нажатия к кошельку, число растёт от прежнего к новому.</summary>
        private async UniTaskVoid GainAsync(int from, CancellationToken ct)
        {
            _counting = true;
            var gained = _targetCoins - from;
            var flying = Mathf.Clamp(3 + gained / 100, 3, MaxFlyingCoins);
            var start = StartPoint();

            for (var i = 0; i < flying; i++)
                FlyCoinAsync(start, i * CoinInterval, ct).Forget();

            // Счёт начинает расти, когда долетает первая монетка.
            if (await UniTask.Delay(TimeSpan.FromSeconds(FlightDuration), cancellationToken: ct).SuppressCancellationThrow())
                return;

            var time = 0f;
            while (time < CountDuration)
            {
                var t = AnimateEaseOut(time / CountDuration);
                SetCoins(from + Mathf.RoundToInt((_targetCoins - from) * t));
                if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow())
                    return;
                time += Time.unscaledDeltaTime;
            }

            SetCoins(_targetCoins);
            _counting = false;

            // За время анимации могли прийти ещё монеты.
            Refresh();
        }

        private async UniTaskVoid FlyCoinAsync(Vector3 start, float delay, CancellationToken ct)
        {
            if (delay > 0f && await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct).SuppressCancellationThrow())
                return;

            var coin = _kit.RentCoin(_flightLayer);
            var rect = coin.rectTransform;
            var target = _coins.rectTransform.position;

            // Дуга вбок и вверх, у каждой монетки своя: из точки нажатия веером к кошельку.
            var side = UnityEngine.Random.Range(-1f, 1f) * 220f;
            var control = (start + target) * 0.5f + new Vector3(side, 180f, 0f);
            try
            {
                var time = 0f;
                while (time < FlightDuration)
                {
                    var t = time / FlightDuration;
                    var eased = t * t;
                    var a = Vector3.Lerp(start, control, eased);
                    var b = Vector3.Lerp(control, target, eased);
                    rect.position = Vector3.Lerp(a, b, eased);
                    rect.localScale = Vector3.one * (1.2f - 0.5f * t);
                    if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow())
                        return;
                    time += Time.unscaledDeltaTime;
                }

                // Толчок числа при попадании монетки.
                if (_coins != null)
                    _coins.rectTransform.localScale = Vector3.one * 1.15f;
                ReleaseScaleAsync(ct).Forget();
            }
            finally
            {
                _kit.ReturnCoin(coin);
            }
        }

        private async UniTaskVoid ReleaseScaleAsync(CancellationToken ct)
        {
            if (await UniTask.Delay(TimeSpan.FromSeconds(0.08f), cancellationToken: ct).SuppressCancellationThrow())
                return;
            if (_coins != null)
                _coins.rectTransform.localScale = Vector3.one;
        }

        /// <summary>Откуда летят монеты: последнее нажатие (кнопка награды), иначе центр экрана.</summary>
        private static Vector3 StartPoint()
        {
            var pointer = Pointer.current;
            if (pointer != null)
            {
                var position = pointer.position.ReadValue();
                if (position.x > 0f && position.y > 0f)
                    return position;
            }

            return new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        }

        private static float AnimateEaseOut(float t)
        {
            var u = 1f - t;
            return 1f - u * u * u;
        }
    }
}
