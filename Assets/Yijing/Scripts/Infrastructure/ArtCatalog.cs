using System;
using UnityEngine;

namespace Yijing.Infrastructure
{
    [Serializable]
    public sealed class ItemArt
    {
        public string itemId;
        public Sprite sprite;
    }

    // Shared visual references only. Gameplay state remains in domain/save objects.
    public sealed class ArtCatalog : ScriptableObject
    {
        public Sprite sanctuaryBase;
        public Sprite elenaPortrait;
        public Sprite[] oracleBackgrounds;
        public ItemArt[] items;

        public Sprite FindItem(string itemId)
        {
            if (items == null) return null;
            foreach (var entry in items)
                if (entry != null && entry.itemId == itemId) return entry.sprite;
            return null;
        }
    }
}
