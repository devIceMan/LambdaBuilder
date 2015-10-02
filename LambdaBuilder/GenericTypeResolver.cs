namespace LambdaBuilder
{
    using System;
    using Microsoft.CodeAnalysis;
    using System.Linq;

    /// <summary>
    /// Разрешитель обобщенного типа
    /// </summary>
    public class GenericTypeResolver : TypeResolver
    {
        public override Type Resolve(TypeResolutionService service)
        {
            var named = service.Symbol as INamedTypeSymbol;

            var unboundType = service.Resolve(named.ConstructUnboundGenericType());
            var arguments = named.TypeArguments.Select(service.Resolve).ToArray();

            if (arguments.Length != named.Arity)
            {
                throw new Exception("Не удалось разрешить все типы аргументов обобщенного типа");
            }

            return unboundType.MakeGenericType(arguments);
        }

        /// <summary>
        /// Определение возможноси разрешить тип
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public override bool CanResolve(TypeResolutionService service)
        {
            var named = service.Symbol as INamedTypeSymbol;
            return named != null && named.Arity > 0 && named.IsGenericType && !named.IsUnboundGenericType;
        }
    }
}