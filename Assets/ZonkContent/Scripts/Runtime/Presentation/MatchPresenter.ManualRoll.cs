using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Zonk.Configs;
using Zonk.Utils;

namespace Zonk.Presentation
{
    /// <summary>
    /// Бросок своей рукой. Игрок зажимает свой стакан (мышью или пальцем): рука берёт его и держит над краем лотка,
    /// стакан ходит за пальцем. Резкие смены направления — кости стучат о стенки, стакан вздрагивает, копится «азарт».
    /// Отпускание — бросок: направление и сила берутся из последнего движения пальца к столу, азарт закручивает кости.
    /// Вялое отпускание просто высыпает кости. Грани по-прежнему решает ГСЧ партии: жест меняет только полёт.
    ///
    /// Порядок: контроллер игрока ждёт кнопку «Бросить» или захват стакана (WaitGrabAsync); захват сразу считается
    /// решением бросать, и пока партия откладывает кости и бросает ГСЧ, игрок уже трясёт стакан. PlayRollAsync
    /// дожидается отпускания и бросает из той точки, где стакан.
    /// Каждый кадр жест не выделяет память: выборки движения — кольцевой массив, всё остальное — поля.
    /// </summary>
    public sealed partial class MatchPresenter
    {
        private const int FlickSamples = 32;

        private readonly Vector3[] _flickPoints = new Vector3[FlickSamples];
        private readonly float[] _flickTimes = new float[FlickSamples];
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private PointerEventData _uiPointer;
        private EventSystem _uiPointerSystem;

        private bool _manualActive;
        private bool _manualReleased;
        private int _manualPlayer;
        private int _flickHead;
        private int _flickCount;
        private Vector3 _manualHold;
        private Vector3 _manualFlick;
        private float _manualEnergy;

        /// <summary>Условие «стакан отпущен»: одно на все броски, без нового делегата на каждый.</summary>
        private Func<bool> _manualDone;

        private ManualRollConfig Manual => _config.ManualRoll != null ? _config.ManualRoll : ManualRollConfig.Fallback;

        /// <summary>Бросок своей рукой включён в конфиге.</summary>
        public bool ManualRollEnabled => Manual.Enabled;

        /// <summary>Последний бросок был сделан своей рукой (для заданий).</summary>
        public bool LastRollManual { get; private set; }

        /// <summary>
        /// Ждать, пока игрок зажмёт свой стакан. canGrab — можно ли сейчас бросать (выбор костей допускает бросок);
        /// invite — покачивать стакан, приглашая взять его. Захват запускает удержание стакана с токеном holdCt
        /// (партия), ожидание отменяется waitCt (сработала кнопка). Возвращает true, если стакан взят.
        /// </summary>
        public async UniTask<bool> WaitGrabAsync(int player, Func<bool> canGrab, bool invite, CancellationToken waitCt,
            CancellationToken holdCt)
        {
            var manual = Manual;
            if (!manual.Enabled)
            {
                await UniTask.WaitUntilCanceled(waitCt);
                return false;
            }

            var seat = _table.SeatOf(player);
            var cup = CupOf(seat);
            var cupTransform = cup.transform;
            var camera = _table.Camera.Camera;
            var bounds = CupBounds(cup);
            var radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            var homePosition = cupTransform.position;
            var homeRotation = cupTransform.rotation;
            var toTray = _table.Tray.Center - homePosition;
            toTray.y = 0f;
            var wobbleAxis = Vector3.Cross(Vector3.up, toTray.sqrMagnitude > 0.0001f ? toTray.normalized : Vector3.forward);

            var idle = 0f;
            var nextInvite = manual.InviteDelay;
            var lift = 0f;
            var posed = false;

            try
            {
                while (true)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, waitCt);
                    var dt = Time.deltaTime;
                    var pointer = Pointer.current;
                    if (pointer == null || cupTransform.parent != cup.HomeParent)
                        continue;

                    var screen = pointer.position.ReadValue();
                    var allowed = canGrab == null || canGrab();
                    var over = allowed && IsOverCup(camera, bounds.center, radius, bounds.extents.y, screen);

                    if (over && pointer.press.wasPressedThisFrame && !IsOverUi(screen))
                    {
                        cupTransform.SetPositionAndRotation(homePosition, homeRotation);
                        StartHold(player, seat, cup, pointer, screen, holdCt);
                        return true;
                    }

                    // Мышь над стаканом — он чуть приподнимается: «меня можно взять».
                    var targetLift = over && pointer is Mouse && !pointer.press.isPressed ? manual.HoverLift : 0f;
                    lift = Mathf.MoveTowards(lift, targetLift, dt * 0.6f);

                    // Долго ничего не происходит — стакан покачивается, приглашая потрясти его.
                    var wobble = 0f;
                    if (invite && manual.InviteDelay > 0f && allowed)
                    {
                        idle += dt;
                        if (idle >= nextInvite)
                        {
                            const float wobbleTime = 0.7f;
                            var t = (idle - nextInvite) / wobbleTime;
                            if (t >= 1f)
                                nextInvite = idle + Mathf.Max(0.5f, manual.InviteInterval);
                            else
                                wobble = Mathf.Sin(t * Mathf.PI * 4f) * manual.InviteAngle * (1f - t);
                        }
                    }

                    if (lift > 0f || wobble != 0f)
                    {
                        cupTransform.SetPositionAndRotation(homePosition + Vector3.up * lift,
                            Quaternion.AngleAxis(wobble, wobbleAxis) * homeRotation);
                        posed = true;
                    }
                    else if (posed)
                    {
                        cupTransform.SetPositionAndRotation(homePosition, homeRotation);
                        posed = false;
                    }
                }
            }
            finally
            {
                if (posed && cupTransform != null && cupTransform.parent == cup.HomeParent)
                    cupTransform.SetPositionAndRotation(homePosition, homeRotation);
            }
        }

        /// <summary>Бросок своей рукой прерван (конец хода, конец партии): стакан на место, рука отдыхает.</summary>
        private void AbortManual()
        {
            if (!_manualActive)
                return;

            _manualActive = false;
            StopCupDice(true);
            var seat = _table.SeatOf(_manualPlayer);
            var cup = CupOf(seat);
            if (!cup.IsHome)
            {
                cup.ReturnHome();
                seat.Hand.SnapToRest();
            }
        }

        private void StartHold(int player, SeatView seat, CupView cup, Pointer pointer, Vector2 screen, CancellationToken ct)
        {
            _manualActive = true;
            _manualReleased = false;
            _manualPlayer = player;
            _manualEnergy = 0f;
            _manualFlick = Vector3.zero;
            _flickHead = 0;
            _flickCount = 0;
            HoldAsync(seat, cup, pointer, screen, ct).Forget();
        }

        /// <summary>Стакан в руке: следует за пальцем, пока его не отпустят.</summary>
        private async UniTaskVoid HoldAsync(SeatView seat, CupView cup, Pointer pointer, Vector2 pressScreen,
            CancellationToken ct)
        {
            var manual = Manual;
            var hand = seat.Hand;
            var camera = _table.Camera.Camera;
            var shake = seat.ShakePoint.position;
            var plane = new Plane(Vector3.up, shake);
            var previousScreen = pressScreen;
            var free = shake;

            try
            {
                // Стакан взяли в виде сверху (после выбора костей): камера плавно переходит в ракурс броска,
                // как в начале хода. Палец при этом не теряет стакан — см. движение по приращениям ниже.
                if (_table.Camera.CurrentShot != CameraShots.Match)
                    _table.Camera.MoveToAsync(CameraShots.Match, 0.45f / Speed, ct).Forget();

                _table.Sound.Play(Sfx.DiceKeep, 0.5f);
                await GrabCupAsync(hand, cup, 0.14f / Speed, ct);
                StartCupDice(cup);

                var hold = cup.transform.TransformPoint(_gripLocal);
                var velocity = Vector3.zero;
                var peak = Vector3.zero;
                var kick = 0f;
                var kickAxis = Vector3.right;
                var lastHit = -1f;
                var raw = shake;

                while (_manualActive && !_manualReleased)
                {
                    var dt = Mathf.Max(Time.deltaTime, 0.0001f);
                    var now = Time.time;
                    var pressed = pointer != null && pointer.added && pointer.press.isPressed;
                    if (pressed)
                    {
                        // Стакан ходит вокруг точки тряски на столько, на сколько сдвинулся палец. Сдвиг за кадр
                        // считается текущей камерой для обеих точек: пока камера летит, стакан не уплывает из-под пальца.
                        var screen = pointer.position.ReadValue();
                        var delta = RayPoint(camera, plane, screen, shake) - RayPoint(camera, plane, previousScreen, shake);
                        delta.y = 0f;
                        previousScreen = screen;

                        // Стакан упирается в край зоны и сразу идёт обратно, когда палец поворачивает.
                        raw = manual.FreeMovement
                            ? ClampToTray(raw + delta, -manual.FreeAreaMargin)
                            : ClampHold(raw + delta, shake, manual.Reach);

                        // Скорость толчка — по пальцу без ограничения: взмах за край зоны не теряет силу.
                        free += delta;
                        AddFlickSample(free, now);
                    }

                    var previous = hold;
                    hold = Vector3.Lerp(hold, raw, 1f - Mathf.Exp(-manual.FollowSharpness * dt));
                    velocity = (hold - previous) / dt;

                    // Кости бьются о стенку, когда стакан резко меняет направление: удар тем сильнее, чем быстрее он шёл.
                    if (Vector3.Dot(velocity, peak) < 0f)
                    {
                        var speed = peak.magnitude;
                        if (speed >= manual.HitMinSpeed && now - lastHit >= manual.HitCooldown)
                        {
                            var strength = Mathf.InverseLerp(manual.HitMinSpeed, manual.HitFullSpeed, speed);
                            _table.Sound.Play(Sfx.DiceClack, 0.3f + 0.7f * strength, 0.25f);
                            Haptic(Feel.HapticCupHit);
                            KickCupDice(strength);
                            _manualEnergy = Mathf.Clamp01(_manualEnergy + manual.EnergyPerHit * (0.4f + 0.6f * strength));
                            kick = manual.HitKick * strength;
                            kickAxis = Vector3.Cross(peak, Vector3.up).normalized;
                            if (strength > 0.6f && manual.HitCameraShake > 0f)
                                _table.Camera.Shake(manual.HitCameraShake * strength, 0.08f);
                            lastHit = now;
                        }

                        peak = velocity;
                    }
                    else if (velocity.sqrMagnitude > peak.sqrMagnitude)
                    {
                        peak = velocity;
                    }
                    else
                    {
                        // Старый разгон забывается: медленный возврат через секунду — уже не удар.
                        peak *= Mathf.Exp(-6f * dt);
                    }

                    _manualEnergy = Mathf.Max(0f, _manualEnergy - manual.EnergyDecay * dt);
                    kick *= Mathf.Exp(-16f * dt);

                    // Стакан клонится по ходу движения и вздрагивает от ударов костей.
                    var flat = new Vector3(velocity.x, 0f, velocity.z);
                    var lean = Mathf.Min(manual.MaxLean, flat.magnitude * manual.LeanPerSpeed);
                    var leanAxis = flat.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
                    var rotation = Quaternion.AngleAxis(lean, leanAxis) * Quaternion.AngleAxis(kick, kickAxis) * _uprightHand;
                    hand.transform.SetPositionAndRotation(hold + _gripOffset, rotation);
                    _manualHold = hold;

                    if (!pressed)
                    {
                        _manualFlick = FlickVelocity(now, manual.FlickSampleTime);
                        _manualReleased = true;
                        break;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException)
            {
                _manualReleased = true;
            }
        }

        /// <summary>
        /// Дождаться, пока игрок отпустит стакан, и бросить: рывок с опрокидыванием в сторону толчка.
        /// Параметры полёта костей (сила, закрутка, разброс) пишутся в style. Возвращает направление броска.
        /// </summary>
        private async UniTask<Vector3> ManualThrowAsync(HandView hand, RollParams style, CancellationToken ct)
        {
            await UniTask.WaitUntil(_manualDone ?? (_manualDone = () => _manualReleased || !_manualActive),
                cancellationToken: ct);

            var manual = Manual;
            var tray = _table.Tray;
            var start = ClampToTray(_manualHold, manual.TrayMargin);

            var forward = tray.Center - start;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

            var flick = new Vector3(_manualFlick.x, 0f, _manualFlick.z);
            float power;
            Vector3 direction;
            bool dropped;
            if (manual.FreeDirection)
            {
                // Куда взмахнул, туда и летят: сила — по скорости взмаха в любую сторону. Вялое отпускание — высыпать
                // на месте, куда стакан чуть двигался (или к центру лотка, если стоял).
                var speed = flick.magnitude;
                power = Mathf.InverseLerp(manual.MinFlickSpeed, manual.MaxFlickSpeed, speed);
                dropped = speed < manual.MinFlickSpeed;
                direction = speed > 0.3f ? flick / speed : forward;
            }
            else
            {
                // Толчок к столу задаёт направление (не дальше MaxAngle от центра лотка) и силу; вялый — просто высыпать.
                var push = Vector3.Dot(flick, forward);
                power = Mathf.InverseLerp(manual.MinFlickSpeed, manual.MaxFlickSpeed, push);
                dropped = false;
                direction = forward;
                if (push >= manual.MinFlickSpeed && flick.sqrMagnitude > 0.0001f)
                {
                    var angle = Mathf.Clamp(Vector3.SignedAngle(forward, flick, Vector3.up), -manual.MaxAngle, manual.MaxAngle);
                    direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                }
            }

            style.ThrowSpeed = dropped ? manual.DropSpeed : Mathf.Lerp(manual.ThrowSpeed.x, manual.ThrowSpeed.y, power);
            style.SpinSpeed = Mathf.Lerp(manual.SpinSpeed.x, manual.SpinSpeed.y, _manualEnergy);
            style.DirectionJitter = 0f;

            _table.Sound.Play(Sfx.Whoosh, 0.35f + 0.5f * power, 0.15f);
            Haptic(Feel.HapticThrow);
            if (power > 0.7f && manual.StrongThrowCameraShake > 0f)
                _table.Camera.Shake(manual.StrongThrowCameraShake * power, 0.2f);

            var release = ClampToTray(start + direction * (manual.SwingDistance * (0.5f + power)), manual.TrayMargin);
            var axis = Vector3.Cross(Vector3.up, direction);
            var pour = Quaternion.AngleAxis(manual.PourAngle, axis) * _uprightHand;
            var duration = manual.SwingDuration * Mathf.Lerp(1.4f, 0.8f, power) / Speed;
            await Animate.PoseAsync(hand.transform, release + _gripOffset, pour, duration, ct, AnimateEase.InCubic);

            _manualActive = false;
            return direction;
        }

        /// <summary>Точка внутри лотка не ближе margin к бортам (высота не меняется).</summary>
        private Vector3 ClampToTray(Vector3 point, float margin)
        {
            var tray = _table.Tray;
            var center = tray.Center;
            var rotation = tray.transform.rotation;
            var local = Quaternion.Inverse(rotation) * (point - center);
            var halfX = Mathf.Max(0f, tray.Size.x * 0.5f - margin);
            var halfZ = Mathf.Max(0f, tray.Size.y * 0.5f - margin);
            local.x = Mathf.Clamp(local.x, -halfX, halfX);
            local.z = Mathf.Clamp(local.z, -halfZ, halfZ);
            var result = center + rotation * local;
            result.y = point.y;
            return result;
        }

        private static Vector3 ClampHold(Vector3 point, Vector3 center, float reach)
        {
            var offset = point - center;
            offset.y = 0f;
            if (offset.sqrMagnitude > reach * reach)
                offset = offset.normalized * reach;
            return center + offset;
        }

        private static Vector3 RayPoint(Camera camera, Plane plane, Vector2 screen, Vector3 fallback)
        {
            var ray = camera.ScreenPointToRay(screen);
            return plane.Raycast(ray, out var distance) && distance < 100f ? ray.GetPoint(distance) : fallback;
        }

        /// <summary>
        /// Палец над стаканом: круг вокруг центра стакана на экране, по размеру видимого стакана с запасом GrabScale,
        /// но не меньше GrabMinScreen высоты экрана — в маленький стакан на телефоне легко попасть.
        /// </summary>
        private bool IsOverCup(Camera camera, Vector3 center, float radius, float halfHeight, Vector2 screen)
        {
            var middle = camera.WorldToScreenPoint(center);
            if (middle.z <= 0f)
                return false;

            var side = camera.WorldToScreenPoint(center + camera.transform.right * radius);
            var top = camera.WorldToScreenPoint(center + Vector3.up * halfHeight);
            var size = Mathf.Max(Vector2.Distance(side, middle), Vector2.Distance(top, middle));
            var zone = Mathf.Max(size * Manual.GrabScale, Manual.GrabMinScreen * Screen.height);
            return (screen - (Vector2)middle).sqrMagnitude <= zone * zone;
        }

        /// <summary>Нажатие пришлось на интерфейс (кнопку, окно): стакан не берётся.</summary>
        private bool IsOverUi(Vector2 screen)
        {
            var system = EventSystem.current;
            if (system == null)
                return false;

            if (_uiPointer == null || _uiPointerSystem != system)
            {
                _uiPointer = new PointerEventData(system);
                _uiPointerSystem = system;
            }

            _uiPointer.position = screen;
            _uiHits.Clear();
            system.RaycastAll(_uiPointer, _uiHits);
            foreach (var hit in _uiHits)
            {
                if (hit.module is GraphicRaycaster)
                {
                    _uiHits.Clear();
                    return true;
                }
            }

            _uiHits.Clear();
            return false;
        }

        private void AddFlickSample(Vector3 point, float time)
        {
            _flickPoints[_flickHead] = point;
            _flickTimes[_flickHead] = time;
            _flickHead = (_flickHead + 1) % FlickSamples;
            _flickCount = Mathf.Min(_flickCount + 1, FlickSamples);
        }

        /// <summary>Скорость пальца за последние window секунд перед отпусканием, в метрах сцены в секунду.</summary>
        private Vector3 FlickVelocity(float now, float window)
        {
            if (_flickCount < 2)
                return Vector3.zero;

            var latest = (_flickHead - 1 + FlickSamples) % FlickSamples;
            var oldest = latest;
            for (var i = 1; i < _flickCount; i++)
            {
                var index = (latest - i + FlickSamples) % FlickSamples;
                oldest = index;
                if (now - _flickTimes[index] >= window)
                    break;
            }

            var dt = _flickTimes[latest] - _flickTimes[oldest];
            return dt > 0.0001f ? (_flickPoints[latest] - _flickPoints[oldest]) / dt : Vector3.zero;
        }
    }
}
