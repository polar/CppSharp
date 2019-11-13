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
        TypeMap("Optional", GeneratorKind = GeneratorKind.CLI),
        TypeMap("Optional", GeneratorKind = GeneratorKind.CSharp),
        TypeMap("global::ALK.Interop.Optional<T>", GeneratorKind = GeneratorKind.CLI),
        TypeMap("global::ALK.Interop.Optional<T>", GeneratorKind = GeneratorKind.CSharp)
    ]
    public class Optional : TypeMap
    {
        public override bool IsIgnored
        {
            get { return false; }
        }
        
        public override Type CLISignatureType(TypePrinterContext ctx)
        {
            return new CustomType(
                $"System::Nullable<{ctx.GetTemplateParameterList()}>^");
        }
        
        public override void CLIMarshalToNative(MarshalContext ctx)
        {
            var templateType = Type as TemplateSpecializationType;
            var type = templateType.Arguments[0].Type;
            var isPointerToPrimitive = type.Type.IsPointerToPrimitiveType();
            var managedType = isPointerToPrimitive
                ? new CILType(typeof(System.IntPtr))
                : type.Type;

            var entryString = (ctx.Parameter != null) ? ctx.Parameter.Name
                : ctx.ArgName;

            var tmpVarName = "_tmp" + entryString;

            var cppTypePrinter = new CppTypePrinter();
            var nativeType = type.Type.Visit(cppTypePrinter);

            ctx.Before.WriteLine("auto {0} = ALK.Interop.Optional<{1}>();",
                tmpVarName, nativeType);
            ctx.Before.WriteOpenBraceAndIndent();
            {
                var param = new Parameter
                {
                    Name = "_element",
                    QualifiedType = type
                };

                var elementCtx = new MarshalContext(ctx.Context, ctx.Indentation)
                                     {
                                         Parameter = param,
                                         ArgName = param.Name,
                                     };

                var marshal = new CLIMarshalManagedToNativePrinter(elementCtx);
                type.Type.Visit(marshal);

                if (!string.IsNullOrWhiteSpace(marshal.Context.Before))
                    ctx.Before.Write(marshal.Context.Before);

                if (isPointerToPrimitive)
                    ctx.Before.WriteLine("auto _marshalElement = {0}.ToPointer();",
                        marshal.Context.Return);
                else
                    ctx.Before.WriteLine("auto _marshalElement = {0};",
                    marshal.Context.Return);

                ctx.Before.WriteLine("{0} = _marshalElement;",
                    tmpVarName);
            }
            
            ctx.Before.UnindentAndWriteCloseBrace();

            ctx.Return.Write(tmpVarName);
        }

        public override bool DoesMarshalling
        {
            get { return true; }
        }
        
        public override void CLIMarshalToManaged(MarshalContext ctx)
        {
            var templateType = Type as TemplateSpecializationType;
            var type = templateType.Arguments[0].Type;
            var isPointerToPrimitive = type.Type.IsPointerToPrimitiveType();
            var managedType = isPointerToPrimitive
                ? new CILType(typeof(System.IntPtr))
                : type.Type;
            var tmpVarName = "_tmp" + ctx.ArgName;
            
            ctx.Before.WriteLine(
                "auto {0} = gcnew System::Nullable<{1}>();",
                tmpVarName, managedType);
            ctx.Before.WriteOpenBraceAndIndent();
            {
                var elementCtx = new MarshalContext(ctx.Context, ctx.Indentation)
                {
                    ReturnVarName = "_element",
                    ReturnType = type
                };

                var marshal = new CLIMarshalNativeToManagedPrinter(elementCtx);
                type.Type.Visit(marshal);

                if (!string.IsNullOrWhiteSpace(marshal.Context.Before))
                    ctx.Before.Write(marshal.Context.Before);

                ctx.Before.WriteLine("auto _marshalElement = {0};",
                    marshal.Context.Return);

                if (isPointerToPrimitive)
                    ctx.Before.WriteLine("{0} = ({1}(_marshalElement));",
                        tmpVarName, managedType);
                else
                    ctx.Before.WriteLine("{0} = _marshalElement;",
                        tmpVarName);
            }
            ctx.Before.UnindentAndWriteCloseBrace();

            ctx.Return.Write(tmpVarName);
        }

        public override Type CSharpSignatureType(TypePrinterContext ctx)
        {
            if (ctx.Kind == TypePrinterContextKind.Managed)
                return new CustomType($"{ctx.GetTemplateParameterList()}?");
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
            	ctx.Before.WriteLine($@"if (value.HasValue)");
            	ctx.Before.WriteOpenBraceAndIndent();
                    var value = insideType.ToString() == "bool" ? $"(byte) ({ctx.Parameter.Name}.Value ? 1 : 0)" : $"{ctx.Parameter.Name}.Value";
                    ctx.Before.WriteLine($@"{varBasicString}._M_payload = {value};");
            	ctx.Before.UnindentAndWriteCloseBrace();
                ctx.Before.WriteLine("else");
            	ctx.Before.WriteOpenBraceAndIndent();
                	 ctx.Before.WriteLine($@"{varBasicString}._M_engaged = (byte) 0;");;
            	ctx.Before.UnindentAndWriteCloseBrace();
            	ctx.ReturnVarName = string.Empty;
            }
            else
            {
                var varBasicString = $"__basicString{ctx.ParameterIndex}";
                ctx.Before.WriteLine($@"var {varBasicString} = {ctx.Parameter.Name}.HasValue ? new {
                    basicString.Visit(typePrinter)}({ctx.Parameter.Name}.Value) : new {
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
            	ctx.Before.WriteLine("var {1} = new System.Nullable<{0}>();", insideType, retBasicString);
            ctx.Before.WriteLine($@"if ({varBasicString}->_M_engaged != 0)");
            ctx.Before.WriteOpenBraceAndIndent();
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
                        ctx.Before.WriteLine($@"{vectorHolderName}.addByRef({vectorItName}.__Instance);");
                    }
                }
                else
                {
                    ctx.Before.WriteLine($@"{vectorHolderName}.add({vectorItName});");
                }
                ctx.Before.UnindentAndWriteCloseBrace();
                var pointerType = type as PointerType;
                if (pointerType != null)
                {
                    ctx.Return.Write($"{vectorHolderName}.@ref()");
                }
                else
                {
                    ctx.Return.Write($"*({typePrinter.PrintNative(basicString)}*){vectorHolderName}.@ref()");
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
            ctx.Before.WriteLine($@"var {vectorName} =  ({typeCast}{ctx.ReturnVarName})._M_impl;");
            var itemSizeName = $"__itemSize{ctx.ParameterIndex}";
            
            ctx.Before.WriteLine($@"var {itemSizeName} = VectorHolder<{insideTypeName}>.itemSize();");
            if (isEnumType(insideType.Type))
            {
                
                var arrayName = $"__list{ctx.ParameterIndex}Array";
                ctx.Before.WriteLine($@"var {arrayName} = ALK.Interop.Utils.toEnumArrayFromNative<{insideTypeName}>({vectorName});");
                ctx.Before.WriteLine($@"var {listName} = new System.Collections.Generic.List<{insideTypeName}>({arrayName});");
            }
            else if (insideTypeName == "bool")
            {
                var arrayName = $"__list{ctx.ParameterIndex}Array";
                ctx.Before.WriteLine($@"var {arrayName} = ALK.Interop.Utils.toBoolArray({vectorName});");
                ctx.Before.WriteLine($@"var {listName} = new System.Collections.Generic.List<{insideTypeName}>({arrayName});");
            }
            else
            {
                var createFunction = getCreateFunction(insideTypeName);
                ctx.Before.WriteLine($@"var {listName} = ALK.Interop.Utils.toList<{insideTypeName}>({createFunction}, {itemSizeName}, {vectorName});");
            }
            
            ctx.Return.Write("{0}", listName);
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
}