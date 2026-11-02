using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zonk.UI.Views
{
    /// <summary>Состояние строки пути уровней.</summary>
    public enum LevelRowState
    {
        /// <summary>Уровень пройден, награда получена.</summary>
        Reached,

        /// <summary>Следующий уровень: к нему идёт опыт.</summary>
        Next,

        /// <summary>Впереди.</summary>
        Ahead,
    }

    /// <summary>
    /// Строка пути уровней (деталь Parts/LevelRewardRow): номер уровня в значке, значок главной вещи (если есть),
    /// награды одной строкой, справа — «получено», «ещё N опыта» или пусто. Пройденный — приглушён,
    /// следующий — подсвечен золотом, вещь в награде — цветом её редкости.
    /// </summary>
    public sealed class LevelRewardRowView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _badge;
        [SerializeField] private TMP_Text _number;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _rewards;
        [SerializeField] private TMP_Text _state;

#if UNITY_EDITOR
        public void EditorSetup(Image background, Image badge, TMP_Text number, Image icon, TMP_Text rewards, TMP_Text state)
        {
            _background = background;
            _badge = badge;
            _number = number;
            _icon = icon;
            _rewards = rewards;
            _state = state;
        }
#endif

        public RectTransform Rect => (RectTransform)transform;

        /// <param name="accent">Цвет главной вещи (редкость) или золото, если вещи нет.</param>
        public void Show(int level, string rewards, string state, Sprite icon, LevelRowState rowState, Color accent, UiPalette palette)
        {
            _number.text = level.ToString();
            _rewards.text = rewards;
            if (_state != null)
            {
                _state.text = state;
                _state.color = rowState == LevelRowState.Reached ? palette.Good : palette.TextMuted;
            }

            var reached = rowState == LevelRowState.Reached;
            var next = rowState == LevelRowState.Next;
            _rewards.color = reached ? palette.TextMuted : icon != null ? accent : palette.Text;
            if (_badge != null)
                _badge.color = next ? palette.Gold : reached ? palette.Good : palette.ButtonMuted;
            _number.color = next || reached ? palette.Panel : palette.Text;

            if (_background != null)
            {
                var background = next ? palette.Gold : icon != null ? accent : Color.white;
                background.a = next ? 0.2f : icon != null ? 0.1f : 0.04f;
                _background.color = background;
            }

            if (_icon != null)
            {
                _icon.gameObject.SetActive(icon != null);
                _icon.sprite = icon;
                _icon.color = reached ? new Color(1f, 1f, 1f, 0.6f) : Color.white;
            }
        }
    }
}
