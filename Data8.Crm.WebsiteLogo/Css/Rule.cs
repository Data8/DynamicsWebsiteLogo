using System;
using System.Collections.Generic;
using System.Linq;

namespace Data8.Crm.WebsiteLogo.Css
{
    public class Rule
    {
        public Rule()
        {
            Values = new Dictionary<string, string>();
        }

        public Selector[] Selectors { get; set; }

        public IDictionary<string, string> Values { get; }

        public override string ToString()
        {
            return String.Join(", ", Selectors.Select(s => s.ToString())) + " { " + String.Join("; ", Values.Select(kvp => kvp.Key + ": " + kvp.Value)) + " }";
        }
    }
}
