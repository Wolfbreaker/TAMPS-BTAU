using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace TAMPS
{
    internal static class TriadMechanism
    {
        private const string HarmonyId =
            "com.nightsentinels.tamps.triadmechanism";

        private static readonly object Sync =
            new object();

        private static readonly object RandomSync =
            new object();

        private static readonly ConditionalWeakTable<object, TriadPocketState> PocketStates =
            new ConditionalWeakTable<object, TriadPocketState>();

        private static readonly Random Random =
            new Random();

        private static Logger logger;
        private static bool enabled;
        private static bool installed;
        private static double firstCoreSurvivalChance;
        private static double secondAbsorptionChance;
        private static Harmony harmony;

        internal static void Configure(
            Logger loadedLogger,
            bool mechanismEnabled,
            double loadedFirstCoreSurvivalChance,
            double loadedSecondAbsorptionChance)
        {
            logger =
                loadedLogger;

            enabled =
                mechanismEnabled;

            firstCoreSurvivalChance =
                ClampChance(
                    loadedFirstCoreSurvivalChance);

            secondAbsorptionChance =
                ClampChance(
                    loadedSecondAbsorptionChance);
        }

        internal static bool TryInstall()
        {
            lock (Sync)
            {
                if (!enabled)
                {
                    Log(
                        "Triad mechanism is disabled in settings.");

                    return false;
                }

                if (installed)
                {
                    return true;
                }

                try
                {
                    Type ammoBoxType =
                        AccessTools.TypeByName(
                            "BattleTech.AmmunitionBox");

                    if (ammoBoxType == null)
                    {
                        Log(
                            "Triad mechanism installation failed: BattleTech.AmmunitionBox was not loaded.");

                        return false;
                    }

                    MethodInfo damageComponent =
                        FindMethod(
                            ammoBoxType,
                            "DamageComponent",
                            3);

                    harmony =
                        new Harmony(
                            HarmonyId);

                    harmony.Patch(
                        damageComponent,
                        prefix: new HarmonyMethod(
                            typeof(TriadMechanismPatches),
                            "AmmunitionBox_DamageComponent_Prefix"));

                    installed =
                        true;

                    Log(
                        "Triad mechanism installed on BattleTech.AmmunitionBox.DamageComponent. " +
                        "First protected AmmoBox explosion is absorbed; the Pocket then has a " +
                        FormatChance(firstCoreSurvivalChance) +
                        " chance to remain functional in Damaged condition. If it survives, " +
                        "the second protected AmmoBox explosion has a " +
                        FormatChance(secondAbsorptionChance) +
                        " absorption chance. The Pocket is destroyed after the second attempt.");

                    return true;
                }
                catch (Exception exception)
                {
                    Log(
                        "Triad mechanism installation failed: " +
                        exception);

                    return false;
                }
            }
        }

        internal static void BeforeAmmoDamage(
            object ammoBox,
            object hitInfo,
            object requestedDamageLevel,
            bool applyEffects)
        {
            try
            {
                if (!enabled ||
                    ammoBox == null ||
                    !IsDestroyedRequest(
                        requestedDamageLevel))
                {
                    return;
                }

                int currentAmmo =
                    ReflectionValue.ToInt32(
                        ReflectionValue.Get(
                            ammoBox,
                            "CurrentAmmo"));

                if (currentAmmo <= 0)
                {
                    return;
                }

                object actor =
                    ReflectionValue.Get(
                        ammoBox,
                        "parent");

                TriadCandidate candidate =
                    TriadCandidate.Evaluate(
                        actor,
                        ammoBox);

                if (!candidate.IsProtectedCandidate ||
                    candidate.Pocket == null)
                {
                    return;
                }

                TriadPocketState state =
                    GetPocketState(
                        candidate.Pocket);

                if (state.Attempts >= 2)
                {
                    Log(
                        "TRIAD: no protection remains | actor={" +
                        DescribeActor(
                            candidate.Actor) +
                        "} | ammo={" +
                        DescribeComponent(
                            ammoBox) +
                        "} | pocket={" +
                        DescribeComponent(
                            candidate.Pocket) +
                        "} | attempts=" +
                        state.Attempts);

                    return;
                }

                if (state.Attempts == 0)
                {
                    HandleFirstExplosion(
                        state,
                        candidate,
                        ammoBox,
                        hitInfo,
                        applyEffects,
                        currentAmmo);

                    return;
                }

                HandleSecondExplosion(
                    state,
                    candidate,
                    ammoBox,
                    hitInfo,
                    applyEffects,
                    currentAmmo);
            }
            catch (Exception exception)
            {
                Log(
                    "Triad mechanism prefix failed; normal ammo behavior will continue. " +
                    exception);
            }
        }

        private static void HandleFirstExplosion(
            TriadPocketState state,
            TriadCandidate candidate,
            object ammoBox,
            object hitInfo,
            bool applyEffects,
            int originalAmmo)
        {
            if (!TrySuppressExplosion(
                    candidate.Actor,
                    ammoBox,
                    originalAmmo))
            {
                Log(
                    "TRIAD FIRST: could not set CurrentAmmo to zero; protection was not consumed and normal explosion continues | actor={" +
                    DescribeActor(
                        candidate.Actor) +
                    "} | ammo={" +
                    DescribeComponent(
                        ammoBox) +
                    "}");

                return;
            }

            state.Attempts =
                1;

            state.FirstAmmo =
                ammoBox;

            double roll =
                NextRoll();

            bool coreSurvives =
                roll <
                firstCoreSurvivalChance;

            string pocketOutcome;

            if (coreSurvives)
            {
                bool damaged =
                    DamagePocket(
                        candidate.Actor,
                        candidate.Pocket,
                        hitInfo,
                        "Penalized",
                        applyEffects);

                pocketOutcome =
                    damaged
                        ? "Damaged and still functional"
                        : "Damaged-state damage call failed";
            }
            else
            {
                // The first containment succeeded, but the shared core did not
                // survive its 75% durability roll. Protection must be exhausted
                // immediately even if another mod blocks the visual component
                // state transition.
                state.Attempts =
                    2;

                bool destroyed =
                    DamagePocket(
                        candidate.Actor,
                        candidate.Pocket,
                        hitInfo,
                        "Destroyed",
                        applyEffects);

                pocketOutcome =
                    destroyed
                        ? "Destroyed"
                        : "Protection exhausted; Pocket destruction state write failed";
            }

            Log(
                "TRIAD FIRST: explosion absorbed | actor={" +
                DescribeActor(
                    candidate.Actor) +
                "} | ammo={" +
                DescribeComponent(
                    ammoBox) +
                "} | originalAmmo=" +
                originalAmmo +
                " | pocket={" +
                DescribeComponent(
                    candidate.Pocket) +
                "} | coreSurvivalRoll=" +
                roll.ToString("0.000000") +
                " | survivalChance=" +
                FormatChance(
                    firstCoreSurvivalChance) +
                " | coreSurvived=" +
                coreSurvives +
                " | pocketOutcome=" +
                pocketOutcome +
                " | remaining intact AmmoBoxes are not modified and remain usable.");
        }

        private static void HandleSecondExplosion(
            TriadPocketState state,
            TriadCandidate candidate,
            object ammoBox,
            object hitInfo,
            bool applyEffects,
            int originalAmmo)
        {
            state.Attempts =
                2;

            state.SecondAmmo =
                ammoBox;

            double roll =
                NextRoll();

            bool absorbed =
                roll <
                secondAbsorptionChance;

            bool suppressionSucceeded =
                false;

            if (absorbed)
            {
                suppressionSucceeded =
                    TrySuppressExplosion(
                        candidate.Actor,
                        ammoBox,
                        originalAmmo);

                if (!suppressionSucceeded)
                {
                    absorbed =
                        false;
                }
            }

            bool pocketDestroyed =
                DamagePocket(
                    candidate.Actor,
                    candidate.Pocket,
                    hitInfo,
                    "Destroyed",
                    applyEffects);

            Log(
                "TRIAD SECOND: passive containment roll resolved | actor={" +
                DescribeActor(
                    candidate.Actor) +
                "} | ammo={" +
                DescribeComponent(
                    ammoBox) +
                "} | originalAmmo=" +
                originalAmmo +
                " | pocket={" +
                DescribeComponent(
                    candidate.Pocket) +
                "} | absorptionRoll=" +
                roll.ToString("0.000000") +
                " | absorptionChance=" +
                FormatChance(
                    secondAbsorptionChance) +
                " | absorbed=" +
                absorbed +
                " | ammoZeroed=" +
                suppressionSucceeded +
                " | pocketDestroyedVerified=" +
                pocketDestroyed +
                " | result=" +
                (absorbed
                    ? "internal explosion suppressed"
                    : "normal ammo explosion allowed") +
                ". Any other AmmoBox that did not explode is left untouched and remains usable.");
        }

        private static bool TrySuppressExplosion(
            object actor,
            object ammoBox,
            int originalAmmo)
        {
            string strategy;
            string diagnostics;

            bool set =
                AmmoRuntimeState.TrySetCurrentAmmo(
                    ammoBox,
                    0,
                    out strategy,
                    out diagnostics);

            int after =
                ReflectionValue.ToInt32(
                    ReflectionValue.Get(
                        ammoBox,
                        "CurrentAmmo"));

            bool succeeded =
                set &&
                after == 0;

            Log(
                "TRIAD containment write | actor={" +
                DescribeActor(
                    actor) +
                "} | ammo={" +
                DescribeComponent(
                    ammoBox) +
                "} | originalAmmo=" +
                originalAmmo +
                " | strategy=" +
                strategy +
                " | currentAmmoAfter=" +
                after +
                " | succeeded=" +
                succeeded +
                (succeeded
                    ? ""
                    : " | diagnostics=" +
                      diagnostics));

            return succeeded;
        }

        private static bool DamagePocket(
            object actor,
            object pocket,
            object hitInfo,
            string damageLevelName,
            bool applyEffects)
        {
            if (pocket == null)
            {
                return false;
            }

            string strategy;
            string diagnostics;

            bool applied =
                PocketRuntimeState.TryApplyDamageLevel(
                    pocket,
                    hitInfo,
                    damageLevelName,
                    applyEffects,
                    out strategy,
                    out diagnostics);

            Log(
                "TRIAD pocket damage result | actor={" +
                DescribeActor(
                    actor) +
                "} | requested=" +
                DisplayDamageLevel(
                    damageLevelName) +
                " | applied=" +
                applied +
                " | strategy=" +
                strategy +
                " | pocketAfter={" +
                DescribeComponent(
                    pocket) +
                "}" +
                (String.IsNullOrEmpty(
                     diagnostics)
                    ? ""
                    : " | diagnostics=" +
                      diagnostics));

            return applied;
        }

        private static TriadPocketState GetPocketState(
            object pocket)
        {
            if (pocket == null)
            {
                throw new ArgumentNullException(
                    "pocket");
            }

            return PocketStates.GetValue(
                pocket,
                delegate(object ignored)
                {
                    return new TriadPocketState();
                });
        }

        private static bool IsDestroyedRequest(
            object damageLevel)
        {
            return String.Equals(
                ReflectionValue.ToText(
                    damageLevel),
                "Destroyed",
                StringComparison.OrdinalIgnoreCase);
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

            throw new MissingMethodException(
                type.FullName +
                "." +
                name +
                "(" +
                parameterCount +
                ")");
        }

        private static string DisplayDamageLevel(
            string engineDamageLevel)
        {
            return String.Equals(
                       engineDamageLevel,
                       "Penalized",
                       StringComparison.OrdinalIgnoreCase)
                ? "Damaged"
                : engineDamageLevel;
        }

        private static double NextRoll()
        {
            lock (RandomSync)
            {
                return Random.NextDouble();
            }
        }

        private static double ClampChance(
            double value)
        {
            if (value < 0.0)
            {
                return 0.0;
            }

            if (value > 1.0)
            {
                return 1.0;
            }

            return value;
        }

        private static string FormatChance(
            double value)
        {
            return (value * 100.0).ToString("0.##") +
                   "%";
        }

        private static string DescribeActor(
            object actor)
        {
            if (actor == null)
            {
                return "null";
            }

            string unitName =
                ReflectionValue.ToText(
                    ReflectionValue.Get(
                        actor,
                        "UnitName"));

            string displayName =
                ReflectionValue.ToText(
                    ReflectionValue.Get(
                        actor,
                        "DisplayName"));

            string mechDefId =
                ReflectionValue.ToText(
                    ReflectionValue.Get(
                        ReflectionValue.Get(
                            actor,
                            "MechDef"),
                        "Description"));

            object mechDef =
                ReflectionValue.Get(
                    actor,
                    "MechDef");

            object mechDescription =
                ReflectionValue.Get(
                    mechDef,
                    "Description");

            mechDefId =
                ReflectionValue.ToText(
                    ReflectionValue.Get(
                        mechDescription,
                        "Id"));

            return "type=" +
                   actor.GetType().FullName +
                   ", GUID=" +
                   ReflectionValue.ToText(
                       ReflectionValue.Get(
                           actor,
                           "GUID")) +
                   ", UnitName=" +
                   unitName +
                   ", DisplayName=" +
                   displayName +
                   ", MechDefId=" +
                   mechDefId;
        }

        private static string DescribeComponent(
            object component)
        {
            if (component == null)
            {
                return "null";
            }

            return "type=" +
                   component.GetType().FullName +
                   ", defId=" +
                   ComponentIdentity.GetDefId(
                       component) +
                   ", GUID=" +
                   ReflectionValue.ToText(
                       ReflectionValue.Get(
                           component,
                           "GUID")) +
                   ", location=" +
                   ReflectionValue.ToText(
                       ReflectionValue.Get(
                           component,
                           "Location")) +
                   ", damageLevel=" +
                   ReflectionValue.ToText(
                       ReflectionValue.Get(
                           component,
                           "DamageLevel")) +
                   ", functional=" +
                   ReflectionValue.ToText(
                       ReflectionValue.Get(
                           component,
                           "IsFunctional")) +
                   ", currentAmmo=" +
                   ReflectionValue.ToText(
                       ReflectionValue.Get(
                           component,
                           "CurrentAmmo"));
        }

        private static void Log(
            string message)
        {
            if (logger != null)
            {
                logger.Write(
                    message);
            }
        }

        private sealed class TriadPocketState
        {
            internal int Attempts;
            internal object FirstAmmo;
            internal object SecondAmmo;
        }
    }

    internal static class TriadMechanismPatches
    {
        public static void AmmunitionBox_DamageComponent_Prefix(
            object __instance,
            object __0,
            object __1,
            bool __2)
        {
            TriadMechanism.BeforeAmmoDamage(
                __instance,
                __0,
                __1,
                __2);
        }
    }


}
