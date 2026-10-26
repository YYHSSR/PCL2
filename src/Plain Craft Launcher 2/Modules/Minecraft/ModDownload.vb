Public Module ModDownload

#Region "DlClient* | Minecraft client"

    ''' <summary>
    ''' Returns download info for the vanilla main jar of a Minecraft version. The required dependency versions must already exist.
    ''' Throws on failure; returns Nothing if no download is needed.
    ''' </summary>
    Public Function DlClientJarGet(Instance As McInstance, ReturnNothingOnFileUseable As Boolean) As NetFile
        'Get the base inherited version
        Try
            Do While Not String.IsNullOrEmpty(Instance.InheritName)
                Instance = New McInstance(Instance.InheritName)
            Loop
        Catch ex As Exception
            Logger.Warn(ex, "获取底层继承版本失败")
        End Try
        'Check whether the JSON is standard
        If Instance.JsonObject("downloads") Is Nothing OrElse Instance.JsonObject("downloads")("client") Is Nothing OrElse Instance.JsonObject("downloads")("client")("url") Is Nothing Then
            Throw New Exception("底层版本 " & Instance.Name & " 中无 jar 文件下载信息")
        End If
        'Check the file
        Dim Checker As New FileChecker With {.MinSize = 1024, .ActualSize = If(Instance.JsonObject("downloads")("client")("size"), -1), .Hash = Instance.JsonObject("downloads")("client")("sha1")}
        If ReturnNothingOnFileUseable AndAlso Checker.Check(Instance.PathVersion & Instance.Name & ".jar") Is Nothing Then Return Nothing 'Passed verification
        'Return the download info
        Dim JarUrl As String = Instance.JsonObject("downloads")("client")("url")
        Return New NetFile(DlSourceLauncherOrMetaGet(JarUrl), Instance.PathVersion & Instance.Name & ".jar", Checker)
    End Function

    ''' <summary>
    ''' Returns download info for the vanilla main AssetIndex file of a Minecraft version. The required dependency versions must already exist.
    ''' If not found, returns the Legacy asset index or Nothing.
    ''' </summary>
    Public Function DlClientAssetIndexGet(Instance As McInstance) As NetFile
        'Get the base inherited version
        Do While Not String.IsNullOrEmpty(Instance.InheritName)
            Instance = New McInstance(Instance.InheritName)
        Loop
        'Get the info
        Dim IndexInfo = McAssetsGetIndex(Instance, True, True)
        Dim IndexAddress As String = McFolderSelected & "assets\indexes\" & IndexInfo("id").ToString & ".json"
        Logger.Info($"版本 {Instance.Name} 对应的资源文件索引为 {IndexInfo("id")}")
        Dim IndexUrl As String = If(IndexInfo("url"), "")
        If IndexUrl = "" Then
            Return Nothing
        Else
            Return New NetFile(DlSourceLauncherOrMetaGet(IndexUrl), IndexAddress, New FileChecker With {.IsJson = True})
        End If
    End Function

    ''' <summary>
    ''' Builds the list of loaders that complete all files of a Minecraft version. Throws on failure.
    ''' </summary>
    Public Function DlClientFix(Instance As McInstance, CheckAssetsHash As Boolean, DownloadAssetIndexInBackground As Boolean) As List(Of LoaderBase)
        Dim Loaders As New List(Of LoaderBase)

#Region "Download library files"
        Dim LoadersLib As New List(Of LoaderBase) From {
            New LoaderTask(Of String, List(Of NetFile))("分析缺失支持库文件", Sub(Task As LoaderTask(Of String, List(Of NetFile))) Task.Output = McLibNetFilesFromInstance(Instance)) With {.ProgressWeight = 1},
            New LoaderDownload("下载支持库文件", New List(Of NetFile)) With {.ProgressWeight = 15}
        }
        'Build the loader
        Loaders.Add(New LoaderCombo(Of String)("下载支持库文件（主加载器）", LoadersLib) With {.Block = False, .Show = False, .ProgressWeight = 16})
#End Region

#Region "Download asset files"
        If ShouldIgnoreFileCheck(Instance) Then
            Logger.Warn("设置要求跳过所有 Assets 检查，文件可能不是最新的", LogBehavior.None)
        Else
            Dim LoadersAssets As New List(Of LoaderBase)
            'Get the asset index URL
            Dim TempAddressMain As String = Nothing
            Dim RealAddressMain As String = Nothing
            LoadersAssets.Add(New LoaderTask(Of String, List(Of NetFile))("分析资源文件索引地址",
            Sub(Task As LoaderTask(Of String, List(Of NetFile)))
                Try
                    Dim IndexFile = DlClientAssetIndexGet(Instance)
                    If IndexFile Is Nothing Then
                        Task.Output = New List(Of NetFile)
                        Logger.Warn($"未找到版本 {Instance.Name} 的合适的资源索引下载地址，游戏 assets 可能缺失")
                        Return
                    End If
                    Dim IndexBackgroundFile = PathTemp & "Cache\" & IndexFile.LocalName & "_background"
                    If FileUtils.Exists(IndexBackgroundFile) AndAlso
                       FileUtils.GetInfo(IndexBackgroundFile).LastWriteTime.Date > FileUtils.GetInfo(IndexFile.LocalPath).LastWriteTime.Date Then
                        Logger.Info($"发现后台更新的资源索引缓存文件：{IndexBackgroundFile}")
                        FileUtils.Move(IndexBackgroundFile, IndexFile.LocalPath)
                    End If
                    If DownloadAssetIndexInBackground Then
                        'Allow updating the index in the background
                        If IndexFile.Check.Check(IndexFile.LocalPath) Is Nothing Then
                            Task.Output = New List(Of NetFile)
                        Else
                            Task.Output = New List(Of NetFile) From {IndexFile}
                        End If
                    Else
                        'Force re-download
                        RealAddressMain = IndexFile.LocalPath
                        TempAddressMain = PathTemp & "Cache\" & IndexFile.LocalName
                        IndexFile.LocalPath = TempAddressMain
                        Task.Output = New List(Of NetFile) From {IndexFile}
                    End If
                Catch ex As Exception
                    Throw New Exception("分析资源文件索引地址失败", ex)
                End Try
            End Sub) With {.ProgressWeight = 0.5, .Show = False})
            'Download the asset index
            LoadersAssets.Add(New LoaderDownload("下载资源文件索引", New List(Of NetFile)) With {.ProgressWeight = 2})
            'Get the asset file URLs
            LoadersAssets.Add(New LoaderTask(Of String, List(Of NetFile))("获取资源文件列表",
            Sub(Task)
                If TempAddressMain IsNot Nothing Then FileUtils.Move(TempAddressMain, RealAddressMain)
                Task.Output = McAssetsFixList(Instance, CheckAssetsHash, Task)
            End Sub) With {.ProgressWeight = 0.01})
            'Download asset files
            LoadersAssets.Add(New LoaderDownload("下载资源文件", New List(Of NetFile)) With {.ProgressWeight = 25})
            'Build the loader
            Loaders.Add(New LoaderCombo(Of String)("下载资源文件（主加载器）", LoadersAssets) With {.Block = False, .Show = False, .ProgressWeight = 27})

            'Update the index independently in the background
            If DownloadAssetIndexInBackground Then
                Dim RealAddressBg As String = Nothing
                Dim TempAddressBg As String = Nothing
                Dim LoadersAssetsUpdate As New List(Of LoaderBase)
                LoadersAssetsUpdate.Add(New LoaderTask(Of String, List(Of NetFile))("后台分析资源文件索引地址",
                Sub(Task As LoaderTask(Of String, List(Of NetFile)))
                    Dim IndexFile As NetFile = DlClientAssetIndexGet(Instance)
                    If IndexFile Is Nothing Then
                        Logger.Warn($"未找到版本 {Instance.Name} 的合适的资源索引下载地址，游戏 assets 可能缺失")
                        Task.Cancel()
                        Throw New OperationCanceledException
                    End If
                    RealAddressBg = IndexFile.LocalPath
                    TempAddressBg = PathTemp & "Cache\" & IndexFile.LocalName & "_background"
                    IndexFile.LocalPath = TempAddressBg
                    Task.Output = New List(Of NetFile) From {IndexFile}
                    'Check whether an update is needed: at most once per day
                    Dim LastUpdate As Date = FileUtils.GetInfo(RealAddressBg).LastWriteTime.Date
                    If FileUtils.Exists(RealAddressBg) AndAlso Math.Abs((LastUpdate - Now.Date).TotalDays) < 1 Then
                        Logger.Info($"无需更新资源文件索引，上次更新于 {LastUpdate}")
                        Task.Cancel()
                    End If
                End Sub))
                LoadersAssetsUpdate.Add(New LoaderDownload("后台下载资源文件索引", New List(Of NetFile)))
                Dim Updater As New LoaderCombo(Of String)("后台更新资源文件索引", LoadersAssetsUpdate)
                Logger.Info("开始后台检查资源文件索引")
                Updater.Start()
            End If
        End If
#End Region

        Return Loaders
    End Function

#End Region

#Region "DlClientList | Minecraft client version list"

    ''' <summary>
    ''' Drop ordinals of all Minecraft release versions.
    ''' Returns Nothing if fetching has never completed; otherwise always non-empty and sorted from high to low.
    ''' </summary>
    Public Property AllDrops As List(Of Integer)
        Get
            If _AllDrops Is Nothing Then
                _AllDrops = Settings.Get(Of String)("CacheDrops").ToString.
                    Split(",", True).Select(Function(d) CInt(Val(d))).ToList()
            End If
            Return If(_AllDrops.Any, _AllDrops, Nothing) 'Never set _AllDrops back to Nothing, to avoid repeatedly triggering fetches from initialization
        End Get
        Set(value As List(Of Integer))
            _AllDrops = value
            Settings.Set("CacheDrops", value.Join(","c))
        End Set
    End Property
    Private _AllDrops As List(Of Integer) = Nothing

    'Main loader
    Public Structure DlClientListResult
        ''' <summary>
        ''' Name of the data source, e.g. "Mojang", "BMCLAPI".
        ''' </summary>
        Public SourceName As String
        ''' <summary>
        ''' Whether this is official real-time data.
        ''' </summary>
        Public IsOfficial As Boolean
        ''' <summary>
        ''' The fetched JSON data.
        ''' </summary>
        Public Value As JObject
    End Structure
    ''' <summary>
    ''' Minecraft client version list, main loader.
    ''' If the mirror is required to contain a specific version, pass that version ID as the input (#5195).
    ''' </summary>
    Public DlClientListLoader As New LoaderTask(Of String, DlClientListResult)("DlClientList Main", AddressOf DlClientListMain)
    Private Sub DlClientListMain(Loader As LoaderTask(Of String, DlClientListResult))
        Select Case Settings.Get(Of Integer)("ToolDownloadVersion")
            Case 0
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)(DlClientListBmclapiLoader, 30),
                    New KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)(DlClientListMojangLoader, 30 + 60)
                }, Loader.IsForceRestarting)
            Case 1
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)(DlClientListMojangLoader, 5),
                    New KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)(DlClientListBmclapiLoader, 5 + 30)
                }, Loader.IsForceRestarting)
            Case Else
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)(DlClientListMojangLoader, 60),
                    New KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)(DlClientListBmclapiLoader, 60 + 60)
                }, Loader.IsForceRestarting)
        End Select
        'Extract all Drop ordinals
        Dim Drops As New List(Of Integer)
        For Each Version As JObject In Loader.Output.Value("versions")
            Dim Id As String = Version("id").ToString
            If Id.Contains("-") Then Continue For
            Dim Drop As Integer = McVersion.VersionToDrop(Id)
            If Drop = 209 Then Continue For
            Drops.Add(Drop)
        Next
        AllDrops = Drops.Distinct.OrderByDescending(Function(d) d).ToList()
    End Sub

    'Per-source sub-loaders
    ''' <summary>
    ''' Minecraft client version list, official Mojang source loader.
    ''' </summary>
    Public DlClientListMojangLoader As New LoaderTask(Of String, DlClientListResult)("DlClientList Mojang", AddressOf DlClientListMojangMain)
    Private Sub DlClientListMojangMain(Loader As LoaderTask(Of String, DlClientListResult))
        Dim StartTime As Long = GetTimeMs()
        Dim Json As JObject = NetRequestByClientRetry("https://launchermeta.mojang.com/mc/game/version_manifest.json", RequireJson:=True).DeserializeJson()
        Try
            Dim Versions As JArray = Json("versions")
            If Versions.Count < 200 Then Throw New Exception("获取到的版本列表长度不足（" & Json.ToString & "）")
            'Determine whether the official source is usable
            If Not DlPreferMojang Then
                Dim DeltaTime = GetTimeMs() - StartTime
                DlPreferMojang = DeltaTime < 4000
                Logger.Info($"Mojang 官方源加载耗时：{DeltaTime}ms，{If(DlPreferMojang, "可优先使用官方源", "不优先使用官方源")}")
            End If
            Loader.Output = New DlClientListResult With {.IsOfficial = True, .SourceName = "Mojang 官方源", .Value = Json}
        Catch ex As Exception
            Throw New Exception("Minecraft 官方源版本列表解析失败", ex)
        End Try
    End Sub
    ''' <summary>
    ''' Minecraft client version list, BMCLAPI source loader.
    ''' </summary>
    Public DlClientListBmclapiLoader As New LoaderTask(Of String, DlClientListResult)("DlClientList Bmclapi", AddressOf DlClientListBmclapiMain)
    Private Sub DlClientListBmclapiMain(Loader As LoaderTask(Of String, DlClientListResult))
        Dim Json As JObject = NetRequestByClientRetry("https://bmclapi2.bangbang93.com/mc/game/version_manifest.json", RequireJson:=True).DeserializeJson()
        Try
            Dim Versions As JArray = Json("versions")
            If Versions.Count < 200 Then Throw New Exception("获取到的版本列表长度不足（" & Json.ToString & "）")
            'Add PCL-specific entries
            If FileUtils.Exists(PathTemp & "Cache\download.json") Then Versions.Merge(FileUtils.ReadAsJson(PathTemp & "Cache\download.json"))
            'Check whether the required version is present (#5195)
            If Not String.IsNullOrEmpty(Loader.Input) Then
                Dim Id = Loader.Input
                If Not Versions.Any(Function(v) v("id")?.ToString = Id) Then
                    Throw New Exception("BMCLAPI 源未包含目标版本 " & Id)
                End If
            End If
            Loader.Output = New DlClientListResult With {.IsOfficial = False, .SourceName = "BMCLAPI", .Value = Json}
        Catch ex As Exception
            Throw New Exception("Minecraft BMCLAPI 版本列表解析失败（" & Json.ToString & "）", ex)
        End Try
    End Sub

    ''' <summary>
    ''' Gets the JSON download URL of a version, or Nothing on failure. Must be run on a worker thread.
    ''' </summary>
    Public Function DlClientListGet(Id As String)
        Try
            'Normalize the version format
            Id = Id.Replace("_", "-") '1.7.10_pre4 is shown as 1.7.10-pre4 in the version list
            If Id <> "1.0" AndAlso Id.EndsWithF(".0") Then Id = Left(Id, Id.Length - 2) 'Triggered when downloading OptiFine 1.8, whose version is shown as 1.8.0
            'Get the Minecraft version list
            Select Case DlClientListLoader.State
                Case LoadState.Finished
                    'Look up the target version in the current result...
                    For Each Version As JObject In DlClientListLoader.Output.Value("versions")
                        If Version("id") = Id Then Return Version("url").ToString
                    Next
                    '...if it's missing, fetch again (can happen right after a new version is released, #5195)
                    DlClientListLoader.WaitForExit(Id, IsForceRestart:=True)
                Case LoadState.Loading
                    DlClientListLoader.WaitForExit(Id)
                Case LoadState.Failed, LoadState.Canceled, LoadState.Waiting
                    DlClientListLoader.WaitForExit(Id, IsForceRestart:=True)
            End Select
            'Look up the version again
            For Each Version As JObject In DlClientListLoader.Output.Value("versions")
                If Version("id") = Id Then Return Version("url").ToString
            Next
            Logger.Warn($"未发现版本 {Id} 的 json 下载地址，版本列表返回为：{vbCrLf}{DlClientListLoader.Output.Value}")
            Return Nothing
        Catch ex As Exception
            Logger.Warn(ex, $"获取版本 {Id} 的 json 下载地址失败")
            Return Nothing
        End Try
    End Function

#End Region

#Region "DlOptiFineList | OptiFine version list"

    Public Structure DlOptiFineListResult
        ''' <summary>
        ''' Name of the data source, e.g. "Official", "BMCLAPI".
        ''' </summary>
        Public SourceName As String
        ''' <summary>
        ''' Whether this is official real-time data.
        ''' </summary>
        Public IsOfficial As Boolean
        ''' <summary>
        ''' The fetched data.
        ''' </summary>
        Public Value As List(Of DlOptiFineListEntry)
    End Structure

    Public Class DlOptiFineListEntry
        ''' <summary>
        ''' Display name with "HD_U" removed, e.g. "1.12.2 C8".
        ''' </summary>
        Public DisplayName As String
        ''' <summary>
        ''' Original file name, e.g. "preview_OptiFine_1.11_HD_U_E1_pre.jar".
        ''' </summary>
        Public FileName As String
        ''' <summary>
        ''' Corresponding version name, e.g. "1.13.2-OptiFine_HD_U_E6".
        ''' </summary>
        Public InstanceName As String
        ''' <summary>
        ''' Whether this is a preview version.
        ''' </summary>
        Public IsPreview As Boolean
        ''' <summary>
        ''' Corresponding Minecraft version, e.g. "1.12.2".
        ''' </summary>
        Public Property Inherit As String
            Get
                Return _inherit
            End Get
            Set(value As String)
                If value.EndsWithF(".0") Then value = Left(value, value.Length - 2)
                _inherit = value
            End Set
        End Property
        Private _inherit As String
        ''' <summary>
        ''' Release date in "yyyy/mm/dd" format. Not available from the official OptiFine source.
        ''' </summary>
        Public ReleaseTime As String
        ''' <summary>
        ''' Minimum required Forge version. Empty string means no restriction, Nothing means incompatible; "28.1.56" is a full version number, "1161" is the last part of the version number.
        ''' </summary>
        Public RequiredForgeVersion As String
    End Class

    ''' <summary>
    ''' OptiFine version list, main loader.
    ''' </summary>
    Public DlOptiFineListLoader As New LoaderTask(Of Integer, DlOptiFineListResult)("DlOptiFineList Main", AddressOf DlOptiFineListMain)
    Private Sub DlOptiFineListMain(Loader As LoaderTask(Of Integer, DlOptiFineListResult))
        Select Case Settings.Get(Of Integer)("ToolDownloadVersion")
            Case 0
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)(DlOptiFineListBmclapiLoader, 30),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)(DlOptiFineListOfficialLoader, 30 + 60)
                }, Loader.IsForceRestarting)
            Case 1
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)(DlOptiFineListOfficialLoader, 5),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)(DlOptiFineListBmclapiLoader, 5 + 30)
                }, Loader.IsForceRestarting)
            Case Else
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)(DlOptiFineListOfficialLoader, 60),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)(DlOptiFineListBmclapiLoader, 60 + 60)
                }, Loader.IsForceRestarting)
        End Select
    End Sub

    ''' <summary>
    ''' OptiFine version list, official source.
    ''' </summary>
    Public DlOptiFineListOfficialLoader As New LoaderTask(Of Integer, DlOptiFineListResult)("DlOptiFineList Official", AddressOf DlOptiFineListOfficialMain)
    Private Sub DlOptiFineListOfficialMain(Loader As LoaderTask(Of Integer, DlOptiFineListResult))
        Dim Result As String = NetRequestByClientRetry("https://optifine.net/downloads", Encoding:=Encoding.Default)
        If Result.Length < 200 Then Throw New Exception("获取到的版本列表长度不足（" & Result & "）")
        Try
            'Get info for all versions
            Dim Forge As List(Of String) = Result.RegexSearch("(?<=colForge'>)[^<]*").ToList
            Dim ReleaseTime As List(Of String) = Result.RegexSearch("(?<=colDate'>)[^<]+").ToList
            Dim Name As List(Of String) = Result.RegexSearch("(?<=OptiFine_)[0-9A-Za-z_.]+(?=.jar"")").ToList
            If Not ReleaseTime.Count = Name.Count Then Throw New Exception("版本与发布时间数据无法对应")
            If Not Forge.Count = Name.Count Then Throw New Exception("版本与 Forge 兼容数据无法对应")
            If ReleaseTime.Count < 10 Then Throw New Exception("获取到的版本数量不足（" & Result & "）")
            'Convert to a list for output
            Dim Versions As New List(Of DlOptiFineListEntry)
            For i = 0 To ReleaseTime.Count - 1
                Name(i) = Name(i).Replace("_", " ")
                Dim Entry As New DlOptiFineListEntry With {
                    .DisplayName = Name(i).Replace("HD U ", "").Replace(".0 ", " "),
                    .ReleaseTime = {ReleaseTime(i).Split(".")(2), ReleaseTime(i).Split(".")(1), ReleaseTime(i).Split(".")(0)}.Join("/"c),
                    .IsPreview = Name(i).ContainsIgnoreCase("pre"),
                    .Inherit = Name(i).ToString.Split(" ")(0),
                    .FileName = If(Name(i).ContainsIgnoreCase("pre"), "preview_", "") & "OptiFine_" & Name(i).Replace(" ", "_") & ".jar",
                    .RequiredForgeVersion = Forge(i).Replace("Forge ", "").Replace("#", "")}
                If Entry.RequiredForgeVersion.Contains("N/A") Then Entry.RequiredForgeVersion = Nothing
                Entry.InstanceName = Entry.Inherit & "-OptiFine_" & Name(i).AfterFirst(" ").Replace(" ", "_")
                Versions.Add(Entry)
            Next
            Loader.Output = New DlOptiFineListResult With {.IsOfficial = True, .SourceName = "OptiFine 官方源", .Value = Versions}
        Catch ex As Exception
            Throw New Exception("OptiFine 官方源版本列表解析失败（" & Result & "）", ex)
        End Try
    End Sub

    ''' <summary>
    ''' OptiFine version list, BMCLAPI.
    ''' </summary>
    Public DlOptiFineListBmclapiLoader As New LoaderTask(Of Integer, DlOptiFineListResult)("DlOptiFineList Bmclapi", AddressOf DlOptiFineListBmclapiMain)
    Private Sub DlOptiFineListBmclapiMain(Loader As LoaderTask(Of Integer, DlOptiFineListResult))
        Dim Json As JArray = NetRequestByClientRetry("https://bmclapi2.bangbang93.com/optifine/versionList", RequireJson:=True).DeserializeJson()
        Try
            Dim Versions As New List(Of DlOptiFineListEntry)
            For Each Token As JObject In Json
                Dim Entry As New DlOptiFineListEntry With {
                    .DisplayName = (Token("mcversion").ToString & Token("type").ToString.Replace("HD_U", "").Replace("_", " ") & " " & Token("patch").ToString).Replace(".0 ", " "),
                    .ReleaseTime = "",
                    .IsPreview = Token("patch").ToString.ContainsIgnoreCase("pre"),
                    .Inherit = Token("mcversion").ToString,
                    .FileName = Token("filename").ToString,
                    .RequiredForgeVersion = If(Token("forge"), "").ToString.Replace("Forge ", "").Replace("#", "")
                }
                If Entry.RequiredForgeVersion.Contains("N/A") Then Entry.RequiredForgeVersion = Nothing
                Entry.InstanceName = Entry.Inherit & "-OptiFine_" & (Token("type").ToString & " " & Token("patch").ToString).Replace(".0 ", " ").Replace(" ", "_").Replace(Entry.Inherit & "_", "")
                Versions.Add(Entry)
            Next
            Loader.Output = New DlOptiFineListResult With {.IsOfficial = False, .SourceName = "BMCLAPI", .Value = Versions}
        Catch ex As Exception
            Throw New Exception("OptiFine BMCLAPI 版本列表解析失败（" & Json.ToString & "）", ex)
        End Try
    End Sub

#End Region

#Region "DlForgeList | Forge Minecraft version list"

    Public Structure DlForgeListResult
        ''' <summary>
        ''' Name of the data source, e.g. "Official", "BMCLAPI".
        ''' </summary>
        Public SourceName As String
        ''' <summary>
        ''' Whether this is official real-time data.
        ''' </summary>
        Public IsOfficial As Boolean
        ''' <summary>
        ''' The fetched data.
        ''' </summary>
        Public Value As List(Of String)
    End Structure

    ''' <summary>
    ''' Forge version list, main loader.
    ''' </summary>
    Public DlForgeListLoader As New LoaderTask(Of Integer, DlForgeListResult)("DlForgeList Main", AddressOf DlForgeListMain)
    Private Sub DlForgeListMain(Loader As LoaderTask(Of Integer, DlForgeListResult))
        Select Case Settings.Get(Of Integer)("ToolDownloadVersion")
            Case 0
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)(DlForgeListBmclapiLoader, 30),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)(DlForgeListOfficialLoader, 30 + 60)
                }, Loader.IsForceRestarting)
            Case 1
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)(DlForgeListOfficialLoader, 5),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)(DlForgeListBmclapiLoader, 5 + 30)
                }, Loader.IsForceRestarting)
            Case Else
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)(DlForgeListOfficialLoader, 60),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)(DlForgeListBmclapiLoader, 60 + 60)
                }, Loader.IsForceRestarting)
        End Select
    End Sub

    ''' <summary>
    ''' Forge version list, official source.
    ''' </summary>
    Public DlForgeListOfficialLoader As New LoaderTask(Of Integer, DlForgeListResult)("DlForgeList Official", AddressOf DlForgeListOfficialMain)
    Private Sub DlForgeListOfficialMain(Loader As LoaderTask(Of Integer, DlForgeListResult))
        Dim Result As String = NetRequestByClientRetry("https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_1.2.4.html", Encoding:=Encoding.Default, Accept:="text/html", SimulateBrowserHeaders:=True)
        If Result.Length < 200 Then Throw New Exception("获取到的版本列表长度不足（" & Result & "）")
        'Get info for all versions
        Dim Names As List(Of String) = Result.RegexSearch("(?<=a href=""index_)[0-9.]+((_pre|[_-]snapshot[_-]?)[0-9]?)?(?=.html)").ToList
        Names.Add("1.2.4") '1.2.4 won't be matched
        If Names.Count < 10 Then Throw New Exception("获取到的版本数量不足（" & Result & "）")
        Loader.Output = New DlForgeListResult With {.IsOfficial = True, .SourceName = "Forge 官方源", .Value = Names}
    End Sub

    ''' <summary>
    ''' Forge version list, BMCLAPI.
    ''' </summary>
    Public DlForgeListBmclapiLoader As New LoaderTask(Of Integer, DlForgeListResult)("DlForgeList Bmclapi", AddressOf DlForgeListBmclapiMain)
    Private Sub DlForgeListBmclapiMain(Loader As LoaderTask(Of Integer, DlForgeListResult))
        Dim Result As JArray = NetRequestByClientRetry("https://bmclapi2.bangbang93.com/forge/minecraft",
                                                       Encoding:=Encoding.Default, RequireJson:=True).DeserializeJson()
        Dim Names As List(Of String) = Result.Select(Function(v) v.ToString).ToList
        If Names.Count < 10 Then Throw New Exception("获取到的版本数量不足")
        Loader.Output = New DlForgeListResult With {.IsOfficial = False, .SourceName = "BMCLAPI", .Value = Names}
    End Sub

#End Region

#Region "DlForgeVersion | Forge version list"

    Public MustInherit Class DlForgelikeEntry
        Implements IComparable(Of DlForgelikeEntry)

        Public IsNeoForge As Boolean
        ''' <summary>
        ''' Loader name: Forge or NeoForge.
        ''' </summary>
        Public MustOverride ReadOnly Property LoaderName As String
        ''' <summary>
        ''' File extension, without a leading dot.
        ''' </summary>
        Public MustOverride ReadOnly Property FileExtension As String
        ''' <summary>
        ''' Normalized version number, only usable for comparison and sorting. NeoForge entries do NOT include the alpha/beta number!
        ''' Format: Major.Minor.Build.Revision
        ''' Forge: e.g. "50.1.9.0" (last part always 0), "14.22.1.2478" (Legacy).
        ''' NeoForge: e.g. "20.4.30.0" (last part always 0), "19.47.1.99" (Legacy: first part always 19).
        ''' </summary>
        Public Version As Version
        ''' <summary>
        ''' Human-readable, unnormalized version name.
        ''' Forge: e.g. "50.1.9", "14.22.1.2478" (Legacy).
        ''' NeoForge: e.g. "20.4.30-beta", "47.1.99" (Legacy).
        ''' </summary>
        Public VersionName As String
        ''' <summary>
        ''' Corresponding Minecraft version, e.g. 1.12.2, 26.1-snapshot-1.
        ''' </summary>
        Public Inherit As String

        Public Function CompareTo(other As DlForgelikeEntry) As Integer Implements IComparable(Of DlForgelikeEntry).CompareTo
            If Version <> other.Version Then
                Return Version.CompareTo(other.Version)
            Else
                Return McUtils.CompareVersion(VersionName, other.VersionName)
            End If
        End Function
    End Class

    Public Class DlForgeVersionEntry
        Inherits DlForgelikeEntry

        ''' <summary>
        ''' Release time in "yyyy/MM/dd HH:mm" format.
        ''' </summary>
        Public ReleaseTime As String
        ''' <summary>
        ''' MD5 or SHA1 of the file (old BMCLAPI versions use MD5, newer ones SHA1; the official source always uses MD5).
        ''' </summary>
        Public Hash As String = Nothing
        ''' <summary>
        ''' Whether this is a recommended version.
        ''' </summary>
        Public IsRecommended As Boolean
        ''' <summary>
        ''' Install type: installer, client or universal.
        ''' </summary>
        Public Category As String
        ''' <summary>
        ''' Version name used for downloading. May have a branch appended to Version.
        ''' </summary>
        Public FileVersion As String

        ''' <summary>
        ''' Loader name: Forge or NeoForge.
        ''' </summary>
        Public Overrides ReadOnly Property LoaderName As String
            Get
                Return "Forge"
            End Get
        End Property
        ''' <summary>
        ''' File extension, without a leading dot.
        ''' </summary>
        Public Overrides ReadOnly Property FileExtension As String
            Get
                Return If(Category = "installer", "jar", "zip")
            End Get
        End Property

        Public Sub New(Version As String, Branch As String, Inherit As String)
            'Special handling for quirky versions
            If Version = "11.15.1.2318" OrElse Version = "11.15.1.1902" OrElse Version = "11.15.1.1890" Then Branch = "1.8.9"
            If Branch Is Nothing AndAlso Inherit = "1.7.10" AndAlso Version.Split(".")(3) >= 1300 Then Branch = "1.7.10"
            'Provide all info for DlForgelikeEntry
            IsNeoForge = False
            VersionName = Version
            Me.Version = New Version(Version)
            Me.Inherit = Inherit
            FileVersion = Version & If(Branch Is Nothing, "", "-" & Branch)
        End Sub

    End Class

    ''' <summary>
    ''' Forge version list, main loader.
    ''' </summary>
    Public Sub DlForgeVersionMain(Loader As LoaderTask(Of String, List(Of DlForgeVersionEntry)))
        Dim DlForgeVersionOfficialLoader As New LoaderTask(Of String, List(Of DlForgeVersionEntry))("DlForgeVersion Official", AddressOf DlForgeVersionOfficialMain)
        Dim DlForgeVersionBmclapiLoader As New LoaderTask(Of String, List(Of DlForgeVersionEntry))("DlForgeVersion Bmclapi", AddressOf DlForgeVersionBmclapiMain)
        Select Case Settings.Get(Of Integer)("ToolDownloadVersion")
            Case 0
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)(DlForgeVersionBmclapiLoader, 30),
                    New KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)(DlForgeVersionOfficialLoader, 30 + 60)
                }, Loader.IsForceRestarting)
            Case 1
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)(DlForgeVersionOfficialLoader, 5),
                    New KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)(DlForgeVersionBmclapiLoader, 5 + 30)
                }, Loader.IsForceRestarting)
            Case Else
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)(DlForgeVersionOfficialLoader, 60),
                    New KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)(DlForgeVersionBmclapiLoader, 60 + 60)
                }, Loader.IsForceRestarting)
        End Select
    End Sub

    ''' <summary>
    ''' Forge version list, official source.
    ''' </summary>
    Public Sub DlForgeVersionOfficialMain(Loader As LoaderTask(Of String, List(Of DlForgeVersionEntry)))
        Dim Result As String
        Try
            Result = NetRequestByLoader("https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_" &
                                          Loader.Input.Replace("-", "_") & 'Compatibility with Forge 1.7.10-pre4, #4057
                                          ".html", SimulateBrowserHeaders:=True)
        Catch ex As Exception
            If ex.GetDisplay(False).Contains("(404)") Then
                Throw New Exception("无")
            Else
                Throw
            End If
        End Try
        If Result.Length < 1000 Then Throw New Exception("获取到的版本列表长度不足（" & Result & "）")
        Dim Versions As New List(Of DlForgeVersionEntry)
        Try
            'Split version info
            Dim VersionCodes = Result.Substring(0, Result.LastIndexOfF("</table>")).Split("<td class=""download-version")
            'Get info for all versions
            For i = 1 To VersionCodes.Count - 1
                Dim VersionCode = VersionCodes(i)
                Try
                    'Basic info
                    Dim Name As String = VersionCode.RegexSeek("(?<=[^(0-9)]+)[0-9\.]+")
                    Dim IsRecommended As Boolean = VersionCode.Contains("fa promo-recommended")
                    Dim Inherit As String = Loader.Input
                    'Branch
                    Dim Branch As String = VersionCode.RegexSeek($"(?<=-{Name}-)[^-""]+(?=-[a-z]+.[a-z]{{3}})")
                    If String.IsNullOrWhiteSpace(Branch) Then Branch = Nothing
                    'Release time
                    Dim ReleaseTimeOriginal = VersionCode.RegexSeek("(?<=""download-time"" title="")[^""]+")
                    Dim ReleaseTimeSplit = ReleaseTimeOriginal.Split(" -:".ToCharArray) 'Original format: "2021-02-15 03:24:02"
                    Dim ReleaseDate As New Date(ReleaseTimeSplit(0), ReleaseTimeSplit(1), ReleaseTimeSplit(2), 'Date
                                                ReleaseTimeSplit(3), ReleaseTimeSplit(4), ReleaseTimeSplit(5), 'Time
                                                0, DateTimeKind.Utc) 'Use UTC as the reference
                    Dim ReleaseTime As String = ReleaseDate.ToLocalTime.ToString("yyyy'/'MM'/'dd HH':'mm") 'Time zone and format conversion
                    'Category and MD5
                    Dim MD5 As String, Category As String
                    If VersionCode.Contains("classifier-installer""") Then
                        'Type installer.jar, supported range ~753 (part of ~1.6.1), 738~684 (all of 1.5.2)
                        VersionCode = VersionCode.Substring(VersionCode.IndexOfF("installer.jar"))
                        MD5 = VersionCode.RegexSeek("(?<=MD5:</strong> )[^<]+")
                        Category = "installer"
                    ElseIf VersionCode.Contains("classifier-universal""") Then
                        'Type universal.zip, supported range 751~449 (part of 1.6.1), 682~183 (part of 1.5.1 ~ 1.3.2)
                        VersionCode = VersionCode.Substring(VersionCode.IndexOfF("universal.zip"))
                        MD5 = VersionCode.RegexSeek("(?<=MD5:</strong> )[^<]+")
                        Category = "universal"
                    ElseIf VersionCode.Contains("client.zip") Then
                        'Type client.zip, supported range 182~ (part of 1.3.2 ~)
                        VersionCode = VersionCode.Substring(VersionCode.IndexOfF("client.zip"))
                        MD5 = VersionCode.RegexSeek("(?<=MD5:</strong> )[^<]+")
                        Category = "client"
                    Else
                        'No downloads at all (happens for some 1.6.4 versions)
                        Continue For
                    End If
                    'Add to the list
                    Versions.Add(New DlForgeVersionEntry(Name, Branch, Inherit) With {.Category = Category, .IsRecommended = IsRecommended, .Hash = MD5.ReplaceLineEndings(""), .ReleaseTime = ReleaseTime})
                Catch ex As Exception
                    Throw New Exception("Forge 官方源版本信息提取失败（" & VersionCode & "）", ex)
                End Try
            Next
        Catch ex As Exception
            Throw New Exception("Forge 官方源版本列表解析失败（" & Result & "）", ex)
        End Try
        If Not Versions.Any() Then Throw New Exception("无")
        Loader.Output = Versions
    End Sub

    ''' <summary>
    ''' Forge version list, BMCLAPI.
    ''' </summary>
    Public Sub DlForgeVersionBmclapiMain(Loader As LoaderTask(Of String, List(Of DlForgeVersionEntry)))
        Dim Json As JArray = NetRequestByClientRetry("https://bmclapi2.bangbang93.com/forge/minecraft/" &
            Loader.Input.Replace("-", "_"), RequireJson:=True).DeserializeJson() 'Compatibility with Forge 1.7.10-pre4, #4057
        Dim Versions As New List(Of DlForgeVersionEntry)
        Try
            Dim Recommended As String = McDownloadForgeRecommendedGet(Loader.Input)
            For Each Token As JObject In Json
                'Category and hash
                Dim Hash As String = Nothing, Category As String = "unknown", Proi As Integer = -1
                For Each File As JObject In Token("files")
                    Select Case File("category").ToString
                        Case "installer"
                            If File("format").ToString = "jar" Then
                                'Type installer.jar, supported range ~753 (part of ~1.6.1), 738~684 (all of 1.5.2)
                                Hash = File("hash")
                                Category = "installer"
                                Proi = 2
                            End If
                        Case "universal"
                            If Proi <= 1 AndAlso File("format").ToString = "zip" Then
                                'Type universal.zip, supported range 751~449 (part of 1.6.1), 682~183 (part of 1.5.1 ~ 1.3.2)
                                Hash = File("hash")
                                Category = "universal"
                                Proi = 1
                            End If
                        Case "client"
                            If Proi <= 0 AndAlso File("format").ToString = "zip" Then
                                'Type client.zip, supported range 182~ (part of 1.3.2 ~)
                                Hash = File("hash")
                                Category = "client"
                                Proi = 0
                            End If
                    End Select
                Next
                'Get the entry
                Dim Branch As String = Token("branch")
                Dim Name As String = Token("version")
                'Basic info
                Dim Entry = New DlForgeVersionEntry(Name, Branch, Loader.Input) With {.Hash = Hash, .Category = Category, .IsRecommended = Recommended = Name}
                Dim TimeSplit = Token("modified").ToString.Split("-"c, "T"c, ":"c, "."c, " "c, "/"c)
                Entry.ReleaseTime = Token("modified").ToObject(Of Date).ToLocalTime.ToString("yyyy'/'MM'/'dd HH':'mm")
                'Add the entry
                Versions.Add(Entry)
            Next
        Catch ex As Exception
            Throw New Exception("Forge BMCLAPI 版本列表解析失败（" & Json.ToString & "）", ex)
        End Try
        If Not Versions.Any() Then Throw New Exception("无")
        Loader.Output = Versions
    End Sub

#End Region

#Region "DlNeoForgeList | NeoForge version list"

    Public Structure DlNeoForgeListResult
        ''' <summary>
        ''' Name of the data source, e.g. "Official", "BMCLAPI".
        ''' </summary>
        Public SourceName As String
        ''' <summary>
        ''' Whether this is official real-time data.
        ''' </summary>
        Public IsOfficial As Boolean
        ''' <summary>
        ''' List of all versions, sorted from newest to oldest.
        ''' </summary>
        Public Value As List(Of DlNeoForgeListEntry)
    End Structure

    Public Class DlNeoForgeListEntry
        Inherits DlForgelikeEntry

        ''' <summary>
        ''' Whether this is a beta version.
        ''' </summary>
        Public IsBeta As Boolean
        ''' <summary>
        ''' Raw version string used by the API, e.g. "20.4.30-beta", "1.20.1-47.1.99" (Legacy).
        ''' </summary>
        Public ApiName As String
        ''' <summary>
        ''' Base URL of the file on the official site, without the suffix.
        ''' </summary>
        Public ReadOnly Property UrlBase As String
            Get
                Dim PackageName As String = If(Inherit = "1.20.1", "forge", "neoforge")
                Return $"https://maven.neoforged.net/releases/net/neoforged/{PackageName}/{ApiName}/{PackageName}-{ApiName}"
            End Get
        End Property

        ''' <summary>
        ''' Loader name: Forge or NeoForge.
        ''' </summary>
        Public Overrides ReadOnly Property LoaderName As String
            Get
                Return "NeoForge"
            End Get
        End Property
        ''' <summary>
        ''' File extension, without a leading dot.
        ''' </summary>
        Public Overrides ReadOnly Property FileExtension As String
            Get
                Return "jar"
            End Get
        End Property

        Public Sub New(ApiName As String)
            IsNeoForge = True
            Me.ApiName = ApiName
            IsBeta = ApiName.ContainsIgnoreCase("beta") OrElse ApiName.ContainsIgnoreCase("alpha")
            If ApiName.Contains("1.20.1") Then '1.20.1-47.1.99
                VersionName = ApiName.Replace("1.20.1-", "")
                Version = New Version("19." & VersionName)
                Inherit = "1.20.1"
            ElseIf ApiName.StartsWithF("0.") Then '0.25w14craftmine.3-beta
                VersionName = ApiName
                Dim Segments = ApiName.BeforeFirst("-").Split("."c)
                Version = New Version(0, 0, Segments.Last)
                Inherit = Segments(1)
            Else '20.4.30-beta; 26.1.0.0-alpha.1+snapshot-1
                VersionName = ApiName
                Version = New Version(ApiName.BeforeFirst("-"))
                If Version.Major >= 24 Then
                    Inherit = $"{Version.Major}.{Version.Minor}.{Version.Build}"
                Else
                    Inherit = $"1.{Version.Major}.{Version.Minor}"
                End If
                If Inherit.EndsWithF(".0") Then Inherit = Left(Inherit, Inherit.Length - 2)
                If VersionName.Contains("+") Then Inherit &= "-" & VersionName.AfterFirst("+")
            End If
        End Sub
    End Class

    ''' <summary>
    ''' NeoForge version list, main loader.
    ''' </summary>
    Public DlNeoForgeListLoader As New LoaderTask(Of Integer, DlNeoForgeListResult)("DlNeoForgeList Main", AddressOf DlNeoForgeListMain)
    Private Sub DlNeoForgeListMain(Loader As LoaderTask(Of Integer, DlNeoForgeListResult))
        Select Case Settings.Get(Of Integer)("ToolDownloadVersion")
            Case 0
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)(DlNeoForgeListBmclapiLoader, 30),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)(DlNeoForgeListOfficialLoader, 30 + 60)
                }, Loader.IsForceRestarting)
            Case 1
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)(DlNeoForgeListOfficialLoader, 5),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)(DlNeoForgeListBmclapiLoader, 5 + 30)
                }, Loader.IsForceRestarting)
            Case Else
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)(DlNeoForgeListOfficialLoader, 60),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)(DlNeoForgeListBmclapiLoader, 60 + 60)
                }, Loader.IsForceRestarting)
        End Select
    End Sub

    ''' <summary>
    ''' NeoForge version list, official source.
    ''' </summary>
    Public DlNeoForgeListOfficialLoader As New LoaderTask(Of Integer, DlNeoForgeListResult)("DlNeoForgeList Official", AddressOf DlNeoForgeListOfficialMain)
    Private Sub DlNeoForgeListOfficialMain(Loader As LoaderTask(Of Integer, DlNeoForgeListResult))
        'Get the version list JSON
        Dim ResultLatest As String = NetRequestByClientRetry("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge", RequireJson:=True)
        Dim ResultLegacy As String = NetRequestByClientRetry("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge", RequireJson:=True)
        If ResultLatest.Length < 100 OrElse ResultLegacy.Length < 100 Then Throw New Exception("获取到的版本列表长度不足（" & ResultLatest & "）")
        'Parse
        Try
            Loader.Output = New DlNeoForgeListResult With {.IsOfficial = True, .SourceName = "NeoForge 官方源",
                .Value = GetNeoForgeEntries(ResultLatest, ResultLegacy)}
        Catch ex As Exception
            Throw New Exception("NeoForge 官方源版本列表解析失败（" & ResultLatest & vbCrLf & vbCrLf & ResultLegacy & "）", ex)
        End Try
    End Sub

    ''' <summary>
    ''' NeoForge version list, BMCLAPI.
    ''' </summary>
    Public DlNeoForgeListBmclapiLoader As New LoaderTask(Of Integer, DlNeoForgeListResult)("DlNeoForgeList Bmclapi", AddressOf DlNeoForgeListBmclapiMain)
    Public Sub DlNeoForgeListBmclapiMain(Loader As LoaderTask(Of Integer, DlNeoForgeListResult))
        'Get the version list JSON
        Dim ResultLatest As String = NetRequestByClientRetry("https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge", RequireJson:=True)
        Dim ResultLegacy As String = NetRequestByClientRetry("https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/forge", RequireJson:=True)
        If ResultLatest.Length < 100 OrElse ResultLegacy.Length < 100 Then Throw New Exception("获取到的版本列表长度不足（" & ResultLatest & "）")
        'Parse
        Try
            Loader.Output = New DlNeoForgeListResult With {.IsOfficial = True, .SourceName = "BMCLAPI",
                .Value = GetNeoForgeEntries(ResultLatest, ResultLegacy)}
        Catch ex As Exception
            Throw New Exception("NeoForge BMCLAPI 版本列表解析失败（" & ResultLatest & vbCrLf & vbCrLf & ResultLegacy & "）", ex)
        End Try
    End Sub

    Private Function GetNeoForgeEntries(LatestJson As String, LatestLegacyJson As String) As List(Of DlNeoForgeListEntry)
        Dim VersionNames = (LatestLegacyJson & LatestJson).RegexSearch(
            "(?<="")(1\.20\.1-)?\d+\.[^\.]+\.\d+(\.\d+)?(-(beta|alpha)(\.\d+)?)?(\+snapshot-\d+)?(?="")")
        Dim Versions = VersionNames.
            Where(Function(name) name <> "47.1.82"). 'This version is in the version list but can't be downloaded
            Select(Function(name) New DlNeoForgeListEntry(name)).
            OrderByDescending(Function(a) a).ToList
        If Not Versions.Any() Then Throw New Exception("无")
        Return Versions
    End Function

#End Region

#Region "DlLiteLoaderList | LiteLoader version list"

    Public Structure DlLiteLoaderListResult
        ''' <summary>
        ''' Name of the data source, e.g. "Official", "BMCLAPI".
        ''' </summary>
        Public SourceName As String
        ''' <summary>
        ''' Whether this is official real-time data.
        ''' </summary>
        Public IsOfficial As Boolean
        ''' <summary>
        ''' The fetched data.
        ''' </summary>
        Public Value As List(Of DlLiteLoaderListEntry)
        ''' <summary>
        ''' Failure reason of the official source, or Nothing if none.
        ''' </summary>
        Public OfficialError As Exception
    End Structure

    Public Class DlLiteLoaderListEntry
        ''' <summary>
        ''' Actual file name, e.g. "liteloader-installer-1.12-00-SNAPSHOT.jar".
        ''' </summary>
        Public FileName As String
        ''' <summary>
        ''' Whether this is a preview version.
        ''' </summary>
        Public IsPreview As Boolean
        ''' <summary>
        ''' Corresponding Minecraft version, e.g. "1.12.2".
        ''' </summary>
        Public Inherit As String
        ''' <summary>
        ''' Whether this is an ancient version (1.7 or earlier).
        ''' </summary>
        Public IsLegacy As Boolean
        ''' <summary>
        ''' Release time in "yyyy/mm/dd HH:mm" format.
        ''' </summary>
        Public ReleaseTime As String
        ''' <summary>
        ''' MD5 of the file.
        ''' </summary>
        Public MD5 As String
        ''' <summary>
        ''' The corresponding JSON entry.
        ''' </summary>
        Public JsonToken As JToken
    End Class

    ''' <summary>
    ''' LiteLoader version list, main loader.
    ''' </summary>
    Public DlLiteLoaderListLoader As New LoaderTask(Of Integer, DlLiteLoaderListResult)("DlLiteLoaderList Main", AddressOf DlLiteLoaderListMain)
    Private Sub DlLiteLoaderListMain(Loader As LoaderTask(Of Integer, DlLiteLoaderListResult))
        Select Case Settings.Get(Of Integer)("ToolDownloadVersion")
            Case 0
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)(DlLiteLoaderListBmclapiLoader, 30),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)(DlLiteLoaderListOfficialLoader, 30 + 60)
                }, Loader.IsForceRestarting)
            Case 1
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)(DlLiteLoaderListOfficialLoader, 5),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)(DlLiteLoaderListBmclapiLoader, 5 + 30)
                }, Loader.IsForceRestarting)
            Case Else
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)(DlLiteLoaderListOfficialLoader, 60),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)(DlLiteLoaderListBmclapiLoader, 60 + 60)
                }, Loader.IsForceRestarting)
        End Select
    End Sub

    ''' <summary>
    ''' LiteLoader version list, official source.
    ''' </summary>
    Public DlLiteLoaderListOfficialLoader As New LoaderTask(Of Integer, DlLiteLoaderListResult)("DlLiteLoaderList Official", AddressOf DlLiteLoaderListOfficialMain)
    Private Sub DlLiteLoaderListOfficialMain(Loader As LoaderTask(Of Integer, DlLiteLoaderListResult))
        Dim Result As JObject = NetRequestByClientRetry("https://dl.liteloader.com/versions/versions.json", RequireJson:=True).DeserializeJson()
        Try
            Dim Json As JObject = Result("versions")
            Dim Versions As New List(Of DlLiteLoaderListEntry)
            For Each Pair As KeyValuePair(Of String, JToken) In Json
                If Pair.Key.StartsWithF("1.6") OrElse Pair.Key.StartsWithF("1.5") Then Continue For
                Dim RealEntry As JToken = If(Pair.Value("artefacts"), Pair.Value("snapshots"))("com.mumfrey:liteloader")("latest")
                Versions.Add(New DlLiteLoaderListEntry With {
                             .Inherit = Pair.Key,
                             .IsLegacy = Pair.Key.Split(".")(1) < 8,
                             .IsPreview = RealEntry("stream").ToString.Lower = "snapshot",
                             .FileName = "liteloader-installer-" & Pair.Key & If(Pair.Key = "1.8" OrElse Pair.Key = "1.9", ".0", "") & "-00-SNAPSHOT.jar",
                             .MD5 = RealEntry("md5"),
                             .ReleaseTime = GetLocalTime(GetDate(RealEntry("timestamp"))).ToString("yyyy'/'MM'/'dd HH':'mm"),
                             .JsonToken = RealEntry
                         })
            Next
            Loader.Output = New DlLiteLoaderListResult With {.IsOfficial = True, .SourceName = "LiteLoader 官方源", .Value = Versions}
        Catch ex As Exception
            Throw New Exception("LiteLoader 官方源版本列表解析失败（" & Result.ToString & "）", ex)
        End Try
    End Sub

    ''' <summary>
    ''' LiteLoader version list, BMCLAPI.
    ''' </summary>
    Public DlLiteLoaderListBmclapiLoader As New LoaderTask(Of Integer, DlLiteLoaderListResult)("DlLiteLoaderList Bmclapi", AddressOf DlLiteLoaderListBmclapiMain)
    Private Sub DlLiteLoaderListBmclapiMain(Loader As LoaderTask(Of Integer, DlLiteLoaderListResult))
        Dim Result As JObject = NetRequestByClientRetry("https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/versions.json", RequireJson:=True).DeserializeJson()
        Try
            Dim Json As JObject = Result("versions")
            Dim Versions As New List(Of DlLiteLoaderListEntry)
            For Each Pair As KeyValuePair(Of String, JToken) In Json
                If Pair.Key.StartsWithF("1.6") OrElse Pair.Key.StartsWithF("1.5") Then Continue For
                Dim RealEntry As JToken = If(Pair.Value("artefacts"), Pair.Value("snapshots"))("com.mumfrey:liteloader")("latest")
                Versions.Add(New DlLiteLoaderListEntry With {
                             .Inherit = Pair.Key,
                             .IsLegacy = Pair.Key.Split(".")(1) < 8,
                             .IsPreview = RealEntry("stream").ToString.Lower = "snapshot",
                             .FileName = "liteloader-installer-" & Pair.Key & If(Pair.Key = "1.8" OrElse Pair.Key = "1.9", ".0", "") & "-00-SNAPSHOT.jar",
                             .MD5 = RealEntry("md5"),
                             .ReleaseTime = GetLocalTime(GetDate(RealEntry("timestamp"))).ToString("yyyy'/'MM'/'dd HH':'mm"),
                             .JsonToken = RealEntry
                         })
            Next
            Loader.Output = New DlLiteLoaderListResult With {.IsOfficial = False, .SourceName = "BMCLAPI", .Value = Versions}
        Catch ex As Exception
            Throw New Exception("LiteLoader BMCLAPI 版本列表解析失败（" & Result.ToString & "）", ex)
        End Try
    End Sub

#End Region

#Region "DlFabricList | Fabric list"

    Public Structure DlFabricListResult
        ''' <summary>
        ''' Name of the data source, e.g. "Official", "BMCLAPI".
        ''' </summary>
        Public SourceName As String
        ''' <summary>
        ''' Whether this is official real-time data.
        ''' </summary>
        Public IsOfficial As Boolean
        ''' <summary>
        ''' The fetched data.
        ''' </summary>
        Public Value As JObject
    End Structure

    ''' <summary>
    ''' Fabric list, main loader.
    ''' </summary>
    Public DlFabricListLoader As New LoaderTask(Of Integer, DlFabricListResult)("DlFabricList Main", AddressOf DlFabricListMain)
    Private Sub DlFabricListMain(Loader As LoaderTask(Of Integer, DlFabricListResult))
        Select Case Settings.Get(Of Integer)("ToolDownloadVersion")
            Case 0
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)(DlFabricListBmclapiLoader, 30),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)(DlFabricListOfficialLoader, 30 + 60)
                }, Loader.IsForceRestarting)
            Case 1
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)(DlFabricListOfficialLoader, 5),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)(DlFabricListBmclapiLoader, 5 + 30)
                }, Loader.IsForceRestarting)
            Case Else
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)(DlFabricListOfficialLoader, 60),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)(DlFabricListBmclapiLoader, 60 + 60)
                }, Loader.IsForceRestarting)
        End Select
    End Sub

    ''' <summary>
    ''' Fabric list, official source.
    ''' </summary>
    Public DlFabricListOfficialLoader As New LoaderTask(Of Integer, DlFabricListResult)("DlFabricList Official", AddressOf DlFabricListOfficialMain)
    Private Sub DlFabricListOfficialMain(Loader As LoaderTask(Of Integer, DlFabricListResult))
        Dim Result As JObject = NetRequestByClientRetry("https://meta.fabricmc.net/v2/versions", RequireJson:=True).DeserializeJson()
        Try
            Dim Output = New DlFabricListResult With {.IsOfficial = True, .SourceName = "Fabric 官方源", .Value = Result}
            If Output.Value("game") Is Nothing OrElse Output.Value("loader") Is Nothing OrElse Output.Value("installer") Is Nothing Then Throw New Exception("获取到的列表缺乏必要项")
            Loader.Output = Output
        Catch ex As Exception
            Throw New Exception("Fabric 官方源版本列表解析失败（" & Result.ToString & "）", ex)
        End Try
    End Sub

    ''' <summary>
    ''' Fabric list, BMCLAPI.
    ''' </summary>
    Public DlFabricListBmclapiLoader As New LoaderTask(Of Integer, DlFabricListResult)("DlFabricList Bmclapi", AddressOf DlFabricListBmclapiMain)
    Private Sub DlFabricListBmclapiMain(Loader As LoaderTask(Of Integer, DlFabricListResult))
        Dim Result As JObject = NetRequestByClientRetry("https://bmclapi2.bangbang93.com/fabric-meta/v2/versions", RequireJson:=True).DeserializeJson()
        Try
            Dim Output = New DlFabricListResult With {.IsOfficial = False, .SourceName = "BMCLAPI", .Value = Result}
            If Output.Value("game") Is Nothing OrElse Output.Value("loader") Is Nothing OrElse Output.Value("installer") Is Nothing Then Throw New Exception("获取到的列表缺乏必要项")
            Loader.Output = Output
        Catch ex As Exception
            Throw New Exception("Fabric BMCLAPI 版本列表解析失败（" & Result.ToString & "）", ex)
        End Try
    End Sub

    ''' <summary>
    ''' Fabric API list, official source.
    ''' </summary>
    Public DlFabricApiLoader As New LoaderTask(Of Integer, List(Of ResourceVersion))("Fabric API List Loader",
        Sub(Task As LoaderTask(Of Integer, List(Of ResourceVersion))) Task.Output = ResourceVersion.FromProjectId("fabric-api", ResourcePlatforms.Modrinth, LoadDependencies:=False))

    ''' <summary>
    ''' OptiFabric list, official source.
    ''' </summary>
    Public DlOptiFabricLoader As New LoaderTask(Of Integer, List(Of ResourceVersion))("OptiFabric List Loader",
        Sub(Task As LoaderTask(Of Integer, List(Of ResourceVersion))) Task.Output = ResourceVersion.FromProjectId("322385", ResourcePlatforms.CurseForge, LoadDependencies:=False))

#End Region

#Region "DlMod | Mod mirror requests"

    ''' <summary>
    ''' Handles requests that may involve a mod mirror source and returns a JToken.
    ''' Calls NetRequest, with retries.
    ''' </summary>
    Public Function DlModRequest(Url As String, Optional Method As HttpMethod = Nothing,
                                 Optional Content As String = Nothing, Optional ContentType As String = Nothing) As JToken
        Dim Urls As New List(Of (Url As String, Timeout As Integer))
        Dim McimUrl As String = DlSourceModGet(Url)
        If McimUrl <> Url Then
            Select Case Settings.Get(Of Integer)("ToolDownloadMod")
                Case 0
                    Urls.Add((McimUrl, 10))
                    Urls.Add((McimUrl, 10))
                    Urls.Add((Url, 30))
                Case 1
                    Urls.Add((Url, If(Url.Contains("modrinth"), 20, 10))) 'At least 20s, otherwise Modrinth can't respond in time
                    Urls.Add((McimUrl, 10))
                    Urls.Add((Url, 30))
                    Urls.Add((McimUrl, 30))
                Case Else
                    Urls.Add((Url, If(Url.Contains("modrinth"), 20, 10)))
                    Urls.Add((Url, 30))
                    Urls.Add((McimUrl, 30))
            End Select
        Else
            Urls.Add((Url, If(Url.Contains("modrinth"), 20, 10)))
            Urls.Add((Url, 30))
        End If
        Dim Errors As New List(Of Exception)
        For Each Source In Urls
            Try
                Return NetRequestByClient(Source.Url, Method, Content, ContentType, Timeout:=Source.Timeout * 1000, Encoding:=Encoding.UTF8, RequireJson:=True).DeserializeJson()
            Catch ex As Exception
                Errors.Add(ex)
            End Try
        Next
        Throw New AggregateException(Errors)
    End Function

#End Region

#Region "DlSource | Mirror download sources"

    Private DlPreferMojang As Boolean = False
    ''' <summary>
    ''' Whether to prefer the official source when downloading files (as opposed to fetching version lists).
    ''' </summary>
    Public ReadOnly Property DlSourcePreferMojang As Boolean
        Get
            Return Settings.Get(Of Integer)("ToolDownloadSource") = 2 OrElse (Settings.Get(Of Integer)("ToolDownloadSource") = 1 AndAlso DlPreferMojang)
        End Get
    End Property
    ''' <summary>
    ''' When downloading files (as opposed to fetching version lists), orders the URLs depending on whether the official source is preferred.
    ''' </summary>
    Public Function DlSourceOrder(OfficialUrls As IEnumerable(Of String), MirrorUrls As IEnumerable(Of String)) As IEnumerable(Of String)
        Return If(DlSourcePreferMojang, OfficialUrls.Union(MirrorUrls), MirrorUrls.Union(OfficialUrls))
    End Function
    ''' <summary>
    ''' Whether to prefer the official source when fetching version lists (as opposed to downloading files).
    ''' </summary>
    Public ReadOnly Property DlVersionListPreferMojang As Boolean
        Get
            Return Settings.Get(Of Integer)("ToolDownloadVersion") = 2 OrElse (Settings.Get(Of Integer)("ToolDownloadVersion") = 1 AndAlso DlPreferMojang)
        End Get
    End Property
    ''' <summary>
    ''' When fetching version lists (as opposed to downloading files), orders the URLs depending on whether the official source is preferred.
    ''' </summary>
    Public Function DlVersionListOrder(OfficialUrls As IEnumerable(Of String), MirrorUrls As IEnumerable(Of String)) As IEnumerable(Of String)
        Return If(DlVersionListPreferMojang, OfficialUrls.Union(MirrorUrls), MirrorUrls.Union(OfficialUrls))
    End Function


    ''' <summary>
    ''' Download Assets files.
    ''' </summary>
    Public Function DlSourceAssetsGet(Original As String) As IEnumerable(Of String)
        Original = Original.Replace("http://resources.download.minecraft.net", "https://resources.download.minecraft.net")
        Return DlSourceOrder(
            {Original},
            {Original.
                Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/assets").
                Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/assets").
                Replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com/assets")
            })
    End Function
    ''' <summary>
    ''' Download Libraries files.
    ''' </summary>
    Public Function DlSourceLibraryGet(Original As String) As IEnumerable(Of String)
        If {"minecraftforge", "fabricmc", "neoforged"}.Any(Function(k) Original.Contains(k)) Then 'Don't add the vanilla source
            Return {
                Original.
                    Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/maven").
                    Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/maven").
                    Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven"),
                Original.
                    Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/libraries").
                    Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/libraries").
                    Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/libraries")
            }
        Else
            Return DlSourceOrder(
                {Original},
                {Original.
                    Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/maven").
                    Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/maven").
                    Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven"),
                 Original.
                    Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/libraries").
                    Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/libraries").
                    Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/libraries")
                })
        End If
    End Function
    ''' <summary>
    ''' Download Launcher or Meta files.
    ''' Should not be used for fetching version lists (it only uses the file download source setting to decide the source order).
    ''' </summary>
    Public Function DlSourceLauncherOrMetaGet(Original As String) As IEnumerable(Of String)
        If Original Is Nothing Then Throw New Exception("无对应的 json 下载地址")
        Return DlSourceOrder(
            {Original},
            {Original.
                Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com").
                Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com").
                Replace("https://launcher.mojang.com", "https://bmclapi2.bangbang93.com").
                Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com")
            })
    End Function

    'Mod download sources
    Public Function DlSourceModGet(Original As String) As String
        Return Original.
            Replace("api.modrinth.com", "mod.mcimirror.top/modrinth").
            Replace("staging-api.modrinth.com", "mod.mcimirror.top/modrinth").
            Replace("cdn.modrinth.com", "mod.mcimirror.top").
            Replace("api.curseforge.com", "mod.mcimirror.top/curseforge").
            Replace("edge.forgecdn.net", "mod.mcimirror.top").
            Replace("mediafilez.forgecdn.net", "mod.mcimirror.top").
            Replace("media.forgecdn.net", "mod.mcimirror.top")
    End Function

    'Automatic loader switching
    Private Sub DlSourceLoader(Of InputType, OutputType)(MainLoader As LoaderTask(Of InputType, OutputType),
                                                         LoaderList As List(Of KeyValuePair(Of LoaderTask(Of InputType, OutputType), Integer)),
                                                         Optional IsForceRestart As Boolean = False)
        Dim WaitCycle As Integer = 0
        Do While True
            'Check state
            Dim BeforeLoadersAllFailed As Boolean = True
            For Each SubLoader In LoaderList
                If WaitCycle = 0 Then 'Check whether loading can be skipped and the already-loaded result used directly
                    If IsForceRestart Then Continue For 'Not if a forced refresh was requested
                    If (SubLoader.Key.Input Is Nothing Xor MainLoader.Input Is Nothing) OrElse
                       (SubLoader.Key.Input IsNot Nothing AndAlso Not SubLoader.Key.Input.Equals(MainLoader.Input)) Then Continue For 'Nor if the parent and child loaders have different inputs
                End If
                If SubLoader.Key.State <> LoadState.Failed Then BeforeLoadersAllFailed = False
                If SubLoader.Key.State = LoadState.Finished Then
                    'Check for loader success
                    MainLoader.Output = SubLoader.Key.Output
                    DlSourceLoaderCancel(LoaderList)
                    Return
                ElseIf BeforeLoadersAllFailed Then
                    'All previous loaders failed, start the next loaders directly
                    If WaitCycle < SubLoader.Value * 100 Then WaitCycle = SubLoader.Value * 100
                End If
            Next
            'First round: since the loaded result isn't used directly, start the first loader
            If WaitCycle = 0 Then
                LoaderList.First.Key.Start(MainLoader.Input, IsForceRestart)
                For Each Loader In LoaderList.Skip(1)
                    Loader.Key.State = LoadState.Waiting 'Mark the other sources as not started so the download source can be switched (#184)
                Next
            End If
            'Check for loader failure or timeout
            For i = 0 To LoaderList.Count - 1
                If WaitCycle <> LoaderList(i).Value * 100 Then Continue For
                If i < LoaderList.Count - 1 AndAlso Not LoaderList.All(Function(l) l.Key.State = LoadState.Failed) Then
                    'If there's another source, start it
                    LoaderList(i + 1).Key.Start(MainLoader.Input, IsForceRestart)
                Else
                    'Otherwise, fail
                    Dim ErrorInfo As Exception = Nothing
                    For ii = 0 To LoaderList.Count - 1
                        LoaderList(ii).Key.Input = Nothing 'Reset the input so a "retry" with the same input doesn't fail immediately
                        If LoaderList(ii).Key.Error IsNot Nothing Then
                            If ErrorInfo Is Nothing OrElse LoaderList(ii).Key.Error.Message = "无" Then
                                ErrorInfo = LoaderList(ii).Key.Error
                            End If
                        End If
                    Next
                    If ErrorInfo Is Nothing Then ErrorInfo = New TimeoutException("下载源连接超时")
                    DlSourceLoaderCancel(LoaderList)
                    Throw ErrorInfo
                End If
                Exit For
            Next
            'Timing
            Thread.Sleep(10)
            WaitCycle += 1
            'Check for parent loader interruption
            If MainLoader.IsCanceled Then
                DlSourceLoaderCancel(LoaderList)
                Return
            End If
        Loop
    End Sub
    Private Sub DlSourceLoaderCancel(Of InputType, OutputType)(LoaderList As List(Of KeyValuePair(Of LoaderTask(Of InputType, OutputType), Integer)))
        For Each Loader In LoaderList
            If Loader.Key.State = LoadState.Loading Then Loader.Key.Cancel()
        Next
    End Sub

#End Region

End Module
