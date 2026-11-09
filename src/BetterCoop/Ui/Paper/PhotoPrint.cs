using System;
using BetterCoop.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// The picture in a scout's printed photo: their passport photo, or a
/// silhouette in their skin colour while there is none. It looks again now
/// and then, so a photo taken after the paper opened, or taken again because
/// the scout changed how they look, takes the place of the one printed.
/// </summary>
internal sealed class PhotoPrint : MonoBehaviour
{
    private const float LookSeconds = 0.5f;

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
        print.Show(portraits.Photo(actor));
        print.next = Time.unscaledTime + LookSeconds;
    }

    private void Update()
    {
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

    private void Show(Texture? photo)
    {
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
