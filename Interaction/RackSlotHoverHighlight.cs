using UnityEngine;

public class RackSlotHoverHighlight : MonoBehaviour
{
    public Renderer targetRenderer;
    public Color hoverColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    private Color _originalColor;
    private bool _hovered;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (targetRenderer != null && targetRenderer.material != null)
            _originalColor = targetRenderer.material.color;
    }

    public void SetHovered(bool on)
    {
        if (_hovered == on) return;
        _hovered = on;

        if (targetRenderer == null || targetRenderer.material == null)
            return;

        targetRenderer.material.color = on ? hoverColor : _originalColor;
    }
}
