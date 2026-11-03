using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;
using Zonk.Utils;

namespace Zonk.Presentation
{
    /// <summary>
    /// Соперник напротив: силуэт в полутени, голова с глазами, свободная рука для жестов.
    /// Сейчас заглушка из примитивов с процедурными жестами. Модель с аниматором заменит её,
    /// сохранив методы (жест = триггер аниматора с тем же именем).
    /// </summary>
    public sealed class OpponentAvatarView : MonoBehaviour
    {
        /// <summary>Глаза босса: значение по умолчанию в сцене и на снятых портретах.</summary>
        public static readonly Color DefaultBossEyes = new Color(1f, 0.2f, 0.1f, 1f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Transform _body;
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _accessoryAnchor;
        [SerializeField] private HandView _gestureHand;
        [SerializeField] private Renderer[] _bodyRenderers = Array.Empty<Renderer>();
        [SerializeField] private Transform _slamPoint;

        [Header("Дымная тень (Zonk/ShadowSmoke). Пусто — старая фигура из примитивов")]
        [SerializeField] private Renderer _smoke;
        [SerializeField] private Camera _camera;

        [Tooltip("Руки дыма тянутся к этим рукам (дальняя рука партии и рука жестов)")]
        [SerializeField] private HandView _wristLeft;
        [SerializeField] private HandView _wristRight;

        [Tooltip("Рукав начинается на столько метров позади центра ладони")]
        [SerializeField] private float _wristBack = 0.45f;

        [Tooltip("Точка головы на плоскости дыма, метры от центра плоскости")]
        [SerializeField] private Vector2 _smokeHead = new Vector2(0f, 0.9f);

        [Tooltip("Метров в одной единице шейдера (радиус головы около 0.5 единицы)")]
        [SerializeField] private float _smokeUnit = 0.9f;

        [Header("Глаза")]
        [SerializeField] private Color _bossEyes = DefaultBossEyes;
        [SerializeField] private float _lookAmount = 0.12f;

        private static readonly int WristLeftId = Shader.PropertyToID("_WristL");
        private static readonly int WristRightId = Shader.PropertyToID("_WristR");
        private static readonly int EyeColorId = Shader.PropertyToID("_EyeColor");
        private static readonly int EyeOpenId = Shader.PropertyToID("_EyeOpen");
        private static readonly int EyeSizeId = Shader.PropertyToID("_EyeSize");
        private static readonly int EyeAngleId = Shader.PropertyToID("_EyeAngle");
        private static readonly int EyeShiftId = Shader.PropertyToID("_EyeShift");

        [Header("Детали соперника: профессию показывают они, дым у всех общий")]
        [SerializeField] private Transform _chestAnchor;

        [Tooltip("Точка на столе, где лежит предмет соперника")]
        [SerializeField] private Transform _propAnchor;

        [Tooltip("Безделушка игрока ближе этого к точке предмета: предмет соперника не ставится, чтобы они не пересекались")]
        [SerializeField] private float _propClearance = 0.6f;

        private GameObject _accessory;
        private GameObject _chestPiece;
        private GameObject _heldProp;
        private Vector3 _bodyRest;
        private Quaternion _headRest;
        private MaterialPropertyBlock _block;
        private CancellationTokenSource _idleCts;

        /// <summary>Удар по столу: кости подпрыгивают, камера вздрагивает.</summary>
        public event Action TableImpact;

#if UNITY_EDITOR
        public void EditorSetup(Transform body, Transform head, Transform accessoryAnchor, HandView gestureHand,
            Renderer[] bodyRenderers, Transform slamPoint)
        {
            _body = body;
            _head = head;
            _accessoryAnchor = accessoryAnchor;
            _gestureHand = gestureHand;
            _bodyRenderers = bodyRenderers;
            _slamPoint = slamPoint;
        }

        public void EditorSetupSmoke(Renderer smoke, HandView wristLeft, HandView wristRight)
        {
            _smoke = smoke;
            _wristLeft = wristLeft;
            _wristRight = wristRight;
        }

        public void EditorSetupParts(Transform chestAnchor, Transform propAnchor)
        {
            _chestAnchor = chestAnchor;
            _propAnchor = propAnchor;
        }

        public void EditorSetCamera(Camera camera)
        {
            _camera = camera;
        }
#endif

        private void Awake()
        {
            _bodyRest = _body.localPosition;

            _headRest = _head.localRotation;
            _block = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            StopIdle();
        }

        /// <summary>Убрать предмет со стола, если его место занято безделушкой игрока (дополнительное место стоит там же).</summary>
        public void YieldPropTo(CosmeticStage stage)
        {
            if (_heldProp == null || stage == null || !stage.HasSpotItemNear(_propAnchor.position, _propClearance))
                return;

            Destroy(_heldProp);
            _heldProp = null;
        }

        public void Show(OpponentConfig opponent)
        {
            gameObject.SetActive(true);

            foreach (var bodyRenderer in _bodyRenderers)
            {
                bodyRenderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, opponent != null ? opponent.BodyColor : Color.gray);
                if (bodyRenderer == _smoke && opponent != null)
                    _block.SetColor(EyeColorId, opponent.IsBoss ? _bossEyes : opponent.EyeColor);
                bodyRenderer.SetPropertyBlock(_block);
            }

            SetEyes(1f, 1f, DefaultEyeAngle);

            if (_accessory != null)
                Destroy(_accessory);
            if (opponent != null && opponent.Accessory != null)
                _accessory = Instantiate(opponent.Accessory, _accessoryAnchor, false);

            if (_chestPiece != null)
                Destroy(_chestPiece);
            if (opponent != null && opponent.ChestPiece != null && _chestAnchor != null)
                _chestPiece = Instantiate(opponent.ChestPiece, _chestAnchor, false);

            PlaceProp(opponent);

            if (_gestureHand != null)
                _gestureHand.SetLook(opponent != null ? opponent.HandSkin : null, opponent != null ? opponent.HandLook : null,
                    opponent != null ? opponent.HandSleeve : null, opponent != null ? opponent.HandWrist : null);

            StartIdle();
        }

        /// <summary>
        /// Предмет соперника на столе: модель уже лежит как надо, начало координат на столешнице.
        /// </summary>
        private void PlaceProp(OpponentConfig opponent)
        {
            if (_heldProp != null)
                Destroy(_heldProp);
            _heldProp = null;

            if (opponent == null || opponent.HeldProp == null || _propAnchor == null)
                return;

            _heldProp = Instantiate(opponent.HeldProp, _propAnchor, false);
        }

        public void Hide()
        {
            StopIdle();
            gameObject.SetActive(false);
        }

        public async UniTask PlayAsync(AvatarGesture gesture, CancellationToken ct)
        {
            if (!gameObject.activeInHierarchy)
                return;

            SetEyesFor(gesture);
            try
            {
                await PlayGestureAsync(gesture, ct);
            }
            finally
            {
                if (this != null)
                    SetEyes(1f, 1f, DefaultEyeAngle);
            }
        }

        private const float DefaultEyeAngle = 0.25f;

        /// <summary>Настроение глаз на время жеста: дым ничего не показывает, кроме глаз.</summary>
        private void SetEyesFor(AvatarGesture gesture)
        {
            switch (gesture)
            {
                case AvatarGesture.Angry:
                case AvatarGesture.SlamTable:
                case AvatarGesture.SlamTwice:
                    SetEyes(0.62f, 1.1f, 0.75f);
                    break;
                case AvatarGesture.Cheer:
                case AvatarGesture.Shrug:
                    SetEyes(1.3f, 1.35f, 0f);
                    break;
                case AvatarGesture.Laugh:
                    SetEyes(0.4f, 1.25f, -0.5f);
                    break;
                case AvatarGesture.Think:
                    SetEyes(0.8f, 1f, 0.1f);
                    break;
            }
        }

        private void SetEyes(float open, float size, float angle)
        {
            if (_smoke == null)
                return;

            _smoke.GetPropertyBlock(_block);
            _block.SetFloat(EyeOpenId, open);
            _block.SetFloat(EyeSizeId, size);
            _block.SetFloat(EyeAngleId, angle);
            _smoke.SetPropertyBlock(_block);
        }

        /// <summary>Руки дыма — к рукавам: точка рукава проецируется от камеры на плоскость дыма. Голова поворачивает взгляд.</summary>
        private void LateUpdate()
        {
            if (_smoke == null || _camera == null)
                return;

            _smoke.GetPropertyBlock(_block);
            if (_wristLeft != null)
                _block.SetVector(WristLeftId, WristInSmoke(_wristLeft));
            if (_wristRight != null)
                _block.SetVector(WristRightId, WristInSmoke(_wristRight));

            var look = _head.localRotation * Vector3.forward;
            _block.SetVector(EyeShiftId, new Vector4(look.x * _lookAmount, look.y * _lookAmount, 0f, 0f));
            _smoke.SetPropertyBlock(_block);
        }

        private Vector4 WristInSmoke(HandView hand)
        {
            var quad = _smoke.transform;
            var cameraPoint = _camera.transform.position;
            var hit = hand.transform.TransformPoint(0f, 0f, -_wristBack);
            var direction = hit - cameraPoint;
            var along = Vector3.Dot(quad.position - cameraPoint, quad.forward) / Mathf.Max(0.001f, Vector3.Dot(direction, quad.forward));
            var world = cameraPoint + direction * along - quad.position;
            var x = Vector3.Dot(world, quad.right) - _smokeHead.x;
            var y = Vector3.Dot(world, quad.up) - _smokeHead.y;
            return new Vector4(x / _smokeUnit, y / _smokeUnit, 0f, 0f);
        }

        private async UniTask PlayGestureAsync(AvatarGesture gesture, CancellationToken ct)
        {
            switch (gesture)
            {
                case AvatarGesture.Think:
                    await TiltHeadAsync(new Vector3(0f, 0f, 14f), 0.9f, ct);
                    break;
                case AvatarGesture.Nod:
                    await TiltHeadAsync(new Vector3(18f, 0f, 0f), 0.5f, ct);
                    break;
                case AvatarGesture.Cheer:
                    await BounceAsync(0.25f, 3, 0.6f, ct);
                    break;
                case AvatarGesture.Laugh:
                    await BounceAsync(0.06f, 6, 0.8f, ct);
                    break;
                case AvatarGesture.Shrug:
                    await BounceAsync(0.12f, 1, 0.5f, ct);
                    break;
                case AvatarGesture.Angry:
                    await ShakeHeadAsync(0.6f, ct);
                    break;
                case AvatarGesture.SlamTwice:
                    await SlamAsync(ct);
                    await SlamAsync(ct);
                    break;
                case AvatarGesture.SlamTable:
                    await SlamAsync(ct);
                    break;
            }
        }

        private async UniTask SlamAsync(CancellationToken ct)
        {
            if (_gestureHand == null || _slamPoint == null)
                return;

            var slam = _gestureHand.SlamAsync(_slamPoint.position, ct);
            await UniTask.Delay(260, cancellationToken: ct);
            TableImpact?.Invoke();
            await slam;
        }

        private async UniTask TiltHeadAsync(Vector3 euler, float duration, CancellationToken ct)
        {
            var target = _headRest * Quaternion.Euler(euler);
            await Animate.RotateLocalAsync(_head, target, duration * 0.35f, ct);
            await UniTask.Delay(TimeSpan.FromSeconds(duration * 0.3f), cancellationToken: ct);
            await Animate.RotateLocalAsync(_head, _headRest, duration * 0.35f, ct);
        }

        private async UniTask ShakeHeadAsync(float duration, CancellationToken ct)
        {
            for (var time = 0f; time < duration; time += Time.deltaTime)
            {
                var t = time / duration;
                _head.localRotation = _headRest * Quaternion.Euler(0f, Mathf.Sin(t * Mathf.PI * 6f) * 20f * (1f - t), 0f);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            _head.localRotation = _headRest;
        }

        private async UniTask BounceAsync(float height, int times, float duration, CancellationToken ct)
        {
            for (var time = 0f; time < duration; time += Time.deltaTime)
            {
                var t = time / duration;
                _body.localPosition = _bodyRest + Vector3.up * (Mathf.Abs(Mathf.Sin(t * Mathf.PI * times)) * height);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            _body.localPosition = _bodyRest;
        }

        private void StartIdle()
        {
            StopIdle();
            _idleCts = new CancellationTokenSource();
            IdleAsync(_idleCts.Token).Forget();
        }

        private void StopIdle()
        {
            if (_idleCts == null)
                return;

            _idleCts.Cancel();
            _idleCts.Dispose();
            _idleCts = null;
        }

        /// <summary>Дыхание: лёгкое покачивание, чтобы силуэт не выглядел статуей.</summary>
        private async UniTaskVoid IdleAsync(CancellationToken ct)
        {
            var time = 0f;
            while (!ct.IsCancellationRequested)
            {
                time += Time.deltaTime;
                _body.localScale = new Vector3(1f, 1f + Mathf.Sin(time * 1.6f) * 0.012f, 1f);
                // Без токена в Yield: SuppressCancellationThrow на каждом кадре создавал бы объект.
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }
    }
}
