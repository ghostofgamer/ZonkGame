using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Match;
using Zonk.MatchFlow;
using Zonk.Progress;
using Zonk.Utils;
using Base.Services.Haptics;

namespace Zonk.Presentation
{
    /// <summary>
    /// Постановка партии на столе: рука берёт стакан, трясёт, высыпает кости, кости раскатываются по записи
    /// физики и ложатся рядом, отложенные уезжают к игроку. Все шаги — UniTask, их ждёт MatchRunner.
    /// Скорость анимаций берётся из настроек игрока.
    /// </summary>
    public sealed partial class MatchPresenter : IDisposable
    {
        private const float ClackCooldown = 0.05f;

        private readonly TableView _table;
        private readonly GameConfig _config;
        private readonly IGameSettings _settings;
        private readonly DiceRollSimulator _simulator;
        private readonly List<int> _keptOrder = new List<int>();

        // Рабочие списки броска: переиспользуются, чтобы бросок не оставлял мусора для сборщика.
        private readonly List<int> _onTable = new List<int>();
        private readonly List<int> _visibleDice = new List<int>();
        private readonly List<int> _settleOrder = new List<int>();
        private readonly List<Vector3> _placed = new List<Vector3>();
        private readonly bool[] _usefulDice = new bool[ZonkMatch.DiceCount];
        private static readonly List<Renderer> CupRenderers = new List<Renderer>();
        private Comparison<int> _byHeight;

        // Анимации, которые ждутся вместе. UniTask.WhenAll копирует список сразу при вызове, поэтому один буфер
        // годится для всех шагов партии, даже если одна анимация ещё идёт, когда начинается другая.
        private readonly List<UniTask> _moves = new List<UniTask>();

        private IReadOnlyList<MatchParticipant> _participants;
        private System.Random _visualRandom;
        private Quaternion _uprightHand = Quaternion.identity;
        private Vector3 _gripLocal;
        private Vector3 _gripOffset;
        private float _gripHeight = 0.4f;

        private readonly CosmeticAssets _assets;

        public MatchPresenter(TableView table, GameConfig config, IGameSettings settings, IHapticsService haptics,
            CosmeticAssets assets)
        {
            _table = table;
            _assets = assets;
            _config = config;
            _settings = settings;
            _haptics = haptics;
            _simulator = new DiceRollSimulator(table.Tray);

            // Размер костей на столе — из конфига: модель одна, масштаб и физика подстраиваются.
            var dieSize = config.DieSize > 0f ? config.DieSize : table.Tray.DieSize;
            table.Tray.SetDieSize(dieSize);
            table.Dice.SetDieSize(dieSize);
            _table.Opponent.TableImpact += OnTableImpact;
        }

        public void Dispose()
        {
            _table.Opponent.TableImpact -= OnTableImpact;
            StopCupDice(false);
            ReleaseCups();
            _simulator.Dispose();
        }

        private float Speed => Mathf.Max(1, _settings.Speed);
        private DiceSetView Dice => _table.Dice;

        public void PrepareMatch(IReadOnlyList<MatchParticipant> participants, ulong seed)
        {
            _participants = participants;
            _visualRandom = new System.Random(unchecked((int)seed));
            Dice.DisableSelection();
            Dice.ClearSelection(false);
            Dice.SetVisible(false);

            // Первый игрок — у камеры. В игре вдвоём места потом меняются каждый ход (SwapSeatsAsync).
            _table.NearPlayer = 0;
            _swapSeats = IsHotSeat(participants);

            // Стакан мог остаться не на месте после прерванного показа: перед партией он всегда на столе.
            ShowCups();

            var north = participants.Count > 1 ? participants[1] : null;

            _table.South.Hand.SnapToRest();
            _table.North.Hand.SnapToRest();

            if (north != null && north.Opponent != null)
                _table.Opponent.Show(north.Opponent);
            else
                _table.Opponent.Hide();
        }

        public void EndMatch()
        {
            AbortManual();
            StopCupDice(true);

            // После игры вдвоём у камеры мог остаться второй игрок: меню и магазин показывают стакан первого.
            if (_table.NearPlayer != 0)
            {
                _table.NearPlayer = 0;
                ShowCups();
            }

            ReleaseCups();
            _swapSeats = false;
            Dice.DisableSelection();
            Dice.ClearSelection(false);
            Dice.SetVisible(false);
            _table.Opponent.Hide();
            _table.South.Hand.SnapToRest();
            _table.North.Hand.SnapToRest();
        }

        public async UniTask BeginTurnAsync(int player, CancellationToken ct)
        {
            AbortManual();
            StopCupDice(true);
            SetCupContentsAll();
            var participant = _participants[player];
            Dice.SetSkin(participant.DiceSkin);
            Dice.SetLoadout(participant.Dice, participant.MasteryLevels, _config.MasteryLevels);
            Dice.SetVisible(false);
            _keptOrder.Clear();
            for (var slot = 0; slot < Dice.Dice.Count; slot++)
            {
                Dice[slot].ClearFace();
                Dice[slot].SetTint(Color.white);
            }

            await _table.Camera.MoveToAsync(CameraShots.Match, 0.5f / Speed, ct);

            // Игра вдвоём: ходящий садится к камере — стаканы меняются местами на глазах у игроков.
            if (_swapSeats && _table.NearPlayer != player)
                await SwapSeatsAsync(player, ct);
        }

        /// <summary>
        /// Игрок решил продолжить ход: оставшиеся в лотке кости запрыгивают обратно в стакан, камера переходит
        /// в ракурс броска. Дальше — как в начале хода: «Бросить» или стакан своей рукой.
        /// </summary>
        public async UniTask PrepareNextRollAsync(ZonkMatch match, CancellationToken ct)
        {
            var seat = _table.SeatOf(match.CurrentPlayerIndex);
            SetCupContents(match);
            var onTable = _onTable;
            onTable.Clear();
            for (var slot = 0; slot < Dice.Dice.Count; slot++)
            {
                if (match.IsInHand(slot) && Dice[slot].gameObject.activeSelf)
                    onTable.Add(slot);
            }

            var camera = _table.Camera.MoveToAsync(CameraShots.Match, 0.45f / Speed, ct);
            if (onTable.Count > 0)
            {
                _table.Sound.Play(Sfx.DiceKeep, 0.6f);
                await HideDiceAsync(onTable, CupOf(seat).Mouth.position, ct);
            }

            await camera;
        }

        public async UniTask PlayRollAsync(RollOutcome roll, CancellationToken ct)
        {
            var seat = _table.SeatOf(roll.Player);
            var cup = CupOf(seat);
            var hand = seat.Hand;
            var cupTransform = cup.transform;
            var cupParent = cup.HomeParent;
            var cupLocalPosition = cup.HomeLocalPosition;
            var cupLocalRotation = cup.HomeLocalRotation;

            // Бросок своей рукой: стакан уже в руке игрока, камера остаётся там, где он его взял.
            var manual = _manualActive && _manualPlayer == roll.Player;
            LastRollManual = manual;
            var cameraMove = manual ? UniTask.CompletedTask : _table.Camera.MoveToAsync(CameraShots.Match, 0.4f / Speed, ct);

            // Стиль броска участника: у соперника может быть свой. Значения случайны в пределах стиля, броски не повторяются.
            var styleConfig = PickStyle(_participants[roll.Player]);
            var style = styleConfig.Pick(_visualRandom);

            try
            {
                // Повторный бросок: неотложенные кости со стола сначала возвращаются в стакан.
                var onTable = _onTable;
                onTable.Clear();
                for (var i = 0; i < roll.RolledDice.Count; i++)
                {
                    var slot = roll.RolledDice[i];
                    if (Dice[slot].gameObject.activeSelf && !IsInCup(slot))
                        onTable.Add(slot);
                }

                if (onTable.Count > 0)
                {
                    _table.Sound.Play(Sfx.DiceKeep, 0.6f);
                    await HideDiceAsync(onTable, cup.Mouth.position, ct);
                }

                Vector3 direction;
                if (manual)
                {
                    // Игрок трясёт стакан сам: ждём, пока отпустит, и бросаем в сторону толчка.
                    direction = await ManualThrowAsync(hand, style, ct);
                }
                else
                {
                    // Рука берёт стакан и несёт его к краю лотка.
                    SetCupContents(roll.RolledDice);
                    await GrabCupAsync(hand, cup, 0.25f / Speed, ct);
                    StartCupDice(cup);
                    await MoveCupToAsync(hand, cup, seat.ShakePoint.position, 0.3f / Speed, ct);
                    await cameraMove;

                    _table.Sound.Play(Sfx.DiceRattle, 0.8f);
                    await hand.ShakeAsync(style.ShakeDuration / Speed, style.ShakeAmplitude, style.ShakeFrequency, style.ShakeTilt,
                        (float)_visualRandom.NextDouble() * 10f, ct);

                    // Замах к себе, пауза, рывок вперёд с опрокидыванием: кости вылетают в конце рывка.
                    direction = await WindUpAndSwingAsync(hand, seat, style, Speed, ct);
                }

                // Кости из стакана переходят в запись броска: вылетают из горла.
                StopCupDice(false);

                // Кости вылетают из горла стакана. Свой стакан игрок мог увести к краю: тогда кости всё равно внутри лотка.
                var origin = manual ? ClampToTray(cup.Mouth.position, _table.Tray.DieSize) : cup.Mouth.position;
                var recording = _simulator.Simulate(roll.RolledDice.Count, origin, direction, _visualRandom, style);
                for (var i = 0; i < roll.RolledDice.Count; i++)
                {
                    var die = Dice[roll.RolledDice[i]];
                    die.SetFace(roll.Faces[die.Slot], DieFaces.Correction(roll.Faces[die.Slot], recording.UpAxes[i]));
                    die.SetTint(Color.white);
                    die.transform.SetPositionAndRotation(recording.Positions[i][0], recording.Rotations[i][0]);
                    die.SetVisible(true);
                }

                var returnCup = ReturnCupAsync(hand, cupTransform, cupParent, cupLocalPosition, cupLocalRotation,
                    direction * style.FollowThrough, ct);
                await PlaybackAsync(recording, roll.RolledDice, IsLocal(roll.Player), ct);
                await returnCup;
            }
            finally
            {
                _manualActive = false;
                StopCupDice(true);

                // Партию могли прервать посреди броска: стакан возвращается на место, рука в исходную позу.
                if (cup != null && !cup.IsHome)
                {
                    cup.ReturnHome();
                    hand.SnapToRest();
                }
            }

            await SettleAsync(roll, ct);
        }

        /// <summary>Доля высоты стакана, на которой ладонь обхватывает его.</summary>
        private const float GripHeightFraction = 0.45f;

        /// <summary>Половина толщины ладони: ладонь касается стенки стакана, а не входит в неё.</summary>
        private const float PalmOffset = 0.08f;

        /// <summary>
        /// Рука подходит к стакану сбоку, со своей стороны, и обхватывает его: ладонь прижата к стенке и смотрит
        /// на стакан, пальцы идут вдоль окружности. Размер стакана берётся из его модели, поэтому хват подходит
        /// к любому скину стакана.
        /// </summary>
        private async UniTask GrabCupAsync(HandView hand, CupView cup, float duration, CancellationToken ct)
        {
            var bounds = CupBounds(cup);
            var cupTransform = cup.transform;
            var radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            var center = new Vector3(cupTransform.position.x, bounds.min.y + bounds.size.y * GripHeightFraction,
                cupTransform.position.z);

            var away = hand.transform.position - center;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : -cupTransform.forward;
            // Правая рука: пальцы идут по окружности вправо, левая — влево.
            var tangent = hand.RightHanded ? Vector3.Cross(away, Vector3.up) : Vector3.Cross(Vector3.up, away);

            // Локальный верх руки смотрит от стакана: ладонь (низ руки) прижата к стенке, пальцы (вперёд) по касательной.
            var rotation = Quaternion.LookRotation(tangent, away);
            var position = center + away * (radius + PalmOffset);
            await hand.MoveToAsync(position, rotation, duration, ct);

            _gripLocal = cupTransform.InverseTransformPoint(center);
            _gripOffset = position - center;
            _gripHeight = center.y - cupTransform.position.y;
            _uprightHand = rotation;
            cupTransform.SetParent(hand.Grip, true);
        }

        /// <summary>Перенести взятый стакан так, чтобы центр хвата оказался в точке target. Поворот руки не меняется.</summary>
        private UniTask MoveCupToAsync(HandView hand, CupView cup, Vector3 target, float duration, CancellationToken ct)
        {
            var delta = target - cup.transform.TransformPoint(_gripLocal);
            return hand.MoveToAsync(hand.transform.position + delta, hand.transform.rotation, duration, ct);
        }

        private static Bounds CupBounds(CupView cup)
        {
            var renderers = CupRenderers;
            cup.GetComponentsInChildren(renderers);
            if (renderers.Count == 0)
                return new Bounds(cup.transform.position + Vector3.up * 0.45f, new Vector3(0.6f, 0.9f, 0.6f));

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Count; i++)
                bounds.Encapsulate(renderers[i].bounds);
            renderers.Clear();
            return bounds;
        }

        /// <summary>
        /// Замах и бросок как в казино: стакан тянется к себе и чуть вверх, горлышко отклоняется назад, короткая пауза,
        /// затем рывок вперёд с ускорением, на ходу стакан опрокидывается к центру лотка. Возвращает направление броска.
        /// </summary>
        private async UniTask<Vector3> WindUpAndSwingAsync(HandView hand, SeatView seat, RollParams style, float speed,
            CancellationToken ct)
        {
            var direction = _table.Tray.Center - seat.ShakePoint.position;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            var axis = Vector3.Cross(Vector3.up, direction);

            var start = hand.transform.position;
            var baseRotation = hand.transform.rotation;
            _uprightHand = baseRotation;

            var back = start - direction * style.WindUpDistance + Vector3.up * style.WindUpLift;
            var backRotation = Quaternion.AngleAxis(-style.WindUpTilt, axis) * baseRotation;
            await Animate.PoseAsync(hand.transform, back, backRotation, style.WindUpDuration / speed, ct, AnimateEase.OutCubic);
            await UniTask.Delay(TimeSpan.FromSeconds(style.WindUpHold / speed), cancellationToken: ct);

            _table.Sound.Play(Sfx.Whoosh, 0.7f, 0.15f);
            var release = start + direction * (style.FollowThrough * 0.4f);
            var pour = Quaternion.AngleAxis(style.PourAngle, axis) * baseRotation;
            await Animate.PoseAsync(hand.transform, release, pour, style.SwingDuration / speed, ct, AnimateEase.InCubic);
            return direction;
        }

        private async UniTask ReturnCupAsync(HandView hand, Transform cup, Transform parent, Vector3 localPosition,
            Quaternion localRotation, Vector3 followThrough, CancellationToken ct)
        {
            // Доводка: рука по инерции проходит дальше точки броска, затем стакан выпрямляется и едет на место.
            await Animate.MoveAsync(hand.transform, hand.transform.position + followThrough, 0.15f / Speed, ct, AnimateEase.OutCubic);
            // Рука несёт стакан обратно тем же хватом: центр хвата над местом стакана, ладонь сбоку.
            var restWorld = parent.TransformPoint(localPosition) + Vector3.up * _gripHeight + _gripOffset;
            await hand.MoveToAsync(restWorld, _uprightHand, 0.35f / Speed, ct);
            cup.SetParent(parent, true);
            await UniTask.WhenAll(
                Animate.PoseAsync(cup, parent.TransformPoint(localPosition), parent.rotation * localRotation, 0.15f / Speed, ct),
                hand.ReturnAsync(0.3f / Speed, ct));
            cup.localPosition = localPosition;
            cup.localRotation = localRotation;
        }

        private async UniTask PlaybackAsync(RollRecording recording, IReadOnlyList<int> slots, bool local, CancellationToken ct)
        {
            var time = 0f;
            var lastClack = -1f;
            var duration = recording.Duration;
            var previousFrame = 0;
            var feel = Feel;
            SlowMoWindow(recording, slots.Count, out var slowFrom, out var slowTo);

            while (time < duration)
            {
                var framePosition = time / recording.FrameTime;
                var frame = Mathf.Min((int)framePosition, recording.FrameCount - 1);
                var next = Mathf.Min(frame + 1, recording.FrameCount - 1);
                var t = framePosition - frame;

                for (var i = 0; i < slots.Count; i++)
                {
                    var position = Vector3.Lerp(recording.Positions[i][frame], recording.Positions[i][next], t);
                    var rotation = Quaternion.Slerp(recording.Rotations[i][frame], recording.Rotations[i][next], t);
                    Dice[slots[i]].transform.SetPositionAndRotation(position, rotation);

                    if (frame > previousFrame && IsImpact(recording, i, frame))
                    {
                        // Удар о стол: стук, пыль по силе удара, у своего броска — лёгкая вибрация.
                        if (time - lastClack > ClackCooldown)
                        {
                            _table.Sound.Play(Sfx.DiceClack, 0.5f, 0.2f);
                            lastClack = time;
                            if (local)
                                Haptic(feel.HapticDiceLand);
                        }

                        DustAt(position, FallSpeed(recording, i, frame));
                    }
                }

                previousFrame = frame;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                var slow = time >= slowFrom && time < slowTo ? feel.SlowMoFactor : 1f;
                time += Time.deltaTime * Speed * slow;
            }

            for (var i = 0; i < slots.Count; i++)
            {
                var last = recording.FrameCount - 1;
                Dice[slots[i]].transform.SetPositionAndRotation(recording.Positions[i][last], recording.Rotations[i][last]);
            }
        }

        /// <summary>Резкая смена вертикальной скорости: кость ударилась о стол или бортик.</summary>
        private static bool IsImpact(RollRecording recording, int die, int frame)
        {
            if (frame < 2)
                return false;

            var positions = recording.Positions[die];
            var before = positions[frame - 1].y - positions[frame - 2].y;
            var after = positions[frame].y - positions[frame - 1].y;
            return before < -0.01f && after > -0.002f;
        }

        /// <summary>
        /// Кости остаются там, где упали, в своих позах. Лежащая на ребре кость доваливается на грань,
        /// а кость на другой кости или вне лотка мягко перекатывается на ближайшее свободное место.
        /// Затем камера переходит в вид сверху.
        /// </summary>
        private async UniTask SettleAsync(RollOutcome roll, CancellationToken ct)
        {
            var tray = _table.Tray;
            var size = tray.DieSize;
            var rest = tray.RestHeight;

            // Сначала те, что ниже: лежащие на полу остаются на месте, лежащие сверху ищут свободное место.
            var order = _settleOrder;
            order.Clear();
            order.AddRange(roll.RolledDice);
            order.Sort(_byHeight ?? (_byHeight = (a, b) => Dice[a].transform.position.y.CompareTo(Dice[b].transform.position.y)));

            var placed = _placed;
            placed.Clear();
            var moves = _moves;
            moves.Clear();
            foreach (var slot in order)
            {
                var die = Dice[slot];
                var position = die.transform.position;
                var target = new Vector3(position.x, rest, position.z);
                var onFloor = position.y <= rest + size * 0.25f;

                if (!onFloor || !tray.IsOnFloor(target, size * 0.6f) || IsCrowded(target, placed, size * 1.1f))
                    target = FindFreeSpot(target, placed, size);

                placed.Add(target);

                var moved = (target - position).sqrMagnitude > size * size * 0.01f;
                if (!moved && die.IsLyingFlat(0.999f))
                    continue;

                var rotation = die.RootRotationFlattened();
                var duration = (moved ? _config.SettleDuration : _config.SettleDuration * 0.5f) / Speed;
                moves.Add(moved
                    ? Animate.JumpPoseAsync(die.transform, target, rotation, size * 0.8f, duration, ct)
                    : Animate.PoseAsync(die.transform, target, rotation, duration, ct, AnimateEase.OutCubic));
            }

            moves.Add(_table.Camera.MoveToAsync(CameraShots.Top, 0.45f / Speed, ct));
            var all = UniTask.WhenAll(moves);
            moves.Clear();
            await all;
        }

        /// <summary>
        /// Показ стиля броска в магазине: рука игрока берёт стакан, трясёт им в этом стиле, наклоняет и ставит на место.
        /// Костей нет. Прерывание (другой выбор) возвращает стакан и руку на места.
        /// </summary>
        public async UniTask PreviewShakeAsync(RollStyleConfig styleConfig, CancellationToken ct)
        {
            var seat = _table.South;
            var cup = CupOf(seat);
            var hand = seat.Hand;
            var cupTransform = cup.transform;
            var cupParent = cup.HomeParent;
            var cupLocalPosition = cup.HomeLocalPosition;
            var cupLocalRotation = cup.HomeLocalRotation;
            var random = _visualRandom ?? (_visualRandom = new System.Random());
            var style = (styleConfig != null ? styleConfig : RollStyleConfig.Fallback).Pick(random);

            try
            {
                hand.SnapToRest();
                await GrabCupAsync(hand, cup, 0.25f, ct);
                await MoveCupToAsync(hand, cup, seat.ShakePoint.position, 0.3f, ct);

                _table.Sound.Play(Sfx.DiceRattle, 0.8f);
                await hand.ShakeAsync(style.ShakeDuration, style.ShakeAmplitude, style.ShakeFrequency, style.ShakeTilt,
                    (float)random.NextDouble() * 10f, ct);
                var direction = await WindUpAndSwingAsync(hand, seat, style, 1f, ct);
                await ReturnCupAsync(hand, cupTransform, cupParent, cupLocalPosition, cupLocalRotation,
                    direction * style.FollowThrough, ct);
            }
            finally
            {
                if (cup != null && !cup.IsHome)
                    cup.ReturnHome();

                if (hand != null)
                    hand.SnapToRest();
            }
        }

        /// <summary>Случайный стиль из отмеченных у участника, иначе общий из GameConfig, иначе значения по умолчанию.</summary>
        private RollStyleConfig PickStyle(MatchParticipant participant)
        {
            var styles = participant.RollStyles;
            if (styles != null && styles.Count > 0)
            {
                var style = styles[_visualRandom.Next(styles.Count)];
                if (style != null)
                    return style;
            }

            return _config.RollStyle != null ? _config.RollStyle : RollStyleConfig.Fallback;
        }

        private static bool IsCrowded(Vector3 point, List<Vector3> placed, float distance)
        {
            foreach (var other in placed)
            {
                var dx = other.x - point.x;
                var dz = other.z - point.z;
                if (dx * dx + dz * dz < distance * distance)
                    return true;
            }

            return false;
        }

        /// <summary>Ближайшая к start точка пола лотка, где кость никого не касается. Поиск по расширяющимся кольцам.</summary>
        private Vector3 FindFreeSpot(Vector3 start, List<Vector3> placed, float size)
        {
            var tray = _table.Tray;
            var margin = size * 0.6f;
            var gap = size * 1.25f;
            var step = size * 0.5f;
            var maxRadius = Mathf.Max(tray.Size.x, tray.Size.y);
            var center = tray.Center;
            start.y = tray.RestHeight;

            if (!tray.IsOnFloor(start, margin))
                start = Vector3.Lerp(start, new Vector3(center.x, start.y, center.z), 0.5f);

            for (var radius = 0f; radius <= maxRadius; radius += step)
            {
                var points = radius <= 0f ? 1 : Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * radius / step));
                var phase = (float)_visualRandom.NextDouble() * Mathf.PI * 2f;
                for (var i = 0; i < points; i++)
                {
                    var angle = phase + i * Mathf.PI * 2f / points;
                    var candidate = start + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (tray.IsOnFloor(candidate, margin) && !IsCrowded(candidate, placed, gap))
                        return candidate;
                }
            }

            return start;
        }

        public async UniTask PlayKeepAsync(KeepOutcome keep, CancellationToken ct)
        {
            Dice.DisableSelection();
            Dice.ClearSelection(false);

            var seat = _table.SeatOf(keep.Player);
            var yaw = seat.KeptYaw.eulerAngles.y;
            var duration = _config.KeepDuration / Speed;
            var moves = _moves;
            moves.Clear();
            for (var i = 0; i < keep.KeptDice.Count; i++)
            {
                var slot = keep.KeptDice[i];
                var die = Dice[slot];
                var index = _keptOrder.Count;
                _keptOrder.Add(slot);
                var target = seat.KeptPosition(index, _table.Tray.DieSize);
                moves.Add(Animate.JumpAsync(die.transform, target, 0.4f, duration, ct));
                moves.Add(Animate.RotateAsync(die.transform, die.RootRotationForFaceUp(yaw), duration, ct));
            }

            _table.Sound.Play(Sfx.DiceKeep);
            var all = UniTask.WhenAll(moves);
            moves.Clear();
            await all;

            var feel = Feel;
            var local = IsLocal(keep.Player);
            if (local)
                Haptic(keep.HotDice ? feel.HapticHotDice : feel.HapticKeep);

            // Крупная комбинация искрит у отложенных костей.
            if (!keep.HotDice && keep.Score != null && keep.Score.Score >= _config.BigKeepScore)
                SparksAt(keep.KeptDice, feel.BigKeepSparks);

            if (keep.HotDice)
            {
                // Горячие кости: все шесть искрят, камеру встряхивает, затем кости летят обратно в стакан.
                _table.Sound.Play(Sfx.HotDice);
                SparksAt(_keptOrder, feel.HotDiceSparks);
                if (feel.HotDiceCameraShake.x > 0f)
                    _table.Camera.Shake(feel.HotDiceCameraShake.x, feel.HotDiceCameraShake.y);
                await UniTask.Delay(TimeSpan.FromSeconds(0.3f / Speed), cancellationToken: ct);
                await HideDiceAsync(_keptOrder, CupOf(seat).Mouth.position, ct);
                _keptOrder.Clear();
            }
        }

        public async UniTask PlayZonkAsync(int player, CancellationToken ct)
        {
            _table.Sound.Play(Sfx.Zonk);
            _table.Camera.Shake(0.05f, 0.35f);
            if (IsLocal(player))
                Haptic(Feel.HapticZonk);
            for (var slot = 0; slot < Dice.Dice.Count; slot++)
            {
                var die = Dice[slot];
                if (die.gameObject.activeSelf && !_keptOrder.Contains(slot))
                    die.SetTint(new Color(0.55f, 0.35f, 0.35f));
            }

            await UniTask.Delay(TimeSpan.FromSeconds(0.9f / Speed), cancellationToken: ct);
            await CollectAsync(player, ct);
        }

        public async UniTask PlayBankAsync(int player, CancellationToken ct)
        {
            _table.Sound.Play(Sfx.Bank);
            if (IsLocal(player))
                Haptic(Feel.HapticBank);
            await UniTask.Delay(TimeSpan.FromSeconds(0.3f / Speed), cancellationToken: ct);
            await CollectAsync(player, ct);
        }

        /// <summary>Конец хода: все кости уходят в стакан игрока.</summary>
        private UniTask CollectAsync(int player, CancellationToken ct)
        {
            var visible = _visibleDice;
            visible.Clear();
            for (var slot = 0; slot < Dice.Dice.Count; slot++)
            {
                if (Dice[slot].gameObject.activeSelf)
                    visible.Add(slot);
            }

            _keptOrder.Clear();
            return HideDiceAsync(visible, CupOf(_table.SeatOf(player)).Mouth.position, ct);
        }

        /// <summary>
        /// Кости прыгают в стакан и скрываются. slots — рабочий список вызывающего: он не меняется, пока кости летят.
        /// </summary>
        private async UniTask HideDiceAsync(List<int> slots, Vector3 target, CancellationToken ct)
        {
            var moves = _moves;
            moves.Clear();
            var duration = 0.35f / Speed;
            for (var i = 0; i < slots.Count; i++)
                moves.Add(Animate.JumpAsync(Dice[slots[i]].transform, target, 0.3f, duration, ct));

            var all = UniTask.WhenAll(moves);
            moves.Clear();
            await all;
            for (var i = 0; i < slots.Count; i++)
            {
                var die = Dice[slots[i]];
                die.SetVisible(false);
                die.SetTint(Color.white);
            }
        }

        public async UniTask ShowResultAsync(bool localWon, CancellationToken ct)
        {
            _table.Sound.Play(localWon ? Sfx.Win : Sfx.Lose);
            await _table.Camera.MoveToAsync(CameraShots.Opponent, 0.8f, ct);
        }

        /// <summary>
        /// Кости, которые не входят ни в одну комбинацию, приглушаются (серые и тёмные) — подсказка новичку.
        /// Обесцвечивание, а не только затемнение: на цветных скинах (у второго игрока в игре вдвоём) иначе не видно.
        /// </summary>
        public void HighlightScoringDice(ZonkMatch match)
        {
            var useful = _usefulDice;
            match.MarkScoringDice(useful);

            var feel = Feel;
            for (var slot = 0; slot < Dice.Dice.Count; slot++)
            {
                if (!match.IsInHand(slot) || match.Faces[slot] <= 0)
                    continue;

                if (useful[slot])
                    Dice[slot].SetTint(Color.white);
                else
                    Dice[slot].SetDimmed(feel.UnusedDiceBrightness, feel.UnusedDiceDesaturate);
            }
        }

        /// <summary>Подсказка лучшего хода: советуемые кости — цветом подсказки поверх подсветки полезных.</summary>
        public void HintDice(IReadOnlyList<int> slots)
        {
            if (slots == null)
                return;

            var tint = Feel.HintTint;
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot >= 0 && slot < Dice.Dice.Count)
                    Dice[slot].SetTint(tint);
            }
        }

        private void OnTableImpact()
        {
            _table.Sound.Play(Sfx.Thud);
            _table.Camera.Shake(0.08f, 0.3f);

            // От удара по столу кости подпрыгивают на месте.
            var ct = _table.GetCancellationTokenOnDestroy();
            for (var slot = 0; slot < Dice.Dice.Count; slot++)
            {
                var die = Dice[slot];
                if (die.gameObject.activeSelf)
                    HopAsync(die.transform, ct).Forget();
            }
        }

        private static async UniTaskVoid HopAsync(Transform die, CancellationToken ct)
        {
            try
            {
                await Animate.HopAsync(die, 0.08f, 0.25f, ct);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static CupView CupOf(SeatView seat)
        {
            var holder = seat.CupAnchor.Instance != null ? seat.CupAnchor.Instance : seat.CupAnchor.gameObject;
            return holder.TryGetComponent<CupView>(out var cup) ? cup : holder.AddComponent<CupView>();
        }
    }
}
