using System.Threading;
using Base.Services.Monetization;
using Cysharp.Threading.Tasks;
using Zonk.Configs;
using Zonk.Progress;
using Zonk.UI;
using Zonk.UI.Windows;

namespace Zonk.Table
{
    /// <summary>
    /// Плата энергией за партию или забег. Не хватает — предложить пополнить за рекламу (или сообщить, что рекламы нет).
    /// Одно место для кампании, башни и «Бесконечного забега».
    /// </summary>
    public sealed class EnergyGate
    {
        /// <summary>Место рекламы «пополнить энергию» (MonetizationConfig, аналитика).</summary>
        public const string Placement = "energy_refill";

        private readonly IWallet _wallet;
        private readonly GameConfig _config;
        private readonly IRewardService _rewards;
        private readonly IUiService _ui;
        private readonly UiKit _kit;
        private readonly Presentation.TableView _table;

        public EnergyGate(IWallet wallet, GameConfig config, IRewardService rewards, IUiService ui, UiKit kit,
            Presentation.TableView table)
        {
            _wallet = wallet;
            _config = config;
            _rewards = rewards;
            _ui = ui;
            _kit = kit;
            _table = table;
        }

        /// <summary>Списать cost энергии. true — списано (или платить нечем не нужно), false — игрок не смог или отказался.</summary>
        public async UniTask<bool> PayAsync(int cost, CancellationToken ct)
        {
            if (cost <= 0 || _config.Energy == null)
                return true;

            if (_wallet.TrySpend(_config.Energy, cost))
                return true;

            if (!_rewards.CanOffer)
            {
                await Toast.ShowAsync(_kit, _table.UiRoot, _kit.T("campaign.noEnergy"), UiColors.Bad, 1f, ct);
                return false;
            }

            if (!await ConfirmWindow.AskAsync(_ui, _kit.T("campaign.energyForAd", cost), ct))
                return false;

            var result = await _rewards.RequestAsync(Placement, ct);
            if (!result.IsGranted())
                return false;

            _wallet.Add(_config.Energy, cost);
            return _wallet.TrySpend(_config.Energy, cost);
        }
    }
}
