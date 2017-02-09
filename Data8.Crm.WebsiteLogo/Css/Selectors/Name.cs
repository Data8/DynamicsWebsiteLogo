using System;
using HtmlAgilityPack;

namespace Data8.Crm.WebsiteLogo.Css.Selectors
{
    public class Name : SelectorPart
    {
        protected override bool IsMatchInternal(HtmlNode node)
        {
            return node.Name.Equals(Value, StringComparison.OrdinalIgnoreCase);
        }

        protected override void UpdateSpecificity()
        {
            Specificity.Elements++;
        }

        protected override string ToStringInternal()
        {
            return Value;
        }
    }
}
