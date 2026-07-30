using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace TAMPS
{
    internal static class PocketRuntime
    {
        internal const string GearId =
            "Gear_TAMPS";

        private const string HarmonyId =
            "com.nightsentinels.tamps";

        private static Logger logger;
        private static Settings settings;
        private static Harmony harmony;

        private static readonly HashSet<string> AllowedAmmoIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static void Initialize(
            IEnumerable<string> allowedAmmoIds,
            Settings loadedSettings,
            Logger loadedLogger)
        {
            logger = loadedLogger;
            settings = loadedSettings;

            AllowedAmmoIds.Clear();

            if (allowedAmmoIds != null)
            {
                foreach (string id in allowedAmmoIds)
                {
                    if (!String.IsNullOrWhiteSpace(id))
                    {
                        AllowedAmmoIds.Add(id);
                    }
                }
            }

            PocketRegistry.Configure(
                AllowedAmmoIds,
                logger,
                settings);

            CollapsiblePocketUI.Configure(
                settings);

            if (!settings.EnableSlotVirtualization)
            {
                logger.Write(
                    "TAMPS slot virtualization is disabled in settings.");
                return;
            }

            try
            {
                harmony = new Harmony(HarmonyId);
                ApplyPatches();

                logger.Write(
                    "Pocket runtime enabled. The two native DynamicSlots are the two TAMPS-protected magazine cells. " +
                    "The DLL tracks the first two AmmoBoxes for pocket identity and later protection.");
                logger.Write(
                    settings.EnableCollapsiblePocketUI
                        ? "Collapsible pocket UI enabled. The working marker Button and tracked hiding are retained. The module row is fixed to one 32 px header, and markers self-remove when a pooled row is reused for another component."
                        : "Collapsible pocket UI disabled in settings.");
                logger.Write(
                    "Triad combat behavior is enabled in v1.0.0 stable. " +
                    "The F9 test detonator acts only when F9 is pressed.");
            }
            catch (Exception exception)
            {
                logger.Write(
                    "Pocket runtime initialization failed: " +
                    exception);
            }
        }

        private static void ApplyPatches()
        {
            PatchPostfix(
                FindMethod(
                    "BattleTech.UI.MechLabPanel",
                    "LoadMech",
                    1,
                    "BattleTech.MechDef"),
                "MechLabPanel_LoadMech_Postfix");

            PatchPostfix(
                FindMethod(
                    "BattleTech.UI.MechLabPanel",
                    "CreateMechDef",
                    2),
                "MechLabPanel_CreateMechDef_Postfix");

            PatchPostfix(
                FindMethod(
                    "BattleTech.UI.MechLabPanel",
                    "ValidateLoadout",
                    1,
                    "System.Boolean"),
                "MechLabPanel_ValidateLoadout_Postfix");

            PatchPrefixAndPostfix(
                FindMethod(
                    "BattleTech.UI.MechLabLocationWidget",
                    "ValidateAdd",
                    1,
                    "BattleTech.MechComponentRef"),
                "MechLabLocationWidget_ValidateAdd_Prefix",
                "MechLabLocationWidget_ValidateAdd_Postfix");

            PatchPrefixAndPostfix(
                FindMethod(
                    "BattleTech.UI.MechLabLocationWidget",
                    "ValidateAddSimple",
                    1,
                    "BattleTech.MechComponentRef"),
                "MechLabLocationWidget_ValidateAdd_Prefix",
                "MechLabLocationWidget_ValidateAdd_Postfix");

            PatchPrefixAndPostfix(
                FindMethod(
                    "BattleTech.UI.MechLabLocationWidget",
                    "OnAddItem",
                    2,
                    "BattleTech.UI.IMechLabDraggableItem",
                    "System.Boolean"),
                "MechLabLocationWidget_OnAddItem_Prefix",
                "MechLabLocationWidget_OnAddItem_Postfix");

            PatchPrefixAndPostfix(
                FindMethod(
                    "BattleTech.UI.MechLabLocationWidget",
                    "OnRemoveItem",
                    2,
                    "BattleTech.UI.IMechLabDraggableItem",
                    "System.Boolean"),
                "MechLabLocationWidget_OnRemoveItem_Prefix",
                "MechLabLocationWidget_OnRemoveItem_Postfix");

            PatchPostfix(
                FindMethod(
                    "BattleTech.UI.MechLabLocationWidget",
                    "RefreshMechComponentData",
                    2,
                    "BattleTech.UI.MechLabItemSlotElement",
                    "System.Boolean"),
                "MechLabLocationWidget_RefreshMechComponentData_Postfix");

            PatchPostfix(
                FindMethod(
                    "BattleTech.UI.MechLabLocationWidget",
                    "SetData",
                    1,
                    "BattleTech.LocationLoadoutDef"),
                "MechLabLocationWidget_SetData_Postfix");

            PatchPrefixAndPostfix(
                FindMethod(
                    "BattleTech.UI.MechLabLocationWidget",
                    "ClearInventory",
                    0),
                "MechLabLocationWidget_ClearInventory_Prefix",
                "MechLabLocationWidget_ClearInventory_Postfix");

        }

        private static MethodInfo FindMethod(
            string typeName,
            string methodName,
            int parameterCount,
            params string[] parameterTypeNames)
        {
            Type type = AccessTools.TypeByName(typeName);

            if (type == null)
            {
                throw new MissingMemberException(
                    "Type not found: " + typeName);
            }

            MethodInfo[] methods = type.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic);

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];

                if (!String.Equals(
                        method.Name,
                        methodName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                ParameterInfo[] parameters =
                    method.GetParameters();

                if (parameters.Length != parameterCount)
                {
                    continue;
                }

                bool matches = true;

                for (int p = 0;
                     p < parameterTypeNames.Length &&
                     p < parameters.Length;
                     p++)
                {
                    string actual =
                        parameters[p].ParameterType.FullName ??
                        parameters[p].ParameterType.Name;

                    if (!String.Equals(
                            actual,
                            parameterTypeNames[p],
                            StringComparison.Ordinal))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    logger.Write(
                        "Patch target resolved: " +
                        typeName +
                        "." +
                        methodName +
                        "(" +
                        parameterCount +
                        ")");

                    return method;
                }
            }

            throw new MissingMethodException(
                typeName +
                "." +
                methodName +
                " with " +
                parameterCount +
                " parameter(s) was not found.");
        }

        private static void PatchPrefix(
            MethodInfo target,
            string prefixName)
        {
            harmony.Patch(
                target,
                prefix: new HarmonyMethod(
                    typeof(PocketPatches),
                    prefixName));
        }

        private static void PatchPostfix(
            MethodInfo target,
            string postfixName)
        {
            harmony.Patch(
                target,
                postfix: new HarmonyMethod(
                    typeof(PocketPatches),
                    postfixName));
        }

        private static void PatchPrefixAndPostfix(
            MethodInfo target,
            string prefixName,
            string postfixName)
        {
            harmony.Patch(
                target,
                prefix: new HarmonyMethod(
                    typeof(PocketPatches),
                    prefixName),
                postfix: new HarmonyMethod(
                    typeof(PocketPatches),
                    postfixName));
        }
    }

    internal static class PocketPatches
    {
        public static void MechLabPanel_LoadMech_Postfix(
            object __instance,
            object __0)
        {
            try
            {
                PocketRegistry.RebuildFromPanel(
                    __instance,
                    "LoadMech");
            }
            catch (Exception exception)
            {
                PocketRegistry.LogPatchError(
                    "MechLabPanel.LoadMech",
                    exception);
            }
        }

        public static void MechLabPanel_CreateMechDef_Postfix(
            object __instance,
            object __result)
        {
            try
            {
                PocketRegistry.LogMechDefAssignments(
                    __result,
                    "CreateMechDef");

                PocketRegistry.RebuildFromPanel(
                    __instance,
                    "CreateMechDef");
            }
            catch (Exception exception)
            {
                PocketRegistry.LogPatchError(
                    "MechLabPanel.CreateMechDef",
                    exception);
            }
        }

        public static void MechLabPanel_ValidateLoadout_Postfix(
            object __instance,
            bool __result)
        {
            try
            {
                PocketRegistry.RebuildFromPanel(
                    __instance,
                    "ValidateLoadout");
            }
            catch (Exception exception)
            {
                PocketRegistry.LogPatchError(
                    "MechLabPanel.ValidateLoadout",
                    exception);
            }
        }

        public static bool MechLabLocationWidget_ValidateAdd_Prefix(
            object __instance,
            object __0,
            ref List<object> __state,
            ref bool __result)
        {
            try
            {
                object panel =
                    PocketReflection.GetFieldValue(
                        __instance,
                        "mechLab");

                string location =
                    PocketReflection.GetWidgetLocation(
                        __instance);

                if (PocketRegistry.IsDuplicateModuleAddition(
                        panel,
                        __0,
                        location))
                {
                    PocketReflection.SetDropError(
                        __instance,
                        "Only one TAMPS can be installed in each side torso (maximum two per BattleMech).");

                    __result = false;
                    return false;
                }

                string reserveError;

                if (!PocketRegistry.ValidateAmmoOnlyReserve(
                        __instance,
                        __0,
                        out reserveError))
                {
                    PocketReflection.SetDropError(
                        __instance,
                        reserveError);

                    __result = false;
                    return false;
                }

                PocketRegistry.ObserveLocationSlots(
                    __instance,
                    "ValidateAdd prefix");
            }
            catch (Exception exception)
            {
                PocketRegistry.LogPatchError(
                    "MechLabLocationWidget.ValidateAdd prefix",
                    exception);
            }

            return true;
        }

        public static void MechLabLocationWidget_ValidateAdd_Postfix(
            List<object> __state)
        {
        }

        public static bool MechLabLocationWidget_OnAddItem_Prefix(
            object __instance,
            object __0,
            ref List<object> __state,
            ref bool __result)
        {
            try
            {
                object componentRef =
                    PocketReflection.GetMemberValue(
                        __0,
                        "ComponentRef");

                object panel =
                    PocketReflection.GetFieldValue(
                        __instance,
                        "mechLab");

                string location =
                    PocketReflection.GetWidgetLocation(
                        __instance);

                if (PocketRegistry.IsDuplicateModuleAddition(
                        panel,
                        componentRef,
                        location))
                {
                    PocketReflection.SetDropError(
                        __instance,
                        "Only one TAMPS can be installed in each side torso (maximum two per BattleMech).");

                    __result = false;
                    return false;
                }

                string reserveError;

                if (!PocketRegistry.ValidateAmmoOnlyReserve(
                        __instance,
                        componentRef,
                        out reserveError))
                {
                    PocketReflection.SetDropError(
                        __instance,
                        reserveError);

                    __result = false;
                    return false;
                }
            }
            catch (Exception exception)
            {
                PocketRegistry.LogPatchError(
                    "MechLabLocationWidget.OnAddItem prefix",
                    exception);
            }

            return true;
        }

        public static void MechLabLocationWidget_OnAddItem_Postfix(
            object __instance,
            List<object> __state)
        {
            try
            {
                object panel =
                    PocketReflection.GetFieldValue(
                        __instance,
                        "mechLab");

                PocketRegistry.RebuildFromPanel(
                    panel,
                    "OnAddItem");
            }
            catch (Exception exception)
            {
                PocketRegistry.LogPatchError(
                    "MechLabLocationWidget.OnAddItem postfix",
                    exception);
            }
        }

        public static void MechLabLocationWidget_OnRemoveItem_Prefix(
            object __instance)
        {
            try
            {
                CollapsiblePocketUI.RestoreAllTrackedRows(
                    "OnRemoveItem prefix");
            }
            catch (Exception exception)
            {
                PocketRegistry.LogPatchError(
                    "MechLabLocationWidget.OnRemoveItem prefix",
                    exception);
            }
        }

        public static void MechLabLocationWidget_OnRemoveItem_Postfix(
            object __instance)
        {
            try
            {
                object panel =
                    PocketReflection.GetFieldValue(
                        __instance,
                        "mechLab");

                PocketRegistry.RebuildFromPanel(
                    panel,
                    "OnRemoveItem");
            }
            catch (Exception exception)
            {
                PocketRegistry.LogPatchError(
                    "MechLabLocationWidget.OnRemoveItem",
                    exception);
            }
        }

        public static void MechLabLocationWidget_RefreshMechComponentData_Postfix(
            object __instance)
        {
            try
            {
                PocketRegistry.ObserveLocationSlots(
                    __instance,
                    "RefreshMechComponentData");

                CollapsiblePocketUI.Refresh(
                    __instance,
                    "RefreshMechComponentData");
            }
            catch (Exception exception)
            {
                PocketRegistry.LogPatchError(
                    "MechLabLocationWidget.RefreshMechComponentData",
                    exception);
            }
        }

        public static void MechLabLocationWidget_SetData_Postfix(
            object __instance)
        {
            try
            {
                PocketRegistry.ObserveLocationSlots(
                    __instance,
                    "SetData");

                CollapsiblePocketUI.Refresh(
                    __instance,
                    "SetData");
            }
            catch (Exception exception)
            {
                PocketRegistry.LogPatchError(
                    "MechLabLocationWidget.SetData",
                    exception);
            }
        }

        public static void MechLabLocationWidget_ClearInventory_Prefix(
            object __instance)
        {
            try
            {
                CollapsiblePocketUI.RestoreAllTrackedRows(
                    "ClearInventory prefix");
            }
            catch (Exception exception)
            {
                PocketRegistry.LogPatchError(
                    "MechLabLocationWidget.ClearInventory prefix",
                    exception);
            }
        }

        public static void MechLabLocationWidget_ClearInventory_Postfix(
            object __instance)
        {
            try
            {
                PocketRegistry.ObserveLocationSlots(
                    __instance,
                    "ClearInventory");

                CollapsiblePocketUI.Refresh(
                    __instance,
                    "ClearInventory");
            }
            catch (Exception exception)
            {
                PocketRegistry.LogPatchError(
                    "MechLabLocationWidget.ClearInventory",
                    exception);
            }
        }

    }

    internal static class PocketRegistry
    {
        private static readonly object Sync =
            new object();

        private static readonly ReferenceComparer RefComparer =
            new ReferenceComparer();

        private static HashSet<object> Contained =
            new HashSet<object>(RefComparer);

        private static HashSet<string> allowedAmmoIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        private static Logger logger;
        private static Settings settings;
        private static string lastSummary = "";

        internal static void Configure(
            IEnumerable<string> ammoIds,
            Logger loadedLogger,
            Settings loadedSettings)
        {
            logger = loadedLogger;
            settings = loadedSettings;

            allowedAmmoIds =
                new HashSet<string>(
                    ammoIds ??
                    new string[0],
                    StringComparer.OrdinalIgnoreCase);
        }

        internal static bool IsContained(
            object componentRef)
        {
            if (componentRef == null)
            {
                return false;
            }

            lock (Sync)
            {
                return Contained.Contains(
                    componentRef);
            }
        }

        internal static void LogInfo(
            string message)
        {
            if (logger != null &&
                !String.IsNullOrWhiteSpace(message))
            {
                logger.Write(message);
            }
        }

        internal static void LogPatchError(
            string patch,
            Exception exception)
        {
            if (logger != null)
            {
                logger.Write(
                    "Patch error in " +
                    patch +
                    ": " +
                    exception);
            }
        }

        internal static void RebuildFromPanel(
            object panel,
            string reason)
        {
            if (panel == null)
            {
                return;
            }

            object inventoryObject =
                PocketReflection.GetFieldValue(
                    panel,
                    "activeMechInventory");

            IEnumerable inventory =
                inventoryObject as IEnumerable;

            HashSet<object> newSet =
                BuildAssignments(inventory);

            ReplaceContained(
                newSet,
                reason);

            object left =
                PocketReflection.GetFieldValue(
                    panel,
                    "leftTorsoWidget");

            object right =
                PocketReflection.GetFieldValue(
                    panel,
                    "rightTorsoWidget");

            ObserveLocationSlots(left, reason + " LT");
            ObserveLocationSlots(right, reason + " RT");

            CollapsiblePocketUI.Refresh(
                left,
                reason + " LT");

            CollapsiblePocketUI.Refresh(
                right,
                reason + " RT");
        }

        internal static void LogMechDefAssignments(
            object mechDef,
            string reason)
        {
            if (mechDef == null)
            {
                return;
            }

            IEnumerable inventory =
                PocketReflection.GetMemberValue(
                    mechDef,
                    "Inventory") as IEnumerable;

            HashSet<object> assignments =
                BuildAssignments(inventory);

            if (settings != null &&
                settings.VerbosePocketLogging)
            {
                logger.Write(
                    reason +
                    " generated " +
                    assignments.Count +
                    " contained AmmoBox reference(s).");
            }
        }

        internal static HashSet<object>
            ReplaceWithMechDefAssignments(
                object mechDef,
                string reason)
        {
            HashSet<object> previous;

            lock (Sync)
            {
                previous =
                    new HashSet<object>(
                        Contained,
                        RefComparer);
            }

            IEnumerable inventory =
                PocketReflection.GetMemberValue(
                    mechDef,
                    "Inventory") as IEnumerable;

            HashSet<object> assignments =
                BuildAssignments(inventory);

            ReplaceContained(
                assignments,
                reason);

            return previous;
        }

        internal static void RestoreContainedSet(
            HashSet<object> previous)
        {
            if (previous == null)
            {
                return;
            }

            lock (Sync)
            {
                Contained =
                    new HashSet<object>(
                        previous,
                        RefComparer);
            }
        }

        internal static bool ValidateAmmoOnlyReserve(
            object widget,
            object proposedComponentRef,
            out string error)
        {
            error = "";

            if (widget == null ||
                proposedComponentRef == null)
            {
                return true;
            }

            string location =
                PocketReflection.GetWidgetLocation(
                    widget);

            if (!IsSideTorso(location))
            {
                return true;
            }

            List<object> currentRefs =
                new List<object>();

            IEnumerable localInventory =
                PocketReflection.GetFieldValue(
                    widget,
                    "localInventory") as IEnumerable;

            if (localInventory != null)
            {
                foreach (object item in localInventory)
                {
                    object componentRef =
                        PocketReflection.GetMemberValue(
                            item,
                            "ComponentRef");

                    if (componentRef == null)
                    {
                        componentRef =
                            PocketReflection.GetMemberValue(
                                item,
                                "componentRef");
                    }

                    if (componentRef != null)
                    {
                        currentRefs.Add(
                            componentRef);
                    }
                }
            }

            bool moduleAlreadyPresent =
                HasModule(
                    currentRefs,
                    location);

            bool proposedIsModule =
                String.Equals(
                    PocketReflection.GetComponentId(
                        proposedComponentRef),
                    PocketRuntime.GearId,
                    StringComparison.OrdinalIgnoreCase);

            if (!moduleAlreadyPresent &&
                !proposedIsModule)
            {
                return true;
            }

            object maxObject =
                PocketReflection.GetFieldValue(
                    widget,
                    "maxSlots");

            if (maxObject == null)
            {
                return true;
            }

            int displayedMax =
                Convert.ToInt32(
                    maxObject);

            // If the module is already mounted, BTA has already increased
            // maxSlots by two. While the module is being added, displayedMax
            // still represents the normal torso capacity.
            int normalCapacity =
                moduleAlreadyPresent
                    ? Math.Max(
                        0,
                        displayedMax - 2)
                    : displayedMax;

            int nonAmmoSlots = 0;

            for (int i = 0;
                 i < currentRefs.Count;
                 i++)
            {
                object componentRef =
                    currentRefs[i];

                if (!IsAmmoBox(
                        componentRef))
                {
                    nonAmmoSlots +=
                        PocketReflection.GetRawInventorySize(
                            componentRef);
                }
            }

            if (!IsAmmoBox(
                    proposedComponentRef))
            {
                nonAmmoSlots +=
                    PocketReflection.GetRawInventorySize(
                        proposedComponentRef);
            }

            if (nonAmmoSlots >
                normalCapacity)
            {
                error =
                    "The two TAMPS DynamicSlots are ammo-only. " +
                    "Non-ammunition equipment must fit inside the torso's normal " +
                    "critical-slot capacity.";

                return false;
            }

            return true;
        }

        internal static List<object>
            PrepareTemporaryAddition(
                object panel,
                object newComponentRef,
                string targetLocation)
        {
            List<object> added =
                new List<object>();

            if (panel == null ||
                newComponentRef == null ||
                !IsSideTorso(targetLocation))
            {
                return added;
            }

            List<object> inventory =
                PocketReflection.ToObjectList(
                    PocketReflection.GetFieldValue(
                        panel,
                        "activeMechInventory")
                    as IEnumerable);

            string newId =
                PocketReflection.GetComponentId(
                    newComponentRef);

            if (String.Equals(
                    newId,
                    PocketRuntime.GearId,
                    StringComparison.OrdinalIgnoreCase))
            {
                List<object> ammo =
                    FindAmmo(
                        inventory,
                        targetLocation);

                for (int i = 0;
                     i < ammo.Count && i < 2;
                     i++)
                {
                    AddTemporary(
                        ammo[i],
                        added);
                }

                return added;
            }

            if (!IsAmmoBox(newComponentRef))
            {
                return added;
            }

            if (!HasModule(
                    inventory,
                    targetLocation))
            {
                return added;
            }

            int existingAmmo =
                FindAmmo(
                    inventory,
                    targetLocation).Count;

            if (existingAmmo < 2)
            {
                AddTemporary(
                    newComponentRef,
                    added);
            }

            return added;
        }

        internal static bool IsDuplicateModuleAddition(
            object panel,
            object newComponent,
            string targetLocation)
        {
            if (panel == null ||
                newComponent == null ||
                !IsSideTorso(targetLocation))
            {
                return false;
            }

            string id =
                PocketReflection.GetComponentId(
                    newComponent);

            if (!String.Equals(
                    id,
                    PocketRuntime.GearId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            List<object> inventory =
                PocketReflection.ToObjectList(
                    PocketReflection.GetFieldValue(
                        panel,
                        "activeMechInventory")
                    as IEnumerable);

            return HasModule(
                inventory,
                targetLocation);
        }

        internal static void RemoveTemporary(
            List<object> added)
        {
            if (added == null ||
                added.Count == 0)
            {
                return;
            }

            lock (Sync)
            {
                for (int i = 0;
                     i < added.Count;
                     i++)
                {
                    Contained.Remove(
                        added[i]);
                }
            }
        }

        private static void AddTemporary(
            object componentRef,
            List<object> added)
        {
            lock (Sync)
            {
                if (Contained.Add(
                        componentRef))
                {
                    added.Add(
                        componentRef);
                }
            }
        }

        private static HashSet<object>
            BuildAssignments(
                IEnumerable inventoryEnumerable)
        {
            HashSet<object> result =
                new HashSet<object>(
                    RefComparer);

            List<object> inventory =
                PocketReflection.ToObjectList(
                    inventoryEnumerable);

            AddLocationAssignments(
                inventory,
                "LeftTorso",
                result);

            AddLocationAssignments(
                inventory,
                "RightTorso",
                result);

            return result;
        }

        private static void AddLocationAssignments(
            List<object> inventory,
            string location,
            HashSet<object> result)
        {
            if (!HasModule(
                    inventory,
                    location))
            {
                return;
            }

            List<object> ammo =
                FindAmmo(
                    inventory,
                    location);

            for (int i = 0;
                 i < ammo.Count && i < 2;
                 i++)
            {
                result.Add(
                    ammo[i]);
            }
        }

        private static bool HasModule(
            List<object> inventory,
            string location)
        {
            for (int i = 0;
                 i < inventory.Count;
                 i++)
            {
                object componentRef =
                    inventory[i];

                if (!String.Equals(
                        PocketReflection.GetLocationName(
                            componentRef),
                        location,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (String.Equals(
                        PocketReflection.GetComponentId(
                            componentRef),
                        PocketRuntime.GearId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<object> FindAmmo(
            List<object> inventory,
            string location)
        {
            List<object> ammo =
                new List<object>();

            for (int i = 0;
                 i < inventory.Count;
                 i++)
            {
                object componentRef =
                    inventory[i];

                if (!String.Equals(
                        PocketReflection.GetLocationName(
                            componentRef),
                        location,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                string id =
                    PocketReflection.GetComponentId(
                        componentRef);

                if (IsAmmoBox(componentRef))
                {
                    ammo.Add(
                        componentRef);
                }
            }

            return ammo;
        }

        internal static bool IsAmmoBox(
            object componentRef)
        {
            if (componentRef == null)
            {
                return false;
            }

            // Primary check: the live component reference knows its actual
            // BattleTech ComponentType, independent of folder names.
            object componentType =
                PocketReflection.GetMemberValue(
                    componentRef,
                    "ComponentDefType");

            if (componentType != null &&
                String.Equals(
                    Convert.ToString(componentType),
                    "AmmunitionBox",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Secondary check: loaded definition type. This catches modded
            // AmmoBoxDefs even when their JSON is stored outside an
            // ammobox/ammunitionbox directory.
            object definition =
                PocketReflection.GetMemberValue(
                    componentRef,
                    "Def");

            if (definition != null)
            {
                Type definitionType =
                    definition.GetType();

                string typeName =
                    definitionType.FullName ??
                    definitionType.Name;

                if (!String.IsNullOrEmpty(typeName) &&
                    typeName.IndexOf(
                        "AmmunitionBoxDef",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                object defComponentType =
                    PocketReflection.GetMemberValue(
                        definition,
                        "ComponentType");

                if (defComponentType != null &&
                    String.Equals(
                        Convert.ToString(defComponentType),
                        "AmmunitionBox",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Compatibility fallback for incomplete/unresolved references.
            string id =
                PocketReflection.GetComponentId(
                    componentRef);

            return !String.IsNullOrWhiteSpace(id) &&
                   allowedAmmoIds.Contains(id);
        }

        private static bool IsSideTorso(
            string location)
        {
            return String.Equals(
                       location,
                       "LeftTorso",
                       StringComparison.Ordinal) ||
                   String.Equals(
                       location,
                       "RightTorso",
                       StringComparison.Ordinal);
        }

        private static void ReplaceContained(
            HashSet<object> newSet,
            string reason)
        {
            string summary =
                BuildSummary(newSet);

            lock (Sync)
            {
                Contained =
                    new HashSet<object>(
                        newSet,
                        RefComparer);
            }

            if (!String.Equals(
                    summary,
                    lastSummary,
                    StringComparison.Ordinal))
            {
                lastSummary = summary;

                if (logger != null)
                {
                    logger.Write(
                        "Pocket assignment [" +
                        reason +
                        "]: " +
                        summary);
                }
            }
        }

        private static string BuildSummary(
            HashSet<object> assignments)
        {
            if (assignments == null ||
                assignments.Count == 0)
            {
                return "no AmmoBoxes contained";
            }

            List<string> labels =
                new List<string>();

            foreach (object componentRef
                     in assignments)
            {
                labels.Add(
                    PocketReflection.GetLocationName(
                        componentRef) +
                    ":" +
                    PocketReflection.GetComponentId(
                        componentRef));
            }

            labels.Sort(
                StringComparer.OrdinalIgnoreCase);

            return String.Join(
                ", ",
                labels.ToArray());
        }

        internal static void ObserveLocationSlots(
            object widget,
            string reason)
        {
            if (widget == null)
            {
                return;
            }

            IEnumerable localInventory =
                PocketReflection.GetFieldValue(
                    widget,
                    "localInventory") as IEnumerable;

            if (localInventory == null)
            {
                return;
            }

            int rawSlots = 0;
            int nonAmmoSlots = 0;
            int ammoCount = 0;
            int pocketAssigned = 0;
            int itemCount = 0;
            bool hasModule = false;

            foreach (object item in localInventory)
            {
                object componentRef =
                    PocketReflection.GetMemberValue(
                        item,
                        "ComponentRef");

                if (componentRef == null)
                {
                    componentRef =
                        PocketReflection.GetMemberValue(
                            item,
                            "componentRef");
                }

                if (componentRef == null)
                {
                    continue;
                }

                itemCount++;

                int rawSize =
                    PocketReflection.GetRawInventorySize(
                        componentRef);

                rawSlots +=
                    rawSize;

                if (String.Equals(
                        PocketReflection.GetComponentId(
                            componentRef),
                        PocketRuntime.GearId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    hasModule = true;
                }

                if (IsAmmoBox(
                        componentRef))
                {
                    ammoCount++;
                }
                else
                {
                    nonAmmoSlots +=
                        rawSize;
                }

                if (IsContained(
                        componentRef))
                {
                    pocketAssigned++;
                }
            }

            object usedObject =
                PocketReflection.GetFieldValue(
                    widget,
                    "usedSlots");

            object maxObject =
                PocketReflection.GetFieldValue(
                    widget,
                    "maxSlots");

            int usedSlots =
                usedObject == null
                    ? -1
                    : Convert.ToInt32(
                        usedObject);

            int maxSlots =
                maxObject == null
                    ? -1
                    : Convert.ToInt32(
                        maxObject);

            int normalCapacity =
                hasModule &&
                maxSlots >= 2
                    ? maxSlots - 2
                    : maxSlots;

            if (logger != null &&
                (hasModule ||
                 (settings != null &&
                  settings.VerbosePocketLogging)))
            {
                logger.Write(
                    "Native pocket accounting [" +
                    reason +
                    "] " +
                    PocketReflection.GetWidgetLocation(
                        widget) +
                    ": items=" +
                    itemCount +
                    ", raw=" +
                    rawSlots +
                    ", nonAmmo=" +
                    nonAmmoSlots +
                    ", ammo=" +
                    ammoCount +
                    ", pocketAssigned=" +
                    pocketAssigned +
                    ", used=" +
                    usedSlots +
                    ", max=" +
                    maxSlots +
                    ", normalCapacity=" +
                    normalCapacity +
                    ". usedSlots is not modified by the DLL.");
            }
        }
    }

    internal static class PocketReflection
    {
        private const BindingFlags AllInstance =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        internal static object GetFieldValue(
            object instance,
            string name)
        {
            if (instance == null)
            {
                return null;
            }

            FieldInfo field =
                AccessTools.Field(
                    instance.GetType(),
                    name);

            return field == null
                ? null
                : field.GetValue(
                    instance);
        }

        internal static void SetFieldValue(
            object instance,
            string name,
            object value)
        {
            if (instance == null)
            {
                return;
            }

            FieldInfo field =
                AccessTools.Field(
                    instance.GetType(),
                    name);

            if (field != null)
            {
                field.SetValue(
                    instance,
                    value);
            }
        }

        internal static object GetMemberValue(
            object instance,
            string name)
        {
            if (instance == null)
            {
                return null;
            }

            PropertyInfo property =
                instance.GetType().GetProperty(
                    name,
                    AllInstance);

            if (property != null &&
                property.GetIndexParameters().Length == 0)
            {
                return property.GetValue(
                    instance,
                    null);
            }

            FieldInfo field =
                instance.GetType().GetField(
                    name,
                    AllInstance);

            if (field != null)
            {
                return field.GetValue(
                    instance);
            }

            return null;
        }

        internal static string GetComponentId(
            object componentOrRef)
        {
            if (componentOrRef == null)
            {
                return "";
            }

            object direct =
                GetMemberValue(
                    componentOrRef,
                    "ComponentDefID");

            if (direct != null)
            {
                return Convert.ToString(
                    direct) ?? "";
            }

            object definition =
                GetMemberValue(
                    componentOrRef,
                    "Def");

            if (definition == null)
            {
                definition =
                    componentOrRef;
            }

            object description =
                GetMemberValue(
                    definition,
                    "Description");

            object id =
                GetMemberValue(
                    description,
                    "Id");

            return id == null
                ? ""
                : Convert.ToString(
                    id) ?? "";
        }

        internal static string GetLocationName(
            object componentRef)
        {
            object location =
                GetMemberValue(
                    componentRef,
                    "MountedLocation");

            return location == null
                ? ""
                : Convert.ToString(
                    location) ?? "";
        }

        internal static string GetWidgetLocation(
            object widget)
        {
            object loadout =
                GetFieldValue(
                    widget,
                    "loadout");

            object location =
                GetMemberValue(
                    loadout,
                    "Location");

            return location == null
                ? ""
                : Convert.ToString(
                    location) ?? "";
        }

        internal static List<object> ToObjectList(
            IEnumerable enumerable)
        {
            List<object> list =
                new List<object>();

            if (enumerable == null)
            {
                return list;
            }

            foreach (object item in enumerable)
            {
                if (item != null)
                {
                    list.Add(
                        item);
                }
            }

            return list;
        }

        internal static int GetRawInventorySize(
            object componentRef)
        {
            if (componentRef == null)
            {
                return 0;
            }

            object definition =
                GetMemberValue(
                    componentRef,
                    "Def");

            object inventorySize =
                GetMemberValue(
                    definition,
                    "InventorySize");

            if (inventorySize != null)
            {
                return Convert.ToInt32(
                    inventorySize);
            }

            // Fallback only. The primary path deliberately does not call
            // MechComponentRef.Size(), because v0.4.4 must measure the
            // unmodified raw size and subtract pocket-contained bins itself.
            MethodInfo sizeMethod =
                AccessTools.Method(
                    componentRef.GetType(),
                    "Size",
                    new Type[0]);

            if (sizeMethod == null)
            {
                return 0;
            }

            object value =
                sizeMethod.Invoke(
                    componentRef,
                    null);

            return value == null
                ? 0
                : Convert.ToInt32(
                    value);
        }

        internal static int GetComponentSize(
            object componentRef)
        {
            if (componentRef == null)
            {
                return 0;
            }

            MethodInfo sizeMethod =
                AccessTools.Method(
                    componentRef.GetType(),
                    "Size",
                    new Type[0]);

            if (sizeMethod != null)
            {
                object value =
                    sizeMethod.Invoke(
                        componentRef,
                        null);

                if (value != null)
                {
                    return Convert.ToInt32(
                        value);
                }
            }

            object definition =
                GetMemberValue(
                    componentRef,
                    "Def");

            object inventorySize =
                GetMemberValue(
                    definition,
                    "InventorySize");

            return inventorySize == null
                ? 0
                : Convert.ToInt32(
                    inventorySize);
        }

        internal static void SetDropError(
            object widget,
            string message)
        {
            if (widget == null)
            {
                return;
            }

            MethodInfo method =
                AccessTools.Method(
                    widget.GetType(),
                    "SetDropErrorMessage",
                    new Type[]
                    {
                        typeof(string),
                        typeof(object[])
                    });

            if (method != null)
            {
                method.Invoke(
                    widget,
                    new object[]
                    {
                        message,
                        new object[0]
                    });
            }
        }
    }

    internal sealed class ReferenceComparer :
        IEqualityComparer<object>
    {
        public new bool Equals(
            object x,
            object y)
        {
            return System.Object.ReferenceEquals(
                x,
                y);
        }

        public int GetHashCode(
            object obj)
        {
            return obj == null
                ? 0
                : RuntimeHelpers.GetHashCode(
                    obj);
        }
    }
}
