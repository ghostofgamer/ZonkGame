namespace Zonk.Progress
{
    /// <summary>
    /// Что знают условия и цели заданий об игроке в момент выдачи. Новое знание (уровень, покупки…) добавляется
    /// сюда свойством, заполняется в QuestService.Context, и его сразу видят все условия.
    /// </summary>
    public sealed class QuestContext
    {
        public IInventory Inventory;
        public ICampaignProgress Campaign;

        /// <summary>Награду за рекламу можно получить прямо сейчас (на VK: реклама предзагружена). Меняется со временем.</summary>
        public bool AdsAvailable;
    }
}
