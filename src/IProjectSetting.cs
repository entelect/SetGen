using System.Collections.Generic;

namespace SetGen
{
    public interface IProjectSetting
    {
        string Name { get; set; }
        string DevValue { get; set; }
        string QaValue { get; set; }
        string StagingValue { get; set; }
        string LiveValue { get; set; }
        string DefaultValue { get; set; }
        List<EnvironmentMapping> OtherEnvironments { get; set; } 
    }
}