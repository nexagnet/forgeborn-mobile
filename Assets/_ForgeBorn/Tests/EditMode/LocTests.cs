using ForgeBorn.Domain.Evaluation;
using ForgeBorn.Domain.Orders;
using ForgeBorn.UnityLayer;
using NUnit.Framework;

namespace ForgeBorn.Tests
{
    public class LocTests
    {
        [TearDown]
        public void TearDown() { Loc.Current = Language.English; }

        [Test]
        public void English_ReturnsSourceUnchanged_AndFormatsTemplates()
        {
            Loc.Current = Language.English;
            Assert.AreEqual("Order", Loc.T("Order"));
            Assert.AreEqual("Delivered: grade A", Loc.F("Delivered: grade {0}", "A"));
        }

        [Test]
        public void Vietnamese_TranslatesKnownKeys_KeepsNumbers_AndFallsBackToEnglishForUnknown()
        {
            Loc.Current = Language.Vietnamese;
            Assert.AreEqual("Đơn", Loc.T("Order"));
            Assert.AreEqual("Đã giao: hạng B", Loc.F("Delivered: grade {0}", "B"));
            Assert.AreEqual("a string nobody translated", Loc.T("a string nobody translated"));
            Assert.AreEqual("", Loc.T(""));
            Assert.IsNull(Loc.T(null));
        }

        [Test]
        public void TranslatedTemplatesKeepTheSamePlaceholdersAsTheirSource()
        {
            Loc.Current = Language.Vietnamese;
            string[] templates =
            {
                "ORDER {0} for {1}\n{2}\nPays up to {3}, blank costs {4}. Needs score {5}+.",
                "COLD hammering at {0}: dent {1} mm, crack risk +{2}",
                "Edge is {0} deg and {1} mm thick 0.5 mm behind the tip; the client wants {2} deg and thin.",
                "Balance point is {0}% from the tang, the client wants about {1}%.",
                "DELIVERED  grade {0}  paid {1}  (net {2})",
                "Grade {0}   Score {1}   {2}",
            };
            foreach (string t in templates)
            {
                Assert.IsTrue(Loc.HasVietnamese(t), t);
                string vi = Loc.T(t);
                for (int n = 0; n < 6; n++)
                    Assert.AreEqual(t.Contains("{" + n + "}"), vi.Contains("{" + n + "}"), "placeholder {" + n + "} in: " + t);
            }
        }

        [Test]
        public void EveryVietnameseEntryKeepsThePlaceholdersOfItsSource()
        {
            foreach (var kv in Loc.VietnameseEntries)
                for (int n = 0; n < 8; n++)
                    Assert.AreEqual(kv.Key.Contains("{" + n + "}"), kv.Value.Contains("{" + n + "}"), "placeholder {" + n + "} in: " + kv.Key);
        }

        [Test]
        public void F_NeverThrowsOnABadTemplate()
        {
            Loc.Current = Language.English;
            Assert.DoesNotThrow(() => Loc.F("broken {0", "x"));
            Assert.DoesNotThrow(() => Loc.F("needs {0} and {1}", "x"));
        }

        [Test]
        public void CareerParse_ClampsNegativeMoneyToZero()
        {
            Assert.AreEqual(0, Career.Parse("career1;money=-500;best=").Money);
            Assert.AreEqual(120, Career.Parse("career1;money=120;best=").Money);
        }

        [Test]
        public void EveryTutorialStepHasAVietnameseTitleWhyAndAction()
        {
            var tut = new TutorialFlow();
            foreach (TutorialStep st in tut.Steps)
            {
                Assert.IsTrue(Loc.HasVietnamese(st.Title), "title: " + st.Title);
                Assert.IsTrue(Loc.HasVietnamese(st.Why), "why: " + st.Why);
                Assert.IsTrue(Loc.HasVietnamese(st.Action), "action: " + st.Action);
            }
            Assert.IsTrue(Loc.HasVietnamese(ForgeSandbox.HintGrind), "grind hint");
            Assert.IsTrue(Loc.HasVietnamese(ForgeSandbox.HintHammer), "hammer hint");
            Assert.IsTrue(Loc.HasVietnamese(ForgeSandbox.HintInspect), "view hint");
            Assert.IsTrue(Loc.HasVietnamese("Too cold to forge! Put it back in the Furnace."));
            Assert.IsTrue(Loc.HasVietnamese("The blade cracked. Press Order for a new blade and quench gentler next time."));
        }

        [Test]
        public void EveryOrderAndUpgradeHasVietnameseText()
        {
            foreach (Order o in OrderBook.All)
            {
                Assert.IsTrue(Loc.HasVietnamese(o.Client), o.Client);
                Assert.IsTrue(Loc.HasVietnamese(o.Title), o.Title);
                Assert.IsTrue(Loc.HasVietnamese(o.Brief), o.Brief);
            }
            foreach (Upgrade u in Shop.All)
            {
                Assert.IsTrue(Loc.HasVietnamese(Shop.Name(u)), Shop.Name(u));
                Assert.IsTrue(Loc.HasVietnamese(Shop.Description(u)), Shop.Description(u));
            }
        }

        [Test]
        public void EveryOrderResultReasonTemplateHasVietnameseText()
        {
            var weak = new BladeReport
            {
                MassKg = 2.0, LengthM = 0.8, BalanceFrac = 0.9, StraightnessDevM = 0.02, TwistRangeDeg = 20, MeanHrc = 20,
                HasBevel = false, Overall = 10, Grade = "F",
            };
            var beveled = weak;
            beveled.HasBevel = true;
            beveled.EdgeAngleDeg = 50;
            beveled.EdgeThickness05M = 0.003;
            foreach (Order o in OrderBook.All)
            {
                foreach (ReasonPart p in OrderBook.Settle(o, weak).Parts) Assert.IsTrue(Loc.HasVietnamese(p.Template), p.Template);
                foreach (ReasonPart p in OrderBook.Settle(o, beveled).Parts) Assert.IsTrue(Loc.HasVietnamese(p.Template), p.Template);
            }
            var cracked = new BladeReport { CrackedStations = 3 };
            var broken = new BladeReport { Broken = true };
            foreach (ReasonPart p in OrderBook.Settle(OrderBook.All[0], cracked).Parts) Assert.IsTrue(Loc.HasVietnamese(p.Template), p.Template);
            foreach (ReasonPart p in OrderBook.Settle(OrderBook.All[0], broken).Parts) Assert.IsTrue(Loc.HasVietnamese(p.Template), p.Template);
        }

        [Test]
        public void EveryAdviceTemplateHasVietnameseText()
        {
            // Reads the advisor source so a template added later cannot be forgotten: every `new Advice(AdviceLevel.X, "template"` must be translated.
            string src = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "_ForgeBorn/Scripts/Domain/Evaluation/ProcessAdvisor.cs"));
            var found = System.Text.RegularExpressions.Regex.Matches(src, @"AdviceLevel\.(?:Info|Warn|Danger|Good), ""([^""]+)""");
            Assert.GreaterOrEqual(found.Count, 15);
            foreach (System.Text.RegularExpressions.Match m in found) Assert.IsTrue(Loc.HasVietnamese(m.Groups[1].Value), m.Groups[1].Value);
        }

        [Test]
        public void EveryQuenchAndForecastStringHasVietnameseText()
        {
            foreach (string t in new[]
            {
                "Quench {0} K/s: too slow, it will not harden", "Quench {0} K/s: only partly hardening", "Quench {0} K/s: hardening fully",
                "Quench {0} K/s: CRACK RISK, take it out of the bath", "Temper {0} C: 10 min {1}, 1 h {2}, 4 h {3} HRC (aim {4})",
                "Forging heat: {0} C and up (blue ring = too cold)", "Cold metal: click again within 1.5 s to strike anyway.", "Next:", "Assist: {0}", "Light", "Strong", "Angle {0} deg", "Flip", "grind angle: {0} deg", "Blade turned over: other face up", "Blade turned back", "Undo",
            })
                Assert.IsTrue(Loc.HasVietnamese(t), t);
        }

        [Test]
        public void LanguageSetting_RoundTripsAndOldSettingsStayAutomatic()
        {
            var s = new GameSettings { Lang = 1 };
            Assert.AreEqual(1, GameSettings.Parse(s.Serialize()).Lang);
            Assert.AreEqual(-1, GameSettings.Parse("v1;res=1;fs=0;tier=-1;vol=1.00").Lang);
            Assert.AreEqual(Language.Vietnamese, new GameSettings().ResolveLanguage()); // automatic = Vietnamese
            Assert.AreEqual(Language.English, new GameSettings { Lang = 0 }.ResolveLanguage());
            Assert.AreEqual(Language.Vietnamese, new GameSettings { Lang = 1 }.ResolveLanguage());
            Assert.AreEqual(Language.Vietnamese, Loc.FromSystem(true));
            Assert.AreEqual(Language.English, Loc.FromSystem(false));
        }

        [Test]
        public void VietnameseDiacriticsAreInTheDefaultFont()
        {
            var font = UnityEngine.Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
            string all = "ẮằẳẵặẤầẩẫậÉèẻẽẹỀểỄệỈỉĩịỌỏỗộỚờởỡợỤủũụỨừửữựỲỷỹỵĐđƠơƯư";
            font.RequestCharactersInTexture(all);
            foreach (char c in all)
                Assert.IsTrue(font.HasCharacter(c), "missing glyph U+" + ((int)c).ToString("X4"));
        }
    }
}
