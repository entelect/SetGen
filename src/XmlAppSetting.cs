using System.Collections.Generic;

namespace SetGen
{
    // represents the settings as read in from the xml document
    public class XmlAppSetting : IProjectSetting
    {
        public string Name { get; set; }
        public string DevValue { get; set; }
        public string QaValue { get; set; }
        public string StagingValue { get; set; }
        public string LiveValue { get; set; }
        public string DefaultValue { get; set; }
        public bool Autogenerate { get; set; }
        public string Type { get; set; }
        public string Delimiter { get; set; }
        public bool AzureExclude { get; set; }
        public bool AzureDefinitonExclude { get; set; }
        public List<XmlProjectSetting> ProjectSettings { get; set; }
        public XmlAppSetting()
        {
            ProjectSettings = new List<XmlProjectSetting>();
        }
        public List<EnvironmentMapping> OtherEnvironments { get; set; }
    }
}