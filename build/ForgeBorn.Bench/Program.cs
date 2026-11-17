using System;
using System.Collections.Generic;
using System.Diagnostics;
using ForgeBorn.Domain.Commands;
using ForgeBorn.Domain.Evaluation;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;
using ForgeBorn.Domain.Persistence;
using ForgeBorn.Domain.Session;

// Usage: dotnet run -c Release --project build/ForgeBorn.Bench
// Prints median / p95 / max milliseconds per operation. PC estimate only (see docs/BENCHMARKS.md).
internal static class Program
{
    private static long _id = 5000;
    private static long Id() { return ++_id; }

    // Assumed frame budget for a 60 fps phone game is 16.7 ms; a single per-input command should stay far below it on a PC
    // so that a phone several times slower still has headroom. This is an ESTIMATE threshold, not a phone measurement.
    private const double PcBudgetMs = 4.0;

    private static ForgeSession Fresh(int stations, int perimeter)
    {
        return new ForgeSession(BladeFactory.CreateBeveled(0.7, 0.05, 0.006, 0.0125, 0.001, stations, perimeter), MaterialData.SteelBasicV1);
    }

    private static int Main()
    {
        Console.WriteLine("ForgeBorn Domain microbenchmarks (PC estimate; NOT device performance)");
        Console.WriteLine(".NET " + Environment.Version + ", " + Environment.ProcessorCount + " logical cores");
        Console.WriteLine();
        Console.WriteLine("{0,-34} {1,6} {2,4} {3,10} {4,10} {5,10}  {6}", "operation", "S", "P", "median", "p95", "max", "budget");

        int over = 0;
        foreach (int[] cfg in new[] { new[] { 129, 32 }, new[] { 257, 48 }, new[] { 513, 64 } })
        {
            int S = cfg[0], P = cfg[1];
            over += Run("grind frame (fast swipe 20 mm/16 ms)", S, P, 60, true, () =>
            {
                var s = Fresh(S, P);
                var samples = new List<GrindSample>
                {
                    new GrindSample(new Vec3d(0.30, 0.0, 0.1), new Vec3d(0, 0, -1), 120.0, 0.016),
                    new GrindSample(new Vec3d(0.32, 0.0, 0.1), new Vec3d(0, 0, -1), 120.0, 0.016)
                };
                var cmd = new GrindStrokeCommand(Id(), 0, AbrasiveWheel.Coarse, 3000.0, samples);
                var sw = Stopwatch.StartNew();
                s.ApplyGrind(cmd);
                return sw.Elapsed.TotalMilliseconds;
            });

            over += Run("grind stroke (60 samples, 0.12 m)", S, P, 30, false, () =>
            {
                var s = Fresh(S, P);
                var samples = new List<GrindSample>();
                for (int i = 0; i < 60; i++)
                    samples.Add(new GrindSample(new Vec3d(0.2 + 0.002 * i, 0.0, 0.1), new Vec3d(0, 0, -1), 120.0, i == 0 ? 0.0 : 0.016));
                var cmd = new GrindStrokeCommand(Id(), 0, AbrasiveWheel.Coarse, 3000.0, samples);
                var sw = Stopwatch.StartNew();
                s.ApplyGrind(cmd);
                return sw.Elapsed.TotalMilliseconds;
            });

            over += Run("hammer blow", S, P, 60, true, () =>
            {
                var s = Fresh(S, P);
                for (int i = 0; i < s.Profile.StationCount; i++) s.Profile.TempK[i] = 1300.0;
                var cmd = new HammerStrikeCommand(Id(), 0, new Vec3d(0.35, 0.0, 0.1), new Vec3d(0, 0, -1), 100.0, HammerFace.Flat);
                var sw = Stopwatch.StartNew();
                s.ApplyHammer(cmd);
                return sw.Elapsed.TotalMilliseconds;
            });

            over += Run("thermal 10 s water quench", S, P, 10, false, () =>
            {
                var s = Fresh(S, P);
                for (int i = 0; i < s.Profile.StationCount; i++) s.Profile.TempK[i] = 1150.0;
                var cmd = new ThermalCommand(Id(), ThermalEnvironment.Water(), 10.0);
                var sw = Stopwatch.StartNew();
                s.ApplyThermal(cmd);
                return sw.Elapsed.TotalMilliseconds;
            });

            over += Run("thermal 1 h temper", S, P, 5, false, () =>
            {
                var s = Fresh(S, P);
                var cmd = new ThermalCommand(Id(), ThermalEnvironment.Oven(523.15), 3600.0);
                var sw = Stopwatch.StartNew();
                s.ApplyThermal(cmd);
                return sw.Elapsed.TotalMilliseconds;
            });

            over += Run("evaluate blade", S, P, 60, true, () =>
            {
                var s = Fresh(S, P);
                var sw = Stopwatch.StartNew();
                s.Evaluate(null);
                return sw.Elapsed.TotalMilliseconds;
            });

            over += Run("serialize + verify (save)", S, P, 30, false, () =>
            {
                var s = Fresh(S, P);
                var sw = Stopwatch.StartNew();
                byte[] b = WeaponSerializer.ToBytes(s.Capture());
                WeaponSnapshot snap;
                string err;
                WeaponSerializer.TryFromBytes(b, out snap, out err);
                return sw.Elapsed.TotalMilliseconds;
            });
            Console.WriteLine();
        }

        Console.WriteLine(over == 0
            ? "All budgeted operations are under the PC estimate budget of " + PcBudgetMs + " ms (median)."
            : over + " budgeted operation(s) exceed the PC estimate budget of " + PcBudgetMs + " ms (median).");
        Console.WriteLine("The budget applies to per-frame operations (grind frame, hammer blow, evaluate); strokes, thermal and save are informational.");
        return 0;
    }

    private static int Run(string name, int s, int p, int iterations, bool budgeted, Func<double> op)
    {
        for (int i = 0; i < 3; i++) op(); // warm-up (JIT)
        var times = new double[iterations];
        for (int i = 0; i < iterations; i++) times[i] = op();
        Array.Sort(times);
        double med = times[iterations / 2];
        double p95 = times[Math.Min(iterations - 1, (int)Math.Ceiling(iterations * 0.95) - 1)];
        double max = times[iterations - 1];
        bool bad = budgeted && med > PcBudgetMs;
        Console.WriteLine("{0,-34} {1,6} {2,4} {3,8:F2}ms {4,8:F2}ms {5,8:F2}ms  {6}", name, s, p, med, p95, max,
            budgeted ? (bad ? "OVER " + PcBudgetMs + " ms" : "ok") : "n/a (one-shot)");
        return bad ? 1 : 0;
    }
}
