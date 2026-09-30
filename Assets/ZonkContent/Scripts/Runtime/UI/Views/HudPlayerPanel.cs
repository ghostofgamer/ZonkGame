using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zonk.Utils;

namespace Zonk.UI.Views
{
    /// <summary>Табличка игрока в HUD: портрет соперника, имя его цветом, счёт, подсветка того, чей ход.</summary>
    public sealed class HudPlayerPanel : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _score;
        [SerializeField] private Image _portrait;

        private int _shownScore = int.MinValue;
        private int _targetScore = int.MinValue;
        private int _countFrom;
        private float _countTime = -1f;
        private float _punchTime = -1f;
        private float _countDuration;
        private float _punch = 1f;
        private float _punchDuration = 0.25f;

#if UNITY_EDITOR
        public void EditorSetup(Image background, TMP_Text name, TMP_Text score, Image portrait)
        {
            _portrait = portrait;
            _background = background;
            _name = name;
            _score = score;
        }
#endif

        public void SetPlayer(string name, Color color)
        {
            _name.text = name;
            _name.color = color;
        }

        /// <summary>Портрет соперника; у людей портрета нет — место скрыто.</summary>
        public void SetPortrait(Sprite portrait)
        {
            if (_portrait == null)
                return;

            _portrait.sprite = portrait;
            _portrait.gameObject.SetActive(portrait != null);
        }

        /// <summary>Как накручивать рост счёта: длительность, подпрыгивание числа (масштаб) и его длительность. 0 — сразу.</summary>
        public void SetCounting(float duration, float punch, float punchDuration)
        {
            _countDuration = duration;
            _punch = punch;
            _punchDuration = punchDuration;
        }

        public void SetScore(int score, bool active)
        {
            if (score != _targetScore)
            {
                // Рост (игрок забрал очки) накручивается с подпрыгиванием; первое значение и сброс — сразу.
                if (_shownScore != int.MinValue && score > _shownScore && _countDuration > 0f && isActiveAndEnabled)
                {
                    _countFrom = _shownScore;
                    _countTime = 0f;
                    _punchTime = 0f;
                }
                else
                {
                    _countTime = -1f;
                    _shownScore = score;
                    NumberText.Set(_score, null, score);
                }

                _targetScore = score;
            }

            _background.color = active ? UiColors.PanelLight : UiColors.Panel;
            transform.localScale = active ? Vector3.one * 1.06f : Vector3.one;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (_countTime >= 0f)
            {
                _countTime += dt / _countDuration;
                var t = Mathf.Clamp01(_countTime);
                var value = Mathf.RoundToInt(Mathf.Lerp(_countFrom, _targetScore, 1f - (1f - t) * (1f - t)));
                if (value != _shownScore)
                {
                    _shownScore = value;
                    NumberText.Set(_score, null, value);
                }

                if (t >= 1f)
                    _countTime = -1f;
            }

            if (_punchTime >= 0f)
            {
                _punchTime += dt / Mathf.Max(0.01f, _punchDuration);
                var p = Mathf.Clamp01(_punchTime);
                _score.rectTransform.localScale = Vector3.one * (1f + (_punch - 1f) * Mathf.Sin(p * Mathf.PI));
                if (p >= 1f)
                    _punchTime = -1f;
            }
        }
    }
}
