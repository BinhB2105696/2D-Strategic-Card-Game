using UnityEngine;
using UnityEngine.EventSystems;

public sealed class DeckCatalogDragHandle : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private DeckBuilderAppController owner;
    private float downY;
    private float startingHeight;
    private bool moved;

    public void Initialize(DeckBuilderAppController controller) => owner = controller;

    public void OnPointerDown(PointerEventData eventData)
    {
        downY = eventData.position.y;
        startingHeight = owner.CatalogHeight;
        moved = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Mathf.Abs(eventData.position.y - downY) > 8f)
            moved = true;
        owner.SetCatalogHeight(startingHeight +
            (eventData.position.y - downY) / owner.UiScale);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!moved)
            owner.ToggleCatalog();
        else
            owner.SnapCatalog();
    }
}
