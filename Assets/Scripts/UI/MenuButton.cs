using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MenuButton : MonoBehaviour
{
    [SerializeField] private Text label;
    [SerializeField] private Image background;
    [SerializeField] private Button button;
    [SerializeField] private Color normalColor = new Color(0.18f, 0.18f, 0.2f, 0.9f);
    [SerializeField] private Color hoverColor = new Color(0.28f, 0.28f, 0.3f, 0.95f);
    [SerializeField] private Color pressedColor = new Color(0.4f, 0.4f, 0.45f, 1f);
    [SerializeField, Min(0f)] private float pressCooldown = 0.35f;

    private float lastPressedTime = -10f;

    public void Initialize(Button btn, Image bg, Text text)
    {
        button = btn;
        background = bg;
        label = text;
        SetHover(false);
    }

    public void SetLabel(string text)
    {
        if (label != null)
        {
            label.text = text;
        }
    }

    public void SetHover(bool hovered)
    {
        if (background == null)
        {
            return;
        }

        background.color = hovered ? hoverColor : normalColor;
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
            background.color = pressedColor;
        }

        if (button != null)
        {
            button.onClick.Invoke();
        }
    }
}
