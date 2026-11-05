using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Крыса на палубе: сидит в укрытии, принюхивается, перебегает к следующей точке и обратно. Точки маршрута проложены так,
    /// чтобы путь по прямой не шёл сквозь предметы; крайние — укрытия, там сидит дольше. Кости крутит код, аниматора нет;
    /// модель — Tools/Blender/zonk_ship_life.py, смотрит вдоль −Z объекта. Числа и маршрут — в префабе локации.
    /// Каждый кадр ничего не выделяет.
    /// </summary>
    public sealed class RatScurry : MonoBehaviour
    {
        [SerializeField] private Transform _body;
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _tailBase;
        [SerializeField] private Transform _tailEnd;

        [Tooltip("Ноги: левая передняя, правая передняя, левая задняя, правая задняя")]
        [SerializeField] private Transform[] _legs = new Transform[0];

        [Tooltip("Точки маршрута, x и z в координатах родителя. Первая и последняя — укрытия")]
        [SerializeField] private Vector2[] _route = new Vector2[0];

        [Header("Движение")]
        [SerializeField] private float _speed = 2.6f;
        [SerializeField] private float _acceleration = 12f;
        [SerializeField] private float _turnSpeed = 720f;

        [Tooltip("Сколько сидит в укрытии, секунды")]
        [SerializeField] private Vector2 _hide = new Vector2(5f, 14f);

        [Tooltip("Сколько принюхивается на промежуточной точке, секунды")]
        [SerializeField] private Vector2 _pause = new Vector2(0.5f, 2.5f);

        [Header("Шаг")]
        [SerializeField] private float _stepsPerMeter = 5f;
        [SerializeField] private float _legSwing = 42f;
        [SerializeField] private float _bobHeight = 0.03f;
        [SerializeField] private float _bodyPitch = 6f;

        [Header("Голова и хвост")]
        [SerializeField] private float _sniffSpeed = 15f;
        [SerializeField] private float _sniffAngle = 7f;
        [SerializeField] private float _lookAngle = 42f;
        [SerializeField] private float _tailSpeed = 4f;
        [SerializeField] private float _tailAngle = 16f;
        [SerializeField] private float _tailRunAngle = 26f;

        private Quaternion _bodyRest;
        private Quaternion _headRest;
        private Quaternion _tailBaseRest;
        private Quaternion _tailEndRest;
        private Quaternion[] _legRest;
        private Vector3 _bodyPosition;

        private int _index;
        private int _direction = 1;
        private bool _running;
        private float _currentSpeed;
        private float _yaw;
        private float _yawTarget;
        private float _waitLeft;
        private float _stepPhase;
        private float _look;
        private float _lookTarget;
        private float _lookTimer;

#if UNITY_EDITOR
        public void EditorSetup(Transform body, Transform head, Transform tailBase, Transform tailEnd, Transform[] legs, Vector2[] route)
        {
            _body = body;
            _head = head;
            _tailBase = tailBase;
            _tailEnd = tailEnd;
            _legs = legs;
            _route = route;
        }
#endif

        private void Awake()
        {
            var inverse = Quaternion.Inverse(transform.rotation);
            _bodyRest = inverse * _body.rotation;
            _headRest = inverse * _head.rotation;
            _tailBaseRest = inverse * _tailBase.rotation;
            _tailEndRest = inverse * _tailEnd.rotation;
            _bodyPosition = transform.InverseTransformPoint(_body.position);

            _legRest = new Quaternion[_legs.Length];
            for (var i = 0; i < _legs.Length; i++)
                _legRest[i] = inverse * _legs[i].rotation;

            _waitLeft = Random.Range(_pause.x, _hide.x);
        }

        private void Update()
        {
            var delta = Time.deltaTime;
            Move(delta);
            Pose(delta);
        }

        private void Move(float delta)
        {
            if (_route.Length < 2)
                return;

            var position = transform.localPosition;
            if (!_running)
            {
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, _acceleration * delta);
                _waitLeft -= delta;
                if (_waitLeft <= 0f)
                    StartRun(position);
            }
            else
            {
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, _speed, _acceleration * delta);
                var point = _route[_index];
                var target = new Vector3(point.x, position.y, point.y);
                var moved = Vector3.MoveTowards(position, target, _currentSpeed * delta);
                _stepPhase += (moved - position).magnitude * _stepsPerMeter * Mathf.PI * 2f;
                transform.localPosition = moved;
                if ((moved - target).sqrMagnitude < 0.0001f)
                {
                    _running = false;
                    var hideout = _index == 0 || _index == _route.Length - 1;
                    _waitLeft = hideout ? Random.Range(_hide.x, _hide.y) : Random.Range(_pause.x, _pause.y);
                }
            }

            _yaw = Mathf.MoveTowardsAngle(_yaw, _yawTarget, _turnSpeed * delta);
            transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        private void StartRun(Vector3 position)
        {
            if (_index + _direction < 0 || _index + _direction >= _route.Length)
                _direction = -_direction;

            _index += _direction;
            _running = true;

            // Морда смотрит вдоль −Z: при повороте на угол вокруг Y вперёд становится (−sin, −cos).
            var point = _route[_index];
            _yawTarget = Mathf.Atan2(-(point.x - position.x), -(point.y - position.z)) * Mathf.Rad2Deg;
        }

        private void Pose(float delta)
        {
            var root = transform.rotation;
            var time = Time.time;
            var moving = Mathf.Clamp01(_currentSpeed / Mathf.Max(_speed, 0.01f));

            // Галоп: передние ноги вместе, задние вместе, тело качается вверх-вниз и клюёт носом.
            var gallop = Mathf.Sin(_stepPhase);
            _body.position = transform.TransformPoint(_bodyPosition + Vector3.up * (Mathf.Abs(gallop) * _bobHeight * moving));
            var bodyRotation = root * Quaternion.Euler(gallop * _bodyPitch * moving, 0f, 0f);
            _body.rotation = bodyRotation * _bodyRest;

            for (var i = 0; i < _legs.Length; i++)
            {
                var swing = (i < 2 ? gallop : -gallop) * _legSwing * moving;
                _legs[i].rotation = bodyRotation * Quaternion.Euler(swing, 0f, 0f) * _legRest[i];
            }

            // Стоя озирается рывками и шевелит носом, на бегу смотрит вперёд.
            _lookTimer -= delta;
            if (_lookTimer <= 0f)
            {
                _lookTimer = Random.Range(0.4f, 1.4f);
                _lookTarget = Random.Range(-_lookAngle, _lookAngle);
            }

            var look = moving > 0.1f ? 0f : _lookTarget;
            _look += (look - _look) * (1f - Mathf.Exp(-14f * delta));
            var sniff = Mathf.Sin(time * _sniffSpeed) * _sniffAngle * (1f - moving);
            _head.rotation = bodyRotation * Quaternion.Euler(sniff - 4f * (1f - moving), _look, 0f) * _headRest;

            var size = Mathf.Lerp(_tailAngle, _tailRunAngle, moving);
            var speed = Mathf.Lerp(_tailSpeed, _tailSpeed * 4f, moving);
            var wag = Mathf.Sin(time * speed) * size;
            var tailRotation = bodyRotation * Quaternion.Euler(0f, wag, 0f);
            _tailBase.rotation = tailRotation * _tailBaseRest;
            _tailEnd.rotation = tailRotation * Quaternion.Euler(0f, Mathf.Sin(time * speed - 1.1f) * size * 1.3f, 0f) * _tailEndRest;
        }
    }
}
