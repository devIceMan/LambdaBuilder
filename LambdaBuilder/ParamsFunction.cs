namespace LambdaBuilder
{
    /// <summary>
    /// Делегат с заданным типом результата, имеющий переменное количетво аргументов
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="params">Набор аргументов</param>
    /// <returns></returns>
    public delegate TResult ParamsFunction<out TResult>(params object[] @params);
}