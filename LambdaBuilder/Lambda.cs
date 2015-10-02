namespace LambdaBuilder
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    using LinqExpression = System.Linq.Expressions.Expression;

    /// <summary>
    /// ������ ����� � ��������� � ������� Roslyn
    /// </summary>
    public class Lambda
    {
        private static readonly Regex LambdaDeclaration = new Regex(@"^\s*(\(*)((,?\w*\s*)*)(\)*)\s*=>(\s*{?).*(\s*}?)$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private static readonly IDictionary<string, LambdaExpression> ExpressionsCache = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal);

        private readonly IList<ParameterExpression> parameters = new List<ParameterExpression>();

        private readonly HashSet<Assembly> references = new HashSet<Assembly>();

        private readonly HashSet<string> usings = new HashSet<string>(StringComparer.Ordinal);
        private readonly IDictionary<string, string> aliases = new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly TypeResolutionService resolutionService = new TypeResolutionService();

        /// <summary>
        /// �������� ���������� ������
        /// </summary>
        /// <param name="expression">������� ��� �������</param>
        private Lambda(string expression)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(expression));

            this.Expression = expression;

            // ������������ ������������ ���� ��-���������
            this.UsingNamespace("System")
                .UsingNamespace("System.Linq")
                .UsingNamespace("System.Linq.Expressions");

            // ������������ ����������� ��-���������
            this.UsingTypeResolver<AppDomainTypeResolver>()
                .UsingTypeResolver<AnonymousTypeResolver>()
                .UsingTypeResolver<ArrayTypeResolver>()
                .UsingTypeResolver<GenericTypeResolver>();
        }

        /// <summary>
        /// �������������� ���������
        /// </summary>
        public string Expression { get; private set; }

        /// <summary>
        /// ����������� �������� ��������� ���������
        /// </summary>
        /// <param name="type">��� ���������</param>
        /// <param name="name">������������ ���������</param>                
        public Lambda WithArgument(Type type, string name = null)
        {
            Contract.Requires(type != null);

            this.parameters.Add(LinqExpression.Parameter(type, name));

            return this;
        }

        /// <summary>
        /// ����������� �������� ��������� ���������
        /// </summary>
        /// <typeparam name="T">��� ���������</typeparam>
        /// <param name="name">������������ ���������</param>        
        public Lambda WithArgument<T>(string name = null)
        {
            return this.WithArgument(typeof(T), name);
        }

        /// <summary>
        /// ����������� ������ ����� ������� ����������
        /// </summary>
        /// <param name="types">����� ����� ������� ����������</param>        
        public Lambda WithArguments(IEnumerable<Type> types)
        {
            Contract.Requires(types != null);

            foreach (var type in types)
            {
                this.WithArgument(type);
            }

            return this;
        }

        /// <summary>
        /// ����������� ������ ����� ������� ����������
        /// </summary>
        /// <param name="types">����� ����� ������� ����������</param>
        public Lambda WithArguments(params Type[] types)
        {
            return this.WithArguments(types.ToList());
        }

        /// <summary>
        /// ����������� ������ �� ������
        /// </summary>
        /// <param name="assembly">������</param>        
        public Lambda WithAssembly(Assembly assembly)
        {
            Contract.Requires(assembly != null);

            this.references.Add(assembly);

            return this;
        }

        /// <summary>
        /// ����������� ������ �� ������, � ������� ��������� ��� <see cref="type"/>
        /// </summary>
        /// <param name="type">���, ������������ � ������</param>        
        public Lambda WithAssemblyOf(Type type)
        {
            Contract.Requires(type != null);

            return this.WithAssembly(type.Assembly);
        }

        /// <summary>
        /// ����������� ������ �� ������, � ������� ��������� ��� <see cref="T"/>
        /// </summary>
        /// <typeparam name="T">���, ������������ � ������</typeparam>        
        public Lambda WithAssemblyOf<T>()
        {
            return this.WithAssemblyOf(typeof(T));
        }

        /// <summary>
        /// �������� ������������ ������������ ����.
        /// ��� ��������� �� ��������� ������ ��� ���� � ���������
        /// </summary>
        /// <param name="namespace">����������� ����</param>        
        public Lambda UsingNamespace(params string[] @namespace)
        {
            foreach (var ns in @namespace)
            {
                this.usings.Add(ns);
            }

            return this;
        }

        /// <summary>
        /// �������� ������������ ���� ����
        /// </summary>
        /// <param name="types">����, ����������� ���� ������� ���������� ��������</param>                
        public Lambda UsingNamespaceOf(params Type[] types)
        {
            Contract.Requires(types != null);
            Contract.Requires(Contract.ForAll(types, x => x != null));

            return this.UsingNamespace(types.Select(x => x.Namespace).ToArray());
        }

        /// <summary>
        /// �������� ������������ ���� ����
        /// </summary>
        /// <typeparam name="T">���, ����������� ���� �������� ���������� ��������</typeparam>        
        public Lambda UsingNamespaceOf<T>()
        {
            return this.UsingNamespace(typeof(T).Namespace);
        }

        /// <summary>
        /// �������� ��������� ����.
        /// ��������� ��������� ������� ����� ����� � ����������.
        /// ����� ����������� � ���� <c>using Alias = Ns1.Ns2.Ns3.Type</c>
        /// </summary>
        /// <param name="alias">��������� ����</param>
        /// <param name="typeFullName">������ ��� ����</param>
        /// <returns></returns>
        public Lambda UsingTypeAlias(string alias, string typeFullName)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(alias));
            Contract.Requires(!string.IsNullOrWhiteSpace(typeFullName));

            this.aliases.Add(alias, typeFullName);

            return this;
        }

        /// <summary>
        /// �������� ��������� ����.
        /// ��������� ��������� ������� ����� ����� � ����������.
        /// ����� ����������� � ���� <c>using Alias = Ns1.Ns2.Ns3.Type</c>
        /// </summary>
        /// <param name="alias">���������</param>
        /// <param name="type">���</param>        
        public Lambda UsingTypeAlias(string alias, Type type)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(alias));
            Contract.Requires(type != null);

            return this.UsingTypeAlias(alias, type.TypeName());
        }

        /// <summary>
        /// �������� ��������� ����
        /// ��������� ��������� ������� ����� ����� � ����������.
        /// ����� ����������� � ���� <c>using Alias = Ns1.Ns2.Ns3.Type</c>
        /// </summary>
        /// <typeparam name="T">���</typeparam>
        /// <param name="alias">���������</param>        
        public Lambda UsingTypeAlias<T>(string alias)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(alias));
            return this.UsingTypeAlias(alias, typeof(T));
        }

        /// <summary>
        /// ������������ ���������� ����������� �����
        /// </summary>
        /// <param name="resolver">��������� ����������� �����</param>
        /// <returns></returns>
        public Lambda UsingTypeResolver(TypeResolver resolver)
        {
            Contract.Requires(resolver != null);

            this.resolutionService.Add(resolver);

            return this;
        }

        /// <summary>
        /// ������������ ����������� ����� ���� <see cref="T"/>
        /// </summary>
        /// <typeparam name="T">��� �����������</typeparam>
        /// <returns></returns>
        public Lambda UsingTypeResolver<T>()
            where T : TypeResolver, new()
        {
            return this.UsingTypeResolver(new T());
        }

        /// <summary>
        /// ������������ ��������� <see cref="LambdaExpression"/>
        /// </summary>
        /// <param name="resultType">��� ����������</param>        
        public LambdaExpression ToLambda(Type resultType)
        {
            var expr = this.ParseExpression(resultType);
            var lambda = expr as LambdaExpression;

            if (lambda != null && !resultType.IsAssignableFrom(lambda.ReturnType))
            {
                expr = LinqExpression.Convert((expr as LambdaExpression).Body, resultType);
            }

            switch (expr.NodeType)
            {
                case ExpressionType.MemberAccess:
                case ExpressionType.Call:
                case ExpressionType.Convert:
                    if (!resultType.IsAssignableFrom(expr.Type))
                    {
                        expr = LinqExpression.Convert(expr, resultType);
                    }

                    return LinqExpression.Lambda(expr, this.parameters);

                case ExpressionType.Lambda:
                    return lambda;

                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// ������������ ��������� <see cref="LambdaExpression"/>
        /// </summary>
        /// <typeparam name="TResult">��� ����������</typeparam>        
        public LambdaExpression ToLambda<TResult>()
        {
            return this.ToLambda(typeof(TResult));
        }

        /// <summary>
        /// ��������� ��������� � �������������� ���
        /// </summary>
        /// <typeparam name="TResult">��� ����������</typeparam>        
        public ParamsFunction<TResult> ToParamsFunction<TResult>()
        {
            var fn = this.ToLambda<TResult>().Compile();
            return x => (TResult)fn.DynamicInvoke(x);
        }

        /// <summary>
        /// ��������� ��������� � �������������� ���
        /// </summary>
        /// <typeparam name="TInput">��� ���������</typeparam>
        /// <typeparam name="TResult">��� ����������</typeparam>                
        public Func<TInput, TResult> ToFunction<TInput, TResult>()
        {
            var fn = this.ToLambda<TResult>().Compile();
            return (Func<TInput, TResult>)fn;
        }

        /// <summary>
        /// ������������ � ��������� ���������
        /// </summary>
        /// <param name="resultType">��� ����������</param>
        /// <param name="params">��������� �����������</param>        
        public object Call(Type resultType, params object[] @params)
        {
            var expression = this.ToLambda(resultType);
            var function = expression.Compile();
            return function.DynamicInvoke(@params);
        }

        /// <summary>
        /// ������������ � ��������� ���������
        /// </summary>
        /// <typeparam name="TResult">��� ����������</typeparam>
        /// <param name="params">��������� �����������</param>
        /// <returns></returns>
        public TResult Call<TResult>(params object[] @params)
        {
            var result = this.Call(typeof(TResult), @params);
            return (TResult)result;
        }

        /// <summary>
        /// ������� ����� ��������� <see cref="Lambda"/> ��� ������� ��������� <see cref="expression"/>
        /// </summary>
        /// <param name="expression">����������� ���������</param>        
        public static Lambda FromString(string expression)
        {
            return new Lambda(expression);
        }

        private string BuildCodeForParsing(Type resultType)
        {
            var sb = new StringBuilder().Append("namespace G {");

            foreach (var @using in this.usings)
            {
                sb.AppendFormat("using {0};", @using);
            }

            foreach (var alias in this.aliases)
            {
                sb.AppendFormat("using {0} = {1};", alias.Key, alias.Value);
            }

            sb.Append("class C {");

            if (LambdaDeclaration.IsMatch(this.Expression))
            {
                var paramsString = this.parameters.Count > 0
                                       ? string.Join(",", this.parameters.Select(x => x.Type.TypeName())) + ","
                                       : "";

                sb.Append("Expression<Func<")
                    .Append(paramsString)
                    .Append(resultType.TypeName())
                    .Append(">>")
                    .Append(" M(){")
                    .AppendFormat("return {0};", this.Expression)
                    .Append("}");
            }
            else
            {
                var paramsString = this.parameters.Count > 0
                                       ? string.Join(",", this.parameters.Select(x => $"{x.Type.TypeName()} {x.Name}"))
                                       : "";

                sb.Append(resultType.TypeName())
                    .AppendFormat(" M({0}){{", paramsString)
                    .AppendFormat("return {0}", this.Expression)
                    .Append("}");
            }

            sb.Append("}}");

            return sb.ToString();
        }

        private string GetExpressionKey(ParameterExpression[] parameterExpressions, Type resultType)
        {
            var sb = new StringBuilder();
            sb.Append(this.Expression.Replace(" ", "").Replace("\t", "").Replace(Environment.NewLine, ""))
                .Append(";")
                .Append(parameterExpressions.Aggregate(string.Empty, (c, n) => c + ";" + n.Name + ":" + n.Type.FullName)).Append(";")
                .Append(resultType.FullName);

            return sb.ToString();
        }

        private Expression ParseExpression(Type resultType)
        {
            var text = this.BuildCodeForParsing(resultType);

            var tree = SyntaxFactory.ParseSyntaxTree(text);

            var assemblies = new List<Assembly>(this.references);

            var types = new[] { resultType, typeof(Enumerable), typeof(ICollection<>) }.Concat(this.parameters.Select(x => x.Type));
            CollectAssembliesFromTypes(types, assemblies);

            var returnStatements = tree.GetRoot()
                .DescendantNodes()
                .OfType<ReturnStatementSyntax>();

            var syntaxNode = returnStatements.First();

            SyntaxNode nodeExpression = syntaxNode.Expression;
            var lambda = nodeExpression as LambdaExpressionSyntax;

            var semanticModel = CSharpCompilation.Create("Instance")
                .AddSyntaxTrees(tree)
                .AddReferences(CollectReferences(assemblies))
                .GetSemanticModel(tree);

            var parameterExpressions = this.parameters.ToArray();
            if (lambda != null)
            {
                var @params = GetLambdaParameters(lambda);

                if (this.parameters.Count != @params.Length)
                {
                    throw new Exception(string.Format("�������������� ����� ���������� - ��������� {0}, �������� {1}", this.parameters.Count, @params.Length));
                }

                // ���� � ���������� ������� ������������� ��������, ���������� ������������ ����� ����� ����������
                parameterExpressions = this.parameters
                    .Select((p, idx) => UseOrCreateParameter(p, @params[idx]))
                    .ToArray();

                nodeExpression = lambda.Body;
            }

            var key = this.GetExpressionKey(parameterExpressions, resultType);
            LambdaExpression expression;
            if (!ExpressionsCache.TryGetValue(key, out expression))
            {
                var parsedExpression = this.Visit(nodeExpression, semanticModel, parameterExpressions);
                expression = LinqExpression.Lambda(parsedExpression, parameterExpressions);
                ExpressionsCache[key] = expression;
            }

            return expression;
        }

        private Expression Visit(SyntaxNode expression, SemanticModel model, ParameterExpression[] expressionParameters)
        {
            var visitor = new ExpressionSyntaxVisitor(null, model, expressionParameters, this.resolutionService);
            return visitor.Visit(expression);
        }

        private static void CollectAssembliesFromTypes(IEnumerable<Type> types, List<Assembly> assemblies)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dictionary = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
            foreach (var type in types)
            {
                CollectAssembliesFromTypes(type, dictionary, set);
            }

            assemblies.AddRange(dictionary.Values);
        }

        private static void CollectAssembliesFromTypes(Type type, IDictionary<string, Assembly> assemblies, HashSet<string> processed)
        {
            if (processed.Contains(type.FullName))
            {
                return;
            }

            var assemblyName = type.GetTypeInfo().Assembly.GetName();
            if (!assemblies.ContainsKey(assemblyName.Name))
            {
                assemblies.Add(assemblyName.Name, type.GetTypeInfo().Assembly);
            }

            processed.Add(type.FullName);

            foreach (var member in type.GetMembers())
            {
                var property = member as PropertyInfo;
                if (property != null)
                {
                    CollectAssembliesFromTypes(property.PropertyType, assemblies, processed);
                }

                var field = member as FieldInfo;
                if (field != null)
                {
                    CollectAssembliesFromTypes(field.FieldType, assemblies, processed);
                }

                var method = member as MethodBase;
                if (method != null)
                {
                    foreach (var parameter in method.GetParameters())
                    {
                        CollectAssembliesFromTypes(parameter.ParameterType, assemblies, processed);
                    }

                    if (method.IsGenericMethod && !method.IsGenericMethodDefinition)
                    {
                        foreach (var argument in method.GetGenericArguments())
                        {
                            CollectAssembliesFromTypes(argument, assemblies, processed);
                        }
                    }

                    var methodInfo = method as MethodInfo;
                    if (methodInfo != null)
                    {
                        CollectAssembliesFromTypes(methodInfo.ReturnType, assemblies, processed);
                    }
                }
            }
        }

        private static ParameterExpression UseOrCreateParameter(ParameterExpression p, ParameterSyntax l)
        {
            return string.IsNullOrWhiteSpace(p.Name) ? LinqExpression.Parameter(p.Type, l.Identifier.Text) : p;
        }

        private static IEnumerable<MetadataReference> CollectReferences(List<Assembly> references)
        {
            var processed = new HashSet<string>();
            foreach (var assembly in references)
            {                
                if (!processed.Contains(assembly.Location))
                {
                    processed.Add(assembly.Location);
                }
            }

            return processed.Select(x => MetadataReference.CreateFromFile(x));
        }

        private static ParameterSyntax[] GetLambdaParameters(LambdaExpressionSyntax lambda)
        {
            var simple = lambda as SimpleLambdaExpressionSyntax;
            if (simple != null)
            {
                return new[] { simple.Parameter };
            }

            var parenthesized = lambda as ParenthesizedLambdaExpressionSyntax;
            if (parenthesized != null)
            {
                return parenthesized.ParameterList.Parameters.ToArray();
            }

            throw new NotImplementedException();
        }
    }
}