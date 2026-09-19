using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Yijing.Presentation
{
    public sealed partial class GamePresenter
    {
        private RectTransform room, roomCard, roomActions, restReturn;
        private Text roomTitle, roomBody, roomCaption;
        private Image roomVisitor, roomPhone, roomPaper, roomCup, roomWarmth;
        private Button roomPrimary, roomSound, roomMotion;
        private RoomAtmosphere atmosphere;
        private SanctuarySound sound;
        private bool resting, reducedMotion, pouring;
        private float pourStarted, breathStarted;
        private Action afterPour;
        private bool RoomVisible => room != null && room.gameObject.activeSelf;

        private void BuildRoomView()
        {
            sound = gameObject.AddComponent<SanctuarySound>();
            reducedMotion = PlayerPrefs.GetInt("Yijing.ReducedMotion", 0) == 1;
            room = Panel(root, "Living tea room", 0, 0, 540, 960, Ink);
            ImageAt(room, "Room painting", art.sanctuaryBase, 0, 0, 540, 960, Color.white);
            roomWarmth = ImageAt(room, "Evening lamplight", null, 0, 0, 540, 960, Color.clear);
            // Scene props are separate from the flattened background so choices have a visible consequence.
            roomVisitor = ImageAt(room, "Seated Elena", art.elenaPortrait, 317, 227, 153, 206, Color.white, true);
            var phone = Panel(room, "Phone on table", 330, 450, 27, 43, new Color(.16f, .22f, .21f));
            phone.localEulerAngles = new Vector3(0, 0, -12); roomPhone = phone.GetComponent<Image>();
            roomPaper = ImageAt(room, "Work note", null, 356, 476, 38, 25, new Color(.90f, .87f, .70f));
            roomPaper.rectTransform.localEulerAngles = new Vector3(0, 0, 9);
            roomCup = ImageAt(room, "Served cup", art.FindItem("ceramic_03"), 248, 428, 64, 64, Color.white, true);
            var fx = Rect(room, "Rain and tea steam", 0, 0, 540, 960);
            atmosphere = fx.gameObject.AddComponent<RoomAtmosphere>(); atmosphere.raycastTarget = false;
            var top = Panel(room, "Room controls", 0, 0, 540, 72, new Color(.09f, .17f, .15f, .62f));
            Label(top, "隐 庐  ·  听 雨", 24, 13, 209, 40, 23, Paper);
            roomSound = ActionButton(top, "room_sound", "", 259, 15, 83, 40, () => { sound.SetMuted(!sound.Muted); UpdateRoomSettings(); }, 16);
            roomMotion = ActionButton(top, "room_motion", "", 350, 15, 83, 40, () => {
                reducedMotion = !reducedMotion; PlayerPrefs.SetInt("Yijing.ReducedMotion", reducedMotion ? 1 : 0); PlayerPrefs.Save(); UpdateRoomSettings();
            }, 16);
            ActionButton(top, "room_help", "引导", 441, 15, 76, 40, () => { if (returnSession != null) ExitPractice(); else ShowHelp(); }, 16);
            // Optional, non-rewarding interactions. No timer, streak or currency is attached to lingering.
            RoomHotspot("listen_rain", 160, 92, 281, 197, () => { if (resting) roomCaption.text = "雨落在竹叶上，又滑进庭院里。"; });
            RoomHotspot("touch_chime", 478, 95, 48, 100, () => { sound.Chime(); if (resting) roomCaption.text = "风铃轻轻响了一声。"; });
            roomCard = Panel(room, "Room conversation", 22, 596, 496, 338, new Color(.95f, .94f, .88f, .94f));
            roomTitle = Label(roomCard, "", 22, 13, 452, 41, 23);
            roomBody = Label(roomCard, "", 22, 57, 452, 139, 19);
            roomActions = Rect(roomCard, "Room actions", 0, 0, 496, 338);
            var captionShade = Panel(room, "Scene caption shade", 22, 542, 496, 48, new Color(.10f, .18f, .16f, .54f));
            captionShade.GetComponent<Image>().raycastTarget = false;
            roomCaption = Label(captionShade, "", 6, 0, 484, 48, 16, Paper, TextAnchor.MiddleCenter);
            restReturn = Rect(room, "Rest return", 0, 0, 540, 960);
            ActionButton(restReturn, "leave_rest", "回到茶舍", 170, 873, 200, 46, () => { ShowRoomHome(); ResumeGuidance(); });
            restReturn.gameObject.SetActive(false);
            UpdateRoomSettings();
        }

        private void RoomHotspot(string name, float x, float y, float w, float h, Action action)
        {
            var b = ActionButton(room, name, "", x, y, w, h, () => action()); b.GetComponent<Image>().color = Color.clear;
        }
        private void UpdateRoomSettings()
        {
            roomSound.GetComponentInChildren<Text>().text = sound.Muted ? "声音 关" : "声音 开";
            roomMotion.GetComponentInChildren<Text>().text = reducedMotion ? "动态 关" : "动态 开";
            atmosphere.Motion = !reducedMotion;
        }
        private void CancelRoomActivity()
        {
            pouring = false; afterPour = null; sound?.StopPour();
        }
        private void RoomPage(string title, string body, string caption, string actionName, Action action)
        {
            CloseModal(); CancelDrag(); CancelRoomActivity(); resting = false;
            room.gameObject.SetActive(true); room.SetAsLastSibling();
            roomCard.gameObject.SetActive(true); restReturn.gameObject.SetActive(false);
            roomTitle.text = title; roomBody.text = body; roomCaption.text = "";
            for (int i = roomActions.childCount - 1; i >= 0; i--) { var child = roomActions.GetChild(i); child.gameObject.SetActive(false); Destroy(child.gameObject); }
            roomPrimary = ActionButton(roomActions, actionName, caption, 22, 215, 452, 48, () => action(), 20);
            ApplyRoomProgress();
        }
        private void RoomSecondary(string name, string label, Action action) => ActionButton(roomActions, name, label, 22, 278, 452, 40, () => action(), 17);

        private void ApplyRoomProgress()
        {
            var s = Session.Snapshot;
            roomVisitor.enabled = s.guide.introStep > 0 && s.completedStory < 3;
            roomWarmth.color = s.lampRepaired ? new Color(1f, .77f, .40f, .075f) : new Color(.08f, .16f, .18f, .08f);
            bool phonePutAside = s.completedStory >= 1 && s.storyChoices[0] == "act";
            roomPhone.gameObject.SetActive(s.guide.introStep > 0 && s.completedStory < 3);
            roomPhone.rectTransform.anchoredPosition = new Vector2(phonePutAside ? 432 : 330, phonePutAside ? -432 : -450);
            roomPhone.color = s.completedStory > 0 ? new Color(.12f, .16f, .15f) : new Color(.43f, .61f, .62f);
            roomPaper.enabled = s.completedStory < 2 && s.guide.introStep > 0;
            roomCup.enabled = s.completedStory > 0;
            atmosphere.Steam = s.completedStory > 0;
            atmosphere.Pouring = false;
        }

        private void ShowRoomHome()
        {
            if (room == null || Session == null) return;
            var s = Session.Snapshot;
            string body = s.completedStory >= 3 ? "来客已经离开。杯里还有余温，雨还在下。\n这里暂时没有什么事需要你完成。" : story.beats[s.completedStory].request;
            RoomPage(s.completedStory >= 3 ? "灯下 · 茶还温着" : story.beats[s.completedStory].title, body,
                s.completedStory >= 3 ? "在窗边坐一会儿" : s.completedStory == 1 && !s.lampRepaired ? "给门边添一盏灯" : "为她备一席茶", "room_prepare", () => {
                    if (Session.Snapshot.completedStory >= 3) EnterQuietMoment();
                    else if (Session.Snapshot.completedStory == 1 && !Session.Snapshot.lampRepaired) ShowSanctuary();
                    else OpenTeaBoard();
                });
            RoomSecondary(s.completedStory >= 3 ? "room_journal" : "room_rest", s.completedStory >= 3 ? "翻一页手账" : "先听一会儿雨", () => {
                if (Session.Snapshot.completedStory >= 3) ShowJournal(); else EnterQuietMoment();
            });
            if (s.completedStory >= 3)
                ActionButton(roomActions, "room_regular", "接待常客", 340, -51, 134, 40, () => { orderSlot = 1; OpenTeaBoard(); }, 16);
        }
        private void OpenTeaBoard()
        {
            CloseModal(); CancelRoomActivity(); resting = false; room.gameObject.SetActive(false); Refresh();
            ResumeGuidance();
        }
        private void EnterQuietMoment()
        {
            CloseModal(); CancelRoomActivity(); room.gameObject.SetActive(true); room.SetAsLastSibling();
            ApplyRoomProgress(); resting = true; breathStarted = Time.unscaledTime;
            roomCard.gameObject.SetActive(false); restReturn.gameObject.SetActive(true);
            roomCaption.text = "可以什么都不做。听一会儿雨。";
        }
        private void AnimateRoom()
        {
            if (!RoomVisible) return;
            if (pouring) {
                float t = Mathf.Clamp01((Time.unscaledTime - pourStarted) / 2.4f);
                atmosphere.Fill = t;
                if (t >= 1f) { var done = afterPour; CancelRoomActivity(); done?.Invoke(); }
            }
            if (resting) {
                float a = Time.unscaledTime - breathStarted < 7 ? 1 : .28f;
                roomCaption.color = new Color(Paper.r, Paper.g, Paper.b, a);
            } else roomCaption.color = Paper;
        }

        private void ShowTeaRitual()
        {
            var session = Session; var order = session.Order(orderSlot); int slot = orderSlot;
            if (order == null) return;
            var variant = order.variants[variantIndex]; bool useStorage = includeInventory;
            var ids = session.SelectMaterials(variant, useStorage);
            if (ids == null) { OpenTeaBoard(); Tell("还差一点备品，点需求可查看合成路径。"); return; }
            long revision = session.Snapshot.revision; string command = Guid.NewGuid().ToString("N");
            string summary = string.Join("、", variant.requirements.Select(x => session.Item(x.itemId).displayNameZh + " ×" + x.quantity));
            Action<bool, bool> preview = (cup, steam) => {
                roomCup.enabled = cup; atmosphere.Steam = steam;
                if (variant.id == "act") { roomPhone.rectTransform.anchoredPosition = new Vector2(432, -432); roomPaper.enabled = false; }
            };
            Action serve = () => {
                if (Session != session) return;
                var result = session.Deliver(slot, order.id, variant.id, ids, useStorage, command, revision);
                if (!result.Success) { ShowRoomHome(); roomBody.text = result.Message + "\n回茶案重新准备即可。"; return; }
                sound.Cup(); selected = -1; Refresh(); ShowStoryResponse(session.Snapshot.completedStory);
            };
            Action ready = () => {
                RoomPage("这一杯 · 已经温热", "把茶放在她伸手可及的地方。\n" + summary, "把茶递给她", "confirm_delivery", serve);
                preview(true, true); atmosphere.Fill = 1;
                RoomSecondary("cancel_ritual", "先放下，回茶案", OpenTeaBoard);
            };
            Action pour = () => {
                RoomPage("水声 · 落进杯里", "水慢慢注入杯中。\n不必赶时间。", "正在倒茶…", "pour_wait", () => { });
                roomPrimary.interactable = false; preview(true, true); atmosphere.Pouring = true; atmosphere.Fill = 0;
                pouring = true; pourStarted = Time.unscaledTime; afterPour = ready; sound.Pour();
                RoomSecondary("finish_pour", "直接端茶", () => { CancelRoomActivity(); ready(); });
            };
            RoomPage("在她面前 · 放一只杯", "备好了：" + summary + "。\n先把杯子放好，再慢慢倒茶。", "轻轻放下杯子", "place_cup", () => {
                sound.Cup();
                RoomPage("杯子放稳了", variant.id == "quiet" ? "先不说话，给雨声留一点位置。" : "把手边的东西挪开，给这一杯腾个位置。", "倒一杯温茶", "pour_tea", pour);
                preview(true, false);
                RoomSecondary("cancel_ritual", "回茶案", OpenTeaBoard);
            });
            roomCup.enabled = false; atmosphere.Steam = false;
            RoomSecondary("cancel_ritual", "回茶案 · 备品仍保留", OpenTeaBoard);
        }
    }
}
