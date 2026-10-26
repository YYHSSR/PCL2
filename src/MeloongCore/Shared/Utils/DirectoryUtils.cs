namespace MeloongCore;
public static class DirectoryUtils {

    /// <summary>
    /// Creates a folder, or the folder containing a file.
    /// Does not throw if the folder already exists.
    /// </summary>
    public static void Create(string folder) {
        if (DirectoryUtils.Exists(folder)) return;
        Logger.Trace($"新建文件夹：{folder}");
        Directory.CreateDirectory(PathUtils.ForApi(folder));
    }

    /// <summary>
    /// Creates a randomly named folder in the temp folder and returns its path.
    /// </summary>
    public static string CreateRandom() {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        DirectoryUtils.Create(tempDir);
        return tempDir;
    }

    /// <summary>
    /// Whether the folder exists.
    /// </summary>
    public static bool Exists(string folder) 
        => Directory.Exists(PathUtils.ForApi(folder));

    /// <summary>
    /// Gets a <see cref="DirectoryInfo"/> object.
    /// </summary>
    public static DirectoryInfo GetInfo(string folder) 
        => new(PathUtils.ForApi(folder));

    /// <summary>
    /// Returns all files under the specified path, without a \\?\ prefix.
    /// Returns an empty list if the folder does not exist.
    /// </summary>
    public static IEnumerable<string> EnumerateFiles(string folder, bool includeSubDirectories = false, string searchPattern = "*") {
        if (!DirectoryUtils.Exists(folder)) return [];
        return Directory.EnumerateFiles(PathUtils.ForApi(folder), searchPattern, includeSubDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).
            Select(PathUtils.RemoveExtendedPrefix);
    }

    /// <summary>
    /// Returns all folders under the specified path, without a trailing separator or a \\?\ prefix.
    /// Returns an empty list if the folder does not exist.
    /// </summary>
    public static IEnumerable<string> EnumerateDirectories(string folder, bool includeSubDirectories = false, string searchPattern = "*") {
        if (!DirectoryUtils.Exists(folder)) return [];
        return Directory.EnumerateDirectories(PathUtils.ForApi(folder), searchPattern, includeSubDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).
            Select(PathUtils.RemoveExtendedPrefix);
    }

    /// <summary>
    /// Whether the folder is empty.
    /// Returns true if the folder does not exist.
    /// </summary>
    public static bool IsEmpty(string folder) {
        if (!DirectoryUtils.Exists(folder)) return true;
        return !Directory.EnumerateFileSystemEntries(PathUtils.ForApi(folder)).Any();
    }

    /// <summary>
    /// Copies a folder.
    /// Does nothing when copying onto itself; if only the case differs, renames the folder.
    /// </summary>
    public static void Copy(string sourceFolder, string destFolder) {
        sourceFolder = PathUtils.ForCompare(sourceFolder);
        destFolder = PathUtils.ForCompare(destFolder);
        if (string.CompareOrdinal(sourceFolder, destFolder) == 0) {
            // Copying onto itself: do nothing
            Logger.Trace($"复制文件夹到自身，不执行操作：{sourceFolder} → {destFolder}");
        } else if (PathUtils.IsSame(sourceFolder, destFolder)) {
            // Paths differ only in case: equivalent to a rename
            Logger.Trace($"复制文件夹到自身，但大小写不同，等效于重命名文件夹：{sourceFolder} → {destFolder}");
            DirectoryUtils.Move(sourceFolder, destFolder);
        } else {
            // The actual copy
            Logger.Trace($"复制文件夹：{sourceFolder} → {destFolder}");
            foreach (var file in DirectoryUtils.EnumerateFiles(sourceFolder, includeSubDirectories: true).ToList()) FileUtils.Copy(file, file.Replace(sourceFolder, destFolder));
        }
    }

    /// <summary>
    /// Moves a folder.
    /// Creates the target folder and overwrites any existing folder.
    /// </summary>
    public static void Move(string sourceFolder, string destFolder) {
        SafetyCheck(sourceFolder);
        sourceFolder = PathUtils.ForCompare(sourceFolder);
        destFolder = PathUtils.ForCompare(destFolder);
        if (string.CompareOrdinal(sourceFolder, destFolder) == 0) {
            // Moving onto itself: do nothing
            Logger.Trace($"剪切文件夹到自身，不执行操作：{sourceFolder} → {destFolder}");
        } else if (PathUtils.IsSame(sourceFolder, destFolder)) {
            // Paths differ only in case
            Logger.Trace($"剪切文件夹到自身，但大小写不同：{sourceFolder} → {destFolder}");
            var temp = Path.Combine(PathUtils.RemoveLastPart(sourceFolder), Path.GetRandomFileName());
            DirectoryUtils.Move(sourceFolder, temp);
            DirectoryUtils.Move(temp, destFolder);
        } else if (PathUtils.GetDiskName(sourceFolder) == PathUtils.GetDiskName(destFolder)) {
            // Same drive: call Move directly (it only renames the folder, which is faster)
            Logger.Trace($"剪切文件夹到同一磁盘：{sourceFolder} → {destFolder}");
            DirectoryUtils.Delete(destFolder); // Move requires that the target folder does not exist yet
            Retrier.Attempt(delay: _ => TimeSpan.FromMilliseconds(200), isRetryAllowed: ex => ex is IOException, action: _ => {
                FileUtils.SetReadOnly(sourceFolder, false);
                Directory.Move(PathUtils.ForApi(sourceFolder), PathUtils.ForApi(destFolder));
            });
        } else {
            // Different drives: must copy and then delete
            Logger.Trace($"剪切文件夹到不同磁盘：{sourceFolder} → {destFolder}");
            DirectoryUtils.Copy(sourceFolder, destFolder);
            DirectoryUtils.Delete(sourceFolder);
        }
    }

    /// <summary>
    /// Deletes a folder.
    /// <para/>If <paramref name="toRecycleBin"/> is set, tries to move it to the recycle bin, falling back to permanent deletion on failure.
    /// </summary>
    /// <returns>
    /// <see langword="null"/> if the folder does not exist.
    /// <para/><see langword="true"/> if it was moved to the recycle bin.
    /// <para/><see langword="false"/> if it was permanently deleted.
    /// </returns>
    public static bool? Delete(string folder, bool toRecycleBin = false) {
        if (!DirectoryUtils.Exists(folder)) return null;
        Logger.Trace($"{(toRecycleBin ? "将文件夹删除到回收站" : "删除文件夹")}：{folder}");
        SafetyCheck(folder);
        // Move to the recycle bin
        if (toRecycleBin) {
            try {
                FileUtils.DeleteToRecycleBin(folder);
                return true;
            } catch (Exception ex) {
                Logger.Warn(ex, $"无法将文件夹删除到回收站，回退到永久删除：{folder}");
            }
        }
        // Permanent deletion
        DeleteInternal(folder);
        static void DeleteInternal(string folder) {
            try {
                folder = PathUtils.ForApi(folder);
                foreach (string filePath in DirectoryUtils.EnumerateFiles(folder)) FileUtils.Delete(filePath); // Delete files
                foreach (string str in DirectoryUtils.EnumerateDirectories(folder)) DeleteInternal(str); // Recursively delete subfolders
                Retrier.Attempt(delay: _ => TimeSpan.FromMilliseconds(200), isRetryAllowed: ex => ex is IOException, action: _ => {
                    FileUtils.SetReadOnly(folder, false);
                    Directory.Delete(folder, true);
                }); // Finally delete the folder itself
            } catch (DirectoryNotFoundException ex) { // #4549, it may also have been deleted by another thread
                if (DirectoryUtils.Exists(folder)) {
                    Logger.Warn(ex, $"该文件夹可能为孤立的符号链接，尝试直接删除（{folder}）");
                    FileUtils.SetReadOnly(folder, false);
                    Directory.Delete(folder);
                } else {
                    throw;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Safety check: throws when trying to operate on the desktop, documents, downloads, Windows, or the program's own folder.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException" />
    private static void SafetyCheck(string folder) {
        folder = PathUtils.ForCompare(folder);
        if (PathUtils.IsSame(folder, Path.GetPathRoot(folder)))
            throw new UnauthorizedAccessException($"不应操作磁盘根目录：{folder}");
        if (criticalFolders.Value.Any(f => PathUtils.IsParentOf(folder, f)))
            throw new UnauthorizedAccessException($"不应操作文件夹：{folder}");
    }
    private static readonly Lazy<HashSet<string>> criticalFolders = new(() => new(new[] {
        Paths.Base,
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") // There is no SpecialFolder value for the Downloads folder
    }.Where(f => f.Contains(Path.VolumeSeparatorChar)))); // GetFolderPath returns an empty string when a folder is missing (#8636)

}
