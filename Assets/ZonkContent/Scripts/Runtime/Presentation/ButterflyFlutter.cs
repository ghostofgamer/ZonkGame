using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Бабочка: порхает восьмёрками над своим местом, то выше, то ниже, часто и неровно бьёт крыльями, иногда замирает с раскрытыми.
    /// Кости крутит код, аниматора нет; модель — Tools/Blender/zonk_garden_life.py, смотрит вдоль −Z объекта.
    /// Место и числа — в префабе локации. Каждый кадр ничего не выделяет.
    /// </summary>
    public sealed class ButterflyFlutter : MonoBehaviour
    {
        [SerializeField] private Transform _wingLeft;
        [SerializeField] private Transform _wingRight;

        [Tooltip("Центр полёта в координатах родителя")]
        [SerializeField] private Vector3 _center;

        [Tooltip("Размах полёта по осям, метры")]
        [SerializeField] private Vector3 _range = new Vector3(1f, 0.25f, 0.6f);

        [Tooltip("Скорость облёта")]
        [SerializeField] private float _speed = 0.55f;

        [Tooltip("Сдвиг по времени: у соседних бабочек разный")]
        [SerializeField] private float _timeOffset;

        [Header("Крылья")]
        [SerializeField] private float _flapSpeed = 21f;
        [SerializeField] private float _flapUp = 62f;
        [SerializeField] private float _flapDown = 14f;
        [SerializeField] private float _turnSharpness = 6f;

        [Tooltip("Пауза между замираниями, секунды")]
        [SerializeField] private Vector2 _restEvery = new Vector2(3f, 8f);

        [SerializeField] private float _restTime = 0.45f;

        private Quaternion _wingLeftRest;
        private Quaternion _wingRightRest;
        private float _leftSide;
        private float _rightSide;
        private float _phase;
        private float _flap;
        private float _restTimer;
        private float _restLeft;
        private Vector3 _previous;

#if UNITY_EDITOR
        public void EditorSetup(Transform wingLeft, Transform wingRight, Vector3 center, Vector3 range, float speed, float timeOffset)
        {
            _wingLeft = wingLeft;
            _wingRight = wingRight;
            _center = center;
            _range = range;
            _speed = speed;
            _timeOffset = timeOffset;
        }
#endif

        private void Awake()
        {
            var inverse = Quaternion.Inverse(transform.rotation);
            _wingLeftRest = inverse * _wingLeft.rotation;
            _wingRightRest = inverse * _wingRight.rotation;
            _leftSide = Mathf.Sign(transform.InverseTransformPoint(_wingLeft.position).x);
            _rightSide = Mathf.Sign(transform.InverseTransformPoint(_wingRight.position).x);
            _phase = _timeOffset;
            _previous = PathPoint(_phase);
            _restTimer = Random.Range(_restEvery.x, _restEvery.y);
        }

        /// <summary>Восьмёрка с разными частотами по осям: путь не повторяется заметно.</summary>
        private Vector3 PathPoint(float t)
        {
            return _center + new Vector3(
                Mathf.Sin(t) * _range.x + Mathf.Sin(t * 2.3f + 1.1f) * _range.x * 0.25f,
                Mathf.Sin(t * 1.7f + 0.6f) * _range.y + Mathf.Sin(t * 4.3f) * _range.y * 0.3f,
                Mathf.Sin(t * 2f + 2.1f) * _range.z);
        }

        private void Update()
        {
            var delta = Time.deltaTime;

            // Иногда зависает на месте с раскрытыми крыльями, потом летит дальше.
            _restTimer -= delta;
            if (_restTimer <= 0f)
            {
                _restTimer = Random.Range(_restEvery.x, _restEvery.y);
                _restLeft = _restTime;
            }

            _restLeft -= delta;
            var resting = _restLeft > 0f;
            if (!resting)
            {
                _phase += _speed * delta;
                _flap += _flapSpeed * delta;
            }

            var point = PathPoint(_phase);
            var moved = point - _previous;
            _previous = point;
            transform.localPosition = point;

            moved.y = 0f;
            if (moved.sqrMagnitude > 0.0000001f)
            {
                // Морда вдоль −Z: при повороте на угол вокруг Y вперёд становится (−sin, −cos).
                var yaw = Mathf.Atan2(-moved.x, -moved.z) * Mathf.Rad2Deg;
                var step = 1f - Mathf.Exp(-_turnSharpness * delta);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Euler(0f, yaw, 0f), step);
            }

            var wave = 0.5f + 0.5f * Mathf.Sin(_flap);
            var angle = resting ? _flapDown : Mathf.Lerp(-_flapDown, _flapUp, wave);
            var root = transform.rotation;
            _wingLeft.rotation = root * Quaternion.Euler(0f, 0f, _leftSide * angle) * _wingLeftRest;
            _wingRight.rotation = root * Quaternion.Euler(0f, 0f, _rightSide * angle) * _wingRightRest;
        }
    }
}
