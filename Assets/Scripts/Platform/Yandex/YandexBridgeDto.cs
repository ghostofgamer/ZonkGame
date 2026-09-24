using System;

namespace Base.Platform.Yandex
{
    /// <summary>
    /// Классы для разбора ответов моста через JsonUtility.
    /// Имена полей должны совпадать с тем, что кладёт YandexBridge.jslib.
    /// </summary>
    [Serializable]
    internal sealed class BridgeResponseDto
    {
        public int id;
        public bool ok;
        /// <summary>Полезная нагрузка, сама по себе строка с JSON.</summary>
        public string result;
        public string error;
    }

    [Serializable]
    internal sealed class PlayerInfoDto
    {
        public string lang;
        /// <summary>deviceInfo.type: desktop, mobile, tablet или tv.</summary>
        public string device;
        public bool isAuthorized;
        public string playerId;
        public string playerName;
    }

    [Serializable]
    internal sealed class InterstitialResultDto
    {
        public bool shown;
    }

    [Serializable]
    internal sealed class RewardedResultDto
    {
        public bool rewarded;
        public bool shown;
    }

    [Serializable]
    internal sealed class ProductDto
    {
        public string id;
        public string title;
        public string description;
        public string price;
        public string imageURI;
    }

    [Serializable]
    internal sealed class ProductListDto
    {
        public ProductDto[] items;
    }

    [Serializable]
    internal sealed class PurchaseDto
    {
        public string productID;
        public string purchaseToken;
    }

    [Serializable]
    internal sealed class PurchaseListDto
    {
        public PurchaseDto[] items;
    }

    [Serializable]
    internal sealed class SaveDataDto
    {
        public string json;
    }

    [Serializable]
    internal sealed class EntryDto
    {
        public int rank;
        public double score;
        public string playerName;
        public bool isCurrentPlayer;
    }

    [Serializable]
    internal sealed class EntryListDto
    {
        public EntryDto[] items;
    }

    [Serializable]
    internal sealed class PlayerEntryDto
    {
        public bool found;
        public int rank;
        public double score;
        public string playerName;
        public bool isCurrentPlayer;
    }
}
