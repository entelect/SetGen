using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SetGen
{
    public class SettingsClass
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
        public string Directory { get; set; }
        public bool ExcludeAzure { get; set; }
    }
}
