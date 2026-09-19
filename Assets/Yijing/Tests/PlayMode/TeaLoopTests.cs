using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Yijing.Presentation;

namespace Yijing.Tests
{
    public sealed class TeaLoopTests
    {
        private string directory;
        private GamePresenter presenter;
        [UnitySetUp] public IEnumerator OpenTeaRoom()
        {
            directory = Path.Combine(Path.GetTempPath(), "yijing-play-" + Guid.NewGuid().ToString("N"));
            GamePresenter.SavePathForTesting = Path.Combine(directory, "save.json");
            yield return SceneManager.LoadSceneAsync("Game"); yield return null;
            presenter = UnityEngine.Object.FindFirstObjectByType<GamePresenter>();
            Assert.That(presenter.Session, Is.Not.Null);
            AssertVisibleTextFits(); Click("intro_next"); AssertVisibleTextFits(); Click("intro_next");
        }
        [UnityTearDown] public IEnumerator CloseTeaRoom()
        {
            GamePresenter.SavePathForTesting = null;
            if (presenter != null) UnityEngine.Object.Destroy(presenter.gameObject);
            yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        private void Click(string name)
        {
            var target = GameObject.Find(name);
            Assert.That(target, Is.Not.Null, name);
            var button = target.GetComponent<Button>();
            Assert.That(button.interactable, Is.True, name + " disabled"); button.onClick.Invoke();
        }
        private void TapCell(int index)
        {
            ExecuteEvents.Execute(GameObject.Find("cell_" + index), new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        }
        private void MakeTierThree(string chain) => MakeTier(chain, 3);
        private void MakeTier(string chain, int tier)
        {
            for (int i = 0; i < (1 << (tier - 1)); i++) Click("produce_" + chain);
            while (true) {
                var s = presenter.Session.Snapshot;
                var pair = s.board.Select((x, i) => new { slot = x, index = i }).Where(x => !x.slot.Empty && x.slot.itemId.StartsWith(chain) && presenter.Session.Item(x.slot.itemId).tier < tier)
                    .GroupBy(x => x.slot.itemId).FirstOrDefault(x => x.Count() >= 2)?.Take(2).ToArray();
                if (pair == null) break;
                TapCell(pair[0].index); TapCell(pair[1].index);
                if (GameObject.Find("choice_understood") != null) { AssertVisibleTextFits(); Click("choice_understood"); Click("choice_quiet"); }
            }
        }

        [UnityTest] public IEnumerator ThreeOrdersLampAndOracleSurviveReload() => PlayThreeOrders(false);
        [UnityTest] public IEnumerator ThreeActiveResponsesFitAndRememberTheirChoices() => PlayThreeOrders(true);
        private IEnumerator PlayThreeOrders(bool allAct)
        {
            Canvas.ForceUpdateCanvases(); yield return null;
            AssertVisibleTextFits();
            for (int order = 0; order < 3; order++) {
                var recipe = presenter.Session.Order(0).variants[allAct || order == 2 ? 1 : 0];
                foreach (var requirement in recipe.requirements) {
                    var item = presenter.Session.Item(requirement.itemId);
                    for (int count = 0; count < requirement.quantity; count++) MakeTier(item.chainId, item.tier);
                }
                if (allAct || order == 2) Click("choice_act");
                AssertVisibleTextFits();
                Click("deliver"); Click("place_cup"); Click("pour_tea"); Click("finish_pour"); yield return null; Click("confirm_delivery"); yield return null;
                Assert.That(presenter.Session.Snapshot.completedStory, Is.EqualTo(order + 1));
                AssertVisibleTextFits(); Click("response_continue"); yield return null;
                if (order == 0) {
                    AssertVisibleTextFits(); Click("repair_lamp"); yield return null; Click("lamp_continue"); yield return null;
                }
                AssertVisibleTextFits();
                if (order < 2) Click("story_begin"); else Click("ending_continue");
                yield return null;
            }
            Assert.That(presenter.Session.Today.oracleKey, Is.EqualTo(allAct ? "111_000" : "001_000"));
            Assert.That(presenter.Session.Snapshot.stones, Is.EqualTo(24));
            Assert.That(presenter.Session.Snapshot.lampRepaired, Is.True);
            Canvas.ForceUpdateCanvases(); yield return null; AssertVisibleTextFits();
            yield return SceneManager.LoadSceneAsync("Game"); yield return null;
            presenter = UnityEngine.Object.FindFirstObjectByType<GamePresenter>();
            Assert.That(presenter.Session.Snapshot.stones, Is.EqualTo(24));
            Assert.That(presenter.Session.Snapshot.completedStory, Is.EqualTo(3));
            Assert.That(presenter.Session.Today.oracleKey, Is.EqualTo(allAct ? "111_000" : "001_000"));
            Click("room_help"); AssertVisibleTextFits(); Click("close_modal");
            Click("room_regular"); Click("order_1"); Click("deliver"); yield return null;
            Assert.That(GameObject.Find("replace_request"), Is.Not.Null);
        }

        [UnityTest] public IEnumerator PointerDragMergesAndOutsideDropLeavesStateUntouched()
        {
            Click("produce_tea"); Click("produce_tea"); Canvas.ForceUpdateCanvases(); yield return null;
            var source = GameObject.Find("cell_0"); var target = GameObject.Find("cell_1");
            Vector2 Point(GameObject obj) => RectTransformUtility.WorldToScreenPoint(null, obj.GetComponent<RectTransform>().TransformPoint(obj.GetComponent<RectTransform>().rect.center));
            var data = new PointerEventData(EventSystem.current) { position = Point(source), pointerId = -1, button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(source, data, ExecuteEvents.beginDragHandler);
            data.position = Point(target); ExecuteEvents.Execute(source, data, ExecuteEvents.dragHandler); ExecuteEvents.Execute(source, data, ExecuteEvents.endDragHandler);
            Assert.That(presenter.Session.Snapshot.board[1].itemId, Is.EqualTo("tea_02"));
            Click("choice_understood");
            long revision = presenter.Session.Snapshot.revision;
            data.position = Point(target); ExecuteEvents.Execute(target, data, ExecuteEvents.beginDragHandler);
            data.position = new Vector2(-100, -100); ExecuteEvents.Execute(target, data, ExecuteEvents.dragHandler); ExecuteEvents.Execute(target, data, ExecuteEvents.endDragHandler);
            Assert.That(presenter.Session.Snapshot.revision, Is.EqualTo(revision));
            yield return null;
        }

        [UnityTest] public IEnumerator UIStorageRecycleAndUndoRestoreTheSameItem()
        {
            MakeTierThree("tea");
            int index = Array.FindIndex(presenter.Session.Snapshot.board, x => !x.Empty); long id = presenter.Session.Snapshot.board[index].instanceId;
            TapCell(index); Click("inventory"); Click("inventory"); yield return null;
            Click("storage_0"); yield return null; Click("recycle_storage"); yield return null; Click("close_modal");
            Click("undo");
            Assert.That(presenter.Session.Snapshot.inventory[0].instanceId, Is.EqualTo(id));
            Assert.That(presenter.Session.Snapshot.teaReserve, Is.Zero);
        }

        [UnityTest] public IEnumerator IndependentPracticeRestoresOriginalProgressAndCanExitDuringIntro()
        {
            MakeTierThree("tea"); var original = JsonUtility.ToJson(presenter.Session.Snapshot);
            Click("help"); AssertVisibleTextFits(); Click("practice_from_start");
            Assert.That(presenter.Session.Snapshot.firstMerge, Is.False);
            Click("skip_guide");
            Assert.That(JsonUtility.ToJson(presenter.Session.Snapshot), Is.EqualTo(original));
            Click("room_help"); Click("practice_from_start");
            Click("intro_next"); Click("intro_next"); Click("produce_tea"); Click("help");
            Assert.That(JsonUtility.ToJson(presenter.Session.Snapshot), Is.EqualTo(original));
            yield return SceneManager.LoadSceneAsync("Game"); yield return null;
            presenter = UnityEngine.Object.FindFirstObjectByType<GamePresenter>();
            Assert.That(JsonUtility.ToJson(presenter.Session.Snapshot), Is.EqualTo(original));
        }

        [UnityTest] public IEnumerator RecipeAndRecoveryGuidanceWorkWithoutAwardingItems()
        {
            MakeTierThree("tea"); Click("recipe_help"); AssertVisibleTextFits(); Click("close_modal");
            Click("help"); Click("help_recovery");
            int i = Array.FindIndex(presenter.Session.Snapshot.board, x => !x.Empty);
            long id = presenter.Session.Snapshot.board[i].instanceId; TapCell(i); Click("recycle"); Click("undo");
            Assert.That(presenter.Session.Snapshot.board[i].instanceId, Is.EqualTo(id));
            Assert.That(presenter.Session.Snapshot.stones, Is.EqualTo(30));
            Assert.That(presenter.Session.Snapshot.guide.undoneOnce, Is.True);
            yield return null;
        }


        [UnityTest] public IEnumerator QuietMomentNeverAwardsOrConsumesAndAlwaysHasAnExit()
        {
            MakeTier("tea", 2); var before = JsonUtility.ToJson(presenter.Session.Snapshot);
            Click("back_to_room"); Click("room_rest");
            Assert.That(GameObject.Find("Room conversation"), Is.Null);
            Click("touch_chime"); Click("listen_rain"); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(JsonUtility.ToJson(presenter.Session.Snapshot), Is.EqualTo(before));
            Click("leave_rest"); Click("room_prepare");
            Assert.That(GameObject.Find("Living tea room"), Is.Null);
        }

        [UnityTest] public IEnumerator TeaRitualIsCancellableAndAutomaticPourCommitsOnlyOnServing()
        {
            MakeTier("tea", 2); MakeTier("tea", 2);
            var before = JsonUtility.ToJson(presenter.Session.Snapshot);
            Click("deliver"); AssertVisibleTextFits(); Click("place_cup"); Click("cancel_ritual");
            Assert.That(JsonUtility.ToJson(presenter.Session.Snapshot), Is.EqualTo(before));
            Click("deliver"); Click("place_cup"); Click("pour_tea");
            yield return new WaitForSecondsRealtime(2.6f);
            Assert.That(JsonUtility.ToJson(presenter.Session.Snapshot), Is.EqualTo(before));
            AssertVisibleTextFits(); Click("confirm_delivery");
            Assert.That(presenter.Session.Snapshot.completedStory, Is.EqualTo(1));
            Assert.That(presenter.Session.Snapshot.stones, Is.EqualTo(38));
            Click("response_rest"); Click("leave_rest");
            Assert.That(GameObject.Find("response_continue"), Is.Not.Null, "Unread response must survive a quiet moment");
        }

        private static void AssertVisibleTextFits()
        {
            foreach (var label in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
                if (label.isActiveAndEnabled && !string.IsNullOrEmpty(label.text))
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1), "Text clipped: " + label.text);
        }
    }
}
