using System.Collections.Generic;
using System.Linq;
using System.Text;
using CppSharp.AST;
using CppSharp.AST.Extensions;
using CppSharp.Generators;
using CppSharp.Generators.AST;
using CppSharp.Generators.C;
using CppSharp.Generators.CLI;
using CppSharp.Generators.CSharp;

namespace CppSharp.Types.Ext
{
    [
        TypeMap("DayOfWeek", GeneratorKind = GeneratorKind.CSharp)
    ]
    public class DayOfWeek : TypeMap
    {
        public override bool IsIgnored
        {
            get { return false; }
        }
        
        public override bool DoesMarshalling
        {
            get { return false; }
        }
        public override Type CSharpSignatureType(TypePrinterContext ctx)
        {
            return new CustomType("System.DayOfWeek");
        }
    }
    [
        TypeMap("StringOptional", GeneratorKind = GeneratorKind.CSharp),
        TypeMap("global::ALK.Interop.StringOptional", GeneratorKind = GeneratorKind.CSharp)
    ]
    public class StringOptional : TypeMap
    {
        public override bool IsIgnored
        {
            get { return false; }
        }

        public override bool DoesMarshalling
        {
            get { return true; }
        }

        public override Type CSharpSignatureType(TypePrinterContext ctx)
        {
            if (ctx.Kind == TypePrinterContextKind.Managed)
            {
               return new CustomType("string");
            }

            Declaration basicString = GetBasicString(ctx.Type);
            var typePrinter = new CSharpTypePrinter(null);
            typePrinter.PushContext(TypePrinterContextKind.Native);
            return new CustomType(basicString.Visit(typePrinter).Type);
        }

        public override void CSharpMarshalToNative(CSharpMarshalContext ctx)
        {
            Type type = ctx.Parameter.Type.Desugar();
            Declaration basicString = GetBasicString(type);
            var typePrinter = new CSharpTypePrinter(ctx.Context);
            //if (!ctx.Parameter.Type.Desugar().IsAddress() &&
            //    ctx.MarshalKind != MarshalKind.NativeField)
            //    ctx.Return.Write($"*({typePrinter.PrintNative(basicString)}*) ");
            if (ctx.MarshalKind == MarshalKind.NativeField)
            {
            	var varBasicString = $"_tmp{ctx.ParameterIndex}";
            	ctx.Before.WriteLine($@"var {varBasicString} =  global::ALK.Interop.StringOptional.__CreateInstance(new global::System.IntPtr(&{ctx.ReturnVarName}));");
                ctx.Before.WriteLine($@"if (value != null)");
                ctx.Before.WriteOpenBraceAndIndent();
                    ctx.Before.WriteLine($@"{varBasicString}._M_payload = {ctx.Parameter.Name};");
                	ctx.Before.WriteLine($@"{varBasicString}._M_engaged = true;");;
            	ctx.Before.UnindentAndWriteCloseBrace();
                ctx.Before.WriteLine("else");
            	ctx.Before.WriteOpenBraceAndIndent();
                	 ctx.Before.WriteLine($@"{varBasicString}._M_engaged = false;");;
            	ctx.Before.UnindentAndWriteCloseBrace();
            	ctx.ReturnVarName = string.Empty;
            }
            else
            {
                var varBasicString = $"__basicString{ctx.ParameterIndex}";
                    ctx.Before.WriteLine($@"var {varBasicString} = {ctx.Parameter.Name} != null ? new {
                            basicString.Visit(typePrinter)}({ctx.Parameter.Name}) : new {
                            basicString.Visit(typePrinter)}();");
                var pointerType = type as PointerType;
                if (pointerType != null)
                {
                    ctx.Return.Write($"{varBasicString}.{Helpers.InstanceIdentifier}");
                }
                else
                {
                    ctx.Return.Write($"*({typePrinter.PrintNative(basicString)}*){varBasicString}.{Helpers.InstanceIdentifier}");
                }
                if (!type.IsPointer() || ctx.Parameter.IsIndirect)
                    ctx.Cleanup.WriteLine($@"{varBasicString}.Dispose({
                        (ctx.MarshalKind == MarshalKind.NativeField ? "false" : string.Empty)});");
            }
        }

        public override void CSharpMarshalToManaged(CSharpMarshalContext ctx)
        {
            Declaration basicString = GetBasicString(Type);
            var typePrinter = new CSharpTypePrinter(ctx.Context); 
            string qualifiedBasicString = GetQualifiedBasicString(basicString);
ctx.Before.WriteLine($"// *({typePrinter.PrintNative(basicString)}*)");
            var ptrBasicString = $"_ptr{ctx.ParameterIndex}";
            var varBasicString = $"_tmp{ctx.ParameterIndex}";
            var retBasicString = $"_ret{ctx.ParameterIndex}";
            //ctx.Before.WriteLine($@"var {ptrBasicString} =  {ctx.ReturnVarName};");
            ctx.Before.WriteLine($@"var {varBasicString} =  global::ALK.Interop.StringOptional.__CreateInstance({ctx.ReturnVarName});");

            //ctx.Before.WriteLine($@"var {varBasicString} =  (({typePrinter.PrintNative(basicString)}*){ctx.ReturnVarName});");
            ctx.Before.WriteLine("string {0} = null;", retBasicString);
            ctx.Before.WriteLine($@"if ({varBasicString}._M_engaged)");
            ctx.Before.WriteOpenBraceAndIndent();
                ctx.Before.WriteLine($@"{retBasicString} = {varBasicString}._M_payload;");
            ctx.Before.UnindentAndWriteCloseBrace();
            ctx.Return.Write(retBasicString);
        }

        private static string GetQualifiedBasicString(Declaration basicString)
        {
            // TODO:: Wonder why this isn't coming out as ALK.Interop
            return $"global::ALK.Interop.StringOptional";
        }

        private static Declaration GetBasicString(Type type)
        {
            var desugared = type.Desugar();
            var template = (desugared.GetFinalPointee() ?? desugared).Desugar();
            return ((TagType) template).Declaration;
        }

    }

    [
        TypeMap("Optional", GeneratorKind = GeneratorKind.CSharp),
    TypeMap("Interop::Optional", GeneratorKind = GeneratorKind.CSharp)
    ]
    public class Optional : TypeMap
    {
        public override bool IsIgnored
        {
            get { return false; }
        }

        public override bool DoesMarshalling
        {
            get { return true; }
        }

        public override Type CSharpSignatureType(TypePrinterContext ctx)
        {
            var templateType = Type as TemplateSpecializationType;
            var insideType = templateType.Arguments[0].Type;
            
            if (ctx.Kind == TypePrinterContextKind.Managed)
            {
                if (!IsPrimitiveType(insideType.Type))
                {
                    return new CustomType($"global::ALK.Interop.Optional<{ctx.GetTemplateParameterList()}>");
                }
                return new CustomType($"{ctx.GetTemplateParameterList()}?");
            }

            ClassTemplateSpecialization basicString = GetBasicString(ctx.Type);
            var typePrinter = new CSharpTypePrinter(null);
            typePrinter.PushContext(TypePrinterContextKind.Native);
            return new CustomType(basicString.Visit(typePrinter).Type);
        }

        public override void CSharpMarshalToNative(CSharpMarshalContext ctx)
        {
            var templateType = Type as TemplateSpecializationType;
            var insideType = templateType.Arguments[0].Type;

            Type type = ctx.Parameter.Type.Desugar();
            ClassTemplateSpecialization basicString = GetBasicString(type);
            var typePrinter = new CSharpTypePrinter(ctx.Context);
            //if (!ctx.Parameter.Type.Desugar().IsAddress() &&
            //    ctx.MarshalKind != MarshalKind.NativeField)
            //    ctx.Return.Write($"*({typePrinter.PrintNative(basicString)}*) ");
            string qualifiedBasicString = GetQualifiedBasicString(basicString);
            if (ctx.MarshalKind == MarshalKind.NativeField)
            {
            	var varBasicString = $"_tmp{ctx.ParameterIndex}";
ctx.Before.WriteLine($"// *({typePrinter.PrintNative(basicString)}*)");
            	ctx.Before.WriteLine($@"var {varBasicString} =  {ctx.ReturnVarName};");
                if (!IsPrimitiveType(insideType.Type))
                    ctx.Before.WriteLine($@"if (value != null && value.has_value())");
                else
            	    ctx.Before.WriteLine($@"if (value.HasValue)");
                ctx.Before.WriteOpenBraceAndIndent();
                    var value = !IsPrimitiveType(insideType.Type) ?
                        $"{ctx.Parameter.Name}.value()" : 
                        (insideType.ToString() == "bool" ? $"(byte) ({ctx.Parameter.Name}.Value ? 1 : 0)" 
                            : $"{ctx.Parameter.Name}.Value");
                    ctx.Before.WriteLine($@"{varBasicString}._M_payload = {value};");
                    ctx.Before.WriteLine($@"{varBasicString}._M_engaged = (byte) 1;");
            	ctx.Before.UnindentAndWriteCloseBrace();
                ctx.Before.WriteLine("else");
            	ctx.Before.WriteOpenBraceAndIndent();
                	 ctx.Before.WriteLine($@"{varBasicString}._M_engaged = (byte) 0;");
            	ctx.Before.UnindentAndWriteCloseBrace();
            	ctx.ReturnVarName = string.Empty;
            }
            else
            {
                var varBasicString = $"__basicString{ctx.ParameterIndex}";
                if (!IsPrimitiveType(insideType.Type))
                    ctx.Before.WriteLine($@"var {varBasicString} = {ctx.Parameter.Name}.has_value() ? new {
                            basicString.Visit(typePrinter)}({ctx.Parameter.Name}.value()) : new {
                            basicString.Visit(typePrinter)}();");
                else
                {
                    ctx.Before.WriteLine($@"var {varBasicString} = {ctx.Parameter.Name}.HasValue ? new {
                            basicString.Visit(typePrinter)}({ctx.Parameter.Name}.Value) : new {
                            basicString.Visit(typePrinter)}();");
                }
                var pointerType = type as PointerType;
                if (pointerType != null)
                {
                    ctx.Return.Write($"{varBasicString}.{Helpers.InstanceIdentifier}");
                }
                else
                {
                    ctx.Return.Write($"*({typePrinter.PrintNative(basicString)}*){varBasicString}.{Helpers.InstanceIdentifier}");
                }
                if (!type.IsPointer() || ctx.Parameter.IsIndirect)
                    ctx.Cleanup.WriteLine($@"{varBasicString}.Dispose({
                        (ctx.MarshalKind == MarshalKind.NativeField ? "false" : string.Empty)});");
            }
        }

        public override void CSharpMarshalToManaged(CSharpMarshalContext ctx)
        {
            var templateType = Type as TemplateSpecializationType;
            var insideType = templateType.Arguments[0].Type;
            ClassTemplateSpecialization basicString = GetBasicString(Type);
            var typePrinter = new CSharpTypePrinter(ctx.Context); 
            var nativeString = typePrinter.PrintNative(basicString);
            string qualifiedBasicString = GetQualifiedBasicString(basicString);
ctx.Before.WriteLine($"// *({typePrinter.PrintNative(basicString)}*)");
            var ptrBasicString = $"_ptr{ctx.ParameterIndex}";
            var varBasicString = $"_tmp{ctx.ParameterIndex}";
            var retBasicString = $"_ret{ctx.ParameterIndex}";
            ctx.Before.WriteLine($@"var {ptrBasicString} =  {ctx.ReturnVarName};");
            ctx.Before.WriteLine($@"var {varBasicString} =  (({typePrinter.PrintNative(basicString)}*){ctx.ReturnVarName});");
            if (!IsPrimitiveType(insideType.Type))
                ctx.Before.WriteLine("var {0} = new global::ALK.Interop.Optional<{0}>();", insideType, retBasicString);
            else
            	ctx.Before.WriteLine("var {1} = new System.Nullable<{0}>();", insideType, retBasicString);
            ctx.Before.WriteLine($@"if ({varBasicString}->_M_engaged != 0)");
            ctx.Before.WriteOpenBraceAndIndent();
            if (!IsPrimitiveType(insideType.Type))
                ctx.Before.WriteLine("{3} = new global::ALK.Interop.Optional<{0}>({1}->_M_payload{2});", insideType, varBasicString,
                    insideType.ToString() == "bool" ? " !=0" : "", retBasicString);
            else
                ctx.Before.WriteLine("{3} = new System.Nullable<{0}>({1}->_M_payload{2});", insideType, varBasicString,
                    insideType.ToString() == "bool" ? " !=0" : "", retBasicString);
            ctx.Before.UnindentAndWriteCloseBrace();
            ctx.Return.Write(retBasicString);
        }

        private static string GetQualifiedBasicString(ClassTemplateSpecialization basicString)
        {
            var declContext = basicString.TemplatedDecl.TemplatedDecl;
            var names = new Stack<string>();
            while (!(declContext is TranslationUnit))
            {
                var isInlineNamespace = declContext is Namespace && ((Namespace)declContext).IsInline;
                if (!isInlineNamespace)
                    names.Push(declContext.Name);
                declContext = declContext.Namespace;
            }
            var qualifiedBasicString = string.Join(".", names);
            // TODO:: Wonder why this isn't coming out as ALK.Interop
            return $"global::ALK.Interop.{qualifiedBasicString}";
        }

        private static ClassTemplateSpecialization GetBasicString(Type type)
        {
            var desugared = type.Desugar();
            var template = (desugared.GetFinalPointee() ?? desugared).Desugar();
            var templateSpecializationType = template as TemplateSpecializationType;
            if (templateSpecializationType != null)
                return templateSpecializationType.GetClassTemplateSpecialization();
            return (ClassTemplateSpecialization) ((TagType) template).Declaration;
        }
        
        public bool IsPrimitiveType(Type type)
        {
            if (type == null)
                return false;
            if (type is BuiltinType)
            {
                var primitiveType = (type as BuiltinType).Type;
                return primitiveType != PrimitiveType.String;
            }
            if (type is TagType)
            {
                Enumeration decl = (type as TagType).Declaration as Enumeration;
                if (decl != null)
                    return IsPrimitiveType(decl.Type);
            }

            if (type is TypedefType)
            {
                TypedefNameDecl decl = (type as TypedefType).Declaration;
                if (decl != null)
                    return IsPrimitiveType(decl.Type);
            }
            return false;
        }
    }

   // [TypeMap("VectorHolder", GeneratorKind = GeneratorKind.CSharp),
 //    TypeMap("global::ALK.Interop.VectorHolder<T>", GeneratorKind = GeneratorKind.CSharp)]
    public class VectorHolder : TypeMap
    {
        public override bool IsIgnored
        {
            get
            {
                var finalType = Type.GetFinalPointee() ?? Type;
                var type = finalType as TemplateSpecializationType;
                if (type == null)
                {
                    var injectedClassNameType = (InjectedClassNameType) finalType;
                    type = (TemplateSpecializationType) injectedClassNameType.InjectedSpecializationType.Type;
                }
                var checker = new TypeIgnoreChecker(TypeMapDatabase);
                type.Arguments[0].Type.Visit(checker);

                return checker.IsIgnored;
            }
        }

        public override Type CLISignatureType(TypePrinterContext ctx)
        {
            throw new System.NotImplementedException();
        }

        public override void CLIMarshalToNative(MarshalContext ctx)
        {
            throw new System.NotImplementedException();
        }

        public override void CLIMarshalToManaged(MarshalContext ctx)
        {
            throw new System.NotImplementedException();
        }

        public override void CSharpMarshalToNative(CSharpMarshalContext ctx)
        {
            var templateType = Type as TemplateSpecializationType;
            var insideType = templateType.Arguments[0].Type;

            Type type = ctx.Parameter.Type.Desugar();
        }

        public override void CSharpMarshalToManaged(CSharpMarshalContext ctx)
        {
            var templateType = Type as TemplateSpecializationType;
            var insideType = templateType.Arguments[0].Type;

        }

    }
    

    [TypeMap("std::vector", GeneratorKind = GeneratorKind.CSharp)]
    public class Vector : TypeMap
    {
        public override bool IsIgnored
        {
            get
            {
                var finalType = Type.GetFinalPointee() ?? Type;
                var type = finalType as TemplateSpecializationType;
                if (type == null)
                {
                    var injectedClassNameType = (InjectedClassNameType) finalType;
                    type = (TemplateSpecializationType) injectedClassNameType.InjectedSpecializationType.Type;
                }
                var checker = new TypeIgnoreChecker(TypeMapDatabase);
                type.Arguments[0].Type.Visit(checker);

                return checker.IsIgnored;
            }
        }

        public override Type CLISignatureType(TypePrinterContext ctx)
        {
            throw new System.NotImplementedException();
        }

        public override void CLIMarshalToNative(MarshalContext ctx)
        {
            throw new System.NotImplementedException();
        }

        public override void CLIMarshalToManaged(MarshalContext ctx)
        {
            throw new System.NotImplementedException();
        }

        public override Type CSharpSignatureType(TypePrinterContext ctx)
        {
            if (ctx.Kind == TypePrinterContextKind.Native)
            {
                ClassTemplateSpecialization basicString = GetBasicString(ctx.Type);
                var typePrinter = new CSharpTypePrinter(null);
                typePrinter.PushContext(TypePrinterContextKind.Native);
                return new CustomType(basicString.Visit(typePrinter).Type);
            }

                var paramList = ctx.GetTemplateParameterList();
                return new CustomType($"System.Collections.Generic.List<{paramList}>");
        }

        public override void CSharpMarshalToNative(CSharpMarshalContext ctx)
        {
            var templateType = Type as TemplateSpecializationType;
            var insideType = templateType.Arguments[0].Type;
            
            //var vectorHolderTypeName = ((PolarDriverOptions)Context.Options).VectorHolderName;

            Type type = ctx.Parameter.Type.Desugar();
            ClassTemplateSpecialization basicString = GetBasicString(type);
            var typePrinter = new CSharpTypePrinter(ctx.Context);
            //if (!ctx.Parameter.Type.Desugar().IsAddress() &&
            //    ctx.MarshalKind != MarshalKind.NativeField)
            //    ctx.Return.Write($"*({typePrinter.PrintNative(basicString)}*) ");
            string qualifiedBasicString = GetQualifiedBasicString(basicString);
            if (ctx.MarshalKind == MarshalKind.NativeField)
            {
                var vectorLocation = $"new System.IntPtr(&{ctx.ReturnVarName})";
                var newVector = $"new {typePrinter.PrintNative(basicString)}()";
                var newVectorHolder = $"new VectorHolder<{insideType.Visit(typePrinter)}>()";
                
                var vectorPointerName = $"_vectorPtr{ctx.ParameterIndex}";
                var vectorHolderName = $"_vectorHolder{ctx.ParameterIndex}";
                var vectorItName = $"_vi{ctx.ParameterIndex}";
                ctx.Before.WriteLine($@"{ctx.ReturnVarName} = {newVector};");
                ctx.Before.WriteLine($@"var {vectorPointerName} = {vectorLocation};");
                ctx.Before.WriteLine($@"var {vectorHolderName} = {newVectorHolder};");
                ctx.Before.WriteLine($@"{vectorHolderName}.assignFrom({vectorPointerName});");
                ctx.Before.WriteLine($@"foreach(var {vectorItName} in {ctx.Parameter.Name})");
                ctx.Before.WriteOpenBraceAndIndent();
                ctx.Before.WriteLine($@"{vectorHolderName}.add({vectorItName});");
                ctx.Before.UnindentAndWriteCloseBrace();
            	ctx.ReturnVarName = string.Empty;
            }
            else
            {
                var insideTypeName = insideType.Visit(typePrinter);
                var vectorHolderName = $"_vectorHolder{ctx.ParameterIndex}";
                var vectorItName = $"_vi{ctx.ParameterIndex}";
                ctx.Before.WriteLine($@"var {vectorHolderName} = new VectorHolder<{insideTypeName}>();");
                if (ctx.Parameter.Usage != ParameterUsage.Out)
                {
                    ctx.Before.WriteLine($@"foreach(var {vectorItName} in {ctx.Parameter.Name})");
                    ctx.Before.WriteOpenBraceAndIndent();
                    if (!IsPrimitiveType(insideType.Type))
                    {
                        if (insideTypeName == "string")
                        {
                            var vectorSName = $"_viS{ctx.ParameterIndex}";
                            ctx.Before.WriteLine(
                                $@"var {vectorSName} = new global::std.basic_string<sbyte, global::std.char_traits<sbyte>, global::std.allocator<sbyte>>();");
                            ctx.Before.WriteLine(
                                $@"global::std.basic_stringExtensions.assign({vectorSName}, (string) (object) {vectorItName});");
                            ctx.Before.WriteLine($@"{vectorHolderName}.addByRef({vectorSName}.__Instance);");
                            ctx.Before.WriteLine($@"{vectorSName}.Dispose();");
                        }
                        else
                        {
                            ctx.Before.WriteLine($@"if ({vectorItName} != null)");
                            ctx.Before.WriteLine($@"    {vectorHolderName}.addByRef({vectorItName}.__Instance);");
                            ctx.Before.WriteLine($@"else");
                            var message =
                                $@"Warning: Skipping add for {ctx.Parameter.Name}. Think about using std::vector<Optional<{insideTypeName}>>.";
                            ctx.Before.WriteLine($"    System.Console.WriteLine(\"{message}\");");
                        }
                    }
                    else
                    {
                        ctx.Before.WriteLine($@"{vectorHolderName}.add({vectorItName});");
                    }

                    ctx.Before.UnindentAndWriteCloseBrace();
                }

                var pointerType = type as PointerType;
                if (pointerType != null)
                {
                    ctx.Return.Write($"{vectorHolderName}.@ref()");
                }
                else
                {
                    ctx.Return.Write($"*({typePrinter.PrintNative(basicString)}*){vectorHolderName}.@ref()");
                }

                if (ctx.Parameter.Usage == ParameterUsage.Out)
                {
                    ctx.Cleanup.WriteLine($@"{ctx.Parameter.Name} = {vectorHolderName}.getList();");
                }

                if (ctx.Parameter.Usage == ParameterUsage.InOut)
                {
                    ctx.Cleanup.WriteLine($@"{vectorHolderName}.assignToList({ctx.Parameter.Name});");
                }
                if (!type.IsPointer() || ctx.Parameter.IsIndirect)
                    ctx.Cleanup.WriteLine($@"{vectorHolderName}.Dispose({
                        (ctx.MarshalKind == MarshalKind.NativeField ? "false" : string.Empty)});");
            }
        }

        public override void CSharpMarshalToManaged(CSharpMarshalContext ctx)
        {
            var templateType = Type as TemplateSpecializationType;
            var insideType = templateType.Arguments[0].Type;

            ClassTemplateSpecialization basicString = GetBasicString(templateType);
            var typePrinter = new CSharpTypePrinter(ctx.Context);

            //var vectorHolderTypeName = ((PolarDriverOptions)Context.Options).VectorHolderName;
            
            var typeCast = $"({typePrinter.PrintNative(basicString)})";
            // So we have the case that when the arg name is not generated, i.e. "_ret", its ReturnVarName is
            // a System.IntPtr of an already typecasted value, i.e. a property, etc. This would be for the
            // return of a function std::vector<?> someFunction(....). 
            // If it is for a function return or a parameter return we need the dereferenced type cast.
            if (ctx.ReturnVarName != ctx.ArgName)
            {
                typeCast = $"*({typePrinter.PrintNative(basicString)}*)";
            }
            var insideTypeName = insideType.Visit(typePrinter);
            var vectorName = $"__vector{ctx.ParameterIndex}";
            var listName = $"__list{ctx.ParameterIndex}";
            if (Platform.IsWindows)
            	ctx.Before.WriteLine($@"var {vectorName} =  ({typeCast}{ctx.ReturnVarName})._Mypair._Myval2 ;");
            else
            	ctx.Before.WriteLine($@"var {vectorName} =  ({typeCast}{ctx.ReturnVarName})._M_impl;");
            var itemSizeName = $"__itemSize{ctx.ParameterIndex}";
            
            ctx.Before.WriteLine($@"var {itemSizeName} = VectorHolder<{insideTypeName}>.itemSize();");
            if (isEnumType(insideType.Type))
            {
                var arrayName = $"__list{ctx.ParameterIndex}Array";
                ctx.Before.WriteLine($@"var {arrayName} = ALK.Interop.Utils.toEnumArrayFromNative<{insideTypeName}>({vectorName});");
                ctx.Before.WriteLine($@"var {listName} = new System.Collections.Generic.List<{insideTypeName}>({arrayName});");
            }
            else
            {
                if (!Platform.IsWindows)
                {
                    if (insideTypeName == "bool")
                    {
                        var arrayName = $"__list{ctx.ParameterIndex}Array";
                        ctx.Before.WriteLine($@"var {arrayName} = ALK.Interop.Utils.toBoolArray({vectorName});");
                        ctx.Before.WriteLine(
                            $@"var {listName} = new System.Collections.Generic.List<{insideTypeName}>({arrayName});");
                    }
                    else
                    {
                        var createFunction = getCreateFunction(insideTypeName);
                        ctx.Before.WriteLine(
                            $@"var {listName} = ALK.Interop.Utils.toList<{insideTypeName}>({createFunction}, {itemSizeName}, {vectorName});");
                    }
                }
                else
                {
                    var createFunction = getCreateFunction(insideTypeName);
                    ctx.Before.WriteLine(
                        $@"var {listName} = ALK.Interop.Utils.toList<{insideTypeName}>({createFunction}, {itemSizeName}, {vectorName});");
                }
            }

            var returnName = listName;
            if (ctx.Parameter != null && ctx.Parameter.Usage == ParameterUsage.Out)
            {
                ctx.Before.WriteLine($@"var {ctx.ReturnVarName} = {listName};");
                ctx.Return.Write(string.Empty);
            }
            else
            {    
                ctx.Return.Write("{0}", listName);
            }
        } 
        List<string> prims = new List<string>() {"bool", "byte","sbyte","int","uint","short","ushort","long", "ulong", "float", "double"};

        
        public bool IsPrimitiveType(Type type)
        {
            if (type == null)
                return false;
            if (type is BuiltinType)
            {
                var primitiveType = (type as BuiltinType).Type;
                return primitiveType != PrimitiveType.String;
            }
            if (type is TagType)
            {
                Enumeration decl = (type as TagType).Declaration as Enumeration;
                if (decl != null)
                    return IsPrimitiveType(decl.Type);
            }

            if (type is TypedefType)
            {
                TypedefNameDecl decl = (type as TypedefType).Declaration;
                if (decl != null)
                    return IsPrimitiveType(decl.Type);
            }
            return false;
        }
        public bool isEnumType(Type type)
        {
            
            if (type == null || type is BuiltinType)
            {
                return false;
            }
            if (type is TagType)
            {
                Enumeration decl = (type as TagType).Declaration as Enumeration;
                if (decl != null)
                    return IsPrimitiveType(decl.Type);
            }

            if (type is TypedefType)
            {
                TypedefNameDecl decl = (type as TypedefType).Declaration;
                if (decl != null)
                    return isEnumType(decl.Type);
            }

            return false;
        }
        
        public string getCreateFunction(string typeName)
        {
            if (typeName == "bool")
            {
                return "ALK.Interop.Utils.boolP";
            }
            if (typeName == "byte")
            {
                return "ALK.Interop.Utils.byteP";
            }
            if (typeName == "sbyte")
            {
                return "ALK.Interop.Utils.sbyteP";
            }
            if (typeName == "int")
            {
                return "ALK.Interop.Utils.intP";
            }
            if (typeName == "uint")
            {
                return "ALK.Interop.Utils.uintP";
            }
            if (typeName == "short")
            {
                return "ALK.Interop.Utils.shortP";
            }
            if (typeName == "ushort")
            {
                return "ALK.Interop.Utils.ushortP";
            }
            if (typeName == "long")
            {
                return "ALK.Interop.Utils.longP";
            }
            if (typeName == "ulong")
            {
                return "ALK.Interop.Utils.ulongP";
            }
            if (typeName == "float")
            {
                return "ALK.Interop.Utils.floatP";
            }
            if (typeName == "double")
            {
                return "ALK.Interop.Utils.doubleP";
            }
            if (typeName == "string")
            {
                return "ALK.Interop.Utils.stringP";
            }
            return $"{typeName}.__CreateInstance";
        }

        private static string GetQualifiedBasicString(ClassTemplateSpecialization basicString)
        {
            var declContext = basicString.TemplatedDecl.TemplatedDecl;
            var names = new Stack<string>();
            while (!(declContext is TranslationUnit))
            {
                var isInlineNamespace = declContext is Namespace && ((Namespace)declContext).IsInline;
                if (!isInlineNamespace)
                    names.Push(declContext.Name);
                declContext = declContext.Namespace;
            }
            var qualifiedBasicString = string.Join(".", names);
            // TODO:: Wonder why this isn't coming out as ALK.Interop
            return $"global::ALK.Interop.{qualifiedBasicString}";
        }

        public static ClassTemplateSpecialization GetBasicString(Type type)
        {
            var desugared = type.Desugar();
            var template = (desugared.GetFinalPointee() ?? desugared).Desugar();
            var templateSpecializationType = template as TemplateSpecializationType;
            if (templateSpecializationType != null)
                return templateSpecializationType.GetClassTemplateSpecialization();
            return (ClassTemplateSpecialization) ((TagType) template).Declaration;
        }
    }
    

    // We are not going to use this.
    //[TypeMap("std::tuple", GeneratorKind = GeneratorKind.CSharp)]
    public class Map : TypeMap
    {
        public override bool IsIgnored { get { return false; } }

        public override Type CLISignatureType(TypePrinterContext ctx)
        {
            throw new System.NotImplementedException();
        }

        public override void CLIMarshalToNative(MarshalContext ctx)
        {
            throw new System.NotImplementedException();
        }

        public override void CLIMarshalToManaged(MarshalContext ctx)
        {
            throw new System.NotImplementedException();
        }

        public override Type CSharpSignatureType(TypePrinterContext ctx)
        {
            if (ctx.Kind == TypePrinterContextKind.Native)
            {
                ClassTemplateSpecialization basicString = GetBasicString(ctx.Type);
                var typePrinter = new CSharpTypePrinter(null);
                typePrinter.PushContext(TypePrinterContextKind.Native);
                return new CustomType(basicString.Visit(typePrinter).Type);
            }


            var type = Type as TemplateSpecializationType;
            string typeString = "System.Tuple<";
            bool first = true;
            foreach (var x in type.Arguments)
            {
                if (first)
                    first = false;
                else
                    typeString += ",";
                typeString += x.Type;
            }

            typeString += ">"; 
            return new CustomType(typeString);
        }
        
        public bool IsPrimitiveType(Type type)
        {
            if (type == null)
                return false;
            if (type is BuiltinType)
            {
                var primitiveType = (type as BuiltinType).Type;
                return primitiveType != PrimitiveType.String;
            }
            if (type is TagType)
            {
                Enumeration decl = (type as TagType).Declaration as Enumeration;
                if (decl != null)
                    return IsPrimitiveType(decl.Type);
            }

            if (type is TypedefType)
            {
                TypedefNameDecl decl = (type as TypedefType).Declaration;
                if (decl != null)
                    return IsPrimitiveType(decl.Type);
            }
            return false;
        }

        private string parameterString(CSharpTypePrinter typePrinter, List<QualifiedType> types)
        {
            string typeString = "";
            bool first = true;
            foreach (var x in types)
            {
                if (first)
                    first = false;
                else
                    typeString += ",";
                typeString += x.Type.Visit(typePrinter);
            }
            return typeString;
        }
        public string getCreateFunction(string typeName)
        {
            if (typeName == "bool")
            {
                return "ALK.Interop.Utils.boolP";
            }
            if (typeName == "byte")
            {
                return "ALK.Interop.Utils.byteP";
            }
            if (typeName == "sbyte")
            {
                return "ALK.Interop.Utils.sbyteP";
            }
            if (typeName == "int")
            {
                return "ALK.Interop.Utils.intP";
            }
            if (typeName == "uint")
            {
                return "ALK.Interop.Utils.uintP";
            }
            if (typeName == "short")
            {
                return "ALK.Interop.Utils.shortP";
            }
            if (typeName == "ushort")
            {
                return "ALK.Interop.Utils.ushortP";
            }
            if (typeName == "long")
            {
                return "ALK.Interop.Utils.longP";
            }
            if (typeName == "ulong")
            {
                return "ALK.Interop.Utils.ulongP";
            }
            if (typeName == "float")
            {
                return "ALK.Interop.Utils.floatP";
            }
            if (typeName == "double")
            {
                return "ALK.Interop.Utils.doubleP";
            }
            if (typeName == "string")
            {
                return "ALK.Interop.Utils.stringP";
            }
            return $"{typeName}.__CreateInstance";
        }

        
        public override void CSharpMarshalToNative(CSharpMarshalContext ctx)
        {
            var templateType = Type as TemplateSpecializationType;
            var insideTypes = templateType.Arguments.ConvertAll(x => x.Type);
            var count = insideTypes.Count;
            var typePrinter = new CSharpTypePrinter(ctx.Context);
            var paramString = parameterString(typePrinter, insideTypes);
            Type type = ctx.Parameter.Type.Desugar();
            ClassTemplateSpecialization basicString = GetBasicString(type);
            //if (!ctx.Parameter.Type.Desugar().IsAddress() &&
            //    ctx.MarshalKind != MarshalKind.NativeField)
            //    ctx.Return.Write($"*({typePrinter.PrintNative(basicString)}*) ");
            string qualifiedBasicString = GetQualifiedBasicString(basicString);
            if (ctx.MarshalKind == MarshalKind.NativeField)
            {
                var vectorLocation = $"new System.IntPtr(&{ctx.ReturnVarName})";
                var newVector = $"new {typePrinter.PrintNative(basicString)}()";
                var newVectorHolder = $"new Tuple{count}Holder<{paramString}>()";
                
                var vectorPointerName = $"_vectorPtr{ctx.ParameterIndex}";
                var vectorHolderName = $"_vectorHolder{ctx.ParameterIndex}";
                for (var i = 1; i <= count; i++)
                {
                    ctx.Before.WriteLine($@"{vectorHolderName}.set{i}({ctx.Parameter.Name}.Item{i});");
                }
            	ctx.ReturnVarName = string.Empty;
            }
            else
            {
                var vectorHolderName = $"_tuple{count}Holder{ctx.ParameterIndex}";
                var vectorItName = ctx.Parameter.Name;
                if (ctx.Parameter.Usage != ParameterUsage.Out)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var itemName = $"__tuple{ctx.ParameterIndex}_item{i}";
                        var insideTypeName = insideTypes[i].Visit(typePrinter);
                        if (!IsPrimitiveType(insideTypes[i].Type))
                        {
                            if (insideTypeName == "string")
                            {
                                var vectorSName = $"_viS{ctx.ParameterIndex}_item{i}";
                                ctx.Before.WriteLine(
                                    $@"var {vectorSName} = new global::std.basic_string<sbyte, global::std.char_traits<sbyte>, global::std.allocator<sbyte>>();");
                                ctx.Before.WriteLine(
                                    $@"global::std.basic_stringExtensions.assign({vectorSName}, (string) (object) {vectorItName}.Item{i+1});");
                                ctx.Before.WriteLine($@"var {itemName} = {vectorSName}.Item{i+1}.__Instance;");
                                ctx.Cleanup.WriteLine($@"{vectorSName}.Dispose();");
                            }
                            else
                            {
                                ctx.Before.WriteLine($@"var {itemName} = {vectorItName}.Item{i+1}.__Instance;");
                            }
                        }
                        else
                        {
                            ctx.Before.WriteLine($@"var {itemName} = {vectorItName}.Item{i+1};");
                        }
                    }
                }
                ctx.Before.Write(
                    $@"var {vectorHolderName} = new Tuple{count}Holder<{paramString}>(");
                bool first = true;
                for (var i = 0; i < count; i++)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        ctx.Before.Write(",");
                    }

                    ctx.Before.Write($@"__tuple{ctx.ParameterIndex}_item{i}");
                }

                ctx.Before.WriteLine(");");

                var pointerType = type as PointerType;
                if (pointerType != null)
                {
                    ctx.Return.Write($"{vectorHolderName}.@ref()");
                }
                else
                {
                    ctx.Return.Write($"*({typePrinter.PrintNative(basicString)}*){vectorHolderName}.@ref()");
                }

                if (ctx.Parameter.Usage == ParameterUsage.Out)
                {
                    ctx.Cleanup.WriteLine($@"{ctx.Parameter.Name} = {vectorHolderName}.getTuple();");
                }

                if (ctx.Parameter.Usage == ParameterUsage.InOut)
                {
                    ctx.Cleanup.WriteLine($@"{vectorHolderName}.assignToTuple({ctx.Parameter.Name});");
                }

                if (!type.IsPointer() || ctx.Parameter.IsIndirect)
                    ctx.Cleanup.WriteLine($@"{vectorHolderName}.Dispose({
                        (ctx.MarshalKind == MarshalKind.NativeField ? "false" : string.Empty)});");
            }
        }

        public override void CSharpMarshalToManaged(CSharpMarshalContext ctx)
        {
            var templateType = Type as TemplateSpecializationType;
            var insideTypes = templateType.Arguments.ConvertAll(x => x.Type);
            var count = insideTypes.Count;
            var typePrinter = new CSharpTypePrinter(ctx.Context);
            var paramString = parameterString(typePrinter, insideTypes);
            ClassTemplateSpecialization basicString = GetBasicString(templateType);

            throw new System.Exception($@"Cannot marshal a native std::tuple<{paramString}> to a managed entity. Please use Tuple{count}Holder instead.");

            /*
            var typeCast = $"({typePrinter.PrintNative(basicString)})";
            // So we have the case that when the arg name is not generated, i.e. "_ret", its ReturnVarName is
            // a System.IntPtr of an already typecasted value, i.e. a property, etc. This would be for the
            // return of a function std::vector<?> someFunction(....). 
            // If it is for a function return or a parameter return we need the dereferenced type cast.
            if (ctx.ReturnVarName != ctx.ArgName)
            {
                typeCast = $"*({typePrinter.PrintNative(basicString)}*)";
            }
            var vectorName = $"__std_tuple{ctx.ParameterIndex}";
            var listName = $"__Tuple{ctx.ParameterIndex}";

            var baseImplName = $@"{listName}_M_head_impl";
            ctx.Before.WriteLine($@"var {baseImplName} =  new System.IntPtr(({typeCast}{ctx.ReturnVarName})._M_head_impl);");
            for (var i = 0; i < count; i++)
            {
                var implName = i > 0 ? $"_M_head_impl{i}" : "_M_head_impl"; 
                ctx.Before.WriteLine($@"var {listName}_{i} = {baseImplName}.Add(({typeCast}{ctx.ReturnVarName}).{implName});");
                var createFunction = getCreateFunction(insideTypes[i].Visit(typePrinter));
                ctx.Before.WriteLine($@"var {listName}_Item{i} = {createFunction}({listName}_{i});");
            }

            ctx.Before.Write($@"var {listName} = new System.Tuple<{paramString}>(");
            bool first = true;
            for (var i = 0; i < count; i++)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    ctx.Before.Write(",");
                }

                ctx.Before.Write($@"{listName}_Item{i}");
            }

            ctx.Before.WriteLine(");");

            var returnName = listName;
            if (ctx.Parameter != null && ctx.Parameter.Usage == ParameterUsage.Out)
            {
                ctx.Before.WriteLine($@"var {ctx.ReturnVarName} = {listName};");
                ctx.Return.Write(string.Empty);
            }
            else
            {    
                ctx.Return.Write("{0}", listName);
            }
            */
        }

        private static string GetQualifiedBasicString(ClassTemplateSpecialization basicString)
        {
            var declContext = basicString.TemplatedDecl.TemplatedDecl;
            var names = new Stack<string>();
            while (!(declContext is TranslationUnit))
            {
                var isInlineNamespace = declContext is Namespace && ((Namespace)declContext).IsInline;
                if (!isInlineNamespace)
                    names.Push(declContext.Name);
                declContext = declContext.Namespace;
            }
            var qualifiedBasicString = string.Join(".", names);
            // TODO:: Wonder why this isn't coming out as ALK.Interop
            return $"global::ALK.Interop.{qualifiedBasicString}";
        }

        public static ClassTemplateSpecialization GetBasicString(Type type)
        {
            var desugared = type.Desugar();
            var template = (desugared.GetFinalPointee() ?? desugared).Desugar();
            var templateSpecializationType = template as TemplateSpecializationType;
            if (templateSpecializationType != null)
                return templateSpecializationType.GetClassTemplateSpecialization();
            return (ClassTemplateSpecialization) ((TagType) template).Declaration;
        }
    }
}