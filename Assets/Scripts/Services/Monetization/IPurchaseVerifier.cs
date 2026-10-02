using System.Threading;
using Cysharp.Threading.Tasks;
using Base.Platform;

namespace Base.Services.Monetization
{
    /// <summary>
    /// Проверка покупки перед выдачей. Сейчас доверяем площадке (TrustingPurchaseVerifier): без своего сервера подпись
    /// покупки проверить негде — секретный ключ в клиенте был бы виден всем. Когда появится сервер, здесь подключается
    /// проверка подписи (Яндекс: getPayments({signed: true}) и ключ из консоли — только на сервере), игра не меняется.
    /// </summary>
    public interface IPurchaseVerifier
    {
        UniTask<bool> VerifyAsync(PurchaseInfo purchase, CancellationToken cancellationToken = default);
    }

    public sealed class TrustingPurchaseVerifier : IPurchaseVerifier
    {
        public UniTask<bool> VerifyAsync(PurchaseInfo purchase, CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(purchase != null && !string.IsNullOrEmpty(purchase.ProductId));
        }
    }
}
