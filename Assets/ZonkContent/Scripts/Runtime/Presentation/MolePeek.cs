using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Крот в норе: время от времени вылезает, оглядывается, шевелит носом, машет лапой и прячется обратно.
    /// Прячется под пол: опускается ниже плитки, запечённая чёрная дыра его закрывает.
    /// Кости крутит код, аниматора нет; модель — Tools/Blender/zonk_basement_life.py, смотрит вдоль −Z объекта.
    /// Числа — в префабе локации. Каждый кадр ничего не выделяет.
    /// </summary>
    public sealed class MolePeek : MonoBehaviour
    {
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _pawLeft;
        [SerializeField] private Transform _pawRight;

        [Tooltip("Высота, когда вылез; в координатах родителя")]
        [SerializeField] private float _shownHeight = -1.68f;

        [Tooltip("На сколько уходит вниз, когда спрятан")]
        [SerializeField] private float _hideDepth = 1.25f;

        [Header("Время, секунды")]
        [SerializeField] private Vector2 _hideTime = new Vector2(6f, 15f);
        [SerializeField] private Vector2 _showTime = new Vector2(3f, 6f);
        [SerializeField] private float _riseTime = 0.35f;
        [SerializeField] private float _sinkTime = 0.22f;

        [Header("Движения")]
        [SerializeField] private float _lookAngle = 42f;
        [SerializeField] private float _snap = 11f;
        [SerializeField] private float _sniffSpeed = 17f;
        [SerializeField] private float _sniffAngle = 5f;
        [SerializeField] private float _waveAngle = 58f;
        [SerializeField] private float _waveSpeed = 12f;

        private Quaternion _headRest;
        private Quaternion _pawLeftRest;
        private Quaternion _pawRightRest;
        private float _rightSide;

        private float _shown;
        private bool _out;
        private float _timer;
        private float _lookTimer;
        private float _lookTarget;
        private float _yaw;
        private float _waveLeft;

#if UNITY_EDITOR
        public void EditorSetup(Transform head, Transform pawLeft, Transform pawRight, float shownHeight)
        {
            _head = head;
            _pawLeft = pawLeft;
            _pawRight = pawRight;
            _shownHeight = shownHeight;
        }
#endif

        private void Awake()
        {
            var inverse = Quaternion.Inverse(transform.rotation);
            _headRest = inverse * _head.rotation;
            _pawLeftRest = inverse * _pawLeft.rotation;
            _pawRightRest = inverse * _pawRight.rotation;
            _rightSide = Mathf.Sign(transform.InverseTransformPoint(_pawRight.position).x);
            _timer = Random.Range(1f, _hideTime.x);
            ApplyHeight();
        }

        private void ApplyHeight()
        {
            // Вылезает быстро и плавно замедляется у верха.
            var rise = 1f - (1f - _shown) * (1f - _shown);
            var position = transform.localPosition;
            position.y = _shownHeight - _hideDepth * (1f - rise);
            transform.localPosition = position;
        }

        private void Update()
        {
            var delta = Time.deltaTime;
            _timer -= delta;
            if (_timer <= 0f)
            {
                _out = !_out;
                _timer = _out ? Random.Range(_showTime.x, _showTime.y) : Random.Range(_hideTime.x, _hideTime.y);
                _lookTimer = 0f;
                _waveLeft = _out && Random.value < 0.5f ? 1.1f : 0f;
            }

            _shown = Mathf.MoveTowards(_shown, _out ? 1f : 0f, delta / Mathf.Max(_out ? _riseTime : _sinkTime, 0.01f));
            ApplyHeight();

            // Спрятан — двигать нечего.
            if (_shown <= 0f)
                return;

            _lookTimer -= delta;
            if (_lookTimer <= 0f)
            {
                _lookTimer = Random.Range(0.45f, 1.3f);
                _lookTarget = Random.Range(-_lookAngle, _lookAngle);
            }

            _yaw += (_lookTarget - _yaw) * (1f - Mathf.Exp(-_snap * delta));
            var root = transform.rotation;
            var time = Time.time;
            _head.rotation = root * Quaternion.Euler(Mathf.Sin(time * _sniffSpeed) * _sniffAngle, _yaw, 0f) * _headRest;

            _waveLeft -= delta;
            var wave = _waveLeft > 0f ? _waveAngle * (0.7f + 0.3f * Mathf.Sin(time * _waveSpeed)) : 0f;
            _pawRight.rotation = root * Quaternion.Euler(0f, 0f, _rightSide * wave) * _pawRightRest;
            _pawLeft.rotation = root * _pawLeftRest;
        }
    }
}
