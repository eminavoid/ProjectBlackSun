using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Zeke.Tooltips
{
    [CreateAssetMenu(fileName = "Tooltip Links Database", menuName = "Tooltip/Links Database", order = 1)]
    public class TooltipLinksDB : ScriptableObject
    {
        [Serializable]
        public struct TooltipInfo
        {
            public string key;
            public string title;
            [Tooltip("Words that link to this entry when they appear in a text, e.g. Cleric, Clerics.")]
            public string[] terms;
            [Tooltip("Off for names that already carry their own color, like rival faiths.")]
            public bool recolor;
            [TextArea(3, 6)] public string description;
        }

        [SerializeField] private Color linkColor = new Color(0.55f, 0.16f, 0.07f);
        [SerializeField] private List<TooltipInfo> entries = new List<TooltipInfo>();

        private Dictionary<string, TooltipInfo> byKey;
        private Dictionary<string, TooltipInfo> byTerm;
        private Regex pattern;

        public bool TryGetValue(string key, out TooltipInfo toolTipInfo)
        {
            Build();
            return byKey.TryGetValue(key, out toolTipInfo);
        }

        public TooltipInfo this[string index]
        {
            get
            {
                Build();
                return byKey[index];
            }
        }

        public string Highlight(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            Build();
            if (pattern == null) return text;

            var linked = new HashSet<string>();
            int insideLink = 0;
            string color = ColorUtility.ToHtmlStringRGB(linkColor);
            return pattern.Replace(text, match =>
            {
                if (match.Groups[1].Success)
                {
                    string tag = match.Value.ToLowerInvariant();
                    if (tag.StartsWith("<link")) insideLink++;
                    else if (tag.StartsWith("</link")) insideLink = Mathf.Max(0, insideLink - 1);
                    return match.Value;
                }

                if (insideLink > 0 || !byTerm.TryGetValue(match.Value, out TooltipInfo info) || !linked.Add(info.key)) return match.Value;
                return info.recolor
                    ? $"<link=\"{info.key}\"><color=#{color}>{match.Value}</color></link>"
                    : $"<link=\"{info.key}\">{match.Value}</link>";
            });
        }

        private void OnValidate()
        {
            byKey = null;
            byTerm = null;
            pattern = null;
        }

        private void Build()
        {
            if (byKey != null) return;

            byKey = new Dictionary<string, TooltipInfo>();
            byTerm = new Dictionary<string, TooltipInfo>(StringComparer.OrdinalIgnoreCase);
            var terms = new List<string>();
            foreach (TooltipInfo entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.key)) continue;
                byKey[entry.key] = entry;
                if (entry.terms == null) continue;
                foreach (string term in entry.terms)
                {
                    if (string.IsNullOrWhiteSpace(term) || byTerm.ContainsKey(term)) continue;
                    byTerm[term] = entry;
                    terms.Add(Regex.Escape(term));
                }
            }

            if (terms.Count == 0) return;
            terms.Sort((a, b) => b.Length.CompareTo(a.Length));
            pattern = new Regex($"(<[^>]*>)|\\b(?:{string.Join("|", terms)})\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
