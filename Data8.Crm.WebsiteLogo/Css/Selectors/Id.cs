using HtmlAgilityPack;

namespace Data8.Crm.WebsiteLogo.Css.Selectors
{
    public class Id : SelectorPart
    {
        protected override bool IsMatchInternal(HtmlNode node)
        {
            var id = node.GetAttributeValue("id", "");
            return id == Value;
        }

        protected override void UpdateSpecificity()
        {
            Specificity.Ids++;
        }

        protected override string ToStringInternal()
        {
            return "#" + Value;
        }
    }
}
