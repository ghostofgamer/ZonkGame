using UnityEngine;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Живое движение детали: качание, подпрыгивание, пульсация. Вешается на любой объект в префабе, можно вместе со
    /// SpriteFlipbook (кадры меняются, картинка ещё и качается). Числа — в префабе. Каждый кадр ничего не выделяет.
    /// </summary>
    public sealed class UiSway : MonoBehaviour
    {
        [Tooltip("Качание: наибольший угол в градусах")]
        [SerializeField] private float _swayAngle = 3f;

        [Tooltip("Подпрыгивание: высота в единицах холста")]
        [SerializeField] private float _bobHeight;

        [Tooltip("Пульсация: насколько растёт масштаб (0.05 = на 5%)")]
        [SerializeField] private float _pulse;

        [Tooltip("Циклов в секунду")]
        [SerializeField] private float _speed = 1.5f;

        [Tooltip("Сдвиг цикла 0..1: у соседних копий разный, чтобы не двигались одинаково")]
        [SerializeField] private float _phase;

        private RectTransform _rect;
        private Vector2 _position;
        private Vector3 _scale;
        private Quaternion _rotation;
        private float _time;

#if UNITY_EDITOR
        public void EditorSetup(float swayAngle, float bobHeight, float pulse, float speed, float phase)
        {
            _swayAngle = swayAngle;
            _bobHeight = bobHeight;
            _pulse = pulse;
            _speed = speed;
            _phase = phase;
        }
#endif

        private void OnEnable()
        {
            _rect = transform as RectTransform;
            if (_rect != null)
                _position = _rect.anchoredPosition;
            _scale = transform.localScale;
            _rotation = transform.localRotation;
            _time = 0f;
        }

        private void OnDisable()
        {
            if (_rect != null)
                _rect.anchoredPosition = _position;
            transform.localScale = _scale;
            transform.localRotation = _rotation;
        }

        private void Update()
        {
            _time += Time.deltaTime;
            var wave = Mathf.Sin((_time * _speed + _phase) * Mathf.PI * 2f);
            if (_swayAngle != 0f)
                transform.localRotation = _rotation * Quaternion.Euler(0f, 0f, wave * _swayAngle);
            if (_bobHeight != 0f && _rect != null)
                _rect.anchoredPosition = _position + new Vector2(0f, wave * _bobHeight);
            if (_pulse != 0f)
                transform.localScale = _scale * (1f + (wave * 0.5f + 0.5f) * _pulse);
        }
    }
}
