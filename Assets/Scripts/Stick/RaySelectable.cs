using System.Collections.Generic;
using UnityEngine;

public class RaySelectable : MonoBehaviour
{
    [SerializeField] private bool enforceOpaqueOnSelect = true;

    private struct RendererInfo
    {
        public Renderer Renderer;
        public int BaseColorId;
        public int ColorId;
        public Color BaseColor;
        public Color Color;
    }

    private readonly List<RendererInfo> rendererInfos = new List<RendererInfo>();
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            RendererInfo info = new RendererInfo
            {
                Renderer = renderer,
                BaseColorId = BaseColorId,
                ColorId = ColorId,
                BaseColor = GetColor(renderer, BaseColorId),
                Color = GetColor(renderer, ColorId)
            };

            rendererInfos.Add(info);
        }
    }

    public void SetSelected(bool selected)
    {
        if (!enforceOpaqueOnSelect)
        {
            return;
        }

        foreach (RendererInfo info in rendererInfos)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            info.Renderer.GetPropertyBlock(block);

            if (selected)
            {
                Color baseColor = info.BaseColor;
                baseColor.a = 1f;
                Color color = info.Color;
                color.a = 1f;

                if (info.Renderer.sharedMaterial != null)
                {
                    if (info.Renderer.sharedMaterial.HasProperty(info.BaseColorId))
                    {
                        block.SetColor(info.BaseColorId, baseColor);
                    }
                    if (info.Renderer.sharedMaterial.HasProperty(info.ColorId))
                    {
                        block.SetColor(info.ColorId, color);
                    }
                }
            }
            else
            {
                if (info.Renderer.sharedMaterial != null)
                {
                    if (info.Renderer.sharedMaterial.HasProperty(info.BaseColorId))
                    {
                        block.SetColor(info.BaseColorId, info.BaseColor);
                    }
                    if (info.Renderer.sharedMaterial.HasProperty(info.ColorId))
                    {
                        block.SetColor(info.ColorId, info.Color);
                    }
                }
            }

            info.Renderer.SetPropertyBlock(block);
        }
    }

    private static Color GetColor(Renderer renderer, int propertyId)
    {
        if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(propertyId))
        {
            return renderer.sharedMaterial.GetColor(propertyId);
        }

        return Color.white;
    }
}
