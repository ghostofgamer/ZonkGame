using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>
    /// Подсказка обучения: табличка сверху с текстом и кнопкой «Понятно». Не модальная: у корня нет картинки,
    /// поэтому нажатия мимо таблички проходят в игру. Открывает и закрывает TutorialDirector.
    /// Подсказка с целью — один шаг и одно действие: таблички нет, экран темнее, вокруг цели круг (шейдер Zonk/UiSpotlight),
    /// над кругом стрелка и короткая надпись. Следует за целью при движении камеры и не ловит нажатия: застрять негде,
    /// закрывает её само действие игрока. Стрелки, затемнения и надписи может не быть в старом префабе: тогда только табличка.
    /// </summary>
    public sealed class TutorialTipWindow : UiWindow
    {
        private const float CaptionRoom = 140f;

        private static readonly int CenterId = Shader.PropertyToID("_Center");
        private static readonly int HalfSizeId = Shader.PropertyToID("_HalfSize");
        private static readonly int CornerId = Shader.PropertyToID("_Corner");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private static readonly int ExtraCirclesId = Shader.PropertyToID("_ExtraCircles");
        private static readonly int ExtraCountId = Shader.PropertyToID("_ExtraCount");

        // Столько же в шейдере Zonk/UiSpotlight: шесть костей — главный круг и пять дополнительных.
        private const int MaxExtraCircles = 5;

        [SerializeField] private TMP_Text _text;
        [SerializeField] private UiButtonView _ok;

        [Tooltip("Стрелка: острие в нижней точке (pivot)")]
        [SerializeField] private RectTransform _pointer;

        [Tooltip("Затемнение на весь экран с кругом вокруг цели")]
        [SerializeField] private Image _spotlight;

        [Tooltip("Короткая надпись над стрелкой, дочерняя у стрелки")]
        [SerializeField] private TMP_Text _caption;

        [Tooltip("Табличка: у подсказки с целью не показывается")]
        [SerializeField] private GameObject _panel;

        [Tooltip("Запас круга вокруг точки стола: 1 — впритык")]
        [SerializeField] private float _focusPadding = 1.25f;

        [Tooltip("Запас рамки вокруг элемента интерфейса, единицы канваса")]
        [SerializeField] private float _rectPadding = 12f;

        [Tooltip("Скругление рамки вокруг элемента интерфейса, единицы канваса")]
        [SerializeField] private float _rectCorner = 24f;

        private static readonly Vector3[] Corners = new Vector3[4];

        private readonly Vector3[] _extraPoints = new Vector3[MaxExtraCircles];
        private readonly Vector4[] _extraCircles = new Vector4[MaxExtraCircles];
        private int _extraCount;

        private RectTransform _targetRect;
        private RectTransform _targetRectSecond;
        private Camera _worldCamera;
        private Vector3 _targetPoint;
        private float _targetRadius;
        private Canvas _canvas;
        private Material _material;
        private bool _hasTarget;

        // У подсказки с целью кнопки не видно, а «Назад» всё равно должна её закрыть: иначе пауза не откроется.
        public override void OnBackPressed()
        {
            RequestClose();
        }

#if UNITY_EDITOR
        public bool EditorHasFocus => _spotlight != null && _caption != null && _pointer != null && _panel != null;

        public void EditorSetupFocus(RectTransform pointer, Image spotlight, TMP_Text caption, GameObject panel)
        {
            _panel = panel;
            _pointer = pointer;
            _spotlight = spotlight;
            _caption = caption;
        }

        public void EditorSetup(TMP_Text text, UiButtonView ok)
        {
            _text = text;
            _ok = ok;
        }
#endif

        private void Awake()
        {
            _ok.OnClick(RequestClose);
            if (_pointer != null)
                _pointer.gameObject.SetActive(false);
            if (_spotlight != null)
            {
                // Своя копия: центр и радиус меняются каждый кадр, ассет материала трогать нельзя.
                _material = new Material(_spotlight.material);
                _spotlight.material = _material;
                _spotlight.gameObject.SetActive(false);
            }

            if (_caption != null)
                _caption.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }

        public void Setup(string text)
        {
            _text.text = text;
            _ok.SetText(T("tutorial.ok"));
        }

        /// <summary>Надпись над стрелкой. Пусто — без надписи.</summary>
        public void SetCaption(string caption)
        {
            if (_caption == null)
                return;

            _caption.text = caption;
            _caption.gameObject.SetActive(!string.IsNullOrEmpty(caption));
        }

        /// <summary>Цель — элемент интерфейса или рамка вокруг двух соседних.</summary>
        public void PointAt(RectTransform target, RectTransform second = null)
        {
            _targetRect = target;
            _targetRectSecond = second;
            ShowFocus();
        }

        /// <summary>Цель — точка стола с радиусом в метрах. Стрелка на ней; others — ещё круги того же радиуса, без стрелки.</summary>
        public void PointAt(Vector3 point, float radius, Camera camera, IReadOnlyList<Vector3> others = null)
        {
            _targetPoint = point;
            _targetRadius = radius;
            _worldCamera = camera;
            _extraCount = others != null ? Mathf.Min(others.Count, MaxExtraCircles) : 0;
            for (var i = 0; i < _extraCount; i++)
                _extraPoints[i] = others[i];
            ShowFocus();
        }

        private void ShowFocus()
        {
            _canvas = GetComponentInParent<Canvas>().rootCanvas;
            _hasTarget = true;
            if (_pointer != null)
                _pointer.gameObject.SetActive(true);
            if (_spotlight != null)
                _spotlight.gameObject.SetActive(true);
            if (_panel != null && _caption != null && _caption.gameObject.activeSelf)
                _panel.SetActive(false);
            MoveFocus();
        }

        private void LateUpdate()
        {
            if (_hasTarget)
                MoveFocus();
        }

        private void MoveFocus()
        {
            var uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (!TargetOnScreen(uiCamera, out var screen, out var half, out var corner))
                return;

            if (_spotlight != null)
                MoveSpotlight(uiCamera, screen, half, corner);
            if (_pointer != null)
                MovePointer(uiCamera, screen, half.y);
        }

        /// <summary>Центр цели, половина размера и скругление в пикселях экрана.</summary>
        private bool TargetOnScreen(Camera uiCamera, out Vector2 screen, out Vector2 half, out float corner)
        {
            if (_targetRect != null)
            {
                var min = new Vector2(float.MaxValue, float.MaxValue);
                var max = new Vector2(float.MinValue, float.MinValue);
                Encapsulate(_targetRect, uiCamera, ref min, ref max);
                if (_targetRectSecond != null)
                    Encapsulate(_targetRectSecond, uiCamera, ref min, ref max);

                var padding = _rectPadding * _canvas.scaleFactor;
                screen = (min + max) * 0.5f;
                half = (max - min) * 0.5f + new Vector2(padding, padding);
                corner = _rectCorner * _canvas.scaleFactor;
                return true;
            }

            if (_worldCamera != null)
            {
                screen = _worldCamera.WorldToScreenPoint(_targetPoint);
                Vector2 edge = _worldCamera.WorldToScreenPoint(_targetPoint + _worldCamera.transform.right * _targetRadius);
                var radius = Vector2.Distance(screen, edge) * _focusPadding;
                half = new Vector2(radius, radius);
                corner = radius;
                return true;
            }

            screen = default;
            half = default;
            corner = 0f;
            return false;
        }

        private static void Encapsulate(RectTransform rect, Camera uiCamera, ref Vector2 min, ref Vector2 max)
        {
            rect.GetWorldCorners(Corners);
            for (var i = 0; i < Corners.Length; i++)
            {
                var point = RectTransformUtility.WorldToScreenPoint(uiCamera, Corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
        }

        private void MoveSpotlight(Camera uiCamera, Vector2 screen, Vector2 half, float corner)
        {
            var rectTransform = _spotlight.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, uiCamera, out var local))
                return;

            var rect = rectTransform.rect;
            var toHeight = 1f / _canvas.scaleFactor / rect.height;
            _material.SetVector(CenterId, new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height));
            _material.SetVector(HalfSizeId, half * toHeight);
            _material.SetFloat(CornerId, corner * toHeight);
            _material.SetFloat(AspectId, rect.width / rect.height);

            for (var i = 0; i < _extraCount; i++)
            {
                Vector2 extra = _worldCamera.WorldToScreenPoint(_extraPoints[i]);
                Vector2 edge = _worldCamera.WorldToScreenPoint(_extraPoints[i] + _worldCamera.transform.right * _targetRadius);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, extra, uiCamera, out var extraLocal);
                _extraCircles[i] = new Vector4((extraLocal.x - rect.xMin) / rect.width, (extraLocal.y - rect.yMin) / rect.height,
                    Vector2.Distance(extra, edge) * _focusPadding * toHeight, 0f);
            }

            _material.SetFloat(ExtraCountId, _extraCount);
            if (_extraCount > 0)
                _material.SetVectorArray(ExtraCirclesId, _extraCircles);
        }

        /// <summary>Стрелка острием на верхний край рамки, надпись над ней, но не за краем экрана.</summary>
        private void MovePointer(Camera uiCamera, Vector2 screen, float halfHeight)
        {
            var parent = (RectTransform)_pointer.parent;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen + Vector2.up * halfHeight, uiCamera, out var local))
                return;

            // Цель у верхнего края: стрелка заходит на круг, зато надпись остаётся на экране.
            local.y = Mathf.Min(local.y, parent.rect.yMax - _pointer.rect.height - CaptionRoom);
            _pointer.localPosition = local;
            if (_caption == null)
                return;

            var captionRect = _caption.rectTransform;
            var room = Mathf.Max(0f, parent.rect.width - captionRect.rect.width) * 0.5f;
            var position = captionRect.anchoredPosition;
            position.x = Mathf.Clamp(local.x, parent.rect.center.x - room, parent.rect.center.x + room) - local.x;
            captionRect.anchoredPosition = position;
        }
    }
}
