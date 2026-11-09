using System;
using System.Collections.Generic;
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

    /// <summary>The scout's right forearm and hand, hand down, on a clear ground; null when none can be taken.</summary>
    Texture? Hand(int actor);
}

/// <summary>
/// Takes passport photos of scouts: a copy of the passport's own dressed-up
/// scout and camera, kept far below the world, framed as the passport frames
/// a face, and photographed once per look into a texture of its own. The copy
/// is made when the Airport's passport wakes and kept across scenes. Without
/// it, or if a photo fails, there is no photo and a silhouette stands in.
/// </summary>
[HarmonyPatch]
[HarmonyWrapSafe]
internal static class PhotoStudio
{
    public const int Width = 160;

    public const int Height = 200;

    /// <summary>Where the passport's camera sits when it shows the face, in its parent.</summary>
    private static readonly Vector3 FacePlace = new(0f, 1.65f, 1f);

    private const float FaceSize = 0.6f;

    private static readonly Vector3 StudioPlace = new(0f, -4000f, 0f);

    private static readonly Dictionary<string, RenderTexture> Photos = new();

    private static GameObject? studio;

    private static PlayerCustomizationDummy? model;

    private static Camera? camera;

    private static Light? lamp;

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

        if (!Ready() || model == null || camera == null)
        {
            return null;
        }

        RenderTexture? photo = null;
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

            photo = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                name = "BetterCoop.Photo",
                antiAliasing = 4,
                hideFlags = HideFlags.HideAndDontSave,
            };
            photo.Create();
            camera.aspect = Width / (float)Height;
            camera.targetTexture = photo;
            if (lamp != null)
            {
                lamp.enabled = true;
            }

            camera.Render();
            Photos[look] = photo;
            Plugin.Verbose($"Took a passport photo of {look} in {clock.Elapsed.TotalMilliseconds:F1} ms.");
            return photo;
        }
        catch (Exception exception)
        {
            // One failure means the studio does not work here; silhouettes from now on.
            broken = true;
            if (photo != null)
            {
                photo.Release();
                Object.Destroy(photo);
            }

            Guard.Report("PhotoStudio.Take", exception);
            return null;
        }
        finally
        {
            camera.targetTexture = null;
            if (lamp != null)
            {
                lamp.enabled = false;
            }

            model.gameObject.SetActive(false);
        }
    }

    public const int HandWidth = 160;

    public const int HandHeight = 320;

    /// <summary>The colour behind a hand while it is taken, keyed out after.</summary>
    private static readonly Color HandKey = new(1f, 0f, 1f, 1f);

    private static readonly Dictionary<string, Texture2D?> Hands = new();

    /// <summary>
    /// A picture of the right forearm and hand of the scout the model is
    /// dressed as, hanging down, on a clear ground: the camera looks at the
    /// arm from outside the body, square to the forearm, and sees only as
    /// deep as the arm so the body behind it is left out. Null when the
    /// model's bones cannot be found or the arm is not in the picture.
    /// </summary>
    public static Texture? TakeHand(string look, Action<PlayerCustomizationDummy> dress)
    {
        if (Hands.TryGetValue(look, out Texture2D? kept))
        {
            return kept;
        }

        if (!Ready() || model == null || camera == null)
        {
            return null;
        }

        Transform lens = camera.transform;
        Vector3 lensPlace = lens.position;
        Quaternion lensTurn = lens.rotation;
        float size = camera.orthographicSize;
        float near = camera.nearClipPlane;
        float far = camera.farClipPlane;
        Vector3 lampPlace = lamp != null ? lamp.transform.position : Vector3.zero;
        RenderTexture? shot = null;
        RenderTexture? active = RenderTexture.active;
        Texture2D? hand = null;
        try
        {
            model.gameObject.SetActive(true);
            dress(model);
            if (model.anim != null)
            {
                model.anim.updateMode = AnimatorUpdateMode.UnscaledTime;
                model.anim.Update(0f);
            }

            // The game names a scout's bones after their body parts.
            Transform? palm = HelperFunctions.FindChildRecursive(nameof(BodypartType.Hand_R), model.transform);
            Transform? elbow = HelperFunctions.FindChildRecursive(nameof(BodypartType.Elbow_R), model.transform)
                ?? HelperFunctions.FindChildRecursive(nameof(BodypartType.Arm_R), model.transform);
            if (palm == null || elbow == null)
            {
                Plugin.Verbose("The model has no right hand and elbow bones to photograph.");
                Hands[look] = null;
                return null;
            }

            Vector3 tip = palm.position;
            float length = Vector3.Distance(tip, elbow.position);
            if (length < 1e-3f)
            {
                Hands[look] = null;
                return null;
            }

            Vector3 down = (tip - elbow.position) / length;
            // From the body's middle out through the hand, square to the forearm.
            Vector3 middle = model.transform.position + Vector3.Project(tip - model.transform.position, model.transform.up);
            Vector3 outward = Vector3.ProjectOnPlane(tip - middle, down);
            if (outward.sqrMagnitude < 1e-6f)
            {
                outward = Vector3.ProjectOnPlane(model.transform.right, down);
            }

            outward.Normalize();
            // From past the fingertips to above the elbow, the hand at the foot of the picture.
            Vector3 bottom = tip + (down * length * 0.55f);
            Vector3 top = elbow.position - (down * length * 0.75f);
            const float distance = 3f;
            lens.position = ((bottom + top) / 2f) + (outward * distance);
            lens.rotation = Quaternion.LookRotation(-outward, -down);
            camera.orthographicSize = Vector3.Distance(bottom, top) / 2f;
            camera.nearClipPlane = distance - (length * 0.45f);
            camera.farClipPlane = distance + (length * 0.45f);
            camera.backgroundColor = HandKey;
            camera.aspect = HandWidth / (float)HandHeight;
            if (lamp != null)
            {
                lamp.transform.position = lens.position + (lens.up * length);
                lamp.enabled = true;
            }

            shot = RenderTexture.GetTemporary(HandWidth, HandHeight, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = shot;
            camera.Render();
            RenderTexture.active = shot;
            hand = new Texture2D(HandWidth, HandHeight, TextureFormat.RGBA32, false)
            {
                name = "BetterCoop.Hand",
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            hand.ReadPixels(new Rect(0, 0, HandWidth, HandHeight), 0, 0);
            if (!KeyOut(hand))
            {
                Plugin.Verbose("The hand was not in its picture.");
                Object.Destroy(hand);
                hand = null;
            }

            Hands[look] = hand;
            return hand;
        }
        catch (Exception exception)
        {
            if (hand != null)
            {
                Object.Destroy(hand);
            }

            Hands[look] = null;
            Guard.Report("PhotoStudio.TakeHand", exception);
            return null;
        }
        finally
        {
            RenderTexture.active = active;
            camera.targetTexture = null;
            if (shot != null)
            {
                RenderTexture.ReleaseTemporary(shot);
            }

            lens.position = lensPlace;
            lens.rotation = lensTurn;
            camera.orthographicSize = size;
            camera.nearClipPlane = near;
            camera.farClipPlane = far;
            camera.backgroundColor = Backdrop;
            if (lamp != null)
            {
                lamp.transform.position = lampPlace;
                lamp.enabled = false;
            }

            model.gameObject.SetActive(false);
        }
    }

    /// <summary>Makes the key colour clear, edges half clear; false when hardly anything but the key was there.</summary>
    private static bool KeyOut(Texture2D picture)
    {
        Color32[] pixels = picture.GetPixels32();
        int solid = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            // How much the pixel is the key's magenta: red and blue high, green low.
            float keyed = Mathf.Clamp01(Mathf.Min(c.r, c.b) - c.g);
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.8f, keyed));
            if (alpha <= 0.01f)
            {
                pixels[i] = new Color32(0, 0, 0, 0);
                continue;
            }

            // Take the key's share out of a half-keyed edge.
            float share = 1f - alpha;
            pixels[i] = new Color(
                Mathf.Clamp01((c.r - (share * HandKey.r)) / alpha),
                Mathf.Clamp01((c.g - (share * HandKey.g)) / alpha),
                Mathf.Clamp01((c.b - (share * HandKey.b)) / alpha),
                alpha);
            if (alpha > 0.5f)
            {
                solid++;
            }
        }

        picture.SetPixels32(pixels);
        picture.Apply(false);
        return solid > pixels.Length / 30;
    }

    /// <summary>Whether a photo of this look is ready.</summary>
    public static bool Has(string look) => Photos.TryGetValue(look, out RenderTexture? kept) && kept != null && kept.IsCreated();

    /// <summary>Lets a photo go, as when its scout has changed how they look.</summary>
    public static void Forget(string look)
    {
        if (Hands.TryGetValue(look, out Texture2D? hand))
        {
            Hands.Remove(look);
            if (hand != null)
            {
                Object.Destroy(hand);
            }
        }

        if (Photos.TryGetValue(look, out RenderTexture? photo))
        {
            Photos.Remove(look);
            if (photo != null)
            {
                photo.Release();
                Object.Destroy(photo);
            }
        }
    }

    /// <summary>Whether photos can be taken here: the studio is set up, or can be from the passport now.</summary>
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
        if (studio != null)
        {
            return true;
        }

        if (!broken && PassportManager.instance is PassportManager passport)
        {
            SetUp(passport);
        }

        return studio != null;
    }

    private static void SetUp(PassportManager passport)
    {
        if (studio != null || broken || passport.dummy == null || passport.dummyCamera == null)
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
            if (studio != null)
            {
                Object.Destroy(studio);
                studio = null;
            }

            Guard.Report("PhotoStudio.SetUp", exception);
        }
    }

    private static void Build(PlayerCustomizationDummy source, Camera sourceCamera)
    {
        // Where the camera sits from the scout when the passport shows the face.
        Transform? cameraParent = sourceCamera.transform.parent;
        Vector3 faceCamera = cameraParent != null ? cameraParent.TransformPoint(FacePlace) : FacePlace;
        Vector3 fromModel = source.transform.InverseTransformPoint(faceCamera);
        Quaternion turn = Quaternion.Inverse(source.transform.rotation) * sourceCamera.transform.rotation;

        studio = new GameObject("BetterCoop.PhotoStudio");
        Object.DontDestroyOnLoad(studio);
        studio.transform.position = StudioPlace;

        GameObject copy = Object.Instantiate(source.gameObject, studio.transform);
        copy.SetActive(false);
        copy.name = "Model";
        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = Quaternion.identity;
        copy.transform.localScale = source.transform.lossyScale;
        model = copy.GetComponent<PlayerCustomizationDummy>();
        // A camera that came along with the scout would keep filming for the passport.
        foreach (Camera stray in copy.GetComponentsInChildren<Camera>(true))
        {
            stray.enabled = false;
        }

        GameObject lens = Object.Instantiate(sourceCamera.gameObject, studio.transform);
        lens.name = "Camera";
        foreach (Transform child in lens.transform)
        {
            Object.Destroy(child.gameObject);
        }

        foreach (AudioListener listener in lens.GetComponents<AudioListener>())
        {
            Object.DestroyImmediate(listener);
        }

        lens.transform.position = copy.transform.TransformPoint(fromModel);
        lens.transform.rotation = copy.transform.rotation * turn;
        camera = lens.GetComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = FaceSize;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Backdrop;
        camera.targetTexture = null;

        // A lamp of its own, reaching nothing but the model this far from the world.
        var light = new GameObject("Lamp");
        light.transform.SetParent(studio.transform, false);
        light.transform.position = lens.transform.position + lens.transform.up * 0.5f;
        lamp = light.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.range = 6f;
        lamp.intensity = 1.5f;
        lamp.enabled = false;
        Plugin.Verbose("Set up the photo studio from the passport.");
    }
}

/// <summary>
/// Photos of the players in the room, dressed as they dress. A player who
/// changes how they look, as at the Airport's passport, gets a new photo and
/// the old one is let go.
/// </summary>
internal sealed class LivePortraits : IPortraits
{
    /// <summary>The look each player was last photographed in.</summary>
    private static readonly Dictionary<int, string> Taken = new();

    public Texture? Photo(int actor) => Take(actor);

    public Color Skin(int actor) => PhotoStudio.SkinColour(Data(actor)?.currentSkin ?? 0);

    public Texture? Hand(int actor)
    {
        // The photo first, so a changed look has already let the old hand go.
        Take(actor);
        return PhotonNetwork.CurrentRoom?.GetPlayer(actor) is Photon.Realtime.Player player && Look(actor) is string look
            ? PhotoStudio.TakeHand(look, model => model.UpdateDummy(player))
            : null;
    }

    /// <summary>Takes one photo a player in the room still lacks; false when every player has theirs.</summary>
    public static bool WarmOne()
    {
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
        if (PhotonNetwork.CurrentRoom?.GetPlayer(actor) is not Photon.Realtime.Player player
            || Look(actor) is not string look)
        {
            return null;
        }

        if (Taken.TryGetValue(actor, out string? before) && before != look)
        {
            PhotoStudio.Forget(before);
        }

        Taken[actor] = look;
        return PhotoStudio.Take(look, model => model.UpdateDummy(player));
    }

    /// <summary>Everything that shows in a player's photo, apart from who they are.</summary>
    private static string? Look(int actor) =>
        Data(actor) is CharacterCustomizationData d
            ? $"{actor}:{d.currentSkin}.{d.currentAccessory}.{d.currentEyes}.{d.currentMouth}.{d.currentOutfit}.{d.currentHat}.{d.currentSash}.{d.currentMedal}"
            : null;

    private static CharacterCustomizationData? Data(int actor)
    {
        try
        {
            return GameHandler.GetService<PersistentPlayerDataService>()?.GetPlayerData(actor)?.customizationData;
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
