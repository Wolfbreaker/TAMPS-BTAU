using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace TAMPS
{
    internal sealed class TriadCandidate
    {
        internal object Actor;
        internal bool IsProtectedCandidate;
        internal object Pocket;

        internal static TriadCandidate Evaluate(
            object actor,
            object ammoBox)
        {
            TriadCandidate result =
                new TriadCandidate();

            result.Actor =
                actor;

            if (actor == null ||
                ammoBox == null)
            {
                return result;
            }

            object ammoLocation =
                ReflectionValue.Get(
                    ammoBox,
                    "Location");

            IList components =
                ReflectionValue.GetList(
                    actor,
                    "allComponents");

            if (components == null)
            {
                return result;
            }

            object pocket =
                null;

            List<object> locationAmmo =
                new List<object>();

            for (int i = 0;
                 i < components.Count;
                 i++)
            {
                object component =
                    components[i];

                if (component == null ||
                    !ReflectionValue.SameEnumValue(
                        ReflectionValue.Get(
                            component,
                            "Location"),
                        ammoLocation))
                {
                    continue;
                }

                string defId =
                    ComponentIdentity.GetDefId(
                        component);

                if (String.Equals(
                        defId,
                        PocketRuntime.GearId,
                        StringComparison.OrdinalIgnoreCase) &&
                    ReflectionValue.ToBoolean(
                        ReflectionValue.Get(
                            component,
                            "IsFunctional")))
                {
                    pocket =
                        component;
                }

                if (String.Equals(
                        component.GetType().FullName,
                        "BattleTech.AmmunitionBox",
                        StringComparison.Ordinal))
                {
                    locationAmmo.Add(
                        component);
                }
            }

            int runtimeAmmoIndex =
                -1;

            for (int i = 0;
                 i < locationAmmo.Count;
                 i++)
            {
                if (Object.ReferenceEquals(
                        locationAmmo[i],
                        ammoBox))
                {
                    runtimeAmmoIndex =
                        i;

                    break;
                }
            }

            result.Pocket =
                pocket;

            result.IsProtectedCandidate =
                pocket != null &&
                runtimeAmmoIndex >= 0 &&
                runtimeAmmoIndex < 2;

            return result;
        }
    }

    internal static class ComponentIdentity
    {
        internal static string GetDefId(
            object component)
        {
            if (component == null)
            {
                return "";
            }

            object direct =
                ReflectionValue.Get(
                    component,
                    "defId");

            string directText =
                ReflectionValue.ToText(
                    direct);

            if (!String.IsNullOrWhiteSpace(
                    directText) &&
                !String.Equals(
                    directText,
                    "null",
                    StringComparison.OrdinalIgnoreCase))
            {
                return directText;
            }

            object componentDef =
                ReflectionValue.Get(
                    component,
                    "componentDef");

            object description =
                ReflectionValue.Get(
                    componentDef,
                    "Description");

            return ReflectionValue.ToText(
                ReflectionValue.Get(
                    description,
                    "Id"));
        }
    }

    internal static class ReflectionValue
    {
        internal static object Get(
            object instance,
            string name)
        {
            if (instance == null ||
                String.IsNullOrWhiteSpace(
                    name))
            {
                return null;
            }

            Type type =
                instance.GetType();

            while (type != null)
            {
                try
                {
                    PropertyInfo property =
                        type.GetProperty(
                            name,
                            BindingFlags.Instance |
                            BindingFlags.Static |
                            BindingFlags.Public |
                            BindingFlags.NonPublic |
                            BindingFlags.DeclaredOnly);

                    if (property != null &&
                        property.GetIndexParameters().Length == 0)
                    {
                        return property.GetValue(
                            instance,
                            null);
                    }
                }
                catch
                {
                }

                try
                {
                    FieldInfo field =
                        type.GetField(
                            name,
                            BindingFlags.Instance |
                            BindingFlags.Static |
                            BindingFlags.Public |
                            BindingFlags.NonPublic |
                            BindingFlags.DeclaredOnly);

                    if (field != null)
                    {
                        return field.GetValue(
                            instance);
                    }
                }
                catch
                {
                }

                type =
                    type.BaseType;
            }

            return null;
        }

        internal static bool Set(
            object instance,
            string name,
            object value)
        {
            if (instance == null ||
                String.IsNullOrWhiteSpace(
                    name))
            {
                return false;
            }

            Type type =
                instance.GetType();

            while (type != null)
            {
                try
                {
                    PropertyInfo property =
                        type.GetProperty(
                            name,
                            BindingFlags.Instance |
                            BindingFlags.Static |
                            BindingFlags.Public |
                            BindingFlags.NonPublic |
                            BindingFlags.DeclaredOnly);

                    if (property != null &&
                        property.GetIndexParameters().Length == 0)
                    {
                        MethodInfo setter =
                            property.GetSetMethod(
                                true);

                        if (setter != null)
                        {
                            object converted =
                                ConvertValue(
                                    value,
                                    property.PropertyType);

                            setter.Invoke(
                                instance,
                                new object[]
                                {
                                    converted
                                });

                            return true;
                        }
                    }
                }
                catch
                {
                }

                try
                {
                    FieldInfo field =
                        type.GetField(
                            name,
                            BindingFlags.Instance |
                            BindingFlags.Static |
                            BindingFlags.Public |
                            BindingFlags.NonPublic |
                            BindingFlags.DeclaredOnly);

                    if (field != null)
                    {
                        object converted =
                            ConvertValue(
                                value,
                                field.FieldType);

                        field.SetValue(
                            instance,
                            converted);

                        return true;
                    }

                    FieldInfo backingField =
                        type.GetField(
                            "<" +
                            name +
                            ">k__BackingField",
                            BindingFlags.Instance |
                            BindingFlags.Static |
                            BindingFlags.Public |
                            BindingFlags.NonPublic |
                            BindingFlags.DeclaredOnly);

                    if (backingField != null)
                    {
                        object converted =
                            ConvertValue(
                                value,
                                backingField.FieldType);

                        backingField.SetValue(
                            instance,
                            converted);

                        return true;
                    }
                }
                catch
                {
                }

                type =
                    type.BaseType;
            }

            return false;
        }

        private static object ConvertValue(
            object value,
            Type targetType)
        {
            if (value == null)
            {
                return null;
            }

            if (targetType.IsInstanceOfType(
                    value))
            {
                return value;
            }

            if (targetType.IsEnum)
            {
                return Enum.Parse(
                    targetType,
                    value.ToString(),
                    true);
            }

            return Convert.ChangeType(
                value,
                targetType);
        }

        internal static IList GetList(
            object instance,
            string name)
        {
            object value =
                Get(
                    instance,
                    name);

            return value as IList;
        }

        internal static string ToText(
            object value)
        {
            if (value == null)
            {
                return "null";
            }

            try
            {
                return value.ToString();
            }
            catch
            {
                return "<ToString failed>";
            }
        }

        internal static int ToInt32(
            object value)
        {
            if (value == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(
                    value);
            }
            catch
            {
                int parsed;

                return Int32.TryParse(
                           ToText(
                               value),
                           out parsed)
                    ? parsed
                    : 0;
            }
        }

        internal static bool ToBoolean(
            object value)
        {
            if (value == null)
            {
                return false;
            }

            if (value is bool)
            {
                return (bool)value;
            }

            bool parsed;

            return Boolean.TryParse(
                       ToText(
                           value),
                       out parsed) &&
                   parsed;
        }

        internal static bool SameEnumValue(
            object left,
            object right)
        {
            if (left == null ||
                right == null)
            {
                return false;
            }

            try
            {
                return Convert.ToInt32(
                           left) ==
                       Convert.ToInt32(
                           right);
            }
            catch
            {
                return String.Equals(
                    ToText(
                        left),
                    ToText(
                        right),
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
