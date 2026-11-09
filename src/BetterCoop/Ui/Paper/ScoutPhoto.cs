using BetterCoop.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// A scout's yearbook photo: their passport photo, or a silhouette in their
/// skin colour when none can be taken, in a thin printed edge, with their
/// name and, if given, their role printed under it. The Pair page and the
/// Pair letter print scouts the same way.
/// </summary>
internal static class ScoutPhoto
{
    public const float Width = 124f;

    public const float PhotoHeight = Width * PhotoStudio.Height / PhotoStudio.Width;

    public const float CaptionHeight = 56f;

    /// <summary>The whole print, photo and caption.</summary>
    public const float Height = PhotoHeight + CaptionHeight;

    /// <summary>A scout as the yearbook prints them: their photo, and their name and role under it.</summary>
    public static void Add(RectTransform parent, TMP_Text template, IPortraits portraits, int actor, string name, string? role)
    {
        RectTransform photo = PhotoArea(parent);
        PhotoPrint.Add(photo, portraits, actor);

        RectTransform edge = PaperList.NewRect("Edge", photo);
        edge.anchorMin = Vector2.zero;
        edge.anchorMax = Vector2.one;
        edge.offsetMin = edge.offsetMax = Vector2.zero;
        VectorView.Add(edge, s => FormDrawing.PhotoEdge(s.x, s.y), Color.clear);

        RectTransform caption = Caption(parent);
        VerticalLayoutGroup lines = caption.gameObject.AddComponent<VerticalLayoutGroup>();
        lines.childControlWidth = lines.childControlHeight = true;
        lines.childForceExpandWidth = true;
        lines.childForceExpandHeight = false;
        lines.childAlignment = TextAnchor.UpperCenter;
        lines.spacing = -2f;
        TMP_Text printed = PaperText.Make(PaperList.NewRect("Name", caption).gameObject, template, PaperText.Value, TextAlignmentOptions.Top);
        PaperText.Fit(printed, 14f);
        Tall(printed, 30f);
        printed.text = name;
        if (role != null)
        {
            TMP_Text under = PaperText.Make(PaperList.NewRect("Role", caption).gameObject, template, PaperText.Note * 0.85f, TextAlignmentOptions.Top);
            under.text = role;
            under.color = PaperList.Faded(PaperText.Ink, 0.6f);
            Tall(under, 20f);
        }
    }

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
        caption.sizeDelta = new Vector2(8f, CaptionHeight - 4f);
        caption.anchoredPosition = Vector2.zero;
        return caption;
    }
}
