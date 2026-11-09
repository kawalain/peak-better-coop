using System;
using System.Collections;
using System.Collections.Generic;
using BetterCoop.Core;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Num = System.Numerics;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// What becomes of a page that goes: thrown away, it is photographed as it
/// stands for one frame and that picture crumpled into a ball in 3D and
/// thrown off at random; handed in, the Troop Leader's hand comes down from
/// the top, takes it by its top edge, and lifts it away.
/// </summary>
internal static class PaperToss
{
    /// <summary>
    /// Crumples <paramref name="page"/> and throws it away. <paramref name="taken"/>
    /// runs once its picture is taken, when the page itself should go; it
    /// runs even if no picture could be taken.
    /// </summary>
    public static void Crumple(RectTransform page, Action taken) =>
        Start(page, "BetterCoop.Crumple", taken, (holder, shot) =>
        {
            PaperWad wad = holder.gameObject.AddComponent<PaperWad>();
            wad.Begin(shot);
        });

    /// <summary>
    /// Has <paramref name="hand"/> (a picture of an arm, hand down) take
    /// <paramref name="page"/> away upwards; null for a drawn hand in
    /// <paramref name="skin"/>. <paramref name="taken"/> runs once the page's
    /// picture is taken, and <paramref name="gone"/> once it is out of sight.
    /// </summary>
    public static void HandIn(RectTransform page, Texture? hand, Color skin, Action taken, Action gone) =>
        Start(page, "BetterCoop.HandIn", taken, (holder, shot) =>
        {
            PaperHandIn handIn = holder.gameObject.AddComponent<PaperHandIn>();
            handIn.Begin(shot, hand, skin, gone);
        }, gone);

    private static void Start(RectTransform page, string name, Action taken, Action<RectTransform, Texture2D> play, Action? failed = null)
    {
        Canvas? canvas = page.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            taken();
            failed?.Invoke();
            return;
        }

        // Over everything on the page's canvas, where the page is.
        Transform root = canvas.rootCanvas.transform;
        RectTransform holder = PaperList.NewRect(name, root);
        holder.SetAsLastSibling();
        holder.position = page.position;
        holder.rotation = page.rotation;
        float scale = page.lossyScale.x / Mathf.Max(1e-6f, root.lossyScale.x);
        holder.sizeDelta = page.rect.size * scale;
        PageShooter shooter = holder.gameObject.AddComponent<PageShooter>();
        shooter.Shoot(page, canvas.rootCanvas, shot =>
        {
            taken();
            if (shot == null)
            {
                Object.Destroy(holder.gameObject);
                failed?.Invoke();
                return;
            }

            try
            {
                play(holder, shot);
            }
            catch (Exception exception)
            {
                Guard.Report(name, exception);
                Object.Destroy(shot);
                Object.Destroy(holder.gameObject);
                failed?.Invoke();
            }
        });
    }
}

/// <summary>Photographs a page as it stands on the screen, at the end of the frame, into a picture of its own.</summary>
internal sealed class PageShooter : MonoBehaviour
{
    public void Shoot(RectTransform page, Canvas canvas, Action<Texture2D?> done) => StartCoroutine(Take(page, canvas, done));

    private static IEnumerator Take(RectTransform page, Canvas canvas, Action<Texture2D?> done)
    {
        yield return new WaitForEndOfFrame();
        Texture2D? shot = null;
        try
        {
            Camera? eye = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4];
            page.GetWorldCorners(corners);
            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 seen = RectTransformUtility.WorldToScreenPoint(eye, corner);
                min = Vector2.Min(min, seen);
                max = Vector2.Max(max, seen);
            }

            Texture2D screen = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                int x = Mathf.Clamp(Mathf.RoundToInt(min.x), 0, screen.width - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt(min.y), 0, screen.height - 1);
                int width = Mathf.Clamp(Mathf.RoundToInt(max.x - min.x), 1, screen.width - x);
                int height = Mathf.Clamp(Mathf.RoundToInt(max.y - min.y), 1, screen.height - y);
                Color32[] pixels = screen.GetPixels32();
                var cut = new Color32[width * height];
                for (int row = 0; row < height; row++)
                {
                    int from = ((y + row) * screen.width) + x;
                    for (int column = 0; column < width; column++)
                    {
                        Color32 pixel = pixels[from + column];
                        // The screen's alpha means nothing here; the paper is opaque.
                        pixel.a = 255;
                        cut[(row * width) + column] = pixel;
                    }
                }

                shot = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "BetterCoop.PageShot",
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                shot.SetPixels32(cut);
                shot.Apply(false);
            }
            finally
            {
                Destroy(screen);
            }
        }
        catch (Exception exception)
        {
            Guard.Report("PageShooter.Take", exception);
            if (shot != null)
            {
                Destroy(shot);
            }

            shot = null;
        }

        done(shot);
    }
}

/// <summary>
/// The page's picture crumpled into a ball and thrown: drawn as a grid of
/// triangles, each shaded by how it faces the light and drawn far to near,
/// the rounded corners left out. It crumples where the page was, folding
/// over and pressing together, then is tossed off in a random direction,
/// tumbling, growing smaller and fainter until it is gone.
/// </summary>
internal sealed class PaperWad : MaskableGraphic
{
    private const int Columns = 21;

    private const int Rows = 17;

    private const float CrumpleSeconds = 1.3f;

    /// <summary>When the toss begins, just before the last of the pressing.</summary>
    private const float ThrowFrom = 1.15f;

    /// <summary>How long the toss lasts, the wad shrinking and fading all the while.</summary>
    private const float FlySeconds = 1.1f;

    /// <summary>How small the wad is by the end of the toss.</summary>
    private const float Smallest = 0.2f;

    private const float Focal = 1800f;

    /// <summary>The rounded corners of the paper, in its own units.</summary>
    private const float Corner = 26f;

    private static readonly Num.Vector3 Light = new(-0.45f, 0.6f, -1f);

    private readonly List<WadTriangle> triangles = new();

    private Texture2D? shot;

    private PaperCrumple crumple = null!;

    private Num.Vector3[] points = null!;

    private float[] shown = null!;

    private Vector2[] uvs = null!;

    private float age;

    private Vector2 place;

    private Vector2 velocity;

    private float gravity;

    private Num.Vector3 spinAxis;

    private float spin;

    private Num.Vector3 wobbleAxis;

    public override Texture mainTexture => shot != null ? shot : s_WhiteTexture;

    public void Begin(Texture2D picture)
    {
        shot = picture;
        raycastTarget = false;
        // The picture comes after the graphic woke with none.
        SetMaterialDirty();
        Vector2 size = rectTransform.rect.size;
        int seed = UnityEngine.Random.Range(1, int.MaxValue);
        crumple = new PaperCrumple(size.x, size.y, Columns, Rows, seed);
        points = new Num.Vector3[crumple.Count];
        shown = new float[crumple.Count];
        uvs = new Vector2[crumple.Count];
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                int i = (row * Columns) + column;
                Num.Vector3 rest = crumple.Rest(column, row);
                uvs[i] = new Vector2(column / (float)(Columns - 1), row / (float)(Rows - 1));
                shown[i] = InsideCorners(rest, size) ? 1f : 0f;
            }
        }

        // Tossed off at random, a little more often up and away than down.
        float angle = UnityEngine.Random.Range(-0.35f, 1.35f) * Mathf.PI;
        float speed = UnityEngine.Random.Range(700f, 1050f);
        velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        gravity = UnityEngine.Random.Range(900f, 1300f);
        spinAxis = Num.Vector3.Normalize(new Num.Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-0.4f, 0.4f)) + new Num.Vector3(0.01f, 0f, 0f));
        spin = UnityEngine.Random.Range(4f, 7f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        wobbleAxis = Num.Vector3.Normalize(new Num.Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), 0.3f));
        Shape();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (shot != null)
        {
            Destroy(shot);
        }
    }

    private static bool InsideCorners(Num.Vector3 point, Vector2 size)
    {
        float x = Mathf.Abs(point.X) - ((size.x / 2f) - Corner);
        float y = Mathf.Abs(point.Y) - ((size.y / 2f) - Corner);
        return x <= 0f || y <= 0f || ((x * x) + (y * y)) <= Corner * Corner;
    }

    private void Update()
    {
        if (crumple == null)
        {
            return;
        }

        float elapsed = Time.unscaledDeltaTime;
        age += elapsed;
        if (age > ThrowFrom)
        {
            velocity.y -= gravity * elapsed;
            place += velocity * elapsed;
        }

        Shape();
        if (age >= ThrowFrom + FlySeconds)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>How far through the toss, 0 before it and 1 at its end.</summary>
    private float Flown => Mathf.Clamp01((age - ThrowFrom) / FlySeconds);

    private void Shape()
    {
        float progress = Mathf.Clamp01(age / CrumpleSeconds);
        crumple.Shape(progress, points);
        float flying = Mathf.Max(0f, age - ThrowFrom);
        float size = Mathf.Lerp(1f, Smallest, Easing.Out(Flown));
        for (int i = 0; i < points.Length; i++)
        {
            points[i] *= size;
        }

        // Turned a little as the hands work it, so its folds catch the light, then tumbling as it flies.
        Num.Quaternion turn =
            Num.Quaternion.CreateFromAxisAngle(spinAxis, spin * flying)
            * Num.Quaternion.CreateFromAxisAngle(wobbleAxis, 0.7f * Easing.InOut(progress));
        var shift = new Num.Vector3(place.x, place.y, flying * 250f);
        crumple.Triangles(points, turn, shift, Focal, Light, triangles);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (crumple == null)
        {
            return;
        }

        float fade = 1f - Easing.InOut(Flown);
        foreach (WadTriangle triangle in triangles)
        {
            float alpha = fade * Mathf.Min(shown[triangle.A], Mathf.Min(shown[triangle.B], shown[triangle.C]));
            if (alpha <= 0f)
            {
                continue;
            }

            // The back of the sheet is the same paper, its print showing through faintly.
            float light = triangle.Light;
            Color32 shade = new Color(light, light, triangle.Back ? light * 0.97f : light, alpha);
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector3(triangle.SeenA.X, triangle.SeenA.Y), shade, uvs[triangle.A]);
            mesh.AddVert(new Vector3(triangle.SeenB.X, triangle.SeenB.Y), shade, uvs[triangle.B]);
            mesh.AddVert(new Vector3(triangle.SeenC.X, triangle.SeenC.Y), shade, uvs[triangle.C]);
            mesh.AddTriangle(start, start + 1, start + 2);
        }
    }
}

/// <summary>
/// A hand taking the page: it comes down from the top of the screen to the
/// page's top edge, closes on it with a little squeeze, and lifts it away
/// upwards, slowly at first and then quickly, the paper swinging a little
/// from where it is held.
/// </summary>
internal sealed class PaperHandIn : MonoBehaviour
{
    private const float ReachSeconds = 0.42f;

    private const float HoldSeconds = 0.16f;

    private const float LiftSeconds = 0.55f;

    /// <summary>The hand's width on the page, in canvas units; the picture keeps its own proportions.</summary>
    private const float HandWidth = 210f;

    /// <summary>How far the hand comes down over the paper's top edge, as a share of its height.</summary>
    private const float Grip = 0.2f;

    private RectTransform paper = null!;

    private RectTransform hand = null!;

    private Texture2D? shot;

    private Action? gone;

    private float age;

    private float handHeight;

    private float above;

    private float sway;

    private float handX;

    public void Begin(Texture2D picture, Texture? arm, Color skin, Action done)
    {
        shot = picture;
        gone = done;
        var holder = (RectTransform)transform;
        Vector2 size = holder.rect.size;

        paper = PaperList.NewRect("Paper", holder);
        paper.anchorMin = paper.anchorMax = new Vector2(0.5f, 1f);
        paper.pivot = new Vector2(0.5f, 1f);
        paper.sizeDelta = size;
        RawImage page = paper.gameObject.AddComponent<RawImage>();
        page.texture = picture;
        page.raycastTarget = false;

        handHeight = HandWidth * PhotoStudio.HandHeight / PhotoStudio.HandWidth;
        handX = UnityEngine.Random.Range(-0.18f, 0.18f) * size.x;
        hand = PaperList.NewRect("Hand", holder);
        hand.anchorMin = hand.anchorMax = new Vector2(0.5f, 1f);
        hand.pivot = new Vector2(0.5f, 0f);
        hand.sizeDelta = new Vector2(HandWidth, handHeight);
        if (arm != null)
        {
            RawImage image = hand.gameObject.AddComponent<RawImage>();
            image.texture = arm;
            image.raycastTarget = false;
        }
        else
        {
            var ink = new Ink(skin.r, skin.g, skin.b);
            VectorView.Add(hand, s => FormDrawing.Hand(s.x, s.y, ink), Color.clear);
            VectorView.DrawAll(hand);
        }

        // From the paper's top edge to wholly above the screen.
        above = Screen.height;
        if (holder.GetComponentInParent<Canvas>() is Canvas canvas && canvas.rootCanvas.transform is RectTransform root)
        {
            above = Mathf.Max(0f, (root.rect.height / 2f) - holder.localPosition.y - (size.y / 2f)) + 40f;
        }

        sway = UnityEngine.Random.Range(-4f, 4f);
        Pose();
    }

    private void OnDestroy()
    {
        if (shot != null)
        {
            Destroy(shot);
        }
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        Pose();
        if (age > ReachSeconds + HoldSeconds + LiftSeconds)
        {
            Action? done = gone;
            gone = null;
            Destroy(gameObject);
            done?.Invoke();
        }
    }

    private void Pose()
    {
        // The hand's foot, where its fingers are, measured down from the paper's top edge.
        float gripped = -handHeight * Grip;
        float handFoot;
        float lift = 0f;
        float squeeze = 1f;
        if (age < ReachSeconds)
        {
            handFoot = Mathf.Lerp(above, gripped, Easing.Out(age / ReachSeconds));
        }
        else if (age < ReachSeconds + HoldSeconds)
        {
            handFoot = gripped;
            squeeze = 1f - (0.06f * Mathf.Sin((age - ReachSeconds) / HoldSeconds * Mathf.PI));
        }
        else
        {
            float t = (age - ReachSeconds - HoldSeconds) / LiftSeconds;
            // Slowly at first, as the paper is taken up, then quickly away.
            lift = Mathf.Pow(Mathf.Clamp01(t), 2.2f) * (above + paper.sizeDelta.y + 80f);
            handFoot = gripped + lift;
        }

        hand.anchoredPosition = new Vector2(handX, handFoot);
        hand.localScale = new Vector3(1f, squeeze, 1f);
        paper.anchoredPosition = new Vector2(0f, lift);
        float swing = lift > 0f ? sway * Mathf.Sin(Mathf.Min(1f, lift / 400f) * Mathf.PI) : 0f;
        paper.localRotation = Quaternion.Euler(0f, 0f, swing);
    }
}
