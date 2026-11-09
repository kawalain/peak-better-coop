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
/// at the size the view takes on screen, and drawn again when that size or
/// the drawing changes. Views showing the same drawing at the same size
/// share one texture, so a view made anew shows at once. Until the drawing
/// is ready, or if it cannot be made, the view shows its fallback colour.
/// </summary>
[RequireComponent(typeof(RawImage))]
internal sealed class VectorView : MonoBehaviour
{
    private const int Samples = 8;

    private const int MaxSide = 4096;

    /// <summary>Textures by drawing and pixel size, with how many views show each.</summary>
    private static readonly Dictionary<string, Shared> Textures = new();

    private static Material? material;

    private RawImage? image;

    private string? key;

    /// <summary>The textures this view counts itself a user of: its drawing's, and while boiling, its other frames'.</summary>
    private readonly HashSet<string> held = new();

    private int drawnFrame = -1;

    private Vector2 drawnSize;

    private float drawnScale;

    private bool stale;

    private bool failed;

    /// <summary>Makes the SVG for the view's size in UI units; its view box must be that size.</summary>
    public Func<Vector2, string> Drawing { get; set; } = _ => string.Empty;

    /// <summary>What shows until the drawing is ready, or if it cannot be made.</summary>
    public Color Fallback { get; set; } = Color.clear;

    /// <summary>
    /// Whether the drawing boils, drawn again for each <see cref="Boil.Frame"/>
    /// as a hand-drawn line wobbles; the view keeps every frame's texture.
    /// </summary>
    public bool Boiling { get; set; }

    /// <summary>Adds a view filling <paramref name="rect"/> that shows <paramref name="drawing"/>.</summary>
    /// <param name="boil">Whether the drawing boils; see <see cref="Boiling"/>.</param>
    public static VectorView Add(RectTransform rect, Func<Vector2, string> drawing, Color fallback, bool boil = false)
    {
        RawImage raw = rect.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;
        raw.color = fallback;
        VectorView view = rect.gameObject.AddComponent<VectorView>();
        view.Drawing = drawing;
        view.Fallback = fallback;
        view.Boiling = boil;
        return view;
    }

    /// <summary>Draws every view under <paramref name="root"/> now, once its layout is known, so none shows its fallback for a frame.</summary>
    public static void DrawAll(Transform root)
    {
        foreach (VectorView view in root.GetComponentsInChildren<VectorView>())
        {
            view.DrawNow();
        }
    }

    /// <summary>Draws again, as when what the drawing shows has changed.</summary>
    public void Redraw()
    {
        if (Boiling)
        {
            // The frames drawn of what it showed before will not show again.
            ReleaseAll();
        }

        stale = true;
        DrawNow();
    }

    /// <summary>Draws now if the view's size, scale, or drawing has changed or its texture was lost.</summary>
    public void DrawNow()
    {
        try
        {
            image ??= GetComponent<RawImage>();
            Canvas? canvas = image.canvas;
            if (failed || canvas == null || !isActiveAndEnabled)
            {
                return;
            }

            Vector2 size = ((RectTransform)transform).rect.size;
            float scale = canvas.rootCanvas.scaleFactor;
            bool lost = key != null && !(Textures.TryGetValue(key, out Shared? shared) && shared.Texture.IsCreated());
            if (size.x < 1f || size.y < 1f
                || (!stale && !lost && key != null && size == drawnSize && Mathf.Approximately(scale, drawnScale)))
            {
                return;
            }

            if (size != drawnSize || !Mathf.Approximately(scale, drawnScale))
            {
                // Frames held for the old size will not show again.
                ReleaseAll();
            }

            Show(size, scale);
            drawnSize = size;
            drawnScale = scale;
            stale = false;
        }
        catch (Exception exception)
        {
            failed = true;
            ReleaseAll();
            if (image != null)
            {
                image.texture = null;
                image.color = Fallback;
            }

            Guard.Report("VectorView.Draw", exception);
        }
    }

    private void LateUpdate()
    {
        if (Boiling && Boil.Frame != drawnFrame)
        {
            stale = true;
        }

        DrawNow();
    }

    private void OnDestroy() => ReleaseAll();

    /// <summary>Shows the drawing for this size, from the shared textures or drawn anew.</summary>
    private void Show(Vector2 size, float scale)
    {
        drawnFrame = Boil.Frame;
        string svg = Drawing(size);
        int pixelWidth = Mathf.Clamp(Mathf.CeilToInt(size.x * scale), 1, MaxSide);
        int pixelHeight = Mathf.Clamp(Mathf.CeilToInt(size.y * scale), 1, MaxSide);
        string wanted = $"{pixelWidth}x{pixelHeight}:{svg}";
        if (wanted != key)
        {
            if (!Boiling)
            {
                ReleaseAll();
            }

            if (!Textures.TryGetValue(wanted, out Shared? shared))
            {
                shared = new Shared(NewTexture(pixelWidth, pixelHeight));
                Textures[wanted] = shared;
                Paint(shared.Texture, svg, size);
            }

            if (held.Add(wanted))
            {
                shared.Users++;
            }

            key = wanted;
        }

        Shared current = Textures[key];
        if (!current.Texture.IsCreated())
        {
            current.Texture.Create();
            Paint(current.Texture, svg, size);
        }

        image!.texture = current.Texture;
        image.color = Color.white;
    }

    /// <summary>Tessellates the SVG and draws its triangles into the texture.</summary>
    private void Paint(RenderTexture target, string svg, Vector2 size)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        SVGParser.SceneInfo scene = SVGParser.ImportSVG(new StringReader(svg));
        // Fine enough that curves stay round at any size the view is shown.
        var options = new VectorUtils.TessellationOptions
        {
            StepDistance = float.MaxValue,
            MaxCordDeviation = 0.05f,
            MaxTanAngleDeviation = 0.05f,
            SamplingStepSize = 0.05f,
        };
        List<VectorUtils.Geometry> shapes = VectorUtils.TessellateScene(scene.Scene, options);

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

        Plugin.Verbose($"Drew {name} at {target.width}x{target.height} from {shapes.Count} shapes in {clock.Elapsed.TotalMilliseconds:F1} ms.");
    }

    private static RenderTexture NewTexture(int width, int height)
    {
        var description = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 0)
        {
            msaaSamples = Samples,
            sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
            useMipMap = false,
            autoGenerateMips = false,
        };
        description.msaaSamples = Mathf.Clamp(SystemInfo.GetRenderTextureSupportedMSAASampleCount(description), 1, Samples);
        var texture = new RenderTexture(description)
        {
            name = "BetterCoop.Vector",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        texture.Create();
        return texture;
    }

    /// <summary>Lets go of every texture this view holds, freeing each once no view shows it.</summary>
    private void ReleaseAll()
    {
        foreach (string heldKey in held)
        {
            if (Textures.TryGetValue(heldKey, out Shared? shared) && --shared.Users <= 0)
            {
                Textures.Remove(heldKey);
                if (image != null && image.texture == shared.Texture)
                {
                    image.texture = null;
                }

                shared.Texture.Release();
                Destroy(shared.Texture);
            }
        }

        held.Clear();
        key = null;
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

    private sealed class Shared
    {
        public Shared(RenderTexture texture) => Texture = texture;

        public RenderTexture Texture { get; }

        public int Users { get; set; }
    }
}
