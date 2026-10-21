using UnityEngine;

// Layout-only Edit Mode preview; never loads profiles or runs app navigation.
[ExecuteAlways]
public sealed class AppPortraitLayout : MonoBehaviour
{
    [SerializeField] private RectTransform frame;
    public void Configure(RectTransform target) => frame = target;

    private void LateUpdate()
    {
        if (frame == null || Screen.width <= 0 || Screen.height <= 0) return;
        RectTransform safe = (RectTransform)transform;
        Rect area = Application.isPlaying ? Screen.safeArea : new Rect(0, 0, Screen.width, Screen.height);
        safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        safe.offsetMin = safe.offsetMax = Vector2.zero;
        float scale = Mathf.Min(safe.rect.width / 1080f, safe.rect.height / 1920f);
        frame.localScale = Vector3.one * Mathf.Max(.01f, scale);
    }
}
