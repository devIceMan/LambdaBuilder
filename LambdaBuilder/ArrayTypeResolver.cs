namespace LambdaBuilder
{
    using System;

    using Microsoft.CodeAnalysis;

    /// <summary>
    /// Разрешитель типов-массивов
    /// </summary>
    public class ArrayTypeResolver : TypeResolver
    {
        /// <summary>
        /// Разрешение типа на основе его описания в синтаксическом дереве
        /// </summary>
        /// <param name="service">Контекст разрешения типа</param>
        public override Type Resolve(TypeResolutionService service)
        {
            var arrayType = service.Symbol as IArrayTypeSymbol;
            var itemType = service.Resolve(arrayType.ElementType);
            return itemType.MakeArrayType();
        }

        /// <summary>
        /// Определение возможноси разрешить тип
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public override bool CanResolve(TypeResolutionService service)
        {
            return service.Symbol is IArrayTypeSymbol;
        }
    }
}