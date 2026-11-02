using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;
using Zonk.MatchFlow;
using Zonk.Utils;

namespace Zonk.Presentation
{
    /// <summary>
    /// Игра вдвоём на одном устройстве: ближнее место (юг, у камеры) всегда у того, чей ход. Камера и ракурсы
    /// настроены на ближнюю сторону, поэтому меняется не камера, а места. Стакан и рука (её вид — скин, позже часы
    /// и цепочки) принадлежат игроку, а не месту. Сопоставление «игрок → место» — TableView.NearPlayer: за ним идут
    /// стакан, рука, ряд отложенных костей и облачка реплик.
    ///
    /// Смена хода: стаканы игроков по дуге меняются местами, а ближняя рука уходит из кадра и возвращается уже рукой
    /// нового игрока; дальняя рука тоже получает вид своего игрока.
    /// </summary>
    public sealed partial class MatchPresenter
    {
        private const float SwapHeight = 0.9f;

        /// <summary>В партии только люди за этим экраном: места меняются каждый ход.</summary>
        private bool _swapSeats;

        private static bool IsHotSeat(IReadOnlyList<MatchParticipant> participants)
        {
            if (participants == null || participants.Count != 2)
                return false;

            foreach (var participant in participants)
            {
                if (participant.Controller != ControllerKind.Local)
                    return false;
            }

            return true;
        }

        private readonly List<PrefabPayload> _heldCups = new List<PrefabPayload>();

        /// <summary>
        /// Загрузить модели стаканов и рукавов игроков до партии и держать до её конца: в партии они пересоздаются
        /// на местах (смена мест в игре вдвоём) и должны ставиться сразу, без ожидания загрузки.
        /// </summary>
        public async UniTask PreloadCupsAsync(IReadOnlyList<MatchParticipant> participants, CancellationToken ct)
        {
            ReleaseCups();
            for (var i = 0; i < participants.Count && i < 2; i++)
            {
                await HoldModelAsync(participants[i].Cup, ct);
                await HoldModelAsync(participants[i].HandSleeve, ct);
                await HoldModelAsync(participants[i].HandWrist, ct);
                await HoldModelAsync(participants[i].HandLook, ct);
            }
        }

        /// <summary>Рука в облике участника: кожа, кисть, рукав, запястье.</summary>
        private static void Dress(HandView hand, MatchParticipant participant)
        {
            hand.SetLook(participant.HandSkin, participant.HandLook, participant.HandSleeve, participant.HandWrist);
        }

        private async UniTask HoldModelAsync(CosmeticItemConfig item, CancellationToken ct)
        {
            if (item == null)
                return;

            var model = item.Payload is PrefabPayload own && own.HasModel ? own
                : item.Slot != null && item.Slot.DefaultItem != null ? item.Slot.DefaultItem.Payload as PrefabPayload : null;
            if (model == null || _heldCups.Contains(model))
                return;

            if (await _assets.AcquireAsync(model, ct) != null)
                _heldCups.Add(model);
        }

        private void ReleaseCups()
        {
            foreach (var model in _heldCups)
                _assets.Release(model);
            _heldCups.Clear();
        }

        /// <summary>Стаканы и руки игроков — на их текущих местах, стаканы на столе.</summary>
        private void ShowCups()
        {
            if (_participants == null)
                return;

            for (var i = 0; i < _participants.Count && i < 2; i++)
            {
                var seat = _table.SeatOf(i);
                var cup = _participants[i].Cup;
                if (cup != null)
                    seat.CupAnchor.Show(cup, cup.Slot != null ? cup.Slot.DefaultItem : null);

                CupOf(seat).ReturnHome();
                Dress(seat.Hand, _participants[i]);
            }
        }

        /// <summary>Ход перешёл к другому игроку на этом же устройстве: к камере садится он — со своими стаканом и рукой.</summary>
        private async UniTask SwapSeatsAsync(int player, CancellationToken ct)
        {
            var feel = Feel;
            var duration = Mathf.Max(0.1f, feel.SeatSwapDuration) / Speed;
            var near = CupOf(_table.South).transform;
            var far = CupOf(_table.North).transform;
            var nearHome = near.position;
            var farHome = far.position;

            try
            {
                _table.Sound.Play(Sfx.Whoosh, 0.5f, 0.1f);
                await UniTask.WhenAll(
                    Animate.JumpAsync(near, farHome, SwapHeight, duration, ct),
                    Animate.JumpAsync(far, nearHome, SwapHeight * 0.7f, duration, ct),
                    SwapHandsAsync(player, duration, ct));

                // Приземление: стук и облачко пыли под стаканами.
                _table.Sound.Play(Sfx.DiceKeep, 0.7f);
                var dust = EffectSystem(ref _dust, feel.DustPrefab);
                Emit(dust, nearHome, feel.SeatSwapDust);
                Emit(dust, farHome, feel.SeatSwapDust);
            }
            finally
            {
                // Стаканы пересоздаются на своих новых местах: с тем же видом, поэтому подмена незаметна.
                _table.NearPlayer = player;
                ShowCups();
            }
        }

        /// <summary>
        /// Обе руки уходят от стола (ближняя — из кадра к игроку, дальняя — за свой край стола) и возвращаются уже
        /// руками других игроков: ближняя — рукой ходящего (его скин), дальняя — рукой ожидающего.
        /// </summary>
        private async UniTask SwapHandsAsync(int player, float duration, CancellationToken ct)
        {
            var near = _table.South.Hand;
            var far = _table.North.Hand;
            var half = duration * 0.5f;

            await UniTask.WhenAll(
                Animate.MoveAsync(near.transform, AwayFromTable(_table.South), half, ct, AnimateEase.InCubic),
                Animate.MoveAsync(far.transform, AwayFromTable(_table.North), half, ct, AnimateEase.InCubic));

            if (_participants != null && _participants.Count == 2)
            {
                Dress(near, _participants[player]);
                Dress(far, _participants[1 - player]);
            }

            await UniTask.WhenAll(near.ReturnAsync(half, ct), far.ReturnAsync(half, ct));
        }

        /// <summary>Куда рука уходит при смене: от стола к своему игроку и вниз.</summary>
        private Vector3 AwayFromTable(SeatView seat)
        {
            return seat.Hand.transform.position + FlatBack(seat) * 1.4f + Vector3.down * 0.4f;
        }

        /// <summary>Направление «от стола к игроку» на этом месте, горизонтально.</summary>
        private Vector3 FlatBack(SeatView seat)
        {
            var back = seat.Hand.transform.position - _table.Tray.Center;
            back.y = 0f;
            return back.sqrMagnitude > 0.0001f ? back.normalized : Vector3.back;
        }
    }
}
