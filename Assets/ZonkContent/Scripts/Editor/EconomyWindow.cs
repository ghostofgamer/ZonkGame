using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Progress;
using Zonk.Progress;

namespace Zonk.Editor
{
    /// <summary>
    /// Отчёт по экономике на настоящих конфигах: сколько стоит всё за монеты, сколько монет даёт игра
    /// (кампания, повторные победы с удвоением, задания, мастерство) и за сколько дней разные игроки всё купят.
    /// Проверять после любых изменений цен, наград и нового контента. Цель: активный игрок докупает то, что
    /// не даёт кампания, за 3–4 недели, случайный — за 2–3 месяца, но первая покупка — в первые дни.
    /// </summary>
    public sealed class EconomyWindow : EditorWindow
    {
        private const string CoinsId = "coins";

        /// <summary>Доля побед в кампании при первом прохождении: сколько партий уходит на кампанию.</summary>
        private const float CampaignWinRate = 0.5f;

        private readonly List<PlayerProfile> _profiles = new List<PlayerProfile>
        {
            new PlayerProfile { Name = "Случайный", MatchesPerDay = 6, WinRate = 0.5, DoubledShare = 0.3, DailyQuestsDone = 2, WeeklyQuestsDone = 1, MenuAdsPerDay = 1 },
            new PlayerProfile { Name = "Активный", MatchesPerDay = 15, WinRate = 0.55, DoubledShare = 1, DailyQuestsDone = 3, WeeklyQuestsDone = 3, MenuAdsPerDay = 6 },
            new PlayerProfile { Name = "Фанат", MatchesPerDay = 30, WinRate = 0.55, DoubledShare = 1, DailyQuestsDone = 3, WeeklyQuestsDone = 3, MenuAdsPerDay = 12 },
        };

        private string _report = string.Empty;
        private Vector2 _scroll;

        [MenuItem("Zonk/Economy Report", priority = 21)]
        public static void Open()
        {
            GetWindow<EconomyWindow>("Zonk Economy");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Игроки", EditorStyles.boldLabel);
            foreach (var profile in _profiles)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    profile.Name = EditorGUILayout.TextField(profile.Name, GUILayout.Width(100));
                    profile.MatchesPerDay = EditorGUILayout.FloatField("партий/день", (float)profile.MatchesPerDay);
                    profile.WinRate = EditorGUILayout.Slider("победы", (float)profile.WinRate, 0f, 1f);
                    profile.DoubledShare = EditorGUILayout.Slider("удваивает", (float)profile.DoubledShare, 0f, 1f);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(104);
                    profile.DailyQuestsDone = EditorGUILayout.FloatField("заданий/день", (float)profile.DailyQuestsDone);
                    profile.WeeklyQuestsDone = EditorGUILayout.FloatField("заданий/неделю", (float)profile.WeeklyQuestsDone);
                    profile.SpecialDice = EditorGUILayout.IntSlider("особых костей", profile.SpecialDice, 0, 6);
                    profile.MenuAdsPerDay = EditorGUILayout.FloatField("реклама в меню", (float)profile.MenuAdsPerDay);
                }
            }

            if (GUILayout.Button("Посчитать"))
                _report = BuildReport(_profiles);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        public static string BuildReport(IReadOnlyList<PlayerProfile> profiles)
        {
            ContentDatabaseBuilder.Rebuild();
            var database = ContentDatabaseBuilder.FindDatabase();
            var config = AssetDatabase.FindAssets("t:" + nameof(GameConfig), new[] { ContentDatabaseBuilder.GameFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(c => c != null);
            if (database == null || config == null)
                return "Нет ContentDatabase или GameConfig: запустите Zonk/Setup/Build Everything";

            var text = new StringBuilder();
            var chapters = database.All<ChapterConfig>();
            chapters.Sort((a, b) => a.Order.CompareTo(b.Order));
            var opponents = chapters.SelectMany(c => c.Opponents).Where(o => o != null).ToList();

            // Что дарит кампания: эти предметы можно не покупать.
            var campaignItems = new HashSet<ContentConfig>();
            var firstWinCoins = 0;
            foreach (var opponent in opponents)
            {
                foreach (var reward in opponent.FirstWinRewards)
                {
                    if (reward is ContentReward content && content.Item != null)
                        campaignItems.Add(content.Item);
                    firstWinCoins += Coins(reward);
                }
            }

            // Всё, что продаётся за монеты (наборы не считаются: их предметы уже в списке).
            var catalog = 0;
            var mustBuy = 0;
            var rows = new List<(string name, int price, bool fromCampaign)>();
            foreach (var item in database.Items)
            {
                if (item == null || item is ThemeSetConfig)
                    continue;

                var price = CoinPrice(Pricing.PriceOf(item));
                if (price <= 0)
                    continue;

                var fromCampaign = campaignItems.Contains(item);
                catalog += price;
                if (!fromCampaign)
                    mustBuy += price;
                rows.Add((item.name, price, fromCampaign));
            }

            var last = chapters.Count > 0 ? chapters[chapters.Count - 1].Opponents.Where(o => o != null).ToList() : opponents;
            var repeat = last.Count > 0 ? last.Average(o => (double)o.RepeatWinRewards.Sum(Coins)) : 0;

            var inputs = new EconomyInputs
            {
                StartCoins = config.Coins != null ? config.Coins.StartAmount : 0,
                CampaignFirstWinCoins = firstWinCoins,
                CampaignMatches = Mathf.CeilToInt(opponents.Count / CampaignWinRate),
                RepeatWinCoins = repeat,
                DailyQuestCoins = QuestCoins(database, QuestPeriod.Daily, config.DailyQuestCount),
                WeeklyQuestCoins = QuestCoins(database, QuestPeriod.Weekly, config.WeeklyQuestCount),
                MasteryPoints = config.MasteryLevels.Select(l => l != null ? l.Points : int.MaxValue).ToArray(),
                MasteryCoins = config.MasteryLevels.Select(l => l != null ? l.Rewards.Sum(Coins) : 0).ToArray(),
                MenuAdCoins = MenuCoins(config).Amount,
                MenuAdsPerDay = MenuCoins(config).PerDay > 0 ? MenuCoins(config).PerDay : int.MaxValue,
            };

            text.AppendLine($"Всё за монеты: {catalog}. Из них кампания дарит: {catalog - mustBuy}. Докупать: {mustBuy}.");
            text.AppendLine($"Разово: старт {inputs.StartCoins}, первые победы в кампании {firstWinCoins} ({opponents.Count} соперников).");
            text.AppendLine($"Повторная победа (последняя глава): {repeat:0} монет, с рекламой вдвое.");
            text.AppendLine($"Задание в среднем: дневное {inputs.DailyQuestCoins:0}, недельное {inputs.WeeklyQuestCoins:0}.");
            text.AppendLine($"Реклама в меню: {inputs.MenuAdCoins} монет, до {inputs.MenuAdsPerDay} раз в день. Ставки в модели не учтены: при честной выплате они в среднем не дают монет.");
            text.AppendLine($"Мастерство: {string.Join(" / ", inputs.MasteryPoints)} очков, {string.Join(" / ", inputs.MasteryCoins)} монет за уровень.");
            text.AppendLine();

            foreach (var profile in profiles)
            {
                inputs.CatalogCoins = mustBuy;
                var needed = EconomyModel.Estimate(inputs, profile);
                inputs.CatalogCoins = catalog;
                var all = EconomyModel.Estimate(inputs, profile);
                text.AppendLine($"{profile.Name}: {needed.CoinsPerDay:0} монет в день, кампания за {needed.DaysToFinishCampaign:0.#} дн.");
                text.AppendLine($"  докупить то, что не дарит кампания: {Days(needed.DaysToBuyAll)}; купить всё за монеты: {Days(all.DaysToBuyAll)}");
                text.AppendLine($"  мастерство кости по уровням: {string.Join(" / ", needed.DaysToMasteryLevel.Select(d => d.ToString("0.#")))} дн.");
            }

            text.AppendLine();
            text.AppendLine("Цены за монеты (* — дарит кампания):");
            foreach (var row in rows.OrderBy(r => r.price))
                text.AppendLine($"  {row.price,6}  {row.name}{(row.fromCampaign ? " *" : string.Empty)}");

            return text.ToString();
        }

        /// <summary>Кнопка монет за рекламу в меню (первая строка MenuAdOffers с монетами).</summary>
        private static MenuAdOffer MenuCoins(GameConfig config)
        {
            return config.MenuAdOffers.Find(o => o != null && o.Currency != null && o.Currency.Id == CoinsId) ?? new MenuAdOffer { Amount = 0, PerDay = 0 };
        }

        /// <summary>Сколько монет в среднем даёт одно задание периода: обязательные плюс остальные по весу.</summary>
        private static double QuestCoins(ContentDatabase database, QuestPeriod period, int count)
        {
            var quests = database.All<QuestConfig>().Where(q => q.Period == period && q.Goal != null).ToList();
            if (quests.Count == 0 || count <= 0)
                return 0;

            var guaranteed = quests.Where(q => q.Guaranteed).Take(count).ToList();
            var others = quests.Where(q => !q.Guaranteed && q.Weight > 0).ToList();
            var weight = others.Sum(q => q.Weight);
            var average = weight > 0 ? others.Sum(q => q.Weight * q.Rewards.Sum(Coins)) / weight : 0;
            var total = guaranteed.Sum(q => q.Rewards.Sum(Coins)) + average * (count - guaranteed.Count);
            return total / count;
        }

        private static int CoinPrice(Price price)
        {
            if (price == null)
                return 0;

            foreach (var option in price.Options)
            {
                if (option is CurrencyPriceOption currency && currency.Currency != null && currency.Currency.Id == CoinsId)
                    return currency.Amount;
            }

            return 0;
        }

        private static int Coins(Reward reward)
        {
            return reward is CurrencyReward currency && currency.Currency != null && currency.Currency.Id == CoinsId ? currency.Amount : 0;
        }

        private static string Days(double days)
        {
            return double.IsInfinity(days) ? "не накопит" : days.ToString("0") + " дн.";
        }
    }
}
