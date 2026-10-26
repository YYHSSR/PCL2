namespace PCLCS;

/// <summary>
/// MC version install manifest and PCL managed-file manifest.
/// </summary>
/// <param name="SchemaVersion"> Manifest schema version; 0 for old manifests without a version.</param>
/// <param name="Components"> Install target and managed component versions.</param>
/// <param name="LastModifiedUtc"> Last modified time (UTC).</param>
/// <param name="ManagedFiles"> Managed files; an empty list if there are none.</param>
public record McInstallManifest(int SchemaVersion, McComponents Components, DateTime LastModifiedUtc, IReadOnlyList<McInstallManifest.ManagedFile> ManagedFiles) {
    /// <summary>
    /// Cleans up records of components installed as mods that have been deleted or modified, and updates the manifest.
    /// </summary>
    public McInstallManifest RefreshModComponents(string gameFolder) {
        List<ManagedFile> invalidFiles = [];
        foreach (ManagedFile entry in ManagedFiles) {
            if (entry.Component is null) continue; // Do not check non-component files
            FileInfo file = new(Path.Combine(gameFolder, entry.RelativePath));
            if (!file.Exists || file.LastWriteTimeUtc != entry.LastWriteTimeUtc.ToUniversalTime()) invalidFiles.Add(entry);
        }
        if (!invalidFiles.Any()) return this;
        return this with {
            Components = Components with {
                OptiFineEntry = invalidFiles.Any(file => file.Component == "OptiFine") ? null : Components.OptiFineEntry,
                FabricApiVersion = invalidFiles.Any(file => file.Component == "FabricApi") ? null : Components.FabricApiVersion,
                OptiFabricVersion = invalidFiles.Any(file => file.Component == "OptiFabric") ? null : Components.OptiFabricVersion
            },
            ManagedFiles = ManagedFiles.Where(file => !invalidFiles.Contains(file)).ToList(),
            LastModifiedUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// A file in the version folder that is managed and recorded by PCL.
    /// </summary>
    /// <param name="RelativePath"> File path relative to the game directory. Guaranteed to be all lower case and comparable.</param>
    /// <param name="Sha1"> Lower-case SHA1.</param>
    /// <param name="LastWriteTimeUtc"> Last modified time of the target file (UTC).</param>
    /// <param name="Component"> Installer component name; empty if the file is managed by a modpack.</param>
    public record ManagedFile(string RelativePath, string Sha1, DateTime LastWriteTimeUtc,
        [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] string? Component = null);
}

/// <param name="MinecraftVersion"> Target Minecraft version.</param>
/// <param name="ModLoaderVersions"> Version of each mod loader in the target; an empty dictionary for vanilla.</param>
/// <param name="OptiFineEntry"> OptiFine entry info, or <see langword="null"/> if not installed.</param>
/// <param name="FabricApiVersion"> Fabric API version managed by PCL, or <see langword="null"/> if not installed.</param>
/// <param name="OptiFabricVersion"> OptiFabric version managed by PCL, or <see langword="null"/> if not installed.</param>
[method: JsonConstructor]
public record McComponents(string MinecraftVersion, Dictionary<ModLoaders, string> ModLoaderVersions,
        [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] JObject? OptiFineEntry = null,
        [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] string? FabricApiVersion = null,
        [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] string? OptiFabricVersion = null) {
    [JsonIgnore]
    public string? OptiFineVersion => OptiFineEntry?["InstanceName"]?.Value<string>()?.AfterLast("-OptiFine_");
    public McComponents(string minecraftVersion, JObject? optiFineEntry, string? forgeVersion,
        string? neoForgeVersion, string? fabricVersion, string? fabricApiVersion, string? optiFabricVersion, string? liteLoaderVersion)
        : this(minecraftVersion, [], optiFineEntry, fabricApiVersion, optiFabricVersion) {
        if (forgeVersion is not null) ModLoaderVersions[ModLoaders.Forge] = forgeVersion;
        if (neoForgeVersion is not null) ModLoaderVersions[ModLoaders.NeoForge] = neoForgeVersion;
        if (fabricVersion is not null) ModLoaderVersions[ModLoaders.Fabric] = fabricVersion;
        if (liteLoaderVersion is not null) ModLoaderVersions[ModLoaders.LiteLoader] = liteLoaderVersion;
    }
    public bool IsEquivalentTo(McComponents other) =>
        MinecraftVersion == other.MinecraftVersion &&
        OptiFineVersion == other.OptiFineVersion &&
        FabricApiVersion == other.FabricApiVersion &&
        OptiFabricVersion == other.OptiFabricVersion &&
        ModLoaderVersions.Count == other.ModLoaderVersions.Count &&
        ModLoaderVersions.All(entry => other.ModLoaderVersions.TryGetValue(entry.Key, out string? version) && version == entry.Value);
}
