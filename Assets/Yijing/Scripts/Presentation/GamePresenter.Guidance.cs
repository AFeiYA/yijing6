using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Yijing.Domain.Gameplay;

namespace Yijing.Presentation
{
    public sealed partial class GamePresenter
    {
        private StoryBook story;
        private GameSession returnSession;
        private int recoveryStage = -1;
        private GuideCue cue;
        private bool guideEnabled;
        private readonly List<Outline> focusOutlines = new List<Outline>();
        private Image guideGhost;

        private sealed class PracticeStore : IGameStore
        {
            private GameState state;
            public GameState Load() => state?.Copy();
            public void Save(GameState value) => state = value.Copy();
        }

        private void UpdateGuidance()
        {
            var s = Session.Snapshot;
            bool teach = !s.guide.skipped;
            guideEnabled = teach;
            bool firstOnly = teach && s.completedStory == 0;
            for (int i = 1; i < tabs.Count; i++) tabs[i].gameObject.SetActive(!firstOnly);
            tabs[0].GetComponent<RectTransform>().sizeDelta = new Vector2(firstOnly ? 488 : 160, 34);
            tabs[0].GetComponentInChildren<Text>().rectTransform.sizeDelta = new Vector2(firstOnly ? 476 : 148, 32);
            tabs[0].GetComponentInChildren<Text>().text = s.completedStory >= 3 ? "首幕已完成" :
                firstOnly ? story.beats[0].title : new[] { "1 · 落座", "2 · 留空白", "3 · 留茶" }[s.completedStory];
            foreach (var choice in choices) choice.gameObject.SetActive(s.firstMerge || !teach);
            ceramic.gameObject.SetActive(s.firstMerge || !teach);
            storeButton.gameObject.SetActive(s.firstMerge || !teach);
            recycle.gameObject.SetActive(s.firstMerge || !teach); undo.gameObject.SetActive(s.firstMerge || !teach);
            deliver.gameObject.SetActive(s.firstMerge);
            root.Find("sanctuary").gameObject.SetActive(s.completedStory > 0 || !teach);
            root.Find("journal").gameObject.SetActive(s.completedStory > 0 || !teach);
            if (!s.firstMerge) requirements.text = "两撮山茶 → 一包山茶\n先试一次，再为来客备茶";
            root.Find("help").GetComponentInChildren<Text>().text = returnSession == null ? "引导" : "退出练习";
            root.Find("help").GetComponentInChildren<Text>().fontSize = returnSession == null ? 18 : 14;
            cue = GuideDirector.Next(Session, Session.Order(orderSlot), variantIndex, selected, recoveryStage);
            narrative.text = cue.title + "\n" + cue.instruction;
            foreach (var outline in focusOutlines) if (outline != null) outline.enabled = false;
            focusOutlines.Clear();
            if (guideGhost != null) guideGhost.enabled = false;
            if (!teach) return;
            if (cue.target == "merge" || cue.target == "cell") {
                Focus(cells[cue.from].background);
                if (cue.to >= 0) {
                    Focus(cells[cue.to].background);
                    if (guideGhost == null) guideGhost = ImageAt(root, "Merge gesture hint", null, 0, 0, 40, 40, Color.white, true);
                    guideGhost.sprite = art.FindItem(s.board[cue.from].itemId);
                    guideGhost.transform.SetAsLastSibling();
                }
            } else if (cue.target == "choices") foreach (var choice in choices) Focus(choice.GetComponent<Image>());
            else { var target = root.Find(cue.target); if (target != null) Focus(target.GetComponent<Image>()); }
        }

        private void Focus(Image graphic)
        {
            if (graphic == null) return;
            var outline = graphic.GetComponent<Outline>() ?? graphic.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(2.5f, -2.5f); outline.useGraphicAlpha = false;
            outline.effectColor = new Color(.80f, .54f, .22f); outline.enabled = true; focusOutlines.Add(outline);
        }

        private void AnimateGuide()
        {
            bool show = Session != null && modal == null && !RoomVisible && dragFrom < 0 && guideEnabled;
            foreach (var outline in focusOutlines) if (outline != null) {
                outline.enabled = show;
                outline.effectColor = new Color(.80f, .54f, .22f, .65f + .25f * Mathf.Sin(Time.unscaledTime * 3));
            }
            if (guideGhost == null) return;
            guideGhost.enabled = show && cue?.target == "merge";
            if (!guideGhost.enabled) return;
            float t = (Time.unscaledTime % 2.6f) / 2.6f;
            var from = (RectTransform)cells[cue.from].transform; var to = (RectTransform)cells[cue.to].transform;
            guideGhost.rectTransform.anchoredPosition = Vector2.Lerp(from.anchoredPosition, to.anchoredPosition, Mathf.SmoothStep(0, 1, Mathf.Clamp01(t * 1.5f))) + new Vector2(13, -8);
            guideGhost.color = new Color(1, 1, 1, t < .82f ? .48f : 0);
        }

        private void ResumeGuidance()
        {
            if (modal != null || Session == null) return;
            var s = Session.Snapshot;
            if (s.guide.introStep < 2 && !s.guide.skipped) ShowIntroduction();
            else if (s.firstMerge && !s.guide.choiceExplained && !s.guide.skipped) ShowChoiceTutorial();
            else if (s.guide.responsesSeen < s.completedStory) ShowStoryResponse(s.guide.responsesSeen + 1);
        }

        private void ShowIntroduction(bool replay = false, int replayPage = 0)
        {
            int page = replay ? replayPage : Session.Snapshot.guide.introStep;
            RoomPage(page == 0 ? "隐庐 · 雨夜" : "第一位来客", page == 0 ? story.premise : story.arrival,
                page == 0 ? "让她进来" : "为她备一席茶", "intro_next", () => {
                    if (!replay) {
                        var result = Session.AdvanceIntroduction(Session.Snapshot.revision);
                        if (!result.Success) { roomBody.text = result.Message; return; }
                    }
                    if (page == 0) ShowIntroduction(replay, 1); else OpenTeaBoard();
                });
            RoomSecondary("skip_guide", returnSession == null ? "直接开店" : "退出练习", () => {
                if (returnSession != null) { ExitPractice(); return; }
                var result = Session.SetGuideSkipped(true, Session.Snapshot.revision);
                if (result.Success) OpenTeaBoard(); else roomBody.text = result.Message;
            });
        }

        private void ShowChoiceTutorial()
        {
            NewModal("第一包茶，合好了");
            ImageAt(modal, "First merge", art.FindItem("tea_02"), 210, 181, 120, 120, Color.white, true);
            Label(modal, "接下来，你可以选择怎样回应 Elena。", 48, 330, 444, 68, 23);
            Label(modal, "静 · 先听她说\n行 · 陪她做一件小事\n\n两种方式需要不同备品，灵石相同。没有标准答案，交付前都能更换。", 48, 419, 444, 206, 21);
            Label(modal, "先选一种方式。需要什么物品，就合成到对应阶数。", 48, 650, 444, 78, 20, Jade);
            ActionButton(modal, "choice_understood", "让我选一种回应", 48, 771, 444, 58, () => {
                var result = Session.ExplainChoice(Session.Snapshot.revision);
                if (result.Success) CloseModal(); Run(result, "点选静或行，查看需求。");
            });
            var close = modal.Find("close_modal").GetComponent<Button>();
            close.onClick.RemoveAllListeners(); close.onClick.AddListener(() => {
                var result = Session.ExplainChoice(Session.Snapshot.revision);
                if (result.Success) CloseModal(); Run(result, "可以随时更换回应。");
            });
        }

        private void ChooseResponse(int index)
        {
            var result = Session.ChooseResponse(index, Session.Snapshot.revision);
            if (result.Success) variantIndex = index;
            Run(result, "需求已更新；点击需求可查看合成路径。");
        }

        private void ShowStoryBrief(int index)
        {
            var beat = story.beats[index];
            RoomPage(beat.title, beat.request, "回茶案 · 备这一席", "story_begin", () => { orderSlot = 0; OpenTeaBoard(); });
            RoomSecondary("brief_rest", "先在这里坐一会儿", EnterQuietMoment);
        }

        private void ShowStoryResponse(int completed)
        {
            var s = Session.Snapshot; var beat = story.beats[completed - 1]; var choice = s.storyChoices[completed - 1];
            RoomPage(beat.title, choice == "act" ? beat.actResponse : choice == "quiet" ? beat.quietResponse : beat.takeaway,
                completed == 1 && !s.lampRepaired ? "替门边添一盏灯" : completed < 3 ? "听她接着说" : "送她到门口", "response_continue", () => {
                    if (Session.Snapshot.guide.responsesSeen < completed) {
                        var result = Session.AcknowledgeStory(completed, Session.Snapshot.revision);
                        if (!result.Success) { roomBody.text = result.Message; return; }
                    }
                    if (completed == 1 && !Session.Snapshot.lampRepaired) ShowSanctuary();
                    else if (completed < 3) ShowStoryBrief(completed);
                    else ShowChapterEnding();
                });
            roomVisitor.enabled = true;
            roomCaption.text = "她留下了 " + config.orders[completed - 1].rewardStones + " 灵石谢礼";
            RoomSecondary("response_rest", "陪她听一会儿雨", EnterQuietMoment);
        }

        private void ShowChapterEnding()
        {
            RoomPage("今晚 · 茶还温着", story.ending, "把这一刻留在手账", "ending_continue", () => {
                if (Session.Today != null) ShowJournal(); else ShowRoomHome();
            });
            RoomSecondary("ending_rest", "再坐一会儿", EnterQuietMoment);
        }

        private void ShowRecipe()
        {
            var order = Session.Order(orderSlot);
            var target = order?.variants[variantIndex].requirements.FirstOrDefault(r => Session.Snapshot.board.Count(x => x.itemId == r.itemId) < r.quantity)
                ?? order?.variants[variantIndex].requirements[0];
            RenderRecipe(target?.itemId ?? "tea_03");
        }
        private void RenderRecipe(string itemId)
        {
            NewModal("物品从哪里来"); var item = Session.Item(itemId);
            Label(modal, (item.chainId == "tea" ? "茶台产出山茶" : "器架产出小盏") + "，相同的两件合成下一阶。\n合到需求阶数即可，不必继续升级。", 48, 167, 444, 76, 20);
            var path = config.items.Where(x => x.chainId == item.chainId && x.tier <= item.tier).OrderBy(x => x.tier).ToArray();
            for (int i = 0; i < path.Length; i++) {
                ImageAt(modal, "Recipe icon", art.FindItem(path[i].id), 67, 274 + i * 85, 68, 68, Color.white, true);
                Label(modal, path[i].tier + " 阶 · " + path[i].displayNameZh + (i == path.Length - 1 ? "（目标）" : " × 2"), 155, 278 + i * 85, 330, 62, 21);
            }
            Label(modal, "高阶不能直接抵低阶。升过头时可以放回工具台，再取回基础材料。", 48, 724, 444, 85, 19, Jade);
            var other = Session.Order(orderSlot)?.variants[variantIndex].requirements.FirstOrDefault(x => Session.Item(x.itemId).chainId != item.chainId);
            if (other != null) ActionButton(modal, "other_recipe", "查看另一种需求的路径", 48, 827, 444, 48, () => RenderRecipe(other.itemId));
        }

        private void ShowHelp()
        {
            NewModal("山房引导");
            Label(modal, story.chapterTitle + "\n落座 → 点灯 → 留空白 → 留茶\n\n" + (Session.Snapshot.completedStory >= 3 ? story.nextChapter : story.beats[Session.Snapshot.completedStory].takeaway), 48, 168, 444, 232, 20);
            ActionButton(modal, "help_story", "重读当前故事与回应", 48, 420, 444, 48, () => {
                if (Session.Snapshot.completedStory >= 3) ShowChapterEnding(); else ShowStoryBrief(Session.Snapshot.completedStory);
            });
            ActionButton(modal, "help_recipe", "查看物品合成路径", 48, 485, 444, 48, ShowRecipe);
            ActionButton(modal, "help_recovery", "练习整理 · 回收与撤销", 48, 550, 444, 48, () => { recoveryStage = 0; OpenTeaBoard(); });
            ActionButton(modal, "toggle_guide", Session.Snapshot.guide.skipped ? "重新开启操作指引" : "关闭操作指引", 48, 615, 444, 48, () => {
                var result = Session.SetGuideSkipped(!Session.Snapshot.guide.skipped, Session.Snapshot.revision);
                if (result.Success) CloseModal(); Run(result, "操作指引已更新。");
            });
            ActionButton(modal, "practice_from_start", "从头练习 · 不影响当前进度", 48, 707, 444, 54, StartPractice);
            ActionButton(modal, "replay_intro", "重看开场", 48, 789, 444, 48, () => ShowIntroduction(true));
            Label(modal, "练习使用独立临时进度。退出练习会回到你的原茶案，不会改动灵石、故事或卦卡。", 48, 853, 444, 92, 17, Jade);
        }
        private void StartPractice()
        {
            CloseModal(); CancelRoomActivity(); returnSession = Session;
            Session = new GameSession(config, new PracticeStore(), () => DateTimeOffset.Now);
            selected = -1; orderSlot = 0; variantIndex = 0; recoveryStage = -1; includeInventory = false;
            Refresh(); ShowIntroduction();
        }
        private void ExitPractice()
        {
            if (returnSession == null) return;
            CloseModal(); CancelRoomActivity(); CancelDrag(); Session = returnSession; returnSession = null;
            selected = -1; orderSlot = 0; variantIndex = Session.Snapshot.guide.preferredVariant; recoveryStage = -1;
            Refresh(); ShowRoomHome(); ResumeGuidance(); Tell("已返回原来的茶案，练习没有改动你的进度。");
        }
    }
}
