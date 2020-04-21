﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Util;
using CppSharp.AST;
using CppSharp.AST.Extensions;
using CppSharp.Generators.CSharp;

namespace CppSharp.Generators
{
    public abstract class CodeGenerator : BlockGenerator, IAstVisitor<bool>
    {
        public BindingContext Context { get; }

        public DriverOptions Options => Context.Options;

        public List<TranslationUnit> TranslationUnits { get; }

        public TranslationUnit TranslationUnit => TranslationUnits[0];

        public abstract string FileExtension { get; }

        public virtual string FilePath =>
            $"{TranslationUnit.FileNameWithoutExtension}.{FileExtension}";

        /// <summary>
        /// Gets the comment style kind for regular comments.
        /// </summary>
        public virtual CommentKind CommentKind
        {
            get
            {
                if (!Options.CommentKind.HasValue)
                    return CommentKind.BCPL;
                
                return Options.CommentKind.Value;
            }
        } 

        /// <summary>
        /// Gets the comment style kind for documentation comments.
        /// </summary>
        public virtual CommentKind DocumentationCommentKind => CommentKind.BCPLSlash;

        public ISet<object> Visited { get; } = new HashSet<object>();

        public AstVisitorOptions VisitOptions { get; } = new AstVisitorOptions();

        protected CodeGenerator(BindingContext context)
        {
            Context = context;
        }

        protected CodeGenerator(BindingContext context, TranslationUnit unit)
            : this(context, new List<TranslationUnit> { unit })
        {
        }

        protected CodeGenerator(BindingContext context, IEnumerable<TranslationUnit> units)
        {
            Context = context;
            TranslationUnits = new List<TranslationUnit>(units);
        }

        public abstract void Process();

        public virtual void GenerateFilePreamble(CommentKind kind, string generatorName = "CppSharp")
        {
            var lines = new List<string>
            {
                "----------------------------------------------------------------------------",
                "<auto-generated>",
                $"This is autogenerated code by {generatorName}.",
                "Do not edit this file or all your changes will be lost after re-generation.",
                "</auto-generated>",
                "----------------------------------------------------------------------------"
            };

            PushBlock(BlockKind.Header);
            GenerateMultiLineComment(lines, kind);
            PopBlock();
        }

        #region Declaration generation

        public virtual void GenerateDeclarationCommon(Declaration decl)
        {
            if (decl.Comment != null)
                GenerateComment(decl.Comment);

            GenerateDebug(decl);
        }

        public virtual void GenerateDebug(Declaration decl)
        {
            if (Options.GenerateDebugOutput && !string.IsNullOrWhiteSpace(decl.DebugText))
                foreach (var line in Regex.Split(decl.DebugText.Trim(), "\r?\n"))
                    WriteLine($"// DEBUG: {line}");
        }

        #endregion

        #region Comment generation

        public virtual void GenerateSummary(string comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
                return;

            var lines = new List<string>
            {
                "<summary>",
                $"{comment}",
                "</summary>"
            };

            GenerateMultiLineComment(lines, DocumentationCommentKind);
        }

        public virtual void GenerateInlineSummary(RawComment comment)
        {
            GenerateComment(comment);
        }

        public virtual void GenerateComment(RawComment comment)
        {
            if (comment.FullComment != null)
            {
                PushBlock(BlockKind.BlockComment);
                ActiveBlock.Text.Print(comment.FullComment, DocumentationCommentKind);
                PopBlock();
                return;
            }

            if (string.IsNullOrWhiteSpace(comment.BriefText))
                return;

            var lines = new List<string>();

            if (comment.BriefText.Contains("\n"))
            {
                lines.Add("<summary>");
                foreach (string line in HtmlEncoder.HtmlEncode(comment.BriefText).Split(
                                            Environment.NewLine.ToCharArray()))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    lines.Add($"<para>{line}</para>");
                }
                lines.Add("</summary>");
            }
            else
            {
                lines.Add($"<summary>{comment.BriefText}</summary>");
            }

            GenerateMultiLineComment(lines, CommentKind);
        }

        public virtual void GenerateMultiLineComment(List<string> lines, CommentKind kind)
        {
            PushBlock(BlockKind.BlockComment);
        
            var lineCommentPrologue = Comment.GetLineCommentPrologue(kind);
            if (!string.IsNullOrWhiteSpace(lineCommentPrologue))
                WriteLine("{0}", lineCommentPrologue);

            var multiLineCommentPrologue = Comment.GetMultiLineCommentPrologue(kind);
            foreach (var line in lines)
                WriteLine("{0} {1}", multiLineCommentPrologue, line);

            var lineCommentEpilogue = Comment.GetLineCommentEpilogue(kind);
            if (!string.IsNullOrWhiteSpace(lineCommentEpilogue))
                WriteLine("{0}", lineCommentEpilogue);

            PopBlock();
        }

        #endregion

        #region Enum generation

        public virtual void GenerateEnumItems(Enumeration @enum)
        {
            for (int i = 0; i < @enum.Items.Count; i++)
            {
                var item = @enum.Items[i];
                if (!item.IsGenerated)
                    continue;

                item.Visit(this);
                WriteLine(i == @enum.Items.Count - 1 ? string.Empty : ",");
            }
        }

        public virtual bool VisitEnumItemDecl(Enumeration.Item item)
        {
            if (item.Comment != null)
                GenerateInlineSummary(item.Comment);

            Write(item.Name);

            var @enum = item.Namespace as Enumeration;
            if (item.ExplicitValue)
                Write(" = {0}", @enum.GetItemValueAsString(item));

            return true;
        }

        #endregion

        #region Class generation

        public virtual void GenerateClassSpecifier(Class @class)
        {
        }

        #endregion

        #region Method generation

        public virtual void GenerateMethodSpecifier(Method method, Class @class)
        {
        }

        #endregion

        #region Visitor methods

        public bool AlreadyVisited(CppSharp.AST.Type type)
        {
            return !Visited.Add(type);
        }

        public bool AlreadyVisited(Declaration decl)
        {
            return !Visited.Add(decl);
        }

        public virtual bool VisitDeclaration(Declaration decl)
        {
            return !AlreadyVisited(decl);
        }

        public virtual bool VisitTranslationUnit(TranslationUnit unit)
        {
            return VisitNamespace(unit);
        }

        public virtual bool VisitDeclContext(DeclarationContext context)
        {
            foreach (var decl in context.Declarations)
                if (decl.IsGenerated)
                    decl.Visit(this);

            return true;
        }

        public virtual bool VisitClassDecl(Class @class)
        {
            return VisitDeclContext(@class);
        }

        public virtual bool VisitFieldDecl(Field field)
        {
            throw new NotImplementedException();
        }

        public virtual bool VisitFunctionDecl(Function function)
        {
            throw new NotImplementedException();
        }

        public virtual bool VisitMethodDecl(Method method)
        {
            throw new NotImplementedException();
        }

        public virtual bool VisitParameterDecl(Parameter parameter)
        {
            throw new NotImplementedException();
        }

        public virtual bool VisitTypedefNameDecl(TypedefNameDecl typedef)
        {
            throw new NotImplementedException();
        }

        public virtual bool VisitTypedefDecl(TypedefDecl typedef)
        {
            return VisitTypedefNameDecl(typedef);
        }

        public virtual bool VisitTypeAliasDecl(TypeAlias typeAlias)
        {
            return VisitTypedefNameDecl(typeAlias);
        }

        public virtual bool VisitEnumDecl(Enumeration @enum)
        {
            throw new NotImplementedException();
        }

        public virtual bool VisitVariableDecl(Variable variable)
        {
            throw new NotImplementedException();
        }

        public virtual bool VisitMacroDefinition(MacroDefinition macro)
        {
            throw new NotImplementedException();
        }

        public virtual bool VisitNamespace(Namespace @namespace)
        {
            return VisitDeclContext(@namespace);
        }

        public virtual bool VisitEvent(Event @event)
        {
            throw new NotImplementedException();
        }

        public virtual bool VisitProperty(Property property)
        {
            if (!VisitDeclaration(property))
                return false;

            if (VisitOptions.VisitPropertyAccessors)
            {
                if (property.GetMethod != null)
                    property.GetMethod.Visit(this);
        
                if (property.SetMethod != null)
                    property.SetMethod.Visit(this);
            }

            return true;
        }

        public virtual bool VisitFriend(Friend friend)
        {
            return true;
        }

        public virtual bool VisitClassTemplateDecl(ClassTemplate template)
        {
            throw new NotImplementedException();
        }

        public virtual bool VisitClassTemplateSpecializationDecl(ClassTemplateSpecialization specialization)
        {
            return VisitClassDecl(specialization);
        }

        public virtual bool VisitFunctionTemplateDecl(FunctionTemplate template)
        {
            throw new NotImplementedException();
        }

        public virtual bool VisitFunctionTemplateSpecializationDecl(FunctionTemplateSpecialization specialization)
        {
            throw new NotImplementedException();
        }

        public virtual bool VisitVarTemplateDecl(VarTemplate template)
        {
            throw new NotImplementedException();
        }

        public virtual bool VisitVarTemplateSpecializationDecl(VarTemplateSpecialization template)
        {
            throw new NotImplementedException();
        }

        public virtual bool VisitTemplateTemplateParameterDecl(TemplateTemplateParameter templateTemplateParameter)
        {
            throw new NotImplementedException();
        }

        public virtual bool VisitTemplateParameterDecl(TypeTemplateParameter templateParameter)
        {
            throw new NotImplementedException();
        }

        public virtual bool VisitNonTypeTemplateParameterDecl(NonTypeTemplateParameter nonTypeTemplateParameter)
        {
            throw new NotImplementedException();
        }

        public virtual bool VisitTypeAliasTemplateDecl(TypeAliasTemplate typeAliasTemplate)
        {
            throw new NotImplementedException();
        }

        public bool VisitTagType(TagType tag, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitUnresolvedUsingDecl(UnresolvedUsingTypename unresolvedUsingTypename)
        {
            throw new NotImplementedException();
        }

        public bool VisitArrayType(ArrayType array, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitFunctionType(FunctionType function, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitPointerType(PointerType pointer, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitMemberPointerType(MemberPointerType member, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitBuiltinType(BuiltinType builtin, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitTypedefType(TypedefType typedef, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitAttributedType(AttributedType attributed, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitDecayedType(DecayedType decayed, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitTemplateSpecializationType(TemplateSpecializationType template, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitDependentTemplateSpecializationType(DependentTemplateSpecializationType template, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitPrimitiveType(PrimitiveType type, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitDeclaration(Declaration decl, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitTemplateParameterType(TemplateParameterType param, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitTemplateParameterSubstitutionType(TemplateParameterSubstitutionType param, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitInjectedClassNameType(InjectedClassNameType injected, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitDependentNameType(DependentNameType dependent, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitPackExpansionType(PackExpansionType packExpansionType, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitUnaryTransformType(UnaryTransformType unaryTransformType, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitUnresolvedUsingType(UnresolvedUsingType unresolvedUsingType, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitVectorType(VectorType vectorType, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitCILType(CILType type, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitUnsupportedType(UnsupportedType type, TypeQualifiers quals)
        {
            throw new NotImplementedException();
        }

        public bool VisitStmt(Stmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitDeclStmt(DeclStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitNullStmt(NullStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCompoundStmt(CompoundStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitSwitchCase(SwitchCase stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCaseStmt(CaseStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitDefaultStmt(DefaultStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitLabelStmt(LabelStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitAttributedStmt(AttributedStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitIfStmt(IfStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitSwitchStmt(SwitchStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitWhileStmt(WhileStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitDoStmt(DoStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitForStmt(ForStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitGotoStmt(GotoStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitIndirectGotoStmt(IndirectGotoStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitContinueStmt(ContinueStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitBreakStmt(BreakStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitReturnStmt(ReturnStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitAsmStmt(AsmStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitGCCAsmStmt(GCCAsmStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitMSAsmStmt(MSAsmStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitSEHExceptStmt(SEHExceptStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitSEHFinallyStmt(SEHFinallyStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitSEHTryStmt(SEHTryStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitSEHLeaveStmt(SEHLeaveStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCapturedStmt(CapturedStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXCatchStmt(CXXCatchStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXTryStmt(CXXTryStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXForRangeStmt(CXXForRangeStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitMSDependentExistsStmt(MSDependentExistsStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCoroutineBodyStmt(CoroutineBodyStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCoreturnStmt(CoreturnStmt stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitExpr(Expr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitFullExpr(FullExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitConstantExpr(ConstantExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitOpaqueValueExpr(OpaqueValueExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitDeclRefExpr(DeclRefExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitIntegerLiteral(IntegerLiteral stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitFixedPointLiteral(FixedPointLiteral stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCharacterLiteral(CharacterLiteral stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitFloatingLiteral(FloatingLiteral stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitImaginaryLiteral(ImaginaryLiteral stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitStringLiteral(StringLiteral stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitPredefinedExpr(PredefinedExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitParenExpr(ParenExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitUnaryOperator(UnaryOperator stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitOffsetOfExpr(OffsetOfExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitUnaryExprOrTypeTraitExpr(UnaryExprOrTypeTraitExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitArraySubscriptExpr(ArraySubscriptExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCallExpr(CallExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitMemberExpr(MemberExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCompoundLiteralExpr(CompoundLiteralExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCastExpr(CastExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitImplicitCastExpr(ImplicitCastExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitExplicitCastExpr(ExplicitCastExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCStyleCastExpr(CStyleCastExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitBinaryOperator(BinaryOperator stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCompoundAssignOperator(CompoundAssignOperator stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitAbstractConditionalOperator(AbstractConditionalOperator stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitConditionalOperator(ConditionalOperator stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitBinaryConditionalOperator(BinaryConditionalOperator stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitAddrLabelExpr(AddrLabelExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitStmtExpr(StmtExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitShuffleVectorExpr(ShuffleVectorExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitConvertVectorExpr(ConvertVectorExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitChooseExpr(ChooseExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitGNUNullExpr(GNUNullExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitVAArgExpr(VAArgExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitInitListExpr(InitListExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitDesignatedInitExpr(DesignatedInitExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitNoInitExpr(NoInitExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitDesignatedInitUpdateExpr(DesignatedInitUpdateExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitArrayInitLoopExpr(ArrayInitLoopExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitArrayInitIndexExpr(ArrayInitIndexExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitImplicitValueInitExpr(ImplicitValueInitExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitParenListExpr(ParenListExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitGenericSelectionExpr(GenericSelectionExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitExtVectorElementExpr(ExtVectorElementExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitBlockExpr(BlockExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitAsTypeExpr(AsTypeExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitPseudoObjectExpr(PseudoObjectExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitAtomicExpr(AtomicExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitTypoExpr(TypoExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXOperatorCallExpr(CXXOperatorCallExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXMemberCallExpr(CXXMemberCallExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCUDAKernelCallExpr(CUDAKernelCallExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXNamedCastExpr(CXXNamedCastExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXStaticCastExpr(CXXStaticCastExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXDynamicCastExpr(CXXDynamicCastExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXReinterpretCastExpr(CXXReinterpretCastExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXConstCastExpr(CXXConstCastExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitUserDefinedLiteral(UserDefinedLiteral stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXBoolLiteralExpr(CXXBoolLiteralExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXNullPtrLiteralExpr(CXXNullPtrLiteralExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXStdInitializerListExpr(CXXStdInitializerListExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXTypeidExpr(CXXTypeidExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitMSPropertyRefExpr(MSPropertyRefExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitMSPropertySubscriptExpr(MSPropertySubscriptExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXUuidofExpr(CXXUuidofExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXThisExpr(CXXThisExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXThrowExpr(CXXThrowExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXDefaultArgExpr(CXXDefaultArgExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXDefaultInitExpr(CXXDefaultInitExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXBindTemporaryExpr(CXXBindTemporaryExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXConstructExpr(CXXConstructExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXInheritedCtorInitExpr(CXXInheritedCtorInitExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXFunctionalCastExpr(CXXFunctionalCastExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXTemporaryObjectExpr(CXXTemporaryObjectExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitLambdaExpr(LambdaExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXScalarValueInitExpr(CXXScalarValueInitExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXNewExpr(CXXNewExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXDeleteExpr(CXXDeleteExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXPseudoDestructorExpr(CXXPseudoDestructorExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitTypeTraitExpr(TypeTraitExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitArrayTypeTraitExpr(ArrayTypeTraitExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitExpressionTraitExpr(ExpressionTraitExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitOverloadExpr(OverloadExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitUnresolvedLookupExpr(UnresolvedLookupExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitDependentScopeDeclRefExpr(DependentScopeDeclRefExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitExprWithCleanups(ExprWithCleanups stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXUnresolvedConstructExpr(CXXUnresolvedConstructExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXDependentScopeMemberExpr(CXXDependentScopeMemberExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitUnresolvedMemberExpr(UnresolvedMemberExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXNoexceptExpr(CXXNoexceptExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitPackExpansionExpr(PackExpansionExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitSizeOfPackExpr(SizeOfPackExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitSubstNonTypeTemplateParmExpr(SubstNonTypeTemplateParmExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitSubstNonTypeTemplateParmPackExpr(SubstNonTypeTemplateParmPackExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitFunctionParmPackExpr(FunctionParmPackExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitMaterializeTemporaryExpr(MaterializeTemporaryExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCXXFoldExpr(CXXFoldExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCoroutineSuspendExpr(CoroutineSuspendExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCoawaitExpr(CoawaitExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitDependentCoawaitExpr(DependentCoawaitExpr stmt)
        {
            throw new NotImplementedException();
        }

        public bool VisitCoyieldExpr(CoyieldExpr stmt)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public static class Helpers
    {
        public static Regex RegexTag = new Regex(@"^(<|</)[a-zA-Z][\w\-]*?>?$");
        public static Regex RegexCommentCommandLeftover = new Regex(@"^\S*");
        public static readonly string InternalStruct = Generator.GeneratedIdentifier("Internal");
        public static readonly string InstanceField = Generator.GeneratedIdentifier("instance");
        public static readonly string InstanceIdentifier = Generator.GeneratedIdentifier("Instance");
        public static readonly string PrimaryBaseOffsetIdentifier = Generator.GeneratedIdentifier("PrimaryBaseOffset");
        public static readonly string ReturnIdentifier = Generator.GeneratedIdentifier("ret");
        public static readonly string DummyIdentifier = Generator.GeneratedIdentifier("dummy");
        public static readonly string TargetIdentifier = Generator.GeneratedIdentifier("target");
        public static readonly string SlotIdentifier = Generator.GeneratedIdentifier("slot");
        public static readonly string PtrIdentifier = Generator.GeneratedIdentifier("ptr");

        public static readonly string OwnsNativeInstanceIdentifier = Generator.GeneratedIdentifier("ownsNativeInstance");

        public static readonly string CreateInstanceIdentifier = Generator.GeneratedIdentifier("CreateInstance");

        public static string GetSuffixForInternal(DeclarationContext @class)
        {
            if (@class == null)
                return string.Empty;

            Class template = null;
            var specialization = @class as ClassTemplateSpecialization ??
                @class.Namespace as ClassTemplateSpecialization;
            if (specialization != null)
            {
                template = specialization.TemplatedDecl.TemplatedClass;
                if (@class != specialization)
                    template = template.Classes.FirstOrDefault(c => c.Name == @class.Name);
            }

            if (template == null || !template.HasDependentValueFieldInLayout())
                return string.Empty;

            if (specialization.Arguments.All(
                a => a.Type.Type?.IsAddress() == true))
                return "_Ptr";

            return GetSuffixFor(specialization);
        }

        public static string GetSuffixFor(Declaration decl)
        {
            var suffixBuilder = new StringBuilder(decl.USR);
            for (int i = 0; i < suffixBuilder.Length; i++)
                if (!char.IsLetterOrDigit(suffixBuilder[i]))
                    suffixBuilder[i] = '_';
            const int maxCSharpIdentifierLength = 480;
            if (suffixBuilder.Length > maxCSharpIdentifierLength)
                return suffixBuilder.Remove(maxCSharpIdentifierLength,
                    suffixBuilder.Length - maxCSharpIdentifierLength).ToString();
            return suffixBuilder.ToString();
        }

        public static string GetAccess(AccessSpecifier accessSpecifier)
        {
            switch (accessSpecifier)
            {
                case AccessSpecifier.Private:
                case AccessSpecifier.Internal:
                    return "internal ";
                case AccessSpecifier.Protected:
                    return "protected ";
                default:
                    return "public ";
            }
        }
    }
}
