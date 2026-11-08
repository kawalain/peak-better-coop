using System;
using System.Collections.Generic;
using System.IO;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.UI;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// Shows an SVG drawing made for its own size: the game's vector graphics
/// parse and tessellate it into triangles, drawn into a multisampled texture
/// at the size the view takes on screen, and drawn again when that size
/// changes. Until the drawing is ready, or if it cannot be made, the view
/// shows its fallback colour.
/// </summary>
[RequireComponent(typeof(RawImage))]
internal sealed class VectorView : MonoBehaviour
{
    private const int Samples = 8;

    private const int MaxSide = 4096;

    private static Material? material;

    private RawImage image = null!;

    private RenderTexture? texture;

    private Vector2 drawnSize;

    private float drawnScale;

    private bool failed;

    /// <summary>Makes the SVG for the view's size in UI units; its view box must be that size.</summary>
    public Func<Vector2, string> Drawing { get; set; } = _ => string.Empty;

    /// <summary>What shows until the drawing is ready, or if it cannot be made.</summary>
    public Color Fallback { get; set; } = Color.clear;

    /// <summary>Adds a view filling <paramref name="rect"/> that shows <paramref name="drawing"/>.</summary>
    public static VectorView Add(RectTransform rect, Func<Vector2, string> drawing, Color fallback)
    {
        RawImage raw = rect.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;
        raw.color = fallback;
        VectorView view = rect.gameObject.AddComponent<VectorView>();
        view.Drawing = drawing;
        view.Fallback = fallback;
        return view;
    }

    /// <summary>Draws again on the next frame, as when what the drawing shows has changed.</summary>
    public void Redraw() => drawnSize = Vector2.zero;

    private void Awake()
    {
        image = GetComponent<RawImage>();
    }

    private void LateUpdate()
    {
        try
        {
            Canvas? canvas = image.canvas;
            if (failed || canvas == null)
            {
                return;
            }

            Vector2 size = ((RectTransform)transform).rect.size;
            float scale = canvas.rootCanvas.scaleFactor;
            bool lost = texture != null && !texture.IsCreated();
            if (size.x < 1f || size.y < 1f || (!lost && texture != null && size == drawnSize && Mathf.Approximately(scale, drawnScale)))
            {
                return;
            }

            Draw(size, scale);
            drawnSize = size;
            drawnScale = scale;
        }
        catch (Exception exception)
        {
            failed = true;
            Release();
            image.texture = null;
            image.color = Fallback;
            Guard.Report("VectorView.Draw", exception);
        }
    }

    private void OnDestroy() => Release();

    private void Draw(Vector2 size, float scale)
    {
        SVGParser.SceneInfo scene = SVGParser.ImportSVG(new StringReader(Drawing(size)));
        // Fine enough that curves stay round at any size the view is shown.
        var options = new VectorUtils.TessellationOptions
        {
            StepDistance = float.MaxValue,
            MaxCordDeviation = 0.05f,
            MaxTanAngleDeviation = 0.05f,
            SamplingStepSize = 0.01f,
        };
        List<VectorUtils.Geometry> shapes = VectorUtils.TessellateScene(scene.Scene, options);

        int pixelWidth = Mathf.Clamp(Mathf.CeilToInt(size.x * scale), 1, MaxSide);
        int pixelHeight = Mathf.Clamp(Mathf.CeilToInt(size.y * scale), 1, MaxSide);
        RenderTexture target = Target(pixelWidth, pixelHeight);

        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        try
        {
            GL.Clear(true, true, Color.clear);
            Material().SetPass(0);
            GL.PushMatrix();
            GL.LoadOrtho();
            GL.Begin(GL.TRIANGLES);
            foreach (VectorUtils.Geometry shape in shapes)
            {
                // The texture is sRGB in a linear project, so colours go in linear.
                GL.Color(linear ? shape.Color.linear : shape.Color);
                foreach (ushort index in shape.Indices)
                {
                    Vector2 point = shape.WorldTransform * shape.Vertices[index];
                    // SVG's y runs down; the texture's runs up.
                    GL.Vertex3(point.x / size.x, 1f - point.y / size.y, 0f);
                }
            }

            GL.End();
            GL.PopMatrix();
        }
        finally
        {
            RenderTexture.active = previous;
        }

        image.texture = target;
        image.color = Color.white;
        Plugin.Verbose($"Drew {name} at {pixelWidth}x{pixelHeight} from {shapes.Count} shapes.");
    }

    private RenderTexture Target(int width, int height)
    {
        if (texture != null && texture.width == width && texture.height == height && texture.IsCreated())
        {
            return texture;
        }

        Release();
        var description = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 0)
        {
            msaaSamples = Samples,
            sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
            useMipMap = false,
            autoGenerateMips = false,
        };
        description.msaaSamples = Mathf.Clamp(SystemInfo.GetRenderTextureSupportedMSAASampleCount(description), 1, Samples);
        texture = new RenderTexture(description)
        {
            name = "BetterCoop.Vector",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        texture.Create();
        return texture;
    }

    private void Release()
    {
        if (texture == null)
        {
            return;
        }

        if (image != null && image.texture == texture)
        {
            image.texture = null;
        }

        texture.Release();
        Destroy(texture);
        texture = null;
    }

    /// <summary>The UI's own material, sampling nothing but white and clipping nothing.</summary>
    private static Material Material()
    {
        if (material != null)
        {
            return material;
        }

        material = new Material(Canvas.GetDefaultCanvasMaterial())
        {
            name = "BetterCoop.VectorInk",
            hideFlags = HideFlags.HideAndDontSave,
        };
        material.mainTexture = Texture2D.whiteTexture;
        material.SetColor("_Color", Color.white);
        material.SetVector("_TextureSampleAdd", Vector4.zero);
        material.SetVector("_ClipRect", new Vector4(-1e6f, -1e6f, 1e6f, 1e6f));
        material.DisableKeyword("UNITY_UI_CLIP_RECT");
        material.DisableKeyword("UNITY_UI_ALPHACLIP");
        return material;
    }
}
