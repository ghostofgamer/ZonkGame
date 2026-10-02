using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Base.Core.Localization;
using Base.Services.Monetization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Dice;
using Zonk.Core.Modifiers;
using Zonk.Progress;
using Zonk.UI;
using Zonk.UI.Windows;

namespace Zonk.Table
{
    /// <summary>
    /// «Бесконечный забег»: лобби (рекорд, текущий забег) → развилка (обычный, сильный, привал или лавка) → этаж
    /// (соперник, цель, правила, сердца, находки) → партия → итоги → находка на выбор (за рекламу — ещё одна или
    /// поменять) → следующий этаж. Проигрыш: щит принимает удар, иначе минус сердце и переигровка; сердца кончились —
    /// «второе дыхание» или продолжить за рекламу (до MaxRevives раз), иначе конец. Выход в меню сохраняет забег,
    /// энергия — только за начало нового. Логика прогресса — EndlessRunProgress, здесь только порядок окон.
    /// </summary>
    public sealed class EndlessRunState : ITableState
    {
        private const string RevivePlacement = "run_revive";
        private const string ExtraPerkPlacement = "run_perk_extra";
        private const string RerollPlacement = "run_perk_reroll";
        private const string MilestonePlacement = "run_milestone_double";
        private const string InterstitialTrigger = "run_floor";

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
        private readonly RewardGranter _granter;
        private readonly UiPalette _palette;
        private readonly List<RunPath> _paths = new List<RunPath>();

        public EndlessRunState(EndlessRunProgress run, ModeMatch match, MatchAftermath aftermath, EnergyGate energy,
            IRewardService rewards, IUiService ui, UiKit kit, ILocalization localization, ParticipantFactory participants,
            PlayerStats stats, ContentDatabase content, TutorialDirector tutorial, Presentation.TableView table, RewardGranter granter,
            GameConfig config)
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
            _granter = granter;
            _palette = config != null && config.Ui != null ? config.Ui.Palette : new UiPalette();
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
                // Невыбранное до перезапуска игры предлагается снова: лавка или находки.
                if (_run.Offers.Count > 0)
                {
                    if (_run.IsShopOpen)
                        await ShopAsync(ct);
                    else
                        await ChooseOfferAsync(ct);
                }

                if (_run.NeedsPath)
                {
                    await ChoosePathAsync(ct);
                    continue;
                }

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
                    PlayerStartScore = floor.PlayerStartScore,
                    PlayerZonkSaves = floor.PlayerZonkSaves,
                }, ct);

                var players = result.Outcome.Match != null ? result.Outcome.Match.Players : null;
                if (players != null && players.Count > 0)
                    _run.OnMatchPlayed(floor, players[0].ZonkSavesLeft);

                // Межстраничная реклама — на паузе после партии, раз в InterstitialEvery этажей (частоту держит общий слой).
                var trigger = floor.Floor % Math.Max(1, _run.Config.InterstitialEvery) == 0 ? InterstitialTrigger : null;

                ResultsChoice choice;
                if (result.Won)
                {
                    var win = _run.OnWin();
                    _stats.SubmitAsync(CancellationToken.None).Forget();
                    choice = await _aftermath.ShowAsync(result.Outcome, result.Players, win.Rewards, trigger, true, ct,
                        notes: WinNotes(win), againText: T("run.next"));
                    await OfferMilestoneDoubleAsync(win, ct);
                }
                else
                {
                    var loss = _run.OnLoss();
                    var notes = new List<string> { LossNote(loss) };
                    choice = await _aftermath.ShowAsync(result.Outcome, result.Players, null, trigger, loss != RunLoss.Out, ct,
                        notes: notes, againText: T("run.retry"));

                    // Сердца кончились: продолжить за рекламу (этаж переигрывается сразу) или конец забега.
                    if (loss == RunLoss.Out)
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

        private string LossNote(RunLoss loss)
        {
            switch (loss)
            {
                case RunLoss.Shielded: return T("run.shielded", _run.Shields);
                case RunLoss.SecondWind: return T("run.secondWind");
                case RunLoss.HeartLost: return T("run.heartLost", _run.Hearts);
                default: return T("run.noHearts");
            }
        }

        private async UniTask<ChallengeChoice> ShowFloorAsync(RunFloor floor, CancellationToken ct)
        {
            var body = new StringBuilder();
            ModeWindows.AppendOpponent(body, floor.Opponent, _kit);
            if (floor.IsElite)
                body.Append("\n<color=#").Append(ColorUtility.ToHtmlStringRGB(UiColors.Bad)).Append('>').Append(T("run.eliteFloor")).Append("</color>");
            body.Append("\n\n").Append(T("run.target", floor.Target));
            body.Append('\n').Append(Status());
            body.Append('\n').Append(T("run.enemyPower", Mathf.RoundToInt(floor.EnemyPower * 100f)));
            if (floor.PlayerStartScore > 0)
                body.Append('\n').Append(T("run.headStart", floor.PlayerStartScore));

            if (floor.NewRule != null)
            {
                body.Append("\n\n<color=#").Append(ColorUtility.ToHtmlStringRGB(UiColors.Bad)).Append('>')
                    .Append(T("run.newRule")).Append(' ').Append(RuleTexts.Describe(floor.NewRule, T, Language)).Append("</color>");
            }

            if (RuleTexts.HasAny(floor.SharedRules))
                body.Append("\n\n").Append(T("campaign.rules")).Append('\n').Append(RuleTexts.List(floor.SharedRules, T, Language));

            body.Append("\n\n");
            AppendPerks(body);

            var title = floor.IsGuardian ? T("run.floorGuardian", floor.Floor) : T("run.floor", floor.Floor);
            return await ModeWindows.AskAsync(_ui, title, floor.Opponent.Portrait, body.ToString(), T("run.fight"), null, T("run.toMenu"), ct);
        }

        /// <summary>«Сердца: 3 · Щиты: 1 · Спасения: 2 · Жетоны: 7».</summary>
        private string Status()
        {
            return T("run.status", _run.Hearts, _run.Shields, _run.ZonkSaves, _run.Tokens);
        }

        /// <summary>Находки игрока: множители, особые кости, взятые находки со стаками.</summary>
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

            foreach (var stack in _run.Perks)
            {
                var entry = _run.FindEntry(stack.Id);
                // Множители и кости уже перечислены выше; сердца — в строке состояния.
                if (entry == null || entry.Perk is ComboPerk || entry.Perk is SpecialDiePerk || entry.Perk is LoadedDiePerk ||
                    entry.Perk is HeartPerk || stack.Stacks <= 0)
                    continue;
                body.Append("\n• ").Append(T(entry.NameKey));
                if (stack.Stacks > 1)
                    body.Append(" ×").Append(stack.Stacks);
                any = true;
            }

            if (!any)
                body.Append("\n• ").Append(T("run.noPerks"));
        }

        // ---------- Развилка ----------

        private async UniTask ChoosePathAsync(CancellationToken ct)
        {
            _run.PathOptions(_paths);
            var options = new List<PerkOption>(_paths.Count);
            foreach (var path in _paths)
            {
                var color = path == RunPath.Elite ? _palette.RarityRare
                    : path == RunPath.Rest ? _palette.Good
                    : path == RunPath.Shop ? _palette.RarityLegendary
                    : _palette.Button;
                options.Add(new PerkOption(PathText(path), color));
            }

            var index = await AskAsync(T("run.choosePath", _run.Floor), Status(), options, null, ct);
            var chosen = index >= 0 && index < _paths.Count ? _paths[index] : RunPath.Normal;
            _run.ChoosePath(chosen);
            if (chosen == RunPath.Rest)
                _table.Sound.Play(Presentation.Sfx.Coin);
        }

        private string PathText(RunPath path)
        {
            switch (path)
            {
                case RunPath.Elite:
                    return T("run.path.elite") + "\n<size=70%>" + T("run.path.elite.desc") + "</size>";
                case RunPath.Rest:
                    return T("run.path.rest") + "\n<size=70%>" + T("run.path.rest.desc", Math.Min(_run.MaxHearts, _run.Hearts + 1)) + "</size>";
                case RunPath.Shop:
                    return T("run.path.shop") + "\n<size=70%>" + T("run.path.shop.desc", _run.Tokens) + "</size>";
                default:
                    return T("run.path.normal") + "\n<size=70%>" + T("run.path.normal.desc") + "</size>";
            }
        }

        // ---------- Лавка ----------

        private async UniTask ShopAsync(CancellationToken ct)
        {
            while (_run.IsShopOpen)
            {
                var options = new List<PerkOption>();
                foreach (var offer in _run.Offers)
                {
                    var entry = _run.EntryOf(offer);
                    var text = OfferText(offer) + "\n" + T("run.price", offer.Price);
                    options.Add(new PerkOption(text, RarityColor(entry), _run.Tokens >= offer.Price));
                }

                var index = await AskAsync(T("run.shop"), T("run.tokens", _run.Tokens), options, new[] { T("run.leaveShop") }, ct);
                if (index < 0)
                {
                    _run.LeaveShop();
                    return;
                }

                if (_run.Buy(index))
                    _table.Sound.Play(Presentation.Sfx.Coin);
                if (_run.Offers.Count == 0)
                {
                    _run.LeaveShop();
                    return;
                }
            }
        }

        // ---------- Находки ----------

        private async UniTask ChooseOfferAsync(CancellationToken ct)
        {
            while (_run.Offers.Count > 0 && !_run.IsShopOpen)
            {
                var options = new List<PerkOption>();
                foreach (var offer in _run.Offers)
                    options.Add(new PerkOption(OfferText(offer), RarityColor(_run.EntryOf(offer))));

                // Дополнительные кнопки: переброс (талант — даром, иначе за рекламу), ещё одна находка за рекламу.
                var extras = new List<string>();
                var actions = new List<int>();
                if (_run.CanRerollFree)
                {
                    extras.Add(T("run.rerollFree", _run.FreeRerolls));
                    actions.Add(0);
                }
                else if (_run.CanRerollForAd && _rewards.CanOffer)
                {
                    extras.Add(T("run.rerollAd"));
                    actions.Add(1);
                }

                if (_run.CanTakeExtra && _rewards.CanOffer)
                {
                    extras.Add(T("run.extraAd"));
                    actions.Add(2);
                }

                var title = _run.Floor <= 1 ? T("run.chooseStartPerk") : T("run.choosePerk");
                var index = await AskAsync(title, Status(), options, extras, ct);
                // После выбора предложения пропадают; если за рекламу взята «ещё одна» — остаются прочие (EndlessRunProgress.Choose).
                if (index >= 0)
                {
                    _run.Choose(index);
                    _table.Sound.Play(Presentation.Sfx.Coin);
                    continue;
                }

                var action = actions[-1 - index];
                if (action == 0)
                {
                    _run.RerollOffers(true);
                }
                else if (action == 1)
                {
                    var outcome = await _rewards.RequestAsync(RerollPlacement, ct);
                    if (outcome.IsGranted())
                        _run.RerollOffers(false);
                }
                else
                {
                    var outcome = await _rewards.RequestAsync(ExtraPerkPlacement, ct);
                    if (outcome.IsGranted())
                        _run.GrantExtraPick();
                }
            }
        }

        private async UniTask<int> AskAsync(string title, string subtitle, List<PerkOption> options, IReadOnlyList<string> extras,
            CancellationToken ct)
        {
            var window = await _ui.OpenAsync<PerkChoiceWindow>(ct, w => w.Setup(title, subtitle, options, extras));
            try
            {
                return await window.WaitChoiceAsync(ct);
            }
            finally
            {
                await _ui.CloseAsync(window, CancellationToken.None);
            }
        }

        private Color RarityColor(RunPerkEntry entry)
        {
            return entry != null ? _palette.RarityColor(entry.Rarity) : _palette.Button;
        }

        /// <summary>Название, тактика, что даёт (с учётом стаков) и сколько уже накоплено.</summary>
        private string OfferText(RunOfferSave offer)
        {
            var entry = _run.EntryOf(offer);
            if (entry == null || entry.Perk == null)
                return offer.PerkId ?? offer.Value;

            var stacks = _run.StacksOf(entry);
            var text = new StringBuilder();
            text.Append("<b>").Append(T(entry.NameKey)).Append("</b>");
            if (entry.Tactic != RunTactic.General)
                text.Append(" <size=70%>· ").Append(T("run.tactic." + entry.Tactic.ToString().ToLowerInvariant())).Append("</size>");
            text.Append("\n<size=75%>").Append(Format(entry.DescriptionKey, entry.Perk.DescriptionArgs(_run.Context, offer.Value, stacks + 1)))
                .Append("</size>");
            if (stacks > 0)
                text.Append("\n<size=70%>").Append(T("run.stacked", stacks)).Append("</size>");
            return text.ToString();
        }

        /// <summary>Текст по ключу: «@ключ» в значениях — тоже текст, дроби — с запятой (кроме английского).</summary>
        private string Format(string key, object[] args)
        {
            var values = new object[Math.Max(args.Length, 4)];
            for (var i = 0; i < values.Length; i++)
            {
                var value = i < args.Length ? args[i] : string.Empty;
                if (value is string s && s.StartsWith("@"))
                    value = T(s.Substring(1));
                else if (value is double d)
                    value = Number(d);
                else if (value is float f)
                    value = Number(f);
                values[i] = value;
            }

            try
            {
                return string.Format(T(key), values);
            }
            catch (FormatException)
            {
                return T(key);
            }
        }

        private string Number(double value)
        {
            var text = value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            return Language == "en" ? text : text.Replace('.', ',');
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
            if (win.Tokens > 0)
                notes.Add(T("run.tokensGained", win.Tokens, _run.Tokens));
            foreach (var milestone in win.Milestones)
                notes.Add(T("run.milestone", milestone));
            return notes;
        }

        /// <summary>Рубеж: награду (монеты, энергию) можно удвоить за рекламу.</summary>
        private async UniTask OfferMilestoneDoubleAsync(RunWin win, CancellationToken ct)
        {
            if (win.MilestoneRewards.Count == 0 || !_run.Config.MilestoneDoubleForAd || !_rewards.CanOffer)
                return;

            var currency = new List<Reward>();
            foreach (var reward in win.MilestoneRewards)
            {
                if (reward is CurrencyReward)
                    currency.Add(reward);
            }

            if (currency.Count == 0 || !await ConfirmWindow.AskAsync(_ui, T("run.milestoneDouble", RewardNames.List(currency, T)), ct))
                return;

            var outcome = await _rewards.RequestAsync(MilestonePlacement, ct);
            if (!outcome.IsGranted())
                return;

            var granted = _granter.Grant(currency);
            _table.Sound.Play(Presentation.Sfx.Coin);
            await Toast.ShowAsync(_kit, _table.UiRoot, RewardNames.Granted(granted, T), UiColors.Gold, 1.2f, ct);
        }

        /// <summary>Сердца кончились: продолжить за рекламу (до MaxRevives раз за забег, со второго — с глубины).</summary>
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
