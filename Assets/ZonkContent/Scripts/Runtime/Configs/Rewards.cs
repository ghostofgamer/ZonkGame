using System;

namespace Zonk.Configs
{
    /// <summary>
    /// Награда. Выдаётся через RewardGranter. Новый тип награды = новый наследник и строчка в RewardGranter.
    /// </summary>
    [Serializable]
    public abstract class Reward
    {
    }

    [Serializable]
    public sealed class CurrencyReward : Reward
    {
        public CurrencyConfig Currency;
        public int Amount = 100;
    }

    /// <summary>Открывает предмет: скин, стакан, локацию или особую кость.</summary>
    [Serializable]
    public sealed class ContentReward : Reward
    {
        public ContentConfig Item;
    }
}
