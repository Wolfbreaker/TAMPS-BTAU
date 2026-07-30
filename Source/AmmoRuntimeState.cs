using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace TAMPS
{
    internal static class AmmoRuntimeState
    {
        private static readonly string[] StatCollectionMemberNames =
        {
            "StatCollection",
            "statCollection",
            "Stats",
            "stats"
        };

        private static readonly string[] CollectionSetterNames =
        {
            "Set",
            "SetValue",
            "SetStatistic"
        };

        private static readonly string[] StatisticValueMemberNames =
        {
            "Value",
            "value",
            "CurrentValue",
            "currentValue"
        };

        private static readonly string[] StatisticSetterNames =
        {
            "SetValue",
            "Set"
        };

        internal static bool TrySetCurrentAmmo(
            object ammoBox,
            int value,
            out string strategy,
            out string diagnostics)
        {
            strategy =
                "none";

            diagnostics =
                "";

            if (ammoBox == null)
            {
                diagnostics =
                    "ammoBox is null";

                return false;
            }

            List<string> attempts =
                new List<string>();

            try
            {
                bool directSet =
                    ReflectionValue.Set(
                        ammoBox,
                        "CurrentAmmo",
                        value);

                attempts.Add(
                    "direct member setter=" +
                    directSet);

                if (directSet &&
                    VerifyCurrentAmmo(
                        ammoBox,
                        value))
                {
                    strategy =
                        "AmmunitionBox.CurrentAmmo direct member";

                    diagnostics =
                        JoinAttempts(
                            attempts);

                    return true;
                }
            }
            catch (Exception exception)
            {
                attempts.Add(
                    "direct member setter threw " +
                    exception.GetType().Name);
            }

            object statCollection =
                FindStatCollection(
                    ammoBox,
                    attempts);

            if (statCollection == null)
            {
                diagnostics =
                    JoinAttempts(
                        attempts);

                return false;
            }

            string usedMethod;

            if (TryInvokeCollectionSetter(
                    statCollection,
                    "CurrentAmmo",
                    value,
                    attempts,
                    out usedMethod) &&
                VerifyCurrentAmmo(
                    ammoBox,
                    value))
            {
                strategy =
                    usedMethod;

                diagnostics =
                    JoinAttempts(
                        attempts);

                return true;
            }

            object statistic =
                FindStatistic(
                    statCollection,
                    "CurrentAmmo",
                    attempts);

            if (statistic != null)
            {
                if (TrySetStatisticValue(
                        statistic,
                        value,
                        attempts,
                        out usedMethod) &&
                    VerifyCurrentAmmo(
                        ammoBox,
                        value))
                {
                    strategy =
                        usedMethod;

                    diagnostics =
                        JoinAttempts(
                            attempts);

                    return true;
                }
            }

            diagnostics =
                JoinAttempts(
                    attempts);

            return false;
        }

        private static object FindStatCollection(
            object ammoBox,
            List<string> attempts)
        {
            for (int i = 0;
                 i < StatCollectionMemberNames.Length;
                 i++)
            {
                object value =
                    ReflectionValue.Get(
                        ammoBox,
                        StatCollectionMemberNames[i]);

                if (value != null)
                {
                    attempts.Add(
                        "found " +
                        StatCollectionMemberNames[i] +
                        "=" +
                        value.GetType().FullName);

                    return value;
                }
            }

            attempts.Add(
                "no StatCollection member found");

            return null;
        }

        private static bool TryInvokeCollectionSetter(
            object statCollection,
            string statName,
            int value,
            List<string> attempts,
            out string usedMethod)
        {
            usedMethod =
                "none";

            MethodInfo[] methods =
                GetAllMethods(
                    statCollection.GetType());

            for (int nameIndex = 0;
                 nameIndex < CollectionSetterNames.Length;
                 nameIndex++)
            {
                string expectedName =
                    CollectionSetterNames[nameIndex];

                for (int methodIndex = 0;
                     methodIndex < methods.Length;
                     methodIndex++)
                {
                    MethodInfo candidate =
                        methods[methodIndex];

                    if (!String.Equals(
                            candidate.Name,
                            expectedName,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    MethodInfo callable =
                        CloseSingleGenericMethod(
                            candidate,
                            typeof(int));

                    if (callable == null)
                    {
                        continue;
                    }

                    ParameterInfo[] parameters =
                        callable.GetParameters();

                    if (parameters.Length < 2 ||
                        parameters[0].ParameterType != typeof(string))
                    {
                        continue;
                    }

                    object[] arguments;

                    if (!TryBuildArguments(
                            parameters,
                            statName,
                            value,
                            true,
                            out arguments))
                    {
                        continue;
                    }

                    string signature =
                        DescribeMethod(
                            callable);

                    try
                    {
                        callable.Invoke(
                            statCollection,
                            arguments);

                        attempts.Add(
                            "invoked " +
                            signature);

                        usedMethod =
                            "StatCollection." +
                            signature;

                        return true;
                    }
                    catch (TargetInvocationException exception)
                    {
                        attempts.Add(
                            signature +
                            " threw " +
                            DescribeInvocationException(
                                exception));
                    }
                    catch (Exception exception)
                    {
                        attempts.Add(
                            signature +
                            " threw " +
                            exception.GetType().Name);
                    }
                }
            }

            attempts.Add(
                "no usable StatCollection setter succeeded");

            return false;
        }

        private static object FindStatistic(
            object statCollection,
            string statName,
            List<string> attempts)
        {
            MethodInfo[] methods =
                GetAllMethods(
                    statCollection.GetType());

            for (int i = 0;
                 i < methods.Length;
                 i++)
            {
                MethodInfo candidate =
                    methods[i];

                if (!String.Equals(
                        candidate.Name,
                        "GetStatistic",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                MethodInfo callable =
                    CloseSingleGenericMethod(
                        candidate,
                        typeof(int));

                if (callable == null)
                {
                    continue;
                }

                ParameterInfo[] parameters =
                    callable.GetParameters();

                if (parameters.Length != 1 ||
                    parameters[0].ParameterType != typeof(string))
                {
                    continue;
                }

                try
                {
                    object statistic =
                        callable.Invoke(
                            statCollection,
                            new object[]
                            {
                                statName
                            });

                    if (statistic != null)
                    {
                        attempts.Add(
                            "GetStatistic returned " +
                            statistic.GetType().FullName);

                        return statistic;
                    }
                }
                catch (TargetInvocationException exception)
                {
                    attempts.Add(
                        "GetStatistic threw " +
                        DescribeInvocationException(
                            exception));
                }
                catch (Exception exception)
                {
                    attempts.Add(
                        "GetStatistic threw " +
                        exception.GetType().Name);
                }
            }

            object indexerStatistic =
                TryReadStringIndexer(
                    statCollection,
                    statName,
                    attempts);

            if (indexerStatistic != null)
            {
                return indexerStatistic;
            }

            object dictionaryStatistic =
                TryFindInDictionaries(
                    statCollection,
                    statName,
                    attempts);

            if (dictionaryStatistic != null)
            {
                return dictionaryStatistic;
            }

            attempts.Add(
                "CurrentAmmo Statistic object was not found");

            return null;
        }

        private static object TryReadStringIndexer(
            object statCollection,
            string statName,
            List<string> attempts)
        {
            Type current =
                statCollection.GetType();

            while (current != null)
            {
                PropertyInfo[] properties =
                    current.GetProperties(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly);

                for (int i = 0;
                     i < properties.Length;
                     i++)
                {
                    PropertyInfo property =
                        properties[i];

                    ParameterInfo[] indexParameters =
                        property.GetIndexParameters();

                    if (indexParameters.Length != 1 ||
                        indexParameters[0].ParameterType != typeof(string))
                    {
                        continue;
                    }

                    try
                    {
                        object value =
                            property.GetValue(
                                statCollection,
                                new object[]
                                {
                                    statName
                                });

                        if (value != null)
                        {
                            attempts.Add(
                                "string indexer " +
                                property.Name +
                                " returned " +
                                value.GetType().FullName);

                            return value;
                        }
                    }
                    catch
                    {
                    }
                }

                current =
                    current.BaseType;
            }

            return null;
        }

        private static object TryFindInDictionaries(
            object statCollection,
            string statName,
            List<string> attempts)
        {
            Type current =
                statCollection.GetType();

            while (current != null)
            {
                FieldInfo[] fields =
                    current.GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly);

                for (int i = 0;
                     i < fields.Length;
                     i++)
                {
                    object value;

                    try
                    {
                        value =
                            fields[i].GetValue(
                                statCollection);
                    }
                    catch
                    {
                        continue;
                    }

                    IDictionary dictionary =
                        value as IDictionary;

                    if (dictionary == null ||
                        !dictionary.Contains(
                            statName))
                    {
                        continue;
                    }

                    object statistic =
                        dictionary[statName];

                    if (statistic != null)
                    {
                        attempts.Add(
                            "dictionary field " +
                            fields[i].Name +
                            " returned " +
                            statistic.GetType().FullName);

                        return statistic;
                    }
                }

                current =
                    current.BaseType;
            }

            return null;
        }

        private static bool TrySetStatisticValue(
            object statistic,
            int value,
            List<string> attempts,
            out string usedMethod)
        {
            usedMethod =
                "none";

            for (int i = 0;
                 i < StatisticValueMemberNames.Length;
                 i++)
            {
                string memberName =
                    StatisticValueMemberNames[i];

                try
                {
                    bool set =
                        ReflectionValue.Set(
                            statistic,
                            memberName,
                            value);

                    attempts.Add(
                        "Statistic." +
                        memberName +
                        " setter=" +
                        set);

                    if (set)
                    {
                        usedMethod =
                            "Statistic." +
                            memberName +
                            " direct member";

                        return true;
                    }
                }
                catch (Exception exception)
                {
                    attempts.Add(
                        "Statistic." +
                        memberName +
                        " setter threw " +
                        exception.GetType().Name);
                }
            }

            MethodInfo[] methods =
                GetAllMethods(
                    statistic.GetType());

            for (int nameIndex = 0;
                 nameIndex < StatisticSetterNames.Length;
                 nameIndex++)
            {
                string expectedName =
                    StatisticSetterNames[nameIndex];

                for (int methodIndex = 0;
                     methodIndex < methods.Length;
                     methodIndex++)
                {
                    MethodInfo candidate =
                        methods[methodIndex];

                    if (!String.Equals(
                            candidate.Name,
                            expectedName,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    MethodInfo callable =
                        CloseSingleGenericMethod(
                            candidate,
                            typeof(int));

                    if (callable == null)
                    {
                        continue;
                    }

                    ParameterInfo[] parameters =
                        callable.GetParameters();

                    if (parameters.Length < 1)
                    {
                        continue;
                    }

                    object[] arguments;

                    if (!TryBuildArguments(
                            parameters,
                            null,
                            value,
                            false,
                            out arguments))
                    {
                        continue;
                    }

                    string signature =
                        DescribeMethod(
                            callable);

                    try
                    {
                        callable.Invoke(
                            statistic,
                            arguments);

                        attempts.Add(
                            "invoked Statistic." +
                            signature);

                        usedMethod =
                            "Statistic." +
                            signature;

                        return true;
                    }
                    catch (TargetInvocationException exception)
                    {
                        attempts.Add(
                            "Statistic." +
                            signature +
                            " threw " +
                            DescribeInvocationException(
                                exception));
                    }
                    catch (Exception exception)
                    {
                        attempts.Add(
                            "Statistic." +
                            signature +
                            " threw " +
                            exception.GetType().Name);
                    }
                }
            }

            attempts.Add(
                "no usable Statistic setter succeeded");

            return false;
        }

        private static bool TryBuildArguments(
            ParameterInfo[] parameters,
            string statName,
            int value,
            bool includesStatName,
            out object[] arguments)
        {
            arguments =
                new object[parameters.Length];

            int valueIndex =
                includesStatName
                    ? 1
                    : 0;

            if (includesStatName)
            {
                arguments[0] =
                    statName;
            }

            if (valueIndex >= parameters.Length)
            {
                return false;
            }

            object convertedValue;

            if (!TryConvert(
                    value,
                    parameters[valueIndex].ParameterType,
                    out convertedValue))
            {
                return false;
            }

            arguments[valueIndex] =
                convertedValue;

            for (int i = valueIndex + 1;
                 i < parameters.Length;
                 i++)
            {
                if (parameters[i].IsOptional)
                {
                    arguments[i] =
                        parameters[i].DefaultValue;

                    continue;
                }

                if (parameters[i].ParameterType == typeof(bool))
                {
                    arguments[i] =
                        false;

                    continue;
                }

                if (parameters[i].ParameterType.IsEnum)
                {
                    arguments[i] =
                        Enum.ToObject(
                            parameters[i].ParameterType,
                            0);

                    continue;
                }

                if (!parameters[i].ParameterType.IsValueType)
                {
                    arguments[i] =
                        null;

                    continue;
                }

                return false;
            }

            return true;
        }

        private static bool TryConvert(
            object value,
            Type targetType,
            out object converted)
        {
            converted =
                null;

            Type effectiveType =
                targetType.IsByRef
                    ? targetType.GetElementType()
                    : targetType;

            if (effectiveType == null)
            {
                return false;
            }

            if (effectiveType == typeof(object))
            {
                converted =
                    value;

                return true;
            }

            if (value != null &&
                effectiveType.IsInstanceOfType(
                    value))
            {
                converted =
                    value;

                return true;
            }

            try
            {
                if (effectiveType.IsEnum)
                {
                    converted =
                        Enum.ToObject(
                            effectiveType,
                            Convert.ToInt32(
                                value));

                    return true;
                }

                converted =
                    Convert.ChangeType(
                        value,
                        effectiveType);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static MethodInfo CloseSingleGenericMethod(
            MethodInfo method,
            Type genericType)
        {
            if (!method.IsGenericMethodDefinition)
            {
                return method.ContainsGenericParameters
                    ? null
                    : method;
            }

            Type[] genericArguments =
                method.GetGenericArguments();

            if (genericArguments.Length != 1)
            {
                return null;
            }

            try
            {
                return method.MakeGenericMethod(
                    genericType);
            }
            catch
            {
                return null;
            }
        }

        private static MethodInfo[] GetAllMethods(
            Type type)
        {
            List<MethodInfo> methods =
                new List<MethodInfo>();

            Type current =
                type;

            while (current != null)
            {
                methods.AddRange(
                    current.GetMethods(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly));

                current =
                    current.BaseType;
            }

            return methods.ToArray();
        }

        private static bool VerifyCurrentAmmo(
            object ammoBox,
            int expected)
        {
            int actual =
                ReflectionValue.ToInt32(
                    ReflectionValue.Get(
                        ammoBox,
                        "CurrentAmmo"));

            return actual ==
                   expected;
        }

        private static string DescribeMethod(
            MethodInfo method)
        {
            string generic =
                method.IsGenericMethod
                    ? "<" +
                      method.GetGenericArguments()[0].Name +
                      ">"
                    : "";

            ParameterInfo[] parameters =
                method.GetParameters();

            List<string> parameterNames =
                new List<string>();

            for (int i = 0;
                 i < parameters.Length;
                 i++)
            {
                parameterNames.Add(
                    parameters[i].ParameterType.Name);
            }

            return method.Name +
                   generic +
                   "(" +
                   String.Join(
                       ",",
                       parameterNames.ToArray()) +
                   ")";
        }

        private static string DescribeInvocationException(
            TargetInvocationException exception)
        {
            Exception inner =
                exception.InnerException;

            return inner == null
                ? exception.GetType().Name
                : inner.GetType().Name +
                  ":" +
                  inner.Message;
        }

        private static string JoinAttempts(
            List<string> attempts)
        {
            if (attempts == null ||
                attempts.Count == 0)
            {
                return "no attempts recorded";
            }

            return String.Join(
                " | ",
                attempts.ToArray());
        }
    }
}
