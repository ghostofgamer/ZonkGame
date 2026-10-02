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

        private readonly CountingNumber _counter = new CountingNumber();

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
            _counter.Configure(duration, punch, punchDuration);
        }

        public void SetScore(int score, bool active)
        {
            // Рост (игрок забрал очки) накручивается с подпрыгиванием; первое значение и сброс — сразу.
            if (_counter.SetTarget(score, isActiveAndEnabled))
                NumberText.Set(_score, null, score);

            _background.color = active ? UiColors.PanelLight : UiColors.Panel;
            transform.localScale = active ? Vector3.one * 1.06f : Vector3.one;
        }

        private void Update()
        {
            if (_counter.Tick(Time.deltaTime, _score.rectTransform))
                NumberText.Set(_score, null, _counter.Shown);
        }
    }

    /// <summary>
    /// Накрутка числа на экране: рост идёт плавно (сначала быстро, к концу медленнее) с подпрыгиванием надписи,
    /// первое значение и уменьшение (новый ход, Зонк) — сразу. Тикается из Update владельца, памяти не выделяет.
    /// </summary>
    public sealed class CountingNumber
    {
        private int _from;
        private float _countTime = -1f;
        private float _punchTime = -1f;
        private float _duration;
        private float _punch = 1f;
        private float _punchDuration = 0.25f;

        /// <summary>Число на экране сейчас; int.MinValue — ещё не показано.</summary>
        public int Shown { get; private set; } = int.MinValue;

        public int Target { get; private set; } = int.MinValue;

        public void Configure(float duration, float punch, float punchDuration)
        {
            _duration = duration;
            _punch = punch;
            _punchDuration = punchDuration;
        }

        /// <summary>Новое значение. true — показать сразу (Shown уже равно value), false — накрутка или без изменений.</summary>
        public bool SetTarget(int value, bool canAnimate)
        {
            if (value == Target)
                return false;

            Target = value;
            if (canAnimate && Shown != int.MinValue && value > Shown && _duration > 0f)
            {
                _from = Shown;
                _countTime = 0f;
                _punchTime = 0f;
                return false;
            }

            Jump(value);
            return true;
        }

        /// <summary>Сразу на значение, без накрутки.</summary>
        public void Jump(int value)
        {
            _countTime = -1f;
            Target = value;
            Shown = value;
        }

        /// <summary>Шаг накрутки и подпрыгивания punchTarget. true — число на экране надо обновить (Shown).</summary>
        public bool Tick(float deltaTime, Transform punchTarget)
        {
            var changed = false;
            if (_countTime >= 0f)
            {
                _countTime += deltaTime / _duration;
                var t = Mathf.Clamp01(_countTime);
                var value = Mathf.RoundToInt(Mathf.Lerp(_from, Target, 1f - (1f - t) * (1f - t)));
                if (value != Shown)
                {
                    Shown = value;
                    changed = true;
                }

                if (t >= 1f)
                    _countTime = -1f;
            }

            if (_punchTime >= 0f)
            {
                _punchTime += deltaTime / Mathf.Max(0.01f, _punchDuration);
                var p = Mathf.Clamp01(_punchTime);
                punchTarget.localScale = Vector3.one * (1f + (_punch - 1f) * Mathf.Sin(p * Mathf.PI));
                if (p >= 1f)
                    _punchTime = -1f;
            }

            return changed;
        }
    }
}
