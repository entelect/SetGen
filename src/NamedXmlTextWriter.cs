using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace SetGen
{
    public class NamedXmlTextWriter : XmlTextWriter
    {
        public string ProjectName { get; set; }
        public Environment Environment { get; set; }
        public string MappedFrom { get; set; }

        public NamedXmlTextWriter(string filename, Encoding encoding)
            : base(filename, encoding)
        {
            
        }
        public NamedXmlTextWriter(string filename, Encoding encoding, string projectName, Environment environment)
            : base(filename, encoding)
        {
            ProjectName = projectName;
            Environment = environment;
        }
    }
}
