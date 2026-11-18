using System;
using System.Collections.Generic;
using ForgeBorn.Domain.Evaluation;

namespace ForgeBorn.Domain.Orders
{
    /// <summary>A customer order: what the blade must be, what it pays and what the blank costs. Gameplay numbers, not engineering data.</summary>
    public sealed class Order
    {
        public string Id;
        public string Client;
        public string Title;
        public string Brief;
        /// <summary>1 = apprentice, 2 = journeyman, 3 = master.</summary>
        public int Tier;
        public BladeSpec Spec;
        /// <summary>Delivered blade must not weigh more than this (kg). Double.MaxValue = no limit.</summary>
        public double MaxMassKg = double.MaxValue;
        public long BasePay;
        public long BlankCost;
    }

    public sealed class OrderResult
    {
        public bool Delivered;
        public string Grade;
        public double Overall;
        public long Payout;
        /// <summary>Payout minus the blank cost. Negative when a blade is lost.</summary>
        public long Net;
        /// <summary>Plain reasons, worst first. Also filled for accepted blades that lost points.</summary>
        public List<string> Reasons = new List<string>();
    }

    public static class OrderBook
    {
        private static readonly Order[] Orders =
        {
            new Order
            {
                Id = "o1", Client = "Village guard", Title = "Plain utility blade", Tier = 1, BasePay = 100, BlankCost = 20,
                Brief = "Any honest edge will do. Keep it straight and do not crack it.",
                Spec = new BladeSpec { TargetEdgeAngleDeg = 25, EdgeAngleTolDeg = 15, TargetEdgeHrc = 55, HrcTol = 15, MaxStraightDevM = 0.006, MaxTwistDeg = 5, BalanceTol = 0.30, PassScore = 55 },
            },
            new Order
            {
                Id = "o2", Client = "Traveling merchant", Title = "Sharp working knife", Tier = 2, BasePay = 180, BlankCost = 30,
                Brief = "Needs a keen, hard edge. Harden it properly, then temper so it does not chip.",
                Spec = new BladeSpec { TargetEdgeAngleDeg = 20, EdgeAngleTolDeg = 10, TargetEdgeHrc = 58, HrcTol = 12, MaxStraightDevM = 0.004, MaxTwistDeg = 3, PassScore = 65 },
                MaxMassKg = 1.50,
            },
            new Order
            {
                Id = "o3", Client = "Master swordsman", Title = "Duelling blade", Tier = 3, BasePay = 320, BlankCost = 50,
                Brief = "Thin keen edge, near-perfect straightness, light in the hand.",
                Spec = new BladeSpec { TargetEdgeAngleDeg = 16, EdgeAngleTolDeg = 6, TargetEdgeHrc = 60, HrcTol = 8, MaxStraightDevM = 0.003, MaxTwistDeg = 2, BalanceTol = 0.15, PassScore = 75 },
                MaxMassKg = 1.45,
            },
        };

        public static IReadOnlyList<Order> All => Orders;

        public static Order ById(string id)
        {
            foreach (Order o in Orders) if (o.Id == id) return o;
            return null;
        }

        /// <summary>Pay multiplier for a delivered grade.</summary>
        public static double GradeFactor(string grade)
        {
            switch (grade)
            {
                case "S": return 1.6;
                case "A": return 1.3;
                case "B": return 1.0;
                case "C": return 0.7;
                case "D": return 0.4;
                default: return 0.0;
            }
        }

        /// <summary>Scores a finished blade against an order. Deterministic: same report, same result.</summary>
        public static OrderResult Settle(Order order, BladeReport r)
        {
            var res = new OrderResult { Grade = r.Grade, Overall = r.Overall, Net = -order.BlankCost };

            if (r.Broken) { res.Reasons.Add("The blade is cut through: lost."); return res; }
            if (r.CrackedStations > 0) { res.Reasons.Add("The blade cracked (" + r.CrackedStations + " stations): lost. Quench gentler or temper sooner."); return res; }

            AddWeakPoints(order, r, res.Reasons);

            if (r.MassKg > order.MaxMassKg)
            {
                res.Reasons.Insert(0, "Too heavy: " + (r.MassKg * 1000.0).ToString("F0") + " g, the client wants at most " + (order.MaxMassKg * 1000.0).ToString("F0") + " g.");
                return res;
            }
            if (r.Overall < order.Spec.PassScore)
            {
                res.Reasons.Insert(0, "Score " + r.Overall.ToString("F0") + " is below the " + order.Spec.PassScore.ToString("F0") + " this client requires.");
                return res;
            }

            res.Delivered = true;
            res.Payout = (long)Math.Round(order.BasePay * GradeFactor(r.Grade));
            res.Net = res.Payout - order.BlankCost;
            return res;
        }

        private static void AddWeakPoints(Order o, BladeReport r, List<string> reasons)
        {
            var list = new List<KeyValuePair<double, string>>();
            void Add(double score, string text) { if (score < 0.6) list.Add(new KeyValuePair<double, string>(score, text)); }

            if (!r.HasBevel) Add(0.0, "No edge ground: grind a bevel on the +Y side.");
            else Add(r.EdgeScore, "Edge is " + r.EdgeAngleDeg.ToString("F1") + " deg and " + (r.EdgeThickness05M * 1000.0).ToString("F2") + " mm thick 0.5 mm behind the tip; the client wants " + o.Spec.TargetEdgeAngleDeg.ToString("F0") + " deg and thin.");
            Add(r.HardnessScore, "Hardness is " + r.MeanHrc.ToString("F0") + " HRC, the client wants about " + o.Spec.TargetEdgeHrc.ToString("F0") + ".");
            Add(r.StraightnessScore, "Bent by " + (r.StraightnessDevM * 1000.0).ToString("F1") + " mm along its length.");
            Add(r.TwistScore, "Twisted by " + r.TwistRangeDeg.ToString("F1") + " deg.");
            Add(r.SymmetryScore, "Uneven faces: one side is thicker.");
            Add(r.ToughnessScore, "Too brittle: temper it to gain toughness.");
            Add(r.BalanceScore, "Balance point is " + (r.BalanceFrac * 100.0).ToString("F0") + "% from the tang, the client wants about " + (o.Spec.TargetBalanceFrac * 100.0).ToString("F0") + "%.");

            list.Sort((a, b) => a.Key.CompareTo(b.Key));
            foreach (var kv in list) reasons.Add(kv.Value);
        }
    }

    /// <summary>Money and best grades earned so far. In memory only: saving it is schema 3 (not done).</summary>
    public sealed class Career
    {
        public long Money;
        private readonly Dictionary<string, string> _best = new Dictionary<string, string>();

        public string BestGrade(string orderId) { string g; return _best.TryGetValue(orderId, out g) ? g : null; }

        public void Apply(Order order, OrderResult result)
        {
            Money += result.Net;
            if (!result.Delivered) return;
            string old = BestGrade(order.Id);
            if (old == null || Rank(result.Grade) > Rank(old)) _best[order.Id] = result.Grade;
        }

        /// <summary>The next tier opens after at least one delivered order of the tier below.</summary>
        public bool IsUnlocked(Order order)
        {
            if (order.Tier <= 1) return true;
            foreach (Order o in OrderBook.All)
                if (o.Tier == order.Tier - 1 && BestGrade(o.Id) != null) return true;
            return false;
        }

        private static int Rank(string g) { return Array.IndexOf(new[] { "F", "D", "C", "B", "A", "S" }, g); }
    }
}
