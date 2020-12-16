
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CppSharp.AST;
using CppSharp.Generators;
using CppSharp.Generators.CSharp;
using CppSharp.Parser;
using CppSharp.Passes;
using CppSharp.Utils;
using CppSharp.Types;

namespace CppSharp
{
    public class PolarDriver
    {
        public Dictionary<string, Module> Modules;

        //  Options: Just for parity with Driver class
        public DriverOptions Options { get; private set; }
        public ParserOptions ParserOptions;
        public LinkerOptions LinkerOptions;
        public BindingContext Context;
        public Generators.Generator Generator;
        public PolarDriverOptions DriverOptions;
        private bool hasParsingErrors;

        public bool HasCompilationErrors { get; set; }

        private Module ExtModule = new Module("Ext");
        
        public PolarDriver()
        {
            // We only use the DriverOptions for parity here using Options.IsCSharpGenerator
            Options = new DriverOptions();
            Options.GeneratorKind = GeneratorKind.CSharp;
            
            DriverOptions = new PolarDriverOptions();
            ParserOptions = new ParserOptions();
            LinkerOptions = new LinkerOptions();
            Context = new BindingContext(DriverOptions, ParserOptions);
            Context.PolarFixesEnabled = true;
            ExtModule.SymbolsLibraryName = "Ext-symbols";
            DriverOptions.AddModule(ExtModule);
            DriverOptions.GenerateClassTemplates = true;
            DriverOptions.UseHeaderDirectories = false;
            DriverOptions.GenerateProfilingCode = false;
        }

        public void SetGenerateProfilingCode(bool val)
        {
            DriverOptions.SetGenerateProfilingCode(val);
        }
        
        public void SetVectorHolderPath(string path)
        {
            DriverOptions.SetVectorHolderPath(path);
            ExtModule.Headers.Add(path);
        }

        public void SetVectorHolderName(string name)
        {
            DriverOptions.SetVectorHolderName(name);
        }
        public void SetOptionalPath(string path)
        {
            DriverOptions.SetOptionalPath(path);
            ExtModule.Headers.Add(path);
        }

        public void SetOptionalName(string name)
        {
            DriverOptions.SetOptionalName(name);
        }
        public void setOutputDirectory(string dir)
        {
            DriverOptions.OutputDir = dir;
        }

        public void setOutputNamespace(string nameSpace)
        {
            mainModule.OutputNamespace = nameSpace;
            mainModule.LibraryName = nameSpace;
        }
        
        public Module AddModule(string name)
        {
            return DriverOptions.AddModule(name);
        }

        public void AddParserArgument(string s)
        {
            ParserOptions.AddArguments(s);
        }

        private Module mainModule = new Module("Main");
        
        public void AddSourceFiles(string s)
        {
            Diagnostics.Message("Add Main Module Source File: {0}", s);
            mainModule.Headers.Add(s);
        }

        public void AddParserDefine(string name, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                Diagnostics.Message("-D {0}", name);
                ParserOptions.AddDefines(name);
            }
            else
            {
                Diagnostics.Message("-D {0}={1}", name, value);
                ParserOptions.AddDefines(name + "=" + value);
            }
        }

        public void AddParserDefine(string s)
        {
            ParserOptions.AddDefines(s);
        }

        public void AddIncludeDirectory(string s)
        {
            ParserOptions.AddIncludeDirs(s);
        }

        private string platform = "linux";

        public void SetPlatform(string x)
        {
            platform = x;
        }
        
        string architecture = "x64";

        public void SetArchitecture(string x)
        {
            architecture = x;
        }

        public void setupParserOptions()
        {
            ParserOptions.LanguageVersion = LanguageVersion.CPP17;
            ParserOptions.EnableRTTI = true;
            if (platform == "linux")
            {
                ParserOptions.TargetTriple = "x86_64-linux-gnu-cxx11abi";
                ParserOptions.Setup();
                ParserOptions.SetupLinux();
                ParserOptions.AddDefines("_GLIBCXX_USE_CXX11_ABI=1");
                ParserOptions.AddArguments("-fcxx-exceptions");
            }
            else
            {
                ParserOptions.TargetTriple = "x86_64-pc-windows-msvc";
                ParserOptions.Setup();
                ParserOptions.SetupMSVC();
            }

            ParserOptions.Verbose = false;
        }

        public void FinalizeSetup()
        {
            setupParserOptions();
            var opts = new DriverOptions();
            Generator = new CSharpGenerator(Context);
        }

        public void SetupTypeMaps()
        {
            Context.TypeMaps = new TypeMapDatabase(Context);
        }

        void OnSourceFileParsed(IEnumerable<string> files, ParserResult result)
        {
            OnFileParsed(files, result);
        }

        void OnFileParsed(IEnumerable<string> files, ParserResult result)
        {
            switch (result.Kind)
            {
                case ParserResultKind.Success:
                    Diagnostics.Message("Parsed '{0}'", string.Join(", ", files));
                    break;
                case ParserResultKind.Error:
                    Diagnostics.Error("Error parsing '{0}'", string.Join(", ", files));
                    hasParsingErrors = true;
                    break;
                case ParserResultKind.FileNotFound:
                    Diagnostics.Error("File{0} not found: '{1}'",
                        (files.Count() > 1) ? "s" : "", string.Join(",", files));
                    hasParsingErrors = true;
                    break;
            }

            for (uint i = 0; i < result.DiagnosticsCount; ++i)
            {
                var diag = result.GetDiagnostics(i);

                if (diag.Level == ParserDiagnosticLevel.Warning &&
                    !DriverOptions.Verbose)
                    continue;

                if (diag.Level == ParserDiagnosticLevel.Note)
                    continue;

                Diagnostics.Message("{0}({1},{2}): {3}: {4}",
                    diag.FileName, diag.LineNumber, diag.ColumnNumber,
                    diag.Level.ToString().ToLower(), diag.Message);
            }
        }

        public bool ParseSourceFiles(IEnumerable<string> sourceFiles)
        {
            ClangParser.SourcesParsed += OnSourceFileParsed;

            ParserOptions.BuildForSourceFile(Options.Modules);
            using (ParserResult result = ClangParser.ParseSourceFiles(
                sourceFiles, ParserOptions))
                Context.TargetInfo = result.TargetInfo;

            Context.ASTContext = ClangParser.ConvertASTContext(ParserOptions.ASTContext);

            ClangParser.SourcesParsed -= OnSourceFileParsed;

            return !hasParsingErrors;
        }
        
        public void Parse() 
        {
            var sourceFiles = DriverOptions.Modules.SelectMany(m => m.Headers);
            
            ParseSourceFiles(sourceFiles);

            foreach (var module in DriverOptions.Modules)
            {
                module.Units.Clear();
            }
            var cleanupPass = new PolarCleanUnitPass() {Context = this.Context, DefaultModule = this.mainModule};
            cleanupPass.VisitASTContext(this.Context.ASTContext);
            
            //DriverOptions.Modules.RemoveAll(m => m != DriverOptions.SystemModule && !m.Units.GetGenerated().Any());
        }

        public bool TryParse()
        {
            var limit = 10;
            while (limit > 0)
            {
                try
                {
                    Parse();
                    return true;
                }
                catch (System.Exception e)
                {
                    --limit;
                }
            }

            return false;
        }
        public void SortModulesByDependencies()
        {
            var sortedModules = DriverOptions.Modules.TopologicalSort(m =>
            {
                var dependencies = (from library in Context.Symbols.Libraries
                    where m.Libraries.Contains(library.FileName)
                    from module in DriverOptions.Modules
                    where library.Dependencies.Intersect(module.Libraries).Any()
                    select module).ToList();
                if (m != DriverOptions.SystemModule)
                    m.Dependencies.Add(DriverOptions.SystemModule);
                m.Dependencies.AddRange(dependencies);
                return m.Dependencies;
            });
            DriverOptions.Modules.Clear();
            DriverOptions.Modules.AddRange(sortedModules);
        }

        public bool ParseLibraries()
        {
            ClangParser.LibraryParsed += OnFileParsed;
            foreach (var module in Options.Modules)
            {
                using (var linkerOptions = new LinkerOptions(LinkerOptions))
                {
                    foreach (var libraryDir in module.LibraryDirs)
                        linkerOptions.AddLibraryDirs(libraryDir);

                    foreach (string library in module.Libraries)
                    {
                        if (Context.Symbols.Libraries.Any(l => l.FileName == library))
                            continue;
                        linkerOptions.AddLibraries(library);
                    }

                    using (var res = ClangParser.ParseLibrary(linkerOptions))
                    {
                        if (res.Kind != ParserResultKind.Success)
                            continue;

                        for (uint i = 0; i < res.LibrariesCount; i++)
                            Context.Symbols.Libraries.Add(ClangParser.ConvertLibrary(res.GetLibraries(i)));
                    }
                }
            }
            ClangParser.LibraryParsed -= OnFileParsed;

            Context.Symbols.IndexSymbols();
            SortModulesByDependencies();

            return true;
        }

        void OnFileParsed(string file, ParserResult result)
        {
            OnFileParsed(new[] {file}, result);
        }
        
        PolarGenerateExtSymbolsPass PolarGenerateExtSymbolsPass = new PolarGenerateExtSymbolsPass();
        
        // Difference with CppSharp defaults:
        // No Library, we are generating everything explicitly, not making an api command.
        // Options.IsCSharpGenerator is always true, since that is what we are doing.
        // We have our own GenerateSymbolsPoss: PolarGenerateSymbolsPass.
        // We have a state retaining SymbolsPass for the multiple passes of passes.: PolarGenerateExtSymbolsPass.
        // We do NOT change parameterless functions to instance methods: FunctionToInstanceMethodPass
        // We have better dealings with the CPP Sharp Macros CS_INGNORE, CS_IN, CS_OUT: PolarCheckMacroPass
        // We do NOT change GetXXX() SetXXX(x) to C# properties: GetterSetterToPropertyPass
        // We do NOT rename classes and methods to camel case: RenameDeclsUpperCase
        public void SetupPasses()
        {
            var TranslationUnitPasses = Context.TranslationUnitPasses;
            TranslationUnitPasses.Passes.Clear();
            TranslationUnitPasses.AddPass(new ResolveIncompleteDeclsPass());
            TranslationUnitPasses.AddPass(new IgnoreSystemDeclarationsPass());
            
            if (Options.IsCSharpGenerator)
                TranslationUnitPasses.AddPass(new EqualiseAccessOfOverrideAndBasePass());
            
            TranslationUnitPasses.AddPass(new CheckIgnoredDeclsPass());
            
            if (Options.IsCSharpGenerator)
            {
                TranslationUnitPasses.AddPass(new TrimSpecializationsPass());
                //TranslationUnitPasses.AddPass(new CheckAmbiguousFunctions());
                //TranslationUnitPasses.AddPass(new GenerateSymbolsPass());
                TranslationUnitPasses.AddPass(new PolarGenerateSymbolsPass());
                TranslationUnitPasses.AddPass(PolarGenerateExtSymbolsPass);
                TranslationUnitPasses.AddPass(new CheckIgnoredDeclsPass());
            }
            
            //library.SetupPasses(this);
            
            TranslationUnitPasses.AddPass(new FindSymbolsPass());
            //TranslationUnitPasses.AddPass(new CheckMacroPass());
            TranslationUnitPasses.AddPass(new PolarCheckMacroPass());
            TranslationUnitPasses.AddPass(new CheckStaticClass());

            if (Options.IsCLIGenerator || Options.IsCSharpGenerator)
            {
                TranslationUnitPasses.AddPass(new MoveFunctionToClassPass());
            }

            TranslationUnitPasses.AddPass(new CheckAmbiguousFunctions());
            TranslationUnitPasses.AddPass(new ConstructorToConversionOperatorPass());
            TranslationUnitPasses.AddPass(new MarshalPrimitivePointersAsRefTypePass());

            if (Options.IsCLIGenerator || Options.IsCSharpGenerator)
            {
                TranslationUnitPasses.AddPass(new CheckOperatorsOverloadsPass());
            }

            TranslationUnitPasses.AddPass(new CheckVirtualOverrideReturnCovariance());
            TranslationUnitPasses.AddPass(new CleanCommentsPass());
            
            Generator.SetupPasses();
            
            TranslationUnitPasses.AddPass(new FlattenAnonymousTypesToFields());
            TranslationUnitPasses.AddPass(new CleanInvalidDeclNamesPass());
            TranslationUnitPasses.AddPass(new FieldToPropertyPass());
            TranslationUnitPasses.AddPass(new CheckIgnoredDeclsPass());
            TranslationUnitPasses.AddPass(new CheckFlagEnumsPass());
            //TranslationUnitPasses.AddPass(new MakeProtectedNestedTypesPublicPass());

            if (Options.IsCSharpGenerator)
            {
                TranslationUnitPasses.AddPass(new GenerateAbstractImplementationsPass());
                TranslationUnitPasses.AddPass(new MultipleInheritancePass());
            }

            if (Options.IsCLIGenerator || Options.IsCSharpGenerator)
            {
                TranslationUnitPasses.AddPass(new DelegatesPass());
                //TranslationUnitPasses.AddPass(new GetterSetterToPropertyPass());
            }

            TranslationUnitPasses.AddPass(new StripUnusedSystemTypesPass());

            if (Options.IsCSharpGenerator)
            {
                TranslationUnitPasses.AddPass(new SpecializationMethodsWithDependentPointersPass());
                TranslationUnitPasses.AddPass(new ParamTypeToInterfacePass());
            }

            TranslationUnitPasses.AddPass(new CheckDuplicatedNamesPass());

            TranslationUnitPasses.AddPass(new MarkUsedClassInternalsPass());

            if (Options.IsCLIGenerator || Options.IsCSharpGenerator)
            {
                //TranslationUnitPasses.RenameDeclsUpperCase(RenameTargets.Any & ~RenameTargets.Parameter);
                TranslationUnitPasses.AddPass(new CheckKeywordNamesPass());
            }

            Context.TranslationUnitPasses.AddPass(new HandleVariableInitializerPass());
        }

        public List<GeneratorOutput> generate()
        {
            List<GeneratorOutput> outputs = Generator.Generate();
            foreach (var output in outputs)
            {
                var name = SaveFile(output);
                Diagnostics.Message("Saved '{0}'", name);
            }

            return outputs;
        }

        public void runPasses()
        {
            Context.RunPasses();
        }

        // Differences with CppSharp Defaults.
        // CppSharp:
        //    ProcessCode:
        //       Sets up passes, and parses code, runs the passes,
        //       generates the code (CLI or C#),
        //       optionally compiles the C++ code.
        // In contrast:
        // We effectively parse the entire given code base twice: once to find
        // out what templates we need (Optional<T>, VectorHolder<T>), generate
        // code for those templates, and then parse everything again to get
        // ready for code generation.
        //
        // PassOne:
        //   Set up the passes and the type maps and add the main module files and parse.
        // PassTwo: Run the passes on the parsed AST.
        // PassThree:
        //   If we generated any external symbols, then we must recompile the 
        //   whole kit and kabootle with the generated symbols.
        //   It's just easier that way, then trying to generate the needed code for the
        //   extra headers on the first pass. We let this tool do it.
        //   The second pass differs in that we remove the pass that generates the
        //   extra symbols, since we've already done that and we don't want to clobber
        //   the file: removing PolarExtGenerateSymbolsPass.
        //   We add the Ext module which now contains the generated headers.
        //   Set up the passes, and typemaps, parse again.
        //   Then run the passes again.
        //   At this point we should have a generatable tree.
        // PassFour:
        //    Generate the C# and symbols files.  We are only using the CSharpGenerator.
        // We do not have a compile C++ option. We are just generating the C# what we need.
        
        public void passOne()
        {
            SetupPasses();
            SetupTypeMaps();
            DriverOptions.AddModule(mainModule);
            Diagnostics.Message("Pass 1: Parsing Code ...");
            Parse();
        }

        public void passTwo()
        {
            Diagnostics.Message("Pass 2: Generating External Symbols ...");
            runPasses();
        }

        public bool TryRunPasses()
        {
            try
            {
                runPasses();
                return true;
            }
            catch (System.Exception e)
            {
                return false;
            }
        }

        // There is some bizzare error in which things get messed up on the C++ side, so things like
        // decl.GetItems(i) or decl.GetClasses(i), etc. return the wrong thing, and sometimes they are
        // coerced to null. This situation causes NullPointer exceptions down the road, or other errors.
        // It seems, I think, to be heap releated and maybe resource dependent. This situation, of course,
        // leaves the problem really hard to find. So, I punt. The fail in the second parsing will happen
        // in ASTConvert and usually come upon a .GetXXX(i) as WWWWW that unexpectantly gets coerced to null.
        // So, the parser will try some number of times until it makes it through. Gawd aweful, I know.
        // If it makes it through the parser, and fails in the passes, then we must reparse. 
        public bool passThree()
        {
            if (PolarGenerateExtSymbolsPass.PathOfGeneratedSymbolsFile != null)
            {
                Diagnostics.Message("Pass 3: Reparsing Code ...");
                mainModule.Headers.Add(Path.GetFullPath(PolarGenerateExtSymbolsPass.PathOfGeneratedSymbolsFile));
                var limit = 10;
                while (limit > 0)
                {
                    SetupPasses();
                    SetupTypeMaps();
                    if (TryParse())
                    {
                        Context.TranslationUnitPasses.Passes.Remove(PolarGenerateExtSymbolsPass);
                        if (TryRunPasses())
                        {
                            Context.Options.Modules.Remove(ExtModule);
                            return true;
                        }
                    }

                    limit--;
                }

                Diagnostics.Message("Pass 3: Error. Cannot survive errors.");

                return false;
            }
            else
            {
                Diagnostics.Message("Pass 3: No action needed.");
                return true;
            }

        }

        public void passFour()
        {
            Diagnostics.Message("Pass 4: Generating C# code.");
            var outputs = generate();
        }

        public void run()
        {
            passOne();
            passTwo();
            if (passThree())
            {
                passFour();
            }
            else
            {
                Diagnostics.Message("Error: Could not surive errors");
            }
            Diagnostics.Message("Done.");
        }

        public string SaveFile(GeneratorOutput output)
        {
            var outputPath = Path.GetFullPath(DriverOptions.OutputDir);

            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            if (output.TranslationUnit.IsValid)
            {
                var filePath = output.TranslationUnit.FilePath;
                var fileBase = output.TranslationUnit.FileNameWithoutExtension;

                if (DriverOptions.UseHeaderDirectories)
                {
                    var dir = Path.Combine(outputPath, output.TranslationUnit.FileRelativeDirectory);
                    Directory.CreateDirectory(dir);
                    fileBase = Path.Combine(output.TranslationUnit.FileRelativeDirectory, fileBase);
                }

                if (DriverOptions.GenerateName != null)
                    fileBase = DriverOptions.GenerateName(output.TranslationUnit);

                foreach (var template in output.Outputs)
                {
                    var fileRelativePath = $"{fileBase}.{template.FileExtension}";

                    var file = Path.Combine(outputPath, fileRelativePath);
                    WriteGeneratedCodeToFile(file, template.Generate());

                    if (output.TranslationUnit.Module != null)
                        output.TranslationUnit.Module.CodeFiles.Add(file);

                    Diagnostics.Message("Generated '{0}'", fileRelativePath);
                }
                return filePath;
            }
            return null;
        }

        private void WriteGeneratedCodeToFile(string file, string generatedCode)
        {
            var fi = new FileInfo(file);

            if (!fi.Exists || fi.Length != generatedCode.Length ||
                File.ReadAllText(file) != generatedCode)
                File.WriteAllText(file, generatedCode);
        }

        public void AddLibraryDirectory(string s)
        {
            LinkerOptions.AddLibraryDirs(s);
        }

        public void SetDebug()
        {
            Diagnostics.Level = DiagnosticKind.Debug;
        }
    }
}
