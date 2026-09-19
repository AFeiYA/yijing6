using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Yijing.Domain.Configuration;

namespace Yijing.Domain.Gameplay
{
    // First playable slice: E01–E03, four rotating regular templates, and the lamp.
    // Scene objects never own items. Each accepted command saves a full candidate before publishing it.
    public sealed class GameSession
    {
        public const int StoryOrderLimit = 3;
        private readonly PrototypeConfig config;
        private readonly IGameStore store;
        private readonly Func<DateTimeOffset> clock;
        private GameState state, undoRecycle;
        public GameState Snapshot => state.Copy();
        public bool CanUndo => undoRecycle != null;
        public int OpenSlots => config.rules.columns * config.rules.initialOpenRows;

        public GameSession(PrototypeConfig config, IGameStore store, Func<DateTimeOffset> clock)
        {
            this.config = config; this.store = store; this.clock = clock;
            ConfigValidator.Validate(config);
            state = store.Load();
            if (state == null) { state = GameState.New(config); state.Validate(config); store.Save(state); }
            state.Validate(config);
        }

        public ItemConfig Item(string id) => config.items.Single(x => x.id == id);
        public DailyRecord Today => state.days.FirstOrDefault(x => x.date == clock().ToString("yyyy-MM-dd"))?.Copy();
        public OracleCardConfig Card(DailyRecord day) => day == null ? null : config.oracleCards.FirstOrDefault(x => x.key == day.oracleKey);

        private ActionResult Commit(long expected, Func<GameState, string> change, bool recycling = false, string commandId = null)
        {
            if (expected != state.revision) return new ActionResult(false, "茶案已经变化，请再试一次。");
            if (commandId != null && state.recentCommands.Contains(commandId)) return new ActionResult(false, "这次操作已经完成。");
            var candidate = state.Copy();
            var error = change(candidate);
            if (error != null) return new ActionResult(false, error);
            candidate.revision++;
            if (commandId != null) {
                candidate.recentCommands.Add(commandId);
                if (candidate.recentCommands.Count > 128) candidate.recentCommands.RemoveAt(0);
            }
            candidate.Validate(config);
            try { store.Save(candidate); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
                return new ActionResult(false, "保存失败，未扣物品或发奖。请检查空间后重试。");
            }
            undoRecycle = recycling ? state.Copy() : null;
            state = candidate;
            return new ActionResult(true, "已保存");
        }

        public ActionResult Produce(string chain, long revision) => Commit(revision, s => {
            if (chain != "tea" && chain != "ceramic") return "没有这座生产台。";
            if (chain == "ceramic" && !s.firstMerge) return "先把两撮山茶合在一起，再打开器架。";
            int index = Array.FindIndex(s.board, 0, OpenSlots, x => x.Empty);
            if (index < 0) return "茶案满了，可合成、交付、储物或免费回收。";
            s.board[index] = new ItemSlot { instanceId = s.nextInstanceId++, itemId = chain + "_01" };
            if (chain == "tea" && s.teaReserve > 0) s.teaReserve--;
            if (chain == "ceramic" && s.ceramicReserve > 0) s.ceramicReserve--;
            return null;
        });

        public ActionResult Move(int from, int to, long instanceId, long revision) => Commit(revision, s => {
            if (from < 0 || from >= OpenSlots || to < 0 || to >= OpenSlots || from == to) return "已放回原位。";
            var source = s.board[from]; var target = s.board[to];
            if (source.Empty || source.instanceId != instanceId) return "这个物品已经移动。";
            var definition = Item(source.itemId);
            if (!target.Empty && target.itemId == source.itemId && !string.IsNullOrEmpty(definition.nextId)) {
                s.board[to] = new ItemSlot { instanceId = s.nextInstanceId++, itemId = definition.nextId };
                s.board[from] = new ItemSlot(); s.firstMerge = true;
            } else { s.board[from] = target; s.board[to] = source; }
            return null;
        });

        public ActionResult Store(int from, long instanceId, long revision) => Commit(revision, s => {
            if (from < 0 || from >= OpenSlots || s.board[from].Empty || s.board[from].instanceId != instanceId) return "先选中茶案上的物品。";
            int index = Array.FindIndex(s.inventory, x => x.Empty);
            if (index < 0) return "储物架满了，仍可免费放回工具台。";
            s.inventory[index] = s.board[from]; s.board[from] = new ItemSlot(); return null;
        });

        public ActionResult Retrieve(int from, long instanceId, long revision) => Commit(revision, s => {
            if (from < 0 || from >= s.inventory.Length || s.inventory[from].Empty || s.inventory[from].instanceId != instanceId) return "这里没有可取出的物品。";
            int index = Array.FindIndex(s.board, 0, OpenSlots, x => x.Empty);
            if (index < 0) return "先在茶案上腾出一格。";
            s.board[index] = s.inventory[from]; s.inventory[from] = new ItemSlot(); return null;
        });

        public ActionResult Recycle(int index, bool inventory, long instanceId, long revision) => Commit(revision, s => {
            var slots = inventory ? s.inventory : s.board;
            if (index < 0 || index >= (inventory ? slots.Length : OpenSlots) || slots[index].Empty || slots[index].instanceId != instanceId) return "先选中要放回的物品。";
            var item = Item(slots[index].itemId);
            if (item.chainId == "tea") s.teaReserve += item.baseUnits; else s.ceramicReserve += item.baseUnits;
            slots[index] = new ItemSlot(); return null;
        }, true);

        public ActionResult Undo(long revision)
        {
            if (undoRecycle == null) return new ActionResult(false, "没有可撤销的回收。");
            var previous = undoRecycle;
            return Commit(revision, s => {
                s.board = previous.board.Select(x => x.Copy()).ToArray();
                s.inventory = previous.inventory.Select(x => x.Copy()).ToArray();
                s.teaReserve = previous.teaReserve; s.ceramicReserve = previous.ceramicReserve;
                return null;
            });
        }

        public OrderConfig Order(int slot)
        {
            if (slot == 0) return state.completedStory < StoryOrderLimit ? config.orders[state.completedStory] : null;
            if (slot < 1 || slot > 2) return null;
            return Regular(state.regularOrders[slot - 1]);
        }

        public static string RegularTitle(int sequence) => new[] { "雨中歇脚", "窗边读书", "带茶回家", "整理茶席" }[sequence % 4];
        public static OrderConfig Regular(int sequence)
        {
            RequirementConfig R(string id, int n) => new RequirementConfig { itemId = id, quantity = n };
            var pairs = new[] {
                new[] { new[] { R("tea_02", 2) }, new[] { R("tea_02", 1), R("ceramic_02", 1) } },
                new[] { new[] { R("tea_03", 1), R("tea_02", 1) }, new[] { R("tea_02", 1), R("ceramic_03", 1) } },
                new[] { new[] { R("tea_03", 2) }, new[] { R("tea_03", 1), R("ceramic_03", 1) } },
                new[] { new[] { R("tea_03", 1), R("ceramic_03", 2) }, new[] { R("tea_03", 2), R("ceramic_03", 1) } }
            };
            int template = sequence % 4;
            return new OrderConfig { id = "R" + sequence, storyNode = "regular", rewardStones = new[] { 8, 12, 16, 24 }[template],
                variants = new[] { new VariantConfig { id = "quiet", lineValue = 0, requirements = pairs[template][0] },
                    new VariantConfig { id = "act", lineValue = 1, requirements = pairs[template][1] } } };
        }

        public ActionResult ReplaceRegular(int slot, string orderId, long revision) => Commit(revision, s => {
            if (slot < 1 || slot > 2 || Order(slot)?.id != orderId) return "请求已经变化。";
            s.regularOrders[slot - 1] = s.nextRegularOrder++; return null;
        });

        public long[] SelectMaterials(VariantConfig variant, bool includeInventory)
        {
            var available = state.board.Take(OpenSlots).Concat(includeInventory ? state.inventory : Array.Empty<ItemSlot>()).ToList();
            var ids = new List<long>();
            foreach (var requirement in variant.requirements) {
                var matches = available.Where(x => !x.Empty && x.itemId == requirement.itemId).Take(requirement.quantity).ToArray();
                if (matches.Length != requirement.quantity) return null;
                foreach (var item in matches) { ids.Add(item.instanceId); available.Remove(item); }
            }
            return ids.ToArray();
        }

        public ActionResult Deliver(int slot, string orderId, string variantId, long[] materialIds, bool includeInventory,
            string commandId, long revision) => Commit(revision, s => {
            if (!s.firstMerge) return "先完成第一次合成。";
            var order = Order(slot);
            if (order == null || order.id != orderId) return "这份请求已经完成或更换。";
            var variant = order.variants.FirstOrDefault(x => x.id == variantId);
            if (variant == null || materialIds == null || materialIds.Distinct().Count() != materialIds.Length || string.IsNullOrEmpty(commandId)) return "交付清单无效。";
            var available = s.board.Take(OpenSlots).Concat(includeInventory ? s.inventory : Array.Empty<ItemSlot>()).Where(x => !x.Empty).ToArray();
            var selected = available.Where(x => materialIds.Contains(x.instanceId)).ToArray();
            if (selected.Length != materialIds.Length || selected.Length != variant.requirements.Sum(x => x.quantity) ||
                variant.requirements.Any(r => selected.Count(x => x.itemId == r.itemId) != r.quantity)) return "材料尚未齐备，高阶物品不会自动代替低阶。";
            foreach (var item in selected) { item.itemId = ""; item.instanceId = 0; }
            s.stones += order.rewardStones;
            if (slot == 0) s.completedStory++; else s.regularOrders[slot - 1] = s.nextRegularOrder++;
            var now = clock(); string date = now.ToString("yyyy-MM-dd");
            var day = s.days.FirstOrDefault(x => x.date == date);
            if (day == null) { day = new DailyRecord { date = date, utcOffsetMinutes = (int)now.Offset.TotalMinutes, upperBits = config.rules.prototypeUpperBits }; s.days.Add(day); }
            if (day.lines.Count < config.rules.oracleOrdersPerDay) {
                day.lines.Add(variant.lineValue);
                if (day.lines.Count == 3) day.oracleKey = string.Concat(day.lines) + "_" + day.upperBits;
            }
            return null;
        }, commandId: commandId);

        public ActionResult RepairLamp(string commandId, long revision) => Commit(revision, s => {
            var repair = config.renovations.Single(x => x.id == "lamp");
            if (s.lampRepaired) return "门边灯已经点亮。";
            if (s.completedStory < 1) return "先为 Elena 完成第一份请求。";
            if (s.stones < repair.cost) return "还需要一些灵石。";
            if (string.IsNullOrEmpty(commandId)) return "操作标记无效。";
            s.stones -= repair.cost; s.lampRepaired = true; return null;
        }, commandId: commandId);
    }
}
