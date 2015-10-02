namespace LambdaBuilder
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.CodeAnalysis;
    using System.Diagnostics.Contracts;

    /// <summary>
    /// ������ ���������� ���� ��� �������� ����� � ���������
    /// </summary>
    public class TypeResolutionService
    {
        private readonly Stack<ITypeSymbol> resolutionStack = new Stack<ITypeSymbol>();

        private readonly Stack<TypeResolver> resolvers = new Stack<TypeResolver>();

        /// <summary>
        /// ������� �������������� ���
        /// </summary>
        public ITypeSymbol Symbol
        {
            get
            {
                return this.resolutionStack.FirstOrDefault();
            }
        }

        /// <summary>
        /// �������� ����������� �����
        /// </summary>
        /// <param name="resolver">��������� ����������� �����</param>        
        public TypeResolutionService Add(TypeResolver resolver)
        {
            Contract.Requires(resolver != null);

            this.resolvers.Push(resolver);
            return this;
        }

        /// <summary>
        /// �������� ����������� �����
        /// </summary>
        /// <typeparam name="T">��� �����������</typeparam>        
        public TypeResolutionService Add<T>()
            where T : TypeResolver, new()
        {
            return this.Add(new T());
        }

        /// <summary>
        /// ��������� ��� �� ��� ��������
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public Type Resolve(ITypeSymbol symbol)
        {
            if (this.resolutionStack.Contains(symbol))
            {
                // ���� ����� ������ � ����, ������� ��� ��������� � ��������� ����������
                // �� ������ ����� �� ����������� � ����������� ������
                throw new InvalidOperationException("�������� ������ � ����������� ���� ��� ���������� ����");
            }

            this.resolutionStack.Push(symbol);

            var type = this.resolvers.Where(x => x.CanResolve(this)).Select(x => x.Resolve(this)).FirstOrDefault(x => x != null);

            if (type == null)
            {
                throw new InvalidOperationException("�� ������� ��������� ��� " + symbol.Name);
            }

            this.resolutionStack.Pop();

            return type;
        }
    }
}