using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CppSharp.AST;
using CppSharp.AST.Extensions;
using CppSharp.Generators;
using CppSharp.Parser;
using CppSharp.Types;
using CppSharp.Utils;

namespace CppSharp.Passes
{
    public class PolarGenerateExtSymbolsPass : TranslationUnitPass
    {
        public PolarGenerateExtSymbolsPass()
        {
            VisitOptions.VisitClassBases = false;
            VisitOptions.VisitClassFields = true;
            VisitOptions.VisitClassTemplateSpecializations = false;
            VisitOptions.VisitEventParameters = false;
            VisitOptions.VisitFunctionParameters = false;
            VisitOptions.VisitFunctionReturnType = false;
            VisitOptions.VisitNamespaceEnums = false;
            VisitOptions.VisitNamespaceEvents = false;
            VisitOptions.VisitNamespaceTemplates = false;
            VisitOptions.VisitNamespaceTypedefs = false;
            VisitOptions.VisitNamespaceVariables = false;
            VisitOptions.VisitTemplateArguments = false;
        }


        private Module extModule;

        Module getExtModule()
        {
            if (extModule == null)
            {
                var modules = Options.Modules;
                foreach (var module in modules)
                {
                    if (module.LibraryName == "Ext")
                    {
                        extModule = module;
                        return extModule;
                    }
                }
            }
            return extModule;
        }
        
        public override bool VisitASTContext(ASTContext context)
        {
            var result = base.VisitASTContext(context);
            
            var modules = Options.Modules.Where(symbolsCodeGenerators.ContainsKey).ToList();
            var findSymbolsPass = Context.TranslationUnitPasses.FindPass<FindSymbolsPass>();
            GenerateSymbols();
            if (remainingCompilationTasks > 0)
                if (findSymbolsPass != null)
                    findSymbolsPass.Wait = true;
            return result;
        }

        public event EventHandler<SymbolsCodeEventArgs> SymbolsCodeGenerated;

        Module findStdModuleIfStdSpecializations(List<Module> modules)
        {
            foreach (var module in modules)
            {
                if (specializations.ContainsKey(module) && module.LibraryName == "Std")
                {
                    return module;
                }
            }
            return null;
        }

        public string PathOfGeneratedSymbolsFile;
        
        private void GenerateSymbols()
        {
            var modules = Options.Modules;
            remainingCompilationTasks = 1;
            var symbolsCodeGenerator = GetSymbolsCodeGenerator(getExtModule());
            var stdModule = findStdModuleIfStdSpecializations(modules);
            // We get any symbols from the Std module, namely std::vector instantiations.
            if (stdModule != null)
            {
                symbolsCodeGenerator.NewLine();
                foreach (var specialization in specializations[stdModule])
                {
                    Func<Method, bool> exportable = m => !m.IsDependent &&
                                                         !m.IsImplicit && !m.IsDeleted && !m.IsDefaulted;
                    if (specialization.Methods.Any(m => m.IsInvalid && exportable(m)))
                        foreach (var method in specialization.Methods.Where(
                            m => m.IsGenerated && (m.InstantiatedFrom == null || m.InstantiatedFrom.IsGenerated) &&
                                 exportable(m)))
                            symbolsCodeGenerator.VisitMethodDecl(method);
                    else
                        symbolsCodeGenerator.VisitClassTemplateSpecializationDecl(specialization);
                }
            }
            var cpp = $"{getExtModule().SymbolsLibraryName}.{symbolsCodeGenerator.FileExtension}";
            Directory.CreateDirectory(Options.OutputDir);
            var path = Path.Combine(Options.OutputDir, cpp);
            File.WriteAllText(path, symbolsCodeGenerator.Generate());
            PathOfGeneratedSymbolsFile = path;
            
            var e = new SymbolsCodeEventArgs(getExtModule());
            SymbolsCodeGenerated?.Invoke(this, e);
            if (string.IsNullOrEmpty(e.CustomCompiler))
                RemainingCompilationTasks--;
            else
                InvokeCompiler(e.CustomCompiler, e.CompilerArguments,
                    e.OutputDir, getExtModule());

        }

        public override bool VisitClassDecl(Class @class)
        {
            if (!base.VisitClassDecl(@class))
                return false;

            if (@class.IsDependent)
                foreach (var specialization in @class.Specializations.Where(
                    s => s.IsExplicitlyGenerated))
                    specialization.Visit(this);
            else
                CheckBasesForSpecialization(@class);

            return true;
        }
        private static bool UnsupportedTemplateArgument(
            ClassTemplateSpecialization specialization, TemplateArgument a, ITypeMapDatabase typeMaps)
        {
            if (a.Type.Type == null ||
                ASTUtils.IsTypeExternal(specialization.TranslationUnit.Module, a.Type.Type))
            {
                System.Console.WriteLine($"UnspportedTemplateArgument External {a.Type.Type}!");
                return true;
            }

            var typeIgnoreChecker = new TypeIgnoreChecker(typeMaps);
            a.Type.Type.Visit(typeIgnoreChecker);
            System.Console.WriteLine($"UnspportedTemplateArgument {a.Type.Type} isIgnored {typeIgnoreChecker.IsIgnored}!");
            return typeIgnoreChecker.IsIgnored;
        }
        // The purpuse of this class is to see if we need to specialize a vector class.
        // Since we are generating a library, we want all possible classes, so if one doesn't exist
        // then add by returnning true.
        private static bool IsVectorSpecializationNeeded(Declaration container,
            ITypeMapDatabase typeMaps, bool internalOnly, AST.Type type,
            ClassTemplateSpecialization specialization)
        {
            TypeMap typeMap;
            typeMaps.FindTypeMap(type, out typeMap);

            if (typeMap != null)
            {
                var typePrinter = new CppSharp.Generators.CSharp.CSharpTypePrinter(typeMap.Context); 
                System.Console.WriteLine($"IsVectorSpecializationNeeded: {specialization.Visit(typePrinter).Type}");
                if (typeMap is CppSharp.Types.Ext.Vector)
                {
                    if (specialization.TemplatedDecl.TemplatedClass.QualifiedOriginalName == "std::vector")
                    {
                        if (specialization.Arguments.Any(a => UnsupportedTemplateArgument(
                            specialization, a, typeMaps)))
                        {
                            System.Console.WriteLine($"IsVectorSpecializationNeeded: Returning false for {specialization.Visit(typePrinter).Type}");
                            return false;
                        }
                    }
                }
            }
            return true;
        }
        private static bool IsOptionalSpecializationNeeded(Declaration container,
            ITypeMapDatabase typeMaps, bool internalOnly, AST.Type type,
            ClassTemplateSpecialization specialization)
        {
            TypeMap typeMap;
            typeMaps.FindTypeMap(type, out typeMap);

            if (typeMap != null)
            {
                var typePrinter = new CppSharp.Generators.CSharp.CSharpTypePrinter(typeMap.Context); 
                System.Console.WriteLine($"IsOptionalSpecializationNeeded: {specialization.Visit(typePrinter).Type}");
                
                if (specialization.TemplatedDecl.TemplatedClass.QualifiedOriginalName == "Interop::Optional")
                {
                    if (specialization.Arguments.Any(a => UnsupportedTemplateArgument(
                        specialization, a, typeMaps)))
                    {
                        System.Console.WriteLine($"IsOptionalSpecializationNeeded: Returning false for  {specialization.Visit(typePrinter).Type}");
                        return false;
                    }
                }
            }
            return true;
        }
        
        public static bool CheckTypeForVectorSpecialization(AST.Type type, Declaration container,
            Action<ClassTemplateSpecialization> addSpecialization,
            ITypeMapDatabase typeMaps, bool internalOnly = false)
        {
            type = type.Desugar();
            type = (type.GetFinalPointee() ?? type).Desugar();
            var listType = type as TemplateSpecializationType;
            if (listType != null)
            {
                ClassTemplateSpecialization specialization = GetParentSpecialization(listType);
                if (specialization == null)
                    return true;

                if (IsVectorSpecializationNeeded(container, typeMaps, internalOnly,
                    type, specialization))
                    return false;

                if (!ASTUtils.CheckTypeForSpecialization(specialization.Arguments[0].Type.Type, specialization, addSpecialization,
                    typeMaps, internalOnly))
                    return false;

                addSpecialization(specialization);
                return true;
            }

            return false;
        }
        
        public static bool CheckTypeForOptionalSpecialization(AST.Type type, Declaration container,
            Action<ClassTemplateSpecialization> addSpecialization,
            ITypeMapDatabase typeMaps, bool internalOnly = false)
        {
            type = type.Desugar();
            type = (type.GetFinalPointee() ?? type).Desugar();
            var listType = type as TemplateSpecializationType;
            if (listType != null)
            {
                ClassTemplateSpecialization specialization = GetParentSpecialization(listType);
                if (specialization == null)
                    return true;

                if (IsOptionalSpecializationNeeded(container, typeMaps, internalOnly,
                    type, specialization))
                    return false;

                if (!ASTUtils.CheckTypeForSpecialization(specialization.Arguments[0].Type.Type, specialization, addSpecialization,
                    typeMaps, internalOnly))
                    return false;

                addSpecialization(specialization);
                return true;
            }

            return false;
        }
        private static ClassTemplateSpecialization GetParentSpecialization(AST.Type type)
        {
            Declaration declaration;
            if (type.TryGetDeclaration(out declaration))
            {
                ClassTemplateSpecialization specialization = null;
                do
                {
                    specialization = declaration as ClassTemplateSpecialization;
                    declaration = declaration.Namespace;
                } while (declaration != null && specialization == null);
                return specialization;
            }
            return null;
        }

        public override bool VisitFunctionDecl(Function function)
        {
            if (!base.VisitFunctionDecl(function))
                return false;

            var module = function.TranslationUnit.Module;

            if (function.IsGenerated)
            {
                CheckTypeForVectorSpecialization(function.OriginalReturnType.Type,
                    function, Add, Context.TypeMaps);
                CheckTypeForOptionalSpecialization(function.OriginalReturnType.Type,
                    function, Add, Context.TypeMaps);
                foreach (var parameter in function.Parameters)
                {
                    CheckTypeForVectorSpecialization(parameter.Type, function,
                        Add, Context.TypeMaps);
                    CheckTypeForOptionalSpecialization(parameter.Type, function,
                        Add, Context.TypeMaps);
                }
            }

            if (!NeedsSymbol(function))
                return false;

            var symbolsCodeGenerator = GetSymbolsCodeGenerator(module);
            return function.Visit(symbolsCodeGenerator);
        }

        public override bool VisitFieldDecl(Field field)
        {
            base.VisitFieldDecl(field);
            var module = field.TranslationUnit.Module;
            // We ignore stuff defined in Std.
            if (module == Options.SystemModule)
                return false;
            if (field.Type is BuiltinType)
                return true;
            CheckTypeForVectorSpecialization(field.Type,
                field, Add, Context.TypeMaps);
            var symbolsCodeGenerator = GetSymbolsCodeGenerator(module);
            return field.Visit(symbolsCodeGenerator);
        }

        public class SymbolsCodeEventArgs : EventArgs
        {
            public SymbolsCodeEventArgs(Module module)
            {
                this.Module = module;
            }

            public Module Module { get; set; }
            public string CustomCompiler { get; set; }
            public string CompilerArguments { get; set; }
            public string OutputDir { get; set; }
        }

        private bool NeedsSymbol(Function function)
        {
            var mangled = function.Mangled;
            var method = function as Method;
            return function.IsGenerated && !function.IsDeleted &&
                !function.IsDependent && !function.IsPure &&
                (!string.IsNullOrEmpty(function.Body) || function.IsImplicit) &&
                !(function.Namespace is ClassTemplateSpecialization) &&
                // we don't need symbols for virtual functions anyway
                (method == null || (!method.IsVirtual && !method.IsSynthetized &&
                 (!method.IsConstructor || !((Class) method.Namespace).IsAbstract))) &&
                // we cannot handle nested anonymous types
                (!(function.Namespace is Class) || !string.IsNullOrEmpty(function.Namespace.OriginalName)) &&
                !Context.Symbols.FindSymbol(ref mangled);
        }

        private PolarSymbolsCodeGenerator GetSymbolsCodeGenerator(Module module)
        {
            // The general GenerateSymbolsPass may have assigned a code generator.
            // We want ours
            if (symbolsCodeGenerators.ContainsKey(module))
            {
                var gen = symbolsCodeGenerators[module];
                if (gen is PolarSymbolsExtCodeGenerator)
                    return gen;
            }
            
            var symbolsCodeGenerator = new PolarSymbolsExtCodeGenerator(Context, module.Units);
            symbolsCodeGenerators[module] = symbolsCodeGenerator;
            symbolsCodeGenerator.Process();

            return symbolsCodeGenerator;
        }

        private void InvokeCompiler(string compiler, string arguments, string outputDir, Module module)
        {
            new Thread(() =>
                {
                    int error;
                    string errorMessage;
                    ProcessHelper.Run(compiler, arguments, out error, out errorMessage);
                    var output = GetOutputFile(module.SymbolsLibraryName);
                    if (!File.Exists(Path.Combine(outputDir, output)))
                        Diagnostics.Error(errorMessage);
                    else
                        compiledLibraries[module] = new CompiledLibrary
                            { OutputDir = outputDir, Library = module.SymbolsLibraryName };
                    RemainingCompilationTasks--;
                }).Start();
        }

        private void CheckBasesForSpecialization(Class @class)
        {
            foreach (var @base in @class.Bases.Where(b => b.IsClass))
            {
                var specialization = @base.Class as ClassTemplateSpecialization;
                if (specialization != null && !specialization.IsExplicitlyGenerated &&
                    specialization.SpecializationKind != TemplateSpecializationKind.ExplicitSpecialization)
                    ASTUtils.CheckTypeForSpecialization(@base.Type, @class, Add, Context.TypeMaps);
                CheckBasesForSpecialization(@base.Class);
            }
        }

        private void Add(ClassTemplateSpecialization specialization)
        {
            ICollection<ClassTemplateSpecialization> specs;
            if (specializations.ContainsKey(specialization.TranslationUnit.Module))
                specs = specializations[specialization.TranslationUnit.Module];
            else specs = specializations[specialization.TranslationUnit.Module] =
                new HashSet<ClassTemplateSpecialization>();
            if (!specs.Contains(specialization))
            {
                specs.Add(specialization);
                foreach (Method method in specialization.Methods)
                    method.Visit(this);
            }
            GetSymbolsCodeGenerator(getExtModule());
        }

        private int RemainingCompilationTasks
        {
            get { return remainingCompilationTasks; }
            set
            {
                if (remainingCompilationTasks != value)
                {
                    remainingCompilationTasks = value;
                    if (remainingCompilationTasks == 0)
                    {
                        foreach (var module in Context.Options.Modules.Where(compiledLibraries.ContainsKey))
                        {
                            CompiledLibrary compiledLibrary = compiledLibraries[module];
                            CollectSymbols(compiledLibrary.OutputDir, compiledLibrary.Library);
                        }
                        var findSymbolsPass = Context.TranslationUnitPasses.FindPass<FindSymbolsPass>();
                        if (findSymbolsPass != null)
                            findSymbolsPass.Wait = false;
                    }
                }
            }
        }

        private void CollectSymbols(string outputDir, string library)
        {
            using (var parserOptions = new ParserOptions())
            {
                parserOptions.AddLibraryDirs(outputDir);
                var output = GetOutputFile(library);
                parserOptions.LibraryFile = output;
                using (var parserResult = Parser.ClangParser.ParseLibrary(parserOptions))
                {
                    if (parserResult.Kind == ParserResultKind.Success)
                    {
                        var nativeLibrary = ClangParser.ConvertLibrary(parserResult.Library);
                        lock (@lock)
                        {
                            Context.Symbols.Libraries.Add(nativeLibrary);
                            Context.Symbols.IndexSymbols();
                        }
                    }
                    else
                        Diagnostics.Error($"Parsing of {Path.Combine(outputDir, output)} failed.");
                }
            }
        }

        private static string GetOutputFile(string library)
        {
            return Path.GetFileName($@"{(Platform.IsWindows ?
                string.Empty : "lib")}{library}.{
                (Platform.IsMacOS ? "dylib" : Platform.IsWindows ? "dll" : "so")}");
        }

        private int remainingCompilationTasks;
        private static readonly object @lock = new object();

        private Dictionary<Module, PolarSymbolsCodeGenerator> symbolsCodeGenerators =
            new Dictionary<Module, PolarSymbolsCodeGenerator>();
        private Dictionary<Module, HashSet<ClassTemplateSpecialization>> specializations =
            new Dictionary<Module, HashSet<ClassTemplateSpecialization>>();
        private Dictionary<Module, CompiledLibrary> compiledLibraries = new Dictionary<Module, CompiledLibrary>();

        private class CompiledLibrary
        {
            public string OutputDir { get; set; }
            public string Library { get; set; }
        }
    }
}
