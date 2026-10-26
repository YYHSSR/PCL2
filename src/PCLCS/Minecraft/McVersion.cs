using System.Globalization;

namespace PCLCS;

/// <summary>
/// Minecraft version number and component info
/// </summary>
public sealed class McVersion {
    /// <summary>
    /// Vanilla version name, such as 26.1, 26.1-snapshot-1, 1.12.2, 16w01a.
    /// </summary>
    public string VanillaName {
        get => _vanillaName;
        set {
            _vanillaName = value ?? "Unknown";
            Vanilla = NameToVersion(_vanillaName);
        }
    }
    private string _vanillaName = "Unknown";
    /// <summary>
    /// Comparable three-part vanilla version number.
    /// The old format moves the minor version into the major version, e.g. 1.20.1 becomes 20.0.1; without a version number, such as old snapshots, it is 9999.0.0.
    /// </summary>
    public Version Vanilla = new(9999, 0, 0);
    /// <summary> Whether the vanilla version number is reliable (not guessed).</summary>
    public bool Reliable;
    /// <summary> Whether the vanilla version number is valid.</summary>
    public bool Valid => Vanilla.Major < 1000;
    /// <summary> Comparable vanilla Drop ordinal, e.g. 263 for 26.3.2 and 210 for 1.21.5; without a version number, such as old snapshots, it is set to 209.</summary>
    public int Drop => Valid ? Vanilla.Major * 10 + Vanilla.Minor : 209;
    /// <summary> Installed mod loaders; there may be several. <see cref="ModLoaders.None"/> if none is installed.</summary>
    public ModLoaders ModLoaders => ModLoaderVersions.Keys.Aggregate(ModLoaders.None, (result, loader) => result | loader);
    /// <summary> Installed mod loaders and their versions; "未知版本" when the exact version cannot be determined.</summary>
    public Dictionary<ModLoaders, string> ModLoaderVersions { get; } = [];
    /// <summary> OptiFine version name, such as C8 or C9_pre10; null if not detected.</summary>
    public string? OptiFine;
    /// <summary> Whether OptiFine is installed as a mod.</summary>
    public bool OptiFineAsMod;
    /// <summary> Release time of the version.</summary>
    public DateTime ReleaseTime = new(1970, 1, 1, 15, 0, 0);
    /// <summary> Whether this is an April Fools version.</summary>
    public bool IsFool;
    /// <summary> Whether this is a snapshot, pre-release, release candidate or experimental version.</summary>
    public bool IsSnapshot;
    /// <summary> Whether an installed OptiFine was detected.</summary>
    public bool HasOptiFine => OptiFine is not null;
    /// <summary>
    /// Comparable OptiFine version ordinal.
    /// </summary>
    public int OptiFineCode {
        get {
            if (OptiFine is null or "未知版本" || OptiFine.Length == 0) return 0;
            // Letter code, e.g. G (7) in G2
            int result = char.ToUpperInvariant(OptiFine[0]) - 'A' + 1;
            // Trailing number, e.g. 5 in C5 beta4
            result = result * 100 + (int.TryParse(OptiFine.Substring(1).RegexSeek(@"\d+"), out int mainVersion) ? mainVersion : 0);
            // Preview marker (99 for releases, 50+x for Pre[x], x for Beta[x])
            result *= 100;
            string? preview = OptiFine.RegexSeek(@"(?i)(pre|beta)\d*");
            if (preview is null) return result + 99;
            int sequence = int.TryParse(preview.RegexSeek(@"\d+"), out int value) ? value : 1; // Treat a bare pre or beta suffix as 1
            return result + (preview.StartsWithF("pre", true) ? 50 : 0) + sequence;
        }
    }
    /// <summary>
    /// Comparable Forge-like version ordinal.
    /// </summary>
    public int ForgelikeCode {
        get {
            if (!ModLoaderVersions.TryGetValue(ModLoaders.Forge, out string? version)) {
                ModLoaderVersions.TryGetValue(ModLoaders.NeoForge, out version);
            }
            if (version is null or "未知版本") return 0;
            // Extract all numeric segments to support non-numeric suffixes such as beta, alpha and snapshot.
            int[] segments = version.RegexSearch(@"\d+")
                .Select(segment => int.TryParse(segment, out int value) ? value : 0).ToArray();
            return segments.Length switch {
                >= 4 => segments[0] * 1000000 + segments[1] * 10000 + segments[3],
                3 => segments[0] * 1000000 + segments[1] * 10000 + segments[2],
                2 => segments[0] * 1000000 + segments[1] * 10000,
                1 => segments[0] * 1000000,
                _ => 0
            };
        }
    }

    /// <summary>
    /// Creates a default Minecraft version.
    /// </summary>
    /// <param name="vanillaName">Vanilla version name.</param>
    /// <param name="reliable">Whether the version name comes from a reliable source.</param>
    public McVersion(string vanillaName, bool reliable = true) {
        Reliable = reliable;
        VanillaName = vanillaName;
        IsFool = IsFoolName(VanillaName);
        IsSnapshot = IsSnapshotName(VanillaName);
    }

    /// <summary>
    /// Creates Minecraft version info from Components.
    /// </summary>
    public McVersion(McComponents components) : this(components.MinecraftVersion) {
        OptiFine = components.OptiFineVersion?.AfterLast("HD_").AfterFirst("_");
        foreach (var loader in components.ModLoaderVersions) ModLoaderVersions[loader.Key] = loader.Value;
    }

    /// <summary>
    /// Detects the Minecraft version and installed components from JSON.
    /// </summary>
    /// <param name="instanceName">Instance folder name.</param>
    /// <param name="json">Expanded version JSON.</param>
    /// <param name="jarVersionJson">version.json inside the version JAR.</param>
    public McVersion(string instanceName, JObject json, JObject? jarVersionJson = null) {
        string jsonText = json.ToString(Formatting.None);
        DateTime releaseTime = new(1970, 1, 1, 15, 0, 0); // Unknown versions may also show as 1970
        try {
            if (json["releaseTime"] is JToken releaseTimeToken) releaseTime = releaseTimeToken.ToObject<DateTime>();
        } catch {
        }
        bool reliable = true;
        // Get the game version
        string vanillaName = (GetVanillaName(instanceName, json, jarVersionJson, releaseTime, ref reliable) ?? "Unknown").Replace("_unobfuscated", "").Replace(" Unobfuscated", "");
        Reliable = reliable;
        VanillaName = vanillaName;
        ReleaseTime = releaseTime;
        // Determine the version category
        string jsonType = json.Value<string>("type") ?? "";
        // OptiFine
        OptiFine = jsonText.ContainsIgnoreCase("optifine") ? jsonText.RegexSeek(@"(?<=HD_U_)[^\"":/]+") ?? "未知版本": null;
        // April Fools and snapshot versions
        DateTime fixedReleaseTime = releaseTime.ToUniversalTime().AddHours(2);
        IsFool = jsonType == "fool" || IsFoolName(vanillaName) || (fixedReleaseTime.Month == 4 && fixedReleaseTime.Day == 1 && jsonType == "snapshot");
        IsSnapshot = IsSnapshotName(vanillaName) || instanceName.ContainsIgnoreCase("combat") || jsonType is "snapshot" or "pending" or "fool";
        if (!Reliable) Logger.Info($"无法完全确认 MC 版本号的版本：{VanillaName}");
        // LiteLoader
        if (jsonText.ContainsIgnoreCase("liteloader")) {
            ModLoaderVersions[ModLoaders.LiteLoader] = jsonText.RegexSeek(@"(?<=com\.mumfrey:liteloader:)[^\"":/]+") ?? "未知版本";
        }
        // Fabric、Forge、NeoForge
        if (jsonText.ContainsIgnoreCase("net.fabricmc:fabric-loader") || jsonText.ContainsIgnoreCase("org.quiltmc:quilt-loader")) {
            ModLoaderVersions[ModLoaders.Fabric] = jsonText.RegexSeek(@"(?<=(net\.fabricmc:fabric-loader:|org\.quiltmc:quilt-loader:))[0-9\.]+(\+build.[0-9]+)?")?
                                                        .Replace("+build", "") ?? "未知版本";
        } else if (jsonText.ContainsIgnoreCase("net.neoforge")) {
            // 1.20.1 JSON example: "--fml.forgeVersion", "47.1.99"
            // 1.20.2+ JSON example: "--fml.neoForgeVersion", "20.6.119-beta"
            ModLoaderVersions[ModLoaders.NeoForge] = FindArgumentValue(json, "--fml.neoForgeVersion") ?? FindArgumentValue(json, "--fml.forgeVersion") ?? "未知版本";
        } else if (jsonText.ContainsIgnoreCase("minecraftforge")) {
            ModLoaderVersions[ModLoaders.Forge] = jsonText.RegexSeek(@"(?<=forge:[0-9\.]+(?:_pre[0-9]*)?-)[0-9\.]+")
                                                    ?? jsonText.RegexSeek(@"(?<=net\.minecraftforge:minecraftforge:)[0-9\.]+")
                                                    ?? jsonText.RegexSeek(@"(?<=net\.minecraftforge:fmlloader:[0-9\.]+-)[0-9\.]+") ?? "未知版本";
        }
    }

    private static string? GetVanillaName(string instanceName, JObject json, JObject? jarVersionJson, DateTime releaseTime, ref bool reliable) {
        // Get the release time and determine whether it is an old version
        if (releaseTime.Year is > 2000 and < 2013) return "Old";
        // Experimental snapshot
        if (json.Value<string>("type") == "pending") return "pending";
        // Get the version number from version info downloaded by PCL
        if (json["clientVersion"] is JToken clientVersion) return clientVersion.ToString();
        // Get the version number from version info downloaded by HMCL
        if (json["patches"] is JArray patches) {
            JToken? gameVersion = patches.OfType<JObject>().FirstOrDefault(patch => patch.Value<string>("id") == "game")?["version"];
            if (gameVersion is not null) return gameVersion.ToString();
        }
        // Get the version number from Forge / NeoForge arguments
        if (FindArgumentValue(json, "--fml.mcVersion") is { Length: > 0 } fmlVersion) return fmlVersion;
        // Get the version number from the inherited version
        string inherit = json["inheritsFrom"]?.ToString() ?? "";
        if (inherit.Length > 0) {
            string jar = json["jar"]?.ToString() ?? ""; // LiteLoader prefers Jar
            return jar.Length > 0 ? jar : inherit;
        }
        // Get the version number from the download URL
        string? download = json["downloads"]?.ToString(Formatting.None).RegexSeek(@"(?<=launcher.mojang.com/mc/game/)[^/]*");
        if (download is not null) return download;
        // Get the version number from the Forge/OptiFine/Fabric version
        string librariesText = json["libraries"]!.ToString();
        string[] libraryPatterns = [
            @"(?<=net.minecraftforge:forge:)[0-9]{1,2}.[0-9+.]+",
            @"(?<=net.minecraftforge:fmlloader:)[0-9]{1,2}.[0-9+.]+",
            @"(?<=optifine:OptiFine:)[0-9]{1,2}.[0-9+.]+",
            @"(?<=((fabricmc)|(quiltmc)):intermediary:)[^\""]*"
        ];
        foreach (string pattern in libraryPatterns) {
            if (librariesText.RegexSeek(pattern) is string match) return match;
        }
        // Get the version number from the jar entry
        if (json["jar"] is JToken jarToken) return jarToken.ToString();
        // Get the version number from version.json in the jar file
        if (jarVersionJson?["name"] is JToken jarNameToken && jarNameToken.ToString() is { Length: < 32 } jarName) { // The wiki says this may be a hash, though I have never seen one
            Logger.Info($"从版本 jar 中的 version.json 获取到版本号：{jarName}");
            return jarName;
        }
        string VersionPattern = @"(([1-9][0-9]w[0-9]{2}[a-g])|((1|[2-9][0-9])\.[0-9]+(\.[0-9]+)?(-(pre|rc|snapshot-?)[1-9]*| Pre-Release( [1-9])?)?))(_unobfuscated)?";
        // Get it from the JSON ID
        if (json["id"]!.ToString().RegexSeek(VersionPattern, RegexOptions.IgnoreCase) is string idMatch) return idMatch;
        // Warning for an inexact version detection
        Logger.Info($"无法完全确认 MC 版本号的版本：{instanceName}");
        reliable = false;
        // Get it from the folder name
        if (instanceName.RegexSeek(VersionPattern, RegexOptions.IgnoreCase) is string folderMatch) return folderMatch;
        // Get it from version numbers appearing in the JSON
        JObject withoutLibraries = (JObject) json.DeepClone();
        withoutLibraries.Remove("libraries");
        return withoutLibraries.ToString(Formatting.None).RegexSeek(VersionPattern, RegexOptions.IgnoreCase);
    }

    private static bool IsFoolName(string name) {
        name = name.Lower();
        return name.StartsWithF("2.0") || name is "15w14a" or "1.rv-pre1" or
            "3d shareware v1.34" or "20w14∞" or "22w13oneblockatatime" or "23w13a_or_b" or
            "24w14potato" or "25w14craftmine" or "26w14a" || name.StartsWithF("20w14inf");
    }

    private static bool IsSnapshotName(string name) =>
        new[] { "w", "snapshot", "rc", "pre", "experimental", "-" }.Any(name.ContainsIgnoreCase);

    /// <summary>
    /// Reads the string value of the specified argument in arguments.
    /// </summary>
    private static string? FindArgumentValue(JObject json, string name) {
        JArray? arguments = json["arguments"]?["game"] as JArray;
        if (arguments is null) return null;
        for (int i = 0; i + 1 < arguments.Count; i++) {
            if (arguments[i]?.ToString() == name) return arguments[i + 1]?.ToString();
        }
        return null;
    }

    /// <summary>
    /// Whether the version string matches the vanilla Minecraft format, such as 1.x or 26.x.
    /// </summary>
    public static bool IsStandardFormat(string? version) {
        if (version is null) return false;
        if (Regex.IsMatch(version, @"^1\.\d")) return true;
        return double.TryParse(version.RegexSeek(@"^[2-9]\d\.\d+"), NumberStyles.Float,
            CultureInfo.InvariantCulture, out double value) && value >= 26;
    }

    /// <summary>
    /// Converts a version name to a Drop ordinal; returns 209 if it cannot be converted.
    /// </summary>
    public static int VersionToDrop(string? vanillaName) {
        if (vanillaName is null) return 209;
        Version version = NameToVersion(vanillaName);
        return version.Major >= 1000 ? 209 : version.Major * 10 + version.Minor;
    }

    /// <summary>
    /// Converts a Drop ordinal to a version string.
    /// </summary>
    public static string DropToVersion(int drop) {
        if (drop >= 250) return $"{drop / 10}.{drop % 10}";
        if (drop != 209) return $"1.{drop / 10}";
        Logger.Warn(new Exception("尝试将旧快照版 Drop 序数转换为版本字符串，这不应该发生！"));
        return "1.20";
    }

    /// <summary>
    /// Converts a version name to a comparable three-part vanilla version number.
    /// Old-format versions such as 1.20.3 are converted to 20.0.3.
    /// For April Fools versions, returns the version number of the release it is based on.
    /// Without a version number, such as old snapshots, it is 9999.0.0.
    /// </summary>
    public static Version NameToVersion(string? name) {
        if (name is null) return new Version(9999, 0, 0);
        name = name.Lower().Replace("_unobfuscated", "").Replace(" unobfuscated", "");
        // April Fools versions
        if (name.StartsWithF("2.0")) return NameToVersion("1.5.1");
        if (name == "15w14a") return NameToVersion("1.8.3");
        if (name.ContainsIgnoreCase(".rv-pre")) return NameToVersion("1.9.2");
        if (name.ContainsIgnoreCase("shareware")) return NameToVersion("1.13.2");
        if (name.StartsWithF("20w14") && name != "20w14a") return NameToVersion("1.15.2");
        if (name.ContainsIgnoreCase("oneblockatatime")) return NameToVersion("1.18.2");
        if (name.ContainsIgnoreCase("23w13a") && name != "20w13a") return NameToVersion("1.19.4");
        if (name == "24w14potato") return NameToVersion("1.20.4");
        if (name == "25w14craftmine") return NameToVersion("1.21.4");
        if (name == "26w14a") return NameToVersion("26.1.1");
        // Other versions
        static int ParseSegment(string[] segments, int index) {
            if (index >= segments.Length) return 0;
            // Read only the leading digits of the segment, ignoring any preview marker after it.
            return int.TryParse(segments[index].RegexSeek(@"^\d+"), out int value) ? value : 0;
        }
        string[] segments = name.Split([' ', '_', '-', '.'], StringSplitOptions.None);
        if (name.StartsWithF("1.")) {
            return new Version(ParseSegment(segments, 1), 0, ParseSegment(segments, 2));
        } else if (name.RegexCheck(@"^[2-9][0-9]\.")) {
            return new Version(ParseSegment(segments, 0), ParseSegment(segments, 1), ParseSegment(segments, 2));
        } else {
            return new Version(9999, 0, 0);
        }
    }
}
