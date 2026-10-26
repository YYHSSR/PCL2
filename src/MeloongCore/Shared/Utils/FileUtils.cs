using System.IO.Compression;

namespace MeloongCore;
public static class FileUtils {

    #region Reading

    /// <summary>
    /// Opens a read-only <see cref="Stream"/> for the specified file.
    /// <para/>If <paramref name="type"/> is specified, reads the embedded resource from that type's assembly instead.
    /// </summary>
    public static Stream ReadAsStream(string filePath, Type? type = null) {
        if (type is null) {
            return Retrier.Attempt(delay: _ => TimeSpan.FromMilliseconds(200), isRetryAllowed: ex => ex is IOException, func: _ 
                => new FileStream(PathUtils.ForApi(filePath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        } else {
            return type.Assembly.GetManifestResourceStream(filePath) ?? throw new FileNotFoundException($"Embedded resource \"{filePath}\" was not found.");
        }
    }

    /// <summary>
    /// Reads all content of the file.
    /// <para/>If <paramref name="type"/> is specified, reads the embedded resource from that type's assembly instead.
    /// </summary>
    public static byte[] ReadAsBytes(string filePath, Type? type = null) {
        using Stream fs = FileUtils.ReadAsStream(filePath, type); // File.ReadAllBytes cannot be used: it does not specify FileShare.ReadWrite and throws when the file is in use
        using MemoryStream ms = new();
        fs.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Reads all content of the file.
    /// <para/>If <paramref name="type"/> is specified, reads the embedded resource from that type's assembly instead.
    /// </summary>
    public static string ReadAsString(string filePath, Encoding? encoding = null, Type? type = null) 
        => FileUtils.ReadAsBytes(filePath, type).GetString(encoding);
    /// <summary>
    /// Reads all content of the file.
    /// <para/>Returns <see langword="null"/> instead of throwing if the file does not exist or cannot be read.
    /// <para/>If <paramref name="type"/> is specified, reads the embedded resource from that type's assembly instead.
    /// </summary>
    public static string? TryReadAsString(string filePath, Encoding? encoding = null, Type? type = null) {
        try {
            if (type == null && !FileUtils.Exists(filePath)) return null;
            return FileUtils.ReadAsBytes(filePath, type).GetString(encoding);
        } catch {
            return null;
        }
    }

    /// <summary>
    /// Reads all content of the file and splits it into lines.
    /// <para/>If <paramref name="type"/> is specified, reads the embedded resource from that type's assembly instead.
    /// </summary>
    public static string[] ReadAsLines(string filePath, bool skipEmptyLines = false, Encoding? encoding = null, Type? type = null)
        => FileUtils.ReadAsString(filePath, encoding, type).SplitLines(skipEmptyLines);
    /// <summary>
    /// Reads all content of the file and splits it into lines.
    /// <para/>Returns an empty array instead of throwing if the file does not exist or cannot be read.
    /// <para/>If <paramref name="type"/> is specified, reads the embedded resource from that type's assembly instead.
    /// </summary>
    public static string[] TryReadAsLines(string filePath, bool skipEmptyLines = false, Encoding? encoding = null, Type? type = null)
        => FileUtils.TryReadAsString(filePath, encoding, type)?.SplitLines(skipEmptyLines) ?? [];

    /// <summary>
    /// Reads all content of a JSON file.
    /// <para/>If <paramref name="type"/> is specified, reads the embedded resource from that type's assembly instead.
    /// </summary>
    public static JToken? ReadAsJson(string filePath, Encoding? encoding = null, Type? type = null)
        => FileUtils.ReadAsString(filePath, encoding, type).DeserializeJson();
    /// <summary>
    /// Reads all content of a JSON file.
    /// <para/>If <paramref name="type"/> is specified, reads the embedded resource from that type's assembly instead.
    /// </summary>
    public static T? ReadAsJson<T>(string filePath, Encoding? encoding = null, Type? type = null) where T : JToken 
        => FileUtils.ReadAsString(filePath, encoding, type).DeserializeJson<T>();

    #endregion

    #region Creating / writing

    /// <summary>
    /// Creates a file and writes <paramref name="text" /> to it.
    /// Overwrites the file if it already exists.
    /// </summary>
    public static void Write(string filePath, string text, Encoding? encoding = null) 
        => FileUtils.Write(filePath, (encoding ?? new UTF8Encoding()).GetBytes(text));
    /// <summary>
    /// Creates a file and writes <paramref name="content" /> to it.
    /// Overwrites the file if it already exists.
    /// </summary>
    public static void Write(string filePath, IEnumerable<byte> content) 
        => FileUtils.Write(filePath, [.. content]);
    /// <summary>
    /// Creates a file and writes <paramref name="content" /> to it.
    /// Overwrites the file if it already exists.
    /// </summary>
    public static void Write(string filePath, byte[] content) {
        DirectoryUtils.Create(PathUtils.RemoveLastPart(filePath));
        Logger.Trace($"写入文件：{filePath}（{content.Length} 字节）");
        Retrier.Attempt(delay: _ => TimeSpan.FromMilliseconds(200), isRetryAllowed: ex => ex is IOException, action: _ => {
            File.WriteAllBytes(PathUtils.ForApi(filePath), content);
        });
    }

    /// <summary>
    /// Writes a file atomically. On success the target is fully replaced by the new content; on failure an existing target is left unchanged.
    /// Does not guarantee merging of concurrent writes.
    /// </summary>
    public static void WriteAtomic(string filePath, string text, Encoding? encoding = null) =>
        FileUtils.WriteAtomic(filePath, (encoding ?? new UTF8Encoding()).GetBytes(text));
    /// <summary>
    /// Writes a file atomically. On success the target is fully replaced by the new content; on failure an existing target is left unchanged.
    /// Does not guarantee merging of concurrent writes.
    /// </summary>
    public static void WriteAtomic(string filePath, byte[] content) {
        string tempPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try {
            FileUtils.Write(tempPath, content);
            if (FileUtils.Exists(filePath)) {
                File.Replace(PathUtils.ForApi(tempPath), PathUtils.ForApi(filePath), null, true);
            } else {
                File.Move(PathUtils.ForApi(tempPath), PathUtils.ForApi(filePath));
            }
        } finally {
            FileUtils.Delete(tempPath);
        }
    }

    /// <summary>
    /// Creates a file and writes <paramref name="stream" /> to it.
    /// Resets the stream position to the beginning first.
    /// Overwrites the file if it already exists.
    /// </summary>
    public static void Write(string filePath, Stream stream) {
        Retrier.Attempt(delay: _ => TimeSpan.FromMilliseconds(200), isRetryAllowed: ex => ex is IOException, action: _ => {
            FileUtils.Delete(filePath);
            using FileStream fileStream = FileUtils.CreateAsStream(filePath);
            if (stream.CanSeek && stream.Position != 0) stream.Seek(0, SeekOrigin.Begin);
            Logger.Trace($"写入文件：{filePath}（{stream.GetType().Name}{(stream.CanSeek ? $" {stream.Length} 字节" : "")}）");
            stream.CopyTo(fileStream);
        });
    }
    /// <summary>
    /// Creates a file and opens a <see cref="FileStream"/> in <see cref="FileMode.Create"/> mode.
    /// Overwrites the file if it already exists.
    /// </summary>
    public static FileStream CreateAsStream(string filePath) {
        DirectoryUtils.Create(PathUtils.RemoveLastPart(filePath));
        Logger.Trace($"创建文件流：{filePath}");
        return Retrier.Attempt(delay: _ => TimeSpan.FromMilliseconds(200), isRetryAllowed: ex => ex is IOException, func: _ => {
            FileUtils.Delete(filePath);
            return new FileStream(PathUtils.ForApi(filePath), FileMode.Create);
        });
    }

    /// <summary>
    /// Creates a randomly named file in the temp folder and returns its path.
    /// </summary>
    public static string CreateRandom()
        => Retrier.Attempt(delay: _ => TimeSpan.FromMilliseconds(200), isRetryAllowed: ex => ex is IOException, func: _ 
            => Path.GetTempFileName());

    /// <summary>
    /// Reads the embedded resource named <paramref name="logicalName"/> from the assembly of <paramref name="type"/> and writes it to <paramref name="localFilePath"/>.
    /// <para/> Skips writing if the file already exists with the same content.
    /// </summary>
    public static void ExtractResources(string logicalName, string localFilePath, Type type) {
        var content = ReadAsBytes(logicalName, type);
        if (Exists(localFilePath) && CryptographyUtils.ComputeFileHash(localFilePath) == CryptographyUtils.ComputeHash(content)) return;
        Logger.Info($"将资源写入到文件：{logicalName} → {localFilePath}");
        Write(localFilePath, content);
    }

    #endregion

    #region Copy / move

    /// <summary>
    /// Copies a file.
    /// Creates the target folder and overwrites any existing file.
    /// Does nothing when copying onto itself; if only the case differs, renames the file.
    /// </summary>
    public static void Copy(string sourceFilePath, string destFilePath) {
        sourceFilePath = PathUtils.ForCompare(sourceFilePath);
        destFilePath = PathUtils.ForCompare(destFilePath);
        if (string.CompareOrdinal(sourceFilePath, destFilePath) == 0) {
            // Copying onto itself: do nothing
            Logger.Trace($"复制文件到自身，不执行操作：{sourceFilePath} → {destFilePath}");
        } else if (PathUtils.IsSame(sourceFilePath, destFilePath)) {
            // Paths differ only in case: equivalent to a rename
            Logger.Trace($"复制文件到自身，但大小写不同，等效于重命名文件：{sourceFilePath} → {destFilePath}");
            FileUtils.Move(sourceFilePath, destFilePath);
        } else {
            // The actual copy
            DirectoryUtils.Create(PathUtils.RemoveLastPart(destFilePath));
            Logger.Trace($"复制文件：{sourceFilePath} → {destFilePath}");
            if (FileUtils.Exists(destFilePath)) FileUtils.SetReadOnly(destFilePath, false);
            Retrier.Attempt(delay: _ => TimeSpan.FromMilliseconds(200), isRetryAllowed: ex => ex is IOException, action: _ 
                => File.Copy(PathUtils.ForApi(sourceFilePath), PathUtils.ForApi(destFilePath), true));
        }
    }

    /// <summary>
    /// Moves a file.
    /// Creates the target folder and overwrites any existing file.
    /// If <paramref name="toRecycleBin"/> is set, the old file is moved to the recycle bin before being overwritten.
    /// </summary>
    public static void Move(string sourceFilePath, string destFilePath, bool toRecycleBin = false) {
        sourceFilePath = PathUtils.ForCompare(sourceFilePath);
        destFilePath = PathUtils.ForCompare(destFilePath);
        if (string.CompareOrdinal(sourceFilePath, destFilePath) == 0) {
            // Moving onto itself: do nothing
            Logger.Trace($"剪切文件到自身，不执行操作：{sourceFilePath} → {destFilePath}");
        } else if (PathUtils.IsSame(sourceFilePath, destFilePath)) {
            // Paths differ only in case
            Logger.Trace($"剪切文件到自身，但大小写不同：{sourceFilePath} → {destFilePath}");
            Retrier.Attempt(delay: _ => TimeSpan.FromMilliseconds(200), isRetryAllowed: ex => ex is IOException, action: _ => {
                var temp = Path.Combine(PathUtils.RemoveLastPart(sourceFilePath), Path.GetRandomFileName());
                FileUtils.SetReadOnly(sourceFilePath, false);
                if (FileUtils.Exists(destFilePath)) FileUtils.SetReadOnly(destFilePath, false);
                File.Move(PathUtils.ForApi(sourceFilePath), PathUtils.ForApi(temp));
                File.Move(PathUtils.ForApi(temp), PathUtils.ForApi(destFilePath));
            });
        } else {
            // The actual move
            DirectoryUtils.Create(PathUtils.RemoveLastPart(destFilePath));
            FileUtils.Delete(destFilePath, toRecycleBin);
            Logger.Trace($"剪切文件：{sourceFilePath} → {destFilePath}");
            Retrier.Attempt(delay: _ => TimeSpan.FromMilliseconds(200), isRetryAllowed: ex => ex is IOException, action: _ => {
                FileUtils.SetReadOnly(sourceFilePath, false);
                if (FileUtils.Exists(destFilePath)) FileUtils.SetReadOnly(destFilePath, false);
                File.Move(PathUtils.ForApi(sourceFilePath), PathUtils.ForApi(destFilePath));
            });
        }
    }

    #endregion

    #region Deletion

    /// <summary>
    /// Deletes a file.
    /// <para/>If <paramref name="toRecycleBin"/> is set, tries to move it to the recycle bin, falling back to permanent deletion on failure.
    /// </summary>
    /// <returns>
    /// <see langword="null"/> if the file does not exist.
    /// <para/><see langword="true"/> if it was moved to the recycle bin.
    /// <para/><see langword="false"/> if it was permanently deleted.
    /// </returns>
    public static bool? Delete(string filePath, bool toRecycleBin = false) {
        if (!FileUtils.Exists(filePath)) return null;
        Logger.Trace($"{(toRecycleBin ? "将文件删除到回收站" : "删除文件")}：{filePath}");
        // Move to the recycle bin
        if (toRecycleBin) {
            try {
                DeleteToRecycleBin(filePath);
                return true;
            } catch (Exception ex) {
                Logger.Warn(ex, $"无法将文件删除到回收站，回退到永久删除：{filePath}");
            }
        }
        // Permanent deletion
        Retrier.Attempt(delay: _ => TimeSpan.FromMilliseconds(200), isRetryAllowed: ex => ex is IOException, action: _ => {
            FileUtils.SetReadOnly(filePath, false);
            File.Delete(PathUtils.ForApi(filePath));
        });
        return false;
    }

    /// <summary>
    /// Moves a file or folder to the recycle bin.
    /// </summary>
    internal static void DeleteToRecycleBin(string target) {
        // The actual deletion method
        void Run() {
            IShellItem? item = null;
            IFileOperation? op = null;
            try {
                var iid = typeof(IShellItem).GUID;
                Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(PathUtils.RemoveExtendedPrefix(target), IntPtr.Zero, ref iid, out item));
                // ReSharper disable once SuspiciousTypeConversion.Global
                op = (IFileOperation) new FileOperation();
                op.SetOperationFlags(0x0040 | 0x0010 | 0x0004); // FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
                op.DeleteItem(item, IntPtr.Zero);
                op.PerformOperations();
                op.GetAnyOperationsAborted(out bool aborted);
                if (aborted) throw new OperationCanceledException("Delete operation was aborted.");
            } finally {
                if (op != null && Marshal.IsComObject(op)) Marshal.FinalReleaseComObject(op);
                if (item != null && Marshal.IsComObject(item)) Marshal.FinalReleaseComObject(item);
            }
        }
        // Run the deletion on an STA thread
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA) {
            Retrier.Attempt(delay: _ => TimeSpan.FromMilliseconds(200), isRetryAllowed: ex => ex is IOException, action: _ => Run());
        } else {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo? internalEx = null; // Captures the inner exception
            var thread = new Thread(() => { 
                try { 
                    Retrier.Attempt(delay: _ => TimeSpan.FromMilliseconds(200), isRetryAllowed: ex => ex is IOException or COMException, action: _ => Run()); 
                } catch (Exception ex) { 
                    internalEx = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex); 
                } 
            }) { IsBackground = true, Name = nameof(DeleteToRecycleBin) };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            internalEx?.Throw();
        }
    }
    // API
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem { }
    [ComImport, Guid("3AD05575-8857-4850-9277-11B85BDB8E09")]
    class FileOperation { }
    [ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileOperation {
        void Advise(IntPtr pfops, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOperationFlags(uint dwOperationFlags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string pszMessage);
        void SetProgressDialog(IntPtr popd);
        void SetProperties(IntPtr pproparray);
        void SetOwnerWindow(IntPtr hwndOwner);
        void ApplyPropertiesToItem(IShellItem psiItem);
        void ApplyPropertiesToItems(IntPtr punkItems);
        void RenameItem(IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IntPtr pfopsItem);
        void RenameItems(IntPtr pUnkItems, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
        void MoveItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IntPtr pfopsItem);
        void MoveItems(IntPtr punkItems, IShellItem psiDestinationFolder);
        void CopyItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszCopyName, IntPtr pfopsItem);
        void CopyItems(IntPtr punkItems, IShellItem psiDestinationFolder);
        void DeleteItem(IShellItem psiItem, IntPtr pfopsItem);
        void DeleteItems(IntPtr punkItems);
        void NewItem(IShellItem psiDestinationFolder, uint dwFileAttributes, [MarshalAs(UnmanagedType.LPWStr)] string pszName, [MarshalAs(UnmanagedType.LPWStr)] string pszTemplateName, IntPtr pfopsItem);
        void PerformOperations();
        void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool pfAnyOperationsAborted);
    }

    #endregion

    #region Compression / extraction

    /// <summary>
    /// Opens a zip file in read-only mode.
    /// Tries UTF8 encoding first, then falls back to GB18030.
    /// </summary>
    public static ZipArchive OpenZip(string zipFilePath, CancellationToken c = default) {
        ZipArchive TryOpen(Encoding encoding) {
            Logger.Trace($"尝试以 {encoding.EncodingName} 编码打开压缩包：{zipFilePath}");
            var result = ZipFile.Open(PathUtils.ForApi(zipFilePath), ZipArchiveMode.Read, encoding);
            try {
                _ = result.Entries; // Throws DecoderFallbackException here if the encoding is wrong, or InvalidDataException if the file is corrupted
                return result;
            } catch {
                result.Dispose();
                throw;
            }
        }
        return Retrier.Attempt(delay: _ => TimeSpan.FromMilliseconds(200), maxAttempts:10, isRetryAllowed: ex => ex is IOException, cancellationToken: c, func: _ => {
            try {
                try { // Try both encodings
                    return TryOpen(new UTF8Encoding(false, true));
                } catch (DecoderFallbackException) {
                    return TryOpen(Encoding.GetEncoding("GB18030"));
                }
            } catch (InvalidDataException ex) {
                throw new InvalidDataException($"文件不是压缩包，或者文件已损坏（{zipFilePath}）", ex);
            }
        });
        
    }

    /// <summary>
    /// Detects the archive type by extension and extracts it; supports gz and zip, and tries to extract jar as zip.
    /// Creates folders automatically. Overwrites existing files but does not delete extra files.
    /// </summary>
    /// <param name="p">The argument is the completed fraction (0~1).</param>
    public static void ExtractToDirectory(string compressionFile, string outputDirectory, CancellationToken c = default, ProgressProvider? p = null) {
        compressionFile = PathUtils.ForApi(compressionFile);
        DirectoryUtils.Create(outputDirectory);
        // Extract gz (gz has no encoding concerns)
        if (PathUtils.GetExtension(compressionFile) == "gz") {
            string outFilePath = Path.Combine(outputDirectory, PathUtils.GetFileNameWithoutExtension(compressionFile));
            Logger.Trace($"解压 gz 文件：{compressionFile} → {outFilePath}");
            using var fileStream = FileUtils.ReadAsStream(compressionFile);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            FileUtils.Write(outFilePath, gzipStream);
            p?.Finish();
            return;
        }
        // Extract zip
        using var archive = FileUtils.OpenZip(compressionFile, c);
        var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList(); // Skip folder entries (ZipArchive also lists folders as entries, but their Name is empty)
        int totalCount = entries.Count;
        int doneCount = 0;
        Logger.Trace($"解压 zip 文件：{compressionFile} → {outputDirectory}（共 {totalCount} 项）");
        foreach (var entry in entries) {
            c.ThrowIfCancellationRequested();
            doneCount++;
            if (totalCount > 0) p?.Set((double) doneCount / totalCount);
            // ZipSlip fix
            string outputFilePath = Path.Combine(outputDirectory, entry.FullName);
            if (!PathUtils.IsParentOf(outputDirectory, outputFilePath))
                throw new InvalidDataException($"Zip 文件项 {entry.FullName} 的路径在压缩包之外，这可能导致安全问题");
            // The actual extraction
            using var entryStream = entry.Open();
            FileUtils.Write(outputFilePath, entryStream);
        }
    }

    /// <summary>
    /// Packs the contents of the specified folder into a zip file.
    /// Creates folders automatically. Overwrites existing files.
    /// </summary>
    public static void CreateZipFromDirectory(string outputFullPath, string sourceDirectory) {
        DirectoryUtils.Create(PathUtils.RemoveLastPart(outputFullPath));
        FileUtils.Delete(outputFullPath);
        Logger.Trace($"将文件夹中的内容压缩为 zip 文件：{sourceDirectory} → {outputFullPath}");
        Retrier.Attempt(delay: _ => TimeSpan.FromMilliseconds(200), isRetryAllowed: ex => ex is IOException, action: _ 
            => ZipFile.CreateFromDirectory(PathUtils.ForApi(sourceDirectory), PathUtils.ForApi(outputFullPath)));
    }

    /// <summary>
    /// Packs multiple files into a zip file, all placed at the root of the archive.
    /// Creates folders automatically. Overwrites existing files.
    /// </summary>
    public static void CreateZipFromFiles(string outputFullPath, params string[] sourceFiles) {
        var sources = new Dictionary<string, string>();
        foreach (var source in sourceFiles) {
            string fileName = PathUtils.GetLastPart(source);
            if (sources.ContainsKey(fileName)) throw new ArgumentException($"尝试将多个同文件名的文件放进压缩包中（{fileName}）", nameof(sourceFiles));
            sources.Add(fileName, source);
        }
        FileUtils.CreateZipFromFiles(outputFullPath, sources);
    }

    /// <summary>
    /// Packs multiple files into a zip file.
    /// Creates folders automatically. Overwrites existing files.
    /// </summary>
    /// <param name="sources">Keys are paths inside the zip; values are local file paths.</param>
    public static void CreateZipFromFiles(string outputFullPath, IDictionary<string, string> sources) {
        DirectoryUtils.Create(PathUtils.RemoveLastPart(outputFullPath));
        FileUtils.Delete(outputFullPath);
        using var archive = Retrier.Attempt(delay: _ => TimeSpan.FromMilliseconds(200), isRetryAllowed: ex => ex is IOException, func: _ 
            => ZipFile.Open(PathUtils.ForApi(outputFullPath), ZipArchiveMode.Create));
        Logger.Trace($"创建 zip 文件：{sources.Count} 个文件 → {outputFullPath}\n{sources.Select(p => $"- {p.Value} → {p.Key}").Join('\n')}");
        foreach (var pair in sources) archive.CreateEntryFromFile(PathUtils.ForApi(pair.Value), pair.Key.Replace('\\', '/'), CompressionLevel.Optimal);
    }

    #endregion

    /// <summary>
    /// Whether the specified file exists.
    /// </summary>
    public static bool Exists(string filePath) 
        => File.Exists(PathUtils.ForApi(filePath));

    /// <summary>
    /// Checks whether the file is not locked by another process
    /// <para>Returns <see langword="false"/> if the file is locked or cannot be read.</para>
    /// </summary>
    public static bool IsAvailable(string filePath) {
        if (!Exists(filePath)) return true;
        try {
            Retrier.Attempt(maxAttempts: 3, delay: _ => TimeSpan.FromMilliseconds(200), isRetryAllowed: ex => ex is IOException, action: _ => {
                using var stream = new FileStream(PathUtils.ForApi(filePath), FileMode.Open, FileAccess.Read, FileShare.None);
            });
            return true;
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            return false;
        }
    }

    /// <summary>
    /// Gets a <see cref="FileInfo"/> object.
    /// </summary>
    public static FileInfo GetInfo(string path) 
        => new(PathUtils.ForApi(path));

    /// <summary>
    /// Sets the read-only attribute of a file or folder.
    /// For a folder, only the folder's own attribute is set; files and subfolders are not changed recursively.
    /// </summary>
    public static void SetReadOnly(string path, bool readOnly) {
        path = PathUtils.ForApi(path);
        var attributes = File.GetAttributes(path);
        var newAttributes = readOnly
            ? attributes | FileAttributes.ReadOnly
            : attributes & ~FileAttributes.ReadOnly;
        if (attributes == newAttributes) return;
        Logger.Trace($"{(readOnly ? "添加" : "移除")}只读属性：{path}（原属性：{attributes}，新属性：{newAttributes}）");
        File.SetAttributes(path, newAttributes);
    }

}

/// <summary>
/// Checks whether a file meets the validation rules: size, hash, and whether it is a JSON file.
/// </summary>
public class FileChecker {

    /// <summary>
    /// The exact size of the file.
    /// <para/>-1 to skip the check.
    /// </summary>
    public long ActualSize = -1;

    /// <summary>
    /// The minimum size of the file.
    /// <para/>-1 to skip the check.
    /// </summary>
    public long MinSize = -1;

    /// <summary>
    /// The MD5, SHA1, SHA256 or SHA512 of the file. The kind is detected from the length of the string.
    /// <para/><c>null</c> to skip the check.
    /// </summary>
    public string? Hash = null;

    /// <summary>
    /// Whether the file must be a JSON file.
    /// </summary>
    public bool IsJson = false;
    
    // TODO: refactor this into a void method that throws instead
    /// <summary>
    /// Checks the file.
    /// <para/>Returns <c>null</c> on success, or an error description (not ending with a period) on failure.
    /// <para/>Never throws.
    /// </summary>
    public string? Check(string localPath) {
        try {
            var info = FileUtils.GetInfo(localPath);
            if (!info.Exists) return "文件不存在：" + localPath;

            long fileSize = info.Length;
            if (ActualSize >= 0 && ActualSize != fileSize) {
                return $"文件大小应为 {ActualSize} B，实际为 {fileSize} B" +
                    (fileSize < 2000 ? "，内容为：" + FileUtils.ReadAsString(localPath) : "");
            }
            if (MinSize >= 0 && MinSize > fileSize) {
                return $"文件大小应至少为 {MinSize} B，实际为 {fileSize} B" +
                    (fileSize < 2000 ? "，内容为：" + FileUtils.ReadAsString(localPath) : "");
            }

            string? hash = Hash;
            if (!string.IsNullOrEmpty(hash)) {
                hash = hash!.Lower();
                switch (hash.Length) {
                    case 32:
                        string md5 = CryptographyUtils.ComputeFileHash(localPath, CryptographyUtils.HashMethod.Md5);
                        if (hash != md5) return "文件 MD5 应为 " + hash + "，实际为 " + md5;
                        break;
                    case 40:
                        string sha1 = CryptographyUtils.ComputeFileHash(localPath, CryptographyUtils.HashMethod.Sha1);
                        if (hash != sha1) return "文件 SHA1 应为 " + hash + "，实际为 " + sha1;
                        break;
                    case 64:
                        string sha256 = CryptographyUtils.ComputeFileHash(localPath, CryptographyUtils.HashMethod.Sha256);
                        if (hash != sha256) return "文件 SHA256 应为 " + hash + "，实际为 " + sha256;
                        break;
                    case 128:
                        string sha512 = CryptographyUtils.ComputeFileHash(localPath, CryptographyUtils.HashMethod.Sha512);
                        if (hash != sha512) return "文件 SHA512 应为 " + hash + "，实际为 " + sha512;
                        break;
                    default:
                        return $"无法识别的 Hash 类型（长度为 {hash.Length}）：{hash}";
                }
            }

            if (IsJson) {
                string content = FileUtils.ReadAsString(localPath);
                if (content == "") return "读取到的文件为空";
                content.DeserializeJson();
            }

            return null;
        } catch (Exception ex) {
            Logger.Warn(ex, "检查文件出错");
            return ex.GetDisplay(false);
        }
    }
}
