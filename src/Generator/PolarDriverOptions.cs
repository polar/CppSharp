using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CppSharp.AST;
using CppSharp.Generators;

namespace CppSharp
{
    public class PolarDriverOptions : DriverOptions
    {
        public Module AddModule(Module module)
        {
            Modules.Add(module);
            return module;
        }

        public  string VectorHolderPath;
        public void SetVectorHolderPath(string path)
        {
            VectorHolderPath = path;
        }

        public string VectorHolderName;

        public void SetVectorHolderName(string name)
        {
            VectorHolderName = name;
        }

        public  string OptionalPath;
        public void SetOptionalPath(string path)
        {
            OptionalPath = path;
        }

        public string OptionalName;

        public void SetOptionalName(string name)
        {
            OptionalName = name;
        }
    }
}