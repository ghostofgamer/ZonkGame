using System.Threading;
using Cysharp.Threading.Tasks;

namespace Base.Services.Monetization
{
    public enum RewardOutcome
    {
        /// <summary>Игрок досмотрел рекламу.</summary>
        Rewarded,
        /// <summary>Награда выдана без рекламы по правилам RewardRules.</summary>
        Free,
        /// <summary>Игрок закрыл рекламу раньше времени.</summary>
        Closed,
        /// <summary>Рекламы нет, а выдавать без неё правила не разрешают.</summary>
        Unavailable,
    }

    public static class RewardOutcomeExtensions
    {
        /// <summary>Награду нужно выдать.</summary>
        public static bool IsGranted(this RewardOutcome outcome)
        {
            return outcome == RewardOutcome.Rewarded || outcome == RewardOutcome.Free;
        }
    }

    /// <summary>
    /// Награды для игры. Игра просит награду за место (placement) и получает ответ "выдать или нет".
    /// Как она получена, через рекламу или бесплатно, решают правила в MonetizationConfig.
    /// Пока реклама на площадке не подключена, игру можно писать и проверять целиком.
    /// </summary>
    public interface IRewardService
    {
        /// <summary>Показывать ли кнопку награды сейчас.</summary>
        bool CanOffer { get; }

        /// <summary>placement: место в игре, например "extra_reroll". Уходит в аналитику и логи.</summary>
        UniTask<RewardOutcome> RequestAsync(string placement, CancellationToken cancellationToken = default);
    }
}
