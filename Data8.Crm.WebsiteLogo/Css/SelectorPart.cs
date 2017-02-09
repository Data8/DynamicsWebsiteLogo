using HtmlAgilityPack;

namespace Data8.Crm.WebsiteLogo.Css
{
    public abstract class SelectorPart
    {
        private Specificity _specificity;

        public string Value { get; set; }

        public SelectorPart ChildSelector { get; set; }

        public bool IsMatch(HtmlNode node)
        {
            if (!IsMatchInternal(node))
                return false;

            if (ChildSelector == null || ChildSelector.IsMatch(node))
                return true;

            return false;
        }

        protected abstract bool IsMatchInternal(HtmlNode node);

        protected abstract void UpdateSpecificity();

        public Specificity Specificity
        {
            get
            {
                if (_specificity == null)
                {
                    _specificity = new Specificity();
                    if (ChildSelector != null)
                    {
                        _specificity.Inline = ChildSelector.Specificity.Inline;
                        _specificity.Ids = ChildSelector.Specificity.Ids;
                        _specificity.Classes = ChildSelector.Specificity.Classes;
                        _specificity.Elements = ChildSelector.Specificity.Elements;
                    }
                    UpdateSpecificity();
                }

                return _specificity;
            }
        }

        protected abstract string ToStringInternal();

        public override string ToString()
        {
            var s = ToStringInternal();

            if (ChildSelector != null)
                s += ChildSelector.ToStringInternal();

            return s;
        }
    }
}
