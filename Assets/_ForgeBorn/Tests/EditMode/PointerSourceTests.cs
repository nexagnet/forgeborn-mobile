using ForgeBorn.UnityLayer;
using NUnit.Framework;
using UnityEngine;

namespace ForgeBorn.Tests
{
    public class PointerSourceTests
    {
        private sealed class Fake : IPointerSource
        {
            public bool Active;
            public PointerState State;
            public bool TryGet(out PointerState s) { s = State; return Active; }
        }

        [Test]
        public void FirstActiveSourceWins()
        {
            var a = new Fake { Active = true, State = new PointerState { Down = true, Position = new Vector2(1, 2), PointerId = 7 } };
            var b = new Fake { Active = true, State = new PointerState { Down = true, Position = new Vector2(9, 9), PointerId = -1 } };
            var c = new CompositePointerSource(a, b);
            Assert.IsTrue(c.TryGet(out var s));
            Assert.AreEqual(7, s.PointerId);
            Assert.AreEqual(new Vector2(1, 2), s.Position);
        }

        [Test]
        public void FallsThroughInactiveSource()
        {
            var a = new Fake { Active = false };
            var b = new Fake { Active = true, State = new PointerState { Down = true, Position = new Vector2(3, 4), PointerId = -1 } };
            var c = new CompositePointerSource(a, b);
            Assert.IsTrue(c.TryGet(out var s));
            Assert.IsTrue(s.Down);
            Assert.AreEqual(-1, s.PointerId);
        }

        [Test]
        public void NoActiveSourceReportsUp()
        {
            var c = new CompositePointerSource(new Fake(), new Fake());
            Assert.IsFalse(c.TryGet(out var s));
            Assert.IsFalse(s.Down);
        }
    }
}
