using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Entelect.Extensions;
using Entelect.Types;

namespace SetGen
{
    public static class SettingsGenerator
    {
        public static void GenerateSettings(string filePath)
        {
            if(string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException("No file path specified, either set the GlobalSettingsFileLocation setting in the config file or pass it in as an argument\r\ne.g. SetGen.exe GlobalSettingsFileLocation=Resources\\GlobalSettings.xml");
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    string.Format("Global settings File not found at path \"{0}\"", Path.GetFullPath(filePath)), filePath);
            }
            XDocument document = XDocument.Load(filePath,LoadOptions.SetLineInfo|LoadOptions.PreserveWhitespace);
            Console.WriteLine("Getting projects & Azure modules from xml");
            IEnumerable<Project> projects = GetAzureModulesFromXml(document);
            Console.WriteLine("Getting Azure mappings from xml");
            IEnumerable<AzureMapping> mappings = GetAzureMappingsFromXml(document);
            Console.WriteLine("Getting App settings from xml");
            IEnumerable<XmlAppSetting> appSettings = GetAppSettingsFromXml(document);
            Console.WriteLine("Getting settings class settings from xml");
            SettingsClass settingsClassSettings = GetSettingsClassSettingsFromXml(document, "settingsClass");
            Console.WriteLine("Getting static settings class settings from xml");
            SettingsClass staticClassSettings = GetSettingsClassSettingsFromXml(document, "staticClass");
            Console.WriteLine("Generating Azure files");
            CreateAzureFiles(appSettings, projects, mappings);
            Console.WriteLine("Generating environment settings files");
            CreateEnvironmentSettings(appSettings, projects);
            Console.WriteLine("Generating settings class");
            CreateSettingsClass(appSettings, settingsClassSettings);
            Console.WriteLine("Generating static settings class");
            CreateStaticSettingsClass(appSettings, staticClassSettings, settingsClassSettings);
        }

        private static IEnumerable<EnvironmentMapping> GetEnvironmentMappings(XDocument document)
        {
            return document.Descendants("environmentMappings").Descendants("map").Select(
                m => new EnvironmentMapping(GetAttribute(m, "name", true), GetAttribute(m, "target", true)));
        }

        private static void CreateStaticSettingsClass(IEnumerable<XmlAppSetting> appSettings, SettingsClass staticClassSettings, SettingsClass settingsClassSettings)
        {
            //staticClassSettings
            if (staticClassSettings == null)
                return;
            if(settingsClassSettings == null)
                throw new ArgumentException("in order to use the static class, you must also include the <settingsClass> node in your xml");
            const string indentation = "    ";
            string doubleIndentation = string.Concat(indentation, indentation);
            string trippleIndentation = string.Concat(doubleIndentation, indentation);
            string quadrupleIndentation = string.Concat(doubleIndentation, doubleIndentation);
            string className = staticClassSettings.Name.Replace(".cs", "").CapitaliseFirstLetter();
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("using System;");
            stringBuilder.AppendLine("using System.Collections.Generic;");
            stringBuilder.AppendLine("using System.Linq;");
            stringBuilder.AppendLine("using Entelect.Settings;");
            if (!staticClassSettings.ExcludeAzure)
            {
                stringBuilder.AppendLine("using Entelect.Azure;");
                stringBuilder.AppendLine("using Microsoft.WindowsAzure.ServiceRuntime;");
            }
            
            stringBuilder.AppendLineFormat("using {0};", settingsClassSettings.Namespace);
            stringBuilder.AppendLineFormat("namespace {0}", staticClassSettings.Namespace);
            stringBuilder.AppendLine("{");
            stringBuilder.AppendLineFormat("{0}public static partial class {1}", indentation, className);
            stringBuilder.AppendLineFormat("{0}{{", indentation);
            stringBuilder.AppendLineFormat("{0}public static ISettingsSource Settings {{ get; set; }}", doubleIndentation).AppendLine();
            BuildStaticConstructor(doubleIndentation, stringBuilder, className, trippleIndentation, quadrupleIndentation, staticClassSettings.ExcludeAzure);
            //Add environment property
            stringBuilder.AppendLineFormat("{0}public static DeploymentEnvironment Environment {{ get {{ return Settings.Environment; }} }}", doubleIndentation).AppendLine();
            //Add get typed setting property
            BuildUntypedGetSetting(doubleIndentation, stringBuilder, trippleIndentation);
            BuildTypedGetSetting(doubleIndentation, stringBuilder, trippleIndentation);
            foreach (var xmlAppSetting in appSettings)
            {
                if (xmlAppSetting.Autogenerate == false)
                    continue;
                GetAppSettingProperty(stringBuilder, xmlAppSetting, settingsClassSettings);
            }
            stringBuilder.AppendLineFormat("{0}}}", indentation);
            stringBuilder.AppendLine("}");
            string filePath = Path.Combine(staticClassSettings.Directory, staticClassSettings.Name);
            if (!Directory.Exists(staticClassSettings.Directory))
                Directory.CreateDirectory(staticClassSettings.Directory);
            using (StreamWriter streamWriter = new StreamWriter(filePath))
            {
                streamWriter.Write(stringBuilder);
            }
        }

        private static void GetAppSettingProperty(StringBuilder stringBuilder, XmlAppSetting xmlAppSetting, SettingsClass settingsClassSettings)
        {
            const string indentation = "    ";
            string doubleIndentation = string.Concat(indentation, indentation);
            string trippleIndentation = string.Concat(doubleIndentation, indentation);
            string quadrupleIndentation = string.Concat(doubleIndentation, doubleIndentation);
            string escapedSettingName = xmlAppSetting.Name.Replace(":", "").Replace(".", "").CapitaliseFirstLetter();

            stringBuilder.AppendLineFormat("{0}public static {1} {2} {{ get {{ return GetSetting({3}.{2}); }} }}", doubleIndentation, xmlAppSetting.Type, escapedSettingName, settingsClassSettings.Name.Replace(".cs", ""));
            stringBuilder.AppendLine();
        }

        private static void BuildUntypedGetSetting(string doubleIndentation, StringBuilder stringBuilder, string trippleIndentation)
        {
            stringBuilder.AppendLineFormat("{0}//dont make this public, it should only be used internally, otherwise you will have magic strings all over your app", doubleIndentation);
            stringBuilder.AppendLineFormat("{0}private static string GetSetting(string settingName)", doubleIndentation);
            stringBuilder.AppendLineFormat("{0}{{", doubleIndentation);
            stringBuilder.AppendLineFormat("{0}return Settings.GetSetting(settingName);", trippleIndentation);
            stringBuilder.AppendLineFormat("{0}}}", doubleIndentation);
            stringBuilder.AppendLine();
        }

        private static void BuildTypedGetSetting(string doubleIndentation, StringBuilder stringBuilder, string trippleIndentation)
        {
            stringBuilder.AppendLineFormat("{0}public static T GetSetting<T>(Setting<T> setting)", doubleIndentation);
            stringBuilder.AppendLineFormat("{0}{{", doubleIndentation);
            stringBuilder.AppendLineFormat("{0}return Settings.GetSetting(setting);", trippleIndentation);
            stringBuilder.AppendLineFormat("{0}}}", doubleIndentation);
            stringBuilder.AppendLine();
        }

        private static void BuildStaticConstructor(string doubleIndentation, StringBuilder stringBuilder, string className, string trippleIndentation, string quadrupleIndentation, bool excludeAzure)
        {
            if (!excludeAzure)
            {
                stringBuilder.AppendLineFormat("{0}static {1}()", doubleIndentation, className);
                stringBuilder.AppendLineFormat("{0}{{", doubleIndentation);
                stringBuilder.AppendLineFormat("{0}bool useAzureSettings;", trippleIndentation);
                stringBuilder.AppendLineFormat("{0}try", trippleIndentation);
                stringBuilder.AppendLineFormat("{0}{{", trippleIndentation);
                stringBuilder.AppendLineFormat("{0}useAzureSettings = RoleEnvironment.IsAvailable;", quadrupleIndentation);
                stringBuilder.AppendLineFormat("{0}}}", trippleIndentation);
                stringBuilder.AppendLineFormat("{0}catch", trippleIndentation);
                stringBuilder.AppendLineFormat("{0}{{", trippleIndentation);
                stringBuilder.AppendLineFormat("{0}/* swallow, using from some other app, use app settings */", quadrupleIndentation);
                stringBuilder.AppendLineFormat("{0}useAzureSettings = false;", quadrupleIndentation);
                stringBuilder.AppendLineFormat("{0}}}", trippleIndentation);
                stringBuilder.AppendLineFormat("{0}Settings = useAzureSettings ? (ISettingsSource) new AzureSettingsSource() : new AppSettingsSettingsSource();", trippleIndentation);
                stringBuilder.AppendLineFormat("{0}}}", doubleIndentation);
            }
            else
            {
                stringBuilder.AppendLineFormat("{0}static {1}()", doubleIndentation, className);
                stringBuilder.AppendLineFormat("{0}{{", doubleIndentation);
                stringBuilder.AppendLineFormat("{0}Settings = new AppSettingsSettingsSource();", trippleIndentation);
                stringBuilder.AppendLineFormat("{0}}}", doubleIndentation);
            }
            stringBuilder.AppendLine();
        }

        private static void CreateAzureFiles(IEnumerable<XmlAppSetting> appSettings, IEnumerable<Project> projects, IEnumerable<AzureMapping> mappings)
        {
            var azureProject = projects.Where(p => !string.IsNullOrWhiteSpace(p.AzureProjectName));
            if(!azureProject.Any())
                return;
            foreach (var project in azureProject)
            {
                string directoryPath = Path.Combine(project.AzureProjectDirectory, project.AzureProjectName);
                CreateAzureFile(appSettings, project, mappings, AzureFile.Definition, directoryPath);
                CreateAzureFile(appSettings, project, mappings, AzureFile.Local, directoryPath);
                CreateAzureFile(appSettings, project, mappings, AzureFile.Cloud, directoryPath);
            }
            
            //XmlReader
        }

        private static void CreateAzureFile(IEnumerable<XmlAppSetting> appSettings, Project project, IEnumerable<AzureMapping> mappings, AzureFile fileType, string directoryPath)
        {
            string filePath;
            XNamespace xNamespace;
            List<XName> roleQualifiedNames;
            XDocument document = GetDocument(fileType, directoryPath, out xNamespace, out filePath, out roleQualifiedNames);
            //Find the ConfigurationSettings for this specific project
            XElement projectRole = null;
            List<XElement> allRoles = new List<XElement>();
            foreach (var roleQualifiedName in roleQualifiedNames)
            {
                allRoles.AddRange(document.Descendants(roleQualifiedName));
            }
            foreach (var xElement in allRoles)
            {
                var nameAttribute = GetAttribute(xElement, "name", true);
                if (nameAttribute.Equals(project.ProjectName, StringComparison.OrdinalIgnoreCase))
                {
                    projectRole = xElement;
                    break;
                }
            }
            if(projectRole == null)
                //This document doesnt have a definition for this project
                return;
            XName configSettingsQualifiedName = XName.Get("ConfigurationSettings", xNamespace.NamespaceName);
            IEnumerable<XElement> configSettingsNodes = projectRole.Descendants(configSettingsQualifiedName);
            /*Remove existing settings for this project*/
            XName settingQualifiedName = XName.Get("Setting", xNamespace.NamespaceName);
            configSettingsNodes.Descendants(settingQualifiedName).Remove();
            switch (fileType)
            {
                case AzureFile.Definition:
                    SaveDefinitionSettings(document, appSettings, configSettingsNodes, filePath,
                                                settingQualifiedName);
                    break;
                case AzureFile.Local:
                    Environment environment = Environment.Dev;
                    var mapping = mappings.Where(m => m.Target.Equals("Local", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (mapping != null)
                    {
                        environment = EnumExtensions.Parse<Environment>(mapping.Name);
                    }
                    SaveActualSettings(document, appSettings, configSettingsNodes, filePath,
                                       settingQualifiedName, project, environment.ToString());
                    break;
                case AzureFile.Cloud:
                    environment = Environment.Live;
                    var cloudMapping = mappings.Where(m => m.Target.Equals("Cloud", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (cloudMapping != null)
                    {
                        environment = EnumExtensions.Parse<Environment>(cloudMapping.Name);
                    }
                    SaveActualSettings(document, appSettings, configSettingsNodes, filePath,
                                       settingQualifiedName, project, environment.ToString());
                    break;
                default: throw new ArgumentOutOfRangeException(string.Format("Unknown enum value \"{0}\"", fileType));
            }
        }

        private static void SaveDefinitionSettings(XDocument document, IEnumerable<XmlAppSetting> appSettings, IEnumerable<XElement> configSettingsNodes, string filePath, XName settingQualifiedName)
        {
            foreach (var appSetting in appSettings)
            {
                if(appSetting.AzureExclude || appSetting.AzureDefinitonExclude)
                    continue;
                XElement element = new XElement(settingQualifiedName);
                element.SetAttributeValue("name", appSetting.Name);
                foreach (var configSettingsNode in configSettingsNodes)
                {
                    configSettingsNode.Add(element);
                }
            }
            document.Save(filePath,SaveOptions.None);
        }

        private static void SaveActualSettings(XDocument document, IEnumerable<XmlAppSetting> appSettings, IEnumerable<XElement> configSettingsNodes, string filePath, XName settingQualifiedName, Project project, string environment)
        {
            foreach (var appSetting in appSettings)
            {
                if (appSetting.AzureExclude)
                    continue;
                //Get the value to apply
                string value = GetProjectEnvironmentSettingValue(appSetting, project.ProjectName, environment);
                if (value != null)
                {
                    //Create a settings element
                    XElement element = new XElement(settingQualifiedName);
                    element.SetAttributeValue("name", appSetting.Name);
                    element.SetAttributeValue("value", value);
                    foreach (var configSettingsNode in configSettingsNodes)
                    {
                        configSettingsNode.Add(element);
                    }
                }
            }
            document.Save(filePath);
        }

        private static XDocument GetDocument(AzureFile fileType, string directoryPath, out XNamespace xNamespace, out string filePath, out List<XName> roleQualifiedNames)
        {
            roleQualifiedNames = new List<XName>();
            switch (fileType)
            {
                case AzureFile.Definition : 
                    filePath = string.Format("{0}\\ServiceDefinition.csdef", directoryPath);
                    xNamespace = XNamespace.Get("http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition");
                    roleQualifiedNames.Add(XName.Get("WebRole", xNamespace.NamespaceName));
                    roleQualifiedNames.Add(XName.Get("WorkerRole", xNamespace.NamespaceName));
                    break;
                case AzureFile.Local: 
                    filePath = string.Format("{0}\\ServiceConfiguration.Local.cscfg", directoryPath);
                    xNamespace = XNamespace.Get("http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceConfiguration");
                    roleQualifiedNames.Add(XName.Get("Role", xNamespace.NamespaceName));
                    break;
                case AzureFile.Cloud: 
                    filePath = string.Format("{0}\\ServiceConfiguration.Cloud.cscfg", directoryPath);
                    xNamespace = XNamespace.Get("http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceConfiguration");
                    roleQualifiedNames.Add(XName.Get("Role", xNamespace.NamespaceName));
                    break;
                default: throw new ArgumentOutOfRangeException(string.Format("Unknown enum value \"{0}\"", fileType));
            }
            if (!File.Exists(filePath))
                throw new FileNotFoundException(string.Format("Azure file was not found at \"{0}\"", Path.GetFullPath(filePath)));
            var document = XDocument.Load(filePath, LoadOptions.SetLineInfo);
            return document;
        }

        private static SettingsClass GetSettingsClassSettingsFromXml(XDocument document, string nodeName)
        {
            var xElement = document.Descendants(nodeName).FirstOrDefault();
            if (xElement == null)
                return null;
            string defaultFileName = string.Empty;
            switch (nodeName)
            {
                case "settingsClass":
                    defaultFileName = "Settings.cs";
                    break;
                case "staticClass":
                    defaultFileName = "ConfigurationManager.cs";
                    break;
                default: throw new ArgumentException(string.Format("Unknown class \"{0}\"", nodeName));
            }
            var settings = new SettingsClass
                       {
                           Directory = GetAttribute(xElement, "directory", false) ?? string.Empty,
                           Name = GetAttribute(xElement, "name", true) ?? defaultFileName,
                           Namespace = GetAttribute(xElement, "namespace", true) ?? "Entelect",
                           ExcludeAzure = bool.Parse(GetAttribute(xElement, "excludeazure", false) ?? "false")
                       };
            if (!settings.Name.EndsWith(".cs"))
                settings.Name = string.Format("{0}.cs", settings.Name);
            return settings;
        }

        private static void CreateSettingsClass(IEnumerable<XmlAppSetting> appSettings, SettingsClass settingsClass)
        {
            if(settingsClass== null)
                return;
            const string indentation = "    ";
            string doubleIndentation = string.Concat(indentation, indentation);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("using System;");
            stringBuilder.AppendLine("using System.Collections.Generic;");
            stringBuilder.AppendLine("using System.Linq;");
            stringBuilder.AppendLine("using System.Text;");
            stringBuilder.AppendLine("using Entelect.Settings;");
            stringBuilder.AppendLineFormat("namespace {0}", settingsClass.Namespace);
            stringBuilder.AppendLine("{");
            stringBuilder.AppendLineFormat("{0}public static partial class {1}", indentation,
                                           settingsClass.Name.Replace(".cs", "").CapitaliseFirstLetter());
            stringBuilder.AppendLineFormat("{0}{{", indentation);
            foreach (var xmlAppSetting in appSettings)
            {
                if(xmlAppSetting.Autogenerate == false)
                    continue;
                string escapedSettingName = xmlAppSetting.Name.Replace(":", "").Replace(".", "").CapitaliseFirstLetter();
                //Check if string
                if(xmlAppSetting.Type.Equals("string",StringComparison.OrdinalIgnoreCase))
                {
                    stringBuilder.AppendLineFormat("{0}public static string {1} = \"{2}\";", doubleIndentation, escapedSettingName, xmlAppSetting.Name);
                }
                // check if list
                else if (xmlAppSetting.Type.StartsWith("list<",StringComparison.OrdinalIgnoreCase))
                {
                    string innerType = xmlAppSetting.Type.ReplaceIgnoreCase("list<", "");
                    innerType = innerType.Remove(innerType.Length - 1);
                    if (string.IsNullOrWhiteSpace(xmlAppSetting.Delimiter))
                        throw new XmlSchemaException(string.Format("No delimiter specified for list with name {0}",
                                                                   xmlAppSetting.Name));
                    //list of strings
                    if (innerType.Equals("string", StringComparison.OrdinalIgnoreCase))
                    {
                        stringBuilder.AppendLineFormat(
                            "{0}public static Setting<{1}> {2} = new Setting<{1}>(x => new {1}(x.GetSetting(\"{3}\").Split(new[]{{'{4}'}},StringSplitOptions.RemoveEmptyEntries)));",
                            doubleIndentation, xmlAppSetting.Type, escapedSettingName, xmlAppSetting.Name,
                            xmlAppSetting.Delimiter);
                    }
                    else
                    {
                        Type parsedType = TypeExtensions.GetTypeFromTypeName(innerType);
                        if (parsedType != null && parsedType.IsGenericType && parsedType.GetGenericTypeDefinition() == typeof(Nullable<>))
                        {
                            //list of nullable base types
                            string trippleIndentation = string.Concat(doubleIndentation, indentation);
                            string quadrupleIndentation = string.Concat(doubleIndentation, doubleIndentation);
                            string quintupleIndentation = string.Concat(quadrupleIndentation, indentation);
                            string baseTypeName =
                                innerType.Replace("?", "").Replace("Nullable<", "").Replace(">", "");
                            stringBuilder.AppendLineFormat(
                                "{0}public static Setting<{1}> {2} = new Setting<{1}>(x => {{",
                                doubleIndentation, xmlAppSetting.Type, escapedSettingName);
                            stringBuilder.AppendLineFormat("{0}string[] values = x.GetSetting(\"{1}\").Split(new[]{{'{2}'}},StringSplitOptions.RemoveEmptyEntries);", trippleIndentation, xmlAppSetting.Name, xmlAppSetting.Delimiter);
                            stringBuilder.AppendLineFormat("{0}{1} results = new {1}();", trippleIndentation, xmlAppSetting.Type);
                            stringBuilder.AppendLineFormat("{0}foreach(string value in values)", trippleIndentation, baseTypeName);
                            stringBuilder.AppendLineFormat("{0}{{", trippleIndentation);
                            stringBuilder.AppendLineFormat("{0}if (string.IsNullOrWhiteSpace(value))", quadrupleIndentation);
                            stringBuilder.AppendLineFormat("{0}results.Add(null);", quintupleIndentation);
                            stringBuilder.AppendLineFormat("{0}if (value.Equals(\"null\",StringComparison.OrdinalIgnoreCase))", quadrupleIndentation);
                            stringBuilder.AppendLineFormat("{0}results.Add(null);", quintupleIndentation);
                            stringBuilder.AppendLineFormat("{0}{1} parsed;", quadrupleIndentation, baseTypeName);
                            stringBuilder.AppendLineFormat("{0}if ({1}.TryParse(value,out parsed))", quadrupleIndentation, baseTypeName);
                            stringBuilder.AppendLineFormat("{0}results.Add(parsed);", quintupleIndentation);
                            stringBuilder.AppendLineFormat("{0}throw new ArgumentException(string.Format(\"Value '{{0}}' could not be converted to type '{1}'\",value));", quadrupleIndentation, xmlAppSetting.Type);
                            stringBuilder.AppendLineFormat("{0}}}", trippleIndentation);
                            stringBuilder.AppendLineFormat("{0}return results;", trippleIndentation);
                            stringBuilder.AppendLineFormat("{0}}});", doubleIndentation);
                        }
                        else
                        {
                            //list of other type
                            stringBuilder.AppendLineFormat(
                                    "{0}public static Setting<{1}> {2} = new Setting<{1}>(x => new {1}(x.GetSetting(\"{3}\").Split(new[]{{'{4}'}},StringSplitOptions.RemoveEmptyEntries).Select({5}.Parse)));",
                                    doubleIndentation, xmlAppSetting.Type, escapedSettingName, xmlAppSetting.Name,
                                    xmlAppSetting.Delimiter, innerType);
                        }
                    }
                }
                else
                {
                    Type parsedType = TypeExtensions.GetTypeFromTypeName(xmlAppSetting.Type);
                    if (parsedType != null && parsedType.IsGenericType && parsedType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        //Is nullable core class
                        string trippleIndentation = string.Concat(doubleIndentation, indentation);
                        string quadrupleIndentation = string.Concat(doubleIndentation, doubleIndentation);
                        string baseTypeName =
                            xmlAppSetting.Type.Replace("?", "").Replace("Nullable<", "").Replace(">", "");
                        stringBuilder.AppendLineFormat(
                            "{0}public static Setting<{1}> {2} = new Setting<{1}>(x => {{",
                            doubleIndentation, xmlAppSetting.Type, escapedSettingName);
                        stringBuilder.AppendLineFormat("{0}string value = x.GetSetting(\"{1}\");", trippleIndentation, xmlAppSetting.Name);
                        stringBuilder.AppendLineFormat("{0}if (string.IsNullOrWhiteSpace(value))", trippleIndentation);
                        stringBuilder.AppendLineFormat("{0}return null;", quadrupleIndentation);
                        stringBuilder.AppendLineFormat("{0}if (value.Equals(\"null\",StringComparison.OrdinalIgnoreCase))", trippleIndentation);
                        stringBuilder.AppendLineFormat("{0}return null;", quadrupleIndentation);
                        stringBuilder.AppendLineFormat("{0}{1} parsed;", trippleIndentation, baseTypeName);
                        stringBuilder.AppendLineFormat("{0}if ({1}.TryParse(value,out parsed))", trippleIndentation, baseTypeName);
                        stringBuilder.AppendLineFormat("{0}return parsed;", quadrupleIndentation);
                        stringBuilder.AppendLineFormat("{0}throw new ArgumentException(string.Format(\"Value '{{0}}' could not be converted to type '{1}'\",value));", trippleIndentation, xmlAppSetting.Type);
                        stringBuilder.AppendLineFormat("{0}}});", doubleIndentation);
                    }
                    else
                        //not nullable core type or is custom user type
                        stringBuilder.AppendLineFormat(
                            "{0}public static Setting<{1}> {2} = new Setting<{1}>(x => {1}.Parse(x.GetSetting(\"{3}\")));",
                            doubleIndentation, xmlAppSetting.Type, escapedSettingName, xmlAppSetting.Name);
                }
            }
            stringBuilder.AppendLineFormat("{0}}}", indentation);
            stringBuilder.AppendLine("}");
            string filePath = Path.Combine(settingsClass.Directory, settingsClass.Name);
            if (!Directory.Exists(settingsClass.Directory))
                Directory.CreateDirectory(settingsClass.Directory);
            using (StreamWriter streamWriter = new StreamWriter(filePath))
            {
                streamWriter.Write(stringBuilder);
            }
        }

        private static void CreateEnvironmentSettings(IEnumerable<XmlAppSetting> appSettings, IEnumerable<Project> projects)
        {
            List<NamedXmlTextWriter> writers = new List<NamedXmlTextWriter>();
            try
            {
#if !DEBUG
                string currentDirectory = System.Reflection.Assembly.GetExecutingAssembly().Location;
#else
                string currentDirectory = @"C:\Source\EntelectLibrary\SetGen\src\SetGen\";
#endif
                currentDirectory = Path.GetDirectoryName(currentDirectory);
                //Create all the text writes
                foreach (Project project in projects)
                {
                    string path = project.ProjectDirectory;
                    if (!path.Contains(":\\"))
                        path = Path.Combine(currentDirectory, path);
                    //Write the default environments files
                    foreach (var environment in Enum.GetValues(typeof(Environment)))
                    {
                        writers.Add(
                            new NamedXmlTextWriter(
                                string.Format("{0}\\{1}\\Settings.{2}.Config", path, project.ProjectName,
                                              environment), System.Text.Encoding.UTF8, project.ProjectName,
                                (Environment)environment));
                    }

                    foreach (var mapping in appSettings.First().OtherEnvironments)
                    {
                        writers.Add(
                            new NamedXmlTextWriter(
                                string.Format("{0}\\{1}\\Settings.{2}.Config", path, project.ProjectName,
                                              mapping.Name), System.Text.Encoding.UTF8, project.ProjectName,
                                (Environment)Enum.Parse(typeof(Environment), "Live")) { MappedFrom = mapping.Name });
                    }

                }
                foreach (var writer in writers)
                {
                    writer.Formatting = Formatting.Indented;
                    writer.Indentation = 2;
                    writer.WriteStartDocument();
                    writer.WriteStartElement("appSettings");
                    foreach (var xmlAppSetting in appSettings)
                    {
                        var environment = string.IsNullOrEmpty(writer.MappedFrom) ? writer.Environment.ToString() : writer.MappedFrom;

                        string value = GetProjectEnvironmentSettingValue(xmlAppSetting, writer.ProjectName, environment);
                        if (value != null)
                        {
                            writer.WriteStartElement("add");
                            writer.WriteAttributeString("key", xmlAppSetting.Name);
                            writer.WriteAttributeString("value", value);
                            writer.WriteEndElement();
                        }
                    }
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }
            }
            finally
            {
                foreach (var xmlTextWriter in writers)
                {
                    xmlTextWriter.Close();
                }
            }
        }

        private static string GetProjectEnvironmentSettingValue(XmlAppSetting appSetting, string projectName, string environment)
        {
            string value;
            var projectSettings = appSetting.ProjectSettings.FirstOrDefault(p => p.Name.Equals(projectName));
            if (projectSettings != null)
            {
                value = GetEnvironmentValueFromSetting(environment, projectSettings);
                if (value == null)
                    value = GetEnvironmentValueFromSetting(environment, appSetting);
            }
            else
            {
                value = GetEnvironmentValueFromSetting(environment, appSetting);
            }
            return value;
        }

        private static string GetEnvironmentValueFromSetting(string environment, IProjectSetting setting)
        {
            var environmentEnum = Environment.Dev;

            if (Enum.TryParse(environment, true, out environmentEnum))
            {
                switch (environmentEnum)
                {
                    case Environment.Dev:
                        return setting.DevValue;
                    case Environment.Qa:
                        return setting.QaValue;
                    case Environment.Staging:
                        return setting.StagingValue;
                    case Environment.Live:
                        return setting.LiveValue;
                    default:
                        throw new ArgumentException(string.Format("Unknown enum value {0}", environment.ToString()));
                }
            }
            else
            {
                var mapping = setting.OtherEnvironments.Single(e => e.Name == environment);
                var value = mapping.Value;
                var defaultValue = string.Empty;

                if (Enum.TryParse(mapping.Target, true, out environmentEnum))
                {
                    switch (environmentEnum)
                    {
                        case Environment.Dev:
                            defaultValue = setting.DevValue;
                            break;
                        case Environment.Qa:
                            defaultValue = setting.QaValue;
                            break;
                        case Environment.Staging:
                            defaultValue = setting.StagingValue;
                            break;
                        case Environment.Live:
                            defaultValue = setting.LiveValue;
                            break;
                        default:
                            throw new ArgumentException(string.Format("Unknown enum value {0}", environment.ToString()));
                    }
                }
                return value ?? defaultValue;

            }
            throw new ArgumentException(string.Format("Unknown enum value {0}", environment.ToString()));
        }

        private static IEnumerable<XmlAppSetting> GetAppSettingsFromXml(XDocument document)
        {
            List<XmlAppSetting> xmlAppSettings = new List<XmlAppSetting>();
            IEnumerable<EnvironmentMapping> environmentMappings = GetEnvironmentMappings(document);
            foreach (var xElement in document.Descendants("appSettings").Descendants("key"))
            {
                XmlAppSetting setting = new XmlAppSetting
                {
                    Name = GetAttribute(xElement, "name", true),
                    DevValue = GetAttribute(xElement, "dev", false),
                    QaValue = GetAttribute(xElement, "qa", false),
                    StagingValue = GetAttribute(xElement, "staging", false),
                    LiveValue = GetAttribute(xElement, "live", false),
                    DefaultValue = GetAttribute(xElement, "default", false),
                    Delimiter = GetAttribute(xElement, "delimiter", false),
                    OtherEnvironments = GetOtherEnvironmentSettings(xElement, environmentMappings, false)
                };
                setting.Autogenerate = GetAutoGenValue(xElement);
                setting.AzureExclude = GetAzureExcludeValue(xElement);
                setting.AzureDefinitonExclude = GetAzureDefinitionExcludeValue(xElement);
                setting.Type = GetTypeString(xElement);
                //only check for not null as that means the attribute existed and was set
                if (setting.DefaultValue != null)
                    setting = ApplyDefaultValueWhereNeeded<XmlAppSetting>(setting);
                setting.ProjectSettings = GetProjectSettings(setting, xElement, environmentMappings);
                xmlAppSettings.Add(setting);
            }
            return xmlAppSettings;
        }

        private static List<XmlProjectSetting> GetProjectSettings(XmlAppSetting setting, XElement xElement, IEnumerable<EnvironmentMapping> environmentMappings)
        {
            List<XmlProjectSetting> projectSettings = new List<XmlProjectSetting>();
            var xProjectSettings = xElement.Descendants("project");
            foreach (var xProjectSetting in xProjectSettings)
            {
                XmlProjectSetting projectSetting = new XmlProjectSetting
                {
                    Name = GetAttribute(xProjectSetting, "name", true),
                    DevValue = GetAttribute(xProjectSetting, "dev", false),
                    QaValue = GetAttribute(xProjectSetting, "qa", false),
                    StagingValue = GetAttribute(xProjectSetting, "staging", false),
                    LiveValue = GetAttribute(xProjectSetting, "live", false),
                    DefaultValue = GetAttribute(xProjectSetting, "default", false),
                    OtherEnvironments = GetOtherEnvironmentSettings(xProjectSetting, environmentMappings, false)
                };
                if (projectSetting.DefaultValue == null)
                    projectSetting.DefaultValue = setting.DefaultValue;
                if (projectSetting.DefaultValue != null)
                    projectSetting = ApplyDefaultValueWhereNeeded<XmlProjectSetting>(projectSetting);
                projectSettings.Add(projectSetting);
            }
            return projectSettings;
        }

        private static List<EnvironmentMapping> GetOtherEnvironmentSettings(XElement xProjectSetting, IEnumerable<EnvironmentMapping> environmentMappings, bool throwError)
        {
            var settings = new List<EnvironmentMapping>();
            foreach (var mapping in environmentMappings)
            {
                var value = GetAttribute(xProjectSetting, mapping.Name.ToLowerInvariant(), throwError);
                mapping.Value = value;
                settings.Add(mapping);
            }

            return settings;
        }

        private static T ApplyDefaultValueWhereNeeded<T>(IProjectSetting setting) where T : IProjectSetting
        {
            //we only check for null here as that means the element didnt have the value
            if (setting.DevValue == null)
                setting.DevValue = setting.DefaultValue;
            if (setting.QaValue == null)
                setting.QaValue = setting.DefaultValue;
            if (setting.StagingValue == null)
                setting.StagingValue = setting.DefaultValue;
            if (setting.LiveValue == null)
                setting.LiveValue = setting.DefaultValue;
            return (T)setting;
        }

        private static string GetTypeString(XElement xElement)
        {
            var typeString = GetAttribute(xElement, "type", false);
            if (string.IsNullOrWhiteSpace(typeString))
                return "string";
            return typeString;
        }

        private static bool GetAutoGenValue(XElement xElement)
        {
            var autoGenString = GetAttribute(xElement, "autogen", false);
            if (string.IsNullOrWhiteSpace(autoGenString))
                return true;
            bool value;
            if (!bool.TryParse(autoGenString, out value))
                throw new ArgumentException(
                    string.Format("Value \"{0}\" for attribute autogen in node <key> could not be converted to a bool", autoGenString));
            return value;
        }

        private static bool GetAzureExcludeValue(XElement xElement)
        {
            var azureExcludeString = GetAttribute(xElement, "azureExclude", false);
            if (string.IsNullOrWhiteSpace(azureExcludeString))
                return false;
            bool value;
            if (!bool.TryParse(azureExcludeString, out value))
                throw new ArgumentException(
                    string.Format("Value \"{0}\" for attribute azureExclude in node <key> could not be converted to a bool", azureExcludeString));
            return value;
        }

        private static bool GetAzureDefinitionExcludeValue(XElement xElement)
        {
            var azureExcludeString = GetAttribute(xElement, "azureDefinitionExclude", false);
            if (string.IsNullOrWhiteSpace(azureExcludeString))
                return false;
            bool value;
            if (!bool.TryParse(azureExcludeString, out value))
                throw new ArgumentException(
                    string.Format("Value \"{0}\" for attribute azureDefinitionExclude in node <key> could not be converted to a bool", azureExcludeString));
            return value;
        }
        private static IEnumerable<AzureMapping> GetAzureMappingsFromXml(XDocument document)
        {
            return document.Descendants("mappings").Descendants("map").Select(
                m => new AzureMapping(GetAttribute(m, "name", true), GetAttribute(m, "target", true)));
        }

        private static IEnumerable<Project> GetAzureModulesFromXml(XDocument document)
        {
            var projects = 
                document.Descendants("projects").Descendants("project").Select(
                    p =>
                    new Project
                        {
                            ProjectName = GetAttribute(p, "projectName", true),
                            ProjectDirectory = GetAttribute(p, "projectDirectory", false) ?? string.Empty,
                            AzureProjectName = GetAttribute(p, "azureProjectName", false),
                            AzureProjectDirectory = GetAttribute(p, "azureProjectDirectory", false) ?? string.Empty
                        }).Distinct();
            if(projects.Count() == 0)
                throw new XmlSchemaException("No <project> nodes have been specified in the <projects> node collection");
            return projects;
        }

        private static string GetAttribute(XElement element, string attributeName, bool throwErrorIfMissing)
        {
            var attribute = element.Attribute(attributeName);
            if (attribute != null)
                return attribute.Value;
            if (!throwErrorIfMissing)
                return null;
            int lineNumber = -1;
            int linePosition = -1;
            IXmlLineInfo lineInfo = element;
            if (lineInfo.HasLineInfo())
            {
                lineNumber = lineInfo.LinePosition;
                linePosition = lineInfo.LinePosition;
            }
            throw new XmlSchemaException(
                string.Format("A <{0}> node is missing the required \"{1}\" attribute at line {2} position {3}", element.Name, attributeName,lineNumber,linePosition),
                null,
                lineNumber,
                linePosition);
        }
    }
}