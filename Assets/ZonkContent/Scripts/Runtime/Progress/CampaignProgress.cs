using System.Collections.Generic;
using Base.Services.Saves;
using Zonk.Configs;

namespace Zonk.Progress
{
    public enum OpponentState
    {
        Locked,
        Available,
        Beaten,
    }

    /// <summary>
    /// Прогресс кампании. Соперники открываются по порядку внутри главы,
    /// глава открывается после босса предыдущей. Побеждённых можно переигрывать.
    /// </summary>
    public interface ICampaignProgress
    {
        IReadOnlyList<ChapterConfig> Chapters { get; }
        bool IsChapterUnlocked(ChapterConfig chapter);
        OpponentState GetState(ChapterConfig chapter, OpponentConfig opponent);

        /// <summary>Отмечает победу. true: победа первая.</summary>
        bool MarkBeaten(OpponentConfig opponent);

        bool IsIntroSeen(ChapterConfig chapter);
        void MarkIntroSeen(ChapterConfig chapter);
        bool IsOutroSeen(ChapterConfig chapter);
        void MarkOutroSeen(ChapterConfig chapter);

        /// <summary>Первая глава, в которой есть непобеждённый открытый соперник, иначе последняя.</summary>
        ChapterConfig CurrentChapter { get; }

        /// <summary>Звёзды соперника маской: бит 0 — победа, бит N — условие N-1.</summary>
        int GetStarMask(OpponentConfig opponent);

        /// <summary>Добавить звёзды; возвращает маску новых (раньше не полученных).</summary>
        int AddStars(OpponentConfig opponent, int mask);

        /// <summary>Сколько звёзд собрано во всей кампании, с грозными боссами (для таблицы лидеров и наград).</summary>
        int TotalStars { get; }

        /// <summary>Грозная версия босса открыта: у обычного босса собрано DreadStarsRequired звёзд.</summary>
        bool IsDreadUnlocked(OpponentConfig dread);
    }

    public sealed class CampaignProgress : ICampaignProgress
    {
        private readonly ISaveStore _saves;
        private readonly List<ChapterConfig> _chapters;

        public CampaignProgress(ISaveStore saves, ContentDatabase content)
        {
            _saves = saves;
            _chapters = content.All<ChapterConfig>();
            _chapters.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        private CampaignSave Data => _saves.Get<CampaignSave>(SaveKeys.Campaign);

        public IReadOnlyList<ChapterConfig> Chapters => _chapters;

        public bool IsChapterUnlocked(ChapterConfig chapter)
        {
            var index = _chapters.IndexOf(chapter);
            if (index <= 0)
                return index == 0;

            var previous = _chapters[index - 1];
            return previous.Opponents.Count == 0 || IsBeaten(previous.Opponents[previous.Opponents.Count - 1]);
        }

        public OpponentState GetState(ChapterConfig chapter, OpponentConfig opponent)
        {
            if (IsBeaten(opponent))
                return OpponentState.Beaten;

            if (!IsChapterUnlocked(chapter))
                return OpponentState.Locked;

            // Грозная версия босса: открывается звёздами у обычного босса, а не порядком в главе.
            if (chapter.DreadBosses.Contains(opponent))
                return IsDreadUnlocked(opponent) ? OpponentState.Available : OpponentState.Locked;

            var index = chapter.Opponents.IndexOf(opponent);
            if (index < 0)
                return OpponentState.Locked;
            if (index == 0)
                return OpponentState.Available;

            return IsBeaten(chapter.Opponents[index - 1]) ? OpponentState.Available : OpponentState.Locked;
        }

        public bool IsDreadUnlocked(OpponentConfig dread)
        {
            if (dread == null || dread.DreadOf == null)
                return dread != null;

            return CountBits(GetStarMask(dread.DreadOf)) >= dread.DreadStarsRequired;
        }

        public bool MarkBeaten(OpponentConfig opponent)
        {
            if (opponent == null || IsBeaten(opponent))
                return false;

            Data.Beaten.Add(opponent.Id);
            _saves.RequestSave();
            return true;
        }

        public int GetStarMask(OpponentConfig opponent)
        {
            if (opponent == null)
                return 0;

            foreach (var entry in Data.Stars)
            {
                if (entry.Id == opponent.Id)
                    return entry.Mask;
            }

            // Сохранение до звёзд: побеждённый соперник уже имеет первую звезду.
            return IsBeaten(opponent) ? 1 : 0;
        }

        public int AddStars(OpponentConfig opponent, int mask)
        {
            if (opponent == null || mask == 0)
                return 0;

            var old = GetStarMask(opponent);
            var gained = mask & ~old;
            if (gained == 0)
                return 0;

            var data = Data;
            var entry = data.Stars.Find(e => e.Id == opponent.Id);
            if (entry == null)
                data.Stars.Add(entry = new StarEntry { Id = opponent.Id });
            entry.Mask = old | mask;
            _saves.RequestSave();
            return gained;
        }

        public int TotalStars
        {
            get
            {
                var total = 0;
                foreach (var chapter in _chapters)
                {
                    foreach (var opponent in chapter.Opponents)
                        total += CountBits(GetStarMask(opponent));
                    foreach (var dread in chapter.DreadBosses)
                        total += CountBits(GetStarMask(dread));
                }

                return total;
            }
        }

        public static int CountBits(int mask)
        {
            var count = 0;
            for (; mask != 0; mask &= mask - 1)
                count++;
            return count;
        }

        public bool IsIntroSeen(ChapterConfig chapter) => Data.SeenIntros.Contains(chapter.Id);

        public void MarkIntroSeen(ChapterConfig chapter)
        {
            if (!IsIntroSeen(chapter))
            {
                Data.SeenIntros.Add(chapter.Id);
                _saves.RequestSave();
            }
        }

        public bool IsOutroSeen(ChapterConfig chapter) => Data.SeenOutros.Contains(chapter.Id);

        public void MarkOutroSeen(ChapterConfig chapter)
        {
            if (!IsOutroSeen(chapter))
            {
                Data.SeenOutros.Add(chapter.Id);
                _saves.RequestSave();
            }
        }

        public ChapterConfig CurrentChapter
        {
            get
            {
                foreach (var chapter in _chapters)
                {
                    if (!IsChapterUnlocked(chapter))
                        break;

                    foreach (var opponent in chapter.Opponents)
                    {
                        if (!IsBeaten(opponent))
                            return chapter;
                    }
                }

                return _chapters.Count > 0 ? _chapters[_chapters.Count - 1] : null;
            }
        }

        private bool IsBeaten(OpponentConfig opponent)
        {
            return opponent != null && Data.Beaten.Contains(opponent.Id);
        }
    }
}
