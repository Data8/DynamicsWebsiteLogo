using System;
using System.Linq;
using HtmlAgilityPack;

namespace Data8.Crm.WebsiteLogo.Css
{
    public class Selector
    {
        private Specificity _specificity;

        public SelectorPart[] Parts { get; set; }

        public Specificity Specificity
        {
            get
            {
                if (_specificity == null)
                {
                    _specificity = new Specificity
                    {
                        Inline = Parts.Sum(p => p.Specificity.Inline),
                        Ids = Parts.Sum(p => p.Specificity.Ids),
                        Classes = Parts.Sum(p => p.Specificity.Classes),
                        Elements = Parts.Sum(p => p.Specificity.Elements)
                    };
                }

                return _specificity;
            }
        }

        internal bool IsMatch(HtmlNode node)
        {
            if (Parts[Parts.Length - 1] == null)
                return false;

            if (!Parts[Parts.Length - 1].IsMatch(node))
                return false;

            var parent = node.ParentNode;

            for (var i = Parts.Length - 2; i >= 0; i--)
            {
                if (Parts[i] == null)
                    return false;

                while (parent != null && !Parts[i].IsMatch(parent))
                    parent = parent.ParentNode;

                if (parent != null && i > 0)
                    parent = parent.ParentNode;
            }

            return parent != null;
        }

        public override string ToString()
        {
            return String.Join(" ", Parts.Select(p => p.ToString()));
        }
    }
}
