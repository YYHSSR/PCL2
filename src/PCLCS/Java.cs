namespace PCLCS;

/// <summary>
/// A single Java installation.
/// </summary>
[Serializable] public class Java(string folder, Version? version = null) : IEquatable<Java> {

    /// <summary>
    /// Path of the folder containing java.exe, usually the bin folder. Ends with \.
    /// </summary>
    public readonly string Folder = PathUtils.AddSlashSuffix(PathUtils.ForCompare(folder));
    /// <summary>
    /// Full path of java.exe.
    /// </summary>
    [JsonIgnore] public string JavaExePath => Path.Combine(Folder, "java.exe");

    /// <summary>
    /// Java version (e.g. 16.0.1); <see cref="Version.Major"/> is guaranteed to be its major version.
    /// <para/> If null, <see cref="CheckAsync"/> still needs to be called to get the version.
    /// </summary>
    public Version? Version = version;
    /// <summary>Java's default character encoding.</summary>
    [JsonIgnore] public string? FileEncodingName;
    /// <summary>
    /// Native character encoding determined by Java from the system environment; only available on Java 17+.
    /// <para/>When the system has UTF-8 enabled, some Java versions wrongly return JDK, so <see cref="Encoding.Default"/> cannot be used.
    /// </summary>
    [JsonIgnore] public string? NativeEncodingName;

    /// <summary>
    /// Checks whether this Java has problems and gets its version.
    /// Returns whether it passed the check.
    /// </summary>
    public async Task<bool> CheckAsync(CancellationToken cancellationToken = default) {
        if (Interlocked.Exchange(ref isChecked, 1) == 1 && available != null) return available!.Value; // Already checked
        string? output = null;
        try {
            if (!FileUtils.Exists(JavaExePath)) throw new FileNotFoundException("未找到 java.exe 文件", JavaExePath);

            // Run -version
            output = (await TaskUtils.RunProgramAsync(JavaExePath, "-XshowSettings:properties -version", 15000, cancellationToken: cancellationToken).NoCapture()).Output.Lower();
            if (output == "") throw new InvalidOperationException("尝试运行该 Java 失败");
            Logger.Trace($"Java 检查输出：{JavaExePath}{Environment.NewLine}{output}");
            if (output.Contains("/lib/ext exists")) throw new InvalidOperationException("无法运行该 Java，请在删除 Java 文件夹中的 /lib/ext 文件夹后再试");
            if (output.Contains("a fatal error") || output.Contains("error: ")) throw new InvalidOperationException("无法运行该 Java，该 Java 或系统存在问题");
           
            // Get the version number
            var versionString = (output.RegexSeek(@"(?<=version "")[^""]+") ?? output.RegexSeek("(?<=openjdk )[0-9]+"))?.Replace("_", ".").Replace("+", ".").BeforeFirst("-");
            if (string.IsNullOrEmpty(versionString)) throw new InvalidOperationException($"未找到该 Java 的版本号{(output.Length < 500 ? $"\n原始输出为：\n{output}" : "")}");
            if (versionString.StartsWithF("1.")) versionString = versionString!.AfterFirst("."); // Remove the leading 1.
            var segments = versionString!.Split(".", true).ToList();
            while (segments.Count < 4) segments.Add("0");
            Version = new Version(segments.Take(4).Join("."));
            if (Version.Major is <= 4 or >= 100) throw new InvalidOperationException($"分析详细信息失败，获取的版本为 {Version}{(output.Length < 500 ? $"\n原始输出为：\n{output}" : "")}");

            // Get Java properties
            if (!output.Contains("64-bit")) throw new InvalidOperationException("该 Java 为 32 位版本，请安装 64 位的 Java");
            string? GetProperty(string name) {
                string prefix = name + " = ";
                string? line = output.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWithF(prefix));
                return line?[prefix.Length..].Trim();
            }
            FileEncodingName = GetProperty("file.encoding");
            NativeEncodingName = GetProperty("native.encoding") ?? FileEncodingName; // Older versions have no native.encoding

            Logger.Info($"检查 Java 成功：{this}");
            available = true;
        } catch (Exception ex) {
            if (ex.IsCanceled()) {
                isChecked = 0;
                throw;
            }
            Logger.Warn($"检查 Java 失败（{JavaExePath}），其输出为：\n{output ?? "无程序输出"}");
            available = false;
        }
        return available!.Value;
    }

    public void InvalidateChecked() {
        isChecked = 0;
        available = null;
    }

    [JsonIgnore] private int isChecked = 0;
    /// <summary>If already checked, whether the check succeeded; null if not checked yet.</summary>
    [JsonIgnore] internal bool? available = null;

    // Same check
    public bool Equals(Java? other)
        => other is not null && PathUtils.IsSame(Folder, other.Folder);
    public override bool Equals(object? obj) => obj is Java other && Equals(other);
    public override int GetHashCode() => StringComparer.CurrentCultureIgnoreCase.GetHashCode(PathUtils.ForCompare(Folder));

    /// <summary>User-friendly description text.</summary>
    public override string ToString()
        => $"Java {Version?.Major.ToString() ?? "尚未检查"} ({Version})：{Folder}";
}

public static class JavaUtils {

    /// <summary>
    /// Checks whether all the specified Javas have problems and gets their versions.
    /// </summary>
    /// <returns>All Javas that passed the check.</returns>
    public static async Task<List<Java>> CheckAllAsync(IEnumerable<Java> javas, CancellationToken cancellationToken = default, ProgressProvider? progress = null) {
        await TaskUtils.WhenAll(javas.Where(j => j.available == null).Select(
            async java => await java.CheckAsync(cancellationToken).NoCapture()
        ), progress).NoCapture();
        return javas.Where(j => j.available == true).ToList();
    }

    /// <summary>
    /// Sorts the Java list.
    /// This also checks all unchecked Javas.
    /// </summary>
    public static async Task<List<Java>> SortAsync(List<Java> javas, CancellationToken cancellationToken = default, ProgressProvider? progress = null) {
        javas = await JavaUtils.CheckAllAsync(javas, cancellationToken, progress).NoCapture();
        javas.Sort((left, right) => {
            // Prefer Javas from the list of folders containing a complete Java
            var isLeftInCandidateFolder = JavaUtils.CandidateFolders.Value.Any(folder => PathUtils.IsParentOf(folder, left.Folder));
            var isRightInCandidateFolder = JavaUtils.CandidateFolders.Value.Any(folder => PathUtils.IsParentOf(folder, right.Folder));
            if (isLeftInCandidateFolder != isRightInCandidateFolder) return isLeftInCandidateFolder ? -1 : 1;
            // Then prefer Javas whose major version is close to 21
            var leftDistance = left.Version is null ? int.MaxValue : Math.Abs(left.Version.Major - 21);
            var rightDistance = right.Version is null ? int.MaxValue : Math.Abs(right.Version.Major - 21);
            return leftDistance.CompareTo(rightDistance);
        });
        return javas;
    }

    #region Search

    /// <summary>
    /// List of folders containing a complete Java.
    /// </summary>
    internal static readonly Lazy<List<string>> CandidateFolders = new(() => {
        List<string> folders = [
            Paths.AppData + @".minecraft\runtime\", // This is also where PCL downloads Java
            Paths.AppData + @".hmcl\java\",
            Paths.AppData + @"ATLauncher\runtimes\minecraft\",
            Paths.AppData + @"ModrinthApp\meta\java_versions\",
            Paths.AppData + @"PrismLauncher\java\",
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\curseforge\minecraft\Install\runtime\",
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\.jdks\",
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\.sdkman\candidates\java\",
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\.ftba\bin\runtime\",
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Packages\Microsoft.4297127D64EC6_8wekyb3d8bbwe\LocalCache\Local\runtime\",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Minecraft Launcher\runtime\",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Minecraft\runtime\",
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\Curse\Minecraft\Install\runtime\",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\Java\",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\Eclipse Adoptium\",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\Amazon Corretto\",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\Zulu\",
            ..DirectoryUtils.EnumerateDirectories(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\Microsoft\", searchPattern: "jdk-*"),
            // Environment variables
            ..(Environment.GetEnvironmentVariable("JDK_HOME") + ";" + Environment.GetEnvironmentVariable("JAVA_HOME")).
                Split(";", true).Select(path => path.Trim(' ', '"'))
        ];
        return folders.Select(f => {
            try {
                return PathUtils.ForCompare(f);
            } catch (Exception ex) {
                Logger.Warn(ex, $"Java 候选文件夹路径无效（{f}）");
                return null;
            }
        }).Where(f => f is not null).Cast<string>().ToList();
    });
    

    /// <summary>
    /// Searches common folders and environment variables for Java and writes the results to the settings.
    /// </summary>
    public static async Task RefreshListAsync(CancellationToken c = default, ProgressProvider? p = null) {
        // Search
        List<Java> javaList = await SearchFoldersAsync(true, CandidateFolders.Value, c, p?.SplitTo(0.5)).NoCapture();
        // Put newly found Javas first; the existing list keeps its order after them
        var oldJavaList = Configs.JavaList.Get()!;
        oldJavaList = await JavaUtils.CheckAllAsync(oldJavaList, c, p?.SplitTo(0.9)).NoCapture(); // Check the Javas in the existing list
        javaList.RemoveAll(oldJavaList); // At this point javaList contains only newly found Javas
        javaList = await JavaUtils.SortAsync(javaList, c).NoCapture(); // Sort all newly found Javas by priority
        javaList.AddRange(oldJavaList);
        javaList = javaList.Distinct().ToList();
        // Remove Javas that no longer exist
        javaList.RemoveIf(j => Configs.JavaRemovedList.Get()!.Contains(j.Folder, StringComparer.OrdinalIgnoreCase));
        // Write to settings
        Configs.JavaList.Set([..javaList]);
        Logger.Info($"Java 搜索完成，发现 {javaList.Count} 个 Java");
    }

    /// <summary>
    /// Searches all specified folders and their subfolders for Java in parallel and checks their validity.
    /// </summary>
    public static async Task<List<Java>> SearchFoldersAsync(bool includeSubDirectories, IEnumerable<string> folders, CancellationToken cancellationToken = default, ProgressProvider? progress = null) {
        // Parallel search
        ConcurrentBag<(string Folder, bool HasReparsePoint, bool IsSpecialPath)> results = [];
        await TaskUtils.ForEachAsync(folders.Distinct(StringComparer.OrdinalIgnoreCase), 4, async (folder, cp) => {
            await Task.Run(() => {
                try {
                    foreach (var file in DirectoryUtils.EnumerateFiles(folder, includeSubDirectories, "java.exe")) {
                        cancellationToken.ThrowIfCancellationRequested();
                        string targetFolder = PathUtils.AddSlashSuffix(PathUtils.ForCompare(PathUtils.RemoveLastPart(file)));
                        // Whether the folder contains a reparse point (such as a symbolic link)
                        static bool HasReparsePoint(FileSystemInfo info) {
                            if (PathUtils.IsParentOf(Paths.AppData + @".minecraft\runtime\", info.FullName)) return false; // Do not scan PCL's download path (#8928)
                            do {
                                if (info.Attributes.HasFlagF(FileAttributes.ReparsePoint)) return true;
                                info = info is FileInfo fileInfo ? fileInfo.Directory : ((DirectoryInfo) info).Parent;
                            } while (info is not null);
                            return false;
                        }
                        // Results
                        var hasReparsePoint = HasReparsePoint(FileUtils.GetInfo(Path.Combine(targetFolder, "java.exe")));
                        var isSpecialPath = targetFolder.ContainsIgnoreCase("java8path_target_") || targetFolder.ContainsIgnoreCase("javapath_target_") || targetFolder.ContainsIgnoreCase("javatmp") || targetFolder.ContainsIgnoreCase("system32");
                        results.Add((targetFolder, hasReparsePoint, isSpecialPath));
                    }
                } catch (UnauthorizedAccessException) {
                    Logger.Info($"快速查找 Java 时没有权限（{folder}）");
                } catch (Exception ex) {
                    ex.ThrowIfCanceled();
                    Logger.Warn(ex, $"快速查找 Java 时出错（{folder}）");
                }
            }, cancellationToken).NoCapture();
        }, cancellationToken, progress?.SplitTo(0.2)).NoCapture();
        if (!results.Any()) return [];

        // Filter out reparse points
        if (results.All(r => r.HasReparsePoint)) {
            Logger.Warn("找到的所有 Java 都包含重解析点，保留全部结果");
        } else if (results.Any(r => r.HasReparsePoint)) {
            Logger.Warn($"找到的以下 Java 包含重解析点，将它们从列表中排除：{results.Where(r => r.HasReparsePoint).Select(r => r.Folder).Join('、')}");
            results = [..results.Where(r => !r.HasReparsePoint)];
        }
        // Filter out special paths
        if (results.All(r => r.IsSpecialPath)) {
            Logger.Warn("找到的所有 Java 都包含特殊路径，保留全部结果");
        } else if (results.Any(r => r.IsSpecialPath)) {
            Logger.Warn($"找到的以下 Java 包含特殊路径，将它们从列表中排除：{results.Where(r => r.IsSpecialPath).Select(r => r.Folder).Join('、')}");
            results = [..results.Where(r => !r.IsSpecialPath)];
        }

        // Check and return
        List<Java> javaList = results.Select(r => r.Folder).Distinct(StringComparer.OrdinalIgnoreCase).Select(folder => new Java(folder)).ToList();
        javaList = (await JavaUtils.CheckAllAsync(javaList, cancellationToken, progress?.SplitTo(1)).NoCapture()).ToList();
        javaList = await JavaUtils.SortAsync(javaList, cancellationToken).NoCapture();
        Logger.Info($"快速查找 Java 完成，共有 {javaList.Count} 个候选：{javaList.Join('、')}");
        return javaList;
    }

    #endregion

}
