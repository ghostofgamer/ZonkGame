using System.Collections.Generic;
using System.Text;
using System.Threading;
using Base.Core.Localization;
using Base.Services.Monetization;
using Cysharp.Threading.Tasks;
using Zonk.Configs;
using Zonk.Core.Modifiers;
using Zonk.Progress;
using Zonk.UI;
using Zonk.UI.Windows;

namespace Zonk.Table
{
    /// <summary>
    /// Башня: лобби (рубеж, рекорд, сколько этажей) → этаж (соперник, цель, правила, награда за первое прохождение,
    /// сердца) → партия → итоги → следующий этаж. Попытка начинается с рубежа и стоит энергию; проигрыш забирает
    /// сердце и переигрывает этаж; сердца кончились — один раз продолжить за рекламу, иначе попытка окончена.
    /// Выход в меню сохраняет попытку. Логика — TowerProgress, здесь только порядок окон.
    /// </summary>
    public sealed class TowerState : ITableState
    {
        private const string RevivePlacement = "tower_revive";
        private const string InterstitialTrigger = "match_end";

        private readonly TowerProgress _tower;
        private readonly ModeMatch _match;
        private readonly MatchAftermath _aftermath;
        private readonly EnergyGate _energy;
        private readonly IRewardService _rewards;
        private readonly IUiService _ui;
        private readonly UiKit _kit;
        private readonly ILocalization _localization;
        private readonly PlayerStats _stats;
        private readonly TutorialDirector _tutorial;
        private readonly Presentation.TableView _table;

        public TowerState(TowerProgress tower, ModeMatch match, MatchAftermath aftermath, EnergyGate energy, IRewardService rewards,
            IUiService ui, UiKit kit, ILocalization localization, PlayerStats stats, TutorialDirector tutorial,
            Presentation.TableView table)
        {
            _tower = tower;
            _match = match;
            _aftermath = aftermath;
            _energy = energy;
            _rewards = rewards;
            _ui = ui;
            _kit = kit;
            _localization = localization;
            _stats = stats;
            _tutorial = tutorial;
            _table = table;
        }

        public string Id => TableStateIds.Tower;

        private string T(string key) => _kit.T(key);
        private string T(string key, params object[] args) => _kit.T(key, args);
        private string Language => _localization != null ? _localization.Language : "ru";

        public async UniTask<string> RunAsync(CancellationToken ct)
        {
            if (!_tower.IsConfigured)
            {
                await Toast.ShowAsync(_kit, _table.UiRoot, T("mode.notReady"), UiColors.Bad, 1.2f, ct);
                return TableStateIds.Menu;
            }

            while (true)
            {
                if (await ShowLobbyAsync(ct) == ChallengeChoice.Back)
                    return TableStateIds.Menu;

                if (!_tower.IsActive)
                {
                    if (!await _energy.PayAsync(_tower.Config.EnergyCost, ct))
                        continue;
                    _tower.StartAttempt();
                }

                await PlayAttemptAsync(ct);
            }
        }

        private async UniTask<ChallengeChoice> ShowLobbyAsync(CancellationToken ct)
        {
            var body = new StringBuilder();
            body.Append(T("tower.about", _tower.FloorCount, _tower.Config.CheckpointEvery, _tower.Config.HeartsPerAttempt)).Append("\n\n");
            body.Append(T("tower.checkpoint", _tower.Checkpoint, _tower.FloorCount)).Append('\n');
            body.Append(T("tower.best", _tower.Best, _tower.FloorCount));
            if (_tower.IsComplete)
                body.Append("\n\n").Append(T("tower.complete"));

            string primary;
            if (_tower.IsActive)
            {
                body.Append("\n\n").Append(T("tower.current", _tower.FloorIndex + 1, _tower.Hearts));
                primary = T("tower.continue", _tower.FloorIndex + 1);
            }
            else
            {
                var cost = _tower.Config.EnergyCost;
                var from = _tower.FloorIndex + 1;
                primary = cost > 0 ? T("tower.startCost", from, cost) : T("tower.start", from);
            }

            var window = await _ui.OpenAsync<ChallengeWindow>(ct,
                w => w.Setup(T("mode.tower"), null, body.ToString(), primary, null, T("ui.back")));
            _tutorial.Show(TutorialTrigger.Tower);
            try
            {
                return await window.WaitChoiceAsync(ct);
            }
            finally
            {
                _tutorial.Hide();
                await _ui.CloseAsync(window, CancellationToken.None);
            }
        }

        private async UniTask PlayAttemptAsync(CancellationToken ct)
        {
            while (_tower.IsActive)
            {
                var index = _tower.FloorIndex;
                var floor = _tower.CurrentFloor;
                if (floor == null || floor.Opponent == null)
                {
                    _tower.EndAttempt();
                    return;
                }

                var rules = new List<MatchModifier>();
                foreach (var rule in floor.Opponent.Modifiers)
                {
                    if (rule != null)
                        rules.Add(rule);
                }

                foreach (var rule in floor.Rules)
                {
                    if (rule != null)
                        rules.Add(rule);
                }

                if (await ShowFloorAsync(index, floor, rules, ct) != ChallengeChoice.Primary)
                    return;

                var result = await _match.PlayAsync(new ModeMatchSetup
                {
                    Opponent = floor.Opponent,
                    Target = floor.Target,
                    SharedRules = rules,
                }, ct);

                if (result.Won)
                {
                    var win = _tower.OnWin();
                    _stats.SubmitAsync(CancellationToken.None).Forget();
                    var notes = new List<string>();
                    if (win.CheckpointReached)
                        notes.Add(T("tower.checkpointReached", win.Floor));
                    if (win.TowerCompleted)
                        notes.Add(T("tower.completed"));

                    var choice = await _aftermath.ShowAsync(result.Outcome, result.Players, win.Rewards, InterstitialTrigger,
                        !win.TowerCompleted, ct, notes: notes, againText: T("tower.next"));
                    if (win.TowerCompleted || choice == ResultsChoice.Menu)
                        return;
                }
                else
                {
                    var hearts = _tower.OnLoss();
                    var notes = new List<string> { hearts > 0 ? T("run.heartLost", hearts) : T("run.noHearts") };
                    var choice = await _aftermath.ShowAsync(result.Outcome, result.Players, null, InterstitialTrigger, hearts > 0, ct,
                        notes: notes, againText: T("run.retry"));

                    if (hearts <= 0)
                    {
                        if (!await TryReviveAsync(ct))
                        {
                            _tower.EndAttempt();
                            await Toast.ShowAsync(_kit, _table.UiRoot, T("tower.attemptOver", _tower.Checkpoint + 1), UiColors.Gold,
                                1.6f, ct);
                            return;
                        }

                        continue;
                    }

                    if (choice == ResultsChoice.Menu)
                        return;
                }
            }
        }

        private async UniTask<ChallengeChoice> ShowFloorAsync(int index, TowerFloor floor, List<MatchModifier> rules,
            CancellationToken ct)
        {
            var opponent = floor.Opponent;
            var body = new StringBuilder();
            body.Append("<b>").Append(T(opponent.NameKey)).Append("</b>");
            if (!string.IsNullOrEmpty(opponent.TitleKey))
                body.Append('\n').Append(T(opponent.TitleKey));
            body.Append("\n\n").Append(T("run.target", floor.Target));
            body.Append('\n').Append(T("run.hearts", _tower.Hearts));

            if (RuleTexts.HasAny(rules))
                body.Append("\n\n").Append(T("campaign.rules")).Append('\n').Append(RuleTexts.List(rules, T, Language));

            // Награда за первое прохождение — чтобы было ради чего; повторное — монеты.
            var rewards = _tower.IsFirstClear(index) ? RewardsText(floor.FirstClearRewards) : string.Empty;
            if (rewards.Length > 0)
                body.Append("\n\n").Append(T("tower.reward", rewards));

            var guardian = opponent.IsBoss;
            var title = T(guardian ? "tower.floorGuardian" : "tower.floor", index + 1, _tower.FloorCount);
            var window = await _ui.OpenAsync<ChallengeWindow>(ct,
                w => w.Setup(title, opponent.Portrait, body.ToString(), T("run.fight"), null, T("run.toMenu")));
            try
            {
                return await window.WaitChoiceAsync(ct);
            }
            finally
            {
                await _ui.CloseAsync(window, CancellationToken.None);
            }
        }

        private string RewardsText(IReadOnlyList<Reward> rewards)
        {
            var parts = new List<string>();
            foreach (var reward in rewards)
            {
                switch (reward)
                {
                    case CurrencyReward currency when currency.Currency != null && currency.Amount > 0:
                        parts.Add(currency.Amount + " " + T(currency.Currency.NameKey));
                        break;
                    case ContentReward content when content.Item != null:
                        parts.Add(RewardNames.Describe(content.Item, T));
                        break;
                }
            }

            return string.Join(", ", parts);
        }

        private async UniTask<bool> TryReviveAsync(CancellationToken ct)
        {
            if (!_tower.CanRevive || !_rewards.CanOffer)
                return false;

            if (!await ConfirmWindow.AskAsync(_ui, T("run.reviveAsk"), ct))
                return false;

            var result = await _rewards.RequestAsync(RevivePlacement, ct);
            if (!result.IsGranted())
                return false;

            _tower.Revive();
            return true;
        }
    }
}
