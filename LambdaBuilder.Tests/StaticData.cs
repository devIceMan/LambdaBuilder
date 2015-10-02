namespace LambdaBuilder.Tests
{
    using System.Linq;

    public static class StaticData
    {
        public static long LongValue()
        {
            return 10;
        }

        public static IQueryable<TestDto> Query()
        {
            return new LambdaBuilderTests.NestedData().EntitySet<TestDto>();
        }
    }
}