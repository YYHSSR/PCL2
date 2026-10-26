namespace PCLCS;
public static class Resource {

    /// <summary>
    /// Maps a CurseForge ModLoaderType to the <see cref="ModLoaders"/> enum.
    /// </summary>
    public static ModLoaders FromCurseForgeModLoaderType(int modLoaderType) => modLoaderType switch {
        // https://docs.curseforge.com/rest-api/#tocS_ModLoaderType
        1 => ModLoaders.Forge,
        3 => ModLoaders.LiteLoader,
        4 => ModLoaders.Fabric,
        6 => ModLoaders.NeoForge,
        _ => ModLoaders.None,
    };

    /// <summary>
    /// Maps <see cref="ModLoaders"/> to a single CurseForge ModLoaderType.
    /// </summary>
    public static int ToCurseForgeModLoaderType(ModLoaders loaders) {
        if (loaders == ModLoaders.None) return 0;
        if (!loaders.Flags().IsSingle()) throw new ArgumentException($"传入的枚举值 {loaders} 并非单个 Mod Loader", nameof(loaders));
        return loaders switch {
            ModLoaders.Forge => 1,
            ModLoaders.LiteLoader => 3,
            ModLoaders.Fabric => 4,
            ModLoaders.NeoForge => 6,
            _ => 0,
        };
    }

}
