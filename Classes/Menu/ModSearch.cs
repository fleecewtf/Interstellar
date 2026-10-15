using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Interstellar.Classes.Menu
{
    public static class ModSearch
    {
        private static readonly Regex RichTextTags = new Regex("<[^>]*>", RegexOptions.Compiled);



        public static bool IsSearchable(ButtonInfo button) => button != null && !button.label
            && !button.excludeFromSearch && !string.IsNullOrWhiteSpace(button.buttonText);

        public static List<ButtonInfo> Search(IEnumerable<ButtonInfo> buttons, string query)
        {
            List<ButtonInfo> matches = new List<ButtonInfo>();
            if (buttons == null) return matches;
            string normalizedQuery = Normalize(query);
            HashSet<ButtonInfo> seen = new HashSet<ButtonInfo>();
            foreach (ButtonInfo button in buttons)
            {
                if (!IsSearchable(button) || !seen.Add(button)) continue;
                if (Matches(button.buttonText, normalizedQuery) || Matches(button.overlapText, normalizedQuery))
                {
                    matches.Add(button);
                    continue;
                }
                if (button.aliases == null) continue;
                foreach (string alias in button.aliases)
                {
                    if (!Matches(alias, normalizedQuery)) continue;
                    matches.Add(button);
                    break;
                }
            }
            return matches;
        }

        private static bool Matches(string value, string query) => value != null
            && Normalize(value).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string plain = RichTextTags.Replace(value, string.Empty);
            StringBuilder normalized = new StringBuilder(plain.Length);
            foreach (char character in plain)
                if (!char.IsWhiteSpace(character)) normalized.Append(character);
            return normalized.ToString();
        }
    }
}
