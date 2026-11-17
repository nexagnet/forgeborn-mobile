using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ForgeBorn.UnityLayer
{
    /// <summary>
    /// Workshop look for the sandbox, built in code: a procedural HDR reflection cubemap (softbox, furnace glow, cool window),
    /// trilight ambient, a backdrop wall with furnace glow, and a global post volume (bloom + ACES tonemapping, skipped on the Low tier).
    /// Everything is generated, no imported assets. Visual quality is judged from headless renders on a PC GPU only.
    /// </summary>
    public static class SandboxEnvironment
    {
        public const int CubeSize = 64;

        /// <summary>Vertical field of view of the sandbox camera (perspective). A phone-friendly, slightly telephoto 35 degrees.</summary>
        public const float FovDeg = 35f;
        /// <summary>Distance from the blade plane at which the view is 0.78 m half-height (the old orthographic framing).</summary>
        public static readonly float BaseDistance = 0.78f / Mathf.Tan(0.5f * FovDeg * Mathf.Deg2Rad);
        /// <summary>Sideways camera offset at base distance: gives a slight three-quarter view so depth and parallax read.</summary>
        public const float SideOffset = 0.16f;

        /// <summary>Camera pose for a framing half-height (m at the blade plane) and vertical look-at height.</summary>
        public static void PoseCamera(Transform cam, float halfHeightM, float lookY)
        {
            float dist = halfHeightM / Mathf.Tan(0.5f * FovDeg * Mathf.Deg2Rad);
            float side = SideOffset * (dist / BaseDistance);
            cam.position = new Vector3(side, lookY, -dist);
            cam.rotation = Quaternion.LookRotation(new Vector3(-side, 0f, dist), Vector3.up);
        }

        /// <summary>HDR radiance seen along a world direction. Pure function so it can be unit tested.</summary>
        public static Color Radiance(Vector3 d)
        {
            d.Normalize();
            // Dark workshop base with a slight vertical gradient.
            float up = Mathf.Clamp01(d.y * 0.5f + 0.5f);
            var c = new Color(0.035f + 0.03f * up, 0.035f + 0.03f * up, 0.045f + 0.04f * up);

            // Big overhead softbox (key reflection on the blade faces).
            float box = Smooth(0.45f, 0.85f, d.y) * (1f - Smooth(0.6f, 1.0f, Mathf.Abs(d.x)));
            c += new Color(5.5f, 5.3f, 5.0f) * box;

            // Behind the camera: broad warm-white strip so flat faces pick up a gradient.
            float behind = Smooth(0.2f, 0.9f, -d.z) * (1f - Smooth(0.2f, 0.8f, Mathf.Abs(d.y - 0.25f)));
            c += new Color(0.55f, 0.53f, 0.5f) * behind * (0.7f + 0.6f * Smooth(-0.6f, 0.9f, d.x));

            // Furnace glow from the left, low.
            float furnace = Smooth(0.25f, 0.9f, -d.x) * (1f - Smooth(0.1f, 0.7f, Mathf.Abs(d.y + 0.1f)));
            c += new Color(4.0f, 1.35f, 0.3f) * furnace;

            // Cool window from the right.
            float window = Smooth(0.5f, 0.95f, d.x) * (1f - Smooth(0.3f, 0.8f, Mathf.Abs(d.y - 0.15f)));
            c += new Color(0.55f, 0.75f, 1.2f) * window * 1.6f;
            c.a = 1f;
            return c;
        }

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        public static Cubemap BuildCubemap()
        {
            var cube = new Cubemap(CubeSize, TextureFormat.RGBAHalf, true) { name = "ForgeBornReflection" };
            var px = new Color[CubeSize * CubeSize];
            for (int f = 0; f < 6; f++)
            {
                for (int y = 0; y < CubeSize; y++)
                {
                    for (int x = 0; x < CubeSize; x++)
                    {
                        float u = (x + 0.5f) / CubeSize * 2f - 1f;
                        float v = (y + 0.5f) / CubeSize * 2f - 1f;
                        px[y * CubeSize + x] = Radiance(FaceDirection((CubemapFace)f, u, v));
                    }
                }
                cube.SetPixels(px, (CubemapFace)f);
            }
            cube.Apply(true, false);
            return cube;
        }

        private static Vector3 FaceDirection(CubemapFace face, float u, float v)
        {
            switch (face)
            {
                case CubemapFace.PositiveX: return new Vector3(1f, -v, -u);
                case CubemapFace.NegativeX: return new Vector3(-1f, -v, u);
                case CubemapFace.PositiveY: return new Vector3(u, 1f, v);
                case CubemapFace.NegativeY: return new Vector3(u, -1f, -v);
                case CubemapFace.PositiveZ: return new Vector3(u, -v, 1f);
                default: return new Vector3(-u, -v, -1f);
            }
        }

        /// <summary>Apply lighting, reflection, backdrop and post processing. Returns the created root object.</summary>
        public static GameObject Apply(Camera cam, QualityTier tier)
        {
            var root = new GameObject("Environment");

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.33f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.16f, 0.14f, 0.14f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.10f, 0.05f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            // Real HDRI (Poly Haven boiler_room, CC0) when imported as a cubemap, otherwise the procedural fallback.
            Texture env = Resources.Load<Cubemap>("Art/workshop");
            if (env == null) env = BuildCubemap();
            RenderSettings.customReflectionTexture = env;
            RenderSettings.reflectionIntensity = 1f;
            DynamicGI.UpdateEnvironment();

            // Backdrop wall behind the blade (camera looks along +Z from z = -2).
            Shader sh = Resources.Load<Shader>("ForgeBornBackdrop");
            if (sh != null && sh.isSupported)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Backdrop";
                Object.Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(root.transform, false);
                quad.transform.position = new Vector3(0f, 0f, 3f);
                quad.transform.localScale = new Vector3(14f, 14f, 1f);
                var mr = quad.GetComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(sh);
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            SandboxProps.Build(root.transform, env);
            if (tier != QualityTier.Low) AddPost(root, cam);
            return root;
        }

        private static void AddPost(GameObject root, Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.65f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var go = new GameObject("Post Volume");
            go.transform.SetParent(root.transform, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
        }
    }
}
