using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// A picture of the Scoutmaster's right forearm and hand, for the hand that
/// takes an announced draft. His model is loaded from the game's prefab,
/// posed with the arm held out, and drawn once, far below the world, by a
/// camera and lamp of its own on a magenta ground that is keyed out after;
/// the picture is trimmed to the arm and everything else is thrown away.
/// Taken once; null when his model or its bones cannot be found or the arm
/// is not in the picture, and the drawn hand shows instead.
/// </summary>
internal static class ScoutmasterHand
{
    /// <summary>How wide the picture is taken, before it is trimmed to the arm.</summary>
    public const int Width = 256;

    /// <summary>How many of the picture's pixels a forearm's length takes, at first try.</summary>
    public const float ForearmPixels = Width / 2.2f;

    /// <summary>The Scoutmaster's prefab, as the game spawns him by name.</summary>
    private const string Prefab = "Character_Scoutmaster";

    /// <summary>His model inside the prefab: armature, skinned body, and animator.</summary>
    private const string Model = "ScoutMaster";

    private const float LampStrength = 1.4f;

    /// <summary>Far below the world, where nothing else stands.</summary>
    private static readonly Vector3 Stage = new(0f, -6000f, 0f);

    /// <summary>The colour behind the arm while it is drawn, keyed out after.</summary>
    private static readonly Color Key = new(1f, 0f, 1f, 1f);

    private static Texture2D? picture;

    private static bool taken;

    /// <summary>The picture, taken the first time it is asked for.</summary>
    public static Texture? Picture()
    {
        if (taken)
        {
            return picture;
        }

        taken = true;
        GameObject? stage = null;
        Light? lamp = null;
        Light? sun = RenderSettings.sun;
        RenderTexture? shot = null;
        RenderTexture? active = RenderTexture.active;
        try
        {
            stage = new GameObject("BetterCoop.HandStage");
            stage.transform.position = Stage;
            GameObject? body = Still(stage.transform);
            if (body == null)
            {
                Plugin.Log.LogWarning("The Scoutmaster's model could not be found to draw; his hand is drawn by hand instead.");
                return null;
            }

            body.SetActive(true);
            if (body.GetComponent<Animator>() is Animator animator)
            {
                // He stands as his animator first poses him, paused or not.
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.Update(0f);
            }

            // The game names the bones after the body parts.
            Transform? palm = HelperFunctions.FindChildRecursive(nameof(BodypartType.Hand_R), body.transform);
            Transform? elbow = HelperFunctions.FindChildRecursive(nameof(BodypartType.Elbow_R), body.transform);
            Transform? upper = HelperFunctions.FindChildRecursive(nameof(BodypartType.Arm_R), body.transform);
            if (palm == null || elbow == null || upper == null)
            {
                Plugin.Log.LogWarning("The Scoutmaster's model has no right hand, elbow, and arm bones; his hand is drawn by hand instead.");
                return null;
            }

            float forearm = Vector3.Distance(palm.position, elbow.position);
            if (forearm < 1e-3f)
            {
                Plugin.Log.LogWarning("The Scoutmaster's forearm has no length; his hand is drawn by hand instead.");
                return null;
            }

            // The arm held straight out to his side and a little down, whatever pose he stood in,
            // so nothing but the arm is in the picture.
            Vector3 up = body.transform.up;
            Vector3 front = body.transform.forward;
            Vector3 across = Vector3.Cross(up, front).normalized;
            float shoulderSide = Vector3.Dot(upper.position - body.transform.position, across);
            Vector3 side = across * (shoulderSide < 0f ? -1f : 1f);
            Vector3 reach = ((side * Mathf.Cos(25f * Mathf.Deg2Rad)) - (up * Mathf.Sin(25f * Mathf.Deg2Rad))).normalized;
            upper.rotation = Quaternion.FromToRotation(elbow.position - upper.position, reach) * upper.rotation;
            elbow.rotation = Quaternion.FromToRotation(palm.position - elbow.position, reach) * elbow.rotation;

            // Seen from the front, turned so the arm stands upright with the hand at the foot.
            Vector3 upright = -reach;
            int layer = body.layer;
            Camera camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(stage.transform, false);
            camera.enabled = false;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Key;
            camera.cullingMask = 1 << layer;
            camera.allowHDR = false;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 6f * forearm;

            // A lamp over the viewer's shoulder, lighting nothing but him, for the moment he is drawn.
            lamp = new GameObject("Lamp").AddComponent<Light>();
            lamp.transform.SetParent(stage.transform, false);
            lamp.type = LightType.Directional;
            lamp.intensity = LampStrength;
            lamp.color = Color.white;
            lamp.shadows = LightShadows.None;
            lamp.cullingMask = 1 << layer;
            lamp.transform.rotation = Quaternion.LookRotation(-((front * 1.5f) + upright + (side * 0.5f)).normalized, upright);
            lamp.enabled = false;

            for (float room = 1f; room <= 2.3f && picture == null; room *= 1.5f)
            {
                float below = forearm * 1.5f * room;
                float above = forearm * 0.4f;
                float tall = below + forearm + above;
                float wide = forearm * 2.2f * room;
                Vector3 middle = palm.position - (upright * below) + (upright * (tall / 2f));
                camera.transform.position = middle + (front * 3f * forearm);
                camera.transform.rotation = Quaternion.LookRotation(-front, upright);
                camera.orthographicSize = tall / 2f;
                int width = Width;
                int height = Mathf.RoundToInt(Width * tall / wide);
                camera.aspect = width / (float)height;

                if (shot != null)
                {
                    RenderTexture.ReleaseTemporary(shot);
                }

                shot = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
                camera.targetTexture = shot;
                lamp.enabled = true;
                RenderSettings.sun = lamp;
                camera.Render();
                RenderSettings.sun = sun;
                lamp.enabled = false;

                RenderTexture.active = shot;
                var whole = new Texture2D(width, height, TextureFormat.RGBA32, false);
                whole.ReadPixels(new Rect(0, 0, width, height), 0, 0);
#if DEBUG
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(BepInEx.Paths.CachePath, "BetterCoop-hand-raw.png"), whole.EncodeToPNG());
#endif
                Texture2D? trimmed = Crop(whole, out bool cut);
                Object.Destroy(whole);
                if (cut && trimmed != null && room * 1.5f <= 2.3f)
                {
                    // The hand ran off the picture: take it again with more room.
                    Object.Destroy(trimmed);
                    trimmed = null;
                }

                picture = trimmed;
            }

            camera.targetTexture = null;
            if (picture == null)
            {
                Plugin.Log.LogWarning("The Scoutmaster's hand was not in its picture; his hand is drawn by hand instead.");
            }
#if DEBUG
            else
            {
                string file = System.IO.Path.Combine(BepInEx.Paths.CachePath, "BetterCoop-hand.png");
                System.IO.File.WriteAllBytes(file, picture.EncodeToPNG());
                Plugin.Verbose($"Saved the hand's picture to {file}.");
            }
#endif

            return picture;
        }
        catch (Exception exception)
        {
            Guard.Report("ScoutmasterHand.Picture", exception);
            return null;
        }
        finally
        {
            RenderSettings.sun = sun;
            RenderTexture.active = active;
            if (shot != null)
            {
                RenderTexture.ReleaseTemporary(shot);
            }

            if (stage != null)
            {
                // Out of every camera's sight now; gone at the end of the frame.
                if (lamp != null)
                {
                    lamp.enabled = false;
                }

                stage.SetActive(false);
                Object.Destroy(stage);
            }
        }
    }

    /// <summary>
    /// A still copy of the Scoutmaster's model under <paramref name="stage"/>:
    /// only his armature, skinned body, and animator. It is copied under a
    /// sleeping holder and stripped of every script, joint, body, collider,
    /// and sound before it can wake, so none of the game's own Scoutmaster
    /// ever runs on it. Inactive until shown.
    /// </summary>
    private static GameObject? Still(Transform stage)
    {
        GameObject? prefab = Resources.Load<GameObject>(Prefab);
        Transform? visual = prefab != null ? prefab.transform.Find(Model) : null;
        if (visual == null)
        {
            return null;
        }

        var holder = new GameObject("Asleep");
        holder.SetActive(false);
        holder.transform.SetParent(stage, false);
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

        // One layer for the whole model, the one the camera and lamp see.
        int layer = copy.layer;
        foreach (Transform part in copy.GetComponentsInChildren<Transform>(true))
        {
            part.gameObject.layer = layer;
        }

        // Skinned bounds follow the arm once it is held out, so the camera never culls it.
        foreach (SkinnedMeshRenderer skin in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            skin.updateWhenOffscreen = true;
        }

        copy.SetActive(false);
        copy.transform.SetParent(stage, false);
        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = Quaternion.identity;
        // The prefab sizes his model; kept, he stands as tall as he does in the game.
        copy.transform.localScale = visual.localScale;
        Object.Destroy(holder);
        return copy;
    }

    /// <summary>
    /// The arm cut out of <paramref name="whole"/>: the key made clear and
    /// the picture trimmed to the arm at its sides and foot, its top kept,
    /// where the arm runs on. Null when hardly any arm is there;
    /// <paramref name="cut"/> says whether the arm ran off a side or the foot.
    /// </summary>
    private static Texture2D? Crop(Texture2D whole, out bool cut)
    {
        cut = false;
        if (!KeyOut(whole))
        {
            return null;
        }

        Color32[] pixels = whole.GetPixels32();
        int width = whole.width;
        int height = whole.height;
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
    private static bool KeyOut(Texture2D whole)
    {
        Color32[] pixels = whole.GetPixels32();
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
                Mathf.Clamp01((c.r - (share * Key.r)) / alpha),
                Mathf.Clamp01((c.g - (share * Key.g)) / alpha),
                Mathf.Clamp01((c.b - (share * Key.b)) / alpha),
                alpha);
            if (alpha > 0.5f)
            {
                solid++;
            }
        }

        whole.SetPixels32(pixels);
        whole.Apply(false);
        // A thin arm in a roomy picture still covers this much.
        return solid > pixels.Length / 200;
    }
}
