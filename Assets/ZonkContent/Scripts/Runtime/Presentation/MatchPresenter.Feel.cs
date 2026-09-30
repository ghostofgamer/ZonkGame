using System.Collections.Generic;
using Base.Services.Haptics;
using UnityEngine;
using Zonk.Configs;
using Zonk.MatchFlow;

namespace Zonk.Presentation
{
    /// <summary>
    /// Ощущение партии на столе: пыль от ударов костей, искры горячих костей и крупных комбинаций,
    /// замедление на решающей кости, вибрация. Числа — MatchFeelConfig. Частицы — две системы из префабов конфига,
    /// создаются один раз и выпускают частицы в нужной точке (Emit), новых объектов на событие нет.
    /// </summary>
    public sealed partial class MatchPresenter
    {
        private const float MoveEpsilon = 0.0004f;
        private const float TurnEpsilon = 0.6f;

        private IHapticsService _haptics;
        private float _lastHaptic = -1f;
        private ParticleSystem _dust;
        private ParticleSystem _sparks;
        private ParticleSystem.EmitParams _emit;
        private readonly int[] _restFrames = new int[6];

        private MatchFeelConfig Feel => _config.Feel != null ? _config.Feel : MatchFeelConfig.Fallback;

        /// <summary>Камера стола: HUD ставит всплывающие надписи над костями.</summary>
        public Camera TableCamera => _table.Camera.Camera;

        /// <summary>Точка над костями в мире: над ней всплывают очки отложенных костей.</summary>
        public Vector3 PopupPoint(IReadOnlyList<int> slots)
        {
            var sum = Vector3.zero;
            var count = 0;
            for (var i = 0; i < slots.Count; i++)
            {
                sum += Dice[slots[i]].transform.position;
                count++;
            }

            var center = count > 0 ? sum / count : _table.Tray.Center;
            return center + Vector3.up * Feel.PopupHeight;
        }

        private bool IsLocal(int player)
        {
            return _participants != null && player >= 0 && player < _participants.Count &&
                   _participants[player].Controller == ControllerKind.Local;
        }

        /// <summary>Вибрация, если она есть, включена игроком и не звучала только что.</summary>
        private void Haptic(int milliseconds)
        {
            if (milliseconds <= 0 || _haptics == null || !_haptics.IsSupported || !_settings.Vibration)
                return;

            var now = Time.unscaledTime;
            if (now - _lastHaptic < Feel.HapticMinInterval)
                return;

            _lastHaptic = now;
            _haptics.Pulse(milliseconds);
        }

        private ParticleSystem EffectSystem(ref ParticleSystem cached, ParticleSystem prefab)
        {
            if (cached == null && prefab != null)
                cached = Object.Instantiate(prefab, _table.transform);
            return cached;
        }

        private void Emit(ParticleSystem system, Vector3 position, int count)
        {
            if (system == null || count <= 0)
                return;

            _emit.position = position;
            _emit.applyShapeToPosition = true;
            system.Emit(_emit, count);
        }

        /// <summary>Пыль под костью, ударившейся о стол: чем быстрее падала, тем больше.</summary>
        private void DustAt(Vector3 position, float fallSpeed)
        {
            var feel = Feel;
            if (fallSpeed < feel.DustMinSpeed)
                return;

            var strength = Mathf.InverseLerp(feel.DustMinSpeed, feel.DustFullSpeed, fallSpeed);
            var count = Mathf.RoundToInt(Mathf.Lerp(feel.DustCount.x, feel.DustCount.y, strength));
            position.y = _table.Tray.Center.y + 0.01f;
            Emit(EffectSystem(ref _dust, feel.DustPrefab), position, count);
        }

        /// <summary>Искры у каждой из костей.</summary>
        private void SparksAt(IReadOnlyList<int> slots, int perDie)
        {
            var system = EffectSystem(ref _sparks, Feel.SparksPrefab);
            if (system == null)
                return;

            for (var i = 0; i < slots.Count; i++)
                Emit(system, Dice[slots[i]].transform.position, perDie);
        }

        /// <summary>Скорость падения кости перед кадром frame, м/с.</summary>
        private static float FallSpeed(RollRecording recording, int die, int frame)
        {
            if (frame < 2)
                return 0f;

            var positions = recording.Positions[die];
            return Mathf.Max(0f, (positions[frame - 2].y - positions[frame - 1].y) / recording.FrameTime);
        }

        /// <summary>
        /// Замедление на решающей кости: в самых рискованных бросках (мало костей) последняя кость докатывается
        /// медленнее, когда остальные уже легли. Зависит только от числа костей, не от исхода: замедление
        /// не подсказывает, будет ли Зонк. Возвращает окно времени записи [from, to), иначе пустое.
        /// </summary>
        private void SlowMoWindow(RollRecording recording, int count, out float from, out float to)
        {
            from = to = 0f;
            var feel = Feel;
            if (feel.SlowMoMaxDice <= 0 || count > feel.SlowMoMaxDice || count > _restFrames.Length)
                return;

            var last = 0;
            var second = 0;
            for (var i = 0; i < count; i++)
            {
                var positions = recording.Positions[i];
                var rotations = recording.Rotations[i];
                var rest = 0;
                for (var f = recording.FrameCount - 1; f > 0; f--)
                {
                    if ((positions[f] - positions[f - 1]).sqrMagnitude > MoveEpsilon * MoveEpsilon ||
                        Quaternion.Angle(rotations[f], rotations[f - 1]) > TurnEpsilon)
                    {
                        rest = f;
                        break;
                    }
                }

                _restFrames[i] = rest;
                if (rest > last)
                {
                    second = last;
                    last = rest;
                }
                else if (rest > second)
                {
                    second = rest;
                }
            }

            var frameTime = recording.FrameTime;
            if (count > 1 && (last - second) * frameTime < feel.SlowMoMinGap)
                return;

            to = last * frameTime;
            from = Mathf.Max(count > 1 ? second * frameTime : 0f, to - feel.SlowMoWindow);
        }
    }
}
