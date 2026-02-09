using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MenuSwatch : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Button button;
    [SerializeField] private Outline selectionOutline;
    [SerializeField] private Color baseColor = Color.white;
    [SerializeField] private Color selectedOutlineColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Vector2 outlineDistance = new Vector2(2f, 2f);
    [SerializeField] private float hoverBrightness = 1.15f;
    [SerializeField] private float selectedBrightness = 1.05f;
    [SerializeField] private float pressedBrightness = 0.85f;
    [SerializeField, Min(0f)] private float pressCooldown = 0.25f;

    private float lastPressedTime = -10f;
    private bool selected;

    public void Initialize(Button btn, Image bg, Color color)
    {
        button = btn;
        background = bg;
        baseColor = color;
        EnsureOutline();
        SetSelected(false);
        SetHover(false);
    }

    public void SetHover(bool hovered)
    {
        if (background == null)
        {
            return;
        }

        float brightness = hovered ? hoverBrightness : 1f;
        if (selected)
        {
            brightness = Mathf.Max(brightness, selectedBrightness);
        }
        background.color = AdjustBrightness(baseColor, brightness);
    }

    public void SetSelected(bool isSelected)
    {
        selected = isSelected;
        if (background != null)
        {
            background.color = AdjustBrightness(baseColor, selected ? selectedBrightness : 1f);
        }

        if (selectionOutline != null)
        {
            selectionOutline.enabled = selected;
        }
    }

    public void Press()
    {
        if (Time.time - lastPressedTime < pressCooldown)
        {
            return;
        }

        lastPressedTime = Time.time;
        if (background != null)
        {
            background.color = AdjustBrightness(baseColor, pressedBrightness);
        }

        if (button != null)
        {
            button.onClick.Invoke();
        }
    }

    private static Color AdjustBrightness(Color color, float factor)
    {
        return new Color(
            Mathf.Clamp01(color.r * factor),
            Mathf.Clamp01(color.g * factor),
            Mathf.Clamp01(color.b * factor),
            color.a
        );
    }

    private void EnsureOutline()
    {
        if (background == null)
        {
            return;
        }

        if (selectionOutline == null)
        {
            selectionOutline = background.gameObject.GetComponent<Outline>();
        }

        if (selectionOutline == null)
        {
            selectionOutline = background.gameObject.AddComponent<Outline>();
        }

        selectionOutline.effectColor = selectedOutlineColor;
        selectionOutline.effectDistance = outlineDistance;
        selectionOutline.enabled = false;
    }
}
