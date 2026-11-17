using ForgeBorn.Domain.Geometry;
using UnityEngine;
using UnityEngine.Rendering;

namespace ForgeBorn.UnityLayer
{
    /// <summary>
    /// Projects a BladeProfile (the single source of truth) onto a Unity Mesh. The mesh is never read back by the Domain.
    /// Side vertices are shared per (station, ring index); end caps use their own vertices for flat normals.
    /// Positions are blade-local meters (x length, y width, z thickness).
    /// </summary>
    public sealed class BladeMeshBuilder
    {
        private readonly BladeProfile _p;
        private readonly int _s, _pc;
        private readonly Vector3[] _verts;
        private readonly Vector3[] _normals;
        private readonly int _capStart, _capEnd; // first vertex index of start / end cap (centre vertex first, then ring)

        public Mesh Mesh { get; }

        public BladeMeshBuilder(BladeProfile profile)
        {
            _p = profile;
            _s = profile.StationCount;
            _pc = profile.PerimeterCount;
            int side = _s * _pc;
            _capStart = side;
            _capEnd = side + _pc + 1;
            int total = side + 2 * (_pc + 1);
            _verts = new Vector3[total];
            _normals = new Vector3[total];

            Mesh = new Mesh { name = "BladeMesh" };
            Mesh.MarkDynamic();
            if (total > 65535) Mesh.indexFormat = IndexFormat.UInt32;

            BuildTriangles();
            Rebuild(0, _s - 1);
        }

        public int VertexCount => _verts.Length;

        private Vector3 Pos(int s, int j)
        {
            j %= _pc;
            if (j < 0) j += _pc;
            Vec2d v = _p.Point(s, j);
            return new Vector3((float)_p.X[s], (float)v.X, (float)v.Y);
        }

        private void BuildTriangles()
        {
            int sideQuads = (_s - 1) * _pc;
            int tris = sideQuads * 2 + 2 * _pc;
            var idx = new int[tris * 3];
            int k = 0;
            for (int s = 0; s < _s - 1; s++)
            {
                for (int j = 0; j < _pc; j++)
                {
                    int j1 = (j + 1) % _pc;
                    int a = s * _pc + j, b = s * _pc + j1;
                    int c = (s + 1) * _pc + j, d = (s + 1) * _pc + j1;
                    idx[k++] = a; idx[k++] = b; idx[k++] = c;
                    idx[k++] = b; idx[k++] = d; idx[k++] = c;
                }
            }
            for (int j = 0; j < _pc; j++)
            {
                int j1 = (j + 1) % _pc;
                // start cap faces -X
                idx[k++] = _capStart; idx[k++] = _capStart + 1 + j1; idx[k++] = _capStart + 1 + j;
                // end cap faces +X
                idx[k++] = _capEnd; idx[k++] = _capEnd + 1 + j; idx[k++] = _capEnd + 1 + j1;
            }
            Mesh.SetVertices(_verts);
            Mesh.SetIndices(idx, MeshTopology.Triangles, 0, false);
        }

        /// <summary>Recomputes stations in [first, last] (plus one neighbour each side for normals) and uploads.</summary>
        public void Rebuild(int first, int last)
        {
            int s0 = Mathf.Max(0, first - 1), s1 = Mathf.Min(_s - 1, last + 1);
            for (int s = s0; s <= s1; s++)
            {
                for (int j = 0; j < _pc; j++)
                {
                    int i = s * _pc + j;
                    _verts[i] = Pos(s, j);
                    Vector3 tr = Pos(s, j + 1) - Pos(s, j - 1);
                    Vector3 tx = Pos(Mathf.Min(s + 1, _s - 1), j) - Pos(Mathf.Max(s - 1, 0), j);
                    Vector3 n = Vector3.Cross(tr, tx);
                    _normals[i] = n.sqrMagnitude > 1e-20f ? n.normalized : Vector3.forward;
                }
            }
            if (s0 == 0) FillCap(0, _capStart, Vector3.left);
            if (s1 == _s - 1) FillCap(_s - 1, _capEnd, Vector3.right);

            Mesh.SetVertices(_verts);
            Mesh.SetNormals(_normals);
            Mesh.RecalculateBounds();
        }

        private void FillCap(int s, int start, Vector3 normal)
        {
            Vector3 centre = Vector3.zero;
            for (int j = 0; j < _pc; j++)
            {
                Vector3 p = Pos(s, j);
                _verts[start + 1 + j] = p;
                _normals[start + 1 + j] = normal;
                centre += p;
            }
            _verts[start] = centre / _pc;
            _normals[start] = normal;
        }
    }
}
