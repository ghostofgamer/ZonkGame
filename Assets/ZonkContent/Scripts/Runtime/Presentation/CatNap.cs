using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Кот на сундуке: спит, дышит, водит кончиком хвоста, дёргает ухом. Иногда открывает глаза, поднимает голову, оглядывается и засыпает снова.
    /// Кости крутит код, аниматора нет; модель — Tools/Blender/zonk_attic_life.py, смотрит вдоль −Z объекта.
    /// Числа — в префабе локации. Каждый кадр ничего не выделяет.
    /// </summary>
    public sealed class CatNap : MonoBehaviour
    {
        [SerializeField] private Transform _body;
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _eyes;
        [SerializeField] private Transform _earLeft;
        [SerializeField] private Transform _earRight;

        [Tooltip("Кости хвоста от корня к кончику")]
        [SerializeField] private Transform[] _tail = new Transform[0];

        [Header("Сон")]
        [SerializeField] private float _breathSpeed = 1.5f;
        [SerializeField] private float _breathSize = 0.03f;
        [SerializeField] private float _tailSpeed = 1.1f;
        [SerializeField] private float _tailAngle = 13f;

        [Tooltip("Пауза между подёргиваниями уха, секунды")]
        [SerializeField] private Vector2 _twitchEvery = new Vector2(2.5f, 8f);

        [SerializeField] private float _twitchAngle = 26f;

        [Header("Пробуждение")]
        [SerializeField] private Vector2 _wakeEvery = new Vector2(9f, 22f);
        [SerializeField] private float _wakeTime = 3.2f;
        [SerializeField] private float _liftAngle = 24f;
        [SerializeField] private float _lookAngle = 38f;
        [SerializeField] private float _headSharpness = 5f;

        private Quaternion _headRest;
        private Quaternion _earLeftRest;
        private Quaternion _earRightRest;
        private Quaternion[] _tailRest;
        private Vector3 _bodyScale;
        private Vector3 _eyesScale;
        private float _leftSide;
        private float _rightSide;

        private float _wakeTimer;
        private float _wakeLeft;
        private float _lookTimer;
        private float _lookTarget;
        private float _yaw;
        private float _lift;
        private float _twitchTimer;
        private float _twitch;
        private bool _twitchLeft;

#if UNITY_EDITOR
        public void EditorSetup(Transform body, Transform head, Transform eyes, Transform earLeft, Transform earRight, Transform[] tail)
        {
            _body = body;
            _head = head;
            _eyes = eyes;
            _earLeft = earLeft;
            _earRight = earRight;
            _tail = tail;
        }
#endif

        private void Awake()
        {
            var inverse = Quaternion.Inverse(transform.rotation);
            _headRest = inverse * _head.rotation;
            _earLeftRest = inverse * _earLeft.rotation;
            _earRightRest = inverse * _earRight.rotation;
            _bodyScale = _body.localScale;
            _eyesScale = _eyes.localScale;
            _leftSide = Mathf.Sign(transform.InverseTransformPoint(_earLeft.position).x - transform.InverseTransformPoint(_earRight.position).x);
            _rightSide = -_leftSide;

            _tailRest = new Quaternion[_tail.Length];
            for (var i = 0; i < _tail.Length; i++)
                _tailRest[i] = inverse * _tail[i].rotation;

            _wakeTimer = Random.Range(_wakeEvery.x, _wakeEvery.y);
            _twitchTimer = Random.Range(_twitchEvery.x, _twitchEvery.y);
        }

        private void Update()
        {
            var delta = Time.deltaTime;
            var time = Time.time;
            var root = transform.rotation;

            _body.localScale = _bodyScale * (1f + Mathf.Sin(time * _breathSpeed) * _breathSize);

            _wakeTimer -= delta;
            if (_wakeTimer <= 0f)
            {
                _wakeTimer = Random.Range(_wakeEvery.x, _wakeEvery.y);
                _wakeLeft = _wakeTime;
                _lookTimer = 0f;
            }

            _wakeLeft -= delta;
            var awake = _wakeLeft > 0f;
            if (awake)
            {
                _lookTimer -= delta;
                if (_lookTimer <= 0f)
                {
                    _lookTimer = Random.Range(0.6f, 1.3f);
                    _lookTarget = Random.Range(-_lookAngle, _lookAngle);
                }
            }

            var step = 1f - Mathf.Exp(-_headSharpness * delta);
            _lift += ((awake ? 1f : 0f) - _lift) * step;
            _yaw += ((awake ? _lookTarget : 0f) - _yaw) * step;

            // Морда на −Z: подъём — поворот вокруг X в плюс.
            var headRotation = root * Quaternion.Euler(_lift * _liftAngle, _yaw, 0f);
            _head.rotation = headRotation * _headRest;
            _eyes.localScale = _lift > 0.45f ? _eyesScale : _eyesScale * 0.01f;

            _twitchTimer -= delta;
            if (_twitchTimer <= 0f)
            {
                _twitchTimer = Random.Range(_twitchEvery.x, _twitchEvery.y);
                _twitch = 1f;
                _twitchLeft = Random.value < 0.5f;
            }

            _twitch = Mathf.MoveTowards(_twitch, 0f, delta * 7f);
            var flick = Mathf.Sin(_twitch * Mathf.PI * 3f) * _twitch * _twitchAngle;
            _earLeft.rotation = headRotation * Quaternion.Euler(0f, 0f, _twitchLeft ? _leftSide * flick : 0f) * _earLeftRest;
            _earRight.rotation = headRotation * Quaternion.Euler(0f, 0f, _twitchLeft ? 0f : _rightSide * flick) * _earRightRest;

            // Хвост: у основания почти неподвижен, волна уходит к кончику; поворот накапливается по цепочке.
            var chain = root;
            for (var i = 0; i < _tail.Length; i++)
            {
                var part = (i + 1f) / _tail.Length;
                var angle = Mathf.Sin(time * _tailSpeed - i * 0.8f) * _tailAngle * part;
                chain *= Quaternion.Euler(0f, angle, 0f);
                _tail[i].rotation = chain * _tailRest[i];
            }
        }
    }
}
