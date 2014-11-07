using System.Collections.Generic;

namespace SetGen
{
    // represents a project specific setting
    public class XmlProjectSetting : IProjectSetting
    {
        public string Name { get; set; }
        public string DevValue { get; set; }
        public string QaValue { get; set; }
        public string StagingValue { get; set; }
        public string LiveValue { get; set; }
        public string DefaultValue { get; set; }
        public List<EnvironmentMapping> OtherEnvironments { get; set; }
    }
}