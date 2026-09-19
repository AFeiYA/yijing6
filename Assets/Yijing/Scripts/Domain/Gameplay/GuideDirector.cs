using System.Linq;
using Yijing.Domain.Configuration;

namespace Yijing.Domain.Gameplay
{
    public sealed class GuideCue
    {
        public string title, instruction, target;
        public int from = -1, to = -1;
    }

    // Derived from live state, never from a fixed board coordinate or elapsed tutorial timer.
    public static class GuideDirector
    {
        private static GuideCue Cue(string title, string instruction, string target, int from = -1, int to = -1) =>
            new GuideCue { title = title, instruction = instruction, target = target, from = from, to = to };

        public static GuideCue Next(GameSession session, OrderConfig order, int variant, int selected = -1, int recoveryStage = -1)
        {
            var s = session.Snapshot;
            var available = s.board.Select((slot, index) => new { slot, index }).Where(x => !x.slot.Empty).ToArray();
            if (recoveryStage >= 0) {
                if (recoveryStage == 1 && session.CanUndo) return Cue("整理练习 · 安心恢复", "点击撤销，物品会回到原位", "undo");
                if (available.Length == 0) return Cue("整理练习 · 先取一份", "点击茶台，准备一撮练习用的茶", "produce_tea");
                if (selected >= 0 && !s.board[selected].Empty) return Cue("整理练习 · 放回工具台", "回收返还基础材料，不扣灵石", "recycle");
                return Cue("整理练习 · 选一件物品", "点亮的物品可以先放回工具台", "cell", available[0].index);
            }
            if (!s.firstMerge) {
                var pair = available.Where(x => x.slot.itemId == "tea_01").Take(2).ToArray();
                return pair.Length == 2 ? Cue("第一步 · 试一次合成", "拖向另一撮茶；也可依次点亮两格", "merge", pair[0].index, pair[1].index) :
                    Cue("第一步 · 备茶迎客", pair.Length == 0 ? "点击茶台，先取两撮山茶" : "再点一次茶台，凑齐两撮山茶", "produce_tea");
            }
            if (order?.id == "E02" && !s.lampRepaired) return Cue("她愿意留下 · 点亮门灯", "点击山房，让来客看见这里", "sanctuary");
            if (order == null) return Cue("这一幕已完成", session.Today?.lines.Count == 3 ? "打开手账，回看今天的三次回应" : "接待常客，继续留下今日一爻", session.Today?.lines.Count == 3 ? "journal" : "order_1");
            if (!s.guide.hasChosen && !s.guide.skipped) return Cue("选择回应 · 没有标准答案", "点选静或行，查看对应的茶席需求", "choices");
            var requirements = order.variants[variant].requirements;
            var missing = requirements.FirstOrDefault(r => s.board.Count(x => !x.Empty && x.itemId == r.itemId) < r.quantity);
            if (missing == null) return Cue("茶席备齐了", "点击交付，核对物品后送给来客", "deliver");
            if (s.inventory.Any(x => !x.Empty && x.itemId == missing.itemId)) return Cue("需要的物品在储物架", "取消选中后打开储物架，取回茶案", "inventory");
            var needed = session.Item(missing.itemId);
            var merge = available.Where(x => session.Item(x.slot.itemId).chainId == needed.chainId && session.Item(x.slot.itemId).tier < needed.tier)
                .GroupBy(x => x.slot.itemId).Where(g => g.Count() >= 2).OrderByDescending(g => session.Item(g.Key).tier).FirstOrDefault()?.Take(2).ToArray();
            if (merge != null) return Cue("合成 " + needed.displayNameZh, "将亮起的两件相同物品合在一起", "merge", merge[0].index, merge[1].index);
            if (available.Length >= session.OpenSlots) return Cue("茶案满了 · 免费整理", selected >= 0 ? "放回工具台可腾出一格，也可撤销" : "先选一件物品，再放回工具台", selected >= 0 ? "recycle" : "cell", available[0].index);
            return Cue("还需 " + needed.displayNameZh, needed.chainId == "tea" ? "点击茶台取茶；点需求可看合成路径" : "点击器架取盏；点需求可看合成路径", "produce_" + needed.chainId);
        }
    }
}
