namespace LambdaBuilder
{
    /// <summary>
    /// ������� � �������� ����� ����������, ������� ���������� ��������� ����������
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="params">����� ����������</param>
    /// <returns></returns>
    public delegate TResult ParamsFunction<out TResult>(params object[] @params);
}