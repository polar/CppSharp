using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using CppSharp.AST;
using CppSharp.AST.Extensions;
using CppSharp.Generators;
using CppSharp.Generators.C;

namespace CppSharp.Passes
{
    public class PolarSymbolsStdCodeGenerator : PolarSymbolsCodeGenerator
    {
        public PolarSymbolsStdCodeGenerator(BindingContext context, IEnumerable<TranslationUnit> units)
            : base(context, units)
        {
        }

        public override void Process()
        {
            WriteLine("#define _LIBCPP_DISABLE_VISIBILITY_ANNOTATIONS");
            WriteLine("#define _LIBCPP_HIDE_FROM_ABI");
            NewLine();

            System.Console.WriteLine($"Creating Std-symbols.cpp");
            if (TranslationUnit.Module == Options.SystemModule)
            {
                System.Console.WriteLine("#include <string>");
                System.Console.WriteLine("#include <vector>");
                WriteLine("#include <string>");
                WriteLine("#include <vector>");
            }
            WriteLine("#include \"{0}\"", ((PolarDriverOptions)Context.Options).VectorHolderPath);
            WriteLine("#include \"{0}\"", ((PolarDriverOptions)Context.Options).OptionalPath);
            // Windows will generate template specializations for the std::allocator. We need these. 
            foreach (var module in Options.Modules)
            {
                if (module != Options.SystemModule && module.LibraryName != "Ext")
                {
                    System.Console.WriteLine($"From Module {module.LibraryName}");
                    foreach (var header in module.Headers)
                    {
                        // The Ext library .cpp files got added to the main module, so we do not want those.
                        if (!header.EndsWith(".cpp"))
                        {
                            System.Console.WriteLine($"#include \"{header}\"");
                            WriteLine($"#include \"{header}\"");
                        }
                    }
                }
            }
            System.Console.WriteLine($"Created Std-symbols.cpp");
            NewLine();
        }
    }
}
