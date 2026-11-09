using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// A stage far below the world where a model is drawn for a moment, as the
/// passport photos and the Scoutmaster's hand are: an orthographic camera
/// and a lamp of its own, taken out for one draw and put away. Whatever
/// stands on it is drawn with nothing of the world round it, many samples a
/// pixel, and with smaller sizes made ahead so it stays smooth shown small.
/// </summary>
internal static class ModelStage
{
    private const int Samples = 8;

    private const float LampStrength = 1.4f;

    /// <summary>Far below the world, where nothing else stands.</summary>
    private static readonly Vector3 Place = new(0f, -6000f, 0f);

    /// <summary>The island's fog, which covers all outside a sphere around the party and so the stage too; off for a draw, as the game turns it off.</summary>
    private static readonly int FogEnabled = Shader.PropertyToID("FogEnabled");

    private static readonly int FogSphereSize = Shader.PropertyToID("_FogSphereSize");

    private const float NoFogSphere = 9999999f;

    private static GameObject? root;

    private static Camera? camera;

    private static Light? lamp;

    /// <summary>Where models stand to be drawn; kept across scenes.</summary>
    public static Transform Root
    {
        get
        {
            Ensure();
            return root!.transform;
        }
    }

    /// <summary>
    /// Draws <paramref name="subject"/>, active and standing on the stage, as
    /// <paramref name="shot"/> frames it, into a new texture of the given size
    /// with its smaller sizes made. The caller owns the texture.
    /// </summary>
    public static RenderTexture Draw(GameObject subject, in Shot shot, int width, int height)
    {
        var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
        {
            name = "BetterCoop.Drawn",
            useMipMap = true,
            autoGenerateMips = true,
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        target.Create();
        Render(subject, shot, target);
        return target;
    }

    /// <summary>Draws as <see cref="Draw"/> does, read back into a picture the caller can change and owns.</summary>
    public static Texture2D Read(GameObject subject, in Shot shot, int width, int height)
    {
        RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        RenderTexture? active = RenderTexture.active;
        try
        {
            Render(subject, shot, target);
            RenderTexture.active = target;
            var picture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            picture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            picture.Apply(false);
            return picture;
        }
        finally
        {
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    /// <summary>
    /// Draws into <paramref name="target"/> through a many-sampled texture of
    /// the same size, with the stage's lamp taken as the sun for the draw, so
    /// the lamp lights the subject however the scene's own sun stands.
    /// </summary>
    private static void Render(GameObject subject, in Shot shot, RenderTexture target)
    {
        Ensure();
        int seen = 0;
        foreach (Renderer part in subject.GetComponentsInChildren<Renderer>(true))
        {
            seen |= 1 << part.gameObject.layer;
        }

        Camera lens = camera!;
        Light light = lamp!;
        lens.cullingMask = seen;
        light.cullingMask = seen;
        lens.transform.SetPositionAndRotation(shot.Eye, shot.Turn);
        lens.orthographicSize = shot.HalfHeight;
        lens.nearClipPlane = shot.Near;
        lens.farClipPlane = shot.Far;
        lens.backgroundColor = shot.Ground;
        lens.aspect = target.width / (float)target.height;
        light.transform.rotation = Quaternion.LookRotation(shot.Light);

        var description = new RenderTextureDescriptor(target.width, target.height, RenderTextureFormat.ARGB32, 24)
        {
            sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
            useMipMap = false,
            autoGenerateMips = false,
            msaaSamples = Samples,
        };
        description.msaaSamples = Mathf.Clamp(SystemInfo.GetRenderTextureSupportedMSAASampleCount(description), 1, Samples);
        RenderTexture sampled = RenderTexture.GetTemporary(description);
        Light? sun = RenderSettings.sun;
        float fog = Shader.GetGlobalFloat(FogEnabled);
        float fogSphere = Shader.GetGlobalFloat(FogSphereSize);
        try
        {
            lens.targetTexture = sampled;
            light.enabled = true;
            RenderSettings.sun = light;
            Shader.SetGlobalFloat(FogEnabled, 0f);
            Shader.SetGlobalFloat(FogSphereSize, NoFogSphere);
            lens.Render();
            Graphics.Blit(sampled, target);
        }
        finally
        {
            Shader.SetGlobalFloat(FogEnabled, fog);
            Shader.SetGlobalFloat(FogSphereSize, fogSphere);
            RenderSettings.sun = sun;
            light.enabled = false;
            lens.targetTexture = null;
            RenderTexture.ReleaseTemporary(sampled);
        }
    }

    private static void Ensure()
    {
        if (root != null && camera != null && lamp != null)
        {
            return;
        }

        if (root != null)
        {
            Object.Destroy(root);
        }

        root = new GameObject("BetterCoop.ModelStage");
        Object.DontDestroyOnLoad(root);
        root.transform.position = Place;

        camera = new GameObject("Camera").AddComponent<Camera>();
        camera.transform.SetParent(root.transform, false);
        camera.enabled = false;
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.allowHDR = false;
        camera.allowMSAA = true;
        camera.useOcclusionCulling = false;

        lamp = new GameObject("Lamp").AddComponent<Light>();
        lamp.transform.SetParent(root.transform, false);
        lamp.type = LightType.Directional;
        lamp.intensity = LampStrength;
        lamp.color = Color.white;
        lamp.shadows = LightShadows.None;
        lamp.enabled = false;
    }

    /// <summary>How a draw is framed: where the camera stands and looks, how much it takes in, on what ground, and lit which way.</summary>
    public readonly struct Shot
    {
        public Shot(Vector3 eye, Quaternion turn, float halfHeight, float near, float far, Color ground, Vector3 light)
        {
            Eye = eye;
            Turn = turn;
            HalfHeight = halfHeight;
            Near = near;
            Far = far;
            Ground = ground;
            Light = light;
        }

        public Vector3 Eye { get; }

        public Quaternion Turn { get; }

        /// <summary>Half the height the camera takes in, in world units.</summary>
        public float HalfHeight { get; }

        public float Near { get; }

        public float Far { get; }

        /// <summary>The colour behind the subject.</summary>
        public Color Ground { get; }

        /// <summary>The way the lamp's light travels.</summary>
        public Vector3 Light { get; }
    }
}
