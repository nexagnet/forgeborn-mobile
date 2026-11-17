using UnityEngine;
using UnityEngine.Rendering;

namespace ForgeBorn.UnityLayer
{
    /// <summary>
    /// Workshop set pieces built from primitives and CC0 PBR textures (ambientCG Metal038 and Bricks075A, Poly Haven boiler_room HDRI):
    /// brick wall lit by the furnace, an anvil under the blade, a brick furnace with a glowing mouth. Static camera is orthographic,
    /// so depth is layered along +Z behind the blade (blade at z = 0). Missing textures fall back to flat colours.
    /// </summary>
    public static class SandboxProps
    {
        private static Texture2D Tex(string name) => Resources.Load<Texture2D>("Art/" + name);

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
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.rotation = Quaternion.Euler(euler.Value);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        public static GameObject Build(Transform parent, Texture env)
        {
            Shader sh = Resources.Load<Shader>("ForgeBornProp");
            if (sh == null || !sh.isSupported) return null;
            var root = new GameObject("Props");
            root.transform.SetParent(parent, false);

            // Brick wall behind everything, warmed by the furnace on the left.
            Material wall = PropMat(sh, "brick", env, new Color(0.26f, 0.24f, 0.25f), 0f, 1f);
            wall.SetVector("_Tiling", new Vector4(7f, 7f, 0f, 0f));
            wall.SetColor("_WallGlow", new Color(1.1f, 0.38f, 0.09f));
            wall.SetVector("_GlowPos", new Vector4(-0.3f, -0.15f, 0f, 0f));
            wall.SetFloat("_GlowRadius", 0.9f);
            Piece(root.transform, PrimitiveType.Quad, "Wall", new Vector3(0f, 0f, 2.5f), new Vector3(3f, 3f, 1f), wall);

            // Anvil: base, waist, face and horn, centred under the blade.
            Material steel = PropMat(sh, "anvil", env, new Color(0.42f, 0.50f, 0.68f), 1f, 0.8f);
            steel.SetVector("_Tiling", new Vector4(1.5f, 1.5f, 0f, 0f));
            steel.SetColor("_WallGlow", new Color(0.9f, 0.32f, 0.07f));
            steel.SetVector("_GlowPos", new Vector4(-0.3f, -0.15f, 0f, 0f));
            steel.SetFloat("_GlowRadius", 0.9f);
            const float z = 0.5f;
            Piece(root.transform, PrimitiveType.Cube, "AnvilBase", new Vector3(0f, -0.50f, z), new Vector3(0.28f, 0.16f, 0.2f), steel);
            Piece(root.transform, PrimitiveType.Cube, "AnvilWaist", new Vector3(0f, -0.38f, z), new Vector3(0.15f, 0.10f, 0.14f), steel);
            Piece(root.transform, PrimitiveType.Cube, "AnvilFace", new Vector3(0f, -0.305f, z), new Vector3(0.46f, 0.05f, 0.17f), steel);
            Piece(root.transform, PrimitiveType.Cylinder, "AnvilHorn", new Vector3(0.30f, -0.305f, z), new Vector3(0.05f, 0.08f, 0.05f), steel, new Vector3(0f, 0f, 90f));

            // Furnace: brick body with a dark arch and a glowing mouth (HDR emission feeds the bloom).
            Material brick = PropMat(sh, "brick", env, new Color(0.34f, 0.3f, 0.3f), 0f, 1f);
            brick.SetVector("_Tiling", new Vector4(3f, 4f, 0f, 0f));
            brick.SetColor("_WallGlow", new Color(1.3f, 0.45f, 0.1f));
            brick.SetVector("_GlowPos", new Vector4(-0.28f, -0.12f, 0f, 0f));
            brick.SetFloat("_GlowRadius", 0.5f);
            Piece(root.transform, PrimitiveType.Cube, "FurnaceBody", new Vector3(-0.30f, -0.14f, 1.2f), new Vector3(0.34f, 0.56f, 0.4f), brick);

            var dark = new Material(sh); dark.SetColor("_Tint", new Color(0.03f, 0.025f, 0.025f));
            Piece(root.transform, PrimitiveType.Quad, "FurnaceArch", new Vector3(-0.28f, -0.12f, 0.995f), new Vector3(0.20f, 0.17f, 1f), dark);
            var fire = new Material(sh);
            fire.SetColor("_Tint", Color.black);
            fire.SetColor("_Emission", new Color(1.5f, 0.42f, 0.06f));
            Piece(root.transform, PrimitiveType.Quad, "FurnaceMouth", new Vector3(-0.28f, -0.13f, 0.99f), new Vector3(0.15f, 0.11f, 1f), fire);
            return root;
        }
    }
}
