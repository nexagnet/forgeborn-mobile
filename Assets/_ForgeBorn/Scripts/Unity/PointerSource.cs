using UnityEngine;

namespace ForgeBorn.UnityLayer
{
    /// <summary>One pointer sample: whether the primary button/finger is held, where, and which pointer.</summary>
    public struct PointerState
    {
        public bool Down;
        public Vector2 Position;
        /// <summary>-1 for the mouse, finger id for touch (used by EventSystem UI hit tests).</summary>
        public int PointerId;
    }

    /// <summary>Abstracts mouse/touch so gameplay reads one pointer regardless of device.</summary>
    public interface IPointerSource
    {
        /// <summary>Returns true when this source is currently driving the pointer (even if released this frame).</summary>
        bool TryGet(out PointerState state);
    }

    public sealed class MousePointerSource : IPointerSource
    {
        public bool TryGet(out PointerState state)
        {
            state = new PointerState { Down = Input.GetMouseButton(0), Position = Input.mousePosition, PointerId = -1 };
            return true;
        }
    }

    public sealed class TouchPointerSource : IPointerSource
    {
        public bool TryGet(out PointerState state)
        {
            state = default;
            if (Input.touchCount == 0) return false;
            Touch t = Input.GetTouch(0);
            state = new PointerState
            {
                Down = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled,
                Position = t.position,
                PointerId = t.fingerId,
            };
            return true;
        }
    }

    /// <summary>Asks sources in order; the first that reports itself active wins.</summary>
    public sealed class CompositePointerSource : IPointerSource
    {
        private readonly IPointerSource[] _sources;

        public CompositePointerSource(params IPointerSource[] sources)
        {
            _sources = sources;
        }

        public bool TryGet(out PointerState state)
        {
            for (int i = 0; i < _sources.Length; i++)
                if (_sources[i].TryGet(out state)) return true;
            state = default;
            return false;
        }

        public static IPointerSource Default()
        {
            return new CompositePointerSource(new TouchPointerSource(), new MousePointerSource());
        }
    }
}
