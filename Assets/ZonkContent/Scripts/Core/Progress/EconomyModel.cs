using System;

namespace Zonk.Core.Progress
{
    /// <summary>Как играет типичный игрок: для оценки, как быстро он копит монеты.</summary>
    public sealed class PlayerProfile
    {
        public string Name;
        public double MatchesPerDay;
        public double WinRate;

        /// <summary>Какую долю побед игрок удваивает рекламой.</summary>
        public double DoubledShare;

        /// <summary>Сколько дневных заданий выполняет в день и недельных в неделю.</summary>
        public double DailyQuestsDone;
        public double WeeklyQuestsDone;

        /// <summary>Сколько очков игрок забирает за партию (идут в мастерство особых костей поровну).</summary>
        public double PointsPerMatch = 3000;

        public int SpecialDice = 6;

        /// <summary>Сколько раз в день берёт монеты за рекламу в главном меню.</summary>
        public double MenuAdsPerDay;
    }

    /// <summary>Доходы и расходы игры из конфигов (собирает окно Zonk/Economy Report).</summary>
    public sealed class EconomyInputs
    {
        public int StartCoins;

        /// <summary>Монеты за первые победы над всеми соперниками кампании (разово).</summary>
        public int CampaignFirstWinCoins;

        /// <summary>Сколько партий нужно, чтобы пройти кампанию (с поражениями).</summary>
        public int CampaignMatches;

        /// <summary>Средние монеты за повторную победу (соперники последней главы).</summary>
        public double RepeatWinCoins;

        public double DailyQuestCoins;
        public double WeeklyQuestCoins;

        /// <summary>Пороги мастерства и монеты за каждый уровень.</summary>
        public int[] MasteryPoints = Array.Empty<int>();
        public int[] MasteryCoins = Array.Empty<int>();

        /// <summary>Монеты за рекламу в главном меню и лимит просмотров в день.</summary>
        public int MenuAdCoins;
        public int MenuAdsPerDay;

        /// <summary>Сколько стоит за монеты всё, что за них продаётся.</summary>
        public int CatalogCoins;
    }

    public sealed class EconomyResult
    {
        public double CoinsPerDay;
        public double DaysToFinishCampaign;
        public double DaysToBuyAll;

        /// <summary>Через сколько дней каждая особая кость (из набора) берёт уровень мастерства.</summary>
        public double[] DaysToMasteryLevel = Array.Empty<double>();
    }

    /// <summary>
    /// Оценка экономики: дневной доход = повторные победы (с удвоением рекламой) + задания + мастерство,
    /// плюс разовые монеты стартовые и за кампанию. Грубая модель для сравнения вариантов, а не точный прогноз.
    /// </summary>
    public static class EconomyModel
    {
        public const int MaxDays = 3650;

        public static EconomyResult Estimate(EconomyInputs inputs, PlayerProfile profile)
        {
            var result = new EconomyResult();
            var matches = Math.Max(0.1, profile.MatchesPerDay);
            var wins = matches * profile.WinRate;
            var winCoins = wins * inputs.RepeatWinCoins * (1 + profile.DoubledShare);
            var questCoins = inputs.DailyQuestCoins * profile.DailyQuestsDone + inputs.WeeklyQuestCoins * profile.WeeklyQuestsDone / 7.0;
            var menuAdCoins = Math.Min(profile.MenuAdsPerDay, inputs.MenuAdsPerDay) * inputs.MenuAdCoins;

            // Очки партии делятся между шестью костями; мастерство копят только особые.
            var pointsPerDiePerDay = matches * profile.PointsPerMatch / 6.0;
            result.DaysToMasteryLevel = new double[inputs.MasteryPoints.Length];
            for (var i = 0; i < inputs.MasteryPoints.Length; i++)
                result.DaysToMasteryLevel[i] = pointsPerDiePerDay > 0 ? inputs.MasteryPoints[i] / pointsPerDiePerDay : double.PositiveInfinity;

            result.CoinsPerDay = winCoins + questCoins + menuAdCoins;
            result.DaysToFinishCampaign = inputs.CampaignMatches / matches;

            // По дням: доход + разовые монеты кампании (равномерно, пока идёт кампания) + мастерство по достижении уровня.
            double coins = inputs.StartCoins;
            var reached = new bool[inputs.MasteryPoints.Length];
            result.DaysToBuyAll = double.PositiveInfinity;
            for (var day = 1; day <= MaxDays; day++)
            {
                coins += result.CoinsPerDay;
                if (day <= Math.Ceiling(result.DaysToFinishCampaign))
                    coins += inputs.CampaignFirstWinCoins / Math.Max(1.0, Math.Ceiling(result.DaysToFinishCampaign));

                for (var i = 0; i < reached.Length; i++)
                {
                    if (!reached[i] && day >= result.DaysToMasteryLevel[i])
                    {
                        reached[i] = true;
                        coins += (double)inputs.MasteryCoins[i] * profile.SpecialDice;
                    }
                }

                if (coins >= inputs.CatalogCoins)
                {
                    result.DaysToBuyAll = day;
                    break;
                }
            }

            return result;
        }
    }
}
