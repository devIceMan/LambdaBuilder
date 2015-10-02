namespace LambdaBuilder
{
    using System;
    using System.Linq;

    using Microsoft.CodeAnalysis;

    /// <summary>
    /// Разрешитель типов, производящий поиск типа в загруженных в текущий домен сборках
    /// </summary>
    internal class AppDomainTypeResolver : TypeResolver
    {
        /// <summary>
        /// Разрешение типа на основе его описания в синтаксическом дереве
        /// </summary>
        /// <param name="service">Контекст разрешения типа</param>
        public override Type Resolve(TypeResolutionService service)
        {
            var typeName = BuildTypeFullName(service.Symbol);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            var assembly = assemblies.FirstOrDefault(x => x.GetName().Name == service.Symbol.ContainingAssembly.Name);
            if (assembly == null)
            {
                return null;
            }

            return assembly.GetType(typeName, true);
        }

        /// <summary>
        /// Определение возможноси разрешить тип
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public override bool CanResolve(TypeResolutionService service)
        {
            return service.Symbol.ContainingAssembly != null;
        }

        private static string BuildTypeFullName(ITypeSymbol typeSymbol)
        {
            if (typeSymbol.ContainingType == null)
            {
                return typeSymbol.ContainingNamespace + "." + typeSymbol.MetadataName;
            }

            return BuildTypeFullName(typeSymbol.ContainingType) + "+" + typeSymbol.MetadataName;
        }
    }
}