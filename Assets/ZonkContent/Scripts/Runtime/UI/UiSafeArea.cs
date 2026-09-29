using UnityEngine;

namespace Zonk.UI
{
    /// <summary>
    /// Вписывает свой RectTransform в безопасную зону экрана (Screen.safeArea): вырезы камеры, скругления,
    /// системные панели телефона. В браузере зона обычно равна всему окну. Следит за поворотом и сменой размера
    /// окна браузера. Весь UI сцены живёт внутри такого объекта.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiSafeArea : MonoBehaviour
    {
        private Rect _applied;
        private Vector2Int _screen;

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (_applied != Screen.safeArea || _screen.x != Screen.width || _screen.y != Screen.height)
                Apply();
        }

        private void Apply()
        {
            var area = Screen.safeArea;
            _applied = area;
            _screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            rect.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
