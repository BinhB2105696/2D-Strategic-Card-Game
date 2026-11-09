using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Small recycled text pool keeps the complete journal scrollable in long Auto duels.
public sealed class DuelLogView : MonoBehaviour
{
    private DuelLogModel journal;
    private DuelBoardReadOnlyDemo board;
    private ScrollRect scroll;
    private RectTransform viewport, content;
    private TMP_Text measure;
    private readonly List<TMP_Text> rows = new List<TMP_Text>();
    private readonly List<float> offsets = new List<float>();
    private IReadOnlyList<string> messages;
    private int revision = -1, viewer = -1;
    private float width = -1;
    private bool initialized;

    public void Initialize(DuelLogModel model, DuelBoardReadOnlyDemo duelBoard)
    {
        if (initialized) return;
        initialized = true; journal = model; board = duelBoard;
        TMP_Text placeholder = GetComponentInChildren<TMP_Text>(true);
        TMP_FontAsset font = placeholder != null ? placeholder.font : TMP_Settings.defaultFontAsset;
        if (placeholder != null) placeholder.gameObject.SetActive(false);
        RectTransform root = Rect(transform, "RuntimeDuelLog");
        Stretch(root);
        root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.08f);
        scroll = root.gameObject.AddComponent<ScrollRect>();
        viewport = Rect(root, "Viewport"); Stretch(viewport);
        viewport.offsetMax = new Vector2(-20, 0);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        content = Rect(viewport, "Content");
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1); content.sizeDelta = Vector2.zero;
        scroll.viewport = viewport; scroll.content = content;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45;

        RectTransform rail = Rect(root, "Scrollbar");
        rail.anchorMin = new Vector2(1, 0); rail.anchorMax = Vector2.one;
        rail.pivot = new Vector2(1, 0.5f); rail.sizeDelta = new Vector2(12, 0);
        rail.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.08f);
        Scrollbar bar = rail.gameObject.AddComponent<Scrollbar>();
        RectTransform handle = Rect(rail, "Handle"); Stretch(handle);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.5f, 0.7f, 0.85f, 0.8f);
        bar.handleRect = handle; bar.targetGraphic = handleImage;
        bar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = bar;

        measure = CreateText(font);
        measure.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (journal == null || viewport.rect.width <= 0) return;
        int perspective = board.ViewingPlayerIndex;
        bool changed = viewer != perspective || revision != journal.Revision ||
            Mathf.Abs(width - viewport.rect.width) > 0.5f;
        if (changed)
        {
            bool rebuild = viewer != perspective || Mathf.Abs(width - viewport.rect.width) > 0.5f || offsets.Count == 0;
            bool bottom = viewer < 0 || viewer != perspective || content.rect.height <= viewport.rect.height ||
                content.rect.height - viewport.rect.height - content.anchoredPosition.y < 36;
            float oldY = content.anchoredPosition.y;
            viewer = perspective; revision = journal.Revision; width = viewport.rect.width;
            messages = journal.Lines(viewer);
            if (rebuild) { offsets.Clear(); offsets.Add(8); }
            for (int i = offsets.Count - 1; i < messages.Count; i++)
            {
                string message = messages[i];
                float height = measure.GetPreferredValues(message, Mathf.Max(1, width - 16), Mathf.Infinity).y + 8;
                offsets.Add(offsets[offsets.Count - 1] + Mathf.Max(34, height));
            }
            content.sizeDelta = new Vector2(0, offsets[offsets.Count - 1] + 8);
            float maxY = Mathf.Max(0, content.sizeDelta.y - viewport.rect.height);
            content.anchoredPosition = new Vector2(0, bottom ? maxY : Mathf.Clamp(oldY, 0, maxY));
        }
        if (messages == null) return;
        float top = content.anchoredPosition.y, end = top + viewport.rect.height;
        int start = 0;
        // Binary search the first visible row.
        int low = 0, high = messages.Count;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (offsets[mid + 1] < top) low = mid + 1; else high = mid;
        }
        start = low;
        int used = 0;
        for (int i = start; i < messages.Count && offsets[i] <= end; i++)
        {
            if (used == rows.Count) rows.Add(CreateText(measure.font));
            TMP_Text row = rows[used++];
            row.gameObject.SetActive(true); row.text = messages[i];
            row.color = messages[i].StartsWith("#") ? new Color32(255, 215, 110, 255) : Color.white;
            row.rectTransform.anchoredPosition = new Vector2(8, -offsets[i]);
            row.rectTransform.sizeDelta = new Vector2(-16, offsets[i + 1] - offsets[i]);
        }
        for (int i = used; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
    }

    private TMP_Text CreateText(TMP_FontAsset font)
    {
        RectTransform rect = Rect(content, "LogLine");
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0, 1);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = 25; text.richText = false;
        text.enableWordWrapping = true; text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform Rect(Transform parent, string name)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = parent.gameObject.layer;
        RectTransform rect = obj.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        return rect;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
