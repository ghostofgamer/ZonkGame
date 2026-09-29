using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Match;

namespace Zonk.Presentation
{
    /// <summary>
    /// Шесть костей на столе. Кости общие для всех игроков: в начале хода они получают скин и метки
    /// костей того, кто ходит. Номер кости = номер слота в наборе игрока и в ZonkMatch.
    /// </summary>
    public sealed class DiceSetView : MonoBehaviour
    {
        [SerializeField] private List<DieView> _dice = new List<DieView>();

        private readonly List<int> _selection = new List<int>();

        public event Action<IReadOnlyList<int>> SelectionChanged;

        public IReadOnlyList<DieView> Dice => _dice;
        public IReadOnlyList<int> Selection => _selection;

#if UNITY_EDITOR
        public void EditorSetup(List<DieView> dice)
        {
            _dice = dice;
        }
#endif

        private void Awake()
        {
            for (var i = 0; i < _dice.Count; i++)
            {
                _dice[i].Init(i);
                _dice[i].Clicked += OnDieClicked;
            }
        }

        private void OnDestroy()
        {
            foreach (var die in _dice)
            {
                if (die != null)
                    die.Clicked -= OnDieClicked;
            }
        }

        public DieView this[int slot] => _dice[slot];

        /// <summary>Модель кости сделана с ребром baseSize; размер на столе задаёт GameConfig.DieSize.</summary>
        public void SetDieSize(float size, float baseSize = 0.3f)
        {
            if (size <= 0f || baseSize <= 0f)
                return;

            foreach (var die in _dice)
                die.transform.localScale = Vector3.one * (size / baseSize);
        }

        public void SetSkin(CosmeticItemConfig skin)
        {
            foreach (var die in _dice)
                die.SetSkin(skin);
        }

        /// <summary>
        /// Особые кости набора: метка и свой вид (перекрывает скин, поэтому вызывать после SetSkin).
        /// dice: по слотам, null = обычная. mastery: уровни мастерства по слотам, levels — их описание (GameConfig).
        /// </summary>
        public void SetLoadout(IReadOnlyList<DieConfig> dice, IReadOnlyList<int> mastery = null,
            IReadOnlyList<MasteryLevel> levels = null)
        {
            for (var i = 0; i < _dice.Count; i++)
            {
                var config = dice != null && i < dice.Count ? dice[i] : null;
                var level = mastery != null && i < mastery.Count ? mastery[i] : 0;
                _dice[i].SetMarker(config != null && config.IsSpecial ? config.MarkerColor : Color.clear);
                _dice[i].SetGlow(level > 0 && levels != null && level <= levels.Count && levels[level - 1] != null
                    ? levels[level - 1].Glow
                    : 1f);
                if (config != null)
                    _dice[i].SetLook(config.LookMesh, LookFor(config, level));
            }
        }

        /// <summary>Вид кости на уровне мастерства: свой материал уровня, иначе обычный вид особой кости.</summary>
        public static Material LookFor(DieConfig die, int level)
        {
            if (die == null)
                return null;

            for (var i = Mathf.Min(level, die.MasteryLooks.Count); i >= 1; i--)
            {
                if (die.MasteryLooks[i - 1] != null)
                    return die.MasteryLooks[i - 1];
            }

            return die.LookMaterial;
        }

        public void SetVisible(bool visible)
        {
            foreach (var die in _dice)
                die.SetVisible(visible);
        }

        /// <summary>Разрешить выбор костей, которые сейчас в руке и брошены.</summary>
        public void EnableSelection(ZonkMatch match)
        {
            ClearSelection(false);
            for (var i = 0; i < _dice.Count; i++)
                _dice[i].Interactable = match.IsInHand(i) && match.Faces[i] > 0;
        }

        public void DisableSelection()
        {
            foreach (var die in _dice)
                die.Interactable = false;
        }

        public void ClearSelection(bool notify = true)
        {
            _selection.Clear();
            foreach (var die in _dice)
                die.SetSelected(false);
            if (notify)
                SelectionChanged?.Invoke(_selection);
        }

        /// <summary>Выбор кости со стороны (ИИ показывает, что откладывает).</summary>
        public void Select(int slot, bool selected)
        {
            if (selected && !_selection.Contains(slot))
                _selection.Add(slot);
            else if (!selected)
                _selection.Remove(slot);

            _dice[slot].SetSelected(selected);
            SelectionChanged?.Invoke(_selection);
        }

        private void OnDieClicked(DieView die)
        {
            Select(die.Slot, !_selection.Contains(die.Slot));
        }
    }
}
