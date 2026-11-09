using BetterCoop.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// A scout's snapshot: their passport photo, or a silhouette in their skin
/// colour when none can be taken, on a white print with a deckle edge, set a
/// little askew as a hand mounts it, with their name and, if given, their
/// role printed under it. The Pair page mounts its prints in an album's
/// photo corners; the Pair letter clips them on.
/// </summary>
internal static class ScoutPhoto
{
    /// <summary>How a print is held to the paper.</summary>
    internal enum Mount
    {
        /// <summary>Held by something else, such as an album's corners, or held in the hand.</summary>
        Loose,

        /// <summary>Clipped on at its top with a paper clip, as a photo sent with a letter.</summary>
        Clip,
    }

    public const float Width = 124f;

    /// <summary>A paper clip's size, standing upright.</summary>
    private static readonly Vector2 ClipSize = new(15f, 42f);

    public const float PhotoHeight = Width * PhotoStudio.Height / PhotoStudio.Width;

    public const float CaptionHeight = 56f;

    /// <summary>The air on each side of a caption, inside its photo's width.</summary>
    private const float CaptionMargin = 4f;

    /// <summary>The whole print, photo and caption.</summary>
    public const float Height = PhotoHeight + CaptionHeight;

    /// <summary>A scout as a snapshot shows them: their photo, and their name and role under it.</summary>
    public static void Add(RectTransform parent, TMP_Text template, IPortraits portraits, int actor, string name, string? role, Mount mount = Mount.Loose)
    {
        RectTransform photo = PhotoArea(parent);
        photo.localRotation = Quaternion.Euler(0f, 0f, Tilt(actor));
        VectorView.Add(photo, s => FormDrawing.Print(s.x, s.y), Color.clear);
        RectTransform picture = PaperList.NewRect("Picture", photo);
        picture.anchorMin = Vector2.zero;
        picture.anchorMax = Vector2.one;
        picture.offsetMin = new Vector2(FormDrawing.PrintBorder, FormDrawing.PrintBorder);
        picture.offsetMax = new Vector2(-FormDrawing.PrintBorder, -FormDrawing.PrintBorder);
        PhotoPrint.Add(picture, portraits, actor);

        RectTransform edge = PaperList.NewRect("Edge", picture);
        edge.anchorMin = Vector2.zero;
        edge.anchorMax = Vector2.one;
        edge.offsetMin = edge.offsetMax = Vector2.zero;
        VectorView.Add(edge, s => FormDrawing.PhotoEdge(s.x, s.y), Color.clear);
        if (mount == Mount.Clip)
        {
            // Slipped over the print's top edge, near its left corner, a little crooked.
            RectTransform clip = PaperList.NewRect("Clip", photo);
            clip.anchorMin = clip.anchorMax = new Vector2(0f, 1f);
            clip.pivot = new Vector2(0.5f, 0.5f);
            clip.sizeDelta = new Vector2(ClipSize.x, ClipSize.y);
            clip.anchoredPosition = new Vector2(24f, -4f);
            clip.localRotation = Quaternion.Euler(0f, 0f, -7f);
            VectorView.Add(clip, s => FormDrawing.PaperClip(s.x, s.y), Color.clear);
        }

        RectTransform caption = Caption(parent);
        VerticalLayoutGroup lines = caption.gameObject.AddComponent<VerticalLayoutGroup>();
        lines.childControlWidth = lines.childControlHeight = true;
        lines.childForceExpandWidth = true;
        lines.childForceExpandHeight = false;
        lines.childAlignment = TextAnchor.UpperCenter;
        lines.spacing = -2f;
        TMP_Text printed = PaperText.Make(PaperList.NewRect("Name", caption).gameObject, template, PaperText.Value, TextAlignmentOptions.Top);
        PaperText.Fit(printed, 14f);
        // A name too long even at the smallest size is cut, never spread under a neighbour's photo.
        printed.overflowMode = TextOverflowModes.Ellipsis;
        Tall(printed, 30f);
        printed.text = name;
        if (role != null)
        {
            TMP_Text under = PaperText.Make(PaperList.NewRect("Role", caption).gameObject, template, PaperText.Note * 0.85f, TextAlignmentOptions.Top);
            under.text = role;
            under.color = PaperText.Ink;
            Tall(under, 20f);
        }
    }

    /// <summary>
    /// An album's black photo corners over the print of <paramref name="actor"/>
    /// in <paramref name="parent"/>, laid out as <see cref="Add"/> lays the print.
    /// They belong to the page, so they stay when the print is taken out.
    /// </summary>
    public static void Corners(RectTransform parent, int actor)
    {
        RectTransform corners = PhotoArea(parent);
        corners.name = "Corners";
        float reach = FormDrawing.CornerReach;
        corners.sizeDelta = new Vector2(Width + 2f * reach, PhotoHeight + 2f * reach);
        // Turned about the same point as the print, the print's top middle.
        corners.pivot = new Vector2(0.5f, 1f - reach / (PhotoHeight + 2f * reach));
        corners.anchoredPosition = Vector2.zero;
        corners.localRotation = Quaternion.Euler(0f, 0f, Tilt(actor));
        VectorView.Add(corners, s => FormDrawing.PhotoCorners(s.x, s.y), Color.clear);
    }

    /// <summary>How far askew a scout's print is mounted, in degrees: the same for them every time.</summary>
    public static float Tilt(int actor) => (((actor * 37) % 7 + 7) % 7 - 3) * 0.6f;

    /// <summary>Gives a caption line a set height, which a shrinking name cannot report for itself.</summary>
    private static void Tall(TMP_Text text, float height)
    {
        LayoutElement size = text.gameObject.AddComponent<LayoutElement>();
        size.minHeight = size.preferredHeight = height;
        size.layoutPriority = 2;
    }

    /// <summary>A whole-area layer of a photo, under its edge.</summary>
    internal static RectTransform Layer(string name, RectTransform photo)
    {
        RectTransform layer = PaperList.NewRect(name, photo);
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = layer.offsetMax = Vector2.zero;
        layer.SetAsFirstSibling();
        return layer;
    }

    public static RectTransform PhotoArea(RectTransform parent)
    {
        RectTransform photo = PaperList.NewRect("Photo", parent);
        photo.anchorMin = photo.anchorMax = new Vector2(0.5f, 1f);
        photo.pivot = new Vector2(0.5f, 1f);
        photo.sizeDelta = new Vector2(Width, PhotoHeight);
        photo.anchoredPosition = Vector2.zero;
        return photo;
    }

    public static RectTransform Caption(RectTransform parent)
    {
        RectTransform caption = PaperList.NewRect("Caption", parent);
        caption.anchorMin = Vector2.zero;
        caption.anchorMax = new Vector2(1f, 0f);
        caption.pivot = new Vector2(0.5f, 0f);
        // A little narrower than the photo, so captions side by side keep apart.
        caption.sizeDelta = new Vector2(-2f * CaptionMargin, CaptionHeight - 4f);
        caption.anchoredPosition = Vector2.zero;
        return caption;
    }
}
