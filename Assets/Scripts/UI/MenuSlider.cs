using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MenuSlider : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private RectTransform trackRect;
    [SerializeField] private Image trackImage;
    [SerializeField] private Text label;
    [SerializeField] private Color trackColor = new Color(0.2f, 0.2f, 0.22f, 0.9f);
    [SerializeField] private Color hoverTrackColor = new Color(0.32f, 0.32f, 0.35f, 0.95f);

    public void Initialize(Slider sliderComponent, RectTransform track, Image trackBg, Text labelText)
    {
        slider = sliderComponent;
        trackRect = track;
        trackImage = trackBg;
        label = labelText;
        SetHover(false);
    }

    public void SetHover(bool hovered)
    {
        if (trackImage != null)
        {
            trackImage.color = hovered ? hoverTrackColor : trackColor;
        }
    }

    public void SetLabel(string text)
    {
        if (label != null)
        {
            label.text = text;
        }
    }

    public void UpdateWithPoke(Vector3 worldPosition)
    {
        if (slider == null || trackRect == null)
        {
            return;
        }

        Vector3 local = trackRect.InverseTransformPoint(worldPosition);
        float width = trackRect.rect.width;
        if (width <= 0.0001f)
        {
            return;
        }

        float normalized = Mathf.Clamp01((local.x + width * trackRect.pivot.x) / width);
        float value = Mathf.Lerp(slider.minValue, slider.maxValue, normalized);
        if (slider.wholeNumbers)
        {
            value = Mathf.Round(value);
        }

        slider.value = value;
    }
}
