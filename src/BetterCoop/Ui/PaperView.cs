using System;
using System.Collections.Generic;
using System.IO;
using BetterCoop.Core;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.UI;

namespace BetterCoop.Ui;

/// <summary>
/// Shows the Coop Menu's paper: draws <see cref="PaperDrawing"/>'s SVG with
/// the game's own vector graphics (parsed and tessellated into triangles)
/// into a multisampled texture at the panel's size on screen, and draws it
/// again when that size changes. Until the paper is drawn, or if it cannot
/// be, the panel shows plain paper colour.
/// </summary>
[RequireComponent(typeof(RawImage))]
internal sealed class PaperView : MonoBehaviour
{
    private const int Samples = 8;

    private const int MaxSide = 4096;

    private static Material? material;

    private RawImage image = null!;

    private RenderTexture? texture;

    private Vector2 drawnSize;

    private int drawnPixels;

    private bool failed;

    public static Color Paper => ToColor(PaperDrawing.Paper);

    public static Color Ink => ToColor(PaperDrawing.InkBlack);

    public static Color Blue => ToColor(PaperDrawing.PassportBlue);

    /// <summary>The paper's top and bottom perforations, from its top and bottom edges.</summary>
    public float TopTear { get; set; }

    public float BottomTear { get; set; }

    /// <summary>The panel the paper is cut to; the paper reaches past it by its margin for the shadow.</summary>
    public RectTransform Panel { get; set; } = null!;

    private void Awake()
    {
        image = GetComponent<RawImage>();
        image.texture = null;
        image.color = Paper;
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

            Vector2 size = Panel.rect.size;
            int pixels = Mathf.RoundToInt(canvas.rootCanvas.scaleFactor * 1000f);
            bool lost = texture != null && !texture.IsCreated();
            if (size.x < 1f || size.y < 1f || (!lost && texture != null && size == drawnSize && pixels == drawnPixels))
            {
                return;
            }

            Draw(size, canvas.rootCanvas.scaleFactor);
            drawnSize = size;
            drawnPixels = pixels;
        }
        catch (Exception exception)
        {
            failed = true;
            Release();
            image.texture = null;
            image.color = Paper;
            Guard.Report("PaperView.Draw", exception);
        }
    }

    private void OnDestroy() => Release();

    private void Draw(Vector2 size, float scale)
    {
        string svg = PaperDrawing.Svg(size.x, size.y, TopTear, BottomTear);
        SVGParser.SceneInfo scene = SVGParser.ImportSVG(new StringReader(svg));
        // Fine enough that curves stay round at any size the panel is shown.
        var options = new VectorUtils.TessellationOptions
        {
            StepDistance = float.MaxValue,
            MaxCordDeviation = 0.05f,
            MaxTanAngleDeviation = 0.05f,
            SamplingStepSize = 0.01f,
        };
        List<VectorUtils.Geometry> shapes = VectorUtils.TessellateScene(scene.Scene, options);

        (float width, float height) = PaperDrawing.Canvas(size.x, size.y);
        int pixelWidth = Mathf.Clamp(Mathf.CeilToInt(width * scale), 1, MaxSide);
        int pixelHeight = Mathf.Clamp(Mathf.CeilToInt(height * scale), 1, MaxSide);
        RenderTexture target = Target(pixelWidth, pixelHeight);

        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        try
        {
            GL.Clear(true, true, Color.clear);
            Material paint = Material();
            paint.SetPass(0);
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
                    GL.Vertex3(point.x / width, 1f - point.y / height, 0f);
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
        Plugin.Verbose($"Drew the Coop Menu's paper at {pixelWidth}x{pixelHeight} from {shapes.Count} shapes.");
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
            name = "BetterCoop.Paper",
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
            name = "BetterCoop.PaperInk",
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

    private static Color ToColor(Ink ink) => new(ink.R, ink.G, ink.B, ink.A);
}
