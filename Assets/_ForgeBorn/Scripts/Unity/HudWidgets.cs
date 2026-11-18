using UnityEngine;
using UnityEngine.UI;

namespace ForgeBorn.UnityLayer
{
    /// <summary>Builds the plain uGUI pieces (label, button) from code. Shared by the workshop HUD and the menu screens.</summary>
    internal static class HudWidgets
    {
        public static readonly Color ButtonIdle = new Color(0.25f, 0.27f, 0.32f, 0.95f);
        public static readonly Color ButtonDisabled = new Color(0.16f, 0.17f, 0.2f, 0.8f);

        public static Text MakeText(RectTransform parent, string name, int size, TextAnchor anchor, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Button placed by anchors (fractions of the parent). Returns its label; the Button is on label.transform.parent.</summary>
        public static Text MakeButton(RectTransform parent, string label, Vector2 aMin, Vector2 aMax, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = new Vector2(3f, 0f);
            rt.offsetMax = new Vector2(-3f, 0f);
            go.GetComponent<Image>().color = ButtonIdle;
            go.GetComponent<Button>().onClick.AddListener(onClick);
            Text t = MakeText(rt, "Label", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            t.text = label;
            return t;
        }

        public static void SetEnabled(Text buttonLabel, bool enabled)
        {
            Transform b = buttonLabel.transform.parent;
            b.GetComponent<Button>().interactable = enabled;
            b.GetComponent<Image>().color = enabled ? ButtonIdle : ButtonDisabled;
            buttonLabel.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }

        /// <summary>Small button pinned to the bottom-right of a panel (tutorial Next / Skip). slot 0 is rightmost.</summary>
        public static Text MakePanelButton(RectTransform panel, string label, float slot, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Tut_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(panel, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(110f, 40f);
            rt.anchoredPosition = new Vector2(-10f - slot * 120f, 8f);
            go.GetComponent<Image>().color = new Color(0.3f, 0.32f, 0.38f, 0.95f);
            go.GetComponent<Button>().onClick.AddListener(onClick);
            Text t = MakeText(rt, "Label", 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            t.text = label;
            return t;
        }
    }
}
