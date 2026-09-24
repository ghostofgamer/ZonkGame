using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Base.Platform
{
    public enum RewardedAdResult
    {
        /// <summary>Игрок досмотрел рекламу, награду нужно выдать.</summary>
        Rewarded,
        /// <summary>Игрок закрыл рекламу раньше времени.</summary>
        Closed,
        /// <summary>Реклама недоступна или не загрузилась.</summary>
        NotAvailable,
        /// <summary>Ошибка SDK.</summary>
        Failed,
    }

    /// <summary>Реклама: межстраничная и за вознаграждение.</summary>
    public interface IAdsService
    {
        bool IsInterstitialAvailable { get; }
        bool IsRewardedAvailable { get; }

        /// <summary>Реклама открылась. Здесь игра глушит звук и ставит паузу.</summary>
        event Action AdOpened;

        /// <summary>Реклама закрылась. Здесь игра возвращает звук и снимает паузу.</summary>
        event Action AdClosed;

        /// <summary>Показать межстраничную рекламу. Возвращает true, если реклама была показана.</summary>
        UniTask<bool> ShowInterstitialAsync(CancellationToken cancellationToken = default);

        /// <summary>Показать рекламу за вознаграждение. placement нужен для аналитики и настроек сетей.</summary>
        UniTask<RewardedAdResult> ShowRewardedAsync(string placement, CancellationToken cancellationToken = default);
    }
}
