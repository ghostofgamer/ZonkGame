using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Zonk.Configs;

namespace Zonk.Presentation
{
    /// <summary>
    /// Одна кость на столе. Корень двигается по записи физики, дочерний Visual повёрнут на поправку
    /// (DieFaces.Correction), чтобы наверху оказалась грань, выбранная ГСЧ. Клик по кости выбирает её.
    /// Внешний вид задаёт скин: меш (пусто = базовый) и материал. Метка особой кости: свечение цветом метки.
    /// </summary>
    public sealed class DieView : MonoBehaviour, IPointerClickHandler
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Transform _visual;
        [SerializeField] private MeshFilter _meshFilter;
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private GameObject _selectionRing;

        private Mesh _baseMesh;
        private Material _baseMaterial;
        private Mesh _skinMesh;
        private Material _skinMaterial;
        private MaterialPropertyBlock _block;
        private Color _markerColor = Color.clear;
        private float _glow = 1f;
        private Color _tint = Color.white;
        private bool _dimmed;
        private float _dimBrightness = 1f;
        private float _dimDesaturate;
        private bool _selected;
        private BoxCollider _box;

        public event Action<DieView> Clicked;

        public int Slot { get; private set; }

        /// <summary>Грань, которая сейчас наверху по данным партии. 0: кость не брошена.</summary>
        public int Face { get; private set; }

        public bool Interactable { get; set; }
        public bool Selected => _selected;
        public Transform Visual => _visual;

#if UNITY_EDITOR
        public void EditorSetup(Transform visual, MeshFilter meshFilter, MeshRenderer meshRenderer, GameObject ring)
        {
            _visual = visual;
            _meshFilter = meshFilter;
            _renderer = meshRenderer;
            _selectionRing = ring;
        }
#endif

        public void Init(int slot)
        {
            Slot = slot;
            _baseMesh = _meshFilter.sharedMesh;
            _baseMaterial = _renderer.sharedMaterial;
            _block = new MaterialPropertyBlock();
            TryGetComponent(out _box);
            SetSelected(false);
        }

        public void SetSkin(CosmeticItemConfig skin)
        {
            var payload = skin != null ? skin.Payload : null;
            switch (payload)
            {
                case MeshMaterialPayload meshMaterial:
                    _meshFilter.sharedMesh = meshMaterial.Mesh != null ? meshMaterial.Mesh : _baseMesh;
                    _renderer.sharedMaterial = meshMaterial.Material != null ? meshMaterial.Material : _baseMaterial;
                    break;
                case MaterialPayload material:
                    _meshFilter.sharedMesh = _baseMesh;
                    _renderer.sharedMaterial = material.Material != null ? material.Material : _baseMaterial;
                    break;
                default:
                    _meshFilter.sharedMesh = _baseMesh;
                    _renderer.sharedMaterial = _baseMaterial;
                    break;
            }

            _skinMesh = _meshFilter.sharedMesh;
            _skinMaterial = _renderer.sharedMaterial;
            ApplyBlock();
        }

        /// <summary>
        /// Свой вид особой кости поверх скина: модель (пусто — модель скина) и материал. Модель особой кости важнее
        /// модели скина — кость узнаётся по силуэту; материал вида или мастерства — поверх.
        /// </summary>
        public void SetLook(Mesh mesh, Material material)
        {
            if (material == null)
                return;

            _meshFilter.sharedMesh = mesh != null ? mesh : _skinMesh != null ? _skinMesh : _baseMesh;
            _renderer.sharedMaterial = material;
            ApplyBlock();
        }

        /// <summary>Снять вид особой кости: вернуть модель и материал скина.</summary>
        public void ClearLook()
        {
            if (_meshFilter == null || _renderer == null)
                return;

            _meshFilter.sharedMesh = _skinMesh != null ? _skinMesh : _baseMesh;
            _renderer.sharedMaterial = _skinMaterial != null ? _skinMaterial : _baseMaterial;
            ApplyBlock();
        }

        public void SetMarker(Color color)
        {
            _markerColor = color;
            ApplyBlock();
        }

        /// <summary>Сила свечения метки: растёт с уровнем мастерства кости.</summary>
        public void SetGlow(float glow)
        {
            _glow = Mathf.Max(0f, glow);
            ApplyBlock();
        }

        /// <summary>Оттенок кости (множитель цвета скина), например красный при Зонке. Снимает приглушение SetDimmed.</summary>
        public void SetTint(Color tint)
        {
            _tint = tint;
            _dimmed = false;
            ApplyBlock();
        }

        /// <summary>
        /// Приглушить кость, которая не входит ни в одну комбинацию: обесцветить и затемнить. Заметно на любом скине —
        /// простое умножение цвета на тёмных и цветных костях (сапфир, обсидиан) почти не видно.
        /// brightness — яркость (1 = как есть), desaturate — доля серого (0..1).
        /// </summary>
        public void SetDimmed(float brightness, float desaturate)
        {
            _tint = Color.white;
            _dimmed = true;
            _dimBrightness = brightness;
            _dimDesaturate = Mathf.Clamp01(desaturate);
            ApplyBlock();
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            if (_selectionRing != null)
                _selectionRing.SetActive(selected);
        }

        /// <summary>
        /// Кольцо выбора — дочерний объект кости, но кость лежит в произвольной позе. Чтобы отметка была одинаковой
        /// у всех костей, кольцо каждый кадр ставится ровно под центр кости и горизонтально, на стол.
        /// </summary>
        private void LateUpdate()
        {
            if (_selectionRing == null || !_selectionRing.activeSelf)
                return;

            var halfHeight = _box != null ? _box.size.y * transform.lossyScale.y * 0.5f : transform.lossyScale.y * 0.15f;

            _selectionRing.transform.SetPositionAndRotation(transform.position + Vector3.down * (halfHeight * 0.97f),
                Quaternion.identity);
        }

        public void SetFace(int face, Quaternion correction)
        {
            Face = face;
            _visual.localRotation = correction;
        }

        public void ClearFace()
        {
            Face = 0;
        }

        /// <summary>Поворот корня, при котором грань Face смотрит вверх при текущей поправке визуала.</summary>
        public Quaternion RootRotationForFaceUp(float yaw = 0f)
        {
            var visualTarget = Quaternion.Euler(0f, yaw, 0f) * DieFaces.FaceUp(Face == 0 ? 1 : Face);
            return visualTarget * Quaternion.Inverse(_visual.localRotation);
        }

        /// <summary>
        /// Поворот корня, при котором грань Face смотрит строго вверх, а поворот вокруг вертикали остаётся как есть:
        /// кость «доваливается» на грань и лежит так, как упала, а не выравнивается по сетке.
        /// </summary>
        public Quaternion RootRotationFlattened()
        {
            var faceWorld = _visual.rotation * DieFaces.Normal(Face == 0 ? 1 : Face);
            var visualTarget = Quaternion.FromToRotation(faceWorld, Vector3.up) * _visual.rotation;
            return visualTarget * Quaternion.Inverse(_visual.localRotation);
        }

        /// <summary>Грань Face смотрит вверх почти строго: кость лежит, а не стоит на ребре.</summary>
        public bool IsLyingFlat(float tolerance = 0.97f)
        {
            return Vector3.Dot(_visual.rotation * DieFaces.Normal(Face == 0 ? 1 : Face), Vector3.up) >= tolerance;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Interactable)
                Clicked?.Invoke(this);
        }

        private void ApplyBlock()
        {
            if (_block == null || _renderer == null)
                return;

            // Цвет материала скина не перекрываем: блок только умножает его на оттенок.
            _block.Clear();
            var material = _renderer.sharedMaterial;
            if ((_tint != Color.white || _dimmed) && material != null && material.HasProperty(BaseColorId))
            {
                var color = material.GetColor(BaseColorId) * _tint;
                if (_dimmed)
                {
                    var alpha = color.a;
                    var luma = color.r * 0.3f + color.g * 0.59f + color.b * 0.11f;
                    color = Color.Lerp(color, new Color(luma, luma, luma), _dimDesaturate) * _dimBrightness;
                    color.a = alpha;
                }

                _block.SetColor(BaseColorId, color);
            }
            _block.SetColor(EmissionColorId, _markerColor.a > 0f ? _markerColor * (0.35f * _glow) : Color.black);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
