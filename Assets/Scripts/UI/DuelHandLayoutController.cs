using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Keeps hand cards inside the existing HandArea without resizing it.
// A focused card receives extra space while the other gaps compress evenly.
public sealed class DuelHandLayoutController : MonoBehaviour
{
    private const float CardWidth = 175f;
    private const float NormalStep = 125f;
    private const float FocusGapExtra = 42f;
    private const float MinimumStep = 24f;

    private readonly List<RectTransform> slots = new List<RectTransform>();
    private RectTransform contentRect;
    private bool allowFocus;
    private bool reverseOrder;
    private int focusedIndex = -1;

    public void BeginLayout(bool enableFocus, bool reverseVisualOrder = false)
    {
        allowFocus = enableFocus;
        reverseOrder = reverseVisualOrder;
        focusedIndex = -1;
        slots.Clear();

        contentRect = GetComponent<RectTransform>();
        HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
            layout.enabled = false;

        ContentSizeFitter fitter = GetComponent<ContentSizeFitter>();
        if (fitter != null)
            fitter.enabled = false;

        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
    }

    public void AddSlot(RectTransform slot)
    {
        if (slot == null)
            return;

        int index = slots.Count;
        slots.Add(slot);
        CardView cardView = slot.GetComponentInChildren<CardView>();
        GameObject focusTarget = cardView != null
            ? cardView.gameObject
            : slot.gameObject;
        DuelHandCardFocus focus =
            focusTarget.GetComponent<DuelHandCardFocus>();
        if (focus == null)
            focus = focusTarget.AddComponent<DuelHandCardFocus>();
        focus.Configure(this, index, allowFocus);
    }

    public void ApplyLayout()
    {
        if (contentRect == null || slots.Count == 0)
            return;

        Canvas.ForceUpdateCanvases();
        float availableWidth = Mathf.Max(CardWidth, contentRect.rect.width);
        float availableSpan = Mathf.Max(0f, availableWidth - CardWidth);
        int gapCount = slots.Count - 1;
        float[] gaps = new float[Mathf.Max(0, gapCount)];

        if (gapCount > 0)
        {
            float baseStep = Mathf.Min(NormalStep, availableSpan / gapCount);
            for (int index = 0; index < gapCount; index++)
                gaps[index] = baseStep;

            if (allowFocus && focusedIndex >= 0)
                ExpandFocusedGaps(gaps, availableSpan, focusedIndex);
        }

        float occupiedSpan = 0f;
        foreach (float gap in gaps)
            occupiedSpan += gap;

        float[] visualPositions = new float[slots.Count];
        float positionX = -occupiedSpan * 0.5f;
        for (int index = 0; index < slots.Count; index++)
        {
            visualPositions[index] = positionX;
            if (index < gaps.Length)
                positionX += gaps[index];
        }
        for (int index = 0; index < slots.Count; index++)
        {
            RectTransform slot = slots[index];
            slot.anchorMin = slot.anchorMax = slot.pivot =
                new Vector2(0.5f, 0.5f);
            int visualIndex = reverseOrder ? slots.Count - 1 - index : index;
            slot.anchoredPosition = new Vector2(
                visualPositions[visualIndex], 0f);
        }
    }

    public void SetFocusedIndex(int index)
    {
        if (!allowFocus || index < 0 || index >= slots.Count ||
            focusedIndex == index)
            return;

        focusedIndex = index;
        ApplyLayout();
    }

    public void ClearFocusedIndex(int index)
    {
        if (!allowFocus || focusedIndex != index)
            return;

        focusedIndex = -1;
        ApplyLayout();
    }

    private static void ExpandFocusedGaps(
        float[] gaps,
        float availableSpan,
        int focusedIndex)
    {
        if (gaps.Length == 0)
            return;

        List<int> focusGaps = new List<int>(2);
        if (focusedIndex > 0)
            focusGaps.Add(focusedIndex - 1);
        if (focusedIndex < gaps.Length)
            focusGaps.Add(focusedIndex);

        float requestedExtra = FocusGapExtra * focusGaps.Count;
        float currentSpan = 0f;
        foreach (float gap in gaps)
            currentSpan += gap;

        List<int> otherGaps = new List<int>();
        for (int index = 0; index < gaps.Length; index++)
        {
            if (!focusGaps.Contains(index))
                otherGaps.Add(index);
        }

        float freeSpan = Mathf.Max(0f, availableSpan - currentSpan);
        float compressionNeeded = Mathf.Max(0f, requestedExtra - freeSpan);
        float compressionAvailable = 0f;
        foreach (int index in otherGaps)
            compressionAvailable += Mathf.Max(0f, gaps[index] - MinimumStep);

        float compression = Mathf.Min(compressionNeeded, compressionAvailable);
        if (compression > 0f && otherGaps.Count > 0)
        {
            float remaining = compression;
            for (int item = 0; item < otherGaps.Count; item++)
            {
                int index = otherGaps[item];
                int remainingGaps = otherGaps.Count - item;
                float share = remaining / remainingGaps;
                float amount = Mathf.Min(share, gaps[index] - MinimumStep);
                gaps[index] -= amount;
                remaining -= amount;
            }
        }

        float grantedExtra = Mathf.Min(
            requestedExtra,
            freeSpan + compression);
        float extraPerGap = focusGaps.Count > 0
            ? grantedExtra / focusGaps.Count
            : 0f;
        foreach (int index in focusGaps)
            gaps[index] += extraPerGap;
    }
}

public sealed class DuelHandCardFocus : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler
{
    private DuelHandLayoutController owner;
    private int index;
    private bool enabledForInput;

    public void Configure(
        DuelHandLayoutController layoutOwner,
        int slotIndex,
        bool enableInput)
    {
        owner = layoutOwner;
        index = slotIndex;
        enabledForInput = enableInput;
        enabled = enableInput;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (enabledForInput)
            owner.SetFocusedIndex(index);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (enabledForInput && eventData.pointerId < 0)
            owner.ClearFocusedIndex(index);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (enabledForInput)
            owner.SetFocusedIndex(index);
    }
}
