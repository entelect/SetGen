using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SetGen
{
    public class EnvironmentMapping : MappingBase
    {
        public string Value { get; set; }

        public EnvironmentMapping(string name, string target)
        {
            Name = name;
            Target = target;
        }
    }
}
