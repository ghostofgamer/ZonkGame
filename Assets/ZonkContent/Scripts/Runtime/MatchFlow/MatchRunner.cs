using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Ai;
using Zonk.Core.Dice;
using Zonk.Core.Match;
using Zonk.Core.Progress;
using Zonk.Presentation;
using Zonk.Progress;
using Zonk.UI;
using Zonk.UI.Windows;

namespace Zonk.MatchFlow
{
    /// <summary>Итог партии для режима: кто победил, сдался ли местный игрок.</summary>
    public sealed class MatchOutcome
    {
        public ZonkMatch Match;
        public int Winner = -1;
        public bool Surrendered;

        /// <summary>Режим партии для статистики (ID состояния стола: campaign, tower, endless_run); пусто — не задан.</summary>
        public string Mode;

        /// <summary>Сколько секунд шла партия (без загрузки).</summary>
        public float Seconds;
    }

    /// <summary>
    /// Проводит партию: ядро правил (ZonkMatch) решает, контроллеры выбирают, MatchPresenter показывает,
    /// HUD даёт кнопки. Режимы (игра вдвоём, кампания, позже онлайн) только собирают участников и настройки.
    /// </summary>
    public sealed class MatchRunner
    {
        /// <summary>Тег события заданий: игрок бросил кости своей рукой (CustomEventGoal).</summary>
        public const string ManualRollTag = "manual_roll";

        private readonly TableView _table;
        private readonly MatchPresenter _presenter;
        private readonly ReactionDirector _reactions;
        private readonly IChatChannel _chat;
        private readonly UiKit _kit;
        private readonly GameConfig _config;
        private readonly IGameSettings _settings;
        private readonly ContentDatabase _content;
        private readonly IUiService _ui;
        private readonly IQuestService _quests;
        private readonly IDieMastery _mastery;
        private readonly IPlayerRecords _records;
        private readonly ITalents _talents;
        private readonly Zonk.Table.TutorialDirector _tutorial;

        public MatchRunner(TableView table, MatchPresenter presenter, ReactionDirector reactions, IChatChannel chat, UiKit kit,
            GameConfig config, IGameSettings settings, ContentDatabase content, IUiService ui, IQuestService quests,
            IDieMastery mastery, Zonk.Table.TutorialDirector tutorial, IPlayerRecords records,
            [Zenject.InjectOptional] ITalents talents)
        {
            _talents = talents;
            _records = records;
            _tutorial = tutorial;
            _quests = quests;
            _mastery = mastery;
            _table = table;
            _presenter = presenter;
            _reactions = reactions;
            _chat = chat;
            _kit = kit;
            _config = config;
            _settings = settings;
            _content = content;
            _ui = ui;
        }

        public async UniTask<MatchOutcome> RunAsync(MatchSettings settings, IReadOnlyList<MatchParticipant> participants,
            CancellationToken ct)
        {
            var match = new ZonkMatch(settings);
            var outcome = new MatchOutcome { Match = match };
            var startedAt = Time.realtimeSinceStartup;

            var phrases = _content.All<PhraseConfig>();
            phrases.Sort((a, b) => a.Order.CompareTo(b.Order));

            using (var surrenderCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                var token = surrenderCts.Token;
                // Модели стаканов — заранее: в партии они ставятся сразу (префабы грузятся по требованию).
                await _presenter.PreloadCupsAsync(participants, ct);
                var hud = await _ui.OpenAsync<MatchHudWindow>(ct, w => w.Setup(phrases, participants));
                hud.Refresh(match);

                _presenter.PrepareMatch(participants, settings.Seed);
                _reactions.Begin(participants, _table.UiRoot, token);

                var controllers = CreateControllers(participants, hud, settings.Seed);
                var progress = new ProgressTracking(participants);

                void OnPhraseChosen(PhraseConfig phrase)
                {
                    _chat.Send(LocalSpeaker(match, participants), phrase);
                }

                void OnPhraseReceived(int from, PhraseConfig phrase)
                {
                    _reactions.Say(from, _kit.T(phrase.NameKey));
                    _reactions.Fire(from, null, MatchEventType.PhraseReceived);
                }

                void OnSurrender()
                {
                    AskSurrenderAsync(surrenderCts, outcome, token).Forget();
                }

                hud.PhraseChosen += OnPhraseChosen;
                hud.SurrenderRequested += OnSurrender;
                _chat.Received += OnPhraseReceived;

                try
                {
                    _reactions.Fire(-1, MatchEventType.MatchStarted, MatchEventType.MatchStarted);
                    while (match.Phase != MatchPhase.Finished)
                        await PlayTurnAsync(match, controllers, hud, progress, token);

                    outcome.Winner = match.Winner != null ? match.Winner.Index : -1;
                    hud.Refresh(match);
                    _reactions.Fire(outcome.Winner, MatchEventType.Won, MatchEventType.Lost);
                }
                catch (OperationCanceledException) when (outcome.Surrendered && !ct.IsCancellationRequested)
                {
                    // Сдался местный игрок: победа остальным, партия закрывается без исключения.
                    outcome.Winner = LocalSpeaker(match, participants) == 0 ? 1 : 0;
                }
                finally
                {
                    _tutorial.Hide();
                    hud.PhraseChosen -= OnPhraseChosen;
                    hud.SurrenderRequested -= OnSurrender;
                    _chat.Received -= OnPhraseReceived;
                    await _ui.CloseAsync(hud, CancellationToken.None);
                }
            }

            outcome.Seconds = Time.realtimeSinceStartup - startedAt;
            return outcome;
        }

        /// <summary>Подсказка лучшего хода: взят талант, включена в настройках, партия против соперника (не вдвоём).</summary>
        private AiProfile HintProfile(IReadOnlyList<MatchParticipant> participants)
        {
            if (_talents == null || _talents.Value(TalentEffect.BestMoveHint) <= 0f || !_settings.BestMoveHint)
                return null;

            var vsAi = false;
            foreach (var participant in participants)
                vsAi |= participant.Controller == ControllerKind.Ai;
            if (!vsAi)
                return null;

            return _config.HintAi != null ? _config.HintAi.ToProfile() : AiProfile.CreateDefault();
        }

        private List<IPlayerController> CreateControllers(IReadOnlyList<MatchParticipant> participants, MatchHudWindow hud, ulong seed)
        {
            var controllers = new List<IPlayerController>(participants.Count);
            for (var i = 0; i < participants.Count; i++)
            {
                var participant = participants[i];
                var index = i;
                switch (participant.Controller)
                {
                    case ControllerKind.Ai:
                        controllers.Add(new AiPlayerController(participant.AiProfile, participant.AiThinkDelay,
                            new SplitMixRandom(seed * 31 + (ulong)i + 7), _table.Dice, () => _settings.Speed,
                            () => _reactions.Fire(index, MatchEventType.Thinking, null)));
                        break;
                    default:
                        // Remote появится вместе с онлайном; до тех пор все люди играют с этого экрана.
                        controllers.Add(new LocalPlayerController(hud, _table.Dice, _presenter, _kit, HintProfile(participants)));
                        break;
                }
            }

            return controllers;
        }

        private async UniTask PlayTurnAsync(ZonkMatch match, IReadOnlyList<IPlayerController> controllers, MatchHudWindow hud,
            ProgressTracking progress, CancellationToken ct)
        {
            var player = match.CurrentPlayerIndex;
            var controller = controllers[player];
            progress.Turn[player].Reset();
            var local = progress.IsLocal(player);
            if (!local)
                _tutorial.Show(TutorialTrigger.OpponentTurn);
            else if (!_tutorial.WasSeen(TutorialTrigger.BeforeFirstRoll))
                _tutorial.Show(TutorialTrigger.BeforeFirstRoll);
            else if (_presenter.ManualRollEnabled)
                _tutorial.Show(TutorialTrigger.ManualRoll); // со второго хода: можно бросать своей рукой

            hud.Refresh(match);
            await _presenter.BeginTurnAsync(player, ct);
            await controller.WaitRollAsync(match, ct);

            while (true)
            {
                var roll = match.Roll();
                RecordRoll(progress, player, roll);
                await _presenter.PlayRollAsync(roll, ct);
                hud.SetHint(string.Empty);
                if (_presenter.LastRollManual && local && !progress.HotSeat)
                    _quests.Report(QuestEvent.CustomEvent(ManualRollTag));

                // Зонк, но сгорел заряд спасения (находка забега): ход продолжается, те же кости — в стакан и снова бросок.
                if (roll.ZonkSaved)
                {
                    hud.Refresh(match);
                    await hud.ToastAsync(_kit.T("match.zonkSaved", match.Players[player].ZonkSavesLeft), UiColors.Gold, 0.8f, ct);
                    await _presenter.PrepareNextRollAsync(match, ct);
                    await controller.WaitRollAsync(match, ct);
                    continue;
                }

                if (roll.IsZonk)
                {
                    if (local)
                        _tutorial.Show(TutorialTrigger.Zonk);
                    hud.Refresh(match);
                    _reactions.Fire(player, MatchEventType.SelfZonk, MatchEventType.OtherZonk);
                    var toast = hud.ToastAsync(_kit.T("match.zonk"), UiColors.Bad, 0.7f, ct);
                    await _presenter.PlayZonkAsync(player, ct);
                    await toast;
                    if (roll.TurnEnd.Penalty > 0)
                        await hud.ToastAsync(_kit.T("match.penalty", roll.TurnEnd.Penalty), UiColors.Bad, 0.6f, ct);
                    return;
                }

                if (local)
                    _tutorial.Show(TutorialTrigger.ChooseDice);

                var decision = await controller.DecideAsync(match, ct);
                var keep = match.Keep(decision.Keep);
                OnKeep(progress, player, keep);

                // Очки всплывают над костями, пока они ещё лежат в лотке.
                if (keep.Score != null)
                    hud.ShowKeepScore(_presenter.PopupPoint(keep.KeptDice), _presenter.TableCamera, keep.Score.Score,
                        ComboNames.KeyFor(keep.Score), keep.HotDice || keep.Score.Score >= _config.BigKeepScore);
                if (local)
                    _tutorial.Show(keep.HotDice ? TutorialTrigger.HotDice : TutorialTrigger.AfterKeep);
                hud.Refresh(match);
                await _presenter.PlayKeepAsync(keep, ct);

                // Крупная комбинация: соперник может в сердцах ударить по столу.
                if (!keep.HotDice && keep.Score.Score >= _config.BigKeepScore)
                    _reactions.Fire(player, MatchEventType.SelfBigKeep, MatchEventType.OtherBigKeep);

                if (keep.HotDice)
                {
                    _reactions.Fire(player, MatchEventType.SelfHotDice, MatchEventType.OtherHotDice);
                    await hud.ToastAsync(_kit.T("match.hotDice"), UiColors.Gold, 0.5f, ct);
                }

                if ((decision.Bank && match.CanBank) || !match.CanRollAgain)
                {
                    if (!match.CanBank)
                        throw new InvalidOperationException("No dice to roll and banking is not allowed");

                    var end = match.Bank();
                    OnBank(progress, player, end.Banked);
                    if (local)
                        _tutorial.Show(TutorialTrigger.Bank);
                    if (end.StartedFinalRound)
                        _tutorial.Show(TutorialTrigger.FinalRound);
                    hud.Refresh(match);
                    if (end.Banked >= _config.BigBankScore)
                        _reactions.Fire(player, MatchEventType.SelfBigBank, MatchEventType.OtherBigBank);

                    var toast = hud.ToastAsync("+" + end.Banked, UiColors.Gold, 0.5f, ct);
                    await _presenter.PlayBankAsync(player, ct);
                    await toast;

                    if (end.StartedFinalRound && !end.MatchFinished)
                        await hud.ToastAsync(_kit.T("match.finalRoundStarted", match.Players[player].Name), UiColors.Gold, 0.9f, ct);
                    return;
                }

                if (match.DiceInHandCount <= 2 && match.TurnScore >= _config.RiskyRollScore)
                    _reactions.Fire(player, MatchEventType.SelfRiskyRoll, null);

                // «Продолжить»: оставшиеся кости — в стакан, камера — в ракурс броска, затем бросок как в начале хода:
                // кнопкой или своей рукой.
                await _presenter.PrepareNextRollAsync(match, ct);
                await controller.WaitRollAsync(match, ct);
            }
        }

        /// <summary>
        /// Прогресс игрока за партию: задания получают ходы людей, мастерство костей — только партии против соперников
        /// (в игре вдвоём с самим собой очки копить нельзя).
        /// </summary>
        private sealed class ProgressTracking
        {
            public ProgressTracking(IReadOnlyList<MatchParticipant> participants)
            {
                Participants = participants;
                Turn = new TurnDiceScore[participants.Count];
                for (var i = 0; i < Turn.Length; i++)
                    Turn[i] = new TurnDiceScore();

                HotSeat = true;
                foreach (var participant in participants)
                    HotSeat &= participant.Controller != ControllerKind.Ai;
            }

            public IReadOnlyList<MatchParticipant> Participants { get; }
            public TurnDiceScore[] Turn { get; }
            public bool HotSeat { get; }

            public bool IsLocal(int player) => Participants[player].Controller == ControllerKind.Local;
        }

        private void OnKeep(ProgressTracking progress, int player, KeepOutcome keep)
        {
            progress.Turn[player].AddKeep(keep);
            if (!progress.IsLocal(player))
                return;

            _quests.Report(new QuestEvent { Kind = QuestEventKind.Keep, Score = keep.Score, HotSeat = progress.HotSeat });
            if (!progress.HotSeat)
                _records.RecordKeep(keep.Score, keep.HotDice);
            if (keep.HotDice)
                _quests.Report(new QuestEvent { Kind = QuestEventKind.HotDice, HotSeat = progress.HotSeat });
        }

        private void OnBank(ProgressTracking progress, int player, int banked)
        {
            var points = progress.Turn[player].Commit();
            if (!progress.IsLocal(player))
                return;

            _quests.Report(new QuestEvent { Kind = QuestEventKind.Bank, Amount = banked, HotSeat = progress.HotSeat });
            if (progress.HotSeat)
                return;

            _records.RecordBank(banked);

            var dice = progress.Participants[player].Dice;
            for (var slot = 0; slot < points.Length && dice != null && slot < dice.Count; slot++)
            {
                // Гружёные кости забега мастерства не копят: их нет в коллекции игрока.
                if (dice[slot] != null && dice[slot].RunOnly)
                    continue;
                _mastery.AddPoints(dice[slot], points[slot]);
            }
        }

        private async UniTaskVoid AskSurrenderAsync(CancellationTokenSource surrender, MatchOutcome outcome, CancellationToken ct)
        {
            bool confirmed;
            try
            {
                confirmed = await ConfirmWindow.AskAsync(_ui, _kit.T("match.surrenderConfirm"), ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!confirmed)
                return;

            outcome.Surrendered = true;
            surrender.Cancel();
        }

        /// <summary>Бросок местного игрока в статистику: сколько костей брошено, сколько из них обычных, единицы и пятёрки на обычных.</summary>
        private void RecordRoll(ProgressTracking progress, int player, RollOutcome roll)
        {
            if (!progress.IsLocal(player) || progress.HotSeat)
                return;

            var dice = progress.Participants[player].Dice;
            var rolled = roll.RolledDice;
            var standard = 0;
            var onesFives = 0;
            for (var i = 0; i < rolled.Count; i++)
            {
                var slot = rolled[i];
                var die = dice != null && slot < dice.Count ? dice[slot] : null;
                if (die != null && die.IsSpecial)
                    continue;

                standard++;
                var face = roll.Faces[slot];
                if (face == 1 || face == 5)
                    onesFives++;
            }

            _records.RecordRoll(roll.IsZonk, rolled.Count, standard, onesFives);
        }

        /// <summary>Кто говорит фразу с этого экрана: текущий игрок, если он местный, иначе первый местный.</summary>
        private static int LocalSpeaker(ZonkMatch match, IReadOnlyList<MatchParticipant> participants)
        {
            if (match.Phase != MatchPhase.Finished && participants[match.CurrentPlayerIndex].Controller == ControllerKind.Local)
                return match.CurrentPlayerIndex;

            for (var i = 0; i < participants.Count; i++)
            {
                if (participants[i].Controller == ControllerKind.Local)
                    return i;
            }

            return 0;
        }
    }
}
