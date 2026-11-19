using UnityEngine;

namespace ForgeBorn.UnityLayer
{
    /// <summary>
    /// Which build is this? The launcher ForgeBorn.exe keeps its file time when the game is rebuilt, so the menu shows a stamp instead.
    /// The text comes from Resources/buildinfo.txt, written before every build by Editor/BuildStamp.cs
    /// (one line: "yyyy-MM-dd HH:mm | commit", with "+" after the commit when there were uncommitted changes).
    /// </summary>
    public static class BuildInfo
    {
        private static string _text;

        public static string Text
        {
            get
            {
                if (_text != null) return _text;
                TextAsset ta = Resources.Load<TextAsset>("buildinfo");
                _text = ta != null && !string.IsNullOrWhiteSpace(ta.text) ? ta.text.Trim() : "Editor";
                return _text;
            }
        }

        public static string Label { get { return Loc.T("Build") + ": " + Text; } }
    }
}
