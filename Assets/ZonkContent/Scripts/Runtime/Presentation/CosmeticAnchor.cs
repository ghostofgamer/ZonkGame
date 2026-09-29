using UnityEngine;
using Zonk.Configs;

namespace Zonk.Presentation
{
    /// <summary>
    /// Точка в сцене, куда ставится предмет косметики слота SlotId. Предмет с префабом заменяет модель,
    /// предмет с материалом перекрашивает базовую модель слота (префаб предмета по умолчанию).
    /// </summary>
    public sealed class CosmeticAnchor : MonoBehaviour
    {
        [SerializeField] private string _slotId;

        private GameObject _instance;
        private GameObject _instancePrefab;

        public string SlotId => _slotId;
        public GameObject Instance => _instance;

#if UNITY_EDITOR
        public void EditorSetup(string slotId)
        {
            _slotId = slotId;
        }
#endif

        public void Show(CosmeticItemConfig item, CosmeticItemConfig slotDefault)
        {
            var payload = item != null ? item.Payload : null;
            switch (payload)
            {
                case PrefabPayload prefab when prefab.Prefab != null:
                    Spawn(prefab.Prefab);
                    break;

                case MaterialPayload material:
                    SpawnBase(slotDefault);
                    if (_instance != null && material.Material != null)
                    {
                        foreach (var renderer in _instance.GetComponentsInChildren<Renderer>())
                        {
                            var materials = renderer.sharedMaterials;
                            for (var i = 0; i < materials.Length; i++)
                                materials[i] = material.Material;
                            renderer.sharedMaterials = materials;
                        }
                    }

                    // Модель перекрашена: следующий предмет с тем же префабом должен её пересоздать.
                    _instancePrefab = null;
                    break;

                case MeshMaterialPayload meshMaterial:
                    SpawnBase(slotDefault);
                    if (_instance != null)
                    {
                        var filter = _instance.GetComponentInChildren<MeshFilter>();
                        if (filter != null && meshMaterial.Mesh != null)
                            filter.sharedMesh = meshMaterial.Mesh;
                        var renderer = filter != null ? filter.GetComponent<Renderer>() : null;
                        if (renderer != null && meshMaterial.Material != null)
                            renderer.sharedMaterial = meshMaterial.Material;
                    }

                    _instancePrefab = null;
                    break;

                default:
                    SpawnBase(slotDefault);
                    break;
            }
        }

        private void SpawnBase(CosmeticItemConfig slotDefault)
        {
            var basePrefab = slotDefault != null && slotDefault.Payload is PrefabPayload prefab ? prefab.Prefab : null;
            if (basePrefab == null)
            {
                Clear();
                return;
            }

            Spawn(basePrefab);
        }

        private void Spawn(GameObject prefab)
        {
            if (_instance != null && _instancePrefab == prefab)
                return;

            Clear();
            _instance = Instantiate(prefab, transform, false);
            _instance.name = prefab.name;
            _instancePrefab = prefab;
        }

        private void Clear()
        {
            if (_instance != null)
                Destroy(_instance);
            _instance = null;
            _instancePrefab = null;
        }
    }
}
