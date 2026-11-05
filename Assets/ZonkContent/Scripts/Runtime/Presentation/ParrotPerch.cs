using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Попугай на жёрдочке: резко поворачивает голову, как настоящая птица, иногда кивает и хлопает крыльями.
    /// Кости крутит код, аниматора нет; модель — Tools/Blender/zonk_beach_life.py. Смотрит вдоль −Z объекта.
    /// Числа — в префабе локации. Каждый кадр ничего не выделяет.
    /// </summary>
    public sealed class ParrotPerch : MonoBehaviour
    {
        [SerializeField] private Transform _body;
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _wingLeft;
        [SerializeField] private Transform _wingRight;
        [SerializeField] private Transform _tail;

        [Header("Голова")]
        [Tooltip("Наибольший поворот головы в сторону, градусы")]
        [SerializeField] private float _headYaw = 55f;

        [Tooltip("Наибольший наклон головы к плечу, градусы")]
        [SerializeField] private float _headTilt = 20f;

        [Tooltip("Как быстро голова доходит до нового положения: птица двигает ей рывками")]
        [SerializeField] private float _headSnap = 16f;

        [SerializeField] private Vector2 _headPause = new Vector2(0.5f, 2.4f);

        [Header("Кивки")]
        [Tooltip("Вероятность кивков при новом повороте головы")]
        [Range(0f, 1f)]
        [SerializeField] private float _nodChance = 0.18f;

        [SerializeField] private float _nodDuration = 1.2f;
        [SerializeField] private float _nodSpeed = 13f;
        [SerializeField] private float _nodAngle = 16f;

        [Header("Крылья")]
        [Tooltip("Пауза между взмахами, секунды")]
        [SerializeField] private Vector2 _flapPause = new Vector2(6f, 15f);

        [SerializeField] private float _flapDuration = 0.9f;
        [SerializeField] private float _flapSpeed = 26f;
        [SerializeField] private float _flapOpen = 62f;
        [SerializeField] private float _flapSwing = 24f;

        [Header("Покой")]
        [SerializeField] private float _breathSpeed = 1.7f;
        [SerializeField] private float _breathAngle = 2f;
        [SerializeField] private float _tailAngle = 6f;

        private Quaternion _bodyRest;
        private Quaternion _headRest;
        private Quaternion _wingLeftRest;
        private Quaternion _wingRightRest;
        private Quaternion _tailRest;
        private float _leftSide;
        private float _rightSide;

        private float _yaw;
        private float _tilt;
        private float _pitch;
        private float _yawTarget;
        private float _tiltTarget;
        private float _pitchTarget;
        private float _headTimer;
        private float _nodLeft;
        private float _flapTimer;
        private float _flapLeft;
        private float _open;

#if UNITY_EDITOR
        public void EditorSetup(Transform body, Transform head, Transform wingLeft, Transform wingRight, Transform tail)
        {
            _body = body;
            _head = head;
            _wingLeft = wingLeft;
            _wingRight = wingRight;
            _tail = tail;
        }
#endif

        private void Awake()
        {
            var inverse = Quaternion.Inverse(transform.rotation);
            _bodyRest = inverse * _body.rotation;
            _headRest = inverse * _head.rotation;
            _wingLeftRest = inverse * _wingLeft.rotation;
            _wingRightRest = inverse * _wingRight.rotation;
            _tailRest = inverse * _tail.rotation;

            // Какое крыло с какой стороны, узнаём по положению кости: при выгрузке из Blender ось X переворачивается.
            _leftSide = Mathf.Sign(transform.InverseTransformPoint(_wingLeft.position).x);
            _rightSide = Mathf.Sign(transform.InverseTransformPoint(_wingRight.position).x);

            _headTimer = Random.Range(_headPause.x, _headPause.y);
            _flapTimer = Random.Range(_flapPause.x, _flapPause.y);
        }

        private void Update()
        {
            var delta = Time.deltaTime;
            var time = Time.time;

            UpdateHead(delta);

            _flapTimer -= delta;
            if (_flapTimer <= 0f)
            {
                _flapTimer = Random.Range(_flapPause.x, _flapPause.y);
                _flapLeft = _flapDuration;
            }

            var flapping = _flapLeft > 0f;
            _flapLeft -= delta;
            _open = Mathf.MoveTowards(_open, flapping ? 1f : 0f, delta * 7f);

            var nod = _nodLeft > 0f ? Mathf.Sin(time * _nodSpeed) * _nodAngle : 0f;
            var breath = Mathf.Sin(time * _breathSpeed) * _breathAngle;

            // Повороты заданы в осях объекта, поэтому не зависят от того, как кости повёрнуты в модели.
            // Птица смотрит на −Z: наклон вперёд — поворот вокруг X в минус. Хлопая, выпрямляется и встаёт на носки.
            var root = transform.rotation;
            var lean = Quaternion.Euler(-(breath + nod * 0.25f) + _open * 7f, 0f, 0f);
            var bodyRotation = root * lean;
            _body.rotation = bodyRotation * _bodyRest;
            _head.rotation = bodyRotation * Quaternion.Euler(-(_pitch + nod), _yaw, _tilt) * _headRest;

            var wing = _open * (_flapOpen + Mathf.Sin(time * _flapSpeed) * _flapSwing);
            _wingLeft.rotation = bodyRotation * Quaternion.Euler(0f, 0f, _leftSide * wing) * _wingLeftRest;
            _wingRight.rotation = bodyRotation * Quaternion.Euler(0f, 0f, _rightSide * wing) * _wingRightRest;

            var wag = Mathf.Sin(time * _breathSpeed * 0.7f) * _tailAngle + _open * Mathf.Sin(time * _flapSpeed * 0.5f) * _tailAngle * 2f;
            _tail.rotation = bodyRotation * Quaternion.Euler(0f, wag, 0f) * _tailRest;
        }

        private void UpdateHead(float delta)
        {
            _nodLeft -= delta;
            _headTimer -= delta;
            if (_headTimer <= 0f)
            {
                _headTimer = Random.Range(_headPause.x, _headPause.y);
                _yawTarget = Random.Range(-_headYaw, _headYaw);
                _tiltTarget = Random.Range(-_headTilt, _headTilt);
                _pitchTarget = Random.Range(-6f, 12f);
                if (Random.value < _nodChance)
                    _nodLeft = _nodDuration;
            }

            var step = 1f - Mathf.Exp(-_headSnap * delta);
            _yaw += (_yawTarget - _yaw) * step;
            _tilt += (_tiltTarget - _tilt) * step;
            _pitch += (_pitchTarget - _pitch) * step;
        }
    }
}
