using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Лягушка: сидит, дышит горлом, моргает, иногда перепрыгивает на другую кувшинку или камень и разворачивается к игроку.
    /// Кости крутит код, аниматора нет; модель — Tools/Blender/zonk_garden_life.py, смотрит вдоль −Z объекта.
    /// Точки и числа — в префабе локации. Каждый кадр ничего не выделяет.
    /// </summary>
    public sealed class FrogHop : MonoBehaviour
    {
        [SerializeField] private Transform _body;
        [SerializeField] private Transform _throat;
        [SerializeField] private Transform _lids;

        [Tooltip("Задние лапы: левая и правая")]
        [SerializeField] private Transform[] _backLegs = new Transform[0];

        [Tooltip("Передние лапы: левая и правая")]
        [SerializeField] private Transform[] _frontLegs = new Transform[0];

        [Tooltip("Где может сидеть, координаты родителя")]
        [SerializeField] private Vector3[] _spots = new Vector3[0];

        [Header("Прыжок")]
        [SerializeField] private Vector2 _sitTime = new Vector2(4f, 12f);
        [SerializeField] private float _hopTime = 0.55f;
        [SerializeField] private float _hopHeight = 0.65f;
        [SerializeField] private float _kick = 55f;
        [SerializeField] private float _turnSpeed = 400f;

        [Header("Покой")]
        [SerializeField] private float _breathSpeed = 5f;
        [SerializeField] private float _throatSize = 0.35f;
        [SerializeField] private Vector2 _blinkEvery = new Vector2(1.5f, 5f);
        [SerializeField] private float _blinkTime = 0.14f;

        [Tooltip("Куда смотрит сидя, угол вокруг вертикали: к камере партии")]
        [SerializeField] private float _sitYaw = -35f;

        private Quaternion[] _backRest;
        private Quaternion[] _frontRest;
        private Quaternion _bodyRest;
        private Vector3 _throatScale;
        private Vector3 _lidsScale;

        private int _index;
        private int _direction = 1;
        private float _waitLeft;
        private float _hopT = -1f;
        private Vector3 _from;
        private Vector3 _to;
        private float _yaw;
        private float _blinkTimer;
        private float _blinkLeft;

#if UNITY_EDITOR
        public void EditorSetup(Transform body, Transform throat, Transform lids, Transform[] backLegs, Transform[] frontLegs, Vector3[] spots)
        {
            _body = body;
            _throat = throat;
            _lids = lids;
            _backLegs = backLegs;
            _frontLegs = frontLegs;
            _spots = spots;
        }
#endif

        private void Awake()
        {
            var inverse = Quaternion.Inverse(transform.rotation);
            _bodyRest = inverse * _body.rotation;
            _throatScale = _throat.localScale;
            _lidsScale = _lids.localScale;

            _backRest = new Quaternion[_backLegs.Length];
            for (var i = 0; i < _backLegs.Length; i++)
                _backRest[i] = inverse * _backLegs[i].rotation;

            _frontRest = new Quaternion[_frontLegs.Length];
            for (var i = 0; i < _frontLegs.Length; i++)
                _frontRest[i] = inverse * _frontLegs[i].rotation;

            _yaw = _sitYaw;
            _waitLeft = Random.Range(_sitTime.x, _sitTime.y);
            _blinkTimer = Random.Range(_blinkEvery.x, _blinkEvery.y);
        }

        private void Update()
        {
            var delta = Time.deltaTime;
            var air = Move(delta);
            Pose(delta, air);
        }

        /// <summary>Перемещение. Возвращает 0..1: как далеко она в прыжке (0 — сидит).</summary>
        private float Move(float delta)
        {
            var air = 0f;
            var yawTarget = _sitYaw;
            if (_hopT >= 0f)
            {
                _hopT += delta / Mathf.Max(_hopTime, 0.01f);
                var t = Mathf.Min(_hopT, 1f);
                var position = Vector3.Lerp(_from, _to, t);
                position.y += Mathf.Sin(t * Mathf.PI) * _hopHeight;
                transform.localPosition = position;
                air = Mathf.Sin(t * Mathf.PI);
                yawTarget = Mathf.Atan2(-(_to.x - _from.x), -(_to.z - _from.z)) * Mathf.Rad2Deg;
                if (_hopT >= 1f)
                {
                    _hopT = -1f;
                    _waitLeft = Random.Range(_sitTime.x, _sitTime.y);
                }
            }
            else if (_spots.Length > 1)
            {
                _waitLeft -= delta;
                if (_waitLeft <= 0f)
                    StartHop();
            }

            _yaw = Mathf.MoveTowardsAngle(_yaw, yawTarget, _turnSpeed * delta);
            transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            return air;
        }

        private void StartHop()
        {
            if (_index + _direction < 0 || _index + _direction >= _spots.Length)
                _direction = -_direction;

            _from = _spots[_index];
            _index += _direction;
            _to = _spots[_index];
            _hopT = 0f;
        }

        private void Pose(float delta, float air)
        {
            var root = transform.rotation;

            // В прыжке тело вытягивается вперёд и вверх, лапы отталкиваются назад и подтягиваются обратно.
            var lean = root * Quaternion.Euler(-air * 22f, 0f, 0f);
            _body.rotation = lean * _bodyRest;
            for (var i = 0; i < _backLegs.Length; i++)
                _backLegs[i].rotation = lean * Quaternion.Euler(-air * _kick, 0f, 0f) * _backRest[i];

            for (var i = 0; i < _frontLegs.Length; i++)
                _frontLegs[i].rotation = lean * Quaternion.Euler(air * _kick * 0.5f, 0f, 0f) * _frontRest[i];

            // Горло дышит, в прыжке не раздувается.
            var breath = 1f + (0.5f + 0.5f * Mathf.Sin(Time.time * _breathSpeed)) * _throatSize * (1f - air);
            _throat.localScale = _throatScale * breath;

            // Веки: в покое спрятаны масштабом, моргание открывает их на миг.
            _blinkTimer -= delta;
            if (_blinkTimer <= 0f)
            {
                _blinkTimer = Random.Range(_blinkEvery.x, _blinkEvery.y);
                _blinkLeft = _blinkTime;
            }

            _blinkLeft -= delta;
            _lids.localScale = _blinkLeft > 0f ? _lidsScale : _lidsScale * 0.01f;
        }
    }
}
