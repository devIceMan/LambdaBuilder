namespace LambdaBuilder
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Класс-контенер для методов-расширений типа <see cref="Type"/>
    /// </summary>
    public static class TypeExtensions
    {
        /// <summary>
        /// Получить имя типа. Возвращает полное имя типа в виде, используемом в коде.
        /// Например для <c>List[int]</c> вернет "System.Collections.Generic.List<int>",
        /// тогда как <see cref="Type.FullName"/> возвратит строку вида "System.Collections.Generic.List`1[System.Int32]"
        /// </summary>
        /// <param name="type">Тип, имя которого необходимо получить</param>        
        public static string TypeName(this Type type)
        {
            if (type == typeof(bool))
            {
                return "bool";
            }

            if (type == typeof(byte))
            {
                return "byte";
            }

            if (type == typeof(sbyte))
            {
                return "sbyte";
            }

            if (type == typeof(char))
            {
                return "char";
            }

            if (type == typeof(decimal))
            {
                return "decimal";
            }

            if (type == typeof(double))
            {
                return "double";
            }

            if (type == typeof(float))
            {
                return "float";
            }

            if (type == typeof(int))
            {
                return "int";
            }

            if (type == typeof(uint))
            {
                return "uint";
            }

            if (type == typeof(long))
            {
                return "long";
            }

            if (type == typeof(ulong))
            {
                return "ulong";
            }

            if (type == typeof(object))
            {
                return "object";
            }

            if (type == typeof(short))
            {
                return "short";
            }

            if (type == typeof(ushort))
            {
                return "ushort";
            }

            if (type == typeof(string))
            {
                return "string";
            }

            if (type == typeof(void))
            {
                return "void";
            }

            if (type.IsGenericType && type != typeof(Nullable<>) && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return type.GetGenericArguments().Single().TypeName() + "?";
            }

            if (type.IsGenericParameter)
            {
                return type.Name;
            }

            if (type.IsArray)
            {
                string arraySuffix = null;
                do
                {
                    var rankCommas = new string(',', type.GetArrayRank() - 1);
                    type = type.GetElementType();
                    arraySuffix = arraySuffix + "[" + rankCommas + "]";
                }
                while (type.IsArray);

                var basename = TypeName(type);
                return basename + arraySuffix;
            }

            if (type.IsGenericType)
            {
                var typeArgs = type.GetGenericArguments();
                var typeArgIdx = typeArgs.Length;
                var revNestedTypeNames = new List<string>();

                while (type != null)
                {
                    var name = type.FullName;
                    var backtickIdx = name.IndexOf('`');
                    if (backtickIdx == -1)
                    {
                        revNestedTypeNames.Add(name);
                    }
                    else
                    {
                        var afterArgCountIdx = name.IndexOf('[', backtickIdx + 1);
                        if (afterArgCountIdx == -1)
                        {
                            afterArgCountIdx = name.Length;
                        }

                        var thisTypeArgCount = int.Parse(name.Substring(backtickIdx + 1, afterArgCountIdx - backtickIdx - 1));
                        var argNames = new List<string>();
                        for (var i = typeArgIdx - thisTypeArgCount; i < typeArgIdx; i++)
                        {
                            argNames.Add(typeArgs[i].TypeName());
                        }

                        typeArgIdx -= thisTypeArgCount;
                        revNestedTypeNames.Add(name.Substring(0, backtickIdx) + "<" + string.Join(", ", argNames) + ">");
                    }

                    type = type.DeclaringType;
                }

                revNestedTypeNames.Reverse();
                return string.Join(".", revNestedTypeNames);
            }

            if (type.DeclaringType != null)
            {
                return type.DeclaringType.TypeName() + "." + type.Name;
            }

            return type.FullName;
        }
    }
}