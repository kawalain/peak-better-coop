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

    /// <summary>How wide the hand's picture is taken, before it is trimmed to the arm.</summary>
    public const int HandWidth = 256;

    /// <summary>How many of the hand picture's pixels a forearm's length takes, at first try.</summary>
    public const float HandForearmPixels = HandWidth / 2.2f;

    /// <summary>The colour behind a hand while it is taken, keyed out after.</summary>
    private static readonly Color HandKey = new(1f, 0f, 1f, 1f);

    /// <summary>The Scoutmaster's prefab, as the game spawns him by name.</summary>
    private const string ScoutmasterPrefab = "Character_Scoutmaster";

    /// <summary>His model inside the prefab: armature, skinned body, and animator.</summary>
    private const string ScoutmasterModel = "ScoutMaster";

    private static Texture2D? scoutmasterHand;

    private static bool handTaken;

    /// <summary>
    /// A picture of the Scoutmaster's right forearm and hand, held out and
    /// seen from the front with the hand at the foot, on a clear ground,
    /// trimmed to the arm. Taken once, from a still copy of his model; null
    /// when his model or its bones cannot be found or the arm is not in the
    /// picture.
    /// </summary>
    public static Texture? ScoutmasterHand()
    {
        if (handTaken)
        {
            return scoutmasterHand;
        }

        if (!Ready() || model == null || camera == null)
        {
            return null;
        }

        handTaken = true;
        GameObject? body = null;
        Transform lens = camera.transform;
        Vector3 lensPlace = lens.position;
        Quaternion lensTurn = lens.rotation;
        float size = camera.orthographicSize;
        float near = camera.nearClipPlane;
        float far = camera.farClipPlane;
        int seen = camera.cullingMask;
        Vector3 lampPlace = lamp != null ? lamp.transform.position : Vector3.zero;
        RenderTexture? shot = null;
        RenderTexture? active = RenderTexture.active;
        try
        {
            body = StillScoutmaster();
            if (body == null)
            {
                Plugin.Verbose("The Scoutmaster's model could not be found to photograph.");
                return null;
            }

            body.SetActive(true);
            if (body.GetComponent<Animator>() is Animator animator)
            {
                // He stands as his animator first poses him.
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.Update(0f);
            }

            // The game names the bones after the body parts.
            Transform? palm = HelperFunctions.FindChildRecursive(nameof(BodypartType.Hand_R), body.transform);
            Transform? elbow = HelperFunctions.FindChildRecursive(nameof(BodypartType.Elbow_R), body.transform);
            Transform? upper = HelperFunctions.FindChildRecursive(nameof(BodypartType.Arm_R), body.transform);
            if (palm == null || elbow == null || upper == null)
            {
                Plugin.Verbose("The Scoutmaster's model has no right hand, elbow, and arm bones to photograph.");
                return null;
            }

            float forearm = Vector3.Distance(palm.position, elbow.position);
            if (forearm < 1e-3f)
            {
                return null;
            }

            // Facing the passport's camera, as the passport's scout stands in the same place.
            Vector3 front = Vector3.ProjectOnPlane(lensPlace - body.transform.position, body.transform.up);
            front = front.sqrMagnitude > 1e-6f ? front.normalized : body.transform.forward;

            // The arm held straight out to its side and a little down, whatever pose he stood in,
            // so nothing but the arm is in the picture.
            Vector3 across = Vector3.Cross(body.transform.up, front).normalized;
            float shoulderSide = Vector3.Dot(upper.position - body.transform.position, across);
            Vector3 side = across * (shoulderSide < 0f ? -1f : 1f);
            Vector3 reach = ((side * Mathf.Cos(25f * Mathf.Deg2Rad)) - (body.transform.up * Mathf.Sin(25f * Mathf.Deg2Rad))).normalized;
            upper.rotation = Quaternion.FromToRotation(elbow.position - upper.position, reach) * upper.rotation;
            elbow.rotation = Quaternion.FromToRotation(palm.position - elbow.position, reach) * elbow.rotation;

            // Seen from the front, turned so the arm stands upright with the hand at the foot.
            Vector3 up = -reach;
            camera.cullingMask = 1 << body.layer;
            for (float room = 1f; room <= 2.3f && scoutmasterHand == null; room *= 1.5f)
            {
                float below = forearm * 1.5f * room;
                float above = forearm * 0.4f;
                float tall = below + forearm + above;
                float wide = forearm * 2.2f * room;
                Vector3 middle = palm.position - (up * below) + (up * (tall / 2f));
                lens.position = middle + (front * 3f * forearm);
                lens.rotation = Quaternion.LookRotation(-front, up);
                camera.orthographicSize = tall / 2f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 6f * forearm;
                camera.backgroundColor = HandKey;
                int width = HandWidth;
                int height = Mathf.RoundToInt(HandWidth * tall / wide);
                camera.aspect = width / (float)height;
                if (lamp != null)
                {
                    lamp.transform.position = lens.position + (up * forearm);
                    lamp.range = 10f * forearm;
                    lamp.enabled = true;
                }

                if (shot != null)
                {
                    RenderTexture.ReleaseTemporary(shot);
                }

                shot = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = shot;
                camera.Render();
                RenderTexture.active = shot;
                var whole = new Texture2D(width, height, TextureFormat.RGBA32, false);
                whole.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                Texture2D? taken = Crop(whole, out bool cut);
                Object.Destroy(whole);
                if (cut && taken != null && room * 1.5f <= 2.3f)
                {
                    // The hand ran off the picture: take it again with more room.
                    Object.Destroy(taken);
                    taken = null;
                }

                scoutmasterHand = taken;
            }

            if (scoutmasterHand == null)
            {
                Plugin.Verbose("The Scoutmaster's hand was not in its picture.");
            }
#if DEBUG
            else
            {
                string file = System.IO.Path.Combine(BepInEx.Paths.CachePath, "BetterCoop-hand.png");
                System.IO.File.WriteAllBytes(file, scoutmasterHand.EncodeToPNG());
                Plugin.Verbose($"Saved the hand's picture to {file}.");
            }
#endif

            return scoutmasterHand;
        }
        catch (Exception exception)
        {
            Guard.Report("PhotoStudio.ScoutmasterHand", exception);
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

            // The copy has done its work; nothing else needs it.
            if (body != null)
            {
                Object.Destroy(body);
            }

            lens.position = lensPlace;
            lens.rotation = lensTurn;
            camera.orthographicSize = size;
            camera.nearClipPlane = near;
            camera.farClipPlane = far;
            camera.cullingMask = seen;
            camera.backgroundColor = Backdrop;
            if (lamp != null)
            {
                lamp.transform.position = lampPlace;
                lamp.range = 6f;
                lamp.enabled = false;
            }
        }
    }

    /// <summary>
    /// A still copy of the Scoutmaster's model, standing where the passport's
    /// scout stands in the studio: only his armature, skinned body, and
    /// animator. It is copied under a sleeping holder and stripped of every
    /// script, joint, body, collider, and sound before it can wake, so none of
    /// the game's own Scoutmaster ever runs on it. Inactive until shown.
    /// </summary>
    private static GameObject? StillScoutmaster()
    {
        if (studio == null || model == null)
        {
            return null;
        }

        GameObject? prefab = Resources.Load<GameObject>(ScoutmasterPrefab);
        Transform? visual = prefab != null ? prefab.transform.Find(ScoutmasterModel) : null;
        if (visual == null)
        {
            return null;
        }

        var holder = new GameObject("Asleep");
        holder.SetActive(false);
        holder.transform.SetParent(studio.transform, false);
        GameObject copy = Object.Instantiate(visual.gameObject, holder.transform);
        foreach (Joint joint in copy.GetComponentsInChildren<Joint>(true))
        {
            Object.DestroyImmediate(joint);
        }

        MonoBehaviour[] scripts = copy.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = scripts.Length - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(scripts[i]);
        }

        foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(collider);
        }

        foreach (Rigidbody rigidbody in copy.GetComponentsInChildren<Rigidbody>(true))
        {
            Object.DestroyImmediate(rigidbody);
        }

        foreach (AudioSource sound in copy.GetComponentsInChildren<AudioSource>(true))
        {
            Object.DestroyImmediate(sound);
        }

        // Seen by the studio's camera as the passport's scout is.
        int layer = model.gameObject.layer;
        foreach (Transform part in copy.GetComponentsInChildren<Transform>(true))
        {
            part.gameObject.layer = layer;
        }

        copy.SetActive(false);
        copy.transform.SetParent(studio.transform, false);
        copy.transform.localPosition = model.transform.localPosition;
        copy.transform.localRotation = model.transform.localRotation;
        copy.transform.localScale = Vector3.one;
        Object.Destroy(holder);
        return copy;
    }

    /// <summary>
    /// The arm cut out of <paramref name="picture"/>: the key made clear and
    /// the picture trimmed to the arm at its sides and foot, its top kept,
    /// where the arm runs on. Null when hardly any arm is there;
    /// <paramref name="cut"/> says whether the arm ran off a side or the foot.
    /// </summary>
    private static Texture2D? Crop(Texture2D picture, out bool cut)
    {
        cut = false;
        if (!KeyOut(picture))
        {
            return null;
        }

        Color32[] pixels = picture.GetPixels32();
        int width = picture.width;
        int height = picture.height;
        int left = width;
        int right = -1;
        int foot = height;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (pixels[(y * width) + x].a > 24)
                {
                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                    foot = Mathf.Min(foot, y);
                }
            }
        }

        if (right < 0)
        {
            return null;
        }

        cut = left == 0 || right == width - 1 || foot == 0;
        const int margin = 3;
        left = Mathf.Max(0, left - margin);
        right = Mathf.Min(width - 1, right + margin);
        foot = Mathf.Max(0, foot - margin);
        int keptWidth = right - left + 1;
        int keptHeight = height - foot;
        var kept = new Color32[keptWidth * keptHeight];
        for (int y = 0; y < keptHeight; y++)
        {
            Array.Copy(pixels, ((y + foot) * width) + left, kept, y * keptWidth, keptWidth);
        }

        var trimmed = new Texture2D(keptWidth, keptHeight, TextureFormat.RGBA32, false)
        {
            name = "BetterCoop.Hand",
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        trimmed.SetPixels32(kept);
        trimmed.Apply(false);
        return trimmed;
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
