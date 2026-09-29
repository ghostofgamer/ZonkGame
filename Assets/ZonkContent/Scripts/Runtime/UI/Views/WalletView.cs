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

        private void Refresh()
        {
            if (_wallet == null || _config == null || _localization == null)
                return;

            _coins.text = Format("ui.coins", _wallet.Get(_config.Coins));
            var energy = Format("ui.energy", _wallet.Get(_config.Energy), _config.Energy != null ? _config.Energy.RegenCap : 0);
            var next = _wallet.TimeToNextRegen(_config.Energy);
            if (next.HasValue)
                energy += $"  {(int)next.Value.TotalMinutes:0}:{next.Value.Seconds:00}";
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
