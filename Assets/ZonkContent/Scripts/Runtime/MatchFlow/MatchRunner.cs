using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Dice;
using Zonk.Core.Match;
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
    }

    /// <summary>
    /// Проводит партию: ядро правил (ZonkMatch) решает, контроллеры выбирают, MatchPresenter показывает,
    /// HUD даёт кнопки. Режимы (игра вдвоём, кампания, позже онлайн) только собирают участников и настройки.
    /// </summary>
    public sealed class MatchRunner
    {
        private readonly TableView _table;
        private readonly MatchPresenter _presenter;
        private readonly ReactionDirector _reactions;
        private readonly IChatChannel _chat;
        private readonly UiKit _kit;
        private readonly GameConfig _config;
        private readonly IGameSettings _settings;
        private readonly ContentDatabase _content;
        private readonly IUiService _ui;

        public MatchRunner(TableView table, MatchPresenter presenter, ReactionDirector reactions, IChatChannel chat, UiKit kit,
            GameConfig config, IGameSettings settings, ContentDatabase content, IUiService ui)
        {
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

            var phrases = _content.All<PhraseConfig>();
            phrases.Sort((a, b) => a.Order.CompareTo(b.Order));

            using (var surrenderCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                var token = surrenderCts.Token;
                var hud = await _ui.OpenAsync<MatchHudWindow>(ct, w => w.Setup(phrases, participants));
                hud.Refresh(match);

                _presenter.PrepareMatch(participants, settings.Seed);
                _reactions.Begin(participants, _table.UiRoot, token);

                var controllers = CreateControllers(participants, hud, settings.Seed);

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
                        await PlayTurnAsync(match, controllers, hud, token);

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
                    hud.PhraseChosen -= OnPhraseChosen;
                    hud.SurrenderRequested -= OnSurrender;
                    _chat.Received -= OnPhraseReceived;
                    await _ui.CloseAsync(hud, CancellationToken.None);
                }
            }

            return outcome;
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
                        controllers.Add(new LocalPlayerController(hud, _table.Dice, _presenter, _kit));
                        break;
                }
            }

            return controllers;
        }

        private async UniTask PlayTurnAsync(ZonkMatch match, IReadOnlyList<IPlayerController> controllers, MatchHudWindow hud,
            CancellationToken ct)
        {
            var player = match.CurrentPlayerIndex;
            var controller = controllers[player];

            hud.Refresh(match);
            await _presenter.BeginTurnAsync(player, ct);
            await controller.WaitRollAsync(match, ct);

            while (true)
            {
                var roll = match.Roll();
                await _presenter.PlayRollAsync(roll, ct);

                if (roll.IsZonk)
                {
                    hud.Refresh(match);
                    _reactions.Fire(player, MatchEventType.SelfZonk, MatchEventType.OtherZonk);
                    var toast = hud.ToastAsync(_kit.T("match.zonk"), UiColors.Bad, 0.7f, ct);
                    await _presenter.PlayZonkAsync(player, ct);
                    await toast;
                    if (roll.TurnEnd.Penalty > 0)
                        await hud.ToastAsync(_kit.T("match.penalty", roll.TurnEnd.Penalty), UiColors.Bad, 0.6f, ct);
                    return;
                }

                var decision = await controller.DecideAsync(match, ct);
                var keep = match.Keep(decision.Keep);
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
