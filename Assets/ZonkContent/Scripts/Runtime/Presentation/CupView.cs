using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Стакан. Модель любая, но дочерний объект "Mouth" обязателен: из него высыпаются кости.
    /// Компонент добавляется к экземпляру стакана автоматически, в префабы его ставить не нужно.
    /// Место стакана на столе запоминается при появлении: рука всегда возвращает стакан туда,
    /// даже если анимацию прервали посреди движения.
    /// </summary>
    public sealed class CupView : MonoBehaviour
    {
        public const string MouthName = "Mouth";

        /// <summary>
        /// Необязательный маркер внутренности: его высота — внутреннее дно, расстояние от оси — свободный радиус
        /// для костей. Нужен стаканам с ручкой, ножкой или толстым дном; без него место считается по габаритам.
        /// </summary>
        public const string InsideName = "Inside";

        private Transform _mouth;
        private Transform _inside;
        private bool _insideSearched;

        public Transform Mouth
        {
            get
            {
                if (_mouth == null)
                    _mouth = FindDeep(transform, MouthName) ?? transform;
                return _mouth;
            }
        }

        /// <summary>Маркер внутренности или null.</summary>
        public Transform Inside
        {
            get
            {
                if (!_insideSearched)
                {
                    _inside = FindDeep(transform, InsideName);
                    _insideSearched = true;
                }

                return _inside;
            }
        }

        /// <summary>Родитель стакана на столе (якорь слота).</summary>
        public Transform HomeParent { get; private set; }

        public Vector3 HomeLocalPosition { get; private set; }
        public Quaternion HomeLocalRotation { get; private set; }

        public bool IsHome => transform.parent == HomeParent && transform.localPosition == HomeLocalPosition &&
                              transform.localRotation == HomeLocalRotation;

        private void Awake()
        {
            HomeParent = transform.parent;
            HomeLocalPosition = transform.localPosition;
            HomeLocalRotation = transform.localRotation;
        }

        /// <summary>Мгновенно поставить стакан на его место.</summary>
        public void ReturnHome()
        {
            if (transform.parent != HomeParent)
                transform.SetParent(HomeParent, false);
            transform.localPosition = HomeLocalPosition;
            transform.localRotation = HomeLocalRotation;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
                return root;

            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
