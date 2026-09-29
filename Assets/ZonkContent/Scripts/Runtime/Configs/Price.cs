using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>
    /// Как получить предмет. Несколько вариантов: например, «500 монет» или «покупка набора».
    /// Пустой список = предмет бесплатный и есть у всех.
    /// </summary>
    [Serializable]
    public sealed class Price
    {
        [SerializeReference, SubclassSelector]
        public List<PriceOption> Options = new List<PriceOption>();

        public bool IsFree => Options == null || Options.Count == 0;
    }

    /// <summary>Один способ получить предмет. Новый способ = новый наследник и его обработка в ShopService.</summary>
    [Serializable]
    public abstract class PriceOption
    {
    }

    /// <summary>За валюту.</summary>
    [Serializable]
    public sealed class CurrencyPriceOption : PriceOption
    {
        public CurrencyConfig Currency;
        public int Amount = 100;
    }

    /// <summary>За просмотры рекламы: прогресс копится, нужно AdsRequired просмотров.</summary>
    [Serializable]
    public sealed class RewardedAdPriceOption : PriceOption
    {
        public int AdsRequired = 3;
    }

    /// <summary>За реальные деньги: ID товара как в консоли площадки и в MonetizationConfig.</summary>
    [Serializable]
    public sealed class PurchasePriceOption : PriceOption
    {
        public string ProductId;
    }

    /// <summary>Не продаётся: выдаётся наградой (победа над боссом, глава). HintKey подсказывает, где взять.</summary>
    [Serializable]
    public sealed class ProgressPriceOption : PriceOption
    {
        public string HintKey;
    }
}
