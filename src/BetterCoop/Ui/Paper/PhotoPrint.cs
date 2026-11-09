using System;
using System.Collections.Generic;
using BetterCoop.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// The picture in a scout's printed photo: their passport photo, or a
/// silhouette in their skin colour while there is none. It looks again now
/// and then, so a photo taken after the paper opened, or taken again because
/// the scout changed how they look, takes the place of the one printed.
/// A picture printed for the first time comes up as in a darkroom's tray
/// (see <see cref="Darkroom"/>); one already printed shows at once.
/// </summary>
internal sealed class PhotoPrint : MonoBehaviour
{
    private const float LookSeconds = 0.5f;

    /// <summary>The pictures already printed: a photo, or a scout's silhouette.</summary>
    private static readonly HashSet<object> Printed = new();

    /// <summary>Counts the times every print was asked to come up again.</summary>
    private static int round;

    private Image developer = null!;

    private float developedAt = float.NegativeInfinity;

    private int seenRound;

    private IPortraits portraits = null!;

    private int actor;

    private RawImage? taken;

    private GameObject? silhouette;

    private float next;

    public static void Add(RectTransform photo, IPortraits portraits, int actor)
    {
        PhotoPrint print = photo.gameObject.AddComponent<PhotoPrint>();
        print.portraits = portraits;
        print.actor = actor;
        print.seenRound = round;
        // Blank paper over the picture, thinning as it develops; the picture's layers go under it.
        RectTransform blank = PaperList.NewRect("Developer", photo);
        blank.anchorMin = Vector2.zero;
        blank.anchorMax = Vector2.one;
        blank.offsetMin = blank.offsetMax = Vector2.zero;
        print.developer = blank.gameObject.AddComponent<Image>();
        print.developer.raycastTarget = false;
        print.developer.color = Color.clear;
        print.Show(portraits.Photo(actor));
        print.next = Time.unscaledTime + LookSeconds;
    }

    /// <summary>Has every print on the paper come up again from blank, as for the Zoo.</summary>
    public static void DevelopAgain()
    {
        Printed.Clear();
        round++;
    }

    private void Update()
    {
        if (seenRound != round)
        {
            seenRound = round;
            developedAt = Time.unscaledTime;
        }

        Develop();
        if (Time.unscaledTime < next)
        {
            return;
        }

        next = Time.unscaledTime + LookSeconds;
        try
        {
            Texture? now = portraits.Photo(actor);
            Texture? shown = taken != null && taken.enabled ? taken.texture : null;
            if (now != shown)
            {
                Show(now);
            }
        }
        catch (Exception exception)
        {
            Guard.Report("PhotoPrint", exception);
            enabled = false;
        }
    }

    /// <summary>Thins the blank paper over the picture and warms or clears its colours, as far as it has developed.</summary>
    private void Develop()
    {
        float elapsed = Time.unscaledTime - developedAt;
        if (elapsed > Darkroom.DevelopSeconds + 0.1f)
        {
            if (developer.color.a > 0f)
            {
                developer.color = Color.clear;
                Tint(Color.white);
            }

            return;
        }

        Color white = PaperText.ToColor(FormDrawing.PrintWhite);
        white.a = Darkroom.Blank(elapsed);
        developer.color = white;
        Color wet = PaperText.ToColor(Darkroom.Wet);
        Tint(Color.Lerp(Color.white, wet, Darkroom.Warmth(elapsed)));
    }

    private void Tint(Color colour)
    {
        if (taken != null)
        {
            taken.color = colour;
        }
    }

    private void Show(Texture? photo)
    {
        if (Printed.Add(photo != null ? photo : (object)(portraits, actor)))
        {
            developedAt = Time.unscaledTime;
            Develop();
        }

        RectTransform area = (RectTransform)transform;
        if (photo != null)
        {
            if (taken == null)
            {
                taken = ScoutPhoto.Layer("Taken", area).gameObject.AddComponent<RawImage>();
                taken.raycastTarget = false;
            }

            taken.texture = photo;
            taken.enabled = true;
            if (silhouette != null)
            {
                silhouette.SetActive(false);
            }

            return;
        }

        if (taken != null)
        {
            taken.enabled = false;
            taken.texture = null;
        }

        if (silhouette == null)
        {
            Color skin = portraits.Skin(actor);
            var ink = new Ink(skin.r, skin.g, skin.b);
            silhouette = VectorView.Add(ScoutPhoto.Layer("Silhouette", area), s => FormDrawing.Silhouette(s.x, s.y, ink), skin).gameObject;
        }

        silhouette.SetActive(true);
    }
}
