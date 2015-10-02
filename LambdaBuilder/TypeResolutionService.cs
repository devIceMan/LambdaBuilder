namespace LambdaBuilder
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.CodeAnalysis;
    using System.Diagnostics.Contracts;

    /// <summary>
    /// —ервис разрешени€ типа при парсинге строк в выражени€
    /// </summary>
    public class TypeResolutionService
    {
        private readonly Stack<ITypeSymbol> resolutionStack = new Stack<ITypeSymbol>();

        private readonly Stack<TypeResolver> resolvers = new Stack<TypeResolver>();

        /// <summary>
        /// “екущий обрабатываемый тип
        /// </summary>
        public ITypeSymbol Symbol
        {
            get
            {
                return this.resolutionStack.FirstOrDefault();
            }
        }

        /// <summary>
        /// ƒобавить разрешитель типов
        /// </summary>
        /// <param name="resolver">Ёкземпл€р разрешител€ типов</param>        
        public TypeResolutionService Add(TypeResolver resolver)
        {
            Contract.Requires(resolver != null);

            this.resolvers.Push(resolver);
            return this;
        }

        /// <summary>
        /// ƒобавить разрешитель типов
        /// </summary>
        /// <typeparam name="T">“ип разрешител€</typeparam>        
        public TypeResolutionService Add<T>()
            where T : TypeResolver, new()
        {
            return this.Add(new T());
        }

        /// <summary>
        /// –азрешить тип по его описанию
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public Type Resolve(ITypeSymbol symbol)
        {
            if (this.resolutionStack.Contains(symbol))
            {
                // если оп€ть пришли к типу, который уже находитс€ в контексте разрешени€
                // то скорее всего мы столкнулись с бесконечным циклом
                throw new InvalidOperationException("¬озможно попали в бесконечный цикл при разрешении типа");
            }

            this.resolutionStack.Push(symbol);

            var type = this.resolvers.Where(x => x.CanResolve(this)).Select(x => x.Resolve(this)).FirstOrDefault(x => x != null);

            if (type == null)
            {
                throw new InvalidOperationException("Ќе удалось разрешить тип " + symbol.Name);
            }

            this.resolutionStack.Pop();

            return type;
        }
    }
}