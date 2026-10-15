using UnityEngine;
using UnityEngine.EventSystems;

public class UIDragWindow : MonoBehaviour, IDragHandler, IPointerDownHandler
{
    private RectTransform rectTransform;
    private Canvas canvas;

    void Awake()
    {
        rectTransform = transform as RectTransform;
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnPointerDown(PointerEventData eventData) =>
        rectTransform.SetAsLastSibling();

    public void OnDrag(PointerEventData eventData) =>
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
}
