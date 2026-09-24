using System.Threading;
using Cysharp.Threading.Tasks;

namespace Base.Services.Monetization
{
    /// <summary>
    /// Межстраничная реклама по правилам. Игра только сообщает о поводе ("партия закончилась",
    /// "перезапуск"), а показывать ли рекламу, решает сервис: не сразу после запуска,
    /// не чаще заданного интервала, не игрокам с правом "без рекламы".
    /// Числа задаются в MonetizationConfig.Interstitial.
    /// </summary>
    public interface IInterstitialService
    {
        /// <summary>Повод для рекламы. Возвращает true, если реклама была показана.</summary>
        UniTask<bool> TryShowAsync(string trigger, CancellationToken cancellationToken = default);
    }
}
