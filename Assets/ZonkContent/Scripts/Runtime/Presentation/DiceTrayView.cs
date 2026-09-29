using System.Collections.Generic;
using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Лоток для броска: пол и бортики (BoxCollider), по ним же считается физика в отдельной сцене.
    /// Центр и размер задают область, где кости раскладываются в ряд после броска.
    /// </summary>
    public sealed class DiceTrayView : MonoBehaviour
    {
        [SerializeField] private List<BoxCollider> _colliders = new List<BoxCollider>();
        [SerializeField] private Transform _center;
        [SerializeField] private Vector2 _size = new Vector2(3.2f, 2f);
        [SerializeField] private float _dieSize = 0.3f;

        public IReadOnlyList<BoxCollider> Colliders => _colliders;
        public Vector3 Center => _center != null ? _center.position : transform.position;
        public Vector2 Size => _size;
        public float DieSize => _dieSize;

        /// <summary>Высота центра кости, лежащей на полу лотка.</summary>
        public float RestHeight => Center.y + _dieSize * 0.5f;

#if UNITY_EDITOR
        public void EditorSetup(List<BoxCollider> colliders, Transform center, Vector2 size, float dieSize)
        {
            _colliders = colliders;
            _center = center;
            _size = size;
            _dieSize = dieSize;
        }
#endif

        /// <summary>Размер кости берётся из GameConfig.DieSize (MatchPresenter при старте).</summary>
        public void SetDieSize(float size)
        {
            if (size > 0f)
                _dieSize = size;
        }

        /// <summary>Точка пола лотка, в которую кость помещается целиком с запасом margin от бортиков.</summary>
        public bool IsOnFloor(Vector3 position, float margin)
        {
            var local = transform.InverseTransformPoint(position) - transform.InverseTransformPoint(Center);
            return Mathf.Abs(local.x) <= _size.x * 0.5f - margin && Mathf.Abs(local.z) <= _size.y * 0.5f - margin;
        }

        /// <summary>Позиции ряда из count костей по центру лотка, слева направо для камеры с юга.</summary>
        public Vector3 RowPosition(int index, int count)
        {
            var spacing = _dieSize * 1.45f;
            var offset = (index - (count - 1) * 0.5f) * spacing;
            var position = Center + transform.right * offset;
            position.y = RestHeight;
            return position;
        }

        /// <summary>Лежит ли точка внутри лотка (с запасом на полкости).</summary>
        public bool Contains(Vector3 position)
        {
            var local = transform.InverseTransformPoint(position) - transform.InverseTransformPoint(Center);
            return Mathf.Abs(local.x) < _size.x * 0.5f && Mathf.Abs(local.z) < _size.y * 0.5f &&
                   position.y < Center.y + _dieSize * 2f;
        }
    }
}
