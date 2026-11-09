using UnityEngine;
using UnityEngine.UI;

// Procedural capsules need no sprite asset and travel with RuntimeMonsterStatus.
public sealed class DuelMonsterHealthBar : MaskableGraphic
{
    [SerializeField] private float fraction = 1f;

    public float Fraction => fraction;

    public void SetHealth(int current, int maximum)
    {
        fraction = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;
        color = fraction <= 0.2f ? new Color32(240, 65, 65, 255)
            : fraction <= 0.4f ? new Color32(255, 145, 40, 255)
            : fraction <= 0.6f ? new Color32(255, 220, 50, 255)
            : fraction <= 0.8f ? new Color32(160, 225, 90, 255)
            : new Color32(35, 230, 105, 255);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect bounds = GetPixelAdjustedRect();
        AddCapsule(mesh, bounds, new Color32(45, 53, 62, 255));
        const float inset = 2f;
        Rect fill = new Rect(bounds.x + inset, bounds.y + inset,
            Mathf.Max(0f, bounds.width - inset * 2f) * fraction,
            Mathf.Max(0f, bounds.height - inset * 2f));
        AddCapsule(mesh, fill, color);
    }

    private static void AddCapsule(VertexHelper mesh, Rect rect, Color32 tint)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;
        const int segments = 12;
        int start = mesh.currentVertCount;
        mesh.AddVert(rect.center, tint, Vector2.zero);
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        for (int side = 0; side < 2; side++)
        {
            Vector2 center = new Vector2(side == 0 ? rect.xMax - radius : rect.xMin + radius,
                rect.center.y);
            for (int i = 0; i <= segments; i++)
            {
                float angle = (-90f + side * 180f + i * 180f / segments) * Mathf.Deg2Rad;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    tint, Vector2.zero);
            }
        }
        int count = 2 * (segments + 1);
        for (int i = 0; i < count; i++)
            mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % count);
    }
}
