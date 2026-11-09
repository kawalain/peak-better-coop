using System;
using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;
using Object = UnityEngine.Object;

namespace BetterCoop.Ui.Paper;

/// <summary>A scout's passport photo and skin colour, by actor.</summary>
internal interface IPortraits
{
    /// <summary>The scout's photo, or null when none can be taken.</summary>
    Texture? Photo(int actor);

    /// <summary>The scout's skin colour, for the silhouette shown without a photo.</summary>
    Color Skin(int actor);
}

/// <summary>
/// Takes passport photos of scouts on the <see cref="ModelStage"/>: a copy
/// of the passport's own dressed-up scout stands there, framed as the
/// passport frames a face, and is drawn once per look into a texture of its
/// own. The copy is made when the Airport's passport wakes and kept across
/// scenes. Without it, or if a photo fails, there is no photo and a
/// silhouette stands in.
/// </summary>
[HarmonyPatch]
[HarmonyWrapSafe]
internal static class PhotoStudio
{
    public const int Width = 320;

    public const int Height = 400;

    /// <summary>Where the passport's camera sits when it shows the face, in its parent.</summary>
    private static readonly Vector3 FacePlace = new(0f, 1.65f, 1f);

    private const float FaceSize = 0.6f;

    private static readonly Dictionary<string, RenderTexture> Photos = new();

    /// <summary>The looks photographed, oldest first, so the oldest goes when there are too many.</summary>
    private static readonly List<string> Order = new();

    /// <summary>The most photos kept at once: a whole room and the Zoo's scouts, with room to spare.</summary>
    private const int MostPhotos = 48;

    /// <summary>Photos let go, kept a little while so a print still showing one can change to the new one first.</summary>
    private static readonly List<(RenderTexture Photo, float At)> Retired = new();

    /// <summary>How long a photo let go is kept: longer than a print takes to look for a new one.</summary>
    private const float RetireSeconds = 3f;

    private static PlayerCustomizationDummy? model;

    /// <summary>Where the camera stands from the model, in the model's own frame, and which way it looks.</summary>
    private static Vector3 eye;

    private static Quaternion turn;

    private static bool broken;

    /// <summary>The pale blue behind a passport photo.</summary>
    public static Color Backdrop => new(0.80f, 0.86f, 0.90f, 1f);

    /// <summary>A photo of the scout the model is dressed as by <paramref name="dress"/>; <paramref name="look"/> names that look.</summary>
    public static Texture? Take(string look, Action<PlayerCustomizationDummy> dress)
    {
        if (Photos.TryGetValue(look, out RenderTexture? kept) && kept != null && kept.IsCreated())
        {
            return kept;
        }

        if (!Ready() || model == null)
        {
            return null;
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            model.gameObject.SetActive(true);
            dress(model);
            if (model.anim != null)
            {
                // The pause stops the clock; the pose is taken now regardless.
                model.anim.updateMode = AnimatorUpdateMode.UnscaledTime;
                model.anim.Update(0f);
            }

            Transform at = model.transform;
            Quaternion facing = at.rotation * turn;
            // Lit from over the viewer's shoulder.
            Vector3 light = (facing * Vector3.forward) - (0.6f * (facing * Vector3.up)) + (0.4f * (facing * Vector3.right));
            var shot = new ModelStage.Shot(at.TransformPoint(eye), facing, FaceSize, 0.05f, 20f, Backdrop, light.normalized);
            RenderTexture photo = ModelStage.Draw(model.gameObject, shot, Width, Height);
            photo.name = "BetterCoop.Photo";
            while (Order.Count >= MostPhotos)
            {
                Forget(Order[0]);
            }

            Photos[look] = photo;
            Order.Add(look);
            Plugin.Verbose($"Took a passport photo of {look} in {clock.Elapsed.TotalMilliseconds:F1} ms.");
            return photo;
        }
        catch (Exception exception)
        {
            // One failure means photos do not work here; silhouettes from now on.
            broken = true;
            Guard.Report("PhotoStudio.Take", exception);
            return null;
        }
        finally
        {
            model.gameObject.SetActive(false);
        }
    }

    /// <summary>Whether a photo of this look is ready.</summary>
    public static bool Has(string look) => Photos.TryGetValue(look, out RenderTexture? kept) && kept != null && kept.IsCreated();

    /// <summary>Lets a photo go, as when its scout has changed how they look.</summary>
    public static void Forget(string look)
    {
        Order.Remove(look);
        if (Photos.TryGetValue(look, out RenderTexture? photo))
        {
            Photos.Remove(look);
            if (photo != null)
            {
                Retired.Add((photo, Time.unscaledTime));
            }
        }
    }

    /// <summary>Destroys the photos let go a while ago, which no print shows any more.</summary>
    public static void Sweep()
    {
        float now = Time.unscaledTime;
        while (Retired.Count > 0 && now - Retired[0].At >= RetireSeconds)
        {
            RenderTexture photo = Retired[0].Photo;
            Retired.RemoveAt(0);
            if (photo != null)
            {
                photo.Release();
                Object.Destroy(photo);
            }
        }
    }

    /// <summary>Whether photos can be taken here: the model is set up, or can be from the passport now.</summary>
    public static bool Working => Ready();

    /// <summary>The skin colour of a skin index, or a plain tan when the game's list is not there.</summary>
    public static Color SkinColour(int index)
    {
        try
        {
            CustomizationOption[] skins = Singleton<Customization>.Instance.skins;
            if (index >= 0 && index < skins.Length)
            {
                return skins[index].color;
            }
        }
        catch (Exception exception)
        {
            Plugin.Verbose($"No skin colours: {exception.Message}");
        }

        return new Color(0.93f, 0.74f, 0.55f);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PassportManager), nameof(PassportManager.Awake))]
    private static void PrepareWithPassport(PassportManager __instance) => SetUp(__instance);

    private static bool Ready()
    {
        if (model != null)
        {
            return true;
        }

        if (!broken && PassportManager.instance is PassportManager passport)
        {
            SetUp(passport);
        }

        return model != null;
    }

    private static void SetUp(PassportManager passport)
    {
        if (model != null || broken || passport.dummy == null || passport.dummyCamera == null)
        {
            return;
        }

        try
        {
            Build(passport.dummy, passport.dummyCamera);
        }
        catch (Exception exception)
        {
            broken = true;
            if (model != null)
            {
                Object.Destroy(model.gameObject);
                model = null;
            }

            Guard.Report("PhotoStudio.SetUp", exception);
        }
    }

    /// <summary>Copies the passport's scout onto the stage and notes where the passport's camera sees its face from.</summary>
    private static void Build(PlayerCustomizationDummy source, Camera sourceCamera)
    {
        Transform? cameraParent = sourceCamera.transform.parent;
        Vector3 faceCamera = cameraParent != null ? cameraParent.TransformPoint(FacePlace) : FacePlace;
        eye = source.transform.InverseTransformPoint(faceCamera);
        turn = Quaternion.Inverse(source.transform.rotation) * sourceCamera.transform.rotation;

        GameObject copy = Object.Instantiate(source.gameObject, ModelStage.Root);
        copy.SetActive(false);
        copy.name = "Scout";
        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = Quaternion.identity;
        copy.transform.localScale = source.transform.lossyScale;
        // A camera that came along with the scout would keep filming for the passport.
        foreach (Camera stray in copy.GetComponentsInChildren<Camera>(true))
        {
            stray.enabled = false;
        }

        model = copy.GetComponent<PlayerCustomizationDummy>();
        Plugin.Verbose("Set up passport photos from the passport's scout.");
    }
}

/// <summary>
/// Photos of the players in the room, dressed as they dress. A player who
/// changes how they look, as at the Airport's passport, gets a new photo and
/// the old one is let go.
/// </summary>
internal sealed class LivePortraits : IPortraits
{
    /// <summary>How soon a player who changed how they look is photographed again.</summary>
    private const float RetakeSeconds = 2f;

    /// <summary>The look each player was last photographed in, and when.</summary>
    private static readonly Dictionary<int, (string Look, float At)> Taken = new();

    public Texture? Photo(int actor) => Take(actor);

    public Color Skin(int actor) => PhotoStudio.SkinColour(Data(actor)?.currentSkin ?? 0);

    /// <summary>Takes one photo a player in the room still lacks; false when every player has theirs.</summary>
    public static bool WarmOne()
    {
        // Photos of players who left go, so joining and leaving again and again keeps no more.
        foreach (int gone in Taken.Keys.Where(actor => PhotonNetwork.CurrentRoom?.GetPlayer(actor) == null).ToList())
        {
            PhotoStudio.Forget(Taken[gone].Look);
            Taken.Remove(gone);
        }

        foreach (Photon.Realtime.Player player in PhotonNetwork.PlayerList)
        {
            if (Look(player.ActorNumber) is string look && !PhotoStudio.Has(look))
            {
                Take(player.ActorNumber);
                return true;
            }
        }

        return false;
    }

    private static Texture? Take(int actor)
    {
        if (PhotonNetwork.CurrentRoom?.GetPlayer(actor) is not Photon.Realtime.Player player)
        {
            return null;
        }

        if (Look(actor) is not string look)
        {
            // The game forgets every look as a scene loads, until they are sent again: the last photo stands meanwhile.
            return Taken.TryGetValue(actor, out (string Look, float At) last) && PhotoStudio.Has(last.Look)
                ? PhotoStudio.Take(last.Look, _ => { })
                : null;
        }

        float now = Time.unscaledTime;
        if (Taken.TryGetValue(actor, out (string Look, float At) before) && before.Look != look)
        {
            // A look changed again and again is photographed at most every few seconds.
            if (now - before.At < RetakeSeconds && PhotoStudio.Has(before.Look))
            {
                return PhotoStudio.Take(before.Look, _ => { });
            }

            PhotoStudio.Forget(before.Look);
        }
        else if (PhotoStudio.Has(look))
        {
            return PhotoStudio.Take(look, _ => { });
        }

        Taken[actor] = (look, now);
        return PhotoStudio.Take(look, model => model.UpdateDummy(player));
    }

    /// <summary>
    /// Everything that shows in a player's photo, apart from who they are; or
    /// null for a look the game has no such parts for, which shows the
    /// silhouette rather than failing the photo studio for everyone.
    /// </summary>
    private static string? Look(int actor) =>
        Data(actor) is CharacterCustomizationData d && Wearable(d)
            ? $"{actor}:{d.currentSkin}.{d.currentAccessory}.{d.currentEyes}.{d.currentMouth}.{d.currentOutfit}.{d.currentHat}.{d.currentSash}.{d.currentMedal}"
            : null;

    private static bool Wearable(CharacterCustomizationData d)
    {
        Customization options = Singleton<Customization>.Instance;
        if (options == null)
        {
            return false;
        }

        static bool In(int index, CustomizationOption[]? list, int least = 0) => list != null && index >= least && index < list.Length;
        return In(d.currentSkin, options.skins)
            && In(d.currentAccessory, options.accessories)
            && In(d.currentEyes, options.eyes)
            && In(d.currentMouth, options.mouths)
            && In(d.currentOutfit, options.fits)
            && In(d.currentHat, options.hats, least: -1)
            && d.currentSash >= 0;
    }

    /// <summary>
    /// The look the game has received for a player, or null before it has.
    /// The game's own lookup makes a default look for a player it has heard
    /// nothing of yet, which would be photographed and then let go as soon as
    /// the real look arrives; and it would keep that default for the game too.
    /// </summary>
    private static CharacterCustomizationData? Data(int actor)
    {
        try
        {
            PersistentPlayerDataService? service = GameHandler.GetService<PersistentPlayerDataService>();
            return service != null && service.PersistentPlayerDatas.TryGetValue(actor, out PersistentPlayerData? data)
                ? data?.customizationData
                : null;
        }
        catch (Exception exception)
        {
            Plugin.Verbose($"No look for {actor}: {exception.Message}");
            return null;
        }
    }
}

/// <summary>
/// Takes the photos the Roster will need before it is opened, one at a time
/// and a few frames apart, so opening it costs no wait: everyone in the room,
/// again whenever someone changes how they look.
/// </summary>
internal sealed class PhotoDarkroom : MonoBehaviour
{
    private const float Pause = 0.3f;

    /// <summary>More photos to take ahead of time, as the Zoo's made-up scouts; false when none is left.</summary>
    public static Func<bool>? More { get; set; }

    private float next;

    private void Update()
    {
        if (Time.unscaledTime < next)
        {
            return;
        }

        next = Time.unscaledTime + Pause;
        try
        {
            PhotoStudio.Sweep();
            if (!PhotoStudio.Working)
            {
                return;
            }

            if (PhotonNetwork.InRoom && LivePortraits.WarmOne())
            {
                return;
            }

            More?.Invoke();
        }
        catch (Exception exception)
        {
            Guard.Report("PhotoDarkroom", exception);
            enabled = false;
        }
    }
}
