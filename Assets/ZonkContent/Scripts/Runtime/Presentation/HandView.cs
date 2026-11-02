using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;
using Zonk.Utils;

namespace Zonk.Presentation
{
    /// <summary>Палец модели руки: суставы от костяшки к кончику.</summary>
    [Serializable]
    public sealed class HandFinger
    {
        public Transform[] Joints;
        public bool Thumb;
    }

    /// <summary>
    /// Рука, которая берёт стакан, трясёт и высыпает кости. Модель — Art/Models/Hands/Hand.fbx (zonk_arms.py): кисть
    /// с куском предплечья, пальцы на костях; срез предплечья закрывает рукав (якорь слота hand_sleeve, модели
    /// zonk_sleeves.py). Вид — кожа (SetSkin) и рукав (SetSleeve) игрока, которому рука принадлежит.
    /// Пальцы гнутся кодом (без аниматора): обхват стакана считает HandGrip под его радиус, позы и скорость — HandConfig.
    /// Без пальцев (старая заглушка из примитивов) рука работает как раньше, только не сжимается.
    /// </summary>
    public sealed class HandView : MonoBehaviour
    {
        [SerializeField] private Transform _grip;
        [SerializeField] private Transform _restPoint;

        [Tooltip("Правая рука: пальцы обхватывают стакан вправо. Выключить для левши")]
        [SerializeField] private bool _rightHanded = true;

        [SerializeField] private HandConfig _config;
        [SerializeField] private HandFinger[] _fingers;

        /// <summary>Половина толщины ладони заглушки: ладонь касается стенки стакана, а не входит в неё.</summary>
        private const float PlaceholderPalmOffset = 0.08f;

        public Transform Grip => _grip;
        public bool RightHanded => _rightHanded;

#if UNITY_EDITOR
        public void EditorSetup(Transform grip, Transform restPoint, bool rightHanded = true)
        {
            _grip = grip;
            _restPoint = restPoint;
            _rightHanded = rightHanded;
        }

        public void EditorSetupRig(HandConfig config, HandFinger[] fingers)
        {
            _config = config;
            _fingers = fingers;
        }
#endif

        [Tooltip("Рендерер кожи модели: скин руки меняет только его первый материал (кожа), не рукав и не заглушку среза")]
        [SerializeField] private Renderer _skinRenderer;

        [Header("Облик: якоря в корне руки (модели выгружены в осях руки, Tools/Blender/zonk_handkit.py)")]
        [Tooltip("Рукав (слот hand_sleeve)")]
        [SerializeField] private CosmeticAnchor _sleeve;
        [SerializeField] private CosmeticSlotConfig _sleeveSlot;

        [Tooltip("Часы и браслеты (слот hand_wrist)")]
        [SerializeField] private CosmeticAnchor _wrist;
        [SerializeField] private CosmeticSlotConfig _wristSlot;

        [Tooltip("Кисть: перчатка или особая рука на скелете руки (слот hand_look, HandLookPart)")]
        [SerializeField] private CosmeticAnchor _handLook;
        [SerializeField] private CosmeticSlotConfig _handLookSlot;

#if UNITY_EDITOR
        public void EditorSetupLook(Renderer skinRenderer, CosmeticAnchor sleeve, CosmeticSlotConfig sleeveSlot)
        {
            _skinRenderer = skinRenderer;
            _sleeve = sleeve;
            _sleeveSlot = sleeveSlot;
        }

        public void EditorSetupParts(CosmeticAnchor wrist, CosmeticSlotConfig wristSlot, CosmeticAnchor handLook,
            CosmeticSlotConfig handLookSlot)
        {
            _wrist = wrist;
            _wristSlot = wristSlot;
            _handLook = handLook;
            _handLookSlot = handLookSlot;
        }
#endif

        /// <summary>Якоря облика (рукав, запястье, кисть); у заглушки из примитивов — null.</summary>
        public CosmeticAnchor Sleeve => _sleeve;
        public CosmeticAnchor Wrist => _wrist;
        public CosmeticAnchor HandLook => _handLook;

        private Renderer[] _renderers;
        private Material[] _baseMaterials;
        private Material _baseSkin;
        private Material _skinMaterial;
        private bool _skinSet;
        private CosmeticItemConfig _skin;

        // Модель кисти: её кости привязаны к костям руки по именам; рука спрятана, если модель её заменяет.
        private readonly List<SkinnedMeshRenderer> _lookRenderers = new List<SkinnedMeshRenderer>();
        private Dictionary<string, Transform> _boneMap;
        private bool _lookSkinSlot;

        // Суставы всех пальцев подряд; у пальца f — с _offsets[f], _counts[f] штук.
        private bool _rigReady;
        private Transform[] _joints;
        private Quaternion[] _bind;
        private Vector3[] _axes;
        private float[] _bases;
        private float[] _lengths;
        private float[] _solved;
        private float[] _current;
        private float[] _target;
        private Vector2[] _starts;
        private int[] _offsets;
        private int[] _counts;
        private bool _moving;

        // Стакан в руке: хват пересчитывается в редакторе каждый кадр, чтобы правки HandConfig были видны сразу.
        private bool _holding;
        private float _heldRadius;
        private CosmeticItemConfig _heldCup;

        private HandConfig Config => _config != null ? _config : HandConfig.Fallback;
        private bool Rigged => EnsureRig();

        private void Awake()
        {
            EnsureRig();
            if (_handLook != null)
                _handLook.InstanceChanged += OnHandLookChanged;
        }

        private void OnDestroy()
        {
            if (_handLook != null)
                _handLook.InstanceChanged -= OnHandLookChanged;
        }

        private void Start()
        {
            // Облик никто не задал (рука жестов, сцена без экипировки): вещи слотов по умолчанию, срез не виден.
            if (_sleeve != null && _sleeve.ModelItem == null)
                SetPart(_sleeve, _sleeveSlot, null);
            if (_wrist != null && _wrist.ModelItem == null)
                SetPart(_wrist, _wristSlot, null);
            if (_handLook != null && _handLook.ModelItem == null)
                SetPart(_handLook, _handLookSlot, null);
        }

        /// <summary>Вид руки целиком: кожа, кисть (перчатка, особая рука), рукав, запястье. null — вещь слота по умолчанию.</summary>
        public void SetLook(CosmeticItemConfig skin, CosmeticItemConfig hand, CosmeticItemConfig sleeve, CosmeticItemConfig wrist)
        {
            SetSkin(skin);
            SetPart(_handLook, _handLookSlot, hand);
            SetPart(_sleeve, _sleeveSlot, sleeve);
            SetPart(_wrist, _wristSlot, wrist);
        }

        private static void SetPart(CosmeticAnchor anchor, CosmeticSlotConfig slot, CosmeticItemConfig item)
        {
            if (anchor != null)
                anchor.Show(item, slot != null ? slot.DefaultItem : null);
        }

        /// <summary>
        /// Кожа руки: материал предмета (MaterialPayload) на коже — первый материал рендерера кожи; null или другое —
        /// кожа по умолчанию. У заглушки без рендерера кожи перекрашиваются все её части.
        /// </summary>
        public void SetSkin(CosmeticItemConfig skin)
        {
            if (_skinRenderer != null)
            {
                SetModelSkin(skin);
                return;
            }

            if (_renderers == null)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
                _baseMaterials = new Material[_renderers.Length];
                for (var i = 0; i < _renderers.Length; i++)
                    _baseMaterials[i] = _renderers[i].sharedMaterial;
            }

            if (_skin == skin)
                return;

            _skin = skin;
            var material = skin != null && skin.Payload is MaterialPayload payload ? payload.Material : null;
            for (var i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].sharedMaterial = material != null ? material : _baseMaterials[i];
            }
        }

        private void SetModelSkin(CosmeticItemConfig skin)
        {
            if (!_skinSet)
            {
                _baseSkin = _skinRenderer.sharedMaterial;
                _skinMaterial = _baseSkin;
                _skinSet = true;
            }
            else if (_skin == skin)
            {
                return;
            }

            _skin = skin;
            _skinMaterial = skin != null && skin.Payload is MaterialPayload payload && payload.Material != null
                ? payload.Material
                : _baseSkin;
            // Смена кожи редкая (смена игрока, примерка): массив материалов можно создать.
            var materials = _skinRenderer.sharedMaterials;
            if (materials.Length > 0 && materials[0] != _skinMaterial)
            {
                materials[0] = _skinMaterial;
                _skinRenderer.sharedMaterials = materials;
            }

            ApplyLookSkin();
        }

        private void OnHandLookChanged(CosmeticAnchor anchor)
        {
            BindHandLook();
        }

        /// <summary>
        /// Модель кисти поставлена (или убрана): её кости — кости руки (по именам), рука спрятана, если модель её
        /// заменяет; в материал кожи модели — кожа игрока. Смена вещи редкая: поиск компонентов допустим.
        /// </summary>
        private void BindHandLook()
        {
            _lookRenderers.Clear();
            _lookSkinSlot = false;
            var hides = false;
            var instance = _handLook != null ? _handLook.Instance : null;
            if (instance != null)
            {
                var part = instance.GetComponent<HandLookPart>();
                hides = part != null && part.HidesHand;
                _lookSkinSlot = part != null && part.SkinSlot;
                instance.GetComponentsInChildren(true, _lookRenderers);
                for (var i = 0; i < _lookRenderers.Count; i++)
                    Rebind(_lookRenderers[i]);
            }

            if (_skinRenderer != null)
                _skinRenderer.enabled = !hides;
            ApplyLookSkin();
        }

        private void Rebind(SkinnedMeshRenderer renderer)
        {
            var map = BoneMap();
            if (map == null)
                return;

            var bones = renderer.bones;
            for (var i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null && map.TryGetValue(bones[i].name, out var bone))
                    bones[i] = bone;
            }

            renderer.bones = bones;
            if (renderer.rootBone != null && map.TryGetValue(renderer.rootBone.name, out var root))
                renderer.rootBone = root;
        }

        private Dictionary<string, Transform> BoneMap()
        {
            if (_boneMap != null)
                return _boneMap;
            if (!(_skinRenderer is SkinnedMeshRenderer skinned))
                return null;

            _boneMap = new Dictionary<string, Transform>();
            foreach (var bone in skinned.bones)
            {
                if (bone != null)
                    _boneMap[bone.name] = bone;
            }

            return _boneMap;
        }

        private void ApplyLookSkin()
        {
            if (!_lookSkinSlot)
                return;

            var skin = _skinMaterial != null ? _skinMaterial : _skinRenderer != null ? _skinRenderer.sharedMaterial : null;
            if (skin == null)
                return;

            for (var i = 0; i < _lookRenderers.Count; i++)
            {
                var materials = _lookRenderers[i].sharedMaterials;
                if (materials.Length < 2 || materials[0] == skin)
                    continue;
                materials[0] = skin;
                _lookRenderers[i].sharedMaterials = materials;
            }
        }

        /// <summary>
        /// Где центр стакана радиуса radius относительно руки, держащей его (локально: Y — от стакана, Z — вдоль пальцев).
        /// У модели — под подушкой ладони у костяшек (HandConfig), у заглушки — под центром ладони.
        /// </summary>
        public Vector3 CupCentre(float radius)
        {
            if (!Rigged)
                return new Vector3(0f, -(radius + PlaceholderPalmOffset), 0f);

            var centre = HandGrip.CupCentre(radius, Config);
            return new Vector3(0f, centre.x, centre.y);
        }

        /// <summary>
        /// На сколько градусов (вокруг вертикали) рука обходит стакан от стороны, откуда пришла: правая — вправо
        /// (минус), левая — влево. У заглушки 0: ладонь к стороне прихода, как раньше.
        /// </summary>
        public float GripAround => !Rigged ? 0f : _rightHanded ? -Config.GripAround : Config.GripAround;

        /// <summary>Радиус хвата стакана cup: измеренный по модели плюс его поправка из HandConfig.CupGrips.</summary>
        public float GripRadius(float measured, CosmeticItemConfig cup)
        {
            var grip = Config.FindCupGrip(cup);
            return grip != null ? Mathf.Max(0.01f, measured + grip.RadiusOffset) : measured;
        }

        /// <summary>
        /// Обхватить стакан радиуса radius (уже с поправкой GripRadius): пальцы ложатся на стенку и сжимаются
        /// к кулаку на долю сжатия (общую или своего стакана), плавно — HandConfig.CurlSpeed.
        /// </summary>
        public void GripCup(float radius, CosmeticItemConfig cup)
        {
            _heldRadius = radius;
            _heldCup = cup;
            _holding = true;
            SolveGrip();
        }

        private void SolveGrip()
        {
            if (!Rigged)
                return;

            var config = Config;
            var grip = config.FindCupGrip(_heldCup);
            var squeeze = grip != null ? grip.Squeeze : config.GripSqueeze;
            var radius = _heldRadius;
            var centre = HandGrip.CupCentre(radius, config);
            var wall = radius + config.FingerPad;
            for (var f = 0; f < _offsets.Length; f++)
            {
                if (_fingers[f].Thumb)
                    HandGrip.BendThumb(_starts[f], _bases, _lengths, _offsets[f], _counts[f], centre, wall,
                        config.ThumbBend * Mathf.Deg2Rad, config.ThumbOpen * Mathf.Deg2Rad, _solved);
                else
                    HandGrip.BendFinger(_starts[f], _bases, _lengths, _offsets[f], _counts[f], centre, wall,
                        config.MaxBend * Mathf.Deg2Rad, config.MaxOpen * Mathf.Deg2Rad, _solved);
            }

            // Сжатие: от расчётного хвата к кулаку.
            for (var f = 0; f < _offsets.Length; f++)
            {
                var share = _fingers[f].Thumb ? config.ThumbFistShare : 1f;
                for (var i = 0; i < _counts[f]; i++)
                {
                    var k = _offsets[f] + i;
                    _target[k] = Mathf.Lerp(_solved[k] * Mathf.Rad2Deg, config.FistBend[Mathf.Min(i, 2)] * share, squeeze);
                }
            }

            _moving = true;
        }

        /// <summary>Тянется к стакану: пальцы раскрыты.</summary>
        public void Reach()
        {
            var config = Config;
            SetUniform(config.ReachBend, config.ReachBend);
        }

        /// <summary>Без стакана: пальцы слегка согнуты.</summary>
        public void Relax()
        {
            var config = Config;
            SetUniform(config.RelaxBend, config.ThumbRelaxBend);
        }

        /// <summary>Кулак (удар по столу).</summary>
        public void MakeFist()
        {
            _holding = false;
            if (!Rigged)
                return;

            var config = Config;
            for (var f = 0; f < _offsets.Length; f++)
            {
                var share = _fingers[f].Thumb ? config.ThumbFistShare : 1f;
                for (var i = 0; i < _counts[f]; i++)
                    _target[_offsets[f] + i] = config.FistBend[Mathf.Min(i, 2)] * share;
            }

            _moving = true;
        }

        public UniTask MoveToAsync(Vector3 position, Quaternion rotation, float duration, CancellationToken ct)
        {
            return Animate.PoseAsync(transform, position, rotation, duration, ct, AnimateEase.InOutCubic);
        }

        public UniTask ReturnAsync(float duration, CancellationToken ct)
        {
            return _restPoint == null
                ? UniTask.CompletedTask
                : Animate.PoseAsync(transform, _restPoint.position, _restPoint.rotation, duration, ct, AnimateEase.InOutCubic);
        }

        /// <summary>Сразу в исходную позу: место покоя и расслабленные пальцы.</summary>
        public void SnapToRest()
        {
            if (_restPoint != null)
                transform.SetPositionAndRotation(_restPoint.position, _restPoint.rotation);

            Relax();
            if (!Rigged)
                return;

            for (var i = 0; i < _current.Length; i++)
                _current[i] = _target[i];
            ApplyPose();
            _moving = false;
        }

        /// <summary>
        /// Тряска вокруг текущей позы: частые смещения и покачивание. Частота, размах и наклон приходят из стиля броска,
        /// фаза каждой оси своя, поэтому тряска каждый раз немного другая.
        /// </summary>
        public async UniTask ShakeAsync(float duration, float amplitude, float frequency, float tilt, float seed,
            CancellationToken ct)
        {
            var basePosition = transform.position;
            var baseRotation = transform.rotation;
            // Свой цикл, а не Animate.RunAsync с замыканием: тряска на каждый бросок не создаёт объектов.
            for (var time = 0f; time < duration; time += Time.deltaTime)
            {
                var phase = time * frequency + seed;
                var fade = Mathf.Sin(time / duration * Mathf.PI);
                var offset = new Vector3(Mathf.Sin(phase), Mathf.Abs(Mathf.Sin(phase * 1.3f)) * 0.6f, Mathf.Cos(phase * 0.9f)) *
                             (amplitude * fade);
                transform.SetPositionAndRotation(basePosition + offset, baseRotation * Quaternion.Euler(
                    Mathf.Sin(phase * 1.1f) * tilt * fade, 0f, Mathf.Cos(phase + seed) * tilt * fade));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            transform.SetPositionAndRotation(basePosition, baseRotation);
        }

        /// <summary>Удар кулаком по столу.</summary>
        public async UniTask SlamAsync(Vector3 tablePoint, CancellationToken ct)
        {
            var start = transform.position;
            var up = tablePoint + Vector3.up * 0.6f;
            MakeFist();
            await Animate.MoveAsync(transform, up, 0.18f, ct, AnimateEase.OutCubic);
            await Animate.MoveAsync(transform, tablePoint, 0.08f, ct, AnimateEase.InCubic);
            await UniTask.Delay(120, cancellationToken: ct);
            Relax();
            await Animate.MoveAsync(transform, start, 0.3f, ct, AnimateEase.InOutCubic);
        }

        private void LateUpdate()
        {
#if UNITY_EDITOR
            if (_holding)
                SolveGrip();
#endif
            if (!_moving)
                return;

            var step = Config.CurlSpeed * Time.deltaTime;
            var done = true;
            for (var i = 0; i < _current.Length; i++)
            {
                _current[i] = Mathf.MoveTowards(_current[i], _target[i], step);
                if (_current[i] != _target[i])
                    done = false;
            }

            ApplyPose();
            _moving = !done;
        }

        private void SetUniform(float fingerBend, float thumbBend)
        {
            _holding = false;
            if (!Rigged)
                return;

            for (var f = 0; f < _offsets.Length; f++)
            {
                var bend = _fingers[f].Thumb ? thumbBend : fingerBend;
                for (var i = 0; i < _counts[f]; i++)
                    _target[_offsets[f] + i] = bend;
            }

            _moving = true;
        }

        private void ApplyPose()
        {
            for (var i = 0; i < _joints.Length; i++)
                _joints[i].localRotation = _bind[i] * Quaternion.AngleAxis(_current[i], _axes[i]);
        }

        /// <summary>
        /// Снять исходную позу пальцев (модель как выгружена, пальцы прямые): поворот каждого сустава,
        /// ось сгиба (X руки) в его осях и фаланги в плоскости руки. Один раз; без пальцев — false.
        /// </summary>
        private bool EnsureRig()
        {
            if (_rigReady)
                return true;
            if (_fingers == null || _fingers.Length == 0)
                return false;

            var total = 0;
            for (var f = 0; f < _fingers.Length; f++)
            {
                var joints = _fingers[f].Joints;
                if (joints == null || joints.Length == 0)
                    return false;
                for (var i = 0; i < joints.Length; i++)
                {
                    if (joints[i] == null)
                        return false;
                }

                total += joints.Length;
            }

            _joints = new Transform[total];
            _bind = new Quaternion[total];
            _axes = new Vector3[total];
            _bases = new float[total];
            _lengths = new float[total];
            _solved = new float[total];
            _current = new float[total];
            _target = new float[total];
            _starts = new Vector2[_fingers.Length];
            _offsets = new int[_fingers.Length];
            _counts = new int[_fingers.Length];

            var bendAxis = transform.right;
            var k = 0;
            for (var f = 0; f < _fingers.Length; f++)
            {
                var joints = _fingers[f].Joints;
                _offsets[f] = k;
                _counts[f] = joints.Length;
                _starts[f] = Plane(joints[0].position);
                for (var i = 0; i < joints.Length; i++, k++)
                {
                    var joint = joints[i];
                    _joints[k] = joint;
                    _bind[k] = joint.localRotation;
                    _axes[k] = Quaternion.Inverse(joint.rotation) * bendAxis;

                    // У последней фаланги нет следующего сустава: она продолжает предыдущую той же длины.
                    var from = Plane(joint.position);
                    var to = i + 1 < joints.Length
                        ? Plane(joints[i + 1].position)
                        : from + (from - Plane(joints[Mathf.Max(0, i - 1)].position));
                    var segment = to - from;
                    _bases[k] = Mathf.Atan2(segment.x, segment.y);
                    _lengths[k] = segment.magnitude;
                }
            }

            _rigReady = true;
            return true;
        }

        /// <summary>Точка в плоскости хвата: (высота, вперёд) = (Y, Z) руки.</summary>
        private Vector2 Plane(Vector3 world)
        {
            var local = transform.InverseTransformPoint(world);
            return new Vector2(local.y, local.z);
        }
    }
}
