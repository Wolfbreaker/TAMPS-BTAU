using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace TAMPS
{
    internal static class PocketRuntimeState
    {
        private static readonly string[] StatCollectionMemberNames =
        {
            "StatCollection",
            "statCollection",
            "Stats",
            "stats"
        };

        private static readonly string[] DamageStatNames =
        {
            "DamageLevel",
            "ComponentDamageLevel"
        };

        private static readonly string[] RuntimeMemberNames =
        {
            "DamageLevel",
            "damageLevel",
            "_damageLevel",
            "componentDamageLevel",
            "_componentDamageLevel",
            "currentDamageLevel",
            "_currentDamageLevel"
        };

        private static readonly string[] CollectionSetterNames =
        {
            "Set",
            "SetValue",
            "SetStatistic"
        };

        private static readonly string[] StatisticSetterNames =
        {
            "SetValue",
            "Set"
        };

        private static readonly string[] StatisticValueMemberNames =
        {
            "Value",
            "value",
            "CurrentValue",
            "currentValue"
        };

        internal static bool TryApplyDamageLevel(
            object pocket,
            object hitInfo,
            string requestedDamageLevelName,
            bool applyEffects,
            out string strategy,
            out string diagnostics)
        {
            strategy =
                "none";

            diagnostics =
                "";

            if (pocket == null)
            {
                diagnostics =
                    "pocket is null";

                return false;
            }

            List<string> attempts =
                new List<string>();

            Type damageLevelType =
                ResolveDamageLevelType(
                    pocket);

            if (damageLevelType == null)
            {
                diagnostics =
                    "ComponentDamageLevel type could not be resolved";

                return false;
            }

            object requestedDamageLevel;

            try
            {
                requestedDamageLevel =
                    Enum.Parse(
                        damageLevelType,
                        requestedDamageLevelName,
                        true);
            }
            catch (Exception exception)
            {
                diagnostics =
                    "requested damage level could not be parsed: " +
                    exception.Message;

                return false;
            }

            bool normalCallInvoked =
                TryInvokeDamageComponent(
                    pocket,
                    hitInfo,
                    requestedDamageLevel,
                    applyEffects,
                    attempts);

            if (IsRequestedLevelReached(
                    pocket,
                    requestedDamageLevelName))
            {
                strategy =
                    normalCallInvoked
                        ? "MechComponent.DamageComponent"
                        : "existing runtime state";

                FinalizeDestroyedState(
                    pocket,
                    requestedDamageLevelName,
                    attempts);

                diagnostics =
                    JoinAttempts(
                        attempts);

                return true;
            }

            // Primary fallback: write the runtime DamageLevel statistic.
            object statCollection =
                FindStatCollection(
                    pocket,
                    attempts);

            if (statCollection != null)
            {
                for (int statIndex = 0;
                     statIndex < DamageStatNames.Length;
                     statIndex++)
                {
                    string statName =
                        DamageStatNames[statIndex];

                    string usedMethod;

                    if (TryInvokeCollectionSetter(
                            statCollection,
                            statName,
                            requestedDamageLevel,
                            damageLevelType,
                            attempts,
                            out usedMethod) &&
                        IsRequestedLevelReached(
                            pocket,
                            requestedDamageLevelName))
                    {
                        strategy =
                            usedMethod;

                        FinalizeDestroyedState(
                            pocket,
                            requestedDamageLevelName,
                            attempts);

                        diagnostics =
                            JoinAttempts(
                                attempts);

                        return true;
                    }

                    object statistic =
                        FindStatistic(
                            statCollection,
                            statName,
                            damageLevelType,
                            attempts);

                    if (statistic != null &&
                        TrySetStatisticValue(
                            statistic,
                            requestedDamageLevel,
                            damageLevelType,
                            attempts,
                            out usedMethod) &&
                        IsRequestedLevelReached(
                            pocket,
                            requestedDamageLevelName))
                    {
                        strategy =
                            usedMethod;

                        FinalizeDestroyedState(
                            pocket,
                            requestedDamageLevelName,
                            attempts);

                        diagnostics =
                            JoinAttempts(
                                attempts);

                        return true;
                    }
                }
            }

            // Secondary fallback: try likely runtime fields/properties on the
            // MechComponent itself. This does not touch save/reference objects.
            for (int i = 0;
                 i < RuntimeMemberNames.Length;
                 i++)
            {
                string memberName =
                    RuntimeMemberNames[i];

                bool set =
                    ReflectionValue.Set(
                        pocket,
                        memberName,
                        requestedDamageLevel);

                attempts.Add(
                    "pocket." +
                    memberName +
                    " setter=" +
                    set);

                if (set &&
                    IsRequestedLevelReached(
                        pocket,
                        requestedDamageLevelName))
                {
                    strategy =
                        "pocket." +
                        memberName;

                    FinalizeDestroyedState(
                        pocket,
                        requestedDamageLevelName,
                        attempts);

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
            object pocket,
            List<string> attempts)
        {
            for (int i = 0;
                 i < StatCollectionMemberNames.Length;
                 i++)
            {
                object value =
                    ReflectionValue.Get(
                        pocket,
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
            object enumValue,
            Type enumType,
            List<string> attempts,
            out string usedMethod)
        {
            usedMethod =
                "none";

            // Try the enum type first, then the underlying integer type.
            Type[] genericTypes =
            {
                enumType,
                Enum.GetUnderlyingType(
                    enumType)
            };

            object[] values =
            {
                enumValue,
                Convert.ChangeType(
                    enumValue,
                    Enum.GetUnderlyingType(
                        enumType))
            };

            MethodInfo[] methods =
                GetAllMethods(
                    statCollection.GetType());

            for (int typeIndex = 0;
                 typeIndex < genericTypes.Length;
                 typeIndex++)
            {
                Type genericType =
                    genericTypes[typeIndex];

                object value =
                    values[typeIndex];

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
                                genericType);

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
                                signature +
                                " stat=" +
                                statName +
                                " value=" +
                                value);

                            usedMethod =
                                "StatCollection." +
                                signature +
                                "[" +
                                statName +
                                "]";

                            return true;
                        }
                        catch (TargetInvocationException exception)
                        {
                            attempts.Add(
                                signature +
                                " stat=" +
                                statName +
                                " threw " +
                                DescribeInvocationException(
                                    exception));
                        }
                        catch (Exception exception)
                        {
                            attempts.Add(
                                signature +
                                " stat=" +
                                statName +
                                " threw " +
                                exception.GetType().Name);
                        }
                    }
                }
            }

            attempts.Add(
                "no usable StatCollection DamageLevel setter succeeded for " +
                statName);

            return false;
        }

        private static object FindStatistic(
            object statCollection,
            string statName,
            Type enumType,
            List<string> attempts)
        {
            Type[] genericTypes =
            {
                enumType,
                Enum.GetUnderlyingType(
                    enumType)
            };

            MethodInfo[] methods =
                GetAllMethods(
                    statCollection.GetType());

            for (int typeIndex = 0;
                 typeIndex < genericTypes.Length;
                 typeIndex++)
            {
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
                            genericTypes[typeIndex]);

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
                                "GetStatistic<" +
                                genericTypes[typeIndex].Name +
                                ">(" +
                                statName +
                                ") returned " +
                                statistic.GetType().FullName);

                            return statistic;
                        }
                    }
                    catch (TargetInvocationException exception)
                    {
                        attempts.Add(
                            "GetStatistic<" +
                            genericTypes[typeIndex].Name +
                            ">(" +
                            statName +
                            ") threw " +
                            DescribeInvocationException(
                                exception));
                    }
                    catch (Exception exception)
                    {
                        attempts.Add(
                            "GetStatistic<" +
                            genericTypes[typeIndex].Name +
                            ">(" +
                            statName +
                            ") threw " +
                            exception.GetType().Name);
                    }
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
                statName +
                " Statistic object was not found");

            return null;
        }

        private static bool TrySetStatisticValue(
            object statistic,
            object enumValue,
            Type enumType,
            List<string> attempts,
            out string usedMethod)
        {
            usedMethod =
                "none";

            Type underlyingType =
                Enum.GetUnderlyingType(
                    enumType);

            object underlyingValue =
                Convert.ChangeType(
                    enumValue,
                    underlyingType);

            object[] candidateValues =
            {
                enumValue,
                underlyingValue
            };

            for (int valueIndex = 0;
                 valueIndex < candidateValues.Length;
                 valueIndex++)
            {
                object value =
                    candidateValues[valueIndex];

                for (int i = 0;
                     i < StatisticValueMemberNames.Length;
                     i++)
                {
                    string memberName =
                        StatisticValueMemberNames[i];

                    bool set =
                        ReflectionValue.Set(
                            statistic,
                            memberName,
                            value);

                    attempts.Add(
                        "Statistic." +
                        memberName +
                        " setter=" +
                        set +
                        " valueType=" +
                        value.GetType().Name);

                    if (set)
                    {
                        usedMethod =
                            "Statistic." +
                            memberName +
                            " direct member";

                        return true;
                    }
                }
            }

            MethodInfo[] methods =
                GetAllMethods(
                    statistic.GetType());

            Type[] genericTypes =
            {
                enumType,
                underlyingType
            };

            for (int typeIndex = 0;
                 typeIndex < genericTypes.Length;
                 typeIndex++)
            {
                object value =
                    typeIndex == 0
                        ? enumValue
                        : underlyingValue;

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
                                genericTypes[typeIndex]);

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
                                signature +
                                " value=" +
                                value);

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
            }

            attempts.Add(
                "no usable Statistic DamageLevel setter succeeded");

            return false;
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
                                "[" +
                                statName +
                                "] returned " +
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
                            "[" +
                            statName +
                            "] returned " +
                            statistic.GetType().FullName);

                        return statistic;
                    }
                }

                current =
                    current.BaseType;
            }

            return null;
        }

        private static Type ResolveDamageLevelType(
            object pocket)
        {
            Type current =
                pocket.GetType();

            while (current != null)
            {
                PropertyInfo property =
                    current.GetProperty(
                        "DamageLevel",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly);

                if (property != null &&
                    property.PropertyType.IsEnum)
                {
                    return property.PropertyType;
                }

                current =
                    current.BaseType;
            }

            MethodInfo method =
                FindMethod(
                    pocket.GetType(),
                    "DamageComponent",
                    3);

            if (method != null)
            {
                ParameterInfo[] parameters =
                    method.GetParameters();

                if (parameters.Length >= 2 &&
                    parameters[1].ParameterType.IsEnum)
                {
                    return parameters[1].ParameterType;
                }
            }

            return null;
        }

        private static bool TryInvokeDamageComponent(
            object pocket,
            object hitInfo,
            object requestedDamageLevel,
            bool applyEffects,
            List<string> attempts)
        {
            MethodInfo method =
                FindMethod(
                    pocket.GetType(),
                    "DamageComponent",
                    3);

            if (method == null)
            {
                attempts.Add(
                    "DamageComponent(3) not found");

                return false;
            }

            try
            {
                method.Invoke(
                    pocket,
                    new object[]
                    {
                        hitInfo,
                        requestedDamageLevel,
                        applyEffects
                    });

                attempts.Add(
                    "DamageComponent invoked with applyEffects=" +
                    applyEffects +
                    " actualAfter=" +
                    CurrentDamageLevel(
                        pocket) +
                    " functionalAfter=" +
                    ReflectionValue.ToText(
                        ReflectionValue.Get(
                            pocket,
                            "IsFunctional")));

                return true;
            }
            catch (TargetInvocationException exception)
            {
                attempts.Add(
                    "DamageComponent threw " +
                    DescribeInvocationException(
                        exception));

                return false;
            }
            catch (Exception exception)
            {
                attempts.Add(
                    "DamageComponent threw " +
                    exception.GetType().Name +
                    ":" +
                    exception.Message);

                return false;
            }
        }

        private static void FinalizeDestroyedState(
            object pocket,
            string requestedDamageLevelName,
            List<string> attempts)
        {
            if (!String.Equals(
                    requestedDamageLevelName,
                    "Destroyed",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            MethodInfo cancelEffects =
                FindMethod(
                    pocket.GetType(),
                    "CancelCreatedEffects",
                    1);

            if (cancelEffects == null)
            {
                attempts.Add(
                    "CancelCreatedEffects(1) not found");

                return;
            }

            try
            {
                cancelEffects.Invoke(
                    pocket,
                    new object[]
                    {
                        true
                    });

                attempts.Add(
                    "CancelCreatedEffects(true) invoked");
            }
            catch (TargetInvocationException exception)
            {
                attempts.Add(
                    "CancelCreatedEffects threw " +
                    DescribeInvocationException(
                        exception));
            }
            catch (Exception exception)
            {
                attempts.Add(
                    "CancelCreatedEffects threw " +
                    exception.GetType().Name +
                    ":" +
                    exception.Message);
            }
        }

        private static bool IsRequestedLevelReached(
            object pocket,
            string requestedDamageLevelName)
        {
            string actual =
                CurrentDamageLevel(
                    pocket);

            if (String.Equals(
                    requestedDamageLevelName,
                    "Destroyed",
                    StringComparison.OrdinalIgnoreCase))
            {
                bool functional =
                    ReflectionValue.ToBoolean(
                        ReflectionValue.Get(
                            pocket,
                            "IsFunctional"));

                return String.Equals(
                           actual,
                           "Destroyed",
                           StringComparison.OrdinalIgnoreCase) &&
                       !functional;
            }

            return String.Equals(
                actual,
                requestedDamageLevelName,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string CurrentDamageLevel(
            object pocket)
        {
            return ReflectionValue.ToText(
                ReflectionValue.Get(
                    pocket,
                    "DamageLevel"));
        }

        private static bool TryBuildArguments(
            ParameterInfo[] parameters,
            string statName,
            object value,
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

        private static MethodInfo FindMethod(
            Type type,
            string name,
            int parameterCount)
        {
            Type current =
                type;

            while (current != null)
            {
                MethodInfo[] methods =
                    current.GetMethods(
                        BindingFlags.Instance |
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly);

                for (int i = 0;
                     i < methods.Length;
                     i++)
                {
                    if (String.Equals(
                            methods[i].Name,
                            name,
                            StringComparison.Ordinal) &&
                        methods[i].GetParameters().Length ==
                        parameterCount)
                    {
                        return methods[i];
                    }
                }

                current =
                    current.BaseType;
            }

            return null;
        }

        private static string DescribeMethod(
            MethodInfo method)
        {
            string generic =
                method.IsGenericMethod
                    ? "<" +
                      String.Join(
                          ",",
                          Array.ConvertAll(
                              method.GetGenericArguments(),
                              delegate(Type type)
                              {
                                  return type.Name;
                              })) +
                      ">"
                    : "";

            ParameterInfo[] parameters =
                method.GetParameters();

            List<string> names =
                new List<string>();

            for (int i = 0;
                 i < parameters.Length;
                 i++)
            {
                names.Add(
                    parameters[i].ParameterType.Name);
            }

            return method.Name +
                   generic +
                   "(" +
                   String.Join(
                       ",",
                       names.ToArray()) +
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
