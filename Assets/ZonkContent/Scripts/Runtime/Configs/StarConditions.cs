using System;
using System.Collections.Generic;
using Zonk.Core.Match;

namespace Zonk.Configs
{
    /// <summary>Итог выигранной партии для условий звёзд: партия и кто из игроков — местный.</summary>
    public readonly struct StarContext
    {
        public StarContext(ZonkMatch match, int player, IReadOnlyList<DieConfig> dice)
        {
            Match = match;
            Player = player;
            Dice = dice;
        }

        public ZonkMatch Match { get; }
        public int Player { get; }

        /// <summary>Кости игрока в партии.</summary>
        public IReadOnlyList<DieConfig> Dice { get; }

        public MatchPlayer Me => Match.Players[Player];

        /// <summary>Лучший счёт среди соперников.</summary>
        public int BestOpponentScore
        {
            get
            {
                var best = 0;
                foreach (var player in Match.Players)
                {
                    if (player.Index != Player && player.Score > best)
                        best = player.Score;
                }

                return best;
            }
        }
    }

    /// <summary>
    /// Условие дополнительной звезды за соперника (OpponentConfig.StarConditions). Первая звезда — сама победа,
    /// следующие — по условиям, проверяются только при победе. Новое условие = новый наследник, в ассете
    /// соперника оно появится в списке. TextKey — текст условия, {0} — число из условия.
    /// </summary>
    [Serializable]
    public abstract class StarCondition
    {
        public abstract string TextKey { get; }

        /// <summary>Число для {0} в тексте условия.</summary>
        public virtual int TextValue => 0;

        public abstract bool IsMet(StarContext context);
    }

    /// <summary>Победить с отрывом не меньше Margin очков.</summary>
    [Serializable]
    public sealed class WinByMarginStar : StarCondition
    {
        public int Margin = 1000;

        public override string TextKey => "star.margin";
        public override int TextValue => Margin;

        public override bool IsMet(StarContext context)
        {
            return context.Me.Score - context.BestOpponentScore >= Margin;
        }
    }

    /// <summary>Победить только обычными костями.</summary>
    [Serializable]
    public sealed class NoSpecialDiceStar : StarCondition
    {
        public override string TextKey => "star.noSpecial";

        public override bool IsMet(StarContext context)
        {
            if (context.Dice == null)
                return true;

            foreach (var die in context.Dice)
            {
                if (die != null && die.IsSpecial)
                    return false;
            }

            return true;
        }
    }

    /// <summary>Победить не больше чем за Turns своих ходов.</summary>
    [Serializable]
    public sealed class MaxTurnsStar : StarCondition
    {
        public int Turns = 10;

        public override string TextKey => "star.maxTurns";
        public override int TextValue => Turns;

        public override bool IsMet(StarContext context)
        {
            return context.Me.TurnsPlayed <= Turns;
        }
    }

    /// <summary>Победить, получив не больше Zonks Зонков.</summary>
    [Serializable]
    public sealed class MaxZonksStar : StarCondition
    {
        public int Zonks = 2;

        public override string TextKey => Zonks == 0 ? "star.noZonks" : "star.maxZonks";
        public override int TextValue => Zonks;

        public override bool IsMet(StarContext context)
        {
            return context.Me.ZonkCount <= Zonks;
        }
    }

    /// <summary>За партию хотя бы раз забрать за ход не меньше Points очков.</summary>
    [Serializable]
    public sealed class BigTurnStar : StarCondition
    {
        public int Points = 1500;

        public override string TextKey => "star.bigTurn";
        public override int TextValue => Points;

        public override bool IsMet(StarContext context)
        {
            return context.Me.BestTurn >= Points;
        }
    }

    /// <summary>За партию получить горячие кости не меньше Count раз.</summary>
    [Serializable]
    public sealed class HotDiceStar : StarCondition
    {
        public int Count = 1;

        public override string TextKey => "star.hotDice";
        public override int TextValue => Count;

        public override bool IsMet(StarContext context)
        {
            return context.Me.HotDiceCount >= Count;
        }
    }
}
