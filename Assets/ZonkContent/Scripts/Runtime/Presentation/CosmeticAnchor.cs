using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using Zonk.Configs;

namespace Zonk.Presentation
{
    /// <summary>
    /// Точка в сцене, куда ставится предмет косметики слота SlotId. Предмет с префабом заменяет модель,
    /// предмет с материалом перекрашивает базовую модель слота (префаб предмета по умолчанию).
    ///
    /// Префабы грузятся по требованию (CosmeticAssets): загруженный ставится сразу, иначе — когда загрузится; пока
    /// грузится, стоит прежняя модель. Более поздний Show отменяет недогрузившийся прежний. Когда модель сменилась,
    /// приходит InstanceChanged (свет ламп и локаций собирается заново).
    /// </summary>
    public sealed class CosmeticAnchor : MonoBehaviour
    {
        [SerializeField] private string _slotId;

        [Tooltip("Место слота: 0 — основное, 1.. — дополнительные (безделушки, CosmeticSlotConfig.ExtraSpots)")]
        [SerializeField] private int _spot;

        private CosmeticAssets _assets;
        private GameObject _instance;
        private PrefabPayload _instancePayload;
        private bool _instanceRecolored;
        private int _request;
        private UniTask _ready = UniTask.CompletedTask;

        public string SlotId => _slotId;
        public int Spot => _spot;
        public GameObject Instance => _instance;

        /// <summary>Модель в якоре сменилась (поставлена, заменена или убрана).</summary>
        public event Action<CosmeticAnchor> InstanceChanged;

#if UNITY_EDITOR
        public void EditorSetup(string slotId, int spot = 0)
        {
            _slotId = slotId;
            _spot = spot;
        }
#endif

        [Inject]
        public void Construct(CosmeticAssets assets)
        {
            _assets = assets;
        }

        /// <summary>Завершается, когда последний Show поставил модель (или не смог).</summary>
        public UniTask WhenReadyAsync() => _ready;

        public void Show(CosmeticItemConfig item, CosmeticItemConfig slotDefault)
        {
            var request = ++_request;
            var payload = item != null ? item.Payload : null;
            var model = payload is PrefabPayload own && own.HasModel ? own : BasePayload(slotDefault);
            var recolor = payload is MaterialPayload || payload is MeshMaterialPayload;

            // Та же модель уже стоит и не перекрашена: менять нечего (кроме нового перекрашивания).
            if (_instance != null && model == _instancePayload && !_instanceRecolored && !recolor)
            {
                _ready = UniTask.CompletedTask;
                return;
            }

            if (model == null)
            {
                Clear();
                _ready = UniTask.CompletedTask;
                return;
            }

            if (_assets != null && _assets.TryAcquire(model, out var loaded))
            {
                Place(model, loaded, payload);
                _ready = UniTask.CompletedTask;
                return;
            }

            // Готовность — через источник: её ждут несколько мест сразу (стол целиком, партия), Preserve так не умеет.
            var done = new UniTaskCompletionSource();
            _ready = done.Task;
            LoadAndPlaceAsync(request, model, payload, done).Forget();
        }

        private async UniTaskVoid LoadAndPlaceAsync(int request, PrefabPayload model, CosmeticPayload payload,
            UniTaskCompletionSource done)
        {
            try
            {
                await LoadAndPlaceAsync(request, model, payload);
            }
            finally
            {
                done.TrySetResult();
            }
        }

        private async UniTask LoadAndPlaceAsync(int request, PrefabPayload model, CosmeticPayload payload)
        {
            GameObject prefab;
            try
            {
                prefab = _assets != null
                    ? await _assets.AcquireAsync(model, destroyCancellationToken)
                    : model.Prefab;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (prefab == null)
                return;

            // Пока грузилось, попросили другое, или якорь уничтожен: загруженное не нужно.
            if (request != _request || this == null)
            {
                _assets?.Release(model);
                return;
            }

            Place(model, prefab, payload);
        }

        /// <summary>Поставить модель (префаб уже взят у CosmeticAssets) и перекрасить, если предмет — материал.</summary>
        private void Place(PrefabPayload model, GameObject prefab, CosmeticPayload payload)
        {
            Clear(false);
            _instance = Instantiate(prefab, transform, false);
            _instance.name = prefab.name;
            _instancePayload = model;
            _instanceRecolored = Recolor(payload);
            InstanceChanged?.Invoke(this);
        }

        private bool Recolor(CosmeticPayload payload)
        {
            switch (payload)
            {
                case MaterialPayload material when material.Material != null:
                    foreach (var renderer in _instance.GetComponentsInChildren<Renderer>())
                    {
                        var materials = renderer.sharedMaterials;
                        for (var i = 0; i < materials.Length; i++)
                            materials[i] = material.Material;
                        renderer.sharedMaterials = materials;
                    }

                    return true;

                case MeshMaterialPayload meshMaterial:
                    var filter = _instance.GetComponentInChildren<MeshFilter>();
                    if (filter != null && meshMaterial.Mesh != null)
                        filter.sharedMesh = meshMaterial.Mesh;
                    var target = filter != null ? filter.GetComponent<Renderer>() : null;
                    if (target != null && meshMaterial.Material != null)
                        target.sharedMaterial = meshMaterial.Material;
                    return true;

                default:
                    return false;
            }
        }

        private static PrefabPayload BasePayload(CosmeticItemConfig slotDefault)
        {
            return slotDefault != null && slotDefault.Payload is PrefabPayload prefab && prefab.HasModel ? prefab : null;
        }

        private void Clear(bool notify = true)
        {
            var had = _instance != null || _instancePayload != null;
            if (_instance != null)
                Destroy(_instance);
            _instance = null;
            if (_instancePayload != null)
                _assets?.Release(_instancePayload);
            _instancePayload = null;
            _instanceRecolored = false;
            if (had && notify)
                InstanceChanged?.Invoke(this);
        }

        private void OnDestroy()
        {
            _request++;
            if (_instancePayload != null)
                _assets?.Release(_instancePayload);
            _instancePayload = null;
        }
    }
}
