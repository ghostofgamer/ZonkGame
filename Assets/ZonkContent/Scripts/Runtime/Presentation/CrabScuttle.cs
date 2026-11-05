using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Краб на песке: перебегает боком на новое место, останавливается, щёлкает клешнями и бежит снова.
    /// Всегда смотрит примерно на игрока: разворачивается только вокруг вертикали, чтобы бежать боком.
    /// Кости крутит код, аниматора нет; модель — Tools/Blender/zonk_beach_life.py. Числа и участок берега — в префабе локации.
    /// Каждый кадр ничего не выделяет.
    /// </summary>
    public sealed class CrabScuttle : MonoBehaviour
    {
        [SerializeField] private Transform _body;
        [SerializeField] private Transform _clawLeft;
        [SerializeField] private Transform _clawRight;
        [SerializeField] private Transform _pincerLeft;
        [SerializeField] private Transform _pincerRight;

        [Tooltip("Ноги по порядку, через одну — две группы шага")]
        [SerializeField] private Transform[] _legs = new Transform[0];

        [Header("Где бегает (x и z в координатах родителя)")]
        [SerializeField] private Vector2 _areaMin = new Vector2(-2.3f, 4.5f);
        [SerializeField] private Vector2 _areaMax = new Vector2(4.4f, 5f);

        [Tooltip("Короче этого расстояния не перебегает")]
        [SerializeField] private float _minRun = 1.2f;

        [Header("Движение")]
        [SerializeField] private float _speed = 1.9f;
        [SerializeField] private float _acceleration = 7f;
        [SerializeField] private float _turnSpeed = 240f;
        [SerializeField] private Vector2 _pause = new Vector2(1.2f, 4.5f);

        [Header("Шаг")]
        [Tooltip("Шагов на метр пути")]
        [SerializeField] private float _stepsPerMeter = 7f;

        [SerializeField] private float _legLift = 28f;
        [SerializeField] private float _legSwing = 20f;
        [SerializeField] private float _bobHeight = 0.018f;

        [Header("Клешни")]
        [SerializeField] private float _clawRaise = 26f;
        [SerializeField] private float _snapAngle = 24f;
        [SerializeField] private float _snapSpeed = 16f;

        [Tooltip("Пауза между щелчками, секунды")]
        [SerializeField] private Vector2 _snapPause = new Vector2(0.8f, 3f);

        [SerializeField] private float _snapDuration = 0.7f;

        private Quaternion _bodyRest;
        private Quaternion _clawLeftRest;
        private Quaternion _clawRightRest;
        private Quaternion _pincerLeftRest;
        private Quaternion _pincerRightRest;
        private Quaternion[] _legRest;
        private float[] _legSide;
        private Vector3 _bodyPosition;
        private float _leftSide;
        private float _rightSide;

        private Vector3 _target;
        private bool _running;
        private float _currentSpeed;
        private float _yaw;
        private float _yawTarget;
        private float _pauseLeft;
        private float _stepPhase;
        private float _raised;
        private float _snapTimer;
        private float _snapLeft;

#if UNITY_EDITOR
        public void EditorSetup(Transform body, Transform clawLeft, Transform clawRight, Transform pincerLeft, Transform pincerRight, Transform[] legs)
        {
            _body = body;
            _clawLeft = clawLeft;
            _clawRight = clawRight;
            _pincerLeft = pincerLeft;
            _pincerRight = pincerRight;
            _legs = legs;
        }
#endif

        private void Awake()
        {
            var inverse = Quaternion.Inverse(transform.rotation);
            _bodyRest = inverse * _body.rotation;
            _clawLeftRest = inverse * _clawLeft.rotation;
            _clawRightRest = inverse * _clawRight.rotation;
            _pincerLeftRest = inverse * _pincerLeft.rotation;
            _pincerRightRest = inverse * _pincerRight.rotation;
            _bodyPosition = transform.InverseTransformPoint(_body.position);
            _leftSide = Mathf.Sign(transform.InverseTransformPoint(_clawLeft.position).x);
            _rightSide = Mathf.Sign(transform.InverseTransformPoint(_clawRight.position).x);

            _legRest = new Quaternion[_legs.Length];
            _legSide = new float[_legs.Length];
            for (var i = 0; i < _legs.Length; i++)
            {
                _legRest[i] = inverse * _legs[i].rotation;
                _legSide[i] = Mathf.Sign(transform.InverseTransformPoint(_legs[i].position).x);
            }

            _pauseLeft = Random.Range(_pause.x, _pause.y);
            _snapTimer = Random.Range(_snapPause.x, _snapPause.y);
        }

        private void Update()
        {
            var delta = Time.deltaTime;
            Move(delta);
            Pose(delta);
        }

        private void Move(float delta)
        {
            var position = transform.localPosition;
            if (!_running)
            {
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, _acceleration * delta);
                _pauseLeft -= delta;
                if (_pauseLeft <= 0f)
                    StartRun(position);
            }
            else
            {
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, _speed, _acceleration * delta);
                var moved = Vector3.MoveTowards(position, _target, _currentSpeed * delta);
                _stepPhase += (moved - position).magnitude * _stepsPerMeter * Mathf.PI * 2f;
                transform.localPosition = moved;
                if ((moved - _target).sqrMagnitude < 0.0001f)
                {
                    _running = false;
                    _pauseLeft = Random.Range(_pause.x, _pause.y);
                }
            }

            _yaw = Mathf.MoveTowardsAngle(_yaw, _yawTarget, _turnSpeed * delta);
            transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        private void StartRun(Vector3 position)
        {
            var x = Random.Range(_areaMin.x, _areaMax.x);
            if (Mathf.Abs(x - position.x) < _minRun)
                x = position.x + (position.x < (_areaMin.x + _areaMax.x) * 0.5f ? _minRun : -_minRun);

            _target = new Vector3(x, position.y, Random.Range(_areaMin.y, _areaMax.y));
            _running = true;

            // Боком: ось X краба вдоль пути, в какую сторону — чтобы лицо оставалось к игроку (−Z).
            var dx = _target.x - position.x;
            var dz = _target.z - position.z;
            if (dx < 0f)
            {
                dx = -dx;
                dz = -dz;
            }

            _yawTarget = Mathf.Atan2(-dz, dx) * Mathf.Rad2Deg;
        }

        private void Pose(float delta)
        {
            var root = transform.rotation;
            var moving = Mathf.Clamp01(_currentSpeed / Mathf.Max(_speed, 0.01f));

            // Тело: подпрыгивает на шаге, бежит, прижавшись к песку.
            var bob = Mathf.Abs(Mathf.Sin(_stepPhase)) * _bobHeight * moving;
            _body.position = transform.TransformPoint(_bodyPosition + Vector3.up * bob);
            _body.rotation = root * _bodyRest;

            for (var i = 0; i < _legs.Length; i++)
            {
                var phase = _stepPhase + (i % 2 == 0 ? 0f : Mathf.PI);
                var lift = Mathf.Max(0f, Mathf.Sin(phase)) * _legLift * moving;
                var swing = Mathf.Cos(phase) * _legSwing * moving;
                _legs[i].rotation = root * Quaternion.Euler(0f, _legSide[i] * swing, _legSide[i] * lift) * _legRest[i];
            }

            // На бегу клешни подняты, стоя щёлкает: то одной, то обеими.
            _raised = Mathf.MoveTowards(_raised, moving > 0.1f ? 1f : 0f, delta * 5f);
            _snapTimer -= delta;
            if (_snapTimer <= 0f)
            {
                _snapTimer = Random.Range(_snapPause.x, _snapPause.y);
                _snapLeft = _snapDuration;
            }

            _snapLeft -= delta;
            var snapping = _snapLeft > 0f && moving < 0.1f;
            var snap = snapping ? Mathf.Abs(Mathf.Sin(Time.time * _snapSpeed)) * _snapAngle : 0f;
            var raise = (_raised + (snapping ? 0.6f : 0f)) * _clawRaise;

            var clawLeft = root * Quaternion.Euler(0f, 0f, _leftSide * raise);
            var clawRight = root * Quaternion.Euler(0f, 0f, _rightSide * raise);
            _clawLeft.rotation = clawLeft * _clawLeftRest;
            _clawRight.rotation = clawRight * _clawRightRest;

            // Краб смотрит на −Z: нижняя челюсть открывается поворотом вокруг X в минус, конец клешни уходит вниз.
            _pincerLeft.rotation = clawLeft * Quaternion.Euler(-snap, 0f, 0f) * _pincerLeftRest;
            _pincerRight.rotation = clawRight * Quaternion.Euler(-snap, 0f, 0f) * _pincerRightRest;
        }
    }
}
