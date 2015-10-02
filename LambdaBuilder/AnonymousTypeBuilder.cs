namespace LambdaBuilder
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// —татический класс дл€ построени€ анонимных типов
    /// </summary>
    internal static class AnonymousTypeBuilder
    {
        private static readonly AssemblyName AssemblyName = new AssemblyName { Name = "AnonymousTypesAssembly" };

        private static readonly ModuleBuilder ModuleBuilder;

        private static readonly Dictionary<string, Type> BuiltTypes = new Dictionary<string, Type>();

        private static int instanceId;

        static AnonymousTypeBuilder()
        {                        
            ModuleBuilder = AppDomain.CurrentDomain
                .DefineDynamicAssembly(AssemblyName, AssemblyBuilderAccess.Run)
                .DefineDynamicModule(AssemblyName.Name);
        }

        /// <summary>
        /// —оздание анонимного типа, содержащего свойства, переданые в параметре <see cref="properties"/>.
        /// “акже дл€ типа формируетс€ конструктор по-умолчанию и конструктор с аргументами дл€ инициализации свойств
        /// </summary>
        /// <param name="properties">Ќабор свойств типа</param>        
        public static Type Build(IDictionary<string, Type> properties)
        {
            var typeKey = GetTypeKey(properties);
            Type anonymousType;

            lock (BuiltTypes)
            {
                if (BuiltTypes.TryGetValue(typeKey, out anonymousType))
                {
                    return anonymousType;
                }

                var className = "AnonymouType<" + instanceId.ToString("X") + ">";
                Interlocked.Increment(ref instanceId);

                var builder = ModuleBuilder.DefineType(className, TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed);
                var fieldBuilders = new List<FieldBuilder>();

                foreach (var pair in properties)
                {
                    var fieldName = "_" + pair.Key.ToLowerInvariant();

                    var field = builder.DefineField(fieldName, pair.Value, FieldAttributes.InitOnly | FieldAttributes.Private);
                    fieldBuilders.Add(field);

                    GenerateProperty(builder, pair.Key, field);
                }

                var propertyNames = properties.Keys.ToArray();

                GenerateClassAttributes(builder, propertyNames);
                GenerateConstructor(builder, propertyNames, fieldBuilders);
                GenerateEqualsMethod(builder, fieldBuilders.ToArray());
                GenerateGetHashCodeMethod(builder, fieldBuilders.ToArray());
                GenerateToStringMethod(builder, propertyNames, fieldBuilders.ToArray());

                BuiltTypes[typeKey] = anonymousType = builder.CreateType();
            }

            return anonymousType;
        }

        private static string GetTypeKey(IDictionary<string, Type> fields)
        {
            return fields.OrderBy(x => x.Key, StringComparer.Ordinal).Aggregate(string.Empty, (current, field) => current + (field.Key + ";" + field.Value.Name + ";"));
        }

        private static void AddDebuggerHiddenAttribute(ConstructorBuilder constructor)
        {
            var type = typeof(DebuggerHiddenAttribute);
            var customBuilder = new CustomAttributeBuilder(type.GetConstructor(new Type[0]), new object[0]);
            constructor.SetCustomAttribute(customBuilder);
        }

        private static void AddDebuggerHiddenAttribute(MethodBuilder method)
        {
            var type = typeof(DebuggerHiddenAttribute);
            var customBuilder = new CustomAttributeBuilder(type.GetConstructor(new Type[0]), new object[0]);
            method.SetCustomAttribute(customBuilder);
        }

        private static void GenerateClassAttributes(TypeBuilder dynamicType, string[] properties)
        {
            var type = typeof(CompilerGeneratedAttribute);
            var customBuilder = new CustomAttributeBuilder(type.GetConstructor(new Type[0]), new object[0]);
            dynamicType.SetCustomAttribute(customBuilder);
            var type2 = typeof(DebuggerDisplayAttribute);
            var builder2 = new StringBuilder(@"\{ ");
            var flag = true;
            foreach (var propertyName in properties)
            {
                builder2.AppendFormat("{0}{1} = ", flag ? "" : ", ", propertyName);
                builder2.Append("{");
                builder2.Append(propertyName);
                builder2.Append("}");
                flag = false;
            }
            builder2.Append(" }");
            var property = type2.GetProperty("Type");
            var builder3 = new CustomAttributeBuilder(type2.GetConstructor(new[] { typeof(string) }), new object[] { builder2.ToString() }, new[] { property }, new object[] { "<Anonymous Type>" });
            dynamicType.SetCustomAttribute(builder3);
        }

        private static void GenerateConstructor(TypeBuilder dynamicType, string[] properties, List<FieldBuilder> fields)
        {
            const MethodAttributes CtorAttributes = MethodAttributes.RTSpecialName | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Public;
            var defaultCtor = dynamicType.DefineDefaultConstructor(CtorAttributes);

            var ctor = dynamicType.DefineConstructor(CtorAttributes, CallingConventions.Standard, fields.Select(x => x.FieldType).ToArray());
            var ctorIl = ctor.GetILGenerator();
            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Call, defaultCtor);

            for (var i = 0; i < properties.Length; i++)
            {
                var strParamName = properties[i];
                var field = fields[i];
                var builder3 = ctor.DefineParameter(i + 1, ParameterAttributes.None, strParamName);
                ctorIl.Emit(OpCodes.Ldarg_0);
                ctorIl.Emit(OpCodes.Ldarg, builder3.Position);
                ctorIl.Emit(OpCodes.Stfld, field);
            }

            ctorIl.Emit(OpCodes.Ret);

            AddDebuggerHiddenAttribute(ctor);
        }

        private static void GenerateEqualsMethod(TypeBuilder dynamicType, FieldBuilder[] fields)
        {
            var methodInfoBody = dynamicType.DefineMethod("Equals", MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Public, CallingConventions.Standard, typeof(bool), new[] { typeof(object) });
            methodInfoBody.DefineParameter(0, ParameterAttributes.None, "value");
            var iLGenerator = methodInfoBody.GetILGenerator();
            var local = iLGenerator.DeclareLocal(dynamicType);
            var label = iLGenerator.DefineLabel();
            var label2 = iLGenerator.DefineLabel();
            iLGenerator.Emit(OpCodes.Ldarg_1);
            iLGenerator.Emit(OpCodes.Isinst, dynamicType);
            iLGenerator.Emit(OpCodes.Stloc, local);
            iLGenerator.Emit(OpCodes.Ldloc, local);
            var genericComparerType = typeof(EqualityComparer<>);

            foreach (var fieldBuilder in fields)
            {
                var comparerType = genericComparerType.MakeGenericType(fieldBuilder.FieldType);
                var getMethod = genericComparerType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static).GetGetMethod();
                //var method = TypeBuilder.GetMethod(comparerType, getMethod);

                iLGenerator.Emit(OpCodes.Brfalse_S, label);
                iLGenerator.EmitCall(OpCodes.Call, getMethod, null);
                iLGenerator.Emit(OpCodes.Ldarg_0);
                iLGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
                iLGenerator.Emit(OpCodes.Ldloc, local);
                iLGenerator.Emit(OpCodes.Ldfld, fieldBuilder);

                var type3 = genericComparerType.GetGenericArguments()[0];
                var info3 = genericComparerType.GetMethod("Equals", new[] { type3, type3 });
                //var methodInfo = TypeBuilder.GetMethod(comparerType, info3);
                iLGenerator.EmitCall(OpCodes.Callvirt, info3, null);
            }
            iLGenerator.Emit(OpCodes.Br_S, label2);
            iLGenerator.MarkLabel(label);
            iLGenerator.Emit(OpCodes.Ldc_I4_0);
            iLGenerator.MarkLabel(label2);
            iLGenerator.Emit(OpCodes.Ret);
            dynamicType.DefineMethodOverride(methodInfoBody, typeof(object).GetMethod("Equals", BindingFlags.Public | BindingFlags.Instance));

            AddDebuggerHiddenAttribute(methodInfoBody);
        }

        private static void GenerateGetHashCodeMethod(TypeBuilder dynamicType, FieldBuilder[] fields)
        {
            var methodInfoBody = dynamicType.DefineMethod("GetHashCode", MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Public, CallingConventions.Standard, typeof(int), new Type[0]);
            var iLGenerator = methodInfoBody.GetILGenerator();
            var type = typeof(EqualityComparer<>);
            var local = iLGenerator.DeclareLocal(typeof(int));
            iLGenerator.Emit(OpCodes.Ldc_I4, -747105811);
            iLGenerator.Emit(OpCodes.Stloc, local);
            foreach (var builder3 in fields)
            {
                iLGenerator.Emit(OpCodes.Ldc_I4, -1521134295);
                iLGenerator.Emit(OpCodes.Ldloc, local);
                iLGenerator.Emit(OpCodes.Mul);
                var type2 = type.MakeGenericType(builder3.FieldType);
                var getMethod = type.GetProperty("Default", BindingFlags.Public | BindingFlags.Static).GetGetMethod();
                //var method = TypeBuilder.GetMethod(type2, getMethod);
                iLGenerator.EmitCall(OpCodes.Call, getMethod, null);
                iLGenerator.Emit(OpCodes.Ldarg_0);
                iLGenerator.Emit(OpCodes.Ldfld, builder3);
                var type3 = type.GetGenericArguments()[0];
                var info3 = type.GetMethod("GetHashCode", new[] { type3 });
                //var methodInfo = TypeBuilder.GetMethod(type2, info3);
                iLGenerator.EmitCall(OpCodes.Callvirt, info3, null);
                iLGenerator.Emit(OpCodes.Add);
                iLGenerator.Emit(OpCodes.Stloc, local);
            }
            iLGenerator.Emit(OpCodes.Ldloc, local);
            iLGenerator.Emit(OpCodes.Ret);
            dynamicType.DefineMethodOverride(methodInfoBody, typeof(object).GetMethod("GetHashCode", BindingFlags.Public | BindingFlags.Instance));

            AddDebuggerHiddenAttribute(methodInfoBody);
        }

        private static void GenerateProperty(TypeBuilder dynamicType, string propertyName, FieldBuilder field)
        {
            const MethodAttributes AccessorAttributes = MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Public;

            var property = dynamicType.DefineProperty(propertyName, PropertyAttributes.None, field.FieldType, null);

            var getter = dynamicType.DefineMethod(string.Format("get_{0}", property.Name), AccessorAttributes);
            getter.SetReturnType(field.FieldType);
            var getterIl = getter.GetILGenerator();
            getterIl.Emit(OpCodes.Ldarg_0);
            getterIl.Emit(OpCodes.Ldfld, field);
            getterIl.Emit(OpCodes.Ret);
            property.SetGetMethod(getter);

            var setter = dynamicType.DefineMethod(string.Format("set_{0}", property.Name), AccessorAttributes);
            var setterIl = setter.GetILGenerator();
            setterIl.Emit(OpCodes.Ldarg_0);
            setterIl.Emit(OpCodes.Ldarg_1);
            setterIl.Emit(OpCodes.Stfld, field);
            property.SetSetMethod(setter);
        }

        private static void GenerateToStringMethod(TypeBuilder dynamicType, string[] propertyNames, FieldBuilder[] fields)
        {
            var methodInfoBody = dynamicType.DefineMethod("ToString", MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Public, CallingConventions.Standard, typeof(string), new Type[0]);
            var iLGenerator = methodInfoBody.GetILGenerator();
            var local = iLGenerator.DeclareLocal(typeof(StringBuilder));
            var methodInfo = typeof(StringBuilder).GetMethod("Append",new[] { typeof(object) });
            var info2 = typeof(StringBuilder).GetMethod("Append", new[] { typeof(string) });
            var info3 = typeof(object).GetMethod("ToString", new Type[0]);
            iLGenerator.Emit(OpCodes.Newobj, typeof(StringBuilder).GetConstructor(new Type[0]));
            iLGenerator.Emit(OpCodes.Stloc, local);
            iLGenerator.Emit(OpCodes.Ldloc, local);
            iLGenerator.Emit(OpCodes.Ldstr, "{ ");
            iLGenerator.EmitCall(OpCodes.Callvirt, info2, null);
            iLGenerator.Emit(OpCodes.Pop);

            for (var i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                iLGenerator.Emit(OpCodes.Ldloc, local);
                iLGenerator.Emit(OpCodes.Ldstr, (i == 0 ? "" : ", ") + propertyNames[i] + " = ");
                iLGenerator.EmitCall(OpCodes.Callvirt, info2, null);
                iLGenerator.Emit(OpCodes.Pop);
                iLGenerator.Emit(OpCodes.Ldloc, local);
                iLGenerator.Emit(OpCodes.Ldarg_0);
                iLGenerator.Emit(OpCodes.Ldfld, field);
                iLGenerator.Emit(OpCodes.Box, field.FieldType);
                iLGenerator.EmitCall(OpCodes.Callvirt, methodInfo, null);
                iLGenerator.Emit(OpCodes.Pop);
            }

            iLGenerator.Emit(OpCodes.Ldloc, local);
            iLGenerator.Emit(OpCodes.Ldstr, " }");
            iLGenerator.EmitCall(OpCodes.Callvirt, info2, null);
            iLGenerator.Emit(OpCodes.Pop);
            iLGenerator.Emit(OpCodes.Ldloc, local);
            iLGenerator.EmitCall(OpCodes.Callvirt, info3, null);
            iLGenerator.Emit(OpCodes.Ret);
            dynamicType.DefineMethodOverride(methodInfoBody, typeof(object).GetMethod("ToString", BindingFlags.Public | BindingFlags.Instance));

            AddDebuggerHiddenAttribute(methodInfoBody);
        }
    }
}