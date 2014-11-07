namespace SetGen
{
    // Represents and xml node which controlls:
    //  which standard configuration type (Dev, Qa,Live Staging) maps to the Azure configuration types (Cloud, Local)
    public class AzureMapping : MappingBase
    {
        public AzureMapping(string name, string target)
        {
            Name = name;
            Target = target;
        }
    }
}

