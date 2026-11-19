using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ForgeBorn.UnityLayer
{
    /// <summary>
    /// Workshop set pieces built from primitives and CC0 PBR textures (ambientCG Metal038 and Bricks075A, Poly Haven boiler_room HDRI):
    /// brick wall lit by the furnace, an anvil under the blade, a brick furnace with a glowing mouth. The camera is perspective, so depth is layered along +Z
    /// behind the blade (blade at z = 0); positions and sizes are pre-scaled by depth so the screen layout matches the 2D design. Missing textures fall back to flat colours.
    /// </summary>
    public static class SandboxProps
    {
        /// <summary>Half-height (m) of the default landscape view at the blade plane; ForgeSandbox.ZoomSizes[0] uses this constant.</summary>
        public const float ViewHalfHeightM = 0.34f;

        private static readonly float ViewDistance = ViewHalfHeightM / Mathf.Tan(0.5f * SandboxEnvironment.FovDeg * Mathf.Deg2Rad);

        /// <summary>Scale that keeps a prop at depth z the same screen size/position it would have at the blade plane (default zoom).</summary>
        private static float DepthScale(float z) { return (ViewDistance + z) / ViewDistance; }

        private static Texture2D Tex(string name) => Resources.Load<Texture2D>("Art/" + name);

        /// <summary>Primitives come with a collider we never use. Destroy() is illegal in edit mode (the headless capture and EditMode tests).</summary>
        internal static void DropCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c == null) return;
            if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
        }

        private static Material PropMat(Shader sh, string set, Texture env, Color tint, float metal, float roughScale)
        {
            var m = new Material(sh);
            m.SetColor("_Tint", tint);
            m.SetFloat("_Metal", metal);
            m.SetFloat("_RoughScale", roughScale);
            Texture2D c = Tex(set + "_color"), n = Tex(set + "_normal"), r = Tex(set + "_rough");
            if (c != null) m.SetTexture("_MainTex", c);
            if (n != null) m.SetTexture("_BumpMap", n);
            if (r != null) m.SetTexture("_RoughTex", r);
            if (env != null) m.SetTexture("_EnvCube", env);
            return m;
        }

        private static GameObject Piece(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            DropCollider(go);
            go.transform.SetParent(parent, false);
            float f = DepthScale(pos.z);
            go.transform.position = new Vector3(pos.x * f, pos.y * f, pos.z);
            go.transform.localScale = new Vector3(scale.x * f, scale.y * f, scale.z);
            if (euler.HasValue) go.transform.rotation = Quaternion.Euler(euler.Value);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }


        /// <summary>Instantiates a Resources/Art/Models FBX. Unity mirrors X on import, so the model is turned 180 deg about Y: the horn and the
        /// furnace mouth then face +X / the camera as designed. Returns null (and logs nothing) when the model is missing.</summary>
        private static GameObject Place(Transform parent, string model, Vector3 pos, float scale, Shader sh, Texture env, Dictionary<string, Material> cache, float depthFactor)
        {
            var prefab = Resources.Load<GameObject>("Art/Models/" + model);
            if (prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, parent);
            go.name = model;
            go.transform.position = new Vector3(pos.x * depthFactor, pos.y * depthFactor, pos.z);
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = Vector3.one * (scale * depthFactor);
            StyleRenderers(go, sh, env, cache);
            return go;
        }

        /// <summary>The 140 mm grip and guard at the blade joint. Parent is the blade root, whose local +X runs along the blade, joint at x = 0.</summary>
        public static GameObject AttachHandle(Transform bladeRoot, Texture env)
        {
            Shader sh = Resources.Load<Shader>("ForgeBornProp");
            var prefab = Resources.Load<GameObject>("Art/Models/handle");
            if (sh == null || !sh.isSupported || prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, bladeRoot);
            go.name = "Handle";
            go.transform.localPosition = Vector3.zero;
            // 180 deg about Y puts the grip on -X (Unity mirrors X); 90 deg about X lays the 0.18 m guard across the blade width (Y).
            go.transform.localRotation = Quaternion.AngleAxis(90f, Vector3.right) * Quaternion.AngleAxis(180f, Vector3.up);
            StyleRenderers(go, sh, env, new Dictionary<string, Material>());
            return go;
        }

        private static void StyleRenderers(GameObject go, Shader sh, Texture env, Dictionary<string, Material> cache)
        {
            foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>())
            {
                Material[] src = mr.sharedMaterials;
                var dst = new Material[src.Length];
                for (int i = 0; i < src.Length; i++) dst[i] = SlotMaterial(src[i] != null ? src[i].name : "", sh, env, cache);
                mr.sharedMaterials = dst;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }

        private static Material _insideMat, _emberMat;
        private static readonly Color InsideGlow = new Color(0.9f, 0.26f, 0.04f), EmberGlow = new Color(1.5f, 0.42f, 0.06f);

        private static void ApplyGlow(float k)
        {
            if (_insideMat != null) _insideMat.SetColor("_Emission", InsideGlow * k);
            if (_emberMat != null) _emberMat.SetColor("_Emission", EmberGlow * k);
        }

        /// <summary>Furnace mouth brightness, 0..1 (banked fire to blade in the fire). Cheap: two material colours.</summary>
        public static void SetFurnaceGlow(float k) { ApplyGlow(Mathf.Clamp01(k)); }

        private static Material SlotMaterial(string slot, Shader sh, Texture env, Dictionary<string, Material> cache)
        {
            Material m;
            if (cache.TryGetValue(slot, out m)) return m;
            switch (slot)
            {
                case "Steel": m = PropMat(sh, "anvil", env, new Color(0.42f, 0.50f, 0.68f), 1f, 0.8f); m.SetVector("_Tiling", new Vector4(2f, 2f, 0f, 0f)); break;
                case "DarkIron": m = PropMat(sh, "anvil", env, new Color(0.20f, 0.23f, 0.30f), 0.9f, 1f); m.SetVector("_Tiling", new Vector4(2f, 2f, 0f, 0f)); break;
                case "Brick": m = PropMat(sh, "brick", env, new Color(0.34f, 0.30f, 0.30f), 0f, 1f); m.SetVector("_Tiling", new Vector4(3f, 3f, 0f, 0f)); break;
                case "FurnaceInside": m = new Material(sh); m.SetColor("_Tint", new Color(0.03f, 0.025f, 0.025f)); _insideMat = m; break; // glowing back wall: the front view cannot see the ember bed
                case "Ember": m = new Material(sh); m.SetColor("_Tint", Color.black); _emberMat = m; break;
                case "Leather": m = new Material(sh); m.SetColor("_Tint", new Color(0.22f, 0.12f, 0.07f)); break;
                default: m = new Material(sh); m.SetColor("_Tint", new Color(0.4f, 0.4f, 0.4f)); break;
            }
            m.name = "Prop_" + slot;
            if (m == _insideMat || m == _emberMat) ApplyGlow(1f);
            if (env != null) m.SetTexture("_EnvCube", env);
            cache[slot] = m;
            return m;
        }

        public static GameObject Build(Transform parent, Texture env)
        {
            Shader sh = Resources.Load<Shader>("ForgeBornProp");
            if (sh == null || !sh.isSupported) return null;
            var root = new GameObject("Props");
            root.transform.SetParent(parent, false);

            // Brick wall behind everything, warmed by the furnace on the left.
            Material wall = PropMat(sh, "brick", env, new Color(0.26f, 0.24f, 0.25f), 0f, 1f);
            wall.SetVector("_Tiling", new Vector4(22.4f, 14f, 0f, 0f)); // quad is 1.6 x 1.0 m: 14 repeats per metre keeps the bricks square
            wall.SetColor("_WallGlow", new Color(1.1f, 0.38f, 0.09f));
            float fw = DepthScale(2.5f);
            wall.SetVector("_GlowPos", new Vector4(-0.30f * fw, 0.07f * fw, 0f, 0f));
            wall.SetFloat("_GlowRadius", 0.45f * fw);
            Piece(root.transform, PrimitiveType.Quad, "Wall", new Vector3(0f, 0f, 2.5f), new Vector3(1.6f, 1.0f, 1f), wall);

            // Anvil and furnace are the FBX models from tools/blender/forge_models.py (Resources/Art/Models), scaled to the stylised view
            // framing: a real furnace is 1.5 m tall, the landscape view only 0.68 m. UV is in metres, so Tiling is repeats per metre.
            _insideMat = _emberMat = null;
            var cache = new Dictionary<string, Material>();
            float fa = DepthScale(0.5f);
            Place(root.transform, "anvil", new Vector3(0f, -0.335f, 0.5f), 0.75f, sh, env, cache, fa);
            float ff = DepthScale(1.2f);
            Place(root.transform, "furnace", new Vector3(-0.30f, -0.08f, 1.2f), 0.38f, sh, env, cache, ff);
            return root;
        }
    }
}
