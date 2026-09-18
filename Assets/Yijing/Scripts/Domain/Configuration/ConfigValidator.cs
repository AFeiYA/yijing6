using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Yijing.Domain.Configuration
{
    public static class ConfigValidator
    {
        public static void Validate(PrototypeConfig config)
        {
            Require(config != null, "Configuration is missing.");
            Require(config.schemaVersion == 1, "Unsupported configuration schema.");
            Require(!string.IsNullOrWhiteSpace(config.contentVersion), "Content version is missing.");
            var r = config.rules;
            Require(r != null, "Rules are missing.");
            Require(r.columns > 0 && r.rows > 0 && r.initialOpenRows > 0 &&
                    r.initialOpenRows <= r.rows, "Invalid board dimensions.");
            Require(r.initialInventorySlots >= 0 && r.maxInventorySlots >= r.initialInventorySlots,
                "Invalid inventory capacity.");
            Require(r.maxTier == 5 && r.initialStones >= 0 && r.stonesPerBaseUnit > 0,
                "Invalid prototype progression or economy rules.");
            Require(!r.energyEnabled && !r.producerCooldownEnabled, "Prototype has no energy or cooldown.");
            Require(r.oracleOrdersPerDay == 3 && r.lineOrder == "bottom-to-top" &&
                    r.prototypeUpperBits == "000", "Prototype oracle uses three bottom-up lines and Kun upper.");

            var items = Index(config.items, x => x.id, "item");
            var producers = Index(config.producers, x => x.id, "producer");
            var orders = Index(config.orders, x => x.id, "order");
            var nodes = Index(config.storyNodes, x => x.id, "story node");
            var renovations = Index(config.renovations, x => x.id, "renovation");
            var cards = Index(config.oracleCards, x => x.key, "oracle card");

            foreach (var item in items.Values)
            {
                Require(!string.IsNullOrWhiteSpace(item.chainId), $"Missing chain: {item.id}");
                Require(item.tier >= 1 && item.tier <= r.maxTier, $"Invalid tier: {item.id}");
                Require(item.baseUnits == 1 << (item.tier - 1), $"Invalid mass: {item.id}");
                if (item.tier == r.maxTier)
                    Require(string.IsNullOrEmpty(item.nextId), $"Terminal item has a next item: {item.id}");
                else
                {
                    Require(item.nextId != null && items.ContainsKey(item.nextId), $"Missing upgrade: {item.id}");
                    var next = items[item.nextId];
                    Require(next.chainId == item.chainId && next.tier == item.tier + 1 &&
                            next.baseUnits == item.baseUnits * 2, $"Invalid upgrade: {item.id}");
                }
            }
            foreach (var chain in items.Values.GroupBy(x => x.chainId))
                Require(chain.Count() == r.maxTier && chain.Select(x => x.tier).Distinct().Count() == r.maxTier,
                    $"Chain must have exactly one item per tier: {chain.Key}");
            foreach (var p in producers.Values)
            {
                Require(p.outputItemId != null && items.ContainsKey(p.outputItemId), $"Missing producer output: {p.id}");
                var item = items[p.outputItemId];
                Require(item.chainId == p.chainId && item.tier == 1, $"Invalid producer output: {p.id}");
                Require(!string.IsNullOrWhiteSpace(p.unlock), $"Missing producer unlock: {p.id}");
            }
            foreach (var chain in items.Values.Select(x => x.chainId).Distinct())
                Require(producers.Values.Any(x => x.chainId == chain), $"No producer for chain: {chain}");

            Require(r.finalBoardRowUnlockOrder != null && orders.ContainsKey(r.finalBoardRowUnlockOrder),
                "Missing board unlock order.");
            Require(r.inventoryExpansionOrder != null && orders.ContainsKey(r.inventoryExpansionOrder),
                "Missing inventory unlock order.");
            foreach (var order in orders.Values)
            {
                Require(order.storyNode != null && nodes.ContainsKey(order.storyNode), $"Missing story node: {order.id}");
                var variants = Index(order.variants, x => x.id, $"variant in {order.id}");
                Require(variants.Count == 2 && variants.ContainsKey("quiet") && variants.ContainsKey("act"),
                    $"Order needs quiet and act: {order.id}");
                Require(variants["quiet"].lineValue == 0 && variants["act"].lineValue == 1,
                    $"Invalid line values: {order.id}");
                var quiet = Cost(variants["quiet"], items);
                Require(quiet == Cost(variants["act"], items), $"Unequal variant costs: {order.id}");
                Require(order.rewardStones == quiet * r.stonesPerBaseUnit, $"Incorrect reward: {order.id}");
            }

            var graph = new Dictionary<string, string[]>();
            var assignedOrders = new HashSet<string>();
            foreach (var node in nodes.Values)
            {
                Require(node.orderIds != null && node.orderIds.Length == 3, $"Story node needs three orders: {node.id}");
                foreach (var id in node.orderIds)
                {
                    Require(id != null && orders.ContainsKey(id) && orders[id].storyNode == node.id,
                        $"Invalid node order: {node.id}/{id}");
                    Require(assignedOrders.Add(id), $"Order assigned twice: {id}");
                }
                Require(node.prerequisiteStoryNodes != null, $"Missing node prerequisites: {node.id}");
                var dependencies = node.prerequisiteStoryNodes.Select(id => "node:" + id).ToList();
                if (!string.IsNullOrEmpty(node.prerequisiteRenovation))
                    dependencies.Add("renovation:" + node.prerequisiteRenovation);
                graph.Add("node:" + node.id, dependencies.ToArray());
            }
            Require(assignedOrders.Count == orders.Count, "Some orders are not assigned to a story node.");
            foreach (var renovation in renovations.Values)
            {
                Require(renovation.cost >= 0, $"Negative renovation cost: {renovation.id}");
                Require(renovation.unlocksBoardRow == 0 || (renovation.unlocksBoardRow > r.initialOpenRows &&
                        renovation.unlocksBoardRow <= r.rows), $"Invalid row unlock: {renovation.id}");
                var dependencies = new List<string>();
                if (!string.IsNullOrEmpty(renovation.requiredAfterOrder))
                {
                    Require(orders.ContainsKey(renovation.requiredAfterOrder), $"Missing renovation order: {renovation.id}");
                    dependencies.Add("node:" + orders[renovation.requiredAfterOrder].storyNode);
                }
                if (!string.IsNullOrEmpty(renovation.prerequisiteRenovation))
                    dependencies.Add("renovation:" + renovation.prerequisiteRenovation);
                graph.Add("renovation:" + renovation.id, dependencies.ToArray());
            }
            var visited = new HashSet<string>();
            foreach (var id in graph.Keys) Visit(id, graph, visited, new HashSet<string>());

            var kingWenByLower = new Dictionary<string, int>
            {
                { "000", 2 }, { "100", 24 }, { "010", 7 }, { "110", 19 },
                { "001", 15 }, { "101", 36 }, { "011", 46 }, { "111", 11 }
            };
            Require(cards.Count == 8, "Prototype requires all eight Kun-upper cards.");
            foreach (var card in cards.Values)
            {
                Require(card.lowerBits != null && kingWenByLower.ContainsKey(card.lowerBits) &&
                        card.upperBits == r.prototypeUpperBits && card.key == card.lowerBits + "_" + card.upperBits,
                    $"Invalid oracle key: {card.key}");
                Require(card.kingWenId == kingWenByLower[card.lowerBits], $"Incorrect King Wen mapping: {card.key}");
                Require(!string.IsNullOrWhiteSpace(card.nameZh) && !string.IsNullOrWhiteSpace(card.titleZh) &&
                        !string.IsNullOrWhiteSpace(card.bodyZh) && !string.IsNullOrWhiteSpace(card.promptZh),
                    $"Missing oracle text: {card.key}");
            }
        }

        private static long Cost(VariantConfig variant, Dictionary<string, ItemConfig> items)
        {
            Require(variant.requirements != null && variant.requirements.Length >= 1 && variant.requirements.Length <= 2,
                $"Invalid requirements: {variant.id}");
            long result = 0;
            var ids = new HashSet<string>();
            foreach (var requirement in variant.requirements)
            {
                Require(requirement != null && requirement.itemId != null && items.ContainsKey(requirement.itemId),
                    $"Unknown requirement item: {variant.id}");
                Require(requirement.quantity > 0 && ids.Add(requirement.itemId), $"Invalid requirement quantity or duplicate: {variant.id}");
                result += (long)items[requirement.itemId].baseUnits * requirement.quantity;
            }
            return result;
        }

        private static Dictionary<string, T> Index<T>(T[] entries, Func<T, string> key, string label) where T : class
        {
            Require(entries != null && entries.Length > 0, $"Missing {label} list.");
            var result = new Dictionary<string, T>();
            foreach (var entry in entries)
            {
                Require(entry != null, $"Null {label}.");
                var id = key(entry);
                Require(!string.IsNullOrWhiteSpace(id) && !result.ContainsKey(id), $"Missing or duplicate {label} ID: {id}");
                result.Add(id, entry);
            }
            return result;
        }

        private static void Visit(string id, Dictionary<string, string[]> graph, HashSet<string> visited, HashSet<string> active)
        {
            if (visited.Contains(id)) return;
            Require(graph.ContainsKey(id), $"Unknown progression prerequisite: {id}");
            Require(active.Add(id), $"Progression dependency cycle: {id}");
            foreach (var dependency in graph[id]) Visit(dependency, graph, visited, active);
            active.Remove(id);
            visited.Add(id);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
    }
}
