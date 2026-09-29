using Zonk.Core.Match;

namespace Zonk.Core.Ai
{
    /// <summary>Что видит ИИ при решении. Только открытые данные партии: будущих бросков ИИ не знает.</summary>
    public readonly struct AiContext
    {
        public AiContext(ZonkMatch match)
        {
            Match = match;

            var me = match.CurrentPlayer;
            var bestOther = 0;
            foreach (var player in match.Players)
            {
                if (player.Index != me.Index && player.Score > bestOther)
                    bestOther = player.Score;
            }

            MyScore = me.Score;
            BestOpponentScore = bestOther;
            TargetScore = match.Rules.TargetScore;
        }

        public ZonkMatch Match { get; }
        public int MyScore { get; }
        public int BestOpponentScore { get; }
        public int TargetScore { get; }

        /// <summary>Отставание от лидера в долях цели: плюс значит отстаём, минус значит впереди.</summary>
        public double Deficit => TargetScore > 0 ? (double)(BestOpponentScore - MyScore) / TargetScore : 0;
    }
}
