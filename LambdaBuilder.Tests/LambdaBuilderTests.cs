namespace LambdaBuilder.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;

    using FluentAssertions;

    using Ploeh.AutoFixture;

    using Xunit;

    public class LambdaBuilderTests
    {        
        [Fact(DisplayName = "Использование пространств имен")]
        public void UsingsTests()
        {
            const string Expression = "p.EntitySet<TestDto>();";
            Expression expr = null;
            Action action = () => expr = Lambda.FromString(Expression)
                                             .WithArgument<NestedData>("p")
                                             .UsingNamespaceOf<LambdaBuilderTests>()
                                             .ToLambda<object>();

            action.ShouldNotThrow();
            expr.Should().NotBeNull();
        }

        [Fact(DisplayName = "Использование псевдонимов типов")]
        public void TypeAliasTests()
        {
            const string Expression = "()=>{ var x = new ListOfInt(); return x; }";
            LambdaExpression expr = null;
            Action action = () => expr = Lambda.FromString(Expression)
                                             .UsingNamespaceOf(typeof(List<>))
                                             .UsingTypeAlias<List<int>>("ListOfInt")
                                             .ToLambda<object>();

            action.ShouldNotThrow();
            expr.Should().NotBeNull();

            var fn = expr.Compile();
            var l = fn.DynamicInvoke();

            l.Should().BeAssignableTo<List<int>>();
        }

        [Theory(DisplayName = "Парсинг lambda-выражений")]
        [InlineData("(x,y)=>x + y", "10", 1, "101")]
        [InlineData("(x,y)=>x", "20", 2, "20")]
        [InlineData("(x,y)=>int.Parse(x)", "30", 3, 30)]
        [InlineData("(x,y)=>int.Parse(x) + y", "30", 3, 33)]
        [InlineData("(x,y)=>int.Parse(x) + y + LambdaBuilder.Tests.StaticData.LongValue()", "30", 3, 43)]
        public void ParseLambda(string expr, object arg1, object arg2, object result)
        {
            var builder = Lambda
                .FromString(expr)
                .WithArgument<string>("x")
                .WithArgument<long>("y")
                .WithAssemblyOf(typeof(StaticData));

            var r = builder.Call(result.GetType(), arg1, arg2);
            r.Should().NotBeNull();
            r.Should().BeOfType(result.GetType());

            r.Should().Be(result);
        }

        [Theory(DisplayName = "Парсинг linq запросов")]
        [InlineData("p.EntitySet<LambdaBuilder.Tests.TestDto>().Select(x=>new LambdaBuilder.Tests.TestDto { x.Id, TotalSum = x.TotalSum * 2 })")]
        [InlineData("(p) => p.EntitySet<LambdaBuilder.Tests.TestDto>().Select(x=>new LambdaBuilder.Tests.TestDto { x.Id, TotalSum = x.TotalSum * 2 })")]
        [InlineData("p => p.EntitySet<LambdaBuilder.Tests.TestDto>().Select(x=>new LambdaBuilder.Tests.TestDto { x.Id, TotalSum = x.TotalSum * 2 })")]
        public void ParseQuery(string query)
        {
            var builder = Lambda
                .FromString(query)
                .WithAssemblyOf<LambdaBuilderTests>()
                .WithArgument<NestedData>("p");

            var r = builder.Call<IEnumerable>(new NestedData());
            r.Should().NotBeNull();
        }

        [Fact(DisplayName = "Парсинг linq-запроса с анонимным типом")]
        public void AnonymousTypeTest()
        {
            const string Expression = "p => p.EntitySet<LambdaBuilder.Tests.TestDto>().Select(x=>new { x.Id, TotalSumX2 = x.TotalSum * 2, TotalSum = x.TotalSum + 1, x.Id + x.TotalSum })";

            var builder = Lambda
                .FromString(Expression)
                .WithAssemblyOf<LambdaBuilderTests>()
                .WithArgument<NestedData>("p");

            var r = builder.Call<IEnumerable>(new NestedData());
            r.Should().NotBeNull();
        }

        [Fact(DisplayName = "Парсинг linq-запроса с массивом анонимных типов и сравнение экземпляров")]
        public void AnonymousTypeTestWithEquality()
        {
            const string Expression = "p => p.EntitySet<LambdaBuilder.Tests.TestDto>().Select(x=> new[]{ new { x.Id, TotalSum = x.TotalSum }, new { x.Id, TotalSum = x.TotalSum } }).Take(2)";

            var builder = Lambda.FromString(Expression).WithAssemblyOf<TestDto>().WithArgument<NestedData>("p");

            var r = builder.Call<IEnumerable>(new NestedData());
            r.Should().NotBeNull();
            r.Should().HaveCount(2);
                
            var pair = r.Cast<object[]>().First();
            pair[0].ShouldBeEquivalentTo(pair[1], x => x.IncludingAllDeclaredProperties().IncludingAllRuntimeProperties().IncludingFields());
        }

        [Theory(DisplayName = "Парсинг выражения с условным результом")]
        [InlineData("x => x * 2 > 3 ? x : 1", 1, 1)]
        [InlineData("x => x * 2 > 3 ? x : 1", 2, 2)]
        [InlineData("x => x + 2 - 1 <= 3 ? true : false", 1, true)]
        [InlineData("x => x + 2 - 1 <= 3 ? true : false", 5, false)]
        [InlineData("x => x + 2 - 1 >= 3", 2, true)]
        [InlineData("x => x + 2 - 1 > 3", 1, false)]
        [InlineData("x => x + 2 - 1 < 3", 1, true)]
        public void SimpleOperations(string expression, int input, object expect)
        {
            var result = Lambda.FromString(expression).WithArgument<int>("x").Call<object>(input);

            result.Should().BeOfType(expect.GetType());
            result.Should().Be(expect);
        }

        [Theory(DisplayName = "Передача параметра в локальную переменную и условный вывод")]
        [InlineData(1, 2, false)]
        [InlineData(2, 1, true)]
        public void VariableTest(int a, int b, bool expect)
        {
            var expr = "(a,b)=>{ var x = a; int y = b; return x > y ? true : false; };";
            var builder = Lambda.FromString(expr).WithArgument<int>("a").WithArgument<int>("b");

            var result = false;
            Action action = () => result = builder.Call<bool>(a, b);
            action.ShouldNotThrow();

            result.Should().Be(expect);
        }

        [Fact(DisplayName = "Использование неименованных параметров")]
        public void UnnamedParametersTest()
        {
            var builder = Lambda.FromString("x => string.Format(\"{0}\", x)").WithArgument<object>();

            var expr = builder.Call<string>(12);

            expr.Should().BeEquivalentTo("12");
        }

        [Fact(DisplayName = "Построение функций")]
        public void FunctionTests()
        {
            var builder = Lambda.FromString("(x,y) => string.Format(\"{0}-{1}\", x,y)").WithArgument<object>().WithArgument<int>();

            var expr = builder.ToParamsFunction<string>();

            var r = expr.Invoke("12", 1);

            r.Should().BeEquivalentTo("12-1");
        }

        [Theory(DisplayName = "Парсинг generic-типов")]
        [InlineData("()=>new System.Collections.Generic.List<string>()", typeof(List<string>))]
        [InlineData("()=>new System.Tuple<string>(\"string\")", typeof(Tuple<string>))]
        [InlineData("()=>new System.Tuple<string, int>(\"string\", 0)", typeof(Tuple<string, int>))]
        public void GenericsTests(string expression, Type expectedType)
        {
            var expr = Lambda.FromString(expression);
            var result = expr.Call<object>();

            result.Should().BeOfType(expectedType);
        }

        [Fact(DisplayName = "Повторяющиеся выражения должны кешироваться")]
        public void SimpleCachingTest()
        {
            const string Expression = "x => { var list = new System.Collections.Generic.List<string>(); list.Add(x); return list; }";
            var expr1 = Lambda.FromString(Expression).WithArgument<string>().ToLambda<List<string>>();
            var expr2 = Lambda.FromString(Expression).WithArgument<string>().ToLambda<List<string>>();

            expr1.Should().BeSameAs(expr2);
        }

        [Theory(DisplayName = "Вызов extension-методов Linq")]
        [InlineData("list => { var x = new System.Collections.Generic.List<int>() { -5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5 }; return x.Where(v => v > 0)}")]
        [InlineData("x => LambdaBuilder.Tests.StaticData.Query().Select(v => v)")]
        [InlineData("x => x.Where(v=>v > 3).Select(v => v)")]
        [InlineData("x => x.Where(v=>v > 3).Select(v => v).Cast<int>()")]
        public void LinqExtensionsTests(string expression)
        {
            var data = new List<int> { -5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5 };
            var expr = Lambda
                .FromString(expression)
                .WithAssemblyOf<NestedData>()
                .WithArgument<IQueryable<int>>();

            object result = null;
            Action action = () => result = expr.Call<object>(data.AsQueryable());
            action.ShouldNotThrow();
            result.Should().NotBeNull();
        }

        public class NestedData
        {
            public IQueryable<TestDto> EntitySet()
            {
                var fixture = new Fixture();
                return fixture.CreateMany<TestDto>().AsQueryable();
            }

            public IQueryable<TData> EntitySet<TData>()
            {
                return new Fixture().CreateMany<TData>().AsQueryable();
            }
        }        
    }
}