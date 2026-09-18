using System;

namespace Yijing.Domain.Configuration
{
    // IDs are stable storage keys; display strings never identify gameplay objects.
    [Serializable]
    public sealed class PrototypeConfig
    {
        public int schemaVersion;
        public string contentVersion;
        public RulesConfig rules;
        public ItemConfig[] items;
        public ProducerConfig[] producers;
        public OrderConfig[] orders;
        public StoryNodeConfig[] storyNodes;
        public RenovationConfig[] renovations;
        public OracleCardConfig[] oracleCards;
    }

    [Serializable]
    public sealed class RulesConfig
    {
        public int columns, rows, initialOpenRows;
        public int initialInventorySlots, maxInventorySlots, maxTier;
        public int initialStones, stonesPerBaseUnit, oracleOrdersPerDay;
        public bool energyEnabled, producerCooldownEnabled;
        public string prototypeUpperBits, lineOrder;
        public string finalBoardRowUnlockOrder, inventoryExpansionOrder;
    }

    [Serializable]
    public sealed class ItemConfig
    {
        public string id, chainId, nextId, displayNameZh;
        public int tier, baseUnits;
    }

    [Serializable]
    public sealed class ProducerConfig
    {
        public string id, chainId, outputItemId, unlock;
    }

    [Serializable]
    public sealed class RequirementConfig
    {
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public sealed class VariantConfig
    {
        public string id;
        public int lineValue;
        public RequirementConfig[] requirements;
    }

    [Serializable]
    public sealed class OrderConfig
    {
        public string id, storyNode;
        public int rewardStones;
        public VariantConfig[] variants;
    }

    [Serializable]
    public sealed class StoryNodeConfig
    {
        public string id, prerequisiteRenovation;
        public string[] orderIds, prerequisiteStoryNodes;
    }

    [Serializable]
    public sealed class RenovationConfig
    {
        public string id, requiredAfterOrder, prerequisiteRenovation;
        public int cost, unlocksBoardRow;
        public bool mandatory;
    }

    [Serializable]
    public sealed class OracleCardConfig
    {
        public string key, lowerBits, upperBits, nameZh, titleZh, bodyZh, promptZh;
        public int kingWenId;
    }
}
