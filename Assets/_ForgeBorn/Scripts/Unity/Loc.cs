using System.Collections.Generic;
using System.Globalization;

namespace ForgeBorn.UnityLayer
{
    public enum Language { English = 0, Vietnamese = 1 }

    /// <summary>
    /// UI text lookup. Source strings are the English text itself: T("Order") returns the Vietnamese entry when the language is
    /// Vietnamese and the source string otherwise (so a missing translation shows English, never an empty label).
    /// F(template, args) translates the template and then formats it, so numbers stay outside the table.
    /// Pure (no UnityEngine), so it is unit tested. Language changes take effect for text built after the change.
    /// </summary>
    public static class Loc
    {
        public static Language Current = Language.English;

        private static Dictionary<string, string> _vi;

        private static Dictionary<string, string> Vi { get { return _vi ?? (_vi = LocVi.Build()); } }

        public static string T(string en)
        {
            if (Current != Language.Vietnamese || string.IsNullOrEmpty(en)) return en;
            string vi;
            return Vi.TryGetValue(en, out vi) ? vi : en;
        }

        public static string F(string template, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, T(template), args);
        }

        /// <summary>True if the table has a Vietnamese entry for this source string (used by completeness tests).</summary>
        public static bool HasVietnamese(string en) { return Vi.ContainsKey(en); }

        public static string LanguageName(Language l) { return l == Language.Vietnamese ? "Tiếng Việt" : "English"; }

        /// <summary>Default for a first launch: Vietnamese on a Vietnamese system, English otherwise.</summary>
        public static Language FromSystem(bool systemIsVietnamese) { return systemIsVietnamese ? Language.Vietnamese : Language.English; }
    }
}
