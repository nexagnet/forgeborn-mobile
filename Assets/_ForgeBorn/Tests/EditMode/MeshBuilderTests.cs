using System.Collections.Generic;
using ForgeBorn.Domain.Commands;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;
using ForgeBorn.Domain.Session;
using ForgeBorn.UnityLayer;
using NUnit.Framework;
using UnityEngine;

namespace ForgeBorn.Tests
{
    public class MeshBuilderTests
    {
        private static BladeProfile Blank()
        {
            return BladeFactory.CreateTapered(0.8, x => 0.025 - 0.010 * x / 0.8, x => 0.004 - 0.002 * x / 0.8, 192, 48);
        }

        /// <summary>Signed volume of a closed triangle mesh (divergence theorem). Positive means outward winding.</summary>
        private static double MeshVolume(Mesh m)
        {
            Vector3[] v = m.vertices;
            int[] t = m.triangles;
            double vol = 0.0;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                vol += Vector3.Dot(a, Vector3.Cross(b, c));
            }
            return vol / 6.0;
        }

        [Test]
        public void Mesh_HasNoNaN_AndMatchesDomainVolume()
        {
            BladeProfile p = Blank();
            var b = new BladeMeshBuilder(p);
            foreach (Vector3 v in b.Mesh.vertices)
                Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z));
            foreach (Vector3 n in b.Mesh.normals)
                Assert.IsFalse(float.IsNaN(n.x) || float.IsNaN(n.y) || float.IsNaN(n.z));

            double mv = MeshVolume(b.Mesh);
            Assert.Greater(mv, 0.0, "winding must be outward");
            Assert.AreEqual(p.VolumeM3(), mv, p.VolumeM3() * 0.01);
        }

        [Test]
        public void Mesh_FollowsGrinding_VolumeDropsWithDomain()
        {
            var session = new ForgeSession(Blank(), MaterialData.SteelBasicV1);
            var b = new BladeMeshBuilder(session.Profile);
            double before = MeshVolume(b.Mesh);

            // Press straight down on the blade face at x = 0.4 m for half a second.
            var samples = new List<GrindSample>();
            for (int i = 0; i < 50; i++)
                samples.Add(new GrindSample(new Vec3d(0.4, 0.0, -1.0), new Vec3d(0, 0, 1), 80.0, 0.01));
            GrindResult r = session.ApplyGrind(new GrindStrokeCommand(1, 0.0, AbrasiveWheel.Medium, 3000.0, samples));
            Assert.IsTrue(r.Accepted);
            Assert.Greater(r.RemovedVolumeM3, 0.0);

            b.Rebuild(r.FirstStation, r.LastStation);
            double after = MeshVolume(b.Mesh);
            Assert.Less(after, before);
            Assert.AreEqual(session.VolumeM3, after, session.VolumeM3 * 0.01);
        }
    }
}
