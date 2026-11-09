using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// A picture of the Scoutmaster's right hand and wrist, for the hand that
/// takes an announced draft. His model is loaded from the game's prefab,
/// posed with the arm held out, and drawn once on the <see cref="ModelStage"/>
/// on a magenta ground that is keyed out after; the picture is trimmed to
/// the hand, fades out above the wrist, and the model is thrown away. Taken
/// once; null when his model or its bones cannot be found or the hand is not
/// in the picture, and the drawn hand shows instead.
/// </summary>
internal static class ScoutmasterHand
{
    /// <summary>How wide the picture is drawn, before it is trimmed to the hand.</summary>
    private const int Width = 1024;

    /// <summary>The Scoutmaster's prefab, as the game spawns him by name.</summary>
    private const string Prefab = "Character_Scoutmaster";

    /// <summary>His model inside the prefab: armature, skinned body, and animator.</summary>
    private const string Model = "ScoutMaster";

    /// <summary>How much of the forearm shows above the wrist, fading out, in forearms.</summary>
    private const float Wrist = 0.35f;

    /// <summary>The colour behind the hand while it is drawn, keyed out after.</summary>
    private static readonly Color Key = new(1f, 0f, 1f, 1f);

    private static Texture2D? picture;

    private static bool taken;

    /// <summary>How many of the picture's pixels lie from the fingertips up to the wrist.</summary>
    public static float HandPixels { get; private set; } = 1f;

    /// <summary>The picture, taken the first time it is asked for.</summary>
    public static Texture? Picture()
    {
        if (taken)
        {
            return picture;
        }

        taken = true;
        GameObject? body = null;
        try
        {
            body = Still(ModelStage.Root);
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
            // so nothing but the hand is in the picture.
            Vector3 up = body.transform.up;
            Vector3 front = body.transform.forward;
            Vector3 across = Vector3.Cross(up, front).normalized;
            float shoulderSide = Vector3.Dot(upper.position - body.transform.position, across);
            Vector3 side = across * (shoulderSide < 0f ? -1f : 1f);
            Vector3 reach = ((side * Mathf.Cos(25f * Mathf.Deg2Rad)) - (up * Mathf.Sin(25f * Mathf.Deg2Rad))).normalized;
            upper.rotation = Quaternion.FromToRotation(elbow.position - upper.position, reach) * upper.rotation;
            elbow.rotation = Quaternion.FromToRotation(palm.position - elbow.position, reach) * elbow.rotation;

            // Seen from the front, turned so the hand hangs at the foot with the wrist above it.
            Vector3 upright = -reach;
            Vector3 light = (-front - (0.6f * upright) - (0.3f * side)).normalized;
            for (float room = 1f; room <= 2.3f && picture == null; room *= 1.5f)
            {
                float below = forearm * 1.1f * room;
                float above = forearm * Wrist;
                float tall = below + above;
                float wide = forearm * 1.2f * room;
                Vector3 middle = palm.position - (upright * below) + (upright * (tall / 2f));
                int height = Mathf.RoundToInt(Width * tall / wide);
                var shot = new ModelStage.Shot(
                    middle + (front * 3f * forearm),
                    Quaternion.LookRotation(-front, upright),
                    tall / 2f,
                    0.01f,
                    6f * forearm,
                    Key,
                    light);
                Texture2D whole = ModelStage.Read(body, shot, Width, height);
#if DEBUG
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(BepInEx.Paths.CachePath, "BetterCoop-hand-raw.png"), whole.EncodeToPNG());
#endif
                float wristRow = height * below / tall;
                Texture2D? trimmed = Crop(whole, wristRow, Width / wide * forearm, out bool cut, out float hand);
                Object.Destroy(whole);
                if (cut && trimmed != null && room * 1.5f <= 2.3f)
                {
                    // The hand ran off the picture: take it again with more room.
                    Object.Destroy(trimmed);
                    trimmed = null;
                }

                picture = trimmed;
                HandPixels = Mathf.Max(1f, hand);
            }

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
            if (body != null)
            {
                // Out of every camera's sight now; gone at the end of the frame.
                body.SetActive(false);
                Object.Destroy(body);
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
    /// The hand cut out of <paramref name="whole"/>: the key made clear, the
    /// picture trimmed to the hand at its sides and foot, and the forearm
    /// above <paramref name="wristRow"/> faded out to the top. Null when hardly
    /// any hand is there; <paramref name="cut"/> says whether the hand ran off
    /// a side or the foot, and <paramref name="hand"/> how many rows lie from
    /// the trimmed foot up to the wrist.
    /// </summary>
    private static Texture2D? Crop(Texture2D whole, float wristRow, float forearmPixels, out bool cut, out float hand)
    {
        cut = false;
        hand = 0f;
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
        int margin = Mathf.Max(3, width / 128);
        left = Mathf.Max(0, left - margin);
        right = Mathf.Min(width - 1, right + margin);
        foot = Mathf.Max(0, foot - margin);
        int keptWidth = right - left + 1;
        int keptHeight = height - foot;
        hand = wristRow - foot;

        // Above the wrist the forearm fades out, so no cut edge shows where the picture ends.
        float fadeFrom = wristRow + (0.08f * forearmPixels);
        var kept = new Color32[keptWidth * keptHeight];
        for (int y = 0; y < keptHeight; y++)
        {
            int row = y + foot;
            float keep = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeFrom, height - 1, row));
            Array.Copy(pixels, (row * width) + left, kept, y * keptWidth, keptWidth);
            if (keep < 1f)
            {
                for (int x = 0; x < keptWidth; x++)
                {
                    int i = (y * keptWidth) + x;
                    kept[i].a = (byte)(kept[i].a * keep);
                }
            }
        }

        var trimmed = new Texture2D(keptWidth, keptHeight, TextureFormat.RGBA32, true)
        {
            name = "BetterCoop.Hand",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        trimmed.SetPixels32(kept);
        trimmed.Apply(true);
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
        // A hand in a roomy picture still covers this much.
        return solid > pixels.Length / 200;
    }
}
