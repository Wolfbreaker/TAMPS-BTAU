using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TAMPS
{
    public static class Mod
    {
        private static string modDirectory;
        private static Logger logger;

        public static void Init(string directory, string settingsJSON)
        {
            modDirectory = directory;
            logger = new Logger(Path.Combine(modDirectory, "TAMPS.log"));

            try
            {
                Settings settings = Settings.Parse(settingsJSON, logger);
                logger.ConfigureRoutinePocketLogging(
                    settings.VerbosePocketLogging);

                string gameRoot = PathResolver.FindGameRoot(modDirectory);

                logger.Write("TAMPS v1.0.7 portable allow-list paths");
                logger.Write("Mod directory: " + modDirectory);
                logger.Write("Game root: " + gameRoot);
                logger.Write("Triad Mechanism operational build. First protected AmmoBox explosion: full containment. Core outcome: 75% Damaged and functional, 25% Destroyed. A surviving core gives the second protected AmmoBox one passive 50% containment attempt.");

                Stopwatch stopwatch = Stopwatch.StartNew();

                DiscoveryResult result = AmmoBoxDiscovery.Discover(
                    Path.Combine(gameRoot, "BattleTech_Data"),
                    Path.Combine(gameRoot, "Mods"),
                    settings,
                    logger);

                stopwatch.Stop();

                logger.Write(
                    "Discovery completed in " +
                    stopwatch.ElapsedMilliseconds +
                    " ms. Unique AmmoBox IDs: " +
                    result.UniqueIds.Count +
                    "; JSON files inspected: " +
                    result.FilesInspected +
                    "; accepted definitions: " +
                    result.AcceptedDefinitions +
                    "; duplicate IDs: " +
                    result.DuplicateIds.Count +
                    "; parse/read errors: " +
                    result.Errors);

                if (settings.WriteAllowList)
                {
                    string outputPath = Path.Combine(
                        modDirectory,
                        "AmmoBoxAllowList.json");

                    result.WriteJson(outputPath);
                    logger.Write("Allow-list written to: " + outputPath);
                }

                PocketRuntime.Initialize(
                    result.UniqueIds,
                    settings,
                    logger);

                TriadMechanism.Configure(
                    logger,
                    settings.EnableTriadMechanism,
                    settings.TriadFirstCoreSurvivalChance,
                    settings.TriadSecondAbsorptionChance);
TriadMechanism.TryInstall();
}
            catch (Exception exception)
            {
                if (logger != null)
                {
                    logger.Write("FATAL: " + exception);
                }
            }
        }
    }

    internal sealed class Settings
    {
        public bool WriteAllowList = false;
        public bool EnableTriadMechanism = true;
        public double TriadFirstCoreSurvivalChance = 0.75;
        public double TriadSecondAbsorptionChance = 0.50;
        public bool EnableSlotVirtualization = true;
        public bool VerbosePocketLogging = false;
        public bool VerboseFileLogging = false;
        public bool SkipModTekCache = true;
        public bool EnableCollapsiblePocketUI = true;
        public bool PocketUICollapsedByDefault = false;

        public static Settings Parse(string json, Logger logger)
        {
            Settings settings = new Settings();

            if (String.IsNullOrWhiteSpace(json))
            {
                return settings;
            }

            try
            {
                JObject root = JObject.Parse(json);
                JToken settingsToken = root["Settings"];
                JObject data = settingsToken as JObject ?? root;

                JToken write = data["WriteAllowList"];
                JToken triadMechanism = data["EnableTriadMechanism"];
                JToken firstCoreSurvival = data["TriadFirstCoreSurvivalChance"];
                JToken secondAbsorption = data["TriadSecondAbsorptionChance"];
                JToken enableSlots = data["EnableSlotVirtualization"];
                JToken verbosePockets = data["VerbosePocketLogging"];
                JToken verbose = data["VerboseFileLogging"];
                JToken skipCache = data["SkipModTekCache"];
                JToken collapsibleUi = data["EnableCollapsiblePocketUI"];
                JToken collapsedDefault = data["PocketUICollapsedByDefault"];

                if (write != null) settings.WriteAllowList = write.Value<bool>();
                if (triadMechanism != null) settings.EnableTriadMechanism = triadMechanism.Value<bool>();
                if (firstCoreSurvival != null) settings.TriadFirstCoreSurvivalChance = firstCoreSurvival.Value<double>();
                if (secondAbsorption != null) settings.TriadSecondAbsorptionChance = secondAbsorption.Value<double>();
                if (enableSlots != null) settings.EnableSlotVirtualization = enableSlots.Value<bool>();
                if (verbosePockets != null) settings.VerbosePocketLogging = verbosePockets.Value<bool>();
                if (verbose != null) settings.VerboseFileLogging = verbose.Value<bool>();
                if (skipCache != null) settings.SkipModTekCache = skipCache.Value<bool>();
                if (collapsibleUi != null) settings.EnableCollapsiblePocketUI = collapsibleUi.Value<bool>();
                if (collapsedDefault != null) settings.PocketUICollapsedByDefault = collapsedDefault.Value<bool>();
            }
            catch (Exception exception)
            {
                logger.Write(
                    "Settings JSON could not be parsed; defaults will be used. " +
                    exception.Message);
            }

            return settings;
        }
    }

    internal static class PathResolver
    {
        public static string FindGameRoot(string modDirectory)
        {
            DirectoryInfo current = new DirectoryInfo(modDirectory);

            while (current != null)
            {
                if (String.Equals(
                    current.Name,
                    "Mods",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (current.Parent == null)
                    {
                        break;
                    }

                    return current.Parent.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the BATTLETECH game root above the mod directory.");
        }
    }

    internal sealed class AmmoBoxRecord
    {
        public string Id;
        public string File;
        public string Root;
        public string ComponentType;
        public int? InventorySize;
        public double? Tonnage;
    }

    internal sealed class DiscoveryResult
    {
        public readonly HashSet<string> UniqueIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public readonly List<AmmoBoxRecord> Records =
            new List<AmmoBoxRecord>();

        public readonly Dictionary<string, List<string>> DuplicateIds =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        public int FilesInspected;
        public int AcceptedDefinitions;
        public int Errors;
        
        public void Add(AmmoBoxRecord record)
        {
            AcceptedDefinitions++;
            Records.Add(record);

            if (!UniqueIds.Add(record.Id))
            {
                List<string> files;
                if (!DuplicateIds.TryGetValue(record.Id, out files))
                {
                    files = new List<string>();
                    DuplicateIds.Add(record.Id, files);

                    for (int i = 0; i < Records.Count - 1; i++)
                    {
                        if (String.Equals(
                            Records[i].Id,
                            record.Id,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            files.Add(Records[i].File);
                            break;
                        }
                    }
                }

                files.Add(record.File);
            }
        }

        public void WriteJson(string path)
        {
            JObject root = new JObject();
            root["GeneratedUtc"] = DateTime.UtcNow.ToString("o");
            root["UniqueAmmoBoxCount"] = UniqueIds.Count;
            root["FilesInspected"] = FilesInspected;
            root["AcceptedDefinitions"] = AcceptedDefinitions;
            root["Errors"] = Errors;

            JArray ids = new JArray();
            List<string> sortedIds = new List<string>(UniqueIds);
            sortedIds.Sort(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < sortedIds.Count; i++)
            {
                ids.Add(sortedIds[i]);
            }

            root["AllowedIds"] = ids;

            JArray records = new JArray();
            for (int i = 0; i < Records.Count; i++)
            {
                AmmoBoxRecord record = Records[i];
                JObject item = new JObject();
                item["Id"] = record.Id;
                item["File"] = record.File;
                item["Root"] = record.Root;
                item["ComponentType"] = record.ComponentType;

                if (record.InventorySize.HasValue)
                {
                    item["InventorySize"] = record.InventorySize.Value;
                }

                if (record.Tonnage.HasValue)
                {
                    item["Tonnage"] = record.Tonnage.Value;
                }

                records.Add(item);
            }

            root["Definitions"] = records;

            JObject duplicates = new JObject();
            foreach (KeyValuePair<string, List<string>> pair in DuplicateIds)
            {
                JArray files = new JArray();
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    files.Add(pair.Value[i]);
                }

                duplicates[pair.Key] = files;
            }

            root["DuplicateIds"] = duplicates;

            File.WriteAllText(
                path,
                root.ToString(Formatting.Indented));
        }
    }

    internal static class AmmoBoxDiscovery
    {
        private static readonly HashSet<string> ProcessedFiles =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static DiscoveryResult Discover(
            string battleTechDataRoot,
            string modsRoot,
            Settings settings,
            Logger logger)
        {
            DiscoveryResult result = new DiscoveryResult();
            ProcessedFiles.Clear();

            logger.Write("Scanning root 1: " + battleTechDataRoot);
            ScanTree(
                battleTechDataRoot,
                "BattleTech_Data",
                settings,
                logger,
                result);

            logger.Write("Scanning root 2: " + modsRoot);
            ScanTree(
                modsRoot,
                "Mods",
                settings,
                logger,
                result);

            return result;
        }

        private static void ScanTree(
            string root,
            string rootLabel,
            Settings settings,
            Logger logger,
            DiscoveryResult result)
        {
            if (!Directory.Exists(root))
            {
                logger.Write("Search root does not exist: " + root);
                return;
            }

            Stack<string> pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                string current = pending.Pop();
                string directoryName = Path.GetFileName(current);

                if (settings.SkipModTekCache &&
                    String.Equals(
                        directoryName,
                        ".modtek",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (settings.VerboseFileLogging)
                    {
                        logger.Write("Skipped ModTek cache: " + current);
                    }

                    continue;
                }

                if (IsAmmoBoxDirectory(directoryName))
                {
                    ScanAmmoBoxDirectory(
                        current,
                        root,
                        rootLabel,
                        settings,
                        logger,
                        result);

                    // ScanAmmoBoxDirectory already handles all subdirectories.
                    continue;
                }

                try
                {
                    string[] childDirectories =
                        Directory.GetDirectories(current);

                    for (int i = 0; i < childDirectories.Length; i++)
                    {
                        string child = childDirectories[i];

                        try
                        {
                            FileAttributes attributes =
                                File.GetAttributes(child);

                            if ((attributes & FileAttributes.ReparsePoint) != 0)
                            {
                                if (settings.VerboseFileLogging)
                                {
                                    logger.Write(
                                        "Skipped reparse point: " +
                                        child);
                                }

                                continue;
                            }
                        }
                        catch
                        {
                            // If attributes cannot be read, directory enumeration
                            // below will handle and log any actual access problem.
                        }

                        pending.Push(child);
                    }
                }
                catch (Exception exception)
                {
                    result.Errors++;
                    logger.Write(
                        "Could not enumerate directory '" +
                        current +
                        "': " +
                        exception.Message);
                }
            }
        }

        private static bool IsAmmoBoxDirectory(string name)
        {
            return String.Equals(
                       name,
                       "ammobox",
                       StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(
                       name,
                       "ammunitionbox",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static void ScanAmmoBoxDirectory(
            string directory,
            string rootPath,
            string rootLabel,
            Settings settings,
            Logger logger,
            DiscoveryResult result)
        {
            if (settings.VerboseFileLogging)
            {
                logger.Write("Accepted AmmoBox directory: " + directory);
            }

            string[] files;

            try
            {
                files = Directory.GetFiles(
                    directory,
                    "*.json",
                    SearchOption.AllDirectories);
            }
            catch (Exception exception)
            {
                result.Errors++;
                logger.Write(
                    "Could not scan AmmoBox directory '" +
                    directory +
                    "': " +
                    exception.Message);
                return;
            }

            for (int i = 0; i < files.Length; i++)
            {
                string fullPath;

                try
                {
                    fullPath = Path.GetFullPath(files[i]);
                }
                catch
                {
                    fullPath = files[i];
                }

                if (!ProcessedFiles.Add(fullPath))
                {
                    continue;
                }

                InspectFile(
                    fullPath,
                    rootPath,
                    rootLabel,
                    settings,
                    logger,
                    result);
            }
        }

        private static void InspectFile(
            string file,
            string rootPath,
            string rootLabel,
            Settings settings,
            Logger logger,
            DiscoveryResult result)
        {
            result.FilesInspected++;

            try
            {
                JObject json = JObject.Parse(File.ReadAllText(file));

                string id = null;
                JObject description = json["Description"] as JObject;

                if (description != null && description["Id"] != null)
                {
                    id = description["Id"].Value<string>();
                }

                if (String.IsNullOrWhiteSpace(id))
                {
                    result.Errors++;
                    logger.Write(
                        "Skipped JSON without Description.Id: " +
                        file);
                    return;
                }

                string componentType = "";
                if (json["ComponentType"] != null)
                {
                    componentType =
                        json["ComponentType"].Value<string>() ?? "";
                }

                if (!String.Equals(
                        componentType,
                        "AmmunitionBox",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (settings.VerboseFileLogging)
                    {
                        logger.Write(
                            "Skipped non-AmmunitionBox JSON: " +
                            file);
                    }

                    return;
                }

                if (String.Equals(
                        id,
                        "AmmoBoxTemplate",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (settings.VerboseFileLogging)
                    {
                        logger.Write(
                            "Skipped AmmoBox template: " +
                            file);
                    }

                    return;
                }

                int? inventorySize = null;
                if (json["InventorySize"] != null)
                {
                    inventorySize = json["InventorySize"].Value<int>();
                }

                double? tonnage = null;
                if (json["Tonnage"] != null)
                {
                    tonnage = json["Tonnage"].Value<double>();
                }

                AmmoBoxRecord record = new AmmoBoxRecord();
                record.Id = id;
                record.File = MakePortablePath(rootPath, rootLabel, file);
                record.Root = rootLabel;
                record.ComponentType = componentType;
                record.InventorySize = inventorySize;
                record.Tonnage = tonnage;

                result.Add(record);

                if (settings.VerboseFileLogging)
                {
                    logger.Write(
                        "Accepted AmmoBox ID: " +
                        id +
                        " from " +
                        file);
                }
            }
            catch (Exception exception)
            {
                result.Errors++;
                logger.Write(
                    "Failed to inspect JSON '" +
                    file +
                    "': " +
                    exception.Message);
            }
        }

        private static string MakePortablePath(
            string rootPath,
            string rootLabel,
            string file)
        {
            try
            {
                string normalizedRoot = Path.GetFullPath(rootPath)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);
                string normalizedFile = Path.GetFullPath(file);
                string prefix = normalizedRoot + Path.DirectorySeparatorChar;

                if (normalizedFile.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    string relative = normalizedFile
                        .Substring(prefix.Length)
                        .Replace('\\', '/');

                    return rootLabel + "/" + relative;
                }
            }
            catch
            {
                // Fall through to a portable file-name-only value.
            }

            return rootLabel + "/" + Path.GetFileName(file);
        }

    }


    internal sealed class Logger
    {
        private static readonly string[] RoutinePocketPrefixes =
        {
            "Native pocket accounting [",
            "Pocket assignment [",
            "Collapsible pocket UI:",
            "Collapsible pocket UI click:",
            "Collapsible pocket UI restored ",
            "Pocket manual reflow [",
            "Unified pocket inventory order [",
            "Side module pooled visual reset ["
        };

        private readonly string path;
        private readonly object sync = new object();
        private bool routinePocketLoggingEnabled;

        public Logger(string path)
        {
            this.path = path;

            try
            {
                File.WriteAllText(
                    this.path,
                    "Triad Ablative Magazine Protection System log started " +
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                    Environment.NewLine);
            }
            catch
            {
                // The mod must not crash the game because logging failed.
            }
        }

        public void ConfigureRoutinePocketLogging(
            bool enabled)
        {
            routinePocketLoggingEnabled =
                enabled;
        }

        public void Write(string message)
        {
            if (!routinePocketLoggingEnabled &&
                IsRoutinePocketMessage(
                    message))
            {
                return;
            }

            string line =
                "[" +
                DateTime.Now.ToString("HH:mm:ss.fff") +
                "] " +
                message;

            lock (sync)
            {
                try
                {
                    File.AppendAllText(
                        path,
                        line + Environment.NewLine);
                }
                catch
                {
                    // The mod must not crash the game because logging failed.
                }
            }
        }

        private static bool IsRoutinePocketMessage(
            string message)
        {
            if (String.IsNullOrEmpty(
                    message))
            {
                return false;
            }

            for (int i = 0;
                 i < RoutinePocketPrefixes.Length;
                 i++)
            {
                if (message.StartsWith(
                        RoutinePocketPrefixes[i],
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
