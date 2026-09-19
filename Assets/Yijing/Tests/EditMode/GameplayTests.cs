using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Yijing.Domain.Configuration;
using Yijing.Domain.Gameplay;
using Yijing.Editor;
using Yijing.Infrastructure;

namespace Yijing.Tests
{
    public sealed class GameplayTests
    {
        private sealed class MemoryStore : IGameStore
        {
            public GameState state;
            public bool fail;
            public GameState Load() => state?.Copy();
            public void Save(GameState next) { if (fail) throw new IOException("Disk full"); state = next.Copy(); }
        }
        private PrototypeConfig config;
        private MemoryStore store;
        private GameSession game;
        private DateTimeOffset now;
        private string directory;

        [SetUp] public void SetUp()
        {
            config = JsonUtility.FromJson<PrototypeConfig>(File.ReadAllText(ProjectSetup.SourcePath));
            store = new MemoryStore(); now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.FromHours(8));
            game = new GameSession(config, store, () => now);
            directory = Path.Combine(Path.GetTempPath(), "yijing-tests-" + Guid.NewGuid().ToString("N"));
        }
        [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        private void OK(ActionResult result) => Assert.That(result.Success, Is.True, result.Message);
        private void Reopen() => game = new GameSession(config, store, () => now);
        private long Rev => game.Snapshot.revision;
        private void Produce(string chain = "tea") => OK(game.Produce(chain, Rev));
        private void Move(int from, int to) => OK(game.Move(from, to, game.Snapshot.board[from].instanceId, Rev));
        private int TotalUnits(GameState s) => s.board.Concat(s.inventory).Where(x => !x.Empty).Sum(x => game.Item(x.itemId).baseUnits) + s.teaReserve + s.ceramicReserve;
        private void Seed(params string[] ids)
        {
            var s = store.state.Copy();
            s.board = Enumerable.Range(0, s.board.Length).Select(_ => new ItemSlot()).ToArray();
            for (int i = 0; i < ids.Length; i++) s.board[i] = new ItemSlot { itemId = ids[i], instanceId = s.nextInstanceId++ };
            s.firstMerge = true; store.state = s; Reopen();
        }
        private void Deliver(int variant = 0, int slot = 0)
        {
            if (slot == 0 && game.Snapshot.completedStory == 1 && !game.Snapshot.lampRepaired)
                OK(game.RepairLamp(Guid.NewGuid().ToString(), Rev));
            var order = game.Order(slot); var choice = order.variants[variant];
            Seed(choice.requirements.SelectMany(r => Enumerable.Repeat(r.itemId, r.quantity)).ToArray());
            OK(game.Deliver(slot, order.id, choice.id, game.SelectMaterials(choice, false), false, Guid.NewGuid().ToString(), Rev));
        }

        [Test] public void FirstMergeUnlocksCeramicsAndConservesMaterials()
        {
            Assert.That(game.Produce("ceramic", Rev).Success, Is.False);
            Produce(); Produce(); Move(0, 1);
            Assert.That(game.Snapshot.board[0].Empty, Is.True);
            Assert.That(game.Snapshot.board[1].itemId, Is.EqualTo("tea_02"));
            Assert.That(TotalUnits(game.Snapshot), Is.EqualTo(2)); Produce("ceramic");
        }
        [Test] public void StaleInputCannotDuplicateOrConsume()
        {
            Produce(); Produce(); long rev = Rev; long id = game.Snapshot.board[0].instanceId;
            OK(game.Move(0, 1, id, rev));
            Assert.That(game.Move(0, 1, id, rev).Success, Is.False);
            Assert.That(TotalUnits(game.Snapshot), Is.EqualTo(2));
        }
        [Test] public void LockedOutsideAndSameCellDoNotMutate()
        {
            Produce(); long id = game.Snapshot.board[0].instanceId, rev = Rev;
            foreach (int target in new[] { -1, 0, 42, 63 }) Assert.That(game.Move(0, target, id, rev).Success, Is.False);
            Assert.That(Rev, Is.EqualTo(rev));
        }
        [Test] public void MoveExchangeAndTierFiveKeepUniqueInstances()
        {
            Seed("tea_05", "tea_05", "ceramic_01");
            long id = game.Snapshot.board[0].instanceId;
            Move(0, 1); Assert.That(game.Snapshot.board[1].instanceId, Is.EqualTo(id));
            Move(1, 2); Assert.That(game.Snapshot.board[1].itemId, Is.EqualTo("ceramic_01"));
            Move(2, 3); Assert.That(game.Snapshot.board[2].Empty, Is.True);
            Assert.That(TotalUnits(game.Snapshot), Is.EqualTo(33));
        }
        [Test] public void FullBoardCanMergeAndRecycleWithoutFreeSlot()
        {
            for (int i = 0; i < 42; i++) Produce();
            Assert.That(game.Produce("tea", Rev).Success, Is.False);
            Move(0, 1); Produce();
            int units = TotalUnits(game.Snapshot); var s = game.Snapshot;
            OK(game.Recycle(1, false, s.board[1].instanceId, Rev));
            Assert.That(TotalUnits(game.Snapshot), Is.EqualTo(units));
            OK(game.Undo(Rev)); Assert.That(game.Snapshot.board[1].itemId, Is.EqualTo("tea_02"));
            Assert.That(TotalUnits(game.Snapshot), Is.EqualTo(units));
        }
        [Test] public void RecycleReserveIsConsumedBeforeFreeProductionAndUndoExpires()
        {
            Seed("tea_04"); OK(game.Recycle(0, false, game.Snapshot.board[0].instanceId, Rev));
            Assert.That(game.Snapshot.teaReserve, Is.EqualTo(8)); Produce();
            Assert.That(game.Snapshot.teaReserve, Is.EqualTo(7)); Assert.That(game.CanUndo, Is.False);
            Assert.That(TotalUnits(game.Snapshot), Is.EqualTo(8));
        }
        [Test] public void FailedOperationDoesNotInvalidateRecycleUndo()
        {
            Seed("tea_02"); OK(game.Recycle(0, false, game.Snapshot.board[0].instanceId, Rev));
            Assert.That(game.Move(0, 42, 0, Rev).Success, Is.False); Assert.That(game.CanUndo, Is.True);
            Reopen(); Assert.That(game.CanUndo, Is.False);
        }
        [Test] public void StorageRetrievalAndStorageRecycleConserveResources()
        {
            Seed("tea_04"); long id = game.Snapshot.board[0].instanceId;
            OK(game.Store(0, id, Rev)); Assert.That(game.Snapshot.board[0].Empty, Is.True);
            OK(game.Recycle(0, true, id, Rev)); OK(game.Undo(Rev));
            Assert.That(game.Snapshot.inventory[0].instanceId, Is.EqualTo(id));
            OK(game.Retrieve(0, id, Rev)); Assert.That(TotalUnits(game.Snapshot), Is.EqualTo(8));
        }
        [Test] public void StorageHasFiniteCapacityAndFullBoardDoesNotLoseRetrievedItem()
        {
            Seed(Enumerable.Repeat("tea_01", 42).ToArray());
            long id = game.Snapshot.board[0].instanceId; OK(game.Store(0, id, Rev)); Produce();
            Assert.That(game.Retrieve(0, id, Rev).Success, Is.False);
            for (int i = 1; i < 6; i++) OK(game.Store(i, game.Snapshot.board[i].instanceId, Rev));
            Assert.That(game.Store(6, game.Snapshot.board[6].instanceId, Rev).Success, Is.False);
            Assert.That(game.Snapshot.inventory.Count(x => !x.Empty), Is.EqualTo(6));
        }
        [Test] public void DeliveryIsAtomicAndCannotReplayOrderOrCommand()
        {
            Seed("tea_03", "tea_03"); var order = game.Order(0); var ids = game.SelectMaterials(order.variants[0], false);
            long rev = Rev; OK(game.Deliver(0, "E01", "quiet", ids, false, "once", rev));
            Assert.That(game.Snapshot.stones, Is.EqualTo(46)); Assert.That(game.Today.lines, Is.EqualTo(new[] { 0 }));
            Assert.That(game.Deliver(0, "E01", "quiet", ids, false, "once", rev).Success, Is.False);
            Seed("tea_03", "tea_03");
            Assert.That(game.Deliver(0, "E02", "quiet", game.SelectMaterials(game.Order(0).variants[0], false), false, "once", Rev).Success, Is.False);
            Assert.That(game.Snapshot.stones, Is.EqualTo(46));
        }
        [Test] public void HigherTierAndDuplicateMaterialIdsAreRejected()
        {
            Seed("tea_04", "tea_03");
            Assert.That(game.SelectMaterials(game.Order(0).variants[0], false), Is.Null);
            long id = game.Snapshot.board[1].instanceId;
            Assert.That(game.Deliver(0, "E01", "quiet", new[] { id, id }, false, "bad", Rev).Success, Is.False);
            Assert.That(game.Deliver(0, "E01", "quiet", game.Snapshot.board.Take(2).Select(x => x.instanceId).ToArray(), false, "bad2", Rev).Success, Is.False);
        }
        [Test] public void StorageOnlySuppliesOrderWithExplicitOptIn()
        {
            Seed("tea_03", "tea_03"); OK(game.Store(1, game.Snapshot.board[1].instanceId, Rev));
            var variant = game.Order(0).variants[0]; Assert.That(game.SelectMaterials(variant, false), Is.Null);
            var ids = game.SelectMaterials(variant, true);
            Assert.That(game.Deliver(0, "E01", "quiet", ids, false, "denied", Rev).Success, Is.False);
            OK(game.Deliver(0, "E01", "quiet", ids, true, "accepted", Rev));
            Assert.That(game.Snapshot.inventory.All(x => x.Empty), Is.True);
        }
        [Test] public void SaveFailureRollsBackProductionDeliveryAndLamp()
        {
            store.fail = true; Assert.That(game.Produce("tea", Rev).Success, Is.False); Assert.That(Rev, Is.Zero);
            store.fail = false; Seed("tea_03", "tea_03"); store.fail = true;
            Assert.That(game.Deliver(0, "E01", "quiet", game.SelectMaterials(game.Order(0).variants[0], false), false, "retry", Rev).Success, Is.False);
            Assert.That(game.Snapshot.stones, Is.EqualTo(30)); Assert.That(game.Today, Is.Null);
            store.fail = false; Deliver(); store.fail = true;
            Assert.That(game.RepairLamp("lamp", Rev).Success, Is.False);
            Assert.That(game.Snapshot.lampRepaired, Is.False); Assert.That(game.Snapshot.stones, Is.EqualTo(46));
            store.fail = false; OK(game.RepairLamp("lamp", Rev)); Assert.That(game.Snapshot.stones, Is.EqualTo(6));
        }
        [Test] public void LampRequiresFirstStoryOrderAndCannotDoubleCharge()
        {
            Assert.That(game.RepairLamp("early", Rev).Success, Is.False); Deliver();
            OK(game.RepairLamp("lamp", Rev)); Assert.That(game.RepairLamp("again", Rev).Success, Is.False);
            Assert.That(game.Snapshot.stones, Is.EqualTo(6));
        }
        [Test] public void QuietQuietActCreatesQian15AndFourthDeliveryDoesNotOverwrite()
        {
            Deliver(); Deliver(); Deliver(1);
            Assert.That(game.Today.oracleKey, Is.EqualTo("001_000")); Assert.That(game.Card(game.Today).kingWenId, Is.EqualTo(15));
            Deliver(1, 1); Assert.That(game.Today.lines, Is.EqualTo(new[] { 0, 0, 1 })); Assert.That(game.Order(0), Is.Null);
        }
        [Test] public void MidnightAndClockRollbackPreserveBothDays()
        {
            Deliver(); string first = game.Today.date; now = now.AddDays(1); Deliver(1);
            Assert.That(game.Today.lines, Is.EqualTo(new[] { 1 })); now = now.AddDays(-1); Deliver(1);
            Assert.That(game.Today.date, Is.EqualTo(first)); Assert.That(game.Today.lines, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(game.Snapshot.days.Count, Is.EqualTo(2));
        }
        [Test] public void RegularRotationPersistsAndReplacingHasNoReward()
        {
            string old = game.Order(1).id; OK(game.ReplaceRegular(1, old, Rev));
            string next = game.Order(1).id; Reopen(); Assert.That(game.Order(1).id, Is.EqualTo(next));
            Assert.That(game.Snapshot.stones, Is.EqualTo(30)); Assert.That(game.Today, Is.Null);
            Assert.That(game.ReplaceRegular(1, old, Rev).Success, Is.False);
            for (int i = 0; i < 4; i++) {
                var o = GameSession.Regular(i);
                var costs = o.variants.Select(v => v.requirements.Sum(r => game.Item(r.itemId).baseUnits * r.quantity)).ToArray();
                Assert.That(costs[0], Is.EqualTo(costs[1])); Assert.That(o.rewardStones, Is.EqualTo(costs[0] * 2));
            }
        }
        [Test] public void SnapshotCannotMutateAuthoritativeState()
        {
            var copy = game.Snapshot; copy.stones = 999; copy.board[0].itemId = "bogus";
            Assert.That(game.Snapshot.stones, Is.EqualTo(30)); Assert.That(game.Snapshot.board[0].Empty, Is.True);
        }
        [Test] public void JsonRoundTripPreservesEmptySlotsAndDailyState()
        {
            Deliver(); Deliver(); Deliver(1);
            string path = Path.Combine(directory, "save.json"); var repo = new JsonGameStore(path, config);
            repo.Save(game.Snapshot); var loaded = new JsonGameStore(path, config).Load();
            Assert.That(JsonUtility.ToJson(loaded), Is.EqualTo(JsonUtility.ToJson(game.Snapshot)));
            Assert.That(loaded.board.All(x => x != null && x.Empty), Is.True);
        }
        [Test] public void CorruptPrimaryRecoversBackupAndDoesNotOverwriteItOnNextSave()
        {
            string path = Path.Combine(directory, "save.json"); var repo = new JsonGameStore(path, config);
            repo.Save(game.Snapshot); Produce(); repo.Save(game.Snapshot);
            string backup = File.ReadAllText(path + ".bak"); File.WriteAllText(path, "broken");
            var recoveredRepo = new JsonGameStore(path, config); var recovered = recoveredRepo.Load();
            Assert.That(recovered.revision, Is.Zero); Assert.That(recoveredRepo.RecoveryNotice, Is.Not.Null);
            recoveredRepo.Save(recovered);
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(backup));
            Assert.That(Directory.GetFiles(directory, "*.corrupt-*").Length, Is.EqualTo(1));
        }
        [Test] public void BothCorruptSavesBlockStartupAndPreserveFiles()
        {
            Directory.CreateDirectory(directory); string path = Path.Combine(directory, "save.json");
            File.WriteAllText(path, "primary"); File.WriteAllText(path + ".bak", "backup");
            Assert.Throws<InvalidDataException>(() => new JsonGameStore(path, config).Load());
            Assert.That(File.ReadAllText(path), Is.EqualTo("primary")); Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo("backup"));
        }
        [Test] public void UnknownItemAndDuplicateInstancesAreRejectedWithoutMigration()
        {
            Seed("tea_01", "tea_01"); var s = game.Snapshot;
            s.board[0].itemId = "future_item"; Assert.Throws<InvalidDataException>(() => s.Validate(config));
            s = game.Snapshot; s.board[1].instanceId = s.board[0].instanceId; Assert.Throws<InvalidDataException>(() => s.Validate(config));
        }

        [Test] public void GuideFindsMovedTeachingItemsInsteadOfFixedCoordinates()
        {
            Assert.That(GuideDirector.Next(game, game.Order(0), 0).target, Is.EqualTo("produce_tea"));
            Produce(); Produce(); Move(0, 9);
            var cue = GuideDirector.Next(game, game.Order(0), 0);
            Assert.That(cue.target, Is.EqualTo("merge"));
            Assert.That(new[] { cue.from, cue.to }, Is.EquivalentTo(new[] { 1, 9 }));
        }

        [Test] public void GuideSelectsMissingChainStorageAndSafeFullBoardRecovery()
        {
            Deliver(); OK(game.RepairLamp("lamp", Rev)); OK(game.ChooseResponse(0, Rev));
            Seed("tea_02", "tea_02");
            Assert.That(GuideDirector.Next(game, game.Order(0), 0).target, Is.EqualTo("produce_ceramic"));
            Seed("tea_02", "tea_02", "ceramic_03"); OK(game.Store(2, game.Snapshot.board[2].instanceId, Rev));
            Assert.That(GuideDirector.Next(game, game.Order(0), 0).target, Is.EqualTo("inventory"));
            Seed(Enumerable.Repeat("ceramic_05", 42).ToArray());
            Assert.That(GuideDirector.Next(game, game.Order(0), 0).target, Is.EqualTo("cell"));
        }

        [Test] public void StoryTwoRequiresLampWithoutConsumingPreparedItems()
        {
            Deliver(); var second = game.Order(0);
            Seed(second.variants[0].requirements.SelectMany(x => Enumerable.Repeat(x.itemId, x.quantity)).ToArray());
            var before = JsonUtility.ToJson(game.Snapshot);
            var result = game.Deliver(0, "E02", "quiet", game.SelectMaterials(second.variants[0], false), false, "blocked", Rev);
            Assert.That(result.Success, Is.False); Assert.That(JsonUtility.ToJson(game.Snapshot), Is.EqualTo(before));
            Assert.That(GuideDirector.Next(game, second, 0).target, Is.EqualTo("sanctuary"));
        }

        [Test] public void IntroductionAndChoiceAcknowledgementPersistAndDoNotAwardResources()
        {
            OK(game.AdvanceIntroduction(Rev)); Reopen(); Assert.That(game.Snapshot.guide.introStep, Is.EqualTo(1));
            OK(game.AdvanceIntroduction(Rev)); Produce(); Produce(); Move(0, 1);
            OK(game.ExplainChoice(Rev)); OK(game.ChooseResponse(1, Rev)); Reopen();
            Assert.That(game.Snapshot.guide.choiceExplained, Is.True); Assert.That(game.Snapshot.guide.preferredVariant, Is.EqualTo(1));
            Assert.That(game.Snapshot.stones, Is.EqualTo(30)); Assert.That(TotalUnits(game.Snapshot), Is.EqualTo(2));
        }

        [Test] public void GuideAcknowledgementsRollBackOnSaveFailureAndPreserveUndo()
        {
            store.fail = true; Assert.That(game.AdvanceIntroduction(Rev).Success, Is.False);
            Assert.That(game.Snapshot.guide.introStep, Is.Zero); store.fail = false;
            Seed("tea_02"); OK(game.Recycle(0, false, game.Snapshot.board[0].instanceId, Rev));
            OK(game.SetGuideSkipped(true, Rev)); Assert.That(game.CanUndo, Is.True); OK(game.Undo(Rev));
            Assert.That(game.Snapshot.guide.undoneOnce, Is.True); Assert.That(TotalUnits(game.Snapshot), Is.EqualTo(2));
        }

        [Test] public void UnreadStoryResponseSurvivesReloadWithActualChoice()
        {
            Deliver(1); Reopen(); Assert.That(game.Snapshot.storyChoices[0], Is.EqualTo("act"));
            Assert.That(game.Snapshot.guide.responsesSeen, Is.Zero);
            OK(game.AcknowledgeStory(1, Rev)); Reopen();
            Assert.That(game.Snapshot.guide.responsesSeen, Is.EqualTo(1));
            Assert.That(game.AcknowledgeStory(1, Rev).Success, Is.False); Assert.That(game.Snapshot.stones, Is.EqualTo(46));
        }

        [Serializable] private sealed class LegacyEnvelope { public string payload, sha256; }
        [Test] public void VersionOneSaveUpgradesWithoutLosingProgressOrInventingChoices()
        {
            Deliver(); Deliver(); Deliver(1); Seed("tea_04");
            var legacy = game.Snapshot; legacy.schemaVersion = 1;
            string payload = JsonUtility.ToJson(legacy);
            payload = payload.Substring(0, payload.IndexOf(",\"guide\":", StringComparison.Ordinal)) + "}";
            string hash;
            using (var sha = System.Security.Cryptography.SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload))).Replace("-", "");
            Directory.CreateDirectory(directory); string path = Path.Combine(directory, "save.json");
            File.WriteAllText(path, JsonUtility.ToJson(new LegacyEnvelope { payload = payload, sha256 = hash }));
            string oldFile = File.ReadAllText(path); var repository = new JsonGameStore(path, config); var restored = repository.Load();
            Assert.That(restored.schemaVersion, Is.EqualTo(2)); Assert.That(restored.stones, Is.EqualTo(38));
            Assert.That(restored.board[0].itemId, Is.EqualTo("tea_04")); Assert.That(restored.days.Single().oracleKey, Is.EqualTo("001_000"));
            Assert.That(restored.guide.responsesSeen, Is.EqualTo(3)); Assert.That(restored.storyChoices, Is.EqualTo(new[] { "", "", "" }));
            repository.Save(restored); Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(oldFile));
        }

        [Test] public void StoryContentCoversEachPlayableOrderWithDistinctResponses()
        {
            var book = JsonUtility.FromJson<StoryBook>(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Yijing/Data/FirstSessionStory.json")));
            book.Validate(); Assert.That(book.beats.Select(x => x.quietResponse).Distinct().Count(), Is.EqualTo(3));
            Assert.That(book.beats.All(x => x.quietResponse != x.actResponse), Is.True);
        }
    }
}
