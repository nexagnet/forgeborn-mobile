using System;
using System.Collections.Generic;

namespace ForgeBorn.Domain.Orders
{
    public enum Upgrade { FineWheel, OilBath, PeenFace, HotFurnace }

    public enum BuyResult { Bought, ShopClosed, AlreadyOwned, NotEnoughMoney }

    /// <summary>The upgrade catalogue. Prices are gameplay numbers: the first order nets about 80 at grade B, so every upgrade is within three orders.</summary>
    public static class Shop
    {
        private static readonly Upgrade[] Items = { Upgrade.FineWheel, Upgrade.OilBath, Upgrade.PeenFace, Upgrade.HotFurnace };
        public static IReadOnlyList<Upgrade> All => Items;

        public static long Cost(Upgrade u)
        {
            switch (u)
            {
                case Upgrade.FineWheel: return 60;
                case Upgrade.OilBath: return 80;
                case Upgrade.PeenFace: return 90;
                default: return 120;
            }
        }

        public static string Name(Upgrade u)
        {
            switch (u)
            {
                case Upgrade.FineWheel: return "Fine grinding wheel";
                case Upgrade.OilBath: return "Oil quench bath";
                case Upgrade.PeenFace: return "Peen hammer face";
                default: return "Hotter furnace";
            }
        }

        public static string Description(Upgrade u)
        {
            switch (u)
            {
                case Upgrade.FineWheel: return "Removes little metal per pass: precise finishing.";
                case Upgrade.OilBath: return "Cools slower than water: less risk of cracking.";
                case Upgrade.PeenFace: return "Narrow hammer face that stretches metal along the blade.";
                default: return "Heats the blade faster.";
            }
        }

        /// <summary>Stable text id used in the save file.</summary>
        public static string Key(Upgrade u)
        {
            switch (u)
            {
                case Upgrade.FineWheel: return "fine_wheel";
                case Upgrade.OilBath: return "oil_bath";
                case Upgrade.PeenFace: return "peen_face";
                default: return "hot_furnace";
            }
        }

        public static bool TryParseKey(string key, out Upgrade u)
        {
            foreach (Upgrade x in Items) if (Key(x) == key) { u = x; return true; }
            u = Upgrade.FineWheel;
            return false;
        }
    }
}
