Public Module LocalResourceLoaders

    'Load the mod list
    Public LocalResourceLoader As New LoaderTask(Of String, List(Of LocalResourceFile))("LocalResourceLoader", AddressOf LocalResourceFileLoad)
    Private Sub LocalResourceFileLoad(Loader As LoaderTask(Of String, List(Of LocalResourceFile)))
        Try
            RunInUiWait(Sub() If FrmInstanceMod IsNot Nothing Then FrmInstanceMod.Load.ShowProgress = False)

            'Wait for mod updates to finish
            If PageInstanceMod.UpdatingInstanceModFolders.Contains(Loader.Input) Then
                Logger.Info($"等待 Mod 更新完成后才能继续加载 Mod 列表：{Loader.Input}")
                Try
                    RunInUiWait(Sub() If FrmInstanceMod IsNot Nothing Then FrmInstanceMod.Load.Text = "正在更新 Mod")
                    Do Until Not PageInstanceMod.UpdatingInstanceModFolders.Contains(Loader.Input)
                        If Loader.IsCanceled Then Return
                        Thread.Sleep(100)
                    Loop
                Finally
                    RunInUiWait(Sub() If FrmInstanceMod IsNot Nothing Then FrmInstanceMod.Load.Text = "正在加载 Mod 列表")
                End Try
                FrmInstanceMod.LoaderRun(LoaderFolderRunType.UpdateOnly)
            End If

            'Get the list of usable files in the mods folder
            Dim ModFileList As New List(Of String)
            If DirectoryUtils.Exists(Loader.Input) Then
                For Each File In DirectoryUtils.EnumerateFiles(Loader.Input, True)
                    If Not PathUtils.IsSame(PathUtils.RemoveLastPart(File), Loader.Input) Then
                        'Only load mods in this subfolder if it is Forge 1.13- and the folder name equals the version number
                        If Not (PageInstanceLeft.Instance IsNot Nothing AndAlso PageInstanceLeft.Instance.Version.ModLoaders.HasFlagF(ModLoaders.Forge) AndAlso
                                PageInstanceLeft.Instance.Version.Vanilla.Major < 13 AndAlso
                                PathUtils.GetLastPart(PathUtils.RemoveLastPart(File)) = $"1.{PageInstanceLeft.Instance.Version.Vanilla.Major}.{PageInstanceLeft.Instance.Version.Vanilla.Build}") Then
                            Continue For
                        End If
                    End If
                    Static IsModFile As Func(Of String, Boolean) =
                    Function(FullName As String)
                        If FullName Is Nothing Then Return False
                        If {"disabled", "old"}.Contains(PathUtils.GetExtension(FullName)) Then FullName = PathUtils.GetFileNameWithoutExtension(FullName)
                        Return {"jar", "zip", "litemod"}.Contains(PathUtils.GetExtension(FullName))
                    End Function
                    If IsModFile(File) Then ModFileList.Add(File)
                Next
            End If

            'Get the local file cache
            Dim CachePath As String = PathTemp & "Cache\LocalMod.json"
            Dim Cache As New JObject
            Try
                Dim CacheContent As String = FileUtils.TryReadAsString(CachePath)
                If Not String.IsNullOrWhiteSpace(CacheContent) Then
                    Cache = CacheContent.DeserializeJson()
                    If Not Cache.ContainsKey("version") OrElse Cache("version").ToObject(Of Integer) <> Versions.LocalModCacheVersion Then
                        Logger.Warn($"本地 Mod 信息缓存版本已过期，将弃用这些缓存信息")
                        Cache = New JObject
                    End If
                End If
            Catch ex As Exception
                Logger.Warn(ex, "读取本地 Mod 信息缓存失败，已重置")
                Cache = New JObject
            End Try
            Cache("version") = Versions.LocalModCacheVersion

            'Load the mod list
            Dim ModList As New List(Of LocalResourceFile)
            For Each ModFile In ModFileList
                If Loader.IsCanceled Then Return
                Dim ModEntry As New LocalResourceFile(ModFile)
                Dim DumpMod As LocalResourceFile = ModList.FirstOrDefault(Function(m) m.EnabledName = ModEntry.EnabledName) 'Two files with the same name exist, one enabled and one disabled
                If DumpMod IsNot Nothing Then
                    Dim DisabledMod As LocalResourceFile = If(DumpMod.IsEnabled, ModEntry, DumpMod)
                    Logger.Warn($"重复的 Mod 文件：{DumpMod.File.Name} 与 {ModEntry.File.Name}，已忽略 {DisabledMod.File.Name}")
                    If DisabledMod Is ModEntry Then
                        Continue For
                    Else
                        ModList.Remove(DisabledMod)
                    End If
                End If
                ModList.Add(ModEntry)
            Next
            Logger.Info($"共发现 {ModList.Count} 个 Mod")

            'Sort
            ModList = ModList.OrderBy(Function(m) m.File.Name).ToList

            'Write back
            If Loader.IsCanceled Then Return
            Loader.Output = ModList

            'Start loading online info
            'TODO: show a hint while info is being fetched
            LocalResourceOnlineLoader.Start((ModList, Cache, PageInstanceLeft.Instance.Version), IsForceRestart:=True)

        Catch ex As Exception
            Logger.Warn(ex, "Mod 列表加载失败")
            Throw
        End Try
    End Sub

    'Load mod details online
    Public LocalResourceOnlineLoader As New LoaderTask(Of (ModList As List(Of LocalResourceFile), Cache As JObject, Target As McVersion), Integer)("LocalResourceOnlineLoader", AddressOf LocalResourceOnlineLoad)
    Private Sub LocalResourceOnlineLoad(Loader As LoaderTask(Of (ModList As List(Of LocalResourceFile), Cache As JObject, Target As McVersion), Integer))
        Dim Mods As New List(Of LocalResourceFile)
        Dim Cache As JObject = Loader.Input.Cache
        Dim ModLoaders = Loader.Input.Target.ModLoaders
        'Read the cache and get the list of mods to update
        For Each ModEntry As LocalResourceFile In Loader.Input.ModList
            If Loader.IsCanceled Then Return
            Dim CacheKey = ModEntry.ModrinthHash & Loader.Input.Target.VanillaName & ModLoaders
            If Cache.ContainsKey(CacheKey) Then
                ModEntry.FromJson(Cache(CacheKey))
                'No need to refetch if the cached info was updated within 6 hours
                If ModEntry.OnlineDataLoaded AndAlso Date.Now - Cache(CacheKey)("Project")("CacheTime").ToObject(Of Date) < New TimeSpan(6, 0, 0) Then Continue For
            End If
            Mods.Add(ModEntry)
        Next
        Logger.Info($"有 {Mods.Where(Function(m) m.Project Is Nothing).Count} 个 Mod 需要联网获取信息，{Mods.Where(Function(m) m.Project IsNot Nothing).Count} 个 Mod 需要更新信息")
        If Not Mods.Any Then Return
        'Get the version to check against
        'Do not extend the check down to older MC minor versions here: e.g. if a mod released a patch for 1.16.2 after updating to 1.16.5, PCL would downgrade the 1.16.5 mod to 1.16.2
        Dim TargetMcVersion As McVersion = Loader.Input.Target
        Dim VanillaVersion = TargetMcVersion.VanillaName
        'Start fetching online
        Logger.Info($"正在检查 Mod 信息的版本：{VanillaVersion}，加载器 {ModLoaders}")
        Dim EndedThreadCount As Integer = 0, IsFailed As Boolean = False
        Dim CurrentTaskThread As Thread = Thread.CurrentThread
        'Fetch info from Modrinth
        RunInNewThread(
        Sub()
            Try
                'Step 1: get the hashes and the corresponding project IDs
                Dim ModrinthHashes = Mods.Select(Function(m) m.ModrinthHash).ToList()
                Dim ModrinthVersion As JObject = DlModRequest("https://api.modrinth.com/v2/version_files", HttpMethod.Post,
                    $"{{""hashes"": [""{ModrinthHashes.Join(""",""")}""], ""algorithm"": ""sha1""}}", "application/json")
                Logger.Info($"从 Modrinth 获取到 {ModrinthVersion.Count} 个本地 Mod 的对应信息")
                'Step 2: try to read the project info cache and map the other mods
                If ModrinthVersion.Count = 0 Then Return
                Dim ModrinthMapping As New Dictionary(Of String, List(Of LocalResourceFile))
                Dim ModrinthLoaders As New Dictionary(Of LocalResourceFile, List(Of String))
                For Each Entry In Mods
                    If Not ModrinthVersion.ContainsKey(Entry.ModrinthHash) Then Continue For
                    If ModrinthVersion(Entry.ModrinthHash)("files")(0)("hashes")("sha1") <> Entry.ModrinthHash Then Continue For
                    Dim ProjectId = ModrinthVersion(Entry.ModrinthHash)("project_id").ToString
                    If ResourceProject.Cache.ContainsKey(ProjectId) AndAlso Entry.Project Is Nothing Then Entry.Project = ResourceProject.Cache(ProjectId) 'Read the already loaded cache to show results faster
                    If Not ModrinthMapping.ContainsKey(ProjectId) Then ModrinthMapping(ProjectId) = New List(Of LocalResourceFile)
                    ModrinthMapping(ProjectId).Add(Entry)
                    'Record the corresponding ProjectVersion
                    Dim File = ResourceVersion.FromPlatformJson(ModrinthVersion(Entry.ModrinthHash), ResourceTypes.Mod)
                    ModrinthLoaders(Entry) = ModrinthVersion(Entry.ModrinthHash)("loaders").
                        Select(Function(l) l.ToString.Lower).Distinct.OrderBy(Function(l) l).ToList
                    If Entry.ProjectVersion Is Nothing OrElse Entry.ProjectVersion.ReleaseDate < File.ReleaseDate Then
                        Entry.ProjectVersion = File
                    Else
                        Entry.ProjectVersion.Version = File.Version 'Use the version number from Modrinth
                    End If
                Next
                If Loader.IsCanceledWithThread(CurrentTaskThread) Then Return
                Logger.Info($"需要从 Modrinth 获取 {ModrinthMapping.Count} 个本地 Mod 的工程信息")
                'Step 3: get project info
                If Not ModrinthMapping.Any() Then Return
                Dim ModrinthProject As JArray = DlModRequest(
                    $"https://api.modrinth.com/v2/projects?ids=[""{ModrinthMapping.Keys.Join(""",""")}""]")
                For Each ProjectJson In ModrinthProject
                    Dim Project As New ResourceProject(ProjectJson)
                    For Each Entry In ModrinthMapping(Project.Id)
                        Entry.Project = Project
                    Next
                Next
                Logger.Info($"已从 Modrinth 获取本地 Mod 信息，继续获取更新信息")
                'Step 4: get update info
                Dim ModsToUpdate = ModrinthMapping.SelectMany(Function(l) l.Value).ToList
                If Not ModsToUpdate.Any Then Return
                Dim UpdateData As String = ModsToUpdate.
                    Select(Function(m) $"{{""hash"": ""{m.ModrinthHash}"", ""loaders"": [""{ModrinthLoaders(m).Join(""",""")}""], ""game_versions"": [""{VanillaVersion}""]}}").
                    Join(",")
                Dim ModrinthUpdate As JObject = DlModRequest("https://api.modrinth.com/v2/version_files/update_individual", HttpMethod.Post,
                    $"{{""algorithm"": ""sha1"", ""hashes"": [{UpdateData}]}}", "application/json")
                For Each Entry In Mods
                    If Not ModrinthUpdate.ContainsKey(Entry.ModrinthHash) OrElse Entry.ProjectVersion Is Nothing Then Continue For
                    Dim UpdateFile = ResourceVersion.FromPlatformJson(ModrinthUpdate(Entry.ModrinthHash), ResourceTypes.Mod)
                    If Not UpdateFile.DownloadAvailable Then Continue For
                    Dim UpdateLoaders = ModrinthUpdate(Entry.ModrinthHash)("loaders").
                        Select(Function(l) l.ToString.Lower).Distinct.OrderBy(Function(l) l).ToList
                    If Not UpdateLoaders.SequenceEqual(ModrinthLoaders(Entry)) Then Continue For
                    Logger.Trace(Function() $"本地文件 {Entry.ProjectVersion.FileName} 在 Modrinth 上的最新版为 {UpdateFile.FileName}")
                    If Entry.ProjectVersion.ReleaseDate >= UpdateFile.ReleaseDate OrElse Entry.ProjectVersion.Hash = UpdateFile.Hash Then Continue For
                    'Set the changelog and update file
                    If Entry.UpdateFile IsNot Nothing AndAlso UpdateFile.Hash = Entry.UpdateFile.Hash Then 'Merge
                        Entry.ChangelogUrls.Add($"https://modrinth.com/mod/{ModrinthUpdate(Entry.ModrinthHash)("project_id")}/changelog?g={VanillaVersion}")
                        UpdateFile.DownloadUrls.AddRange(Entry.UpdateFile.DownloadUrls) 'Merge download sources
                        Entry.UpdateFile = UpdateFile 'Prefer the Modrinth file
                    ElseIf Entry.UpdateFile Is Nothing OrElse UpdateFile.ReleaseDate >= Entry.UpdateFile.ReleaseDate Then 'Replace
                        Entry.ChangelogUrls = New List(Of String) From {$"https://modrinth.com/mod/{ModrinthUpdate(Entry.ModrinthHash)("project_id")}/changelog?g={VanillaVersion}"}
                        Entry.UpdateFile = UpdateFile
                    End If
                Next
                Logger.Info($"从 Modrinth 获取本地 Mod 信息结束")
            Catch ex As Exception
                Logger.Warn(ex, "从 Modrinth 获取本地 Mod 信息失败")
                IsFailed = True
            Finally
                EndedThreadCount += 1
            End Try
        End Sub, "Mod List Detail Loader Modrinth")
        'Fetch info from CurseForge
        RunInNewThread(
        Sub()
            Try
                'Step 1: get the hashes and the corresponding project IDs
                Dim Hashes As New List(Of UInteger)
                For Each Entry In Mods
                    Hashes.Add(Entry.CurseForgeHash)
                    If Loader.IsCanceledWithThread(CurrentTaskThread) Then Return
                Next
                Dim CurseForgeRaw As JContainer = DlModRequest("https://api.curseforge.com/v1/fingerprints/432", HttpMethod.Post,
                    $"{{""fingerprints"": [{Hashes.Join(",")}]}}", "application/json")("data")("exactMatches")
                Logger.Info($"从 CurseForge 获取到 {CurseForgeRaw.Count} 个本地 Mod 的对应信息")
                If Not CurseForgeRaw.Any() Then Return

                'Step 2: try to read the project info cache and map the other mods
                Dim ProjectIdToLocalFiles As New Dictionary(Of Integer, List(Of LocalResourceFile))
                Dim CurseForgeLoaders As New Dictionary(Of LocalResourceFile, ModLoaders)
                For Each Project In CurseForgeRaw
                    Dim ProjectId = Project("id").ToString
                    Dim Hash As UInteger = Project("file")("fileFingerprint")
                    For Each Entry In Mods
                        If Entry.CurseForgeHash <> Hash Then Continue For
                        If ResourceProject.Cache.ContainsKey(ProjectId) AndAlso Entry.Project Is Nothing Then Entry.Project = ResourceProject.Cache(ProjectId) 'Read the already loaded cache to show results faster
                        ProjectIdToLocalFiles.AddIntoValueCollection(ProjectId, Entry)
                        'Record the corresponding ProjectVersion
                        Dim File = ResourceVersion.FromPlatformJson(Project("file"), ResourceTypes.Mod)
                        CurseForgeLoaders(Entry) = File.ModLoaders
                        If Entry.ProjectVersion Is Nothing OrElse Entry.ProjectVersion.ReleaseDate < File.ReleaseDate Then Entry.ProjectVersion = File
                    Next
                Next
                If Loader.IsCanceledWithThread(CurrentTaskThread) Then Return
                Logger.Info($"需要从 CurseForge 获取 {ProjectIdToLocalFiles.Count} 个本地 Mod 的工程信息")
                If Not ProjectIdToLocalFiles.Any() Then Return

                'Step 3: get project info
                Dim CurseForgeProject = DlModRequest("https://api.curseforge.com/v1/mods", HttpMethod.Post,
                    $"{{""modIds"": [{ProjectIdToLocalFiles.Keys.Join(",")}]}}", "application/json")("data")
                Dim UpdateFileIds As New Dictionary(Of Integer, List(Of LocalResourceFile)) 'FileId -> list of local mod files
                Dim FileIdToProjectSlug As New Dictionary(Of Integer, String)
                For Each ProjectJson In CurseForgeProject
                    If ProjectJson("isAvailable") IsNot Nothing AndAlso Not ProjectJson("isAvailable").ToObject(Of Boolean) Then Continue For
                    'Set the project info in the entry
                    Dim Project As New ResourceProject(ProjectJson)
                    For Each LocalFile In ProjectIdToLocalFiles(Project.Id) 'Reverse lookup in case CurseForge's response is missing items
                        If LocalFile.Project?.Platform = ResourcePlatforms.Modrinth Then
                            LocalFile.Project = LocalFile.Project 'Raise the change event again
                        Else
                            LocalFile.Project = Project
                        End If
                        'Find files that may have version updates
                        Dim LatestFiles = ProjectJson("latestFilesIndexes").Where(
                        Function(Entry)
                            'Match the mod loader
                            Dim EntryLoader As ModLoaders = If(Entry("modLoader") Is Nothing, ModLoaders.None,
                                Resource.FromCurseForgeModLoaderType(Entry("modLoader").ToObject(Of Integer)))
                            If CurseForgeLoaders(LocalFile) = ModLoaders.None Then
                                If EntryLoader <> ModLoaders.None Then Return False
                            ElseIf Not CurseForgeLoaders(LocalFile).HasFlagF(EntryLoader) Then
                                Return False
                            End If
                            'Match the MC version
                            Dim EntryVersion As String = Entry("gameVersion")
                            Return EntryVersion IsNot Nothing AndAlso EntryVersion = VanillaVersion
                        End Function).
                        MaxByAll(Function(Entry) Entry("gameVersion").ToString, Comparer(Of String).Create(Function(x, y) McUtils.CompareVersion(x, y))) 'Keep only the latest MC version
                        For Each Entry In LatestFiles
                            Dim FileId = Entry("fileId").ToObject(Of Integer)
                            If Not UpdateFileIds.ContainsKey(FileId) Then UpdateFileIds(FileId) = New List(Of LocalResourceFile)
                            UpdateFileIds(FileId).Add(LocalFile)
                            FileIdToProjectSlug(FileId) = Project.Slug
                        Next
                    Next
                Next
                Logger.Info($"已从 CurseForge 获取本地 Mod 信息，需要获取 {UpdateFileIds.Count} 个用于检查更新的文件信息")
                If Not UpdateFileIds.Any() Then Return

                'Step 4: get update file info
                Dim CurseForgeFiles = DlModRequest("https://api.curseforge.com/v1/mods/files", HttpMethod.Post,
                                    $"{{""fileIds"": [{UpdateFileIds.Keys.Join(",")}]}}", "application/json")("data")
                Dim UpdateFiles As New Dictionary(Of LocalResourceFile, ResourceVersion)
                For Each FileJson In CurseForgeFiles
                    Dim File = ResourceVersion.FromPlatformJson(FileJson, ResourceTypes.Mod)
                    If Not File.DownloadAvailable Then Continue For
                    For Each Entry As LocalResourceFile In UpdateFileIds(File.Id)
                        If File.ModLoaders <> CurseForgeLoaders(Entry) Then
                            Logger.Warn($"从 CurseForge 获取到的更新文件 {File.FileName} 加载器为 {File.ModLoaders}，与本地文件 {Entry.File.Name} 的加载器 {CurseForgeLoaders(Entry)} 不一致，已忽略", LogBehavior.None)
                            Continue For
                        End If
                        If UpdateFiles.ContainsKey(Entry) AndAlso UpdateFiles(Entry).ReleaseDate >= File.ReleaseDate Then Continue For
                        UpdateFiles(Entry) = File
                    Next
                Next
                For Each Pair In UpdateFiles
                    Dim Entry As LocalResourceFile = Pair.Key
                    Dim UpdateFile As ResourceVersion = Pair.Value
                    Logger.Trace(Function() $"本地文件 {Entry.ProjectVersion.FileName} 在 CurseForge 上的最新版为 {UpdateFile.FileName}")
                    If Entry.ProjectVersion.ReleaseDate >= UpdateFile.ReleaseDate OrElse Entry.ProjectVersion.Hash = UpdateFile.Hash Then Continue For
                    'Set the changelog and update file
                    If Entry.UpdateFile IsNot Nothing AndAlso UpdateFile.Hash = Entry.UpdateFile.Hash Then 'Merge
                        Entry.ChangelogUrls.Add($"https://www.curseforge.com/minecraft/mc-mods/{FileIdToProjectSlug(UpdateFile.Id)}/files/{UpdateFile.Id}")
                        Entry.UpdateFile.DownloadUrls.AddRange(UpdateFile.DownloadUrls) 'Merge download sources
                    ElseIf Entry.UpdateFile Is Nothing OrElse UpdateFile.ReleaseDate > Entry.UpdateFile.ReleaseDate Then 'Replace
                        Entry.ChangelogUrls = New List(Of String) From {$"https://www.curseforge.com/minecraft/mc-mods/{FileIdToProjectSlug(UpdateFile.Id)}/files/{UpdateFile.Id}"}
                        Entry.UpdateFile = UpdateFile
                    End If
                Next

                Logger.Info($"从 CurseForge 获取 Mod 更新信息结束")
            Catch ex As Exception
                Logger.Warn(ex, "从 CurseForge 获取本地 Mod 信息失败")
                IsFailed = True
            Finally
                EndedThreadCount += 1
            End Try
        End Sub, "Mod List Detail Loader CurseForge")
        'Wait for the threads to finish
        Do Until EndedThreadCount = 2
            Thread.Sleep(10)
            If Loader.IsCanceled Then Return
        Loop
        'Save the cache
        Mods = Mods.Where(Function(m) m.Project IsNot Nothing).ToList()
        Logger.Info($"联网获取本地 Mod 信息完成，为 {Mods.Count} 个 Mod 更新缓存")
        If Not Mods.Any() Then Return
        If Loader.IsCanceled Then Return
        For Each Entry In Mods
            Entry.OnlineDataLoaded = Not IsFailed
            Cache(Entry.ModrinthHash & VanillaVersion & ModLoaders) = Entry.ToJson()
        Next
        FileUtils.Write(PathTemp & "Cache\LocalMod.json", Cache.ToString(If(ModeDebug, Newtonsoft.Json.Formatting.Indented, Newtonsoft.Json.Formatting.None)))
        'Refresh the sidebar
        If Loader.IsCanceled Then Return
        If FrmInstanceMod?.Filter = PageInstanceMod.FilterType.CanUpdate Then
            RunInUi(Sub() FrmInstanceMod?.RefreshUI()) 'Sync the "updatable" list (#4677)
        Else
            RunInUi(Sub() FrmInstanceMod?.RefreshBars())
        End If
    End Sub

End Module
