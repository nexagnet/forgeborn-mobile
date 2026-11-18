using System;
using ForgeBorn.Domain.Orders;
using UnityEngine;
using UnityEngine.UI;
using static ForgeBorn.UnityLayer.HudWidgets;

namespace ForgeBorn.UnityLayer
{
    /// <summary>
    /// Upgrade shop: a full-screen dim layer (blocks the workshop behind it) with one row per upgrade. All rules live in the Domain
    /// (Career.Buy); this class only shows them. The career is fetched through a delegate because loading a save replaces the object.
    /// </summary>
    internal sealed class ShopScreen
    {
        private readonly Func<Career> _career;
        private readonly Action<string> _say;
        private readonly Action _bought;
        private GameObject _layer;
        private Text _title;
        private readonly Text[] _rowLabels = new Text[4];
        private readonly Text[] _rowInfo = new Text[4];

        public bool IsOpen { get { return _layer.activeSelf; } }

        public ShopScreen(RectTransform safe, Func<Career> career, Action<string> say, Action bought)
        {
            _career = career;
            _say = say;
            _bought = bought;
            RectTransform box = MenuScreens.Layer(safe, "Shop", "SHOP", 40, new Vector2(0.18f, 0.12f), new Vector2(0.82f, 0.88f), out _layer);
            _title = box.Find("Title").GetComponent<Text>();
            for (int i = 0; i < Shop.All.Count; i++)
            {
                Upgrade u = Shop.All[i];
                _rowLabels[i] = MakeButton(box, "", MenuScreens.RowMin(i, 0.04f), MenuScreens.RowMax(i, 0.44f), () => Buy(u));
                _rowInfo[i] = MakeText(box, "Info" + i, 20, TextAnchor.MiddleLeft, MenuScreens.RowMin(i, 0.46f), MenuScreens.RowMax(i, 0.96f), Vector2.zero, Vector2.zero);
                _rowInfo[i].color = new Color(0.8f, 0.8f, 0.8f);
            }
            MakeButton(box, "Close", MenuScreens.RowMin(4, 0.04f), MenuScreens.RowMax(4, 0.96f), Hide);
            _layer.SetActive(false);
        }

        public void Show() { Refresh(); _layer.SetActive(true); }

        public void Hide() { _layer.SetActive(false); }

        private void Buy(Upgrade u)
        {
            BuyResult r = _career().Buy(u);
            switch (r)
            {
                case BuyResult.Bought: _say("Bought: " + Shop.Name(u)); _bought(); break;
                case BuyResult.NotEnoughMoney: _say("Not enough money for " + Shop.Name(u)); break;
                case BuyResult.AlreadyOwned: _say("You already own " + Shop.Name(u)); break;
                default: _say("Deliver an order first to open the shop"); break;
            }
            Refresh();
        }

        private void Refresh()
        {
            Career c = _career();
            _title.text = "SHOP    Money " + c.Money;
            for (int i = 0; i < Shop.All.Count; i++)
            {
                Upgrade u = Shop.All[i];
                bool owned = c.Owns(u);
                _rowLabels[i].text = owned ? Shop.Name(u) + "  (owned)" : Shop.Name(u) + "  -  " + Shop.Cost(u);
                SetEnabled(_rowLabels[i], !owned && c.Money >= Shop.Cost(u));
                _rowInfo[i].text = Shop.Description(u);
            }
        }
    }
}
