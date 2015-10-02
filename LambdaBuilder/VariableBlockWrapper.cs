namespace LambdaBuilder
{
    using System.Collections.Generic;
    using System.Linq.Expressions;

    /// <summary>
    /// ������� ��� BlockExpression, ������������ ��� ���������� ����������.
    /// ����� ��� ����������� ������������� �������� ���������� � ������������ ����
    /// </summary>
    internal class VariableBlockWrapper : Expression
    {
        private readonly BlockExpression inline;

        public VariableBlockWrapper(BlockExpression inline)
        {
            this.inline = inline;
        }

        public IEnumerable<Expression> Expressions
        {
            get
            {
                return this.inline.Expressions;
            }
        }

        public IEnumerable<ParameterExpression> Variables
        {
            get
            {
                return this.inline.Variables;
            }
        }
    }
}