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

            if (TranslationUnit.Module == Options.SystemModule)
            {
                WriteLine("#include <string>");
                WriteLine("#include <vector>");
            }
            NewLine();
        }
    }
}
