using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Поезд рамки «Железная дорога»: паровоз и вагоны едут по замкнутому пути (локальные точки префаба), каждый
    /// на своём отставании от головы. Путь и вагоны проставляет генератор (ZonkSetup.Rims). Каждый кадр — только
    /// позиции и повороты нескольких объектов, без выделения памяти. Скорость настраивается в префабе.
    /// Модели вагонов смотрят вперёд по +Z.
    /// </summary>
    public sealed class RimTrain : MonoBehaviour
    {
        [Tooltip("Замкнутый путь по центру колеи, локальные координаты")]
        [SerializeField] private Vector3[] _path = new Vector3[0];

        [Tooltip("Паровоз, затем вагоны")]
        [SerializeField] private Transform[] _cars = new Transform[0];

        [Tooltip("Отставание каждого вагона от головы поезда по пути, метры")]
        [SerializeField] private float[] _offsets = new float[0];

        [Tooltip("Скорость, метры в секунду")]
        [SerializeField] private float _speed = 0.35f;

        private float[] _distance;
        private float _length;
        private float _head;

#if UNITY_EDITOR
        public void EditorSetup(Vector3[] path, Transform[] cars, float[] offsets, float speed)
        {
            _path = path;
            _cars = cars;
            _offsets = offsets;
            _speed = speed;
        }
#endif

        private void Awake()
        {
            var count = _path.Length;
            _distance = new float[count + 1];
            for (var i = 0; i < count; i++)
                _distance[i + 1] = _distance[i] + Vector3.Distance(_path[i], _path[(i + 1) % count]);
            _length = _distance[count];
            Place();
        }

        private void Update()
        {
            if (_length <= 0f)
                return;

            _head += _speed * Time.deltaTime;
            if (_head >= _length)
                _head -= _length;
            Place();
        }

        private void Place()
        {
            if (_length <= 0f)
                return;

            for (var i = 0; i < _cars.Length; i++)
            {
                var car = _cars[i];
                if (car == null)
                    continue;

                var offset = i < _offsets.Length ? _offsets[i] : 0f;
                var s = Mathf.Repeat(_head - offset, _length);
                Sample(s, out var position, out var forward);
                car.localPosition = position;
                if (forward.sqrMagnitude > 1e-6f)
                    car.localRotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }

        /// <summary>Точка и направление на расстоянии s от начала пути (двоичный поиск по накопленным длинам).</summary>
        private void Sample(float s, out Vector3 position, out Vector3 forward)
        {
            var count = _path.Length;
            int lo = 0, hi = count - 1;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) / 2;
                if (_distance[mid] <= s)
                    lo = mid;
                else
                    hi = mid - 1;
            }

            var a = _path[lo];
            var b = _path[(lo + 1) % count];
            var segment = _distance[lo + 1] - _distance[lo];
            var t = segment > 1e-6f ? (s - _distance[lo]) / segment : 0f;
            position = Vector3.LerpUnclamped(a, b, t);
            forward = b - a;
        }
    }
}
