using Zonk.Core.Match;

namespace Zonk.Core.Progress
{
    /// <summary>
    /// Сколько очков хода принесла каждая кость. Очки отложенной комбинации делятся поровну между её костями.
    /// Засчитываются только забранные очки: Зонк обнуляет ход. Нужен мастерству костей.
    /// </summary>
    public sealed class TurnDiceScore
    {
        private readonly int[] _turn = new int[ZonkMatch.DiceCount];

        public void Reset()
        {
            for (var i = 0; i < _turn.Length; i++)
                _turn[i] = 0;
        }

        public void AddKeep(KeepOutcome keep)
        {
            if (keep == null || keep.KeptDice == null || keep.KeptDice.Count == 0 || keep.Score == null || keep.Score.Score <= 0)
                return;

            var share = keep.Score.Score / keep.KeptDice.Count;
            var rest = keep.Score.Score - share * keep.KeptDice.Count;
            for (var i = 0; i < keep.KeptDice.Count; i++)
            {
                var slot = keep.KeptDice[i];
                if (slot >= 0 && slot < _turn.Length)
                    _turn[slot] += share + (i < rest ? 1 : 0);
            }
        }

        /// <summary>Очки хода по слотам 0..5 после Bank; массив копируется, счётчик сбрасывается.</summary>
        public int[] Commit()
        {
            var result = (int[])_turn.Clone();
            Reset();
            return result;
        }
    }

    /// <summary>Уровень мастерства по очкам: пороги по возрастанию, 0 = ни одного порога.</summary>
    public static class MasteryMath
    {
        public static int LevelFor(int points, int[] thresholds)
        {
            var level = 0;
            if (thresholds == null)
                return level;

            foreach (var threshold in thresholds)
            {
                if (points >= threshold)
                    level++;
                else
                    break;
            }

            return level;
        }
    }
}
