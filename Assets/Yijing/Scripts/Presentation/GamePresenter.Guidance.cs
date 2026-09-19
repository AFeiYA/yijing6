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
            bool show = Session != null && modal == null && dragFrom < 0 && guideEnabled;
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
            NewModal(page == 0 ? story.chapterTitle : "雨夜的第一位来客");
            var closeIntro = modal.Find("close_modal").GetComponent<Button>();
            closeIntro.gameObject.SetActive(replay || returnSession != null);
            if (returnSession != null) {
                closeIntro.GetComponentInChildren<Text>().text = "退出";
                closeIntro.onClick.RemoveAllListeners(); closeIntro.onClick.AddListener(ExitPractice);
            }
            ImageAt(modal, "Opening art", page == 0 ? art.sanctuaryBase : art.elenaPortrait, 130, 172, 280, 260, Color.white, true);
            Label(modal, page == 0 ? story.premise : story.arrival, 52, 460, 436, 240, 22);
            ActionButton(modal, "intro_next", page == 0 ? "迎接第一位来客" : "开始备茶", 48, 756, 444, 58, () => {
                if (!replay) {
                    var result = Session.AdvanceIntroduction(Session.Snapshot.revision);
                    if (!result.Success) { Run(result, ""); return; }
                }
                CloseModal(); Refresh();
                if (page == 0) ShowIntroduction(replay, 1);
            });
            if (!replay) ActionButton(modal, "skip_guide", "熟悉合成，直接开店", 48, 842, 444, 46, () => {
                var result = Session.SetGuideSkipped(true, Session.Snapshot.revision);
                if (result.Success) CloseModal(); Run(result, "已关闭操作指引，可随时从右上角重新打开。");
            }, 18);
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
            var beat = story.beats[index]; NewModal(beat.title);
            ImageAt(modal, "Elena", art.elenaPortrait, 184, 170, 172, 182, Color.white, true);
            Label(modal, beat.request, 48, 392, 444, 220, 23);
            Label(modal, "备齐物件 → 交付茶席 → 来客回应\n每次交付都让这一幕向前一步。", 48, 640, 444, 84, 19, Jade);
            ActionButton(modal, "story_begin", "回茶案，准备这一席", 48, 754, 444, 56, () => { orderSlot = 0; CloseModal(); Refresh(); });
            if (index == 1 && !Session.Snapshot.guide.undoneOnce)
                ActionButton(modal, "learn_recovery", "先学整理 · 放回与撤销", 48, 833, 444, 48, () => { CloseModal(); recoveryStage = 0; Refresh(); });
        }

        private void ShowStoryResponse(int completed)
        {
            var s = Session.Snapshot; var beat = story.beats[completed - 1];
            string choice = s.storyChoices[completed - 1];
            NewModal(beat.title + " · 回应");
            ImageAt(modal, "Elena", art.elenaPortrait, 184, 158, 172, 174, Color.white, true);
            Label(modal, choice == "act" ? beat.actResponse : choice == "quiet" ? beat.quietResponse : beat.takeaway, 48, 360, 444, 228, 22);
            Label(modal, "已获得 " + config.orders[completed - 1].rewardStones + " 灵石 · " + (choice == "act" ? "行的回应" : choice == "quiet" ? "静的回应" : "过去的茶席"), 48, 603, 444, 43, 19, Jade);
            Label(modal, beat.takeaway, 48, 666, 444, 75, 21);
            Action proceed = () => {
                if (Session.Snapshot.guide.responsesSeen < completed) {
                    var result = Session.AcknowledgeStory(completed, Session.Snapshot.revision);
                    if (!result.Success) { Run(result, ""); return; }
                }
                CloseModal(); Refresh();
                if (completed == 1 && !Session.Snapshot.lampRepaired) ShowSanctuary();
                else if (completed < 3) ShowStoryBrief(completed);
                else ShowChapterEnding();
            };
            ActionButton(modal, "response_continue", completed == 1 ? "下一步 · 点亮门灯" : completed == 2 ? "下一步 · 为她留茶" : "送她到门口", 48, 784, 444, 58, () => proceed());
            var close = modal.Find("close_modal").GetComponent<Button>();
            close.onClick.RemoveAllListeners(); close.onClick.AddListener(() => {
                if (Session.Snapshot.guide.responsesSeen < completed) {
                    var result = Session.AcknowledgeStory(completed, Session.Snapshot.revision);
                    if (!result.Success) { Run(result, ""); return; }
                }
                CloseModal(); Refresh();
            });
        }

        private void ShowChapterEnding()
        {
            NewModal("第一幕 · 今晚到这里");
            Label(modal, story.ending, 48, 178, 444, 260, 22);
            Label(modal, "这一幕的三次回应\n" + string.Join(" → ", Session.Snapshot.storyChoices.Select(x => x == "quiet" ? "静" : x == "act" ? "行" : "未记录")), 48, 453, 444, 90, 21, Jade);
            Label(modal, "每日最先交付的三份茶席（含常客），从下往上留爻。静为阴，行为阳；跨日分别记录，不为选择打分。", 48, 570, 444, 119, 20);
            ActionButton(modal, "ending_continue", Session.Today?.lines.Count == 3 ? "翻开今日卦卡" : "接待常客，续写今日小记", 48, 749, 444, 56, () => {
                CloseModal(); if (Session.Today?.lines.Count == 3) ShowJournal(); else { orderSlot = 1; Refresh(); }
            });
            ActionButton(modal, "ending_rest", "今天先到这里 · 进度已保存", 48, 833, 444, 48, () => { CloseModal(); Refresh(); Tell("随时可以回来，茶席会留在这里。"); });
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
            ActionButton(modal, "help_recovery", "练习整理 · 回收与撤销", 48, 550, 444, 48, () => { CloseModal(); recoveryStage = 0; Refresh(); });
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
            CloseModal(); returnSession = Session;
            Session = new GameSession(config, new PracticeStore(), () => DateTimeOffset.Now);
            selected = -1; orderSlot = 0; variantIndex = 0; recoveryStage = -1; includeInventory = false;
            Refresh(); ShowIntroduction();
        }
        private void ExitPractice()
        {
            if (returnSession == null) return;
            CloseModal(); CancelDrag(); Session = returnSession; returnSession = null;
            selected = -1; orderSlot = 0; variantIndex = Session.Snapshot.guide.preferredVariant; recoveryStage = -1;
            Refresh(); Tell("已返回原来的茶案，练习没有改动你的进度。");
        }
    }
}
