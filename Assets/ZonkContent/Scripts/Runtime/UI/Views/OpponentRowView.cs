using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Строка соперника в окне кампании (префаб Prefabs/UI/Parts/OpponentRow): кнопка-фон, портрет, имя, состояние
    /// и звёзды (вложенный префаб Stars). Внешний вид целиком настраивается в префабе; любое поле, кроме кнопки,
    /// можно удалить — строка без него просто не покажет эту часть.
    /// </summary>
    public sealed class OpponentRowView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private Image _portrait;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private StarsView _stars;

#if UNITY_EDITOR
        public void EditorSetup(Button button, Image background, Image portrait, TMP_Text name, TMP_Text status, StarsView stars)
        {
            _button = button;
            _background = background;
            _portrait = portrait;
            _name = name;
            _status = status;
            _stars = stars;
        }
#endif

        public void Setup(string name, string status, Sprite portrait, int starMask, int starCount, UiConfig ui, Color color,
            bool interactable, UnityAction onClick)
        {
            if (_name != null)
                _name.text = name;

            if (_status != null)
            {
                _status.text = status;
                _status.gameObject.SetActive(!string.IsNullOrEmpty(status));
            }

            if (_portrait != null)
            {
                _portrait.sprite = portrait;
                _portrait.gameObject.SetActive(portrait != null);
            }

            if (_stars != null)
                _stars.Show(starMask, starCount, ui);

            if (_background != null)
                _background.color = color;

            _button.interactable = interactable;
            _button.onClick.RemoveAllListeners();
            if (onClick != null)
                _button.onClick.AddListener(onClick);
        }
    }
}
