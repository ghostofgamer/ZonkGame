using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;
using Zonk.Presentation;
using Zonk.UI;

namespace Zonk.MatchFlow
{
    /// <summary>
    /// Реакции соперников на события партии: жест аватара и реплика в облачке. Берутся из ReactionSetConfig
    /// соперника. Не задерживают партию: играются параллельно, одна реакция за раз.
    /// </summary>
    public sealed class ReactionDirector
    {
        private readonly TableView _table;
        private readonly UiKit _kit;
        private readonly System.Random _random = new System.Random();

        private IReadOnlyList<MatchParticipant> _participants;
        private RectTransform _canvas;
        private CancellationToken _ct;
        private bool _busy;

        public ReactionDirector(TableView table, UiKit kit)
        {
            _table = table;
            _kit = kit;
        }

        public void Begin(IReadOnlyList<MatchParticipant> participants, RectTransform canvas, CancellationToken ct)
        {
            _participants = participants;
            _canvas = canvas;
            _ct = ct;
            _busy = false;
        }

        /// <summary>
        /// Событие с участником actor. Соперник-ИИ с индексом actor реагирует на selfEvent, остальные на otherEvent.
        /// null = на это событие этой стороне реагировать не нужно.
        /// </summary>
        public void Fire(int actor, MatchEventType? selfEvent, MatchEventType? otherEvent)
        {
            if (_participants == null || _busy)
                return;

            for (var i = 0; i < _participants.Count; i++)
            {
                var opponent = _participants[i].Opponent;
                if (opponent == null || opponent.Reactions == null)
                    continue;

                var type = i == actor ? selfEvent : otherEvent;
                if (!type.HasValue)
                    continue;

                var entry = Find(opponent.Reactions, type.Value);
                if (entry == null || _random.NextDouble() > entry.Chance)
                    continue;

                PlayAsync(i, entry).Forget();
                return;
            }
        }

        /// <summary>Реплика игрока (быстрая фраза): облачко над его местом.</summary>
        public void Say(int player, string text)
        {
            if (_canvas == null)
                return;

            SpeechBubble.ShowAsync(_kit, _canvas, _table.Camera.Camera, _table.SeatOf(player).BubbleAnchor, text, 2.2f, _ct)
                .SuppressCancellationThrow().Forget();
        }

        private async UniTaskVoid PlayAsync(int participant, ReactionEntry entry)
        {
            _busy = true;
            try
            {
                if (entry.LineKeys != null && entry.LineKeys.Count > 0)
                    Say(participant, _kit.T(entry.LineKeys[_random.Next(entry.LineKeys.Count)]));

                if (entry.Gesture != AvatarGesture.None)
                    await _table.Opponent.PlayAsync(entry.Gesture, _ct);
                else
                    await UniTask.Delay(TimeSpan.FromSeconds(1.2f), cancellationToken: _ct);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>Случайная реакция на событие: два прохода по списку вместо нового списка на каждое событие партии.</summary>
        private static ReactionEntry Find(ReactionSetConfig reactions, MatchEventType type)
        {
            var count = 0;
            foreach (var entry in reactions.Entries)
            {
                if (entry != null && entry.Event == type)
                    count++;
            }

            if (count == 0)
                return null;

            var pick = UnityEngine.Random.Range(0, count);
            foreach (var entry in reactions.Entries)
            {
                if (entry != null && entry.Event == type && pick-- == 0)
                    return entry;
            }

            return null;
        }
    }

    /// <summary>
    /// Канал быстрых фраз. Сейчас локальный: фраза сразу видна на этом экране. Для онлайна появится
    /// сетевая реализация с тем же интерфейсом.
    /// </summary>
    public interface IChatChannel
    {
        void Send(int fromPlayer, PhraseConfig phrase);
        event Action<int, PhraseConfig> Received;
    }

    public sealed class LocalChatChannel : IChatChannel
    {
        public event Action<int, PhraseConfig> Received;

        public void Send(int fromPlayer, PhraseConfig phrase)
        {
            Received?.Invoke(fromPlayer, phrase);
        }
    }
}
