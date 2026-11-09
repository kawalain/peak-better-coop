using System;
using System.Collections.Generic;
using BetterCoop.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// A pen stroke on the paper: a polyline drawn as a ribbon that thins at
/// its ends as a pen lifts, with softened edges. Only the part between
/// <see cref="Erased"/> and <see cref="Drawn"/> shows, both as fractions of
/// its length, so it can be written and rubbed out along the way the pen went.
/// </summary>
internal sealed class PenStroke : MaskableGraphic
{
    /// <summary>The soft edge each side, in UI units, standing in for antialiasing.</summary>
    private const float Feather = 1.2f;

    private IReadOnlyList<(float X, float Y)> points = Array.Empty<(float X, float Y)>();

    private float drawn;

    private float erased;

    /// <summary>The path, in the rect's size with y down.</summary>
    public IReadOnlyList<(float X, float Y)> Points
    {
        get => points;
        set
        {
            points = value;
            SetVerticesDirty();
        }
    }

    public float Width { get; set; } = 3f;

    /// <summary>How far along the pen has written, 0 to 1.</summary>
    public float Drawn
    {
        get => drawn;
        set
        {
            if (!Mathf.Approximately(drawn, value))
            {
                drawn = value;
                SetVerticesDirty();
            }
        }
    }

    /// <summary>How far along the eraser has rubbed out, 0 to 1.</summary>
    public float Erased
    {
        get => erased;
        set
        {
            if (!Mathf.Approximately(erased, value))
            {
                erased = value;
                SetVerticesDirty();
            }
        }
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        float length = PenPaths.Length(points);
        float from = erased * length;
        float to = drawn * length;
        if (points.Count < 2 || to - from < 0.01f)
        {
            return;
        }

        // The part of the path that shows, cut where the eraser and the pen are.
        var shown = new List<(Vector2 Point, float Along)>();
        float along = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            if (i > 0)
            {
                float step = PenPaths.Distance(points[i - 1], points[i]);
                float next = along + step;
                if (step > 0f && along < from && next > from)
                {
                    shown.Add((Lerp(points[i - 1], points[i], (from - along) / step), from));
                }

                if (step > 0f && along < to && next > to)
                {
                    shown.Add((Lerp(points[i - 1], points[i], (to - along) / step), to));
                    break;
                }

                along = next;
            }

            if (along >= from && along <= to)
            {
                shown.Add((Place(points[i]), along));
            }
        }

        if (shown.Count < 2)
        {
            return;
        }

        Color32 solid = color;
        Color32 clear = solid;
        clear.a = 0;
        for (int i = 0; i < shown.Count; i++)
        {
            Vector2 before = shown[Math.Max(0, i - 1)].Point;
            Vector2 after = shown[Math.Min(shown.Count - 1, i + 1)].Point;
            Vector2 direction = (after - before).normalized;
            var normal = new Vector2(-direction.y, direction.x);

            // A pen presses in after it lands and lifts before it leaves.
            float s = shown[i].Along / length;
            float pressure = Mathf.Min(1f, 0.45f + s * 6f) * Mathf.Min(1f, 0.45f + (1f - s) * 6f);
            float half = Width * 0.5f * pressure;
            float inner = Mathf.Max(0f, half - Feather * 0.5f);
            float outer = half + Feather * 0.5f;
            Vector2 p = shown[i].Point;
            mesh.AddVert(p - normal * outer, clear, Vector4.zero);
            mesh.AddVert(p - normal * inner, solid, Vector4.zero);
            mesh.AddVert(p + normal * inner, solid, Vector4.zero);
            mesh.AddVert(p + normal * outer, clear, Vector4.zero);
            if (i > 0)
            {
                int a = (i - 1) * 4;
                int b = i * 4;
                for (int k = 0; k < 3; k++)
                {
                    mesh.AddTriangle(a + k, b + k, b + k + 1);
                    mesh.AddTriangle(a + k, b + k + 1, a + k + 1);
                }
            }
        }
    }

    private Vector2 Place((float X, float Y) point)
    {
        Rect rect = rectTransform.rect;
        return new Vector2(rect.xMin + point.X, rect.yMax - point.Y);
    }

    private Vector2 Lerp((float X, float Y) a, (float X, float Y) b, float t) => Vector2.Lerp(Place(a), Place(b), t);
}

/// <summary>
/// A mark a pen writes on the form, such as a tick or a ring, that comes
/// and goes with what it marks: rubbed out along its stroke as with an
/// eraser, and written again stroke by stroke, a little differently each time.
/// </summary>
internal sealed class PenMark : MonoBehaviour
{
    private const float EraseSeconds = 0.16f;

    private PenStroke stroke = null!;

    private Func<Vector2, System.Random, IReadOnlyList<(float X, float Y)>> path = null!;

    private readonly System.Random hand = new();

    /// <summary>The wobble of the mark now on the paper, so a mark fitted again keeps its hand.</summary>
    private int strokeSeed;

    /// <summary>The size the mark on the paper was made for.</summary>
    private Vector2 madeFor;

    private bool wanted;

    private float drawSeconds;

    private float wait;

    private Phase phase;

    /// <summary>How far the pen or eraser has gone in time, 0 to 1, before easing.</summary>
    private float progress;

    private enum Phase
    {
        Hidden,
        Shown,
        Drawing,
        Erasing,
    }

    /// <summary>Adds a mark over <paramref name="rect"/>, written along <paramref name="path"/> for the rect's size.</summary>
    public static PenMark Add(
        RectTransform rect,
        Func<Vector2, System.Random, IReadOnlyList<(float X, float Y)>> path,
        float width,
        float drawSeconds,
        bool shown)
    {
        PenStroke stroke = rect.gameObject.AddComponent<PenStroke>();
        stroke.color = PaperList.Pen;
        stroke.raycastTarget = false;
        stroke.Width = width;
        PenMark mark = rect.gameObject.AddComponent<PenMark>();
        mark.stroke = stroke;
        mark.path = path;
        mark.drawSeconds = drawSeconds;
        mark.wanted = shown;
        mark.phase = shown ? Phase.Shown : Phase.Hidden;
        stroke.Drawn = shown ? 1f : 0f;
        mark.pending = shown;
        return mark;
    }

    /// <summary>Whether the mark should be on the paper; a change plays out over the next moments.</summary>
    public bool Shown
    {
        get => wanted;
        set
        {
            if (wanted == value)
            {
                return;
            }

            wanted = value;
            // A new mark waits for the eraser of any other to pass first.
            wait = value ? EraseSeconds : 0f;
        }
    }

    /// <summary>Whether the path still has to be made once the rect has its size.</summary>
    private bool pending;

    /// <summary>Makes the mark's path for <paramref name="size"/>: a fresh hand each time it is written anew, the same hand when only fitted again.</summary>
    private void Make(Vector2 size, bool fresh)
    {
        if (fresh)
        {
            strokeSeed = hand.Next();
        }

        madeFor = size;
        stroke.Points = path(size, new System.Random(strokeSeed));
    }

    private void LateUpdate()
    {
        try
        {
            Step(Time.unscaledDeltaTime);
        }
        catch (Exception exception)
        {
            Guard.Report("PenMark", exception);
            enabled = false;
        }
    }

    private void Step(float seconds)
    {
        Vector2 size = stroke.rectTransform.rect.size;
        if (pending && size.x > 1f)
        {
            pending = false;
            Make(size, fresh: true);
        }
        else if (phase != Phase.Hidden && size.x > 1f && (size - madeFor).sqrMagnitude > 0.25f)
        {
            // Its place was laid out again, as the page settling on opening: the same mark, fitted to it.
            Make(size, fresh: false);
        }

        switch (phase)
        {
            case Phase.Hidden when wanted:
                wait -= seconds;
                if (wait <= 0f)
                {
                    Make(size, fresh: true);
                    stroke.Erased = 0f;
                    stroke.Drawn = 0f;
                    progress = 0f;
                    phase = Phase.Drawing;
                }

                break;
            case Phase.Shown when !wanted:
            case Phase.Drawing when !wanted:
                progress = 0f;
                phase = Phase.Erasing;
                break;
            case Phase.Drawing:
                progress = Mathf.Min(1f, progress + seconds / drawSeconds);
                stroke.Drawn = Easing.InOut(progress);
                if (progress >= 1f)
                {
                    phase = Phase.Shown;
                }

                break;
            case Phase.Erasing:
                progress = Mathf.Min(1f, progress + seconds / EraseSeconds);
                stroke.Erased = Mathf.Min(stroke.Drawn, Easing.Out(progress) * stroke.Drawn);
                if (progress >= 1f)
                {
                    stroke.Drawn = 0f;
                    stroke.Erased = 0f;
                    phase = Phase.Hidden;
                }

                break;
        }
    }
}
