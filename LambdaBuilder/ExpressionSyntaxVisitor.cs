namespace LambdaBuilder
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    using TypeInfo = Microsoft.CodeAnalysis.TypeInfo;

    internal class ExpressionSyntaxVisitor : CSharpSyntaxVisitor<Expression>
    {
        private readonly Stack<ParameterExpression> parameters;

        private readonly ExpressionSyntaxVisitor parent;

        private readonly SemanticModel semanticModel;

        private readonly TypeResolutionService resolutionService;

        public ExpressionSyntaxVisitor(ExpressionSyntaxVisitor parent, SemanticModel semanticModel, ParameterExpression[] inputParameters, TypeResolutionService resolutionService)
        {
            this.parent = parent;
            this.semanticModel = semanticModel;

            this.parameters = new Stack<ParameterExpression>(inputParameters);
            this.resolutionService = resolutionService;
        }

        public override Expression VisitSizeOfExpression(SizeOfExpressionSyntax node)
        {
            return base.VisitSizeOfExpression(node);
        }

        public override Expression VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression.Kind() == SyntaxKind.SimpleMemberAccessExpression)
            {
                var method = (MemberAccessExpressionSyntax)node.Expression;

                var nameInfo = ModelExtensions.GetSymbolInfo(this.semanticModel, method.Name);

                var symbol = nameInfo.Symbol;

                if (symbol == null || symbol.Kind != SymbolKind.Method)
                {
                    switch (nameInfo.CandidateReason)
                    {
                        case CandidateReason.Inaccessible:
                            throw new Exception(string.Format("ћетод {0} не доступен", method.Name.Identifier.Text));

                        case CandidateReason.OverloadResolutionFailure:
                            symbol = nameInfo.CandidateSymbols.Cast<IMethodSymbol>().First(x => x.Parameters.Length == node.ArgumentList.Arguments.Count);

                            break;

                        default:
                            throw new Exception(string.Format("ћетод {0} не найден", method.Name.Identifier.Text));
                    }
                }

                var methodSymbol = (IMethodSymbol)symbol;
                switch (methodSymbol.MethodKind)
                {
                    case MethodKind.ReducedExtension:
                    {
                        var expression = method.Expression.Accept(this);
                        var argumentTypes = new List<Type>();
                        argumentTypes.Add(expression.Type);
                        argumentTypes.AddRange(methodSymbol.Parameters.Select(x => this.ResolveType(x.Type, node)));
                        var methodInfo = this.ResolveMethod((IMethodSymbol)symbol, argumentTypes.ToArray());
                        var arguments = new List<Expression>();
                        arguments.Add(expression);
                        arguments.AddRange(node.ArgumentList.Arguments.Select(x => x.Expression.Accept(this)));

                        return Expression.Call(methodInfo, arguments.ToArray());
                    }

                    case MethodKind.Ordinary:
                    {
                        var @params = methodSymbol.Parameters.Select(x => this.ResolveType(x.Type, node)).ToArray();

                        var methodInfo = this.ResolveMethod(methodSymbol, @params.ToArray());

                        var arguments = node.ArgumentList.Arguments.Select(x => x.Expression.Accept(this)).ToArray();

                        // при необходимости конвертим типы аргументов
                        var parameters = methodInfo.GetParameters();
                        for (var idx = 0; idx < parameters.Length; idx++)
                        {
                            var parameter = parameters[idx];
                            if (parameter.ParameterType != arguments[idx].Type)
                            {
                                arguments[idx] = Expression.Convert(arguments[idx], parameter.ParameterType);
                            }
                        }

                        if (methodInfo.IsStatic)
                        {
                            return Expression.Call(null, methodInfo, arguments);
                        }

                        var expression = method.Expression.Accept(this);
                        return Expression.Call(expression, methodInfo, arguments);
                    }
                    default:
                        throw new NotImplementedException();
                }
            }

            throw new NotImplementedException();
        }

        public override Expression VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            return base.VisitElementAccessExpression(node);
        }

        private bool CheckMethodArguments(ParameterInfo[] @params, Type[] parameterTypes)
        {
            if (@params.Length < parameterTypes.Length)
            {
                return false;
            }

            if (parameterTypes.Where((t, idx) => !@params[idx].ParameterType.IsAssignableFrom(t)).Any())
            {
                return false;
            }

            if (@params.Length > parameterTypes.Length)
            {
                return @params.Skip(parameterTypes.Length).All(x => x.IsOptional);
            }

            return true;
        }

        private MethodInfo ResolveMethod(IMethodSymbol symbol, Type[] argumentTypes)
        {
            var type = this.ResolveType(symbol.ContainingType);

            var methods = type.GetMethods().Where(x => x.Name == symbol.Name).ToArray();

            foreach (var method in methods)
            {
                if (method.IsGenericMethod)
                {
                    if (!symbol.IsGenericMethod)
                    {
                        continue;
                    }

                    if (symbol.TypeArguments.Length != method.GetGenericArguments().Length)
                    {
                        continue;
                    }

                    var typeArguments = symbol.TypeArguments.Select(typeSymbol => this.ResolveType(typeSymbol)).ToArray();
                    var realMethod = method.MakeGenericMethod(typeArguments);

                    if (realMethod.ReturnType != this.ResolveType(symbol.ReturnType))
                    {
                        continue;
                    }

                    if (this.CheckMethodArguments(realMethod.GetParameters(), argumentTypes))
                    {
                        return realMethod;
                    }
                }
                else if (this.CheckMethodArguments(method.GetParameters(), argumentTypes))
                {
                    return method;
                }
            }

            throw new NotImplementedException();
        }

        private Type ResolveType(ITypeSymbol typeSymbol, SyntaxNode node = null)
        {
            return this.resolutionService.Resolve(typeSymbol);
        }

        public override Expression VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            return base.VisitAccessorDeclaration(node);
        }

        public override Expression VisitParameterList(ParameterListSyntax node)
        {
            return base.VisitParameterList(node);
        }

        public override Expression VisitBracketedParameterList(BracketedParameterListSyntax node)
        {
            return base.VisitBracketedParameterList(node);
        }

        public override Expression VisitParameter(ParameterSyntax node)
        {
            return base.VisitParameter(node);
        }

        public override Expression VisitIncompleteMember(IncompleteMemberSyntax node)
        {
            return base.VisitIncompleteMember(node);
        }

        public override Expression VisitSkippedTokensTrivia(SkippedTokensTriviaSyntax node)
        {
            return base.VisitSkippedTokensTrivia(node);
        }

        public override Expression VisitDocumentationCommentTrivia(DocumentationCommentTriviaSyntax node)
        {
            return base.VisitDocumentationCommentTrivia(node);
        }

        public override Expression VisitTypeCref(TypeCrefSyntax node)
        {
            return base.VisitTypeCref(node);
        }

        public override Expression VisitQualifiedCref(QualifiedCrefSyntax node)
        {
            return base.VisitQualifiedCref(node);
        }

        public override Expression VisitNameMemberCref(NameMemberCrefSyntax node)
        {
            return base.VisitNameMemberCref(node);
        }

        public override Expression VisitIndexerMemberCref(IndexerMemberCrefSyntax node)
        {
            return base.VisitIndexerMemberCref(node);
        }

        public override Expression VisitOperatorMemberCref(OperatorMemberCrefSyntax node)
        {
            return base.VisitOperatorMemberCref(node);
        }

        public override Expression VisitConversionOperatorMemberCref(ConversionOperatorMemberCrefSyntax node)
        {
            return base.VisitConversionOperatorMemberCref(node);
        }

        public override Expression VisitCrefParameterList(CrefParameterListSyntax node)
        {
            return base.VisitCrefParameterList(node);
        }

        public override Expression VisitCrefBracketedParameterList(CrefBracketedParameterListSyntax node)
        {
            return base.VisitCrefBracketedParameterList(node);
        }

        public override Expression VisitCrefParameter(CrefParameterSyntax node)
        {
            return base.VisitCrefParameter(node);
        }

        public override Expression VisitXmlElement(XmlElementSyntax node)
        {
            return base.VisitXmlElement(node);
        }

        public override Expression VisitXmlElementStartTag(XmlElementStartTagSyntax node)
        {
            return base.VisitXmlElementStartTag(node);
        }

        public override Expression VisitXmlElementEndTag(XmlElementEndTagSyntax node)
        {
            return base.VisitXmlElementEndTag(node);
        }

        public override Expression VisitXmlEmptyElement(XmlEmptyElementSyntax node)
        {
            return base.VisitXmlEmptyElement(node);
        }

        public override Expression VisitXmlName(XmlNameSyntax node)
        {
            return base.VisitXmlName(node);
        }

        public override Expression VisitXmlPrefix(XmlPrefixSyntax node)
        {
            return base.VisitXmlPrefix(node);
        }

        public override Expression VisitXmlTextAttribute(XmlTextAttributeSyntax node)
        {
            return base.VisitXmlTextAttribute(node);
        }

        public override Expression VisitXmlCrefAttribute(XmlCrefAttributeSyntax node)
        {
            return base.VisitXmlCrefAttribute(node);
        }

        public override Expression VisitXmlNameAttribute(XmlNameAttributeSyntax node)
        {
            return base.VisitXmlNameAttribute(node);
        }

        public override Expression VisitXmlText(XmlTextSyntax node)
        {
            return base.VisitXmlText(node);
        }

        public override Expression VisitXmlCDataSection(XmlCDataSectionSyntax node)
        {
            return base.VisitXmlCDataSection(node);
        }

        public override Expression VisitXmlProcessingInstruction(XmlProcessingInstructionSyntax node)
        {
            return base.VisitXmlProcessingInstruction(node);
        }

        public override Expression VisitXmlComment(XmlCommentSyntax node)
        {
            return base.VisitXmlComment(node);
        }

        public override Expression VisitIfDirectiveTrivia(IfDirectiveTriviaSyntax node)
        {
            return base.VisitIfDirectiveTrivia(node);
        }

        public override Expression VisitElifDirectiveTrivia(ElifDirectiveTriviaSyntax node)
        {
            return base.VisitElifDirectiveTrivia(node);
        }

        public override Expression VisitElseDirectiveTrivia(ElseDirectiveTriviaSyntax node)
        {
            return base.VisitElseDirectiveTrivia(node);
        }

        public override Expression VisitEndIfDirectiveTrivia(EndIfDirectiveTriviaSyntax node)
        {
            return base.VisitEndIfDirectiveTrivia(node);
        }

        public override Expression VisitRegionDirectiveTrivia(RegionDirectiveTriviaSyntax node)
        {
            return base.VisitRegionDirectiveTrivia(node);
        }

        public override Expression VisitEndRegionDirectiveTrivia(EndRegionDirectiveTriviaSyntax node)
        {
            return base.VisitEndRegionDirectiveTrivia(node);
        }

        public override Expression VisitErrorDirectiveTrivia(ErrorDirectiveTriviaSyntax node)
        {
            return base.VisitErrorDirectiveTrivia(node);
        }

        public override Expression VisitWarningDirectiveTrivia(WarningDirectiveTriviaSyntax node)
        {
            return base.VisitWarningDirectiveTrivia(node);
        }

        public override Expression VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            return base.VisitIndexerDeclaration(node);
        }

        public override Expression VisitAccessorList(AccessorListSyntax node)
        {
            return base.VisitAccessorList(node);
        }

        public override Expression VisitTypeArgumentList(TypeArgumentListSyntax node)
        {
            return base.VisitTypeArgumentList(node);
        }

        public override Expression VisitAliasQualifiedName(AliasQualifiedNameSyntax node)
        {
            return base.VisitAliasQualifiedName(node);
        }

        public override Expression VisitPredefinedType(PredefinedTypeSyntax node)
        {
            var typeInfo = ModelExtensions.GetTypeInfo(this.semanticModel, node);
            var type = this.ResolveType(typeInfo.Type, node);

            return Expression.Constant(type);
        }

        public override Expression VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
        {
            return base.VisitAnonymousMethodExpression(node);
        }

        public override Expression VisitAnonymousObjectMemberDeclarator(AnonymousObjectMemberDeclaratorSyntax node)
        {
            return base.VisitAnonymousObjectMemberDeclarator(node);
        }

        public override Expression VisitBracketedArgumentList(BracketedArgumentListSyntax node)
        {
            return base.VisitBracketedArgumentList(node);
        }

        public override Expression VisitArgument(ArgumentSyntax node)
        {
            return node.Expression.Accept(this);
        }

        public override Expression VisitNameColon(NameColonSyntax node)
        {
            return base.VisitNameColon(node);
        }

        public override Expression VisitArgumentList(ArgumentListSyntax node)
        {
            return base.VisitArgumentList(node);
        }

        public override Expression VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
        {
            return base.VisitArrayCreationExpression(node);
        }

        public override Expression VisitImplicitArrayCreationExpression(ImplicitArrayCreationExpressionSyntax node)
        {
            var initializers = node.Initializer.Expressions.Select(x => x.Accept(this)).ToArray();
            var isSigleType = initializers.Length == 1 || initializers.Select(x => x.Type).Distinct().Count() == 1;
            var arrayType = isSigleType ? initializers.First().Type : typeof(object);

            return Expression.NewArrayInit(arrayType, initializers);
        }

        public override Expression VisitStackAllocArrayCreationExpression(StackAllocArrayCreationExpressionSyntax node)
        {
            return base.VisitStackAllocArrayCreationExpression(node);
        }

        public override Expression VisitQueryExpression(QueryExpressionSyntax node)
        {
            return base.VisitQueryExpression(node);
        }

        public override Expression VisitQueryBody(QueryBodySyntax node)
        {
            return base.VisitQueryBody(node);
        }

        public override Expression VisitFromClause(FromClauseSyntax node)
        {
            return base.VisitFromClause(node);
        }

        public override Expression VisitLetClause(LetClauseSyntax node)
        {
            return base.VisitLetClause(node);
        }

        public override Expression VisitJoinClause(JoinClauseSyntax node)
        {
            return base.VisitJoinClause(node);
        }

        public override Expression VisitJoinIntoClause(JoinIntoClauseSyntax node)
        {
            return base.VisitJoinIntoClause(node);
        }

        public override Expression VisitWhereClause(WhereClauseSyntax node)
        {
            return base.VisitWhereClause(node);
        }

        public override Expression VisitOrderByClause(OrderByClauseSyntax node)
        {
            return base.VisitOrderByClause(node);
        }

        public override Expression VisitOrdering(OrderingSyntax node)
        {
            return base.VisitOrdering(node);
        }

        public override Expression VisitSelectClause(SelectClauseSyntax node)
        {
            return base.VisitSelectClause(node);
        }

        public override Expression VisitGroupClause(GroupClauseSyntax node)
        {
            return base.VisitGroupClause(node);
        }

        public override Expression VisitQueryContinuation(QueryContinuationSyntax node)
        {
            return base.VisitQueryContinuation(node);
        }

        public override Expression VisitOmittedArraySizeExpression(OmittedArraySizeExpressionSyntax node)
        {
            return base.VisitOmittedArraySizeExpression(node);
        }

        public override Expression VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
        {
            return base.VisitInterpolatedStringExpression(node);
        }

        public override Expression VisitInterpolatedStringText(InterpolatedStringTextSyntax node)
        {
            return base.VisitInterpolatedStringText(node);
        }

        public override Expression VisitInterpolation(InterpolationSyntax node)
        {
            return base.VisitInterpolation(node);
        }

        public override Expression VisitInterpolationAlignmentClause(InterpolationAlignmentClauseSyntax node)
        {
            return base.VisitInterpolationAlignmentClause(node);
        }

        public override Expression VisitInterpolationFormatClause(InterpolationFormatClauseSyntax node)
        {
            return base.VisitInterpolationFormatClause(node);
        }

        public override Expression VisitGlobalStatement(GlobalStatementSyntax node)
        {
            return base.VisitGlobalStatement(node);
        }

        public override Expression VisitBlock(BlockSyntax node)
        {
            var inBlockExpressions = new List<Expression>();
            var inBlockVariables = new List<ParameterExpression>();

            var inlineParameters = 0;
            foreach (var statement in node.Statements)
            {
                var expression = this.Visit(statement);

                var wrapper = expression as VariableBlockWrapper;
                if (wrapper != null)
                {
                    inBlockExpressions.AddRange(wrapper.Expressions);
                    inBlockVariables.AddRange(wrapper.Variables);
                }
                else
                {
                    inBlockExpressions.Add(expression);
                }

                this.PushParameters(expression, ref inlineParameters);
            }

            while (inlineParameters != 0)
            {
                this.parameters.Pop();
                inlineParameters--;
            }

            return Expression.Block(inBlockVariables, inBlockExpressions);
        }

        private void PushParameters(Expression expression, ref int count)
        {
            var parameter = expression as ParameterExpression;
            if (parameter != null)
            {
                this.parameters.Push(parameter);
                count++;
            }

            var block = expression as BlockExpression;
            if (block != null)
            {
                foreach (var expr in block.Expressions)
                {
                    this.PushParameters(expr, ref count);
                }

                foreach (var variable in block.Variables)
                {
                    this.PushParameters(variable, ref count);
                }
            }

            var wrapper = expression as VariableBlockWrapper;
            if (wrapper != null)
            {
                foreach (var expr in wrapper.Expressions)
                {
                    this.PushParameters(expr, ref count);
                }

                foreach (var variable in wrapper.Variables)
                {
                    this.PushParameters(variable, ref count);
                }
            }
        }

        public override Expression VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            // пока не можем обработать объ€вление нескольких переменных вида
            // var x = 1, y = 2
            // только var x = 1

            Type variableType = null;
            var variable = node.Declaration.Variables.First();
            var typeInfo = ModelExtensions.GetTypeInfo(this.semanticModel, node.Declaration.Type);

            if (typeInfo.Type != null)
            {
                variableType = this.ResolveType(typeInfo.Type, node);
            }
            else if (node.Declaration.Type.IsVar)
            {
                typeInfo = ModelExtensions.GetTypeInfo(this.semanticModel, variable.Initializer.Value);
                variableType = this.ResolveType(typeInfo.Type, node);
            }

            var expression = Expression.Variable(variableType, variable.Identifier.Text);
            if (variable.Initializer != null)
            {
                var initializer = variable.Initializer.Value.Accept(this);
                var block = Expression.Block(new[] { expression }, Expression.Assign(expression, initializer));
                return new VariableBlockWrapper(block);
            }

            return expression;
        }

        public override Expression VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            return base.VisitVariableDeclaration(node);
        }

        public override Expression VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            return base.VisitVariableDeclarator(node);
        }

        public override Expression VisitEqualsValueClause(EqualsValueClauseSyntax node)
        {
            return base.VisitEqualsValueClause(node);
        }

        public override Expression VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            return node.Expression.Accept(this);
        }

        public override Expression VisitEmptyStatement(EmptyStatementSyntax node)
        {
            return base.VisitEmptyStatement(node);
        }

        public override Expression VisitLabeledStatement(LabeledStatementSyntax node)
        {
            return base.VisitLabeledStatement(node);
        }

        public override Expression VisitGotoStatement(GotoStatementSyntax node)
        {
            return base.VisitGotoStatement(node);
        }

        public override Expression VisitBreakStatement(BreakStatementSyntax node)
        {
            return base.VisitBreakStatement(node);
        }

        public override Expression VisitContinueStatement(ContinueStatementSyntax node)
        {
            return base.VisitContinueStatement(node);
        }

        public override Expression VisitReturnStatement(ReturnStatementSyntax node)
        {
            var expression = this.Visit(node.Expression);
            return expression;
            //var target = Expression.Label(expression.Type);
            //var @return = Expression.Return(target, expression);
            //var label = Expression.Label(target, Expression.Default(expression.Type));

            //var block = Expression.Block(@return, label);

            //return new VirtualBlockWrapper(block);
        }

        public override Expression VisitThrowStatement(ThrowStatementSyntax node)
        {
            return base.VisitThrowStatement(node);
        }

        public override Expression VisitYieldStatement(YieldStatementSyntax node)
        {
            return base.VisitYieldStatement(node);
        }

        public override Expression VisitWhileStatement(WhileStatementSyntax node)
        {
            return base.VisitWhileStatement(node);
        }

        public override Expression VisitDoStatement(DoStatementSyntax node)
        {
            return base.VisitDoStatement(node);
        }

        public override Expression VisitForStatement(ForStatementSyntax node)
        {
            return base.VisitForStatement(node);
        }

        public override Expression VisitForEachStatement(ForEachStatementSyntax node)
        {
            return base.VisitForEachStatement(node);
        }

        public override Expression VisitUsingStatement(UsingStatementSyntax node)
        {
            return base.VisitUsingStatement(node);
        }

        public override Expression VisitFixedStatement(FixedStatementSyntax node)
        {
            return base.VisitFixedStatement(node);
        }

        public override Expression VisitCheckedStatement(CheckedStatementSyntax node)
        {
            return base.VisitCheckedStatement(node);
        }

        public override Expression VisitUnsafeStatement(UnsafeStatementSyntax node)
        {
            return base.VisitUnsafeStatement(node);
        }

        public override Expression VisitLockStatement(LockStatementSyntax node)
        {
            return base.VisitLockStatement(node);
        }

        public override Expression VisitIfStatement(IfStatementSyntax node)
        {
            return base.VisitIfStatement(node);
        }

        public override Expression VisitElseClause(ElseClauseSyntax node)
        {
            return base.VisitElseClause(node);
        }

        public override Expression VisitSwitchStatement(SwitchStatementSyntax node)
        {
            return base.VisitSwitchStatement(node);
        }

        public override Expression VisitSwitchSection(SwitchSectionSyntax node)
        {
            return base.VisitSwitchSection(node);
        }

        public override Expression VisitCaseSwitchLabel(CaseSwitchLabelSyntax node)
        {
            return base.VisitCaseSwitchLabel(node);
        }

        public override Expression VisitDefaultSwitchLabel(DefaultSwitchLabelSyntax node)
        {
            return base.VisitDefaultSwitchLabel(node);
        }

        public override Expression VisitTryStatement(TryStatementSyntax node)
        {
            return base.VisitTryStatement(node);
        }

        public override Expression VisitCatchClause(CatchClauseSyntax node)
        {
            return base.VisitCatchClause(node);
        }

        public override Expression VisitCatchDeclaration(CatchDeclarationSyntax node)
        {
            return base.VisitCatchDeclaration(node);
        }

        public override Expression VisitCatchFilterClause(CatchFilterClauseSyntax node)
        {
            return base.VisitCatchFilterClause(node);
        }

        public override Expression VisitFinallyClause(FinallyClauseSyntax node)
        {
            return base.VisitFinallyClause(node);
        }

        public override Expression VisitCompilationUnit(CompilationUnitSyntax node)
        {
            return base.VisitCompilationUnit(node);
        }

        public override Expression VisitExternAliasDirective(ExternAliasDirectiveSyntax node)
        {
            return base.VisitExternAliasDirective(node);
        }

        public override Expression VisitUsingDirective(UsingDirectiveSyntax node)
        {
            return base.VisitUsingDirective(node);
        }

        public override Expression VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            return base.VisitNamespaceDeclaration(node);
        }

        public override Expression VisitArrayRankSpecifier(ArrayRankSpecifierSyntax node)
        {
            return base.VisitArrayRankSpecifier(node);
        }

        public override Expression VisitPointerType(PointerTypeSyntax node)
        {
            return base.VisitPointerType(node);
        }

        public override Expression VisitNullableType(NullableTypeSyntax node)
        {
            return base.VisitNullableType(node);
        }

        public override Expression VisitOmittedTypeArgument(OmittedTypeArgumentSyntax node)
        {
            return base.VisitOmittedTypeArgument(node);
        }

        public override Expression VisitArrayType(ArrayTypeSyntax node)
        {
            return base.VisitArrayType(node);
        }

        public override Expression VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            return base.VisitPropertyDeclaration(node);
        }

        public override Expression VisitArrowExpressionClause(ArrowExpressionClauseSyntax node)
        {
            return base.VisitArrowExpressionClause(node);
        }

        public override Expression VisitEventDeclaration(EventDeclarationSyntax node)
        {
            return base.VisitEventDeclaration(node);
        }

        public override Expression VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            return base.VisitAssignmentExpression(node);
        }

        public override Expression VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            var test = node.Condition.Accept(this);
            var @true = node.WhenTrue.Accept(this);
            var @false = node.WhenFalse.Accept(this);

            return Expression.Condition(test, @true, @false);
        }

        public override Expression VisitThisExpression(ThisExpressionSyntax node)
        {
            return base.VisitThisExpression(node);
        }

        public override Expression VisitBaseExpression(BaseExpressionSyntax node)
        {
            return base.VisitBaseExpression(node);
        }

        public override Expression VisitAttribute(AttributeSyntax node)
        {
            return base.VisitAttribute(node);
        }

        public override Expression VisitAttributeArgument(AttributeArgumentSyntax node)
        {
            return base.VisitAttributeArgument(node);
        }

        public override Expression VisitNameEquals(NameEqualsSyntax node)
        {
            return base.VisitNameEquals(node);
        }

        public override Expression VisitTypeParameterList(TypeParameterListSyntax node)
        {
            return base.VisitTypeParameterList(node);
        }

        public override Expression VisitTypeParameter(TypeParameterSyntax node)
        {
            return base.VisitTypeParameter(node);
        }

        public override Expression VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            return base.VisitClassDeclaration(node);
        }

        public override Expression VisitStructDeclaration(StructDeclarationSyntax node)
        {
            return base.VisitStructDeclaration(node);
        }

        public override Expression VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            return base.VisitInterfaceDeclaration(node);
        }

        public override Expression VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            return base.VisitEnumDeclaration(node);
        }

        public override Expression VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            return base.VisitDelegateDeclaration(node);
        }

        public override Expression VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            return base.VisitEnumMemberDeclaration(node);
        }

        public override Expression VisitBaseList(BaseListSyntax node)
        {
            return base.VisitBaseList(node);
        }

        public override Expression VisitSimpleBaseType(SimpleBaseTypeSyntax node)
        {
            return base.VisitSimpleBaseType(node);
        }

        public override Expression VisitTypeParameterConstraintClause(TypeParameterConstraintClauseSyntax node)
        {
            return base.VisitTypeParameterConstraintClause(node);
        }

        public override Expression VisitConstructorConstraint(ConstructorConstraintSyntax node)
        {
            return base.VisitConstructorConstraint(node);
        }

        public override Expression VisitClassOrStructConstraint(ClassOrStructConstraintSyntax node)
        {
            return base.VisitClassOrStructConstraint(node);
        }

        public override Expression VisitTypeConstraint(TypeConstraintSyntax node)
        {
            return base.VisitTypeConstraint(node);
        }

        public override Expression VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            return base.VisitFieldDeclaration(node);
        }

        public override Expression VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
        {
            return base.VisitEventFieldDeclaration(node);
        }

        public override Expression VisitExplicitInterfaceSpecifier(ExplicitInterfaceSpecifierSyntax node)
        {
            return base.VisitExplicitInterfaceSpecifier(node);
        }

        public override Expression VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            return base.VisitMethodDeclaration(node);
        }

        public override Expression VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            return base.VisitOperatorDeclaration(node);
        }

        public override Expression VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            return base.VisitConversionOperatorDeclaration(node);
        }

        public override Expression VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            return base.VisitConstructorDeclaration(node);
        }

        public override Expression VisitConstructorInitializer(ConstructorInitializerSyntax node)
        {
            return base.VisitConstructorInitializer(node);
        }

        public override Expression VisitDestructorDeclaration(DestructorDeclarationSyntax node)
        {
            return base.VisitDestructorDeclaration(node);
        }

        public override Expression VisitAttributeArgumentList(AttributeArgumentListSyntax node)
        {
            return base.VisitAttributeArgumentList(node);
        }

        public override Expression VisitAttributeList(AttributeListSyntax node)
        {
            return base.VisitAttributeList(node);
        }

        public override Expression VisitAttributeTargetSpecifier(AttributeTargetSpecifierSyntax node)
        {
            return base.VisitAttributeTargetSpecifier(node);
        }

        public override Expression VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
        {
            var operand = node.Operand.Accept(this);
            switch (node.OperatorToken.Text)
            {
                case "-":
                    return Expression.Negate(operand);
            }

            return base.VisitPrefixUnaryExpression(node);
        }

        public override Expression VisitAwaitExpression(AwaitExpressionSyntax node)
        {
            return base.VisitAwaitExpression(node);
        }

        public override Expression VisitBadDirectiveTrivia(BadDirectiveTriviaSyntax node)
        {
            return base.VisitBadDirectiveTrivia(node);
        }

        public override Expression VisitDefineDirectiveTrivia(DefineDirectiveTriviaSyntax node)
        {
            return base.VisitDefineDirectiveTrivia(node);
        }

        public override Expression VisitUndefDirectiveTrivia(UndefDirectiveTriviaSyntax node)
        {
            return base.VisitUndefDirectiveTrivia(node);
        }

        public override Expression VisitLineDirectiveTrivia(LineDirectiveTriviaSyntax node)
        {
            return base.VisitLineDirectiveTrivia(node);
        }

        public override Expression VisitPragmaWarningDirectiveTrivia(PragmaWarningDirectiveTriviaSyntax node)
        {
            return base.VisitPragmaWarningDirectiveTrivia(node);
        }

        public override Expression VisitPragmaChecksumDirectiveTrivia(PragmaChecksumDirectiveTriviaSyntax node)
        {
            return base.VisitPragmaChecksumDirectiveTrivia(node);
        }

        public override Expression VisitReferenceDirectiveTrivia(ReferenceDirectiveTriviaSyntax node)
        {
            return base.VisitReferenceDirectiveTrivia(node);
        }

        public override Expression VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var nameInfo = ModelExtensions.GetSymbolInfo(this.semanticModel, node.Name);
            var symbol = nameInfo.Symbol;

            var field = symbol as IFieldSymbol;
            if (field != null && field.IsStatic)
            {
                var type = this.ResolveType(field.ContainingType);
                var typeField = type.GetField(field.Name);
                if (typeField.IsStatic)
                {
                    return Expression.Field(null, typeField);
                }
            }

            var expression = node.Expression.Accept(this);

            if (nameInfo.Symbol == null)
            {
                if (nameInfo.CandidateSymbols.Length == 1 && nameInfo.CandidateReason == CandidateReason.NotAValue)
                {
                    symbol = nameInfo.CandidateSymbols[0];
                }
                else
                {
                    throw new Exception(string.Format("—войство {0} не найдено", node.Name.Identifier.Text));
                }
            }

            switch (symbol.Kind)
            {
                case SymbolKind.Property:
                    return Expression.Property(expression, node.Name.Identifier.Text);
            }

            return base.VisitMemberAccessExpression(node);
        }

        public override Expression VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
        {
            return base.VisitConditionalAccessExpression(node);
        }

        public override Expression VisitMemberBindingExpression(MemberBindingExpressionSyntax node)
        {
            return base.VisitMemberBindingExpression(node);
        }

        public override Expression VisitElementBindingExpression(ElementBindingExpressionSyntax node)
        {
            return base.VisitElementBindingExpression(node);
        }

        public override Expression VisitImplicitElementAccess(ImplicitElementAccessSyntax node)
        {
            return base.VisitImplicitElementAccess(node);
        }

        public override Expression VisitIdentifierName(IdentifierNameSyntax node)
        {
            var result = this.parameters.SingleOrDefault(x => x.Name == node.Identifier.ValueText);
            if (result != null)
            {
                return result;
            }

            if (this.parent != null)
            {
                return this.parent.VisitIdentifierName(node);
            }

            throw new Exception(string.Format("ѕеременна€ {0} не найдена", node.Identifier.ValueText));
        }

        public override Expression VisitQualifiedName(QualifiedNameSyntax node)
        {
            return base.VisitQualifiedName(node);
        }

        public override Expression VisitGenericName(GenericNameSyntax node)
        {
            return base.VisitGenericName(node);
        }

        public override Expression VisitAnonymousObjectCreationExpression(AnonymousObjectCreationExpressionSyntax node)
        {
            // получаем описани типа
            var typeInfo = ModelExtensions.GetTypeInfo(this.semanticModel, node);
            var type = this.ResolveType(typeInfo.Type, node);

            if (!node.Initializers.Any())
            {
                return Expression.New(type);
            }

            var arguments = new List<Expression>();

            foreach (var declarer in node.Initializers)
            {
                var expression = declarer.Expression;

                if (expression is AssignmentExpressionSyntax)
                {
                    var x = declarer.Expression.Accept(this);
                    arguments.Add(x);
                }
                else if (expression is MemberAccessExpressionSyntax)
                {
                    var member = expression as MemberAccessExpressionSyntax;
                    var right = this.VisitMemberAccessExpression(member);
                    arguments.Add(right);
                }
                else if (expression is BinaryExpressionSyntax)
                {
                    var binary = expression as BinaryExpressionSyntax;
                    var right = this.Visit(binary);
                    arguments.Add(right);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            var ctor = type.GetConstructors().First(x => x.GetParameters().Any());
            var members = type.GetProperties().Cast<MemberInfo>().ToArray();
            var result = Expression.New(ctor, arguments, members);
            return result;
        }

        public override Expression VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            var methodInfo = (IMethodSymbol)ModelExtensions.GetSymbolInfo(this.semanticModel, node).Symbol;
            var lambdaParams = new[] { Expression.Parameter(this.ResolveType(methodInfo.Parameters[0].Type, node), node.Parameter.Identifier.Text) };

            var context = new ExpressionSyntaxVisitor(this, this.semanticModel, lambdaParams, this.resolutionService);
            return Expression.Lambda(node.Body.Accept(context), lambdaParams);
        }

        public override Expression VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            return base.VisitParenthesizedLambdaExpression(node);
        }

        public override Expression VisitInitializerExpression(InitializerExpressionSyntax node)
        {
            return base.VisitInitializerExpression(node);
        }

        public override Expression VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            var left = node.Left.Accept(this);
            var right = node.Right.Accept(this);

            if (left.Type == typeof(string) && node.OperatorToken.Text == "+")
            {
                var objectType = typeof(object);
                var method = typeof(string).GetMethod("Concat", new[] { objectType, objectType });
                return Expression.Call(null, method, Expression.Convert(left, objectType), Expression.Convert(right, objectType));
            }

            if (!left.Type.IsAssignableFrom(right.Type))
            {
                right = Expression.Convert(right, left.Type);
            }

            switch (node.OperatorToken.Text)
            {
                case "+":
                    return Expression.Add(left, right);

                case "+=":
                    return Expression.AddAssign(left, right);

                case "-":
                    return Expression.Subtract(left, right);

                case "-=":
                    return Expression.SubtractAssign(left, right);

                case ">=":
                    return Expression.GreaterThanOrEqual(left, right);

                case ">":
                    return Expression.GreaterThan(left, right);

                case "<=":
                    return Expression.LessThanOrEqual(left, right);

                case "<":
                    return Expression.LessThan(left, right);

                case "==":
                    return Expression.Equal(left, right);

                case "!=":
                    return Expression.NotEqual(left, right);

                case "||":
                    return Expression.Or(left, right);

                case "&&":
                    return Expression.And(left, right);

                case "*":
                    return Expression.Multiply(left, right);

                case "*=":
                    return Expression.MultiplyAssign(left, right);

                case "??":
                    return Expression.Coalesce(left, right);
            }

            throw new NotImplementedException();
        }

        public override Expression VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            return Expression.Constant(node.Token.Value);
        }

        public override Expression VisitMakeRefExpression(MakeRefExpressionSyntax node)
        {
            return base.VisitMakeRefExpression(node);
        }

        public override Expression VisitRefTypeExpression(RefTypeExpressionSyntax node)
        {
            return base.VisitRefTypeExpression(node);
        }

        public override Expression VisitRefValueExpression(RefValueExpressionSyntax node)
        {
            return base.VisitRefValueExpression(node);
        }

        public override Expression VisitCheckedExpression(CheckedExpressionSyntax node)
        {
            return base.VisitCheckedExpression(node);
        }

        public override Expression VisitDefaultExpression(DefaultExpressionSyntax node)
        {
            return base.VisitDefaultExpression(node);
        }

        public override Expression VisitTypeOfExpression(TypeOfExpressionSyntax node)
        {
            return base.VisitTypeOfExpression(node);
        }

        public override Expression VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
        {
            return node.Expression.Accept(this);
        }

        private TypeInfo GetTypeInfo(SyntaxNode node)
        {
            var typeInfo = this.semanticModel.GetTypeInfo(node);
            if (typeInfo.Type == null)
            {
                typeInfo = this.semanticModel.GetSpeculativeTypeInfo(node.FullSpan.Start, node, SpeculativeBindingOption.BindAsTypeOrNamespace);
            }

            if (typeInfo.Type == null)
            {
                var expressionProperty = node.GetType().GetProperty("Expression");
                if (expressionProperty != null && typeof(ExpressionSyntax).IsAssignableFrom(expressionProperty.PropertyType))
                {
                    var expression = expressionProperty.GetValue(node) as ExpressionSyntax;
                    typeInfo = this.GetTypeInfo(expression);
                }
            }

            return typeInfo;
        }

        public override Expression VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var typeInfo = ModelExtensions.GetTypeInfo(this.semanticModel, node);
            var type = this.ResolveType(typeInfo.Type, node);

            NewExpression @new;
            if (node.ArgumentList == null)
            {
                @new = Expression.New(type);
            }
            else
            {
                var argumentTypes = node.ArgumentList.Arguments.Select(x => this.ResolveType(this.GetTypeInfo(x).Type, node)).ToArray();
                var ctor = type.GetConstructor(argumentTypes);

                @new = Expression.New(ctor, node.ArgumentList.Arguments.Select(x => x.Accept(this)).ToArray());
            }

            if (node.Initializer != null)
            {
                if (typeof(ICollection).IsAssignableFrom(type))
                {
                    // например new List<int>{ 1, 2, 3 }                    
                    var initializers = node.Initializer.Expressions.Select(this.Visit).ToArray();
                    var listInit = Expression.ListInit(@new, initializers);
                    return listInit;
                }

                var bindings = new List<MemberBinding>();
                foreach (var expression in node.Initializer.Expressions)
                {
                    if (expression is AssignmentExpressionSyntax)
                    {
                        bindings.Add(this.VisitMemberInitExpression(type, (AssignmentExpressionSyntax)expression));
                    }
                    else if (expression is MemberAccessExpressionSyntax)
                    {
                        var propertyName = (expression as MemberAccessExpressionSyntax).Name.Identifier.Text;
                        var left = Expression.Property(@new, propertyName);
                        var right = this.VisitMemberAccessExpression((expression as MemberAccessExpressionSyntax));

                        if (!left.Type.IsAssignableFrom(right.Type))
                        {
                            right = Expression.Convert(right, left.Type);
                        }

                        bindings.Add(Expression.Bind(left.Member, right));
                    }
                    else
                    {
                        var value = expression.Accept(this);
                        throw new NotImplementedException();
                    }
                }

                return Expression.MemberInit(@new, bindings);
            }

            return @new;
        }

        private MemberBinding VisitMemberInitExpression(Type ownerType, AssignmentExpressionSyntax expression)
        {
            if (expression.Left is IdentifierNameSyntax)
            {
                var target = (IdentifierNameSyntax)expression.Left;

                if (expression.Right is InitializerExpressionSyntax)
                {
                    var property = ownerType.GetProperty(target.Identifier.Text);

                    return Expression.ListBind(property, ((InitializerExpressionSyntax)expression.Right).Expressions.Select(x => this.VisitElementInit(property.PropertyType, x)));
                }
                return Expression.Bind(ownerType.GetProperty(target.Identifier.Text), expression.Right.Accept(this));
            }

            throw new NotImplementedException();
        }

        private ElementInit VisitElementInit(Type ownerType, ExpressionSyntax expression)
        {
            return Expression.ElementInit(ownerType.GetMethod("Add"), expression.Accept(this));
        }

        public override Expression VisitCastExpression(CastExpressionSyntax node)
        {
            var typeInfo = ModelExtensions.GetTypeInfo(this.semanticModel, node);
            return Expression.Convert(node.Expression.Accept(this), this.ResolveType(typeInfo.Type, node));
        }

        public override Expression VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
        {
            var operand = node.Operand.Accept(this);

            switch (node.Kind())
            {
                case SyntaxKind.PostDecrementExpression:
                    return Expression.PostDecrementAssign(operand);

                case SyntaxKind.PreDecrementExpression:
                    return Expression.PreDecrementAssign(operand);

                case SyntaxKind.PostIncrementExpression:
                    return Expression.PostIncrementAssign(operand);

                case SyntaxKind.PreIncrementExpression:
                    return Expression.PreIncrementAssign(operand);

                default:
                    throw new NotImplementedException();
            }
        }

        public override Expression Visit(SyntaxNode node)
        {
            return base.Visit(node);
        }

        public override Expression DefaultVisit(SyntaxNode node)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return base.ToString();
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}