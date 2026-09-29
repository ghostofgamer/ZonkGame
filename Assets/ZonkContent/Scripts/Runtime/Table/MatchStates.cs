using System.Collections.Generic;
using System.Threading;
using Base.Platform;
using Base.Services.Monetization;
using Base.Services.Saves;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Dice;
using Zonk.Core.Match;
using Zonk.MatchFlow;
using Zonk.Presentation;
using Zonk.Progress;
using Zonk.UI;
using Zonk.UI.Windows;

namespace Zonk.Table
{
    /// <summary>Общее для режимов после партии: итоги, награды, удвоение за рекламу, межстраничная реклама.</summary>
    public sealed class MatchAftermath
    {
        private readonly UiKit _kit;
        private readonly TableView _table;
        private readonly MatchPresenter _presenter;
        private readonly RewardGranter _granter;
        private readonly IRewardService _rewards;
        private readonly IInterstitialService _interstitials;
        private readonly ISaveStore _saves;
        private readonly IUiService _ui;

        public MatchAftermath(UiKit kit, TableView table, MatchPresenter presenter, RewardGranter granter,
            IRewardService rewards, IInterstitialService interstitials, ISaveStore saves, IUiService ui)
        {
            _kit = kit;
            _table = table;
            _presenter = presenter;
            _granter = granter;
            _rewards = rewards;
            _interstitials = interstitials;
            _saves = saves;
            _ui = ui;
        }

        /// <summary>Показывает итоги. rewards выдаются здесь; удвоение: те же валютные награды ещё раз за рекламу.</summary>
        public async UniTask<ResultsChoice> ShowAsync(MatchOutcome outcome, IReadOnlyList<MatchParticipant> participants,
            IReadOnlyList<Reward> rewards, string interstitialTrigger, bool canAgain, CancellationToken ct)
        {
            var match = outcome.Match;
            var winner = outcome.Winner >= 0 ? participants[outcome.Winner] : null;
            var localWon = winner != null && winner.Controller == ControllerKind.Local;
            var vsAi = false;
            foreach (var participant in participants)
                vsAi |= participant.Controller == ControllerKind.Ai;

            string title;
            Color color;
            if (!vsAi && winner != null)
            {
                title = _kit.T("results.playerWon", winner.Name);
                color = UiColors.Gold;
            }
            else
            {
                title = _kit.T(localWon ? "results.won" : outcome.Surrendered ? "results.surrendered" : "results.lost");
                color = localWon ? UiColors.Good : UiColors.Bad;
            }

            await _presenter.ShowResultAsync(localWon || !vsAi, ct);

            var granted = localWon && rewards != null ? _granter.Grant(rewards) : new List<GrantedReward>();
            await _saves.SaveNowAsync(ct);

            var canDouble = HasCurrency(rewards) && localWon && _rewards.CanOffer;
            ResultsChoice choice;
            var screen = await _ui.OpenAsync<ResultsWindow>(ct, w => w.Setup(match, title, color, granted, canDouble, canAgain));
            try
            {
                while (true)
                {
                    choice = await screen.WaitChoiceAsync(ct);
                    if (choice != ResultsChoice.DoubleReward)
                        break;

                    var result = await _rewards.RequestAsync("double_reward", ct);
                    screen.HideDouble();
                    if (result.IsGranted())
                    {
                        var extra = _granter.Grant(CurrencyOnly(rewards));
                        granted.AddRange(extra);
                        screen.ShowRewards(granted);
                        _table.Sound.Play(Sfx.Coin);
                    }
                }
            }
            finally
            {
                await _ui.CloseAsync(screen, CancellationToken.None);
            }

            _presenter.EndMatch();

            // Естественная пауза между партиями: общий слой сам решит, пора ли показывать рекламу.
            if (!string.IsNullOrEmpty(interstitialTrigger))
                await _interstitials.TryShowAsync(interstitialTrigger, ct);

            return choice;
        }

        private static bool HasCurrency(IReadOnlyList<Reward> rewards)
        {
            if (rewards == null)
                return false;
            foreach (var reward in rewards)
            {
                if (reward is CurrencyReward)
                    return true;
            }

            return false;
        }

        private static IEnumerable<Reward> CurrencyOnly(IReadOnlyList<Reward> rewards)
        {
            foreach (var reward in rewards)
            {
                if (reward is CurrencyReward)
                    yield return reward;
            }
        }
    }

    /// <summary>Игра вдвоём на одном экране: бесплатно, без наград.</summary>
    public sealed class HotSeatState : ITableState
    {
        private readonly UiKit _kit;
        private readonly TableView _table;
        private readonly HotSeatSettings _settings;
        private readonly GameConfig _config;
        private readonly ContentDatabase _content;
        private readonly IInventory _inventory;
        private readonly OwnedContent _owned;
        private readonly ParticipantFactory _participants;
        private readonly MatchRunner _runner;
        private readonly MatchAftermath _aftermath;
        private readonly IPlatformService _platform;
        private readonly IUiService _ui;

        public HotSeatState(UiKit kit, TableView table, HotSeatSettings settings, GameConfig config, ContentDatabase content,
            IInventory inventory, OwnedContent owned, ParticipantFactory participants, MatchRunner runner,
            MatchAftermath aftermath, IPlatformService platform, IUiService ui)
        {
            _kit = kit;
            _table = table;
            _settings = settings;
            _config = config;
            _content = content;
            _inventory = inventory;
            _owned = owned;
            _participants = participants;
            _runner = runner;
            _aftermath = aftermath;
            _platform = platform;
            _ui = ui;
        }

        public string Id => TableStateIds.HotSeat;

        public async UniTask<string> RunAsync(CancellationToken ct)
        {
            var setup = await _ui.OpenAsync<HotSeatSetupWindow>(ct, w => w.Setup(_settings, _config, _content, _inventory,
                _owned.Dice(), _owned.Items(SlotIds.DiceSkin)));
            bool start;
            try
            {
                start = await setup.WaitStartAsync(ct);
            }
            finally
            {
                await _ui.CloseAsync(setup, CancellationToken.None);
            }

            if (!start)
                return TableStateIds.Menu;

            while (true)
            {
                var data = _settings.Data;
                var players = new List<MatchParticipant>();
                for (var i = 0; i < HotSeatSettings.PlayerCount; i++)
                    players.Add(_participants.HotSeatPlayer(i, data.Players[i], data.SpecialDice));

                // Одинаковые скины: второму игроку запасной, чтобы кости не путались.
                if (players[0].DiceSkin == players[1].DiceSkin && _config.SecondPlayerFallbackSkin != null)
                    players[1].DiceSkin = _config.SecondPlayerFallbackSkin;

                var mode = _config.HotSeatMode;
                var settings = new MatchSettings
                {
                    Rules = mode != null && mode.Rules != null ? mode.Rules.ToRuleSet(data.Target) : Core.Rules.RuleSet.CreateClassic(),
                    Players = new List<PlayerSetup>
                    {
                        new PlayerSetup(players[0].Name, ParticipantFactory.Specs(players[0].Dice)),
                        new PlayerSetup(players[1].Name, ParticipantFactory.Specs(players[1].Dice)),
                    },
                    Seed = SplitMixRandom.NewSeed(),
                    FirstPlayer = _settings.PickFirstPlayer(),
                };
                settings.Rules.TargetScore = data.Target;

                _platform.NotifyGameplayStart();
                var outcome = await _runner.RunAsync(settings, players, ct);
                _platform.NotifyGameplayStop();

                var choice = await _aftermath.ShowAsync(outcome, players, mode != null ? mode.WinRewards : null,
                    mode != null ? mode.InterstitialTrigger : null, true, ct);
                if (choice != ResultsChoice.Again)
                    return TableStateIds.Menu;
            }
        }
    }

    /// <summary>
    /// Кампания: выбор соперника, сюжет главы, энергия за вход, партия, награды,
    /// концовка главы после босса.
    /// </summary>
    public sealed class CampaignState : ITableState
    {
        private const string EnergyPlacement = "energy_refill";

        private readonly UiKit _kit;
        private readonly TableView _table;
        private readonly ICampaignProgress _progress;
        private readonly IWallet _wallet;
        private readonly GameConfig _config;
        private readonly ContentDatabase _content;
        private readonly IInventory _inventory;
        private readonly ILoadout _loadout;
        private readonly OwnedContent _owned;
        private readonly ParticipantFactory _participants;
        private readonly MatchRunner _runner;
        private readonly MatchAftermath _aftermath;
        private readonly StageDresser _dresser;
        private readonly IRewardService _rewards;
        private readonly IPlatformService _platform;
        private readonly IUiService _ui;

        public CampaignState(UiKit kit, TableView table, ICampaignProgress progress, IWallet wallet, GameConfig config,
            ContentDatabase content, IInventory inventory, ILoadout loadout, OwnedContent owned,
            ParticipantFactory participants, MatchRunner runner, MatchAftermath aftermath, StageDresser dresser,
            IRewardService rewards, IPlatformService platform, IUiService ui)
        {
            _kit = kit;
            _table = table;
            _progress = progress;
            _wallet = wallet;
            _config = config;
            _content = content;
            _inventory = inventory;
            _loadout = loadout;
            _owned = owned;
            _participants = participants;
            _runner = runner;
            _aftermath = aftermath;
            _dresser = dresser;
            _rewards = rewards;
            _platform = platform;
            _ui = ui;
        }

        public string Id => TableStateIds.Campaign;

        public async UniTask<string> RunAsync(CancellationToken ct)
        {
            while (true)
            {
                OpponentConfig opponent;
                ChapterConfig chapter;
                var allowSpecial = _config.CampaignMode == null || _config.CampaignMode.AllowSpecialDice;
                var screen = await _ui.OpenAsync<CampaignWindow>(ct,
                    w => w.Setup(_progress, _config, _loadout, _owned.Dice(allowSpecial)));
                try
                {
                    opponent = await screen.WaitChoiceAsync(ct);
                    chapter = screen.Chapter;
                }
                finally
                {
                    await _ui.CloseAsync(screen, CancellationToken.None);
                }

                if (opponent == null)
                    return TableStateIds.Menu;

                if (!_progress.IsIntroSeen(chapter) && chapter.Intro.Count > 0)
                {
                    await StoryWindow.ShowAsync(_ui, chapter.Intro, ct);
                    _progress.MarkIntroSeen(chapter);
                }

                if (!await PayEnergyAsync(opponent, ct))
                    continue;

                while (await PlayAsync(chapter, opponent, allowSpecial, ct) && await PayEnergyAsync(opponent, ct))
                {
                }
            }
        }

        /// <summary>Партия с соперником. true: игрок хочет ещё раз с тем же соперником.</summary>
        private async UniTask<bool> PlayAsync(ChapterConfig chapter, OpponentConfig opponent, bool allowSpecial,
            CancellationToken ct)
        {
            _dresser.Override(chapter.Environment);

            var me = _participants.LocalPlayer(_kit.T("campaign.you"), allowSpecial);
            var enemy = _participants.Opponent(opponent, _kit.T(opponent.NameKey));
            var players = new List<MatchParticipant> { me, enemy };

            var mode = _config.CampaignMode;
            var rules = mode != null && mode.Rules != null ? mode.Rules.ToRuleSet(opponent.TargetScore) : Core.Rules.RuleSet.CreateClassic();
            var settings = new MatchSettings
            {
                Rules = rules,
                Players = new List<PlayerSetup>
                {
                    new PlayerSetup(me.Name, ParticipantFactory.Specs(me.Dice)),
                    new PlayerSetup(enemy.Name, ParticipantFactory.Specs(enemy.Dice)),
                },
                Modifiers = new List<Core.Modifiers.MatchModifier>(opponent.Modifiers),
                Seed = SplitMixRandom.NewSeed(),
                FirstPlayer = Random.Range(0, 2),
            };

            _platform.NotifyGameplayStart();
            var outcome = await _runner.RunAsync(settings, players, ct);
            _platform.NotifyGameplayStop();

            var won = outcome.Winner == 0;
            var firstWin = won && _progress.MarkBeaten(opponent);
            var rewards = new List<Reward>();
            if (won)
            {
                rewards.AddRange(firstWin ? opponent.FirstWinRewards : opponent.RepeatWinRewards);
                if (mode != null)
                    rewards.AddRange(mode.WinRewards);
            }

            var choice = await _aftermath.ShowAsync(outcome, players, rewards, mode != null ? mode.InterstitialTrigger : null,
                true, ct);

            _dresser.ApplyEquipped();

            if (firstWin && opponent.IsBoss && !_progress.IsOutroSeen(chapter) && chapter.Outro.Count > 0)
            {
                await StoryWindow.ShowAsync(_ui, chapter.Outro, ct);
                _progress.MarkOutroSeen(chapter);
            }

            return choice == ResultsChoice.Again;
        }

        /// <summary>Списывает энергию. Не хватает: предложить пополнение за рекламу.</summary>
        private async UniTask<bool> PayEnergyAsync(OpponentConfig opponent, CancellationToken ct)
        {
            var cost = CampaignWindow.EnergyCost(opponent, _config);
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

            var result = await _rewards.RequestAsync(EnergyPlacement, ct);
            if (!result.IsGranted())
                return false;

            _wallet.Add(_config.Energy, cost);
            return _wallet.TrySpend(_config.Energy, cost);
        }
    }
}
