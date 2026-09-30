using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Match;

namespace Zonk.Presentation
{
    /// <summary>
    /// Кости внутри взятого стакана: видны сверху через горло и болтаются от движений стакана — от ручной тряски
    /// и от автоматической одинаково. Своя простая физика в системе стакана: инерция от ускорения стакана,
    /// тяжесть, отскоки от стенок, дна и горла, столкновения костей друг с другом, закрутка от ударов.
    /// Это те же кости, что потом вылетают: в момент броска они переходят в запись физики броска.
    /// Грани по-прежнему решает ГСЧ партии. Каждый кадр память не выделяется: состояния — массивы-поля.
    /// </summary>
    public sealed partial class MatchPresenter
    {
        private const int CupDiceMax = 6;

        private readonly int[] _cupSlots = new int[CupDiceMax];
        private readonly Vector3[] _cupPos = new Vector3[CupDiceMax];
        private readonly Vector3[] _cupVel = new Vector3[CupDiceMax];
        private readonly Vector3[] _cupSpin = new Vector3[CupDiceMax];
        private readonly Quaternion[] _cupRot = new Quaternion[CupDiceMax];
        private int _cupSlotCount;
        private int _cupDiceCount;
        private bool _cupDiceActive;
        private CancellationTokenSource _cupDiceCts;
        private Vector3 _dieTableScale = Vector3.one;

        /// <summary>В стакане все кости (начало хода, горячие кости).</summary>
        private void SetCupContentsAll()
        {
            _cupSlotCount = 0;
            for (var slot = 0; slot < Dice.Dice.Count && _cupSlotCount < CupDiceMax; slot++)
                _cupSlots[_cupSlotCount++] = slot;
        }

        /// <summary>В стакане кости, которые сейчас в руке у игрока.</summary>
        private void SetCupContents(ZonkMatch match)
        {
            _cupSlotCount = 0;
            for (var slot = 0; slot < Dice.Dice.Count && _cupSlotCount < CupDiceMax; slot++)
            {
                if (match.IsInHand(slot))
                    _cupSlots[_cupSlotCount++] = slot;
            }
        }

        private void SetCupContents(IReadOnlyList<int> slots)
        {
            _cupSlotCount = 0;
            for (var i = 0; i < slots.Count && _cupSlotCount < CupDiceMax; i++)
                _cupSlots[_cupSlotCount++] = slots[i];
        }

        /// <summary>Стакан взят рукой: кости появляются внутри и начинают болтаться.</summary>
        private void StartCupDice(CupView cup)
        {
            StopCupDice(true);
            var feel = Feel;
            if (!feel.ShowDiceInCup || cup == null || _cupSlotCount == 0)
                return;

            var cupTransform = cup.transform;
            var up = cupTransform.up;
            var bounds = CupBounds(cup);
            var height = Vector3.Dot(cup.Mouth.position - cupTransform.position, up);
            if (height < 0.05f)
                height = bounds.size.y;

            // В стакане кости меньше: болтаются свободно и не торчат сквозь стенки. На столе — обычного размера.
            var scale = Mathf.Clamp(feel.CupDiceScale, 0.2f, 1f);
            var size = _table.Tray.DieSize * scale;
            var radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * feel.CupInnerRadius;
            var floor = height * feel.CupFloor + size * 0.6f;

            // Раскладка по слоям: по три кости в слое, по кругу, с небольшим разбросом.
            _cupDiceCount = _cupSlotCount;
            for (var i = 0; i < _cupDiceCount; i++)
            {
                var layer = i / 3;
                var angle = (i % 3) * 120f + layer * 60f + (float)_visualRandom.NextDouble() * 30f;
                var ring = Mathf.Max(0f, radius - size * 0.87f) * 0.8f;
                var offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(ring, 0f, 0f);
                _cupPos[i] = new Vector3(offset.x, floor + layer * size * 1.05f, offset.z);
                _cupVel[i] = Vector3.zero;
                _cupSpin[i] = Vector3.zero;
                _cupRot[i] = Quaternion.Euler((float)_visualRandom.NextDouble() * 360f,
                    (float)_visualRandom.NextDouble() * 360f, (float)_visualRandom.NextDouble() * 360f);

                var die = Dice[_cupSlots[i]];
                if (i == 0)
                    _dieTableScale = die.transform.localScale;
                die.transform.localScale = _dieTableScale * scale;
                die.SetTint(Color.white);
                die.transform.SetPositionAndRotation(cupTransform.position + cupTransform.rotation * _cupPos[i],
                    cupTransform.rotation * _cupRot[i]);
                die.SetVisible(true);
            }

            _cupDiceActive = true;
            _cupDiceCts = new CancellationTokenSource();
            CupDiceAsync(cupTransform, radius, height, floor, size, _cupDiceCts.Token).Forget();
        }

        /// <summary>Кости больше не в стакане: бросок (hide = false, их подхватывает запись броска) или отмена.</summary>
        private void StopCupDice(bool hide)
        {
            if (!_cupDiceActive)
                return;

            _cupDiceActive = false;
            _cupDiceCts?.Cancel();
            _cupDiceCts?.Dispose();
            _cupDiceCts = null;

            for (var i = 0; i < _cupDiceCount; i++)
            {
                var die = Dice[_cupSlots[i]];
                if (die == null)
                    continue;

                // Из стакана — снова обычного размера.
                die.transform.localScale = _dieTableScale;
                if (hide)
                    die.SetVisible(false);
            }
        }

        /// <summary>
        /// Стук при ручной тряске: кости подпрыгивают и разлетаются в стороны по силе удара (0..1), вместе со звуком.
        /// </summary>
        private void KickCupDice(float strength)
        {
            if (!_cupDiceActive || strength <= 0f)
                return;

            var kick = Feel.CupHitKick * strength;
            for (var i = 0; i < _cupDiceCount; i++)
            {
                var side = new Vector3((float)_visualRandom.NextDouble() * 2f - 1f, 0f, (float)_visualRandom.NextDouble() * 2f - 1f);
                _cupVel[i] += (side * 0.6f + Vector3.up * (0.6f + (float)_visualRandom.NextDouble() * 0.6f)) * kick;
                Spin(i, kick, Feel);
            }
        }

        /// <summary>Кость сейчас болтается в стакане (её не считать лежащей на столе).</summary>
        private bool IsInCup(int slot)
        {
            if (!_cupDiceActive)
                return false;

            for (var i = 0; i < _cupDiceCount; i++)
            {
                if (_cupSlots[i] == slot)
                    return true;
            }

            return false;
        }

        private async UniTaskVoid CupDiceAsync(Transform cup, float radius, float height, float floor, float size,
            CancellationToken ct)
        {
            var previous = cup.position;
            var previousVelocity = Vector3.zero;
            var first = true;
            var half = size * 0.5f;
            // Ограничения по углам кости (полудиагональ куба 0.87 ребра), а не по центру: углы не торчат сквозь стенки.
            var top = Mathf.Max(floor, height - size * 0.9f);
            var wall = Mathf.Max(0.01f, radius - size * 0.87f);

            while (true)
            {
                // После Update: рука и стакан уже встали в позу этого кадра.
                if (await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, ct).SuppressCancellationThrow() || cup == null)
                    return;

                var feel = Feel;
                var dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);

                // Ускорение стакана: кости «отстают» от него, как в настоящем стакане.
                var position = cup.position;
                var velocity = (position - previous) / dt;
                var acceleration = first ? Vector3.zero : (velocity - previousVelocity) / dt;
                previous = position;
                previousVelocity = velocity;
                first = false;

                var toCup = Quaternion.Inverse(cup.rotation);
                var inertia = Vector3.ClampMagnitude(toCup * (-acceleration * feel.CupInertiaShare), feel.CupMaxAcceleration);
                var force = inertia + toCup * new Vector3(0f, -feel.CupGravity, 0f);

                // Два подшага: быстрые удары тряски не пробивают стенки.
                var step = dt * 0.5f;
                for (var sub = 0; sub < 2; sub++)
                {
                    for (var i = 0; i < _cupDiceCount; i++)
                    {
                        _cupVel[i] += force * step;
                        _cupPos[i] += _cupVel[i] * step;
                        Collide(i, wall, floor, top, feel);
                    }

                    SeparateDice(size * 0.95f, feel.CupBounce);
                }

                var rotation = cup.rotation;
                var damping = Mathf.Exp(-feel.CupSpinDamping * dt);
                for (var i = 0; i < _cupDiceCount; i++)
                {
                    var spin = _cupSpin[i];
                    var speed = spin.magnitude;
                    if (speed > 0.001f)
                        _cupRot[i] = Quaternion.AngleAxis(speed * dt * Mathf.Rad2Deg, spin / speed) * _cupRot[i];
                    _cupSpin[i] = spin * damping;

                    Dice[_cupSlots[i]].transform.SetPositionAndRotation(position + rotation * _cupPos[i], rotation * _cupRot[i]);
                }
            }
        }

        /// <summary>Стенка (цилиндр), дно и горло стакана: кость отскакивает и закручивается от удара.</summary>
        private void Collide(int i, float wall, float floor, float top, MatchFeelConfig feel)
        {
            var p = _cupPos[i];
            var v = _cupVel[i];

            var r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
            if (r > wall)
            {
                var normal = new Vector3(p.x / r, 0f, p.z / r);
                p.x = normal.x * wall;
                p.z = normal.z * wall;
                var into = Vector3.Dot(v, normal);
                if (into > 0f)
                {
                    v -= normal * (into * (1f + feel.CupBounce));
                    v.y *= 1f - feel.CupFriction;
                    Spin(i, into, feel);
                }
            }

            if (p.y < floor)
            {
                p.y = floor;
                if (v.y < 0f)
                {
                    var hit = -v.y;
                    v.y = hit * feel.CupBounce;
                    v.x *= 1f - feel.CupFriction;
                    v.z *= 1f - feel.CupFriction;
                    Spin(i, hit, feel);
                }
            }
            else if (p.y > top)
            {
                // Горло: кости не вылетают, пока стакан не опрокинут броском.
                p.y = top;
                if (v.y > 0f)
                {
                    Spin(i, v.y, feel);
                    v.y = -v.y * feel.CupBounce;
                }
            }

            _cupPos[i] = p;
            _cupVel[i] = v;
        }

        private void Spin(int i, float impact, MatchFeelConfig feel)
        {
            if (impact < 0.2f)
                return;

            var axis = new Vector3((float)_visualRandom.NextDouble() * 2f - 1f, (float)_visualRandom.NextDouble() * 2f - 1f,
                (float)_visualRandom.NextDouble() * 2f - 1f);
            if (axis.sqrMagnitude > 0.0001f)
                _cupSpin[i] += axis.normalized * (impact * feel.CupSpinPerHit);
        }

        /// <summary>Кости не проходят друг сквозь друга: раздвигаются и обмениваются скоростью вдоль удара.</summary>
        private void SeparateDice(float distance, float bounce)
        {
            for (var a = 0; a < _cupDiceCount; a++)
            {
                for (var b = a + 1; b < _cupDiceCount; b++)
                {
                    var delta = _cupPos[b] - _cupPos[a];
                    var length = delta.magnitude;
                    if (length >= distance || length < 0.0001f)
                        continue;

                    var normal = delta / length;
                    var push = (distance - length) * 0.5f;
                    _cupPos[a] -= normal * push;
                    _cupPos[b] += normal * push;

                    var closing = Vector3.Dot(_cupVel[b] - _cupVel[a], normal);
                    if (closing < 0f)
                    {
                        var impulse = -closing * (1f + bounce) * 0.5f;
                        _cupVel[a] -= normal * impulse;
                        _cupVel[b] += normal * impulse;
                    }
                }
            }
        }
    }
}
