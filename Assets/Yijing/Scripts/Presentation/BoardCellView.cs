using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Yijing.Presentation
{
    public sealed class BoardCellView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public GamePresenter owner;
        public int index;
        public Image background, icon;
        public Text tier;
        private bool dragged;
        public void OnPointerClick(PointerEventData e) { if (!dragged && e.button == PointerEventData.InputButton.Left) owner.SelectCell(index); }
        public void OnBeginDrag(PointerEventData e) { dragged = true; owner.BeginItemDrag(index, e); }
        public void OnDrag(PointerEventData e) => owner.DragItem(e);
        public void OnEndDrag(PointerEventData e) { owner.EndItemDrag(e); dragged = false; }
    }
}
