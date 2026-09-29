using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Кнопка языка (префаб Prefabs/UI/Parts/LanguageButton): флаг и название языка на нём самом («Русский»,
    /// «English»). Та же деталь — кнопка текущего языка в настройках и варианты в окне выбора языка.
    /// Флаги — UiConfig.LanguageFlags; пока флага нет, видно только название.
    /// </summary>
    public sealed class LanguageButtonView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _flag;
        [SerializeField] private TMP_Text _name;

        [Tooltip("Отметка выбранного языка")]
        [SerializeField] private GameObject _selected;

#if UNITY_EDITOR
        public void EditorSetup(Button button, Image flag, TMP_Text name, GameObject selected)
        {
            _button = button;
            _flag = flag;
            _name = name;
            _selected = selected;
        }
#endif

        public void Setup(string name, Sprite flag, bool selected, UnityAction onClick)
        {
            if (_name != null)
                _name.text = name;

            if (_flag != null)
            {
                _flag.sprite = flag;
                _flag.gameObject.SetActive(flag != null);
            }

            if (_selected != null)
                _selected.SetActive(selected);

            _button.onClick.RemoveAllListeners();
            if (onClick != null)
                _button.onClick.AddListener(onClick);
        }
    }
}
