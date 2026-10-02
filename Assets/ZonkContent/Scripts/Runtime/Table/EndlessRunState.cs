using System.Collections.Generic;
using System.Text;
using System.Threading;
using Base.Core.Localization;
using Base.Services.Monetization;
using Cysharp.Threading.Tasks;
using Zonk.Configs;
using Zonk.Core.Dice;
using Zonk.Core.Modifiers;
using Zonk.Progress;
using Zonk.UI;
using Zonk.UI.Windows;

namespace Zonk.Table
{
    /// <summary>
    /// «Бесконечный забег»: лобби (рекорд, текущий забег) → этаж (соперник, цель, правила, сердца, находки) → партия →
    /// итоги → находка на выбор → следующий этаж. Проигрыш забирает сердце и переигрывает этаж; сердца кончились —
    /// один раз продолжить за рекламу, иначе конец забега. Выход в меню сохраняет забег, энергия берётся только
    /// за начало нового. Логика прогресса — EndlessRunProgress, здесь только порядок окон.
    /// </summary>
    public sealed class EndlessRunState : ITableState
    {
        private const string RevivePlacement = "run_revive";
        private const string InterstitialTrigger = "match_end";

        private readonly EndlessRunProgress _run;
        private readonly ModeMatch _match;
        private readonly MatchAftermath _aftermath;
        private readonly EnergyGate _energy;
        private readonly IRewardService _rewards;
        private readonly IUiService _ui;
        private readonly UiKit _kit;
        private readonly ILocalization _localization;
        private readonly ParticipantFactory _participants;
        private readonly PlayerStats _stats;
        private readonly ContentDatabase _content;
        private readonly TutorialDirector _tutorial;
        private readonly Presentation.TableView _table;

        public EndlessRunState(EndlessRunProgress run, ModeMatch match, MatchAftermath aftermath, EnergyGate energy,
            IRewardService rewards, IUiService ui, UiKit kit, ILocalization localization, ParticipantFactory participants,
            PlayerStats stats, ContentDatabase content, TutorialDirector tutorial, Presentation.TableView table)
        {
            _run = run;
            _match = match;
            _aftermath = aftermath;
            _energy = energy;
            _rewards = rewards;
            _ui = ui;
            _kit = kit;
            _localization = localization;
            _participants = participants;
            _stats = stats;
            _content = content;
            _tutorial = tutorial;
            _table = table;
        }

        public string Id => TableStateIds.EndlessRun;

        private string T(string key) => _kit.T(key);
        private string T(string key, params object[] args) => _kit.T(key, args);

        public async UniTask<string> RunAsync(CancellationToken ct)
        {
            if (!_run.IsConfigured)
            {
                await Toast.ShowAsync(_kit, _table.UiRoot, T("mode.notReady"), UiColors.Bad, 1.2f, ct);
                return TableStateIds.Menu;
            }

            while (true)
            {
                var choice = await ShowLobbyAsync(ct);
                if (choice == ChallengeChoice.Back)
                    return TableStateIds.Menu;

                if (choice == ChallengeChoice.Secondary)
                {
                    // Новый забег поверх незаконченного — только с подтверждением.
                    if (!await ConfirmWindow.AskAsync(_ui, T("run.abandonConfirm", _run.Floor), ct))
                        continue;
                    _run.EndRun();
                }

                if (!_run.IsActive)
                {
                    if (!await _energy.PayAsync(_run.Config.EnergyCost, ct))
                        continue;

                    var me = _participants.LocalPlayer(T("campaign.you"), true);
                    _run.StartNew(me.Dice, SplitMixRandom.NewSeed());
                }

                await PlayRunAsync(ct);
            }
        }

        private async UniTask<ChallengeChoice> ShowLobbyAsync(CancellationToken ct)
        {
            var body = new StringBuilder();
            body.Append(T("run.about")).Append("\n\n");
            body.Append(T("run.best", _run.Best)).Append('\n');
            string primary;
            string secondary = null;
            if (_run.IsActive)
            {
                body.Append('\n').Append(T("run.current", _run.Floor, _run.Hearts)).Append('\n');
                AppendPerks(body);
                primary = T("run.continue", _run.Floor);
                secondary = T("run.newRun");
            }
            else
            {
                var cost = _run.Config.EnergyCost;
                primary = cost > 0 ? T("run.startCost", cost) : T("run.start");
            }

            return await ModeWindows.AskAsync(_ui, T("mode.endlessRun"), null, body.ToString(), primary, secondary, T("ui.back"), ct,
                _tutorial, TutorialTrigger.EndlessRun);
        }

        /// <summary>Этажи подряд, пока игрок не выйдет в меню или забег не кончится.</summary>
        private async UniTask PlayRunAsync(CancellationToken ct)
        {
            while (_run.IsActive)
            {
                // Находки, не выбранные до перезапуска игры, предлагаются снова.
                if (_run.Offers.Count > 0)
                    await ChooseOfferAsync(ct);

                var floor = _run.BuildFloor();
                if (await ShowFloorAsync(floor, ct) != ChallengeChoice.Primary)
                    return;

                var result = await _match.PlayAsync(new ModeMatchSetup
                {
                    Mode = TableStateIds.EndlessRun,
                    Opponent = floor.Opponent,
                    EnemyDice = floor.EnemyDice,
                    EnemyAi = floor.EnemyAi,
                    PlayerDice = floor.PlayerDice,
                    Target = floor.Target,
                    SharedRules = floor.SharedRules,
                    PlayerModifiers = floor.PlayerModifiers,
                    EnemyModifiers = floor.EnemyModifiers,
                }, ct);

                ResultsChoice choice;
                if (result.Won)
                {
                    var win = _run.OnWin();
                    _stats.SubmitAsync(CancellationToken.None).Forget();
                    choice = await _aftermath.ShowAsync(result.Outcome, result.Players, win.Rewards, InterstitialTrigger, true, ct,
                        notes: WinNotes(win), againText: T("run.next"));
                }
                else
                {
                    var hearts = _run.OnLoss();
                    var notes = new List<string> { hearts > 0 ? T("run.heartLost", hearts) : T("run.noHearts") };
                    choice = await _aftermath.ShowAsync(result.Outcome, result.Players, null, InterstitialTrigger, hearts > 0, ct,
                        notes: notes, againText: T("run.retry"));

                    // Сердца кончились: продолжить за рекламу (этаж переигрывается сразу) или конец забега.
                    if (hearts <= 0)
                    {
                        if (!await TryReviveAsync(ct))
                        {
                            await EndRunAsync(ct);
                            return;
                        }

                        continue;
                    }
                }

                if (choice == ResultsChoice.Menu)
                    return;
            }
        }

        private async UniTask<ChallengeChoice> ShowFloorAsync(RunFloor floor, CancellationToken ct)
        {
            var body = new StringBuilder();
            ModeWindows.AppendOpponent(body, floor.Opponent, _kit);
            body.Append("\n\n").Append(T("run.target", floor.Target));
            body.Append('\n').Append(T("run.hearts", _run.Hearts));
            body.Append('\n').Append(T("run.enemyPower", UnityEngine.Mathf.RoundToInt(floor.EnemyPower * 100f)));

            if (floor.NewRule != null)
            {
                body.Append("\n\n<color=#").Append(UnityEngine.ColorUtility.ToHtmlStringRGB(UiColors.Bad)).Append('>')
                    .Append(T("run.newRule")).Append(' ').Append(RuleTexts.Describe(floor.NewRule, T, Language)).Append("</color>");
            }

            if (RuleTexts.HasAny(floor.SharedRules))
                body.Append("\n\n").Append(T("campaign.rules")).Append('\n').Append(RuleTexts.List(floor.SharedRules, T, Language));

            body.Append("\n\n");
            AppendPerks(body);

            var title = floor.IsGuardian ? T("run.floorGuardian", floor.Floor) : T("run.floor", floor.Floor);
            return await ModeWindows.AskAsync(_ui, title, floor.Opponent.Portrait, body.ToString(), T("run.fight"), null, T("run.toMenu"), ct);
        }

        /// <summary>Находки игрока: множители очков и особые кости забега.</summary>
        private void AppendPerks(StringBuilder body)
        {
            body.Append(T("run.yourPerks"));
            var any = false;
            foreach (var combo in _run.Combos)
            {
                if (combo == null || combo.Multiplier <= 1.001f)
                    continue;
                body.Append("\n• ").Append(ComboText(combo.Category, combo.Multiplier));
                any = true;
            }

            // Кости забега по ID из сохранения: собирать этаж (соперник, правила) ради них не нужно.
            foreach (var id in _run.DiceIds)
            {
                var die = string.IsNullOrEmpty(id) ? null : _content.Get<DieConfig>(id);
                if (die == null)
                    continue;
                body.Append("\n• ").Append(T("reward.die", T(die.NameKey)));
                any = true;
            }

            if (!any)
                body.Append("\n• ").Append(T("run.noPerks"));
        }

        private async UniTask ChooseOfferAsync(CancellationToken ct)
        {
            var options = new List<string>();
            foreach (var offer in _run.Offers)
                options.Add(OfferText(offer));

            var window = await _ui.OpenAsync<PerkChoiceWindow>(ct, w => w.Setup(T("run.choosePerk"), options));
            int index;
            try
            {
                index = await window.WaitChoiceAsync(ct);
            }
            finally
            {
                await _ui.CloseAsync(window, CancellationToken.None);
            }

            _run.Choose(index);
            _table.Sound.Play(Presentation.Sfx.Coin);
        }

        private string OfferText(RunOfferSave offer)
        {
            switch (offer.Kind)
            {
                case RunOfferKind.Combo:
                    var current = _run.ComboMultiplier(offer.Value);
                    return T("run.perk.combo") + "\n" + ComboText(offer.Value, current + _run.Config.ComboStep);
                case RunOfferKind.Die:
                    var die = _content.Get<DieConfig>(offer.Value);
                    return T("run.perk.die") + "\n" + (die != null ? T(die.NameKey) : offer.Value);
                default:
                    return T("run.perk.heart") + "\n" + T("run.perk.heartText", _run.Hearts + 1);
            }
        }

        /// <summary>«Очки за стрит: ×1,6» — тем же текстом, что правила.</summary>
        private string ComboText(string category, float multiplier)
        {
            return RuleTexts.Describe(new ComboMultiplierModifier { Category = category, Multiplier = multiplier }, T, Language);
        }

        private List<string> WinNotes(RunWin win)
        {
            var notes = new List<string>();
            if (win.NewRecord)
                notes.Add(T("run.newRecord", win.Floor));
            if (win.HeartsGained > 0)
                notes.Add(T("run.heartGained", win.HeartsGained));
            foreach (var milestone in win.Milestones)
                notes.Add(T("run.milestone", milestone));
            return notes;
        }

        /// <summary>Сердца кончились: один раз за забег продолжить за рекламу.</summary>
        private async UniTask<bool> TryReviveAsync(CancellationToken ct)
        {
            if (!await ModeWindows.TryReviveAsync(_run.CanRevive, RevivePlacement, _ui, _rewards, _kit, ct))
                return false;

            _run.Revive();
            return true;
        }

        private async UniTask EndRunAsync(CancellationToken ct)
        {
            var reached = _run.Floor - 1;
            _run.EndRun();
            _stats.SubmitAsync(CancellationToken.None).Forget();
            await Toast.ShowAsync(_kit, _table.UiRoot, T("run.over", reached, _run.Best), UiColors.Gold, 1.6f, ct);
        }

        private string Language => _localization != null ? _localization.Language : "ru";
    }
}
