using System.Text.Json;
using Yijing.Domain.Configuration;
using Yijing.Domain.Gameplay;

// A real .NET rules check, not a substitute for Unity's serializer, pointer, audio or layout tests.
var root = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
var json = new JsonSerializerOptions { IncludeFields = true };
var config = JsonSerializer.Deserialize<PrototypeConfig>(File.ReadAllText(Path.Combine(root, "docs/prototype_config.json")), json)!;
ConfigValidator.Validate(config);
var story = JsonSerializer.Deserialize<StoryBook>(File.ReadAllText(Path.Combine(root, "Assets/Yijing/Data/FirstSessionStory.json")), json)!;
story.Validate();
int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
void Ok(ActionResult result) => Check(result.Success, result.Message);
foreach (var order in config.orders.Take(3)) {
    Check(order.variants.All(v => v.requirements.Sum(r => config.items.Single(i => i.id == r.itemId).baseUnits * r.quantity) == 4), "First-act work must be four base units");
    Check(order.rewardStones == 8, "Reward must stay proportional to work");
}
int budget = config.rules.initialStones;
foreach (var order in config.orders) {
    budget += order.rewardStones;
    foreach (var repair in config.renovations.Where(x => x.mandatory && x.requiredAfterOrder == order.id)) { budget -= repair.cost; Check(budget >= 0, repair.id + " is unreachable"); }
}
Check(budget == 252, "Full configured story budget");
for (int bits = 0; bits < 8; bits++) {
    var store = new MemoryStore(); var now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.FromHours(8));
    var session = new GameSession(config, store, () => now);
    Ok(session.AdvanceIntroduction(session.Snapshot.revision)); Ok(session.AdvanceIntroduction(session.Snapshot.revision));
    for (int o = 0; o < 3; o++) {
        var order = session.Order(0); var variant = order.variants[(bits >> o) & 1];
        foreach (var need in variant.requirements) {
            var item = session.Item(need.itemId);
            while (session.Snapshot.board.Count(x => x.itemId == need.itemId) < need.quantity) {
                // Ensure the first merge occurs before accessing ceramics, matching the live onboarding.
                if (!session.Snapshot.firstMerge && item.chainId == "ceramic") throw new Exception("Unexpected onboarding recipe");
                for (int i = 0; i < item.baseUnits; i++) Ok(session.Produce(item.chainId, session.Snapshot.revision));
                while (true) {
                    var pair = session.Snapshot.board.Select((x, i) => new { slot = x, i }).Where(x => !x.slot.Empty && session.Item(x.slot.itemId).chainId == item.chainId && session.Item(x.slot.itemId).tier < item.tier)
                        .GroupBy(x => x.slot.itemId).FirstOrDefault(g => g.Count() > 1)?.Take(2).ToArray();
                    if (pair == null) break;
                    Ok(session.Move(pair[0].i, pair[1].i, pair[0].slot.instanceId, session.Snapshot.revision));
                }
            }
        }
        var ids = session.SelectMaterials(variant, false); var rev = session.Snapshot.revision;
        if (o == 1) {
            Check(!session.Deliver(0, order.id, variant.id, ids, false, "blocked", rev).Success, "Lamp gating");
            Check(session.Snapshot.revision == rev && session.SelectMaterials(variant, false).SequenceEqual(ids), "Blocked delivery preserves all materials");
            Ok(session.RepairLamp("lamp", rev));
        }
        var before = session.Snapshot; store.fail = true;
        Check(!session.Deliver(0, order.id, variant.id, ids, false, "serve", before.revision).Success, "Save failure rejects serving");
        Check(session.Snapshot.stones == before.stones && session.Snapshot.revision == before.revision, "Save rollback"); store.fail = false;
        var command = "serve-" + o;
        Ok(session.Deliver(0, order.id, variant.id, ids, false, command, before.revision));
        Check(!session.Deliver(0, order.id, variant.id, ids, false, command, before.revision).Success, "Serving is not replayable");
        session = new GameSession(config, store, () => now);
        Check(session.Snapshot.guide.responsesSeen == o, "Unread reply survives reload");
        Check(session.Snapshot.storyChoices[o] == variant.id, "Actual branch remembered");
        Ok(session.AcknowledgeStory(o + 1, session.Snapshot.revision));
    }
    Check(session.Snapshot.stones == 24, "First-act wallet should be 24");
    Check(session.Snapshot.board.All(x => x.Empty), "No surplus required by first-act recipes");
    Check(session.Today.oracleKey == string.Concat(Enumerable.Range(0, 3).Select(i => (bits >> i) & 1)) + "_000", "Choice order maps to correct card");
    var old = session.Snapshot; old.schemaVersion = 1; old.stones = 38; store.state = old;
    session = new GameSession(config, store, () => now);
    Check(session.Snapshot.stones == 38 && session.Snapshot.completedStory == 3, "Prior balance and story preserved");
    Check(session.Snapshot.storyChoices.All(string.IsNullOrEmpty), "Never invent legacy choices");
}
Console.WriteLine($"PASS: {checks} assertions; all eight story routes, 4-unit recipes, lamp budget, save rollback, duplicate delivery and legacy balances.");
sealed class MemoryStore : IGameStore
{
    public GameState state; public bool fail;
    public GameState Load() => state?.Copy();
    public void Save(GameState next) { if (fail) throw new IOException("Simulated full disk"); state = next.Copy(); }
}
