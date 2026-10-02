using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Строка достижения (деталь Parts/AchievementRow): кубок цветом редкости (не открытое — серый), название, описание,
    /// полоса прогресса «37 / 100», награда. Открытое — ярко, полоса полная.
    /// </summary>
    public sealed class AchievementRowView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private RectTransform _fill;
        [SerializeField] private TMP_Text _progress;
        [SerializeField] private TMP_Text _reward;

#if UNITY_EDITOR
        public void EditorSetup(Image background, Image icon, TMP_Text title, TMP_Text description, RectTransform fill, TMP_Text progress,
            TMP_Text reward)
        {
            _background = background;
            _icon = icon;
            _title = title;
            _description = description;
            _fill = fill;
            _progress = progress;
            _reward = reward;
        }
#endif

        public void Show(string title, string description, long value, long target, string reward, bool unlocked, Color rarity,
            Color locked, Color text, Color muted)
        {
            _title.text = title;
            _title.color = unlocked ? rarity : text;
            _description.text = description;
            _description.color = muted;
            if (_icon != null)
                _icon.color = unlocked ? rarity : locked;
            if (_background != null)
            {
                var background = unlocked ? rarity : locked;
                background.a = unlocked ? 0.16f : 0.06f;
                _background.color = background;
            }

            var shown = unlocked ? target : value < target ? value : target;
            if (_progress != null)
                _progress.text = shown + " / " + target;
            if (_fill != null)
            {
                var max = _fill.anchorMax;
                max.x = target > 0 ? Mathf.Clamp01((float)shown / target) : 1f;
                _fill.anchorMax = max;
            }

            if (_reward != null)
                _reward.text = reward;
        }
    }
}
