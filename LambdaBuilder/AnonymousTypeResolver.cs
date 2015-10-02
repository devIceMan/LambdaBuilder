namespace LambdaBuilder
{
    using System;
    using System.Collections.Generic;

    using Microsoft.CodeAnalysis;

    /// <summary>
    /// –азрешитель анонимных типов.
    /// ¬ случае отсутстви€ анонимного типа, он будет создан
    /// </summary>
    public class AnonymousTypeResolver : TypeResolver
    {
        /// <summary>
        /// –азрешение типа на основе его описани€ в синтаксическом дереве
        /// </summary>
        /// <param name="service"> онтекст разрешени€ типа</param>
        public override Type Resolve(TypeResolutionService service)
        {
            var properties = new Dictionary<string, Type>();
            foreach (var member in service.Symbol.GetMembers())
            {
                var property = member as IPropertySymbol;
                if (property == null)
                {
                    continue;
                }

                var propertyType = service.Resolve(property.Type);
                var propertyName = property.Name;

                properties[propertyName] = propertyType;
            }

            return AnonymousTypeBuilder.Build(properties);
        }

        /// <summary>
        /// ќпределение возможноси разрешить тип
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public override bool CanResolve(TypeResolutionService service)
        {
            return service.Symbol.IsAnonymousType;
        }
    }
}