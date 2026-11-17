using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;
using ForgeBorn.Domain.Session;

namespace ForgeBorn.Domain.Persistence
{
    /// <summary>
    /// Binary weapon snapshot: "FBWS", int schema, int idLen, id utf8, int S, int P, long version,
    /// double cumulativeRemovedKg, double[S] X, double[S*P*2] ring, byte[S] severed,
    /// schema 2 adds double[S] each of centerY, centerZ, twist, tempK, austenite, martensite, temperP, damage,
    /// then SHA-256(all preceding bytes). Schema 1 files still load (straight, ambient, annealed defaults).
    /// Untrusted input is size-bounded and checksum-verified before parsing.
    /// </summary>
    public static class WeaponSerializer
    {
        public const int SchemaVersion = 2;
        public const int MaxFileBytes = 64 * 1024 * 1024;
        public const int MaxStations = 4096;
        public const int MaxPerimeter = 256;
        private const int HashBytes = 32;
        private static readonly byte[] Magic = { (byte)'F', (byte)'B', (byte)'W', (byte)'S' };

        public static byte[] ToBytes(WeaponSnapshot snap)
        {
            BladeProfile p = snap.Profile;
            byte[] id = Encoding.UTF8.GetBytes(snap.MaterialId);
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
                {
                    w.Write(Magic);
                    w.Write(SchemaVersion);
                    w.Write(id.Length);
                    w.Write(id);
                    w.Write(p.StationCount);
                    w.Write(p.PerimeterCount);
                    w.Write(snap.Version);
                    w.Write(snap.CumulativeRemovedMassKg);
                    for (int i = 0; i < p.X.Length; i++) w.Write(p.X[i]);
                    for (int i = 0; i < p.Ring.Length; i++) w.Write(p.Ring[i]);
                    for (int i = 0; i < p.Severed.Length; i++) w.Write((byte)(p.Severed[i] ? 1 : 0));
                    WriteArray(w, p.CenterY);
                    WriteArray(w, p.CenterZ);
                    WriteArray(w, p.Twist);
                    WriteArray(w, p.TempK);
                    WriteArray(w, p.Austenite);
                    WriteArray(w, p.Martensite);
                    WriteArray(w, p.TemperP);
                    WriteArray(w, p.Damage);
                }
                byte[] body = ms.ToArray();
                byte[] hash;
                using (var sha = SHA256.Create()) hash = sha.ComputeHash(body);
                byte[] all = new byte[body.Length + HashBytes];
                Buffer.BlockCopy(body, 0, all, 0, body.Length);
                Buffer.BlockCopy(hash, 0, all, body.Length, HashBytes);
                return all;
            }
        }

        private static void WriteArray(BinaryWriter w, double[] a)
        {
            for (int i = 0; i < a.Length; i++) w.Write(a[i]);
        }

        private static double[] ReadArray(BinaryReader r, int n)
        {
            var a = new double[n];
            for (int i = 0; i < n; i++) a[i] = r.ReadDouble();
            return a;
        }

        public static bool TryFromBytes(byte[] data, out WeaponSnapshot snap, out string error)
        {
            snap = null;
            if (data == null) { error = "null data"; return false; }
            if (data.Length < 40 || data.Length > MaxFileBytes) { error = "bad size"; return false; }

            int bodyLen = data.Length - HashBytes;
            byte[] expected;
            using (var sha = SHA256.Create()) expected = sha.ComputeHash(data, 0, bodyLen);
            int diff = 0;
            for (int i = 0; i < HashBytes; i++) diff |= expected[i] ^ data[bodyLen + i];
            if (diff != 0) { error = "checksum mismatch"; return false; }

            try
            {
                using (var ms = new MemoryStream(data, 0, bodyLen, false))
                using (var r = new BinaryReader(ms, Encoding.UTF8))
                {
                    byte[] magic = r.ReadBytes(4);
                    for (int i = 0; i < 4; i++)
                        if (magic.Length != 4 || magic[i] != Magic[i]) { error = "bad magic"; return false; }
                    int schema = r.ReadInt32();
                    if (schema < 1 || schema > SchemaVersion) { error = "unsupported schema " + schema; return false; }
                    int idLen = r.ReadInt32();
                    if (idLen < 1 || idLen > 64) { error = "bad material id length"; return false; }
                    string materialId = Encoding.UTF8.GetString(r.ReadBytes(idLen));
                    int s = r.ReadInt32();
                    int pc = r.ReadInt32();
                    if (s < 2 || s > MaxStations || pc < 4 || pc > MaxPerimeter) { error = "bad dimensions"; return false; }
                    long version = r.ReadInt64();
                    double removed = r.ReadDouble();

                    long expectedLen = 4 + 4 + 4 + idLen + 4 + 4 + 8 + 8 + (long)s * 8 + (long)s * pc * 16 + s;
                    if (schema >= 2) expectedLen += (long)s * 8 * 8; // eight per-station double arrays
                    if (expectedLen != bodyLen) { error = "length mismatch"; return false; }

                    var x = new double[s];
                    var ring = new double[s * pc * 2];
                    var sev = new bool[s];
                    for (int i = 0; i < x.Length; i++) x[i] = r.ReadDouble();
                    for (int i = 0; i < ring.Length; i++) ring[i] = r.ReadDouble();
                    for (int i = 0; i < sev.Length; i++) sev[i] = r.ReadByte() != 0;

                    // Schema 1 has no section frames or metallurgical state: defaults (straight, untwisted, ambient, annealed).
                    double[] cy = null, cz = null, tw = null, tk = null, au = null, ma = null, tp = null, dm = null;
                    if (schema >= 2)
                    {
                        cy = ReadArray(r, s);
                        cz = ReadArray(r, s);
                        tw = ReadArray(r, s);
                        tk = ReadArray(r, s);
                        au = ReadArray(r, s);
                        ma = ReadArray(r, s);
                        tp = ReadArray(r, s);
                        dm = ReadArray(r, s);
                    }

                    if (MaterialData.ById(materialId) == null) { error = "unknown material " + materialId; return false; }
                    var profile = new BladeProfile(x, ring, sev, pc, cy, cz, tw, tk, au, ma, tp, dm);
                    string reason;
                    if (!profile.Validate(out reason)) { error = "invalid geometry: " + reason; return false; }
                    snap = new WeaponSnapshot(profile, materialId, version, removed);
                    error = null;
                    return true;
                }
            }
            catch (Exception e)
            {
                error = "parse error: " + e.Message;
                return false;
            }
        }
    }

    /// <summary>Atomic file IO: write temp, verify by reading back, then replace (keeping the previous version as .bak).</summary>
    public static class WeaponFile
    {
        public static void SaveAtomic(string path, WeaponSnapshot snap)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string tmp = path + ".tmp";
            File.WriteAllBytes(tmp, WeaponSerializer.ToBytes(snap));

            WeaponSnapshot check;
            string err;
            if (!WeaponSerializer.TryFromBytes(File.ReadAllBytes(tmp), out check, out err))
            {
                File.Delete(tmp);
                throw new IOException("save verification failed: " + err);
            }

            if (File.Exists(path)) File.Replace(tmp, path, path + ".bak");
            else File.Move(tmp, path);
        }

        /// <summary>Loads the main file; if corrupt or missing, falls back to the last valid .bak.</summary>
        public static bool TryLoadWithRecovery(string path, out WeaponSnapshot snap, out string error)
        {
            string e1 = TryLoad(path, out snap);
            if (e1 == null) { error = null; return true; }
            string e2 = TryLoad(path + ".bak", out snap);
            if (e2 == null) { error = null; return true; }
            error = "main: " + e1 + "; backup: " + e2;
            return false;
        }

        private static string TryLoad(string path, out WeaponSnapshot snap)
        {
            snap = null;
            if (!File.Exists(path)) return "missing";
            try
            {
                string err;
                return WeaponSerializer.TryFromBytes(File.ReadAllBytes(path), out snap, out err) ? null : err;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}
