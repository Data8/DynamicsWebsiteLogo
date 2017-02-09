using System;

namespace Data8.Crm.WebsiteLogo.Css
{
    public class Specificity : IComparable<Specificity>
    {
        public int Inline { get; set; }

        public int Ids { get; set; }

        public int Classes { get; set; }

        public int Elements { get; set; }

        public int CompareTo(Specificity other)
        {
            var comparison = Inline.CompareTo(other.Inline);
            if (comparison != 0)
                return comparison;

            comparison = Ids.CompareTo(other.Ids);
            if (comparison != 0)
                return comparison;

            comparison = Classes.CompareTo(other.Classes);
            if (comparison != 0)
                return comparison;

            comparison = Elements.CompareTo(other.Elements);
            if (comparison != 0)
                return comparison;

            return 0;
        }
    }
}
