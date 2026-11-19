using UnityEngine;
using UnityEngine.UI;

namespace ForgeBorn.UnityLayer
{
    /// <summary>
    /// Visual aiming aid drawn in the HUD layer (no effect on the grind or hammer model): a ring that follows the mouse and turns green
    /// over the blade / red off it, and a guide line along the edge being ground. Images never block the pointer (raycastTarget off).
    /// </summary>
    internal sealed class AimAssist
    {
        public enum AimState { Miss, OnBlade, Hot, Cold }

        private static readonly Color Hit = new Color(0.35f, 1f, 0.45f, 0.95f), Miss = new Color(1f, 0.35f, 0.3f, 0.9f), Guide = new Color(1f, 0.85f, 0.3f, 0.55f),
            HotC = new Color(1f, 0.62f, 0.15f, 0.97f), ColdC = new Color(0.55f, 0.72f, 1f, 0.97f);

        private readonly RectTransform _area;
        private readonly Image _ring, _line;
        private static Sprite _ringSprite;

        public AimAssist(RectTransform area)
        {
            _area = area;
            _line = Make("AimGuide", null, new Vector2(10f, 3f));
            _line.color = Guide;
            _ring = Make("AimRing", RingSprite(), new Vector2(56f, 56f));
            Hide();
        }

        public void Hide()
        {
            _ring.gameObject.SetActive(false);
            _line.gameObject.SetActive(false);
        }

        /// <summary>Ring at a screen position (pixels): red off the blade, green on it; for hammering orange = hot enough to forge, blue = cold (a blow here can crack the blade).</summary>
        public void ShowRing(Vector2 screen, AimState state)
        {
            Vector2 local;
            if (!ToLocal(screen, out local)) { _ring.gameObject.SetActive(false); return; }
            _ring.gameObject.SetActive(true);
            _ring.rectTransform.anchoredPosition = local;
            _ring.color = state == AimState.Miss ? Miss : state == AimState.Hot ? HotC : state == AimState.Cold ? ColdC : Hit;
        }

        /// <summary>Thin line between two screen points (the edge to follow while grinding).</summary>
        public void ShowGuide(Vector2 a, Vector2 b)
        {
            Vector2 la, lb;
            if (!ToLocal(a, out la) || !ToLocal(b, out lb) || (lb - la).sqrMagnitude < 4f) { _line.gameObject.SetActive(false); return; }
            _line.gameObject.SetActive(true);
            Vector2 d = lb - la;
            RectTransform rt = _line.rectTransform;
            rt.anchoredPosition = (la + lb) * 0.5f;
            rt.sizeDelta = new Vector2(d.magnitude, 3f);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        /// <summary>Ring diameter in HUD pixels (the footprint of the wheel or hammer face).</summary>
        public void SetRingSize(float px) { _ring.rectTransform.sizeDelta = new Vector2(px, px); }

        public void HideGuide() { _line.gameObject.SetActive(false); }

        private bool ToLocal(Vector2 screen, out Vector2 local)
        {
            // Overlay canvas: no camera. Anchors are centred, so local is relative to the area's centre.
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screen, null, out local);
        }

        private Image Make(string name, Sprite sprite, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_area, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false; // otherwise IsPointerOverGameObject would swallow the grind/hammer input
            return img;
        }

        private static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;       // 0 centre .. 1 edge
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) / 0.10f);          // thin circle
                    float dot = Mathf.Clamp01(1f - r / 0.07f);                              // centre dot
                    float a = Mathf.Max(ring, dot);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _ringSprite;
        }
    }
}
