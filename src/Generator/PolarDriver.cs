
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

        public ParserOptions ParserOptions;
        public BindingContext Context;
        public Generators.Generator Generator;
        public PolarDriverOptions DriverOptions;
        private bool hasParsingErrors;

        public bool HasCompilationErrors { get; set; }

        private ClangParser parser;

        private Module ExtModule = new Module("Ext");
        
        public PolarDriver()
        {
            DriverOptions = new PolarDriverOptions();
            ParserOptions = new ParserOptions();
            Context = new BindingContext(DriverOptions, ParserOptions);
            Context.PolarFixesEnabled = true;
            ExtModule.SymbolsLibraryName = "Ext-symbols";
            DriverOptions.AddModule(ExtModule);
            DriverOptions.GenerateClassTemplates = true;
            DriverOptions.UseHeaderDirectories = false;
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
                ParserOptions.TargetTriple = architecture + "-windows-msvc";
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

        public bool ParseSourceFiles(ClangParser parser, IEnumerable<string> sourceFiles)
        {
            foreach (var sourceFile in sourceFiles)
            {
                using (var parserOptions = ParserOptions.BuildForSourceFile(
                    DriverOptions.Modules, sourceFile))
                {
                    using (ParserResult result = parser.ParseSourceFile(
                        sourceFile, parserOptions))
                        if (Context.TargetInfo == null)
                            Context.TargetInfo = result.TargetInfo;
                        else if (result.TargetInfo != null)
                            result.TargetInfo.Dispose();
                    if (string.IsNullOrEmpty(ParserOptions.TargetTriple))
                        ParserOptions.TargetTriple = parserOptions.TargetTriple;
                }
            }

            return !hasParsingErrors;
        }
        
        public void Parse() 
        {
            var astContext = new Parser.AST.ASTContext();
            var parser = new ClangParser(astContext);
            parser.SourcesParsed += OnFileParsed;

            var sourceFiles = DriverOptions.Modules.SelectMany(m => m.Headers);
            
            ParseSourceFiles(parser, sourceFiles);
            
            Context.ASTContext = ClangParser.ConvertASTContext(astContext);

            foreach (var module in DriverOptions.Modules)
            {
                module.Units.Clear();
            }
            var cleanupPass = new PolarCleanUnitPass() {Context = this.Context, DefaultModule = this.mainModule};
            cleanupPass.VisitASTContext(this.Context.ASTContext);
            
            //DriverOptions.Modules.RemoveAll(m => m != DriverOptions.SystemModule && !m.Units.GetGenerated().Any());
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
            foreach (var module in DriverOptions.Modules)
            {
                foreach (var libraryDir in module.LibraryDirs)
                    ParserOptions.AddLibraryDirs(libraryDir);

                foreach (var library in module.Libraries)
                {
                    if (Context.Symbols.Libraries.Any(l => l.FileName == library))
                        continue;

                    var parser = new ClangParser();
                    parser.LibraryParsed += OnFileParsed;

                    using (var res = parser.ParseLibrary(library, ParserOptions))
                    {
                        if (res.Kind != ParserResultKind.Success)
                            continue;

                        Context.Symbols.Libraries.Add(ClangParser.ConvertLibrary(res.Library));
                    }
                }
            }

            Context.Symbols.IndexSymbols();
            SortModulesByDependencies();

            return true;
        }

        void OnFileParsed(string file, ParserResult result)
        {
            OnFileParsed(new[] {file}, result);
        }
        
        PolarGenerateExtSymbolsPass PolarGenerateExtSymbolsPass = new PolarGenerateExtSymbolsPass();
        
        public void SetupPasses()
        {
            var TranslationUnitPasses = Context.TranslationUnitPasses;
            TranslationUnitPasses.Passes.Clear();
            TranslationUnitPasses.AddPass(new ResolveIncompleteDeclsPass());
            TranslationUnitPasses.AddPass(new IgnoreSystemDeclarationsPass());
            TranslationUnitPasses.AddPass(new EqualiseAccessOfOverrideAndBasePass());
            TranslationUnitPasses.AddPass(new CheckIgnoredDeclsPass());
            TranslationUnitPasses.AddPass(new TrimSpecializationsPass());
            TranslationUnitPasses.AddPass(new PolarGenerateSymbolsPass());
            TranslationUnitPasses.AddPass(PolarGenerateExtSymbolsPass);
            TranslationUnitPasses.AddPass(new CheckIgnoredDeclsPass());
            TranslationUnitPasses.AddPass(new FunctionToInstanceMethodPass());
            TranslationUnitPasses.AddPass(new MarshalPrimitivePointersAsRefTypePass());
            TranslationUnitPasses.AddPass(new FindSymbolsPass());
            TranslationUnitPasses.AddPass(new PolarCheckMacroPass());
            TranslationUnitPasses.AddPass(new CheckStaticClass());
            TranslationUnitPasses.AddPass(new MoveFunctionToClassPass());
            TranslationUnitPasses.AddPass(new CheckAmbiguousFunctions());
            TranslationUnitPasses.AddPass(new ConstructorToConversionOperatorPass());
            TranslationUnitPasses.AddPass(new MarshalPrimitivePointersAsRefTypePass());
            TranslationUnitPasses.AddPass(new CheckOperatorsOverloadsPass());
            TranslationUnitPasses.AddPass(new CheckVirtualOverrideReturnCovariance());
            TranslationUnitPasses.AddPass(new CleanCommentsPass());
            TranslationUnitPasses.AddPass(new FlattenAnonymousTypesToFields());
            TranslationUnitPasses.AddPass(new CleanInvalidDeclNamesPass());
            TranslationUnitPasses.AddPass(new FieldToPropertyPass());
            TranslationUnitPasses.AddPass(new CheckIgnoredDeclsPass());
            TranslationUnitPasses.AddPass(new CheckFlagEnumsPass());
            //TranslationUnitPasses.AddPass(new MakeProtectedNestedTypesPublicPass());
            TranslationUnitPasses.AddPass(new GenerateAbstractImplementationsPass());
            TranslationUnitPasses.AddPass(new MultipleInheritancePass());
            TranslationUnitPasses.AddPass(new DelegatesPass());
            TranslationUnitPasses.AddPass(new StripUnusedSystemTypesPass());
            TranslationUnitPasses.AddPass(new SpecializationMethodsWithDependentPointersPass());
            TranslationUnitPasses.AddPass(new ParamTypeToInterfacePass());
            TranslationUnitPasses.AddPass(new CheckDuplicatedNamesPass());
            TranslationUnitPasses.AddPass(new MarkUsedClassInternalsPass());
            TranslationUnitPasses.AddPass(new CheckKeywordNamesPass());
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

        public void passOne()
        {
            SetupPasses();
            SetupTypeMaps();
            DriverOptions.AddModule(mainModule);
            Diagnostics.Message("Parsing code...");
            Parse();
        }

        public void passTwo()
        {
            runPasses();
        }

        public void passThree()
        {
            if (PolarGenerateExtSymbolsPass.PathOfGeneratedSymbolsFile != null)
            {
                mainModule.Headers.Add(Path.GetFullPath(PolarGenerateExtSymbolsPass.PathOfGeneratedSymbolsFile));
                SetupPasses();
                SetupTypeMaps();
                Parse();
                Context.TranslationUnitPasses.Passes.Remove(PolarGenerateExtSymbolsPass);
                Diagnostics.Level = DiagnosticKind.Debug;
                runPasses();
                Context.Options.Modules.Remove(ExtModule);
            }
        }

        public void passFour()
        {
            var outputs = generate();
        }

        public void run()
        {
            passOne();
            passTwo();
            passThree();
            passFour();
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
                    var fileRelativePath = string.Format("{0}.{1}", fileBase, template.FileExtension);

                    var file = Path.Combine(outputPath, fileRelativePath);
                    File.WriteAllText(file, template.Generate());
                    output.TranslationUnit.Module.CodeFiles.Add(file);

                    Diagnostics.Message("Generated '{0}'", fileRelativePath);
                }

                return filePath;
            }

            return null;
        }

        public void AddLibraryDirectory(string s)
        {
            ParserOptions.AddLibraryDirs(s);
        }
    }
}
