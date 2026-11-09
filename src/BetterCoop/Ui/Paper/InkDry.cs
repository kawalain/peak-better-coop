using System.Collections.Generic;
using BetterCoop.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// Dries a struck impression back in patches (see <see cref="InkDrying"/>):
/// the graphic's quad is cut into a fine grid whose corners carry how much
/// ink is left there, so the drawing breaks into soft-edged islands that
/// shrink as it dries (ADR 0011). Undried or fully dried, it leaves the quad
/// as it is.
/// </summary>
[RequireComponent(typeof(Graphic))]
internal sealed class InkDry : BaseMeshEffect
{
    /// <summary>The grid's cell, in UI units: fine enough that the patches' edges stay round.</summary>
    private const float Cell = 2.5f;

    private readonly List<UIVertex> quad = new();

    private float dried;

    private int seed = 1;

    /// <summary>How far the ink has dried, 0 (wet) to 1 (gone).</summary>
    public float Dried
    {
        get => dried;
        set
        {
            value = Mathf.Clamp01(value);
            if (!Mathf.Approximately(value, dried))
            {
                dried = value;
                graphic.SetVerticesDirty();
            }
        }
    }

    /// <summary>Which pattern the ink soaks in; a new one for every strike.</summary>
    public int Seed
    {
        get => seed;
        set
        {
            if (value != seed)
            {
                seed = value;
                graphic.SetVerticesDirty();
            }
        }
    }

    /// <summary>How much ink is left at a point of <paramref name="area"/>, in its own local units.</summary>
    public static float LeftAt(Rect area, Vector2 local, float dried, int seed) =>
        dried <= 0f ? 1f
        : dried >= 1f ? 0f
        : InkDrying.Left(dried, InkDrying.Wetness(local.x - area.xMin, area.yMax - local.y, area.width, area.height, seed));

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive() || dried <= 0f || mesh.currentVertCount != 4)
        {
            return;
        }

        quad.Clear();
        mesh.GetUIVertexStream(quad);
        // The stream is two triangles: 0-1-2 and 2-3-0, from the bottom-left corner round.
        UIVertex bottomLeft = quad[0];
        UIVertex topRight = quad[2];
        mesh.Clear();
        if (dried >= 1f)
        {
            return;
        }

        Rect area = graphic.rectTransform.rect;
        Vector2 from = bottomLeft.position;
        Vector2 to = topRight.position;
        int columns = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(to.x - from.x) / Cell), 1, 240);
        int rows = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(to.y - from.y) / Cell), 1, 240);
        Color32 tint = bottomLeft.color;
        for (int row = 0; row <= rows; row++)
        {
            float v = row / (float)rows;
            for (int column = 0; column <= columns; column++)
            {
                float u = column / (float)columns;
                var position = new Vector2(Mathf.Lerp(from.x, to.x, u), Mathf.Lerp(from.y, to.y, v));
                Color32 colour = tint;
                colour.a = (byte)Mathf.RoundToInt(tint.a * LeftAt(area, position, dried, seed));
                mesh.AddVert(
                    position,
                    colour,
                    new Vector4(
                        Mathf.Lerp(bottomLeft.uv0.x, topRight.uv0.x, u),
                        Mathf.Lerp(bottomLeft.uv0.y, topRight.uv0.y, v)));
            }
        }

        int stride = columns + 1;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int a = row * stride + column;
                mesh.AddTriangle(a, a + stride, a + stride + 1);
                mesh.AddTriangle(a, a + stride + 1, a + 1);
            }
        }
    }
}
