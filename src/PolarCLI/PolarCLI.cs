using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Options;

namespace CppSharp
{
    class PolarCLI
    {
        private static OptionSet optionSet = new OptionSet();
        
        public static PolarDriver driver = new PolarDriver();
        
        static bool ParseCommandLineArgs(string[] args, List<string> errorMessages, ref bool helpShown)
        {
            var showHelp = false;

            optionSet.Add("V=", "the Full {PATH} of VectorHolder.hpp", (i) => { SetVectorHolderPath(i); });
            optionSet.Add("I=", "the {PATH} of a folder to search for include files", (i) => { AddIncludeDirs(i, errorMessages); });
            optionSet.Add("L=", "the {PATH} of a folder to search for additional libraries", l => driver.AddLibraryDirectory(l) );
            optionSet.Add("D:", "additional define with (optional) value to add to be used while parsing the given header files", (n, v) => AddDefine(n, v, errorMessages) );
            optionSet.Add("A=", "additional Clang arguments to pass to the compiler while parsing the given header files", (v) => AddArgument(v, errorMessages) );

            optionSet.Add("o=|output=", "the {PATH} for the generated bindings file (doesn't need the extension since it will depend on the generator)", v => HandleOutputArg(v, errorMessages) );
            optionSet.Add("on=|outputnamespace=", "the {NAMESPACE} that will be used for the generated code", on => driver.setOutputNamespace(on) );
            
            optionSet.Add("h|help", "shows the help", hl => { showHelp = (hl != null); });

            List<string> additionalArguments = null;

            try
            {
                additionalArguments = optionSet.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
                        
            if (showHelp || additionalArguments != null && additionalArguments.Count == 0)
            {
                helpShown = true;
                ShowHelp();
                return false;
            }

            foreach(string s in additionalArguments)
                HandleAdditionalArgument(s, errorMessages);

            return true;
        }

        static void ShowHelp()
        {
            string appName = Platform.IsWindows ? "CppSharp.CLI.exe" : "CppSharp.CLI";

            Console.WriteLine();
            Console.WriteLine("Usage: {0} [OPTIONS]+ [FILES]+", appName);
            Console.WriteLine("Generates target language bindings to interop with unmanaged code.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            optionSet.WriteOptionDescriptions(Console.Out);
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Useful informations:");
            Console.WriteLine(" - to specify a file to generate bindings from you just have to add their path without any option flag, just like you");
            Console.WriteLine("   would do with GCC compiler. You can specify a path to a single file (local or absolute) or a path to a folder with");
            Console.WriteLine("   a search query.");
            Console.WriteLine("   e.g.: '{0} [OPTIONS]+ my_header.h' will generate the bindings for the file my_header.h", appName);
            Console.WriteLine("   e.g.: '{0} [OPTIONS]+ include/*.h' will generate the bindings for all the '.h' files inside the include folder", appName);
            Console.WriteLine(" - the options 'iln' (same as 'inputlibraryname') and 'l' have a similar meaning. Both of them are used to tell");
            Console.WriteLine("   the generator which library has to be used to P/Invoke the functions from your native code.");
            Console.WriteLine("   The difference is that if you want to generate the bindings for more than one library within a single managed");
            Console.WriteLine("   file you need to use the 'l' option to specify the names of all the libraries that contain the symbols to be loaded");
            Console.WriteLine("   and you MUST set the 'cs' ('checksymbols') flag to let the generator automatically understand which library");
            Console.WriteLine("   to use to P/Invoke. This can be also used if you plan to generate the bindings for only one library.");
            Console.WriteLine("   If you specify the 'iln' (or 'inputlibraryname') options, this option's value will be used for all the P/Invokes");
            Console.WriteLine("   that the generator will create.");
            Console.WriteLine(" - If you specify the 'unitybuild' option then the generator will output a file for each given header file that will");
            Console.WriteLine("   contain only the bindings for that header file.");
        }
        

        static void AddIncludeDirs(string dir, List<string> errorMessages)
        {
            if (Directory.Exists(dir))
                driver.AddIncludeDirectory(dir);
            else
                errorMessages.Add(string.Format("Directory '{0}' doesn't exist. Ignoring as include directory.", dir));
        }

        static void HandleOutputArg(string arg, List<string> errorMessages)
        {
            try
            {
                string file = Path.GetFileNameWithoutExtension(arg);
                driver.setOutputDirectory(file);
            }
            catch(Exception e)
            {
                driver.setOutputDirectory(".");
            }
        }

        static void SetVectorHolderPath(string path)
        {
            driver.SetVectorHolderPath(path);
        }
        
        static void AddArgument(string value, List<string> errorMessages)
        {
             driver.AddParserArgument(value);
        }

        static void AddDefine(string name, string value, List<string> errorMessages)
        {
            driver.AddParserDefine(name, value);
        }

        static void HandleAdditionalArgument(string args, List<string> errorMessages)
        {
            if (!Path.IsPathRooted(args))
                args = Path.Combine(Directory.GetCurrentDirectory(), args);
            
            bool searchQuery = args.IndexOf('*') >= 0 || args.IndexOf('?') >= 0;
            try
            {
                if (searchQuery || Directory.Exists(args))
                {
                    GetFilesFromPath(args, errorMessages);
                }
                else if (File.Exists(args))
                {
                    driver.AddSourceFiles(args);
                }
                else
                {
                    errorMessages.Add($"File '{args}' could not be found.");
                }
            }
            catch(Exception)
            {
                errorMessages.Add($"Error while looking for files inside path '{args}'. Ignoring.");
            }
        }

        static void GetFilesFromPath(string path, List<string> errorMessages)
        {
            path = path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            string searchPattern = string.Empty;
            int lastSeparatorPosition = path.LastIndexOf(Path.AltDirectorySeparatorChar);

            if (lastSeparatorPosition >= 0)
            {
                if (path.IndexOf('*', lastSeparatorPosition) >= lastSeparatorPosition || path.IndexOf('?', lastSeparatorPosition) >= lastSeparatorPosition)
                {
                    searchPattern = path.Substring(lastSeparatorPosition + 1);
                    path = path.Substring(0, lastSeparatorPosition);
                }
            }

            try
            {
                if (!string.IsNullOrEmpty(searchPattern))
                {
                    string[] files = Directory.GetFiles(path, searchPattern);

                    foreach (string s in files)
                        driver.AddSourceFiles(s);
                }
                else
                {
                    var files = Directory.GetFiles(path).Where(f =>
                        Path.GetExtension(f) == ".h" || Path.GetExtension(f) == ".hpp");
                    foreach (var file in files)
                    {
                        driver.AddSourceFiles(file);
                    }
                }
            }
            catch (Exception)
            {
                errorMessages.Add(string.Format("Error while looking for files inside path '{0}'. Ignoring.", path));
            }
        }

        static void PrintErrorMessages(List<string> errorMessages)
        {
            foreach (string m in errorMessages)
                Console.Error.WriteLine(m);
        }

        static void Main(string[] args)
        {
            List<string> errorMessages = new List<string>();
            bool helpShown = false;

            try
            {
                if (!ParseCommandLineArgs(args, errorMessages, ref helpShown))
                {
                    PrintErrorMessages(errorMessages);

                    // Don't need to show the help since if ParseCommandLineArgs returns false the help has already been shown
                    return;
                }

                driver.FinalizeSetup();
                PrintErrorMessages(errorMessages);

                driver.run();
            }
            catch (Exception ex)
            {
                PrintErrorMessages(errorMessages);
                Console.Error.WriteLine();

                throw ex;
            }
        }
    }
}