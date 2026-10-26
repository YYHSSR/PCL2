namespace MeloongCore;
public static class PathUtils {

    #region Separators

    /// <summary>
    /// Ensures the path ends with a folder separator.
    /// </summary>
    public static string AddSlashSuffix(string folder) 
        => folder + (folder.EndsWithF(Path.DirectorySeparatorChar) || folder.EndsWithF(Path.AltDirectorySeparatorChar) ? "" : Path.DirectorySeparatorChar);

    /// <summary>
    /// Ensures the path does not end with a folder separator.
    /// </summary>
    public static string RemoveSlashSuffix(string folder) 
        => folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    #endregion

    #region Long paths

    /// <summary>
    /// If the path is long, tries to convert it to a short path.
    /// <para/>The result has no <c>\\?\</c> prefix and no trailing separator.
    /// </summary>
    public static string ToShortPath(string pathName) {
        // Fast path
        if (string.IsNullOrEmpty(pathName)) return pathName;
        if (!pathName.Contains(":") || pathName.Contains("://")) return pathName;
        if (pathName.Length <= 220) return pathName;
        // Actual processing
        pathName = PathUtils.ForCompare(pathName);
        // Try to keep the original file name
        string pathToKeep = "";
        string pathToShorten = pathName;
        if (FileUtils.Exists(pathName)) {
            var fileName = PathUtils.GetLastPart(pathName);
            if (fileName.Length >= 180) {
                Logger.Warn($"文件名过长，将为文件名生成短路径：{pathName}", LogBehavior.None); // Some programs, such as Forge's module loading, rely on file names, so generating a short name for the file name is risky
            } else {
                pathToKeep = fileName;
                pathToShorten = PathUtils.RemoveLastPart(pathName);
            }
        }
        // Walk up to the nearest existing folder, moving the non-existent part into suffix without shortening it
        while (!DirectoryUtils.Exists(pathToShorten) && !FileUtils.Exists(pathToShorten)) { // If the path does not exist
            string? parentPath = Path.GetDirectoryName(pathToShorten);
            if (string.IsNullOrEmpty(parentPath) || PathUtils.IsSame(parentPath, pathToShorten)) return pathName; // Reached the root and nothing exists, return directly
            pathToKeep = Path.Combine(PathUtils.GetLastPart(pathToShorten), pathToKeep);
            pathToShorten = parentPath;
        }
        if (pathToKeep.Length >= 200) {
            Logger.Warn($"路径中的大部分内容都尚未建立，无法在内容不存在时缩短路径：{pathName} → {pathToShorten}", LogBehavior.None);
            return pathName;
        }
        // Get the short path
        Func<string, string> TryGetShortPath = static pathName => {
            char[] buffer = new char[220];
            int result = GetShortPathNameW(PathUtils.ForApi(pathName), buffer, buffer.Length);
            return result is 0 or > 220 ? pathName : new string(buffer, 0, result);
        };
        var shortPath = TryGetShortPath(pathToShorten);
        // Try to generate new short names to bring the length below 220 (only happens when Windows has never generated a short name for it, which is rare)
        if (Path.Combine(shortPath, pathToKeep).Length > 220) {
            Logger.Warn($"{nameof(GetShortPathNameW)} 未能有效缩短路径，将尝试生成新的短名称：{pathToShorten} → {shortPath}", LogBehavior.None);
            var candidates = new List<(string Path, string Name)>();
            string? candidatePath = pathToShorten;
            while (!string.IsNullOrEmpty(candidatePath)) {
                string candidateName = PathUtils.GetLastPart(candidatePath);
                if (candidateName.Length > 12 &&
                    string.Equals(PathUtils.GetLastPart(TryGetShortPath(candidatePath)), candidateName, StringComparison.OrdinalIgnoreCase)) {
                    candidates.Add((candidatePath, candidateName));
                }
                candidatePath = Path.GetDirectoryName(candidatePath);
            }
            foreach (var candidate in candidates.OrderByDescending(candidate => candidate.Name.Length)) {
                using var handle = CreateFileW(PathUtils.ForApi(candidate.Path), 0x40010000, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero); // GENERIC_WRITE | DELETE; share read, write, delete; OPEN_EXISTING; FILE_FLAG_BACKUP_SEMANTICS
                if (handle.IsInvalid) continue;
                bool created = false;
                try {
                    unsafe {
                        fixed (char* originalNameBuffer = candidate.Name) {
                            char* shortNameBuffer = stackalloc char[12]; // An 8.3 name is at most 12 Unicode characters
                            var name = new UnicodeString {Length = (ushort)(candidate.Name.Length * 2), MaximumLength = (ushort)((candidate.Name.Length + 1) * 2), Buffer = originalNameBuffer};
                            var shortName = new UnicodeString {MaximumLength = 24, Buffer = shortNameBuffer};
                            var context = new GenerateNameContext();
                            for (int i = 0; i < 20; i++) { // Use Windows' own 8.3 name generation algorithm, using context to generate collision-avoiding candidates one by one
                                shortName.Length = 0;
                                if (RtlGenerate8dot3Name(ref name, 0, ref context, ref shortName) != 0) break;
                                string generatedName = new(shortName.Buffer, 0, shortName.Length / 2);
                                if (SetFileShortNameW(handle, generatedName)) { created = true; break; }
                                if (Marshal.GetLastWin32Error() != 183) break; // ERROR_ALREADY_EXISTS
                            }
                        }
                    }
                } catch (EntryPointNotFoundException) {}
                if (!created) continue;
                shortPath = TryGetShortPath(pathToShorten);
                if (Path.Combine(shortPath, pathToKeep).Length <= 220) break;
            }
        }
        return PathUtils.RemoveSlashSuffix(PathUtils.RemoveExtendedPrefix(Path.Combine(shortPath, pathToKeep)));
    }
    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct UnicodeString { public ushort Length, MaximumLength; public char* Buffer; }
    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct GenerateNameContext { public ushort Checksum; public byte ChecksumInserted, NameLength; public fixed char NameBuffer[8]; public uint ExtensionLength; public fixed char ExtensionBuffer[4]; public uint LastIndexValue; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] 
    private static extern int GetShortPathNameW(string lpszLongPath, [Out] char[]? buffer, int cchBuffer);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] 
    private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] 
    private static extern bool SetFileShortNameW(Microsoft.Win32.SafeHandles.SafeFileHandle file, string shortName);
    [DllImport("ntdll.dll")] 
    private static extern int RtlGenerate8dot3Name(ref UnicodeString name, byte allowExtendedCharacters, ref GenerateNameContext context, ref UnicodeString name8dot3);

    /// <summary>
    /// Converts a full path to the extended format prefixed with <c>\\?\</c>.
    /// This removes the trailing separator and replaces / with \.
    /// </summary>
    public static string ToExtendedFormat(string pathName) {
        if (string.IsNullOrWhiteSpace(pathName)) return pathName;
        if (!pathName.Contains(@":")) return pathName; // Possibly a relative path, keep it unchanged
        if (pathName.StartsWithF(@"\\?\")) return pathName; // Already a long path
        pathName = RemoveSlashSuffix(pathName).Replace('/', '\\'); // The API requires this format
        if (pathName.StartsWithF(@"\\")) {
            return $@"\\?\UNC\{pathName[2..]}";
        } else {
            return $@"\\?\{pathName}";
        }
    }

    /// <summary>
    /// Removes the leading <c>\\?\</c> from the path.
    /// </summary>
    public static string RemoveExtendedPrefix(string pathName) {
        if (pathName.StartsWithF(@"\\?\UNC\")) return @"\\" + pathName.AfterLast(@"\\?\UNC\");
        if (pathName.StartsWithF(@"\\?\")) return pathName.AfterLast(@"\\?\");
        return pathName;
    }

    #endregion

    #region Path manipulation

    /// <summary>
    /// Removes the last segment of the path and the trailing separator.
    /// <code>
    /// "C:\foo\bar.txt" → "C:\foo"
    /// "C:\foo\bar\" → "C:\foo"
    /// "C:\foo\bar" → "C:\foo"
    /// "C:\foo\" → "C:"
    /// "https://foo.bar/file/pack.zip?arg=1" → "https://foo.bar/file"
    /// </code></summary>
    public static string RemoveLastPart(string pathName) {
        if (pathName.Contains("://")) { // Network path
            pathName = PathUtils.RemoveSlashSuffix(pathName.BeforeFirst("#").BeforeFirst("?")); // Remove query and fragment
        }
        return PathUtils.RemoveSlashSuffix(pathName).BeforeLastOfAny([@"\", "/"]);
    }

    /// <summary>
    /// Gets the last segment of the path, i.e. the file name or the current folder name.
    /// <code>
    /// "C:\foo\bar.txt" → "bar.txt"
    /// "C:\foo\bar\" → "bar"
    /// "C:\foo\bar" → "bar"
    /// "https://foo.bar/file/pack.zip?arg=1" → "pack.zip"
    /// </code></summary>
    public static string GetLastPart(string pathName) {
        if (pathName.Contains("://")) { // Network path
            pathName = PathUtils.RemoveSlashSuffix(pathName.BeforeFirst("#").BeforeFirst("?")); // Remove query and fragment
        } else { // File path
            pathName = PathUtils.RemoveSlashSuffix(pathName);
        }
        return pathName.AfterLastOfAny([@"\", "/"]);
    }

    /// <summary>
    /// Gets the path or file name without its last extension.
    /// <code>
    /// "C:\foo\bar.txt" → "bar"
    /// "create.jar.disabled" → "create.jar"
    /// "https://foo.bar/file/page.xaml.vb?arg=1" → "page.xaml"
    /// </code>
    /// </summary>
    public static string GetFileNameWithoutExtension(string pathName) 
        => GetLastPart(pathName).BeforeLast(".");

    /// <summary>
    /// Gets the last file extension of the path or file name, without the <c>.</c>, always in lower case.
    /// Returns an empty string if there is no <c>.</c>.
    /// <code>
    /// "C:\FOO\BAR.TXT" → "txt"
    /// "create.jar.disabled" → "disabled"
    /// "C:\foo\bar" → ""
    /// "https://foo.bar/file/page.xaml.vb?arg=1" → "vb"
    /// </code>
    /// </summary>
    public static string GetExtension(string pathName) {
        var name = GetLastPart(pathName);
        return name.Contains(".") ? name.AfterLast(".").Lower() : "";
    }

    /// <summary>
    /// Gets the drive letter of the path as an upper-case letter.
    /// Returns <c>null</c> if the path has no drive letter.
    /// </summary>
    public static char? GetDiskName(string? pathName) {
        if (string.IsNullOrEmpty(pathName)) return null;
        pathName = PathUtils.RemoveExtendedPrefix(pathName!);
        if (pathName.StartsWithF(@"\\") || pathName.StartsWithF("/")) return null;
        return char.ToUpper(pathName[0]);
    }

    /// <summary>
    /// Normalizes the path format for comparison.
    /// <para/>Specifically: expands short paths, removes the leading <c>\\?\</c>, changes separators to \, and removes the trailing separator.
    /// </summary>
    public static string ForCompare(string pathName) {
        try {
            return PathUtils.RemoveSlashSuffix(PathUtils.RemoveExtendedPrefix(Path.GetFullPath(pathName)).Replace("/", @"\"));
        } catch (ArgumentException ex) {
            throw new ArgumentException($"路径不正确：{pathName}", ex);
        }
    }

    /// <summary>
    /// Whether two paths are the same. <c>null</c> and the empty string are treated as the same empty path.
    /// </summary>
    public static bool IsSame(string? path1, string? path2) {
        return PathUtils.Compare(path1, path2) == 0;
    }
    /// <summary>
    /// Compares two paths, ignoring case.
    /// <para/><c>null</c> and the empty string are treated as the same empty path, which sorts before non-empty paths.
    /// </summary>
    /// <returns>
    /// Less than 0: <paramref name="path1"/> sorts before <paramref name="path2"/>.
    /// <para/>0: the two paths are equal.
    /// <para/>Greater than 0: <paramref name="path1"/> sorts after <paramref name="path2"/>.
    /// </returns>
    public static int Compare(string? path1, string? path2) {
        if (string.IsNullOrEmpty(path1)) return string.IsNullOrEmpty(path2) ? 0 : -1;
        if (string.IsNullOrEmpty(path2)) return 1;
        return string.Compare(PathUtils.ForCompare(path1!), PathUtils.ForCompare(path2!), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converts the path to a format compatible with all Windows APIs.
    /// <para/>Specifically: changes separators to \ and adds the leading <c>\\?\</c>. Adds a trailing separator for drives, otherwise removes it.
    /// </summary>
    public static string ForApi(string pathName) {
        pathName = PathUtils.ToExtendedFormat(pathName);
        if (pathName.EndsWithF(":")) pathName += @"\"; // Drive paths must keep the trailing separator
        if (pathName.EndsWithF(@":\")) return pathName;
        return PathUtils.RemoveSlashSuffix(pathName);
    }

    #endregion

    /// <summary>
    /// Whether <param name="parentPath"/> is a parent path of <paramref name="childPath"/>, or the two are the same.
    /// </summary>
    public static bool IsParentOf(string parentPath, string childPath) {
        parentPath = PathUtils.ForCompare(parentPath);
        childPath = PathUtils.ForCompare(childPath);
        return childPath.StartsWithF(parentPath + @"\", true) || PathUtils.IsSame(childPath, parentPath);
    }

}

/// <summary>
/// Common paths.
/// All paths end with a separator.
/// </summary>
public static class Paths {
    /// <summary>
    /// The folder containing the executable.
    /// </summary>
    public static string Base => PathUtils.AddSlashSuffix(AppContext.BaseDirectory);
    /// <summary>
    /// The subfolder named <see cref="Main.AppName"/> in the folder containing the executable.
    /// </summary>
    public static string BaseThenName => PathUtils.AddSlashSuffix(Base + Main.AppName);
    /// <summary>
    /// The %AppData% folder.
    /// </summary>
    public static string AppData => PathUtils.AddSlashSuffix(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
    /// <summary>
    /// The subfolder named <see cref="Main.AppName"/> in the %AppData% folder.
    /// </summary>
    public static string AppDataThenName => PathUtils.AddSlashSuffix(AppData + Main.AppName);
}
