using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Yijing.Domain.Configuration;
using Yijing.Domain.Gameplay;
using Yijing.Infrastructure;

namespace Yijing.Presentation
{
    public sealed class GamePresenter : MonoBehaviour
    {
        [SerializeField] private PrototypeConfigAsset configuration;
        [SerializeField] private ArtCatalog art;
        [SerializeField] private Font font;
        public GameSession Session { get; private set; }
#if UNITY_EDITOR
        public static string SavePathForTesting;
#endif
        private PrototypeConfig config;
        private RectTransform root, modal, dragGhost;
        private Text wallet, narrative, requirements, status, selection, daily, inventoryLabel;
        private Button ceramic, deliver, storeButton, recycle, undo;
        private readonly List<BoardCellView> cells = new List<BoardCellView>();
        private readonly List<Button> tabs = new List<Button>(), choices = new List<Button>();
        private int selected = -1, orderSlot, variantIndex;
        private int dragFrom = -1, dragPointer;
        private long dragRevision, dragInstance;
        private string lastDate;
        private bool includeInventory;
        private static readonly Color Ink = new Color(.17f, .25f, .23f);
        private static readonly Color Jade = new Color(.30f, .45f, .40f);
        private static readonly Color Paper = new Color(.95f, .94f, .88f);
        private static readonly Color Pale = new Color(.84f, .88f, .80f);

        private void Start()
        {
            BuildCanvas();
            try {
                if (configuration == null || art == null || font == null) throw new InvalidDataException("游戏资源尚未装配，请运行 Yijing > Playable > Prepare Scene。");
                config = configuration.CreateSnapshot();
                string path = Path.Combine(UnityEngine.Application.persistentDataPath, "tea-room-v1.json");
#if DEVELOPMENT_BUILD
                if (PreviewSmokeCapture.Enabled) path = PreviewSmokeCapture.IsolatedSavePath;
#endif
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(SavePathForTesting)) path = SavePathForTesting;
#endif
                var repository = new JsonGameStore(path, config);
                Session = new GameSession(config, repository, () => DateTimeOffset.Now);
                BuildTeaTable(); Refresh();
                Tell(repository.RecoveryNotice ?? (Session.Snapshot.firstMerge ? "茶席已恢复，按自己的节奏继续。" : "取两撮山茶，拖到一起，开始今天的茶席。"));
            } catch (Exception e) {
                Debug.LogError("Yijing could not start: " + e.Message);
                Label(root, "暂时无法打开茶舍", 36, 180, 468, 60, 28);
                Label(root, e.Message, 36, 260, 468, 260, 21);
            }
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("Yijing Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            root = Rect(canvasObject.transform, "Safe portrait layout", 0, 0, 540, 960);
            root.anchorMin = root.anchorMax = Vector2.zero; root.pivot = new Vector2(.5f, .5f);
            ImageAt(root, "Sanctuary backdrop", art == null ? null : art.sanctuaryBase, 0, 0, 540, 960, Color.white);
            Panel(root, "Tea table wash", 0, 148, 540, 812, new Color(.94f, .94f, .88f, .96f));
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            FitSafeArea();
        }

        private void Update()
        {
            if (root != null) FitSafeArea();
            if (Session != null && lastDate != DateTimeOffset.Now.ToString("yyyy-MM-dd")) {
                lastDate = DateTimeOffset.Now.ToString("yyyy-MM-dd"); Refresh();
            }
        }

        private void FitSafeArea()
        {
            var safe = Screen.safeArea;
            float scale = Mathf.Min(safe.width / 540f, safe.height / 960f);
            root.localScale = Vector3.one * scale;
            root.anchoredPosition = safe.center;
        }

        private void BuildTeaTable()
        {
            Panel(root, "Header shade", 0, 0, 540, 62, new Color(.10f, .20f, .18f, .76f));
            Label(root, "易 · 境", 26, 6, 220, 50, 29, Color.white);
            wallet = Label(root, "", 280, 19, 234, 32, 19, Color.white, TextAnchor.MiddleRight);
            ImageAt(root, "Elena", art.elenaPortrait, 16, 64, 98, 100, Color.white, true);
            Panel(root, "Visitor words", 119, 75, 395, 72, new Color(.96f, .96f, .90f, .92f));
            narrative = Label(root, "", 134, 79, 365, 65, 18);
            for (int i = 0; i < 3; i++) {
                int slot = i;
                tabs.Add(ActionButton(root, "order_" + i, i == 0 ? "Elena · 故事" : "常客 " + i,
                    26 + i * 164, 170, 160, 34, () => { orderSlot = slot; variantIndex = 0; Refresh(); }));
            }
            choices.Add(ActionButton(root, "choice_quiet", "静 · 先听她说", 26, 216, 238, 42, () => { variantIndex = 0; Refresh(); }));
            choices.Add(ActionButton(root, "choice_act", "行 · 一起整理", 276, 216, 238, 42, () => { variantIndex = 1; Refresh(); }));
            requirements = Label(root, "", 29, 262, 334, 60, 18);
            deliver = ActionButton(root, "deliver", "准备交付", 372, 269, 142, 42, ShowDelivery);
            for (int i = 0; i < Session.OpenSlots; i++) {
                int col = i % 7, row = i / 7;
                var rect = Panel(root, "cell_" + i, 26 + col * 70, 326 + row * 66, 66, 62, Color.white);
                var cell = rect.gameObject.AddComponent<BoardCellView>();
                cell.owner = this; cell.index = i; cell.background = rect.GetComponent<Image>();
                cell.icon = ImageAt(rect, "item", null, 5, 2, 56, 54, Color.white, true);
                cell.tier = Label(rect, "", 38, 38, 26, 24, 12, Ink, TextAnchor.MiddleCenter);
                cells.Add(cell);
            }
            Label(root, "另有 3 行空间，将在后续故事中开放", 26, 725, 488, 26, 14, Jade, TextAnchor.MiddleCenter);
            ActionButton(root, "produce_tea", "茶台 · 取一撮山茶", 26, 762, 238, 48, () => Produce("tea"));
            ceramic = ActionButton(root, "produce_ceramic", "器架 · 取一只小盏", 276, 762, 238, 48, () => Produce("ceramic"));
            storeButton = ActionButton(root, "inventory", "", 26, 824, 160, 44, InventoryAction);
            inventoryLabel = storeButton.GetComponentInChildren<Text>();
            recycle = ActionButton(root, "recycle", "放回工具台", 194, 824, 160, 44, RecycleSelected);
            undo = ActionButton(root, "undo", "撤销回收", 362, 824, 152, 44, () => Run(Session.Undo(Session.Snapshot.revision), "物品已回到原位。"));
            selection = Label(root, "", 26, 872, 488, 23, 14, Ink, TextAnchor.MiddleCenter);
            status = Label(root, "", 26, 896, 488, 23, 14, Jade, TextAnchor.MiddleCenter);
            ActionButton(root, "sanctuary", "山房 · 门边灯", 26, 922, 238, 36, ShowSanctuary, 16);
            var diary = ActionButton(root, "journal", "", 276, 922, 238, 36, () => ShowJournal(), 16);
            daily = diary.GetComponentInChildren<Text>();
        }

        private void Refresh()
        {
            if (Session == null || wallet == null) return;
            var s = Session.Snapshot;
            wallet.text = "灵石 " + s.stones + "  ·  " + s.completedStory + " / 3";
            if (selected >= 0 && s.board[selected].Empty) selected = -1;
            for (int i = 0; i < cells.Count; i++) {
                var item = s.board[i]; var cell = cells[i];
                cell.background.color = selected == i ? new Color(.66f, .79f, .65f) : new Color(.99f, .99f, .96f, .92f);
                cell.icon.sprite = item.Empty ? null : art.FindItem(item.itemId); cell.icon.enabled = !item.Empty;
                cell.tier.text = item.Empty ? "" : Session.Item(item.itemId).tier.ToString();
            }
            var order = Session.Order(orderSlot);
            for (int i = 0; i < tabs.Count; i++) tabs[i].GetComponent<Image>().color = i == orderSlot ? Jade : Pale;
            for (int i = 0; i < choices.Count; i++) {
                choices[i].interactable = s.firstMerge && order != null;
                choices[i].GetComponent<Image>().color = i == variantIndex ? Jade : Pale;
                choices[i].GetComponentInChildren<Text>().color = i == variantIndex ? Color.white : Ink;
            }
            for (int i = 0; i < tabs.Count; i++) tabs[i].GetComponentInChildren<Text>().color = i == orderSlot ? Color.white : Ink;
            if (!s.firstMerge) narrative.text = "Elena：我想把手机放远一点。\n先将两撮山茶拖到一起吧。";
            else if (orderSlot == 0) narrative.text = new[] {
                "Elena：今天不太想整理思路。\n可以在这里坐一会吗？",
                "Elena：手机安静下来以后，\n我才发现自己一直没有歇过。",
                "Elena：还有些事没做完。\n不过，也许可以先喝完这杯茶。",
                "这一段故事已结束。\n今天到这里也很好，也可继续接待常客。"
            }[s.completedStory];
            else narrative.text = GameSession.RegularTitle(s.regularOrders[orderSlot - 1]) + "\n为这位来客准备一份温柔的茶席。";
            requirements.text = order == null ? "首段已完成 · 后续故事开发中" : string.Join("\n", order.variants[variantIndex].requirements.Select(r => {
                int count = s.board.Count(x => !x.Empty && x.itemId == r.itemId);
                return Session.Item(r.itemId).displayNameZh + "  " + Math.Min(count, r.quantity) + "/" + r.quantity;
            }));
            deliver.interactable = s.firstMerge && order != null;
            deliver.GetComponentInChildren<Text>().text = order == null ? "已完成" : "交付 · +" + order.rewardStones;
            ceramic.interactable = s.firstMerge;
            recycle.interactable = selected >= 0;
            inventoryLabel.text = selected >= 0 ? "存入储物架" : "储物架  " + s.inventory.Count(x => !x.Empty) + "/6";
            undo.interactable = Session.CanUndo;
            selection.text = selected < 0 ? "拖动相同物品合成，也可依次点击两个格子" : Session.Item(s.board[selected].itemId).displayNameZh + " · " + Session.Item(s.board[selected].itemId).tier + " 阶　点另一格移动或合成";
            daily.text = Session.Today?.lines.Count == 3 ? "今日手账 · 查看卦卡" : "今日手账 · " + (Session.Today?.lines.Count ?? 0) + " / 3";
        }

        public void Produce(string chain) => Run(Session.Produce(chain, Session.Snapshot.revision), chain == "tea" ? "取来一撮山茶。" : "取来一只小盏。");

        public void SelectCell(int index)
        {
            if (modal != null || dragFrom >= 0) return;
            var s = Session.Snapshot;
            if (selected >= 0 && selected != index) {
                MoveSelected(selected, index, s.board[selected].instanceId, s.revision); selected = -1;
            } else selected = !s.board[index].Empty && selected != index ? index : -1;
            Refresh();
        }

        private void MoveSelected(int from, int to, long id, long revision)
        {
            var before = Session.Snapshot;
            bool merge = to >= 0 && to < cells.Count && !before.board[from].Empty && before.board[from].itemId == before.board[to].itemId && Session.Item(before.board[from].itemId).tier < 5;
            var result = Session.Move(from, to, id, revision);
            Run(result, merge ? "合成了 " + Session.Item(Session.Snapshot.board[to].itemId).displayNameZh + "。" : "物品已安放。");
            if (result.Success && merge) StartCoroutine(Pulse(cells[to].icon.rectTransform));
        }

        public void BeginItemDrag(int index, PointerEventData e)
        {
            if (dragFrom >= 0 || modal != null || e.button != PointerEventData.InputButton.Left) return;
            var s = Session.Snapshot; if (s.board[index].Empty) return;
            dragFrom = index; dragPointer = e.pointerId; dragRevision = s.revision; dragInstance = s.board[index].instanceId;
            dragGhost = ImageAt(root, "Dragging item", art.FindItem(s.board[index].itemId), 0, 0, 70, 70, Color.white, true).rectTransform;
            dragGhost.pivot = new Vector2(.5f, .5f); DragItem(e);
        }
        public void DragItem(PointerEventData e)
        {
            if (dragFrom < 0 || e.pointerId != dragPointer || dragGhost == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, e.position, null, out var point);
            dragGhost.localPosition = point;
        }
        public void EndItemDrag(PointerEventData e)
        {
            if (dragFrom < 0 || e.pointerId != dragPointer) return;
            int from = dragFrom; dragFrom = -1;
            if (dragGhost != null) Destroy(dragGhost.gameObject);
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(e, hits);
            var target = hits.Select(x => x.gameObject.GetComponentInParent<BoardCellView>()).FirstOrDefault(x => x != null);
            if (target != null) MoveSelected(from, target.index, dragInstance, dragRevision);
            else Tell("已放回原位。");
            selected = -1; Refresh();
        }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelDrag(); }
        private void OnApplicationPause(bool paused) { if (paused) CancelDrag(); }
        private void CancelDrag() { dragFrom = -1; if (dragGhost != null) Destroy(dragGhost.gameObject); }

        private IEnumerator Pulse(RectTransform target)
        {
            float elapsed = 0;
            while (elapsed < .22f && target != null) { elapsed += Time.unscaledDeltaTime; target.localScale = Vector3.one * (1 + .16f * Mathf.Sin(elapsed / .22f * Mathf.PI)); yield return null; }
            if (target != null) target.localScale = Vector3.one;
        }

        private void InventoryAction()
        {
            if (selected < 0) { ShowInventory(); return; }
            var s = Session.Snapshot;
            Run(Session.Store(selected, s.board[selected].instanceId, s.revision), "已存入储物架，交付不会自动取用。");
        }
        private void RecycleSelected()
        {
            if (selected < 0) return;
            var s = Session.Snapshot;
            Run(Session.Recycle(selected, false, s.board[selected].instanceId, s.revision), "已折回基础库存，可撤销；下次生产优先取回。");
        }

        private void ShowInventory(int selectedInventory = -1)
        {
            NewModal("储物架");
            var s = Session.Snapshot;
            Label(modal, "暂存的物品不会被默认交付。", 46, 175, 448, 45, 19);
            for (int i = 0; i < s.inventory.Length; i++) {
                int index = i;
                var button = ActionButton(modal, "storage_" + i, "", 48 + i % 3 * 151, 244 + i / 3 * 133, 142, 121, () => ShowInventory(index));
                button.GetComponent<Image>().color = selectedInventory == i ? new Color(.62f, .76f, .62f) : Pale;
                if (!s.inventory[i].Empty) {
                    ImageAt(button.transform, "item", art.FindItem(s.inventory[i].itemId), 27, 5, 88, 86, Color.white, true);
                    Label(button.transform, Session.Item(s.inventory[i].itemId).displayNameZh, 4, 93, 134, 23, 15, Ink, TextAnchor.MiddleCenter);
                }
            }
            bool valid = selectedInventory >= 0 && !s.inventory[selectedInventory].Empty;
            ActionButton(modal, "retrieve", "取回茶案", 48, 535, 213, 50, () => {
                var result = Session.Retrieve(selectedInventory, s.inventory[selectedInventory].instanceId, s.revision);
                Run(result, "已取回茶案。"); if (result.Success) ShowInventory();
            }).interactable = valid;
            ActionButton(modal, "recycle_storage", "放回工具台", 279, 535, 213, 50, () => {
                var result = Session.Recycle(selectedInventory, true, s.inventory[selectedInventory].instanceId, s.revision);
                Run(result, "已回收，可以在茶案撤销。"); if (result.Success) ShowInventory();
            }).interactable = valid;
            Label(modal, "基础库存：山茶 " + s.teaReserve + " · 青瓷 " + s.ceramicReserve + "\n生产时优先取回，不产生额外灵石。", 48, 625, 444, 75, 18);
        }

        private void ShowDelivery() => RenderDelivery(null);

        private void RenderDelivery(string error)
        {
            var order = Session.Order(orderSlot); if (order == null) return;
            NewModal("为来客备好茶席");
            var variant = order.variants[variantIndex];
            Label(modal, variantIndex == 0 ? "静 · 先泡一壶茶，不急着说。" : "行 · 一起把桌面空出来，再慢慢坐。", 44, 175, 452, 62, 22);
            for (int i = 0; i < variant.requirements.Length; i++) {
                var requirement = variant.requirements[i];
                ImageAt(modal, "Required item", art.FindItem(requirement.itemId), 54, 270 + i * 114, 92, 92, Color.white, true);
                Label(modal, Session.Item(requirement.itemId).displayNameZh + " × " + requirement.quantity, 162, 286 + i * 114, 326, 58, 23);
            }
            ActionButton(modal, "include_storage", "储物架供货：" + (includeInventory ? "已开启" : "关闭"), 48, 530, 444, 46, () => { includeInventory = !includeInventory; ShowDelivery(); });
            long[] ids = Session.SelectMaterials(variant, includeInventory);
            var s = Session.Snapshot;
            Label(modal, ids == null ? "材料未齐；高阶物品不能代替低阶。" : "本次消耗 " + ids.Length + " 件物品，获得 " + order.rewardStones + " 灵石。", 48, 600, 444, 55, 19);
            var confirm = ActionButton(modal, "confirm_delivery", "确认交付", 48, 678, 444, 54, () => {
                string response = variantIndex == 0 ? "谢谢。原来坐下来，不需要先有一个结论。" : "只整理这一小块，好像就够了。";
                bool hadCard = Session.Today?.lines.Count == 3;
                var result = Session.Deliver(orderSlot, order.id, variant.id, ids, includeInventory, Guid.NewGuid().ToString("N"), s.revision);
                if (result.Success) { CloseModal(); selected = -1; Refresh(); ShowResponse(response, !hadCard && Session.Today?.lines.Count == 3, !hadCard); }
                else { Tell(result.Message); RenderDelivery(result.Message); }
            }); confirm.interactable = ids != null;
            if (orderSlot > 0) ActionButton(modal, "replace_request", "免费换一份请求", 48, 756, 444, 42, () => {
                var result = Session.ReplaceRegular(orderSlot, order.id, s.revision);
                if (result.Success) CloseModal(); Run(result, "已换一份请求，没有扣除物品。");
            });
            if (error != null) Label(modal, error, 48, 820, 444, 76, 18, new Color(.57f, .23f, .16f));
        }

        private void ShowResponse(string response, bool newCard, bool recordedLine)
        {
            NewModal("茶席已备好");
            ImageAt(modal, "Elena", art.elenaPortrait, 160, 200, 220, 220, Color.white, true);
            Label(modal, response, 56, 442, 428, 108, 24, Ink, TextAnchor.MiddleCenter);
            Label(modal, recordedLine ? "灵石与今日一爻已保存。" : "灵石已保存，今日卦卡保持不变。", 56, 571, 428, 40, 19, Jade, TextAnchor.MiddleCenter);
            ActionButton(modal, "response_continue", newCard ? "翻开今日卦卡" : "回到茶案", 56, 645, 428, 54, () => { CloseModal(); if (newCard) ShowJournal(); });
        }

        private void ShowSanctuary()
        {
            NewModal("山房 · 门边灯");
            ImageAt(modal, "Room view", art.sanctuaryBase, 45, 175, 450, 455, Color.white, true);
            var s = Session.Snapshot;
            // The v1 room painting is flattened. This overlay is explicitly a prototype light response.
            Panel(modal, "Warm light response", 45, 175, 450, 455, s.lampRepaired ? new Color(1f, .73f, .30f, .15f) : new Color(.08f, .13f, .16f, .28f));
            Label(modal, s.lampRepaired ? "门边的光暖了一点。\n这里始终可以留一个座位。" : "完成第一份请求后，用 40 灵石点亮门边灯。", 48, 648, 444, 74, 21);
            ActionButton(modal, "repair_lamp", s.lampRepaired ? "门灯已点亮" : "点亮门灯 · 40 灵石", 48, 749, 444, 52, () => {
                var result = Session.RepairLamp(Guid.NewGuid().ToString("N"), s.revision);
                Run(result, "门边灯已点亮。"); if (result.Success) ShowSanctuary();
            }).interactable = !s.lampRepaired && s.completedStory >= 1 && s.stones >= 40;
        }

        private void ShowJournal(int historyOffset = 0)
        {
            NewModal("每日手账");
            var records = Session.Snapshot.days.OrderByDescending(x => x.date).ToList();
            var today = Session.Today;
            if (today == null) records.Insert(0, new DailyRecord { date = DateTimeOffset.Now.ToString("yyyy-MM-dd"), upperBits = config.rules.prototypeUpperBits });
            historyOffset = Mathf.Clamp(historyOffset, 0, records.Count - 1);
            var record = records[historyOffset]; var card = Session.Card(record);
            ImageAt(modal, "Oracle paper", art.oracleBackgrounds[historyOffset % art.oracleBackgrounds.Length], 28, 147, 484, 680, Color.white);
            Label(modal, record.date + " · 坤上", 62, 172, 416, 35, 18, Jade, TextAnchor.MiddleCenter);
            string bits = string.Concat(record.lines).PadRight(3, '?') + record.upperBits;
            for (int row = 0; row < 6; row++) {
                char bit = bits[5 - row]; float top = 229 + row * 20;
                Color color = bit == '?' ? new Color(.55f, .58f, .53f, .35f) : Ink;
                if (bit == '1') Panel(modal, "Yang", 214, top, 112, 8, color);
                else { Panel(modal, "Yin left", 214, top, 48, 8, color); Panel(modal, "Yin right", 278, top, 48, 8, color); }
            }
            Label(modal, card == null ? "未完小记 · " + record.lines.Count + " / 3" : "第 " + card.kingWenId + " 卦 · " + card.nameZh, 62, 365, 416, 48, 29, Ink, TextAnchor.MiddleCenter);
            Label(modal, card == null ? "每份茶席，留下一次回应。" : card.titleZh, 62, 421, 416, 45, 24, Ink, TextAnchor.MiddleCenter);
            Label(modal, card == null ? "每天前三次交付从下往上留爻。静为阴，行为阳。\n\n跨日后未完成的小记会保留。没有连续签到，也不需要补齐错过的日子。" : card.bodyZh, 69, 493, 402, 196, 20);
            Label(modal, card == null ? "继续接待来客，或今天先到这里。" : card.promptZh, 69, 699, 402, 66, 20, Jade, TextAnchor.MiddleCenter);
            Label(modal, "原创现代反思 · 供自我觉察", 62, 780, 416, 28, 14, Jade, TextAnchor.MiddleCenter);
            int offset = historyOffset;
            ActionButton(modal, "previous_day", "较早小记", 48, 836, 212, 42, () => ShowJournal(offset + 1)).interactable = offset + 1 < records.Count;
            ActionButton(modal, "next_day", "较新小记", 280, 836, 212, 42, () => ShowJournal(offset - 1)).interactable = offset > 0;
        }

        private void Run(ActionResult result, string success)
        {
            Refresh(); Tell(result.Success ? success : result.Message);
            if (!result.Success && modal != null) {
                var notice = modal.Find("Operation notice"); if (notice != null) Destroy(notice.gameObject);
                var label = Label(modal, result.Message, 38, 884, 464, 50, 16, new Color(.57f, .23f, .16f), TextAnchor.MiddleCenter);
                label.name = "Operation notice";
            }
        }
        private void Tell(string message) { if (status != null) status.text = message; }
        private void NewModal(string title)
        {
            CloseModal(); CancelDrag();
            modal = Panel(root, "Modal", 0, 0, 540, 960, Paper);
            Label(modal, title, 32, 67, 366, 50, 30);
            ActionButton(modal, "close_modal", "返回", 416, 68, 88, 43, CloseModal);
        }
        private void CloseModal() { if (modal != null) { modal.gameObject.SetActive(false); Destroy(modal.gameObject); modal = null; } }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
        private static RectTransform Panel(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var rect = Rect(parent, name, x, y, w, h); rect.gameObject.AddComponent<Image>().color = color; return rect;
        }
        private static Image ImageAt(Transform parent, string name, Sprite sprite, float x, float y, float w, float h, Color color, bool preserve = false)
        {
            var rect = Rect(parent, name, x, y, w, h); var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.preserveAspect = preserve; image.raycastTarget = false; return image;
        }
        private Text Label(Transform parent, string content, float x, float y, float w, float h, int size, Color? color = null, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var rect = Rect(parent, "Label", x, y, w, h); var text = rect.gameObject.AddComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content; text.fontSize = size; text.color = color ?? Ink; text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false; text.supportRichText = false; return text;
        }
        private Button ActionButton(Transform parent, string name, string caption, float x, float y, float w, float h, UnityEngine.Events.UnityAction action, int size = 18)
        {
            var rect = Panel(parent, name, x, y, w, h, Pale); var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(action);
            Label(rect, caption, 6, 1, w - 12, h - 2, size, Ink, TextAnchor.MiddleCenter); return button;
        }
    }
}
