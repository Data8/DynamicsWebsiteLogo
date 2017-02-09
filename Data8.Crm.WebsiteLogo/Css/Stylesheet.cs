using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Data8.Crm.WebsiteLogo.Css.Selectors;
using HtmlAgilityPack;

namespace Data8.Crm.WebsiteLogo.Css
{
    public class Stylesheet
    {
        public Stylesheet(string text)
        {
            Parse(text);
        }

        public Rule[] Rules { get; set; }

        public IDictionary<string,string> GetAppliedStyles(HtmlNode node)
        {
            var specificity = new Dictionary<string, Specificity>(StringComparer.OrdinalIgnoreCase);
            var styles = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rule in Rules)
            {
                foreach (var selector in rule.Selectors)
                {
                    if (selector.IsMatch(node))
                    {
                        foreach (var style in rule.Values)
                        {
                            var compoundStyle = new Dictionary<string, string>();
                            if (style.Key.Equals("background", StringComparison.OrdinalIgnoreCase))
                            {
                                var parts = style.Value.Split(' ');
                                foreach (var part in parts)
                                {
                                    if (IsColor(part))
                                        compoundStyle["background-color"] = part;
                                    else if (part.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
                                        compoundStyle["background-image"] = part;
                                }
                            }
                            else
                            {
                                compoundStyle.Add(style.Key, style.Value);
                            }

                            foreach (var subStyle in compoundStyle)
                            {
                                Specificity existingSpecificity;
                                if (!specificity.TryGetValue(subStyle.Key, out existingSpecificity) || existingSpecificity.CompareTo(selector.Specificity) < 0)
                                {
                                    specificity[subStyle.Key] = selector.Specificity;
                                    styles[subStyle.Key] = subStyle.Value;
                                }
                            }
                        }
                    }
                }
            }

            return styles;
        }

        private bool IsColor(string part)
        {
            if (part.StartsWith("#"))
                return true;

            if (part.StartsWith("rbg(", StringComparison.OrdinalIgnoreCase))
                return true;

            if (part.StartsWith("rbga(", StringComparison.OrdinalIgnoreCase))
                return true;

            if (part.StartsWith("hsl(", StringComparison.OrdinalIgnoreCase))
                return true;

            if (part.StartsWith("hsla(", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private void Parse(string text)
        {
            // Remove comments first.
            text = Regex.Replace(text, @"/\*([^*]|\*[^/])*\*/", "");

            // Identify each rule.
            var ruleMatches = Regex.Matches(text, @"(?<selector>[^{]+)\{(?<rules>[^}]*)\}");
            Rules = ruleMatches
                .OfType<Match>()
                .Select(ParseRule)
                .ToArray();
        }

        private Rule ParseRule(Match m)
        {
            var selector = m.Groups["selector"].Value;
            var rules = m.Groups["rules"].Value;

            var rule = new Rule {Selectors = ParseSelectors(selector)};
            ParseRules(rule, rules);

            return rule;
        }

        private void ParseRules(Rule rule, string text)
        {
            var rules = Regex.Matches(text, @"(?<name>[^}:]+):?(?<value>[^};]+);?").OfType<Match>();
            foreach (var match in rules)
                rule.Values[match.Groups["name"].Value.Trim()] = match.Groups["value"].Value.Trim();
        }

        private Selector[] ParseSelectors(string text)
        {
            return text
                .Split(',')
                .Select(s => ParseSelector(s.Trim()))
                .ToArray();
        }

        private Selector ParseSelector(string text)
        {
            var parts = text.Split(new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var selector = new Selector();
            selector.Parts = parts
                .Select(ParseSelectorPart)
                .ToArray();

            return selector;
        }

        private SelectorPart ParseSelectorPart(string text)
        {
            var classParts = Regex.Matches(text, "\\.([^ .#]+)")
                .OfType<Match>()
                .Select(m => new Class { Value = m.Groups[1].Value })
                .OfType<SelectorPart>();

            var idParts = Regex.Matches(text, "#([^ .#]+)")
                .OfType<Match>()
                .Select(m => new Id { Value = m.Groups[1].Value })
                .OfType<SelectorPart>();

            var nameParts = Regex.Matches(text, "^[^ .#]+")
                .OfType<Match>()
                .Select(m => new Name { Value = m.Value })
                .OfType<SelectorPart>();

            var allParts = classParts.Concat(idParts).Concat(nameParts);
            SelectorPart rootPart = null;
            SelectorPart currentPart = null;

            foreach (var part in allParts)
            {
                if (rootPart == null)
                    rootPart = part;
                else
                    currentPart.ChildSelector = part;

                currentPart = part;
            }

            return rootPart;
        }
    }
}
