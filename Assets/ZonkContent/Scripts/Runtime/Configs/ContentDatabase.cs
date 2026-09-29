using System.Collections.Generic;
using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>
    /// Реестр всего контента. Заполняется в редакторе автоматически (Zonk/Content/Rebuild Database):
    /// все ассеты ContentConfig из папки игры. Руками список не правится.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Content Database", fileName = "ContentDatabase")]
    public sealed class ContentDatabase : ScriptableObject
    {
        [SerializeField] private List<ContentConfig> _items = new List<ContentConfig>();

        private Dictionary<string, ContentConfig> _byId;

        public IReadOnlyList<ContentConfig> Items => _items;

        public T Get<T>(string id) where T : ContentConfig
        {
            if (string.IsNullOrEmpty(id))
                return null;

            BuildIndex();
            return _byId.TryGetValue(id, out var item) ? item as T : null;
        }

        public List<T> All<T>() where T : ContentConfig
        {
            var result = new List<T>();
            foreach (var item in _items)
            {
                if (item is T typed)
                    result.Add(typed);
            }

            return result;
        }

        private void BuildIndex()
        {
            if (_byId != null)
                return;

            _byId = new Dictionary<string, ContentConfig>();
            foreach (var item in _items)
            {
                if (item == null || string.IsNullOrEmpty(item.Id))
                    continue;

                if (_byId.ContainsKey(item.Id))
                {
                    Debug.LogError($"[Content] Duplicate id '{item.Id}': {_byId[item.Id].name} and {item.name}");
                    continue;
                }

                _byId[item.Id] = item;
            }
        }

#if UNITY_EDITOR
        public void EditorSetItems(List<ContentConfig> items)
        {
            _items = items;
            _byId = null;
        }
#endif
    }
}
