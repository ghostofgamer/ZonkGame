using System;
using System.Collections.Generic;
using Base.Services.Saves;
using Zonk.Configs;
using Zonk.Core.Rules;

namespace Zonk.Progress
{
    /// <summary>Комбинации, которые считает статистика (индекс в RecordsSave.Combos).</summary>
    public enum RecordedCombo
    {
        Straight,
        ThreePairs,
        ThreeOfAKind,
        FourOfAKind,
        FiveOfAKind,
        SixOfAKind,
    }

    [Serializable]
    public sealed class ModeRecord
    {
        public string Id;
        public int Matches;
        public int Wins;
    }

    [Serializable]
    public sealed class DieRecord
    {
        public string Id;
        public int Matches;
        public int Wins;
    }

    /// <summary>
    /// Статистика игрока (раздел zonk_records). Новые поля добавляются без миграции; переименовать — только с ISaveMigration.
    /// «Честные» броски — те, где все брошенные кости обычные: по ним считается удача.
    /// </summary>
    [Serializable]
    public sealed class RecordsSave
    {
        public int Matches;
        public int Wins;
        public int Surrenders;
        public int CurrentStreak;
        public int BestStreak;
        public int BossMatches;
        public int BossWins;
        public List<ModeRecord> Modes = new List<ModeRecord>();

        public int Rolls;
        public int Zonks;
        public int Banks;
        public long PointsBanked;
        public int BestTurn;
        public int HotDice;
        public int[] Combos = new int[6];

        /// <summary>Честные броски и Зонки в них по числу брошенных костей (индекс 1..6).</summary>
        public int[] FairRolls = new int[7];
        public int[] FairZonks = new int[7];

        /// <summary>Граней на обычных костях и сколько из них единиц и пятёрок.</summary>
        public long FairFaces;
        public long FairOnesFives;

        public List<DieRecord> Dice = new List<DieRecord>();
        public long SecondsPlayed;
    }

    /// <summary>
    /// Статистика игрока для окна профиля и достижений: партии, броски, комбинации, удача, кости.
    /// Считаются только партии против соперников (как опыт и мастерство). Запись хода не выделяет память.
    /// </summary>
    public interface IPlayerRecords
    {
        RecordsSave Data { get; }

        /// <summary>Бросок местного игрока: Зонк ли, сколько костей брошено, все ли обычные, единиц и пятёрок на обычных.</summary>
        void RecordRoll(bool zonk, int rolledDice, int standardDice, int standardOnesFives);

        void RecordKeep(ScoreResult score, bool hotDice);
        void RecordBank(int banked);

        /// <summary>Итог партии: режим (ID состояния стола), победа, сдача, босс, кости игрока, длительность.</summary>
        void RecordMatch(string mode, bool won, bool surrendered, bool vsBoss, IReadOnlyList<DieConfig> dice, float seconds);

        event Action Changed;
    }

    public sealed class PlayerRecords : IPlayerRecords
    {
        private readonly ISaveStore _saves;

        public PlayerRecords(ISaveStore saves)
        {
            _saves = saves;
        }

        public event Action Changed;

        public RecordsSave Data
        {
            get
            {
                var data = _saves.Get<RecordsSave>(SaveKeys.Records);
                // Старое сохранение без массивов (JsonUtility оставит null).
                if (data.Combos == null || data.Combos.Length < 6)
                    data.Combos = Resize(data.Combos, 6);
                if (data.FairRolls == null || data.FairRolls.Length < 7)
                    data.FairRolls = Resize(data.FairRolls, 7);
                if (data.FairZonks == null || data.FairZonks.Length < 7)
                    data.FairZonks = Resize(data.FairZonks, 7);
                return data;
            }
        }

        public void RecordRoll(bool zonk, int rolledDice, int standardDice, int standardOnesFives)
        {
            var data = Data;
            data.Rolls++;
            if (zonk)
                data.Zonks++;

            data.FairFaces += standardDice;
            data.FairOnesFives += standardOnesFives;
            if (rolledDice >= 1 && rolledDice <= 6 && standardDice == rolledDice)
            {
                data.FairRolls[rolledDice]++;
                if (zonk)
                    data.FairZonks[rolledDice]++;
            }

            _saves.RequestSave();
        }

        public void RecordKeep(ScoreResult score, bool hotDice)
        {
            var data = Data;
            if (hotDice)
                data.HotDice++;

            var combos = score != null ? score.Combos : null;
            for (var i = 0; combos != null && i < combos.Count; i++)
            {
                var kind = Classify(combos[i]);
                if (kind >= 0)
                    data.Combos[kind]++;
            }

            _saves.RequestSave();
        }

        public void RecordBank(int banked)
        {
            var data = Data;
            data.Banks++;
            data.PointsBanked += Math.Max(0, banked);
            data.BestTurn = Math.Max(data.BestTurn, banked);
            _saves.RequestSave();
        }

        public void RecordMatch(string mode, bool won, bool surrendered, bool vsBoss, IReadOnlyList<DieConfig> dice, float seconds)
        {
            var data = Data;
            data.Matches++;
            if (won)
            {
                data.Wins++;
                data.CurrentStreak++;
                data.BestStreak = Math.Max(data.BestStreak, data.CurrentStreak);
            }
            else
            {
                data.CurrentStreak = 0;
            }

            if (surrendered)
                data.Surrenders++;
            if (vsBoss)
            {
                data.BossMatches++;
                if (won)
                    data.BossWins++;
            }

            if (!string.IsNullOrEmpty(mode))
            {
                var record = data.Modes.Find(m => m.Id == mode);
                if (record == null)
                    data.Modes.Add(record = new ModeRecord { Id = mode });
                record.Matches++;
                if (won)
                    record.Wins++;
            }

            // Особые кости: в скольких партиях была и сколько побед (каждая кость — один раз за партию).
            for (var i = 0; dice != null && i < dice.Count; i++)
            {
                var die = dice[i];
                if (die == null || !die.IsSpecial || die.RunOnly || string.IsNullOrEmpty(die.Id) || IndexOf(dice, die) != i)
                    continue;

                var record = data.Dice.Find(d => d.Id == die.Id);
                if (record == null)
                    data.Dice.Add(record = new DieRecord { Id = die.Id });
                record.Matches++;
                if (won)
                    record.Wins++;
            }

            data.SecondsPlayed += (long)Math.Max(0f, seconds);
            _saves.RequestSave();
            Changed?.Invoke();
        }

        /// <summary>Вид комбинации для статистики; -1 — не считается (одиночные единицы и пятёрки, новые правила).</summary>
        public static int Classify(ScoringCombo combo)
        {
            if (combo == null)
                return -1;

            if (combo.Category == ComboCategory.Straight)
                return (int)RecordedCombo.Straight;
            if (combo.Category == ComboCategory.ThreePairs)
                return (int)RecordedCombo.ThreePairs;
            if (combo.Category != ComboCategory.OfAKind)
                return -1;

            var count = combo.DiceCount;
            return count >= 6 ? (int)RecordedCombo.SixOfAKind
                : count == 5 ? (int)RecordedCombo.FiveOfAKind
                : count == 4 ? (int)RecordedCombo.FourOfAKind
                : count == 3 ? (int)RecordedCombo.ThreeOfAKind
                : -1;
        }

        private static int IndexOf(IReadOnlyList<DieConfig> dice, DieConfig die)
        {
            for (var i = 0; i < dice.Count; i++)
            {
                if (dice[i] == die)
                    return i;
            }

            return -1;
        }

        private static int[] Resize(int[] source, int length)
        {
            var result = new int[length];
            if (source != null)
                Array.Copy(source, result, Math.Min(source.Length, length));
            return result;
        }
    }
}
