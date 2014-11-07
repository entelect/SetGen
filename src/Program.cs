using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reflection;

namespace SetGen
{
    class Program
    {
        private static string _version;
        private const string BEER = 
@"  _.._..,_,_    _.._..,_,_     _.._..,_,_
 (          )  (          )   (          )
  ]~,'-.-~~[    ]~,'-.-~~[     ]~,'-.-~~[
.=])' (;  ([  .=])' (;  ([   .=])' (;  ([
| ]:: '    [  | ]:: '    [   | ]:: '    [
'=]): .)  ([  '=]): .)  ([   '=]): .)  ([
  |:: '    |    |:: '    |     |:: '    |
   ~~----~~      ~~----~~       ~~----~~";

        private const string _divider = "-------==========|||||||||||||||==========-------";
        static void Main(string[] args)
        {
            try
            {
                SetVersion();
                Console.WriteLine(_divider);
                Console.WriteLine("Welcome to SetGen version :{0}", _version);
                Console.WriteLine("Written by Ryan Kotzen and Rishal Hurbans (with an insignificant contribution by Matthew Butler)");
                Console.WriteLine("Insignificant contributions by Matthew Butler");
                Console.WriteLine("Moral support from Bradley van Aardt");
                Console.WriteLine(_divider);
                Console.WriteLine(BEER);
                Console.WriteLine(_divider);
                Dictionary<ArgumentsEnum, string> arguments = ProcessArgs(args);
                SetWorkingDirectory(arguments);
                SettingsGenerator.GenerateSettings(GetFilePath(arguments));
                Console.WriteLine("Done");
                #if DEBUG
                Console.ReadLine();
                #endif
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
                Console.WriteLine(exception.StackTrace);
                Console.ReadLine();
            }
        }

        private static void SetWorkingDirectory(Dictionary<ArgumentsEnum, string> arguments)
        {
            string workingDirectory;
            if(arguments.ContainsKey(ArgumentsEnum.WorkingDirectory))
                workingDirectory = arguments[ArgumentsEnum.WorkingDirectory];
            else
            {
                workingDirectory = Path.GetDirectoryName(Assembly.GetAssembly(typeof(SettingsGenerator)).CodeBase).Replace("file:\\","");
                int binIndex = workingDirectory.IndexOf("bin", StringComparison.OrdinalIgnoreCase);
                if (binIndex > 0)
                {
                    workingDirectory = workingDirectory.Remove(binIndex);
                    workingDirectory = Directory.GetParent(workingDirectory).FullName;
                }
                
            }
            Directory.SetCurrentDirectory(workingDirectory);
            Console.WriteLine("Working Directory {0}", Directory.GetCurrentDirectory());
        }

        private static void SetVersion()
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            _version = "V" + v.Major + "." + v.Minor + "." + v.Revision;
        }

        private static string GetFilePath(Dictionary<ArgumentsEnum, string> arguments)
        {
            string filePath;
            
            if (arguments.ContainsKey(ArgumentsEnum.GlobalSettingsFileLocation))
            {
                filePath = arguments[ArgumentsEnum.GlobalSettingsFileLocation];
            }
            else
            {
                filePath = ConfigurationManager.AppSettings[ArgumentsEnum.GlobalSettingsFileLocation.ToString()];
            }
            return filePath;
        }

        private enum ArgumentsEnum
        {
            GlobalSettingsFileLocation,
            WorkingDirectory
        }

        private static Dictionary<ArgumentsEnum, string> ProcessArgs(string[] args)
        {
            var arguments = new Dictionary<ArgumentsEnum, string>();
            foreach (string argument in args)
            {
                string key, value;
                string[] splitString = argument.Split('=');
                if (splitString.Length != 2)
                    throw new ArgumentException(string.Format("Argument does not have the correct format:{0}", argument));
                key = splitString[0];
                value = splitString[1].Replace("\"", "");
                ArgumentsEnum selectedArgumentEnum;
                if (!ArgumentsEnum.TryParse(key, true, out selectedArgumentEnum))
                {
                    throw new ArgumentException(string.Format("Argument is not of a known type:{0}", argument));
                }
                arguments.Add(selectedArgumentEnum, value);
            }
            return arguments;
        }
    }
}
