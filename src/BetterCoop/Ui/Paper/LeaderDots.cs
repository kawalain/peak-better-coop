using System;
using BetterCoop.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// The dots leading from a printed label to its answer: one dot in a small
/// tile, repeated along the leader, ending at its right edge. One texture
/// serves every leader, so a form with many fields costs no drawing.
/// </summary>
[RequireComponent(typeof(RawImage))]
internal sealed class LeaderDots : MonoBehaviour
{
    /// <summary>Room for one dot, in UI units.</summary>
    private const float Step = 7f;

    private const int TileWidth = 14;

    private const int TileHeight = 20;

    private static Texture2D? tile;

    private RawImage image = null!;

    public static void Add(RectTransform rect)
    {
        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        image.texture = Tile();
        rect.gameObject.AddComponent<LeaderDots>().image = image;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (image == null)
        {
            return;
        }

        // Whole tiles end at the right edge, the dots running on from the label.
        float tiles = ((RectTransform)transform).rect.width / Step;
        image.uvRect = new Rect(-(tiles - Mathf.Floor(tiles)), 0f, tiles, 1f);
    }

    private void Start() => OnRectTransformDimensionsChange();

    /// <summary>One soft dot near the tile's foot, in premultiplied colour as the menu's shader blends.</summary>
    private static Texture2D Tile()
    {
        if (tile != null)
        {
            return tile;
        }

        tile = new Texture2D(TileWidth, TileHeight, TextureFormat.RGBA32, false)
        {
            name = "BetterCoop.LeaderDot",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        Ink ink = PaperDrawing.Paper.Mix(PaperDrawing.InkBlack, 0.45f);
        float cx = TileWidth / 2f;
        float cy = 3.6f;
        const float radius = 2.4f;
        var pixels = new Color[TileWidth * TileHeight];
        for (int y = 0; y < TileHeight; y++)
        {
            for (int x = 0; x < TileWidth; x++)
            {
                float d = Mathf.Sqrt(((x + 0.5f - cx) * (x + 0.5f - cx)) + ((y + 0.5f - cy) * (y + 0.5f - cy)));
                float cover = Mathf.Clamp01(radius + 0.5f - d);
                pixels[(y * TileWidth) + x] = new Color(ink.R * cover, ink.G * cover, ink.B * cover, cover);
            }
        }

        tile.SetPixels(pixels);
        tile.Apply(false, true);
        return tile;
    }
}
