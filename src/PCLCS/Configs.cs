namespace PCLCS;
public static class Configs {

    public static readonly DynamicConfigEntry<McInstallManifest> McInstallManifest = new("MinecraftInstallManifest", null);

    // Java

    /// <summary> Version of the current Java-related config. 0 means not yet migrated from the old Settings system.</summary>
    public static readonly ConfigEntry<int> JavaConfigVersion = new("JavaConfigVersion", 0);
    public static readonly ConfigEntry<ConcurrentList<Java>> JavaList = new("JavaList", []);
    public static readonly ConfigEntry<List<string>> JavaRemovedList = new("JavaRemovedList", []);
    public static readonly DynamicConfigEntry<bool> JavaMigrated = new("InstanceMigratedJava", false);
    public static readonly DynamicConfigEntry<Java> JavaForced = new("InstanceForcedJava", null);

    // Hints

    public static readonly ConfigEntry<bool> HintModifyInstanceWarning = new("HintModifyInstanceWarning", false);

    static Configs() {
        /// When JavaForced gets a value not in JavaList, resets it to null.
        JavaForced.PreviewGet += (args, provider) => {
            if (args.Value is not null && !JavaList.Get()!.Contains(args.Value)) {
                args.Value = null;
                JavaForced.Reset(provider);
            }
        };
    }
}
