namespace PCLCS;

public static class LaunchUtils {

    /// <summary>
    /// Extracts the patch file and returns its full path.
    /// </summary>
    /// <param name="patchFilePath">Patch file path
    /// <param name="outputDirectory">Directory to extract the patch file to.</param>
    public static string ExtractPatch(string patchFilePath, string outputDirectory) {
        string resourceName = patchFilePath;
        Logger.Info($"选定的资源 {patchFilePath} 输出路径：{outputDirectory}");
        lock (ExtractPatchLock) { // Prevent conflicts from OptiFine and Forge installations extracting at the same time
            try {
                FileUtils.ExtractResources(resourceName, Path.Combine(outputDirectory, PathUtils.GetLastPart(patchFilePath)), typeof(LaunchUtils));
            } catch (Exception ex) {
                if (!DirectoryUtils.Exists(outputDirectory)) throw new FileNotFoundException($"释放 {patchFilePath} 失败", ex);
                Logger.Warn(ex, $"{patchFilePath} 文件重新释放失败，将尝试更换文件名重新生成");
                patchFilePath = Path.Combine(outputDirectory, $"{PathUtils.GetFileNameWithoutExtension(patchFilePath)}2.{PathUtils.GetExtension(patchFilePath)}");
                try {
                    FileUtils.ExtractResources(resourceName, Path.Combine(outputDirectory, PathUtils.GetLastPart(patchFilePath)), typeof(LaunchUtils));
                } catch (Exception ex2) {
                    throw new FileNotFoundException($"释放 {patchFilePath} 最终尝试失败", ex2);
                }
            }
        }
        return Path.Combine(outputDirectory, PathUtils.GetLastPart(patchFilePath));
    }
    private static readonly object ExtractPatchLock = new();

}
