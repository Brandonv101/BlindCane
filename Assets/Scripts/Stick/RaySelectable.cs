using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class RaySelectable : MonoBehaviour
{
    [SerializeField] private bool enforceOpaqueOnSelect = true;

    private sealed class MaterialSnapshot
    {
        public Material Material;
        public bool HasBaseColor;
        public bool HasColor;
        public Color BaseColor;
        public Color Color;
        public bool HasSurface;
        public float Surface;
        public bool HasBlend;
        public float Blend;
        public bool HasSrcBlend;
        public float SrcBlend;
        public bool HasDstBlend;
        public float DstBlend;
        public bool HasZWrite;
        public float ZWrite;
        public int RenderQueue;
        public string RenderTypeTag;
        public bool AlphaTestKeyword;
        public bool AlphaBlendKeyword;
        public bool AlphaPremultiplyKeyword;
    }

    private readonly List<MaterialSnapshot> materialSnapshots = new List<MaterialSnapshot>();
    private bool isSelected;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
    private static readonly int BlendId = Shader.PropertyToID("_Blend");
    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

    private void Awake()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.materials;
            foreach (Material material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                materialSnapshots.Add(CaptureSnapshot(material));
            }
        }
    }

    public void SetSelected(bool selected)
    {
        if (!enforceOpaqueOnSelect)
        {
            return;
        }

        if (isSelected == selected)
        {
            return;
        }

        isSelected = selected;

        foreach (MaterialSnapshot snapshot in materialSnapshots)
        {
            if (snapshot.Material == null)
            {
                continue;
            }

            if (selected)
            {
                ApplyOpaqueState(snapshot);
            }
            else
            {
                RestoreMaterialState(snapshot);
            }
        }
    }

    private static MaterialSnapshot CaptureSnapshot(Material material)
    {
        MaterialSnapshot snapshot = new MaterialSnapshot
        {
            Material = material,
            HasBaseColor = material.HasProperty(BaseColorId),
            HasColor = material.HasProperty(ColorId),
            HasSurface = material.HasProperty(SurfaceId),
            HasBlend = material.HasProperty(BlendId),
            HasSrcBlend = material.HasProperty(SrcBlendId),
            HasDstBlend = material.HasProperty(DstBlendId),
            HasZWrite = material.HasProperty(ZWriteId),
            RenderQueue = material.renderQueue,
            RenderTypeTag = material.GetTag("RenderType", false, string.Empty),
            AlphaTestKeyword = material.IsKeywordEnabled("_ALPHATEST_ON"),
            AlphaBlendKeyword = material.IsKeywordEnabled("_ALPHABLEND_ON"),
            AlphaPremultiplyKeyword = material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON")
        };

        if (snapshot.HasBaseColor)
        {
            snapshot.BaseColor = material.GetColor(BaseColorId);
        }

        if (snapshot.HasColor)
        {
            snapshot.Color = material.GetColor(ColorId);
        }

        if (snapshot.HasSurface)
        {
            snapshot.Surface = material.GetFloat(SurfaceId);
        }

        if (snapshot.HasBlend)
        {
            snapshot.Blend = material.GetFloat(BlendId);
        }

        if (snapshot.HasSrcBlend)
        {
            snapshot.SrcBlend = material.GetFloat(SrcBlendId);
        }

        if (snapshot.HasDstBlend)
        {
            snapshot.DstBlend = material.GetFloat(DstBlendId);
        }

        if (snapshot.HasZWrite)
        {
            snapshot.ZWrite = material.GetFloat(ZWriteId);
        }

        return snapshot;
    }

    private static void ApplyOpaqueState(MaterialSnapshot snapshot)
    {
        Material material = snapshot.Material;

        if (snapshot.HasBaseColor)
        {
            Color color = snapshot.BaseColor;
            color.a = 1f;
            material.SetColor(BaseColorId, color);
        }

        if (snapshot.HasColor)
        {
            Color color = snapshot.Color;
            color.a = 1f;
            material.SetColor(ColorId, color);
        }

        if (snapshot.HasSurface)
        {
            material.SetFloat(SurfaceId, 0f);
        }

        if (snapshot.HasBlend)
        {
            material.SetFloat(BlendId, 0f);
        }

        if (snapshot.HasSrcBlend)
        {
            material.SetFloat(SrcBlendId, (float)BlendMode.One);
        }

        if (snapshot.HasDstBlend)
        {
            material.SetFloat(DstBlendId, (float)BlendMode.Zero);
        }

        if (snapshot.HasZWrite)
        {
            material.SetFloat(ZWriteId, 1f);
        }

        material.SetOverrideTag("RenderType", "Opaque");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Geometry;
    }

    private static void RestoreMaterialState(MaterialSnapshot snapshot)
    {
        Material material = snapshot.Material;

        if (snapshot.HasBaseColor)
        {
            material.SetColor(BaseColorId, snapshot.BaseColor);
        }

        if (snapshot.HasColor)
        {
            material.SetColor(ColorId, snapshot.Color);
        }

        if (snapshot.HasSurface)
        {
            material.SetFloat(SurfaceId, snapshot.Surface);
        }

        if (snapshot.HasBlend)
        {
            material.SetFloat(BlendId, snapshot.Blend);
        }

        if (snapshot.HasSrcBlend)
        {
            material.SetFloat(SrcBlendId, snapshot.SrcBlend);
        }

        if (snapshot.HasDstBlend)
        {
            material.SetFloat(DstBlendId, snapshot.DstBlend);
        }

        if (snapshot.HasZWrite)
        {
            material.SetFloat(ZWriteId, snapshot.ZWrite);
        }

        if (snapshot.AlphaTestKeyword)
        {
            material.EnableKeyword("_ALPHATEST_ON");
        }
        else
        {
            material.DisableKeyword("_ALPHATEST_ON");
        }

        if (snapshot.AlphaBlendKeyword)
        {
            material.EnableKeyword("_ALPHABLEND_ON");
        }
        else
        {
            material.DisableKeyword("_ALPHABLEND_ON");
        }

        if (snapshot.AlphaPremultiplyKeyword)
        {
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        else
        {
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        material.SetOverrideTag("RenderType", snapshot.RenderTypeTag);
        material.renderQueue = snapshot.RenderQueue;
    }
}
