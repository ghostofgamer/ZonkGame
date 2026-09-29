using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Кнопка в префабе окна: сама кнопка, подпись, фон. Шаблон кнопки (неактивный объект в префабе)
    /// клонируется для списков через Spawn: стиль всех кнопок списка меняется в одном месте.
    /// </summary>
    public sealed class UiButtonView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Image _background;

        public Button Button => _button;
        public TMP_Text Label => _label;
        public Image Background => _background;

        public bool Interactable
        {
            get => _button.interactable;
            set => _button.interactable = value;
        }

#if UNITY_EDITOR
        public void EditorSetup(Button button, TMP_Text label, Image background)
        {
            _button = button;
            _label = label;
            _background = background;
        }
#endif

        public void SetText(string text)
        {
            if (_label != null)
                _label.text = text;
        }

        public void SetColor(Color color)
        {
            if (_background != null)
                _background.color = color;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        /// <summary>Заменить обработчик нажатия (у клонов шаблона не остаётся чужих подписок).</summary>
        public void OnClick(UnityAction action)
        {
            _button.onClick.RemoveAllListeners();
            if (action != null)
                _button.onClick.AddListener(action);
        }

        /// <summary>Копия шаблона в parent, включённая.</summary>
        public UiButtonView Spawn(Transform parent)
        {
            var copy = Instantiate(this, parent);
            copy.gameObject.SetActive(true);
            return copy;
        }
    }
}
