using ForgeBorn.Domain.Orders;
using Xunit;

namespace ForgeBorn.Domain.Tests
{
    public class ShopTests
    {
        private static Career Deliverer(long extraMoney = 0)
        {
            var c = new Career();
            Order o1 = OrderBook.ById("o1");
            c.Apply(o1, new OrderResult { Delivered = true, Grade = "B", Payout = 100, Net = 80 });
            c.Money += extraMoney;
            return c;
        }

        [Fact]
        public void Shop_IsClosedUntilAnOrderWasDelivered()
        {
            var c = new Career { Money = 10000 };
            Assert.False(c.ShopOpen);
            Assert.Equal(BuyResult.ShopClosed, c.Buy(Upgrade.FineWheel));
            Assert.False(c.Owns(Upgrade.FineWheel));
            Assert.Equal(10000, c.Money);
        }

        [Fact]
        public void Buy_SpendsMoneyOnce_AndRefusesWhenPoorOrOwned()
        {
            Career c = Deliverer();
            long cost = Shop.Cost(Upgrade.FineWheel);
            Assert.True(c.Money >= cost);
            Assert.Equal(BuyResult.Bought, c.Buy(Upgrade.FineWheel));
            Assert.Equal(80 - cost, c.Money);
            Assert.True(c.Owns(Upgrade.FineWheel));
            Assert.Equal(BuyResult.AlreadyOwned, c.Buy(Upgrade.FineWheel));
            Assert.Equal(80 - cost, c.Money);

            Career poor = Deliverer();
            poor.Money = Shop.Cost(Upgrade.HotFurnace) - 1;
            Assert.Equal(BuyResult.NotEnoughMoney, poor.Buy(Upgrade.HotFurnace));
            Assert.False(poor.Owns(Upgrade.HotFurnace));
        }

        [Fact]
        public void EveryUpgradeHasPositiveCostNameAndIsAffordableWithinThreeOrders()
        {
            foreach (Upgrade u in Shop.All)
            {
                Assert.True(Shop.Cost(u) > 0);
                Assert.False(string.IsNullOrEmpty(Shop.Name(u)));
                Assert.False(string.IsNullOrEmpty(Shop.Description(u)));
                Assert.True(Shop.Cost(u) <= 3 * 80);
            }
        }

        [Fact]
        public void OwnedUpgrades_SurviveSerialization_AndBadNamesAreIgnored()
        {
            Career c = Deliverer(1000);
            c.Buy(Upgrade.OilBath);
            c.Buy(Upgrade.PeenFace);
            Career back = Career.Parse(c.Serialize());
            Assert.True(back.Owns(Upgrade.OilBath));
            Assert.True(back.Owns(Upgrade.PeenFace));
            Assert.False(back.Owns(Upgrade.HotFurnace));
            Assert.Equal(c.Serialize(), back.Serialize());

            Career odd = Career.Parse("career1;money=5;up=oil_bath,nonsense,,fine_wheel");
            Assert.True(odd.Owns(Upgrade.OilBath));
            Assert.True(odd.Owns(Upgrade.FineWheel));
            Assert.False(odd.Owns(Upgrade.PeenFace));
        }

        [Fact]
        public void OldCareerTextWithoutUpgrades_StillLoads()
        {
            Career c = Career.Parse("career1;money=90;best=o1:B");
            Assert.Equal(90, c.Money);
            Assert.True(c.ShopOpen);
            Assert.False(c.Owns(Upgrade.FineWheel));
        }
    }
}
