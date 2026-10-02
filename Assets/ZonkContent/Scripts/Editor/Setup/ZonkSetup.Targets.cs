using UnityEditor;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Цели партий кампании (v3, 2026-09-30): раньше у всех 4000 из правил режима, это мало. Цель — просто число
    /// в конфиге каждого соперника (OpponentConfig.TargetScore), любое: нынешние боссы — 10000, будущий босс может
    /// быть и на 340000. Обычные соперники: первый 4000, дальше вразнобой, с общим ростом к концу кампании.
    ///
    /// Звёзды — точные числа в конфиге каждого соперника (что в конфиге, то и в игре), подобраны симулятором под
    /// его цель (обычные кости, ИИ по умолчанию; доля побед, в которых условие выполнено): глава 1 — около 75%,
    /// глава 2 — 60%, глава 3 — 50%, глава 4 — 40%, боссы — 35–50%. «Крупный ход», «горячие кости» и «без особых
    /// костей» не зависят от цели и остались авторскими. Сменил цель — подбери звёзды заново.
    /// Как и экономика: меняется, только если в ассете стоит прежнее значение генератора (0). Следующая
    /// перенастройка — новая таблица (v4 …), эту не удалять.
    /// </summary>
    public static partial class ZonkSetup
    {
        private static readonly (string id, int target)[] TargetsV3 =
        {
            ("opp_vitya", 4000), ("opp_klava", 5000), ("opp_petrovich", 4500), ("opp_semenych", 6000), ("opp_agafya", 10000),
            ("opp_lutik", 5500), ("opp_gustav", 7000), ("opp_irma", 5000), ("opp_zhora", 6500), ("opp_bo", 10000),
            ("opp_stepan", 6000), ("opp_zina", 8000), ("opp_max", 5500), ("opp_efim", 7000), ("opp_claw", 10000),
            ("opp_pit", 7500), ("opp_bart", 6000), ("opp_greta", 8000), ("opp_hook", 7000), ("opp_captain", 10000),
        };

        private enum StarKind
        {
            MaxTurns,
            Margin,
            MaxZonks,
        }

        /// <summary>Звёзды под новые цели: прежнее значение генератора → новое.</summary>
        private static readonly (string id, StarKind kind, int from, int to)[] StarsV3 =
        {
            ("opp_klava", StarKind.MaxTurns, 16, 10),
            ("opp_semenych", StarKind.MaxTurns, 14, 12),
            ("opp_agafya", StarKind.MaxZonks, 1, 3), ("opp_agafya", StarKind.Margin, 1000, 2400),
            ("opp_lutik", StarKind.MaxTurns, 13, 10),
            ("opp_irma", StarKind.Margin, 1500, 1300),
            ("opp_zhora", StarKind.MaxTurns, 11, 12),
            ("opp_bo", StarKind.MaxZonks, 1, 3),
            ("opp_stepan", StarKind.MaxTurns, 13, 10),
            ("opp_zina", StarKind.Margin, 1000, 2100),
            ("opp_efim", StarKind.MaxTurns, 11, 12), ("opp_efim", StarKind.Margin, 1500, 2000),
            ("opp_claw", StarKind.MaxZonks, 1, 2),
            ("opp_pit", StarKind.MaxZonks, 0, 1),
            ("opp_bart", StarKind.Margin, 2000, 2200),
            ("opp_greta", StarKind.MaxTurns, 10, 13),
            ("opp_hook", StarKind.MaxZonks, 1, 2),
            ("opp_captain", StarKind.MaxTurns, 11, 15), ("opp_captain", StarKind.Margin, 1500, 3100),
        };

        private static void RetuneTargets()
        {
            var opponents = ContentById<OpponentConfig>();

            var changed = 0;
            foreach (var (id, target) in TargetsV3)
            {
                // 0 = «из правил режима» (прежнее значение генератора): ручную цель не трогаем.
                if (opponents.TryGetValue(id, out var opponent) && opponent.TargetScore == 0)
                {
                    opponent.TargetScore = target;
                    changed += Dirty(opponent);
                }
            }

            foreach (var (id, kind, from, to) in StarsV3)
            {
                if (!opponents.TryGetValue(id, out var opponent))
                    continue;

                foreach (var star in opponent.StarConditions)
                {
                    if (RetuneStar(star, kind, from, to))
                    {
                        changed += Dirty(opponent);
                        break;
                    }
                }
            }

            if (changed > 0)
                Debug.Log($"[Setup] Campaign targets and stars v3 applied ({changed} changes)");
        }

        private static bool RetuneStar(StarCondition star, StarKind kind, int from, int to)
        {
            switch (star)
            {
                case MaxTurnsStar turns when kind == StarKind.MaxTurns && turns.Turns == from:
                    turns.Turns = to;
                    return true;
                case WinByMarginStar margin when kind == StarKind.Margin && margin.Margin == from:
                    margin.Margin = to;
                    return true;
                case MaxZonksStar zonks when kind == StarKind.MaxZonks && zonks.Zonks == from:
                    zonks.Zonks = to;
                    return true;
                default:
                    return false;
            }
        }
    }
}
