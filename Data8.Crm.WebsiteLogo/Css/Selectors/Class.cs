using System;
using System.Linq;
using HtmlAgilityPack;

namespace Data8.Crm.WebsiteLogo.Css.Selectors
{
    public class Class : SelectorPart
    {
        protected override bool IsMatchInternal(HtmlNode node)
        {
            var classNames = node.GetAttributeValue("class", "");
            if (String.IsNullOrEmpty(classNames))
                return false;

            var classes = classNames.Split(' ');
            return classes.Any(c => c.Equals(Value, StringComparison.OrdinalIgnoreCase));
        }

        protected override void UpdateSpecificity()
        {
            Specificity.Classes++;
        }

        protected override string ToStringInternal()
        {
            return "." + Value;
        }
    }
}
