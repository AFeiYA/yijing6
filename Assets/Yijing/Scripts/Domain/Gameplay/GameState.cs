using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Yijing.Domain.Configuration;

namespace Yijing.Domain.Gameplay
{
    [Serializable]
    public sealed class GuideProgress
    {
        public int introStep, responsesSeen, preferredVariant;
        public bool choiceExplained, hasChosen, skipped, recycledOnce, undoneOnce;
        public GuideProgress Copy() => (GuideProgress)MemberwiseClone();
    }
    [Serializable]
    public sealed class ItemSlot
    {
        public long instanceId;
        public string itemId = "";
        public bool Empty => instanceId == 0;
        public ItemSlot Copy() => new ItemSlot { instanceId = instanceId, itemId = itemId };
    }

    [Serializable]
    public sealed class DailyRecord
    {
        public string date, upperBits, oracleKey = "";
        public int utcOffsetMinutes;
        public List<int> lines = new List<int>();
        public DailyRecord Copy() => new DailyRecord { date = date, upperBits = upperBits,
            oracleKey = oracleKey, utcOffsetMinutes = utcOffsetMinutes, lines = new List<int>(lines) };
    }

    [Serializable]
    public sealed class GameState
    {
        public int schemaVersion = 2;
        public string contentVersion;
        public long revision, nextInstanceId = 1;
        public ItemSlot[] board, inventory;
        public int stones, teaReserve, ceramicReserve, completedStory;
        public bool firstMerge, lampRepaired;
        public int[] regularOrders = { 0, 1 };
        public int nextRegularOrder = 2;
        public List<string> recentCommands = new List<string>();
        public List<DailyRecord> days = new List<DailyRecord>();
        public GuideProgress guide = new GuideProgress();
        public string[] storyChoices = { "", "", "" };

        public bool UpgradeLegacy()
        {
            if (schemaVersion != 1) return false;
            // Old progress remains authoritative. Do not invent past story choices from daily lines.
            guide = new GuideProgress { introStep = firstMerge || completedStory > 0 ? 2 : 0,
                choiceExplained = firstMerge, hasChosen = firstMerge, responsesSeen = completedStory };
            storyChoices = new[] { "", "", "" }; schemaVersion = 2; return true;
        }

        public static GameState New(PrototypeConfig config) => new GameState {
            contentVersion = config.contentVersion, stones = config.rules.initialStones,
            board = Enumerable.Range(0, config.rules.columns * config.rules.rows).Select(_ => new ItemSlot()).ToArray(),
            inventory = Enumerable.Range(0, config.rules.initialInventorySlots).Select(_ => new ItemSlot()).ToArray()
        };

        public GameState Copy() => new GameState {
            schemaVersion = schemaVersion, contentVersion = contentVersion, revision = revision,
            nextInstanceId = nextInstanceId, board = board.Select(x => x.Copy()).ToArray(),
            inventory = inventory.Select(x => x.Copy()).ToArray(), stones = stones,
            teaReserve = teaReserve, ceramicReserve = ceramicReserve, completedStory = completedStory,
            firstMerge = firstMerge, lampRepaired = lampRepaired,
            regularOrders = (int[])regularOrders.Clone(), nextRegularOrder = nextRegularOrder,
            recentCommands = new List<string>(recentCommands), days = days.Select(x => x.Copy()).ToList(),
            guide = guide.Copy(), storyChoices = (string[])storyChoices.Clone()
        };

        public void Validate(PrototypeConfig config)
        {
            void Require(bool valid, string reason) { if (!valid) throw new InvalidDataException(reason); }
            Require(schemaVersion == 2 && contentVersion == config.contentVersion, "Unsupported save version; original files are preserved.");
            Require(guide != null && guide.introStep >= 0 && guide.introStep <= 2 && guide.responsesSeen >= 0 &&
                guide.responsesSeen <= completedStory && guide.preferredVariant >= 0 && guide.preferredVariant <= 1, "Invalid guide progress.");
            Require(storyChoices != null && storyChoices.Length == GameSession.StoryOrderLimit &&
                storyChoices.All(x => x == "" || x == "quiet" || x == "act") &&
                storyChoices.Skip(completedStory).All(string.IsNullOrEmpty), "Invalid story choices.");
            Require(board != null && board.Length == config.rules.columns * config.rules.rows &&
                inventory != null && inventory.Length == config.rules.initialInventorySlots, "Invalid storage size.");
            Require(revision >= 0 && nextInstanceId > 0 && stones >= 0 && teaReserve >= 0 && ceramicReserve >= 0 &&
                completedStory >= 0 && completedStory <= GameSession.StoryOrderLimit, "Invalid progress.");
            var ids = new HashSet<long>();
            foreach (var slot in board.Concat(inventory))
            {
                Require(slot != null, "Missing slot.");
                Require(slot.Empty ? string.IsNullOrEmpty(slot.itemId) : slot.instanceId > 0 && slot.instanceId < nextInstanceId &&
                    config.items.Any(x => x.id == slot.itemId) && ids.Add(slot.instanceId), "Unknown or duplicate item; original save is preserved.");
            }
            Require(board.Skip(config.rules.initialOpenRows * config.rules.columns).All(x => x.Empty), "Item in locked slot.");
            Require(!lampRepaired || completedStory >= 1, "Invalid renovation.");
            Require(regularOrders != null && regularOrders.Length == 2 && regularOrders.All(x => x >= 0 && x < nextRegularOrder) &&
                regularOrders.Distinct().Count() == 2, "Invalid regular requests.");
            Require(recentCommands != null && recentCommands.Count <= 128 && recentCommands.All(x => !string.IsNullOrEmpty(x)) &&
                recentCommands.Distinct().Count() == recentCommands.Count, "Invalid command history.");
            Require(days != null && days.All(x => x != null) && days.Select(x => x.date).Distinct().Count() == days.Count, "Duplicate day.");
            foreach (var day in days)
            {
                Require(DateTime.TryParseExact(day.date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _) && day.upperBits == config.rules.prototypeUpperBits &&
                    Math.Abs(day.utcOffsetMinutes) <= 840 && day.lines != null && day.lines.Count <= 3 && day.lines.All(x => x == 0 || x == 1), "Invalid daily record.");
                var key = day.lines.Count == 3 ? string.Concat(day.lines) + "_" + day.upperBits : "";
                Require(day.oracleKey == key && (key == "" || config.oracleCards.Any(x => x.key == key)), "Invalid oracle mapping.");
            }
        }
    }

    public interface IGameStore
    {
        GameState Load();
        void Save(GameState state);
    }

    public readonly struct ActionResult
    {
        public bool Success { get; }
        public string Message { get; }
        public ActionResult(bool success, string message) { Success = success; Message = message; }
    }
}
