using System;
using System.Threading;
using Base.Core.Localization;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zenject;
using Zonk.Configs;
using Zonk.Progress;

namespace Zonk.UI.Views
{
    /// <summary>Монеты и энергия с таймером восстановления. Часть префабов окон, обновляется сама, пока видна.</summary>
    public sealed class WalletView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _coins;
        [SerializeField] private TMP_Text _energy;

        private IWallet _wallet;
        private GameConfig _config;
        private ILocalization _localization;
        private CancellationTokenSource _cts;

        [Inject]
        public void Construct(IWallet wallet, GameConfig config, ILocalization localization)
        {
            _wallet = wallet;
            _config = config;
            _localization = localization;
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
            UpdateAsync(_cts.Token).Forget();
        }

        private void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
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

        // Что показано сейчас: текст пересобирается, только если число изменилось (без мусора и перестройки меша
        // TextMeshPro каждые полсекунды).
        private int _shownCoins = int.MinValue;
        private int _shownEnergy = int.MinValue;
        private int _shownRegenSeconds = int.MinValue;

        private void Refresh()
        {
            if (_wallet == null || _config == null || _localization == null)
                return;

            var coins = _wallet.Get(_config.Coins);
            if (coins != _shownCoins)
            {
                _shownCoins = coins;
                _coins.text = Format("ui.coins", coins);
            }

            var energyAmount = _wallet.Get(_config.Energy);
            var next = _wallet.TimeToNextRegen(_config.Energy);
            var regenSeconds = next.HasValue ? (int)next.Value.TotalSeconds : -1;
            if (energyAmount == _shownEnergy && regenSeconds == _shownRegenSeconds)
                return;

            _shownEnergy = energyAmount;
            _shownRegenSeconds = regenSeconds;
            var energy = Format("ui.energy", energyAmount, _config.Energy != null ? _config.Energy.RegenCap : 0);
            if (next.HasValue)
                energy += $"  {regenSeconds / 60}:{regenSeconds % 60:00}";
            _energy.text = energy;
        }

        private string Format(string key, params object[] args)
        {
            try
            {
                return string.Format(_localization.Get(key), args);
            }
            catch (FormatException)
            {
                return _localization.Get(key);
            }
        }
    }
}
