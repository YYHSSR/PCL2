namespace PCLCS;

/// <summary>
/// Type of community resource.
/// </summary>
[Flags]
public enum ResourceTypes {
    /// <summary>Mod。</summary>
    Mod = 1,
    /// <summary>Modpack.</summary>
    ModPack = 2,
    /// <summary>Resource pack.</summary>
    ResourcePack = 4,
    /// <summary>Shader pack.</summary>
    Shader = 8,
    /// <summary>Data pack.</summary>
    DataPack = 16,
    /// <summary>Server plugin.</summary>
    Plugin = 32,
    /// <summary>World.</summary>
    Map = 64,
    /// <summary>Contains both data packs and mods.</summary>
    ModOrDataPack = Mod | DataPack,
    /// <summary>Any kind is allowed, or the kind is unknown.</summary>
    Any = Mod | ModPack | ResourcePack | Shader | DataPack | Plugin | Map,
}

/// <summary>
/// Source platform of community resources.
/// </summary>
[Flags]
public enum ResourcePlatforms {
    CurseForge = 1,
    Modrinth = 2,
    Any = CurseForge | Modrinth,
}

/// <summary>
/// Type of mod loader.
/// </summary>
[Flags]
public enum ModLoaders {
    None = 0,
    Forge = 1,
    LiteLoader = 2,
    Fabric = 4,
    NeoForge = 16,
    All = Forge | LiteLoader | Fabric | NeoForge,
}

public static class Versions {
    /// <summary>
    /// Version of the mod info cache in version management.
    /// </summary>
    public const int LocalModCacheVersion = 21;
    /// <summary>
    /// Version of the local Minecraft instance info cache.
    /// </summary>
    public const int McInstanceCacheVersion = 39;
    /// <summary>
    /// Version of the Java-related config.
    /// </summary>
    public const int JavaConfigVersion = 1;
    /// <summary>
    /// Version of the Minecraft install manifest.
    /// </summary>
    public const int McInstallManifestVersion = 1;
}
