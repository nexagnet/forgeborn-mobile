using ForgeBorn.Domain.Evaluation;
using ForgeBorn.Domain.Orders;
using Xunit;

namespace ForgeBorn.Domain.Tests
{
    public class OrderTests
    {
        private static BladeReport Good(string grade = "B", double overall = 80.0)
        {
            return new BladeReport
            {
                MassKg = 1.40, LengthM = 0.8, BalanceFrac = 0.4, StraightnessDevM = 0.001, StraightnessScore = 0.9, TwistRangeDeg = 0.5, TwistScore = 0.9,
                SymmetryScore = 0.9, HasBevel = true, EdgeAngleDeg = 20, EdgeThickness05M = 0.0012, EdgeScore = 0.9, MeanHrc = 58, HardnessScore = 0.95,
                ToughnessScore = 0.8, BalanceScore = 0.95, Overall = overall, Grade = grade, Pass = true,
            };
        }

        [Fact]
        public void BookHasThreeTiers_WithUniqueIds_AndSensibleEconomy()
        {
            Assert.Equal(3, OrderBook.All.Count);
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (Order o in OrderBook.All)
            {
                Assert.True(ids.Add(o.Id));
                Assert.True(o.BasePay > o.BlankCost, o.Id + ": an order must be profitable at grade B");
                Assert.NotNull(o.Spec);
            }
            Assert.True(OrderBook.All[0].Spec.PassScore < OrderBook.All[2].Spec.PassScore, "harder tiers demand more");
        }

        [Fact]
        public void DeliveredBlade_PaysByGrade_MinusBlankCost()
        {
            Order o = OrderBook.ById("o2");
            OrderResult r = OrderBook.Settle(o, Good("B", 80));
            Assert.True(r.Delivered);
            Assert.Equal(o.BasePay, r.Payout);
            Assert.Equal(o.BasePay - o.BlankCost, r.Net);
            Assert.True(OrderBook.Settle(o, Good("S", 95)).Payout > r.Payout);
        }

        [Fact]
        public void CrackedOrBrokenBlade_IsLost_WithAReason_AndPaysNothing()
        {
            Order o = OrderBook.ById("o1");
            BladeReport cracked = Good();
            cracked.CrackedStations = 3;
            OrderResult r = OrderBook.Settle(o, cracked);
            Assert.False(r.Delivered);
            Assert.Equal(0, r.Payout);
            Assert.Equal(-o.BlankCost, r.Net);
            Assert.Contains("cracked", r.Reasons[0]);

            BladeReport broken = Good();
            broken.Broken = true;
            Assert.Contains("cut through", OrderBook.Settle(o, broken).Reasons[0]);
        }

        [Fact]
        public void OverweightBlade_IsRejected_EvenWithAGoodScore()
        {
            Order o = OrderBook.ById("o3");
            BladeReport heavy = Good("A", 90);
            heavy.MassKg = o.MaxMassKg + 0.05;
            OrderResult r = OrderBook.Settle(o, heavy);
            Assert.False(r.Delivered);
            Assert.StartsWith("Too heavy", r.Reasons[0]);
        }

        [Fact]
        public void BelowTheClientsPassScore_IsRejected_AndWeakPointsAreNamedWorstFirst()
        {
            Order o = OrderBook.ById("o3");
            BladeReport weak = Good("D", 55);
            weak.Pass = false;
            weak.HardnessScore = 0.2; weak.MeanHrc = 40;
            weak.StraightnessScore = 0.5; weak.StraightnessDevM = 0.0015;
            OrderResult r = OrderBook.Settle(o, weak);
            Assert.False(r.Delivered);
            Assert.StartsWith("Score 55", r.Reasons[0]);
            Assert.Contains("Hardness is 40 HRC", r.Reasons[1]);
            Assert.Contains("Bent by", r.Reasons[2]);
        }

        [Fact]
        public void NoBevel_IsCalledOut()
        {
            BladeReport blank = Good("F", 30);
            blank.HasBevel = false;
            blank.EdgeScore = 0;
            OrderResult r = OrderBook.Settle(OrderBook.ById("o1"), blank);
            Assert.Contains(r.Reasons, s => s.StartsWith("No edge ground"));
        }

        [Fact]
        public void Career_TracksMoney_BestGrade_AndUnlocksNextTier()
        {
            var c = new Career();
            Order o1 = OrderBook.ById("o1"), o2 = OrderBook.ById("o2");
            Assert.True(c.IsUnlocked(o1));
            Assert.False(c.IsUnlocked(o2));

            c.Apply(o1, OrderBook.Settle(o1, Good("C", 70)));
            Assert.True(c.IsUnlocked(o2));
            Assert.Equal("C", c.BestGrade("o1"));

            c.Apply(o1, OrderBook.Settle(o1, Good("A", 88)));
            c.Apply(o1, OrderBook.Settle(o1, Good("D", 60)));
            Assert.Equal("A", c.BestGrade("o1"));

            long before = c.Money;
            BladeReport cracked = Good();
            cracked.CrackedStations = 1;
            c.Apply(o1, OrderBook.Settle(o1, cracked));
            Assert.Equal(before - o1.BlankCost, c.Money);
        }
    
        [Fact]
        public void Career_RoundTripsThroughText()
        {
            var c = new Career();
            Order o1 = OrderBook.ById("o1"), o2 = OrderBook.ById("o2");
            c.Apply(o1, OrderBook.Settle(o1, Good("A", 88)));
            c.Apply(o2, OrderBook.Settle(o2, Good("C", 70)));
            Career back = Career.Parse(c.Serialize());
            Assert.Equal(c.Money, back.Money);
            Assert.Equal("A", back.BestGrade("o1"));
            Assert.Equal("C", back.BestGrade("o2"));
            Assert.Null(back.BestGrade("o3"));
            Assert.Equal(c.Serialize(), back.Serialize());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("garbage")]
        [InlineData("career1;money=abc;best=o1:Z,:A,o2")]
        [InlineData("career9;money=5")]
        public void Career_ParseNeverThrowsAndFallsBackToEmpty(string text)
        {
            Career c = Career.Parse(text);
            Assert.NotNull(c);
            Assert.Equal(0, c.Money);
            Assert.Null(c.BestGrade("o1"));
        }

        [Fact]
        public void Career_ParseKeepsValidPartsAndIgnoresUnknownKeys()
        {
            Career c = Career.Parse("career1;money=250;future=1;best=o1:B,o2:Q,o3");
            Assert.Equal(250, c.Money);
            Assert.Equal("B", c.BestGrade("o1"));
            Assert.Null(c.BestGrade("o2"));
        }

        [Fact]
        public void ReasonParts_FormatToExactlyTheEnglishReasons_InTheSameOrder()
        {
            Order o = OrderBook.ById("o1");
            BladeReport r = Good("D", 40);
            r.MeanHrc = 40; r.HardnessScore = 0.1; r.StraightnessDevM = 0.01; r.StraightnessScore = 0.2; r.HasBevel = false;
            OrderResult res = OrderBook.Settle(o, r);
            Assert.True(res.Reasons.Count >= 3);
            Assert.Equal(res.Reasons.Count, res.Parts.Count);
            for (int i = 0; i < res.Reasons.Count; i++)
                Assert.Equal(res.Reasons[i], string.Format(System.Globalization.CultureInfo.InvariantCulture, res.Parts[i].Template, (object[])res.Parts[i].Args));
            Assert.Contains(res.Parts, p => p.Template.StartsWith("Hardness is {0}"));
        }
}
}
