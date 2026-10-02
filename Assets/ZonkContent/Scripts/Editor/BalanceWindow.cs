using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Ai;
using Zonk.Core.Dice;
using Zonk.Core.Match;
using Zonk.Core.Simulation;

namespace Zonk.Editor
{
    /// <summary>
    /// Симулятор баланса на настоящих конфигах. Особые кости: набор из двух особых и четырёх обычных против
    /// шести обычных, цель — победы 47–53%. Соперники: профиль ИИ против «среднего игрока»,
    /// победы должны расти от первого соперника главы к боссу, но не выше ~60%: боссов должно быть реально обыграть.
    /// </summary>
    public sealed class BalanceWindow : EditorWindow
    {
        private const float DiceBandLow = 0.47f;
        private const float DiceBandHigh = 0.53f;

        private int _matches = 4000;
        private string _report = string.Empty;
        private Vector2 _scroll;

        [MenuItem("Zonk/Balance Simulator", priority = 20)]
        public static void Open()
        {
            GetWindow<BalanceWindow>("Zonk Balance");
        }

        /// <summary>«Средний игрок»: разумная игра с ошибками. Эталон для сложности соперников.</summary>
        public static AiProfile ReferencePlayer()
        {
            return new AiProfile
            {
                Selection = new GreedySelection(),
                Risk = new ThresholdRisk { BankAt = new[] { 300, 300, 400, 600, 1500, 100000 }, Jitter = 0.25f },
                MistakeChance = 0.12,
            };
        }

        private void OnGUI()
        {
            _matches = EditorGUILayout.IntSlider("Партий на проверку", _matches, 500, 30000);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Особые кости"))
                    _report = RunDice(_matches);
                if (GUILayout.Button("Соперники"))
                    _report = RunOpponents(_matches);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        public static string RunDice(int matches)
        {
            var database = ContentDatabaseBuilder.FindDatabase();
            if (database == null)
                return "ContentDatabase not found";

            var profile = new AiProfile { Selection = new ValueSelection { DieValue = 60 } };
            var text = new StringBuilder("Кость | победы 2 особых + 4 обычных против 6 обычных | очки за ход | Зонк\n");
            var dice = database.All<DieConfig>().Where(d => d.IsSpecial && !d.RunOnly).ToList();
            for (var i = 0; i < dice.Count; i++)
            {
                var die = dice[i];
                EditorUtility.DisplayProgressBar("Zonk Balance", die.name, (float)i / dice.Count);
                var loadout = new DieSpec[ZonkMatch.DiceCount];
                for (var slot = 0; slot < loadout.Length; slot++)
                    loadout[slot] = slot < 2 ? die.Spec : DieSpec.Standard;

                var settings = new MatchSettings
                {
                    Players = new List<PlayerSetup> { new PlayerSetup("S", loadout), new PlayerSetup("N", PlayerSetup.StandardDice()) },
                };
                var result = MatchSimulator.Run(settings, new[] { profile, profile }, matches, 99);
                var win = result.WinRate(0);
                var mark = win < DiceBandLow || win > DiceBandHigh ? "  <-- вне полосы 47–53%" : string.Empty;
                text.AppendLine($"{die.Id,-14} {win:P1}  {result.AverageTurnScore(0):F0}  {result.ZonkRate(0):P1}{mark}");
            }

            EditorUtility.ClearProgressBar();
            return text.ToString();
        }

        public static string RunOpponents(int matches)
        {
            var database = ContentDatabaseBuilder.FindDatabase();
            if (database == null)
                return "ContentDatabase not found";

            var reference = ReferencePlayer();
            var text = new StringBuilder("Глава / соперник | победы ИИ против среднего игрока | очки за ход | Зонк\n");
            var chapters = database.All<ChapterConfig>().OrderBy(c => c.Order).ToList();
            var total = chapters.Sum(c => c.Opponents.Count + c.DreadBosses.Count);
            var done = 0;

            foreach (var chapter in chapters)
            {
                text.AppendLine($"== {chapter.Id}");
                // Обычные соперники, затем грозные версии боссов.
                foreach (var opponent in chapter.Opponents.Concat(chapter.DreadBosses).Where(o => o != null && o.Ai != null))
                {
                    EditorUtility.DisplayProgressBar("Zonk Balance", opponent.name, (float)done++ / Mathf.Max(1, total));

                    var dice = new DieSpec[ZonkMatch.DiceCount];
                    for (var slot = 0; slot < dice.Length; slot++)
                        dice[slot] = slot < opponent.Dice.Count && opponent.Dice[slot] != null ? opponent.Dice[slot].Spec : DieSpec.Standard;

                    var settings = new MatchSettings
                    {
                        Players = new List<PlayerSetup> { new PlayerSetup("AI", dice), new PlayerSetup("P", PlayerSetup.StandardDice()) },
                        Modifiers = new List<Core.Modifiers.MatchModifier>(opponent.Modifiers.Where(m => m != null)),
                    };
                    // Цель — из конфига соперника, иначе из правил.
                    settings.Rules.TargetScore = opponent.TargetFor(settings.Rules.TargetScore);

                    var result = MatchSimulator.Run(settings, new[] { opponent.Ai.ToProfile(), reference }, matches, 5);
                    var boss = opponent.IsDread ? " ★ грозный" : opponent.IsBoss ? " ★" : string.Empty;
                    text.AppendLine($"{opponent.Id + boss,-22} {result.WinRate(0):P1}  {result.AverageTurnScore(0):F0}  {result.ZonkRate(0):P1}");
                }
            }

            EditorUtility.ClearProgressBar();
            return text.ToString();
        }
    }
}
