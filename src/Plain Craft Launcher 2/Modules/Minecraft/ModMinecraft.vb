Public Module ModMinecraft

#Region "Folders"

    ''' <summary>
    ''' Path of the current Minecraft folder, ending with \.
    ''' </summary>
    Public Property McFolderSelected As String
        Get
            If _McFolderSelected Is Nothing Then SetMcFolder(Settings.Get(Of String)("LaunchFolderSelect")) 'Can't use McFolderSelected to trigger Set; that would be treated as setting the return value of Get
            Return _McFolderSelected
        End Get
        Set(Value As String)
            SetMcFolder(Value)
        End Set
    End Property
    Private Sub SetMcFolder(Value As String) 'Equivalent to McFolderSelected.Set
        If PathUtils.IsSame(_McFolderSelected, Value.Replace("$", Paths.Base)) Then Return
        _McFolderSelected = Value.Replace("$", Paths.Base)
        _McFolderSelected = PathUtils.AddSlashSuffix(PathUtils.ForCompare(_McFolderSelected))
        Settings.Set("LaunchFolderSelect", Value.Replace(Paths.Base, "$"))
        Logger.Info($"当前选择的 Minecraft 文件夹：{_McFolderSelected}")
    End Sub
    Private _McFolderSelected As String
    ''' <summary>
    ''' The current list of Minecraft folders.
    ''' </summary>
    Public McFolderList As New List(Of McFolder)

    Public Class McFolder
        Public Name As String
        ''' <summary>
        ''' Folder path.
        ''' Ends with \, e.g. "D:\Game\MC\.minecraft\".
        ''' </summary>
        Public Location As String
        Public Type As Types
        Public Enum Types
            Vanilla
            RenamedVanilla
            Custom
        End Enum

        Public Overrides Function Equals(obj As Object) As Boolean
            If Not (TypeOf obj Is McFolder) Then Return False
            Dim Folder = DirectCast(obj, McFolder)
            Return Name = Folder.Name AndAlso PathUtils.IsSame(Location, Folder.Location) AndAlso Type = Folder.Type
        End Function
        Public Overrides Function ToString() As String
            Return Location
        End Function
    End Class

    ''' <summary>
    ''' Loads the list of Minecraft folders.
    ''' </summary>
    Public McFolderListLoader As New LoaderTask(Of Integer, Integer)("Minecraft Folder List", AddressOf McFolderListLoadSub, Priority:=ThreadPriority.AboveNormal)
    Private Sub McFolderListLoadSub()
        Try
            'Initialize
            Dim CacheMcFolderList = New List(Of McFolder)

#Region "Read default (Original) folders, i.e. the current and official launcher folders; may yield nothing"

            'Scan the current folder
            Try
                If DirectoryUtils.Exists(Paths.Base & "versions\") Then CacheMcFolderList.Add(New McFolder With {.Name = "当前文件夹", .Location = Paths.Base, .Type = McFolder.Types.Vanilla})
                For Each Folder As DirectoryInfo In DirectoryUtils.GetInfo(Paths.Base).GetDirectories
                    If DirectoryUtils.Exists(Folder.FullName & "versions\") OrElse Folder.Name = ".minecraft" Then
                        CacheMcFolderList.Add(New McFolder With {.Name = "当前文件夹", .Location = PathUtils.AddSlashSuffix(PathUtils.ForCompare(Folder.FullName)), .Type = McFolder.Types.Vanilla})
                    End If
                Next
            Catch ex As Exception
                Logger.Warn(ex, "扫描 PCL 所在文件夹中是否有 MC 文件夹失败")
            End Try

            'Scan the official launcher folder
            Dim MojangPath As String = Paths.AppData & ".minecraft\"
            If (Not CacheMcFolderList.Any OrElse Not PathUtils.IsSame(MojangPath, CacheMcFolderList(0).Location)) AndAlso 'The current folder is not the official launcher folder
                DirectoryUtils.Exists(MojangPath & "versions\") Then 'Has permission and the versions folder exists
                CacheMcFolderList.Add(New McFolder With {.Name = "官方启动器文件夹", .Location = MojangPath, .Type = McFolder.Types.Vanilla})
            End If

#End Region

#Region "Read custom (Custom) folders; may yield nothing"

            'Format: TMZ 12>C://xxx/xx/|Test>D://xxx/xx/|Name>Path
            For Each Folder As String In Settings.Get(Of String)("LaunchFolders").ToString.Split("|"c, True)
                If Folder = "" Then Continue For
                If Not Folder.Contains(">") OrElse Not Folder.EndsWithF("\") Then
                    Hint("无效的 Minecraft 文件夹：" & Folder, HintType.Red)
                    Continue For
                End If
                Dim FolderName As String = Folder.BeforeFirst(">"), FolderPath As String = Folder.AfterFirst(">")
                Try
                    CheckPermissionWithException(FolderPath)
                    'If the folder already exists, just rename it; otherwise add it
                    Dim Renamed As Boolean = False
                    For Each OriginalFolder As McFolder In CacheMcFolderList
                        If PathUtils.IsSame(OriginalFolder.Location, FolderPath) Then
                            OriginalFolder.Name = FolderName
                            OriginalFolder.Type = McFolder.Types.RenamedVanilla
                            Renamed = True
                        End If
                    Next
                    If Not Renamed Then CacheMcFolderList.Add(New McFolder With {.Name = FolderName, .Location = PathUtils.AddSlashSuffix(PathUtils.ForCompare(FolderPath)), .Type = McFolder.Types.Custom})
                Catch ex As Exception
                    MyMsgBox("失效的 Minecraft 文件夹：" & vbCrLf & FolderPath & vbCrLf & vbCrLf & ex.GetDisplay(False), "Minecraft 文件夹失效", IsWarn:=True)
                    Logger.Warn(ex, $"无法访问 Minecraft 文件夹 {FolderPath}")
                End Try
            Next

            'Sync the custom folders to the settings
            Dim NewSetup As New List(Of String)
            For Each Folder As McFolder In CacheMcFolderList
                If Not Folder.Type = McFolder.Types.Vanilla Then NewSetup.Add(Folder.Name & ">" & Folder.Location)
            Next
            If Not NewSetup.Any() Then NewSetup.Add("") 'Prevent Join on 0 elements from returning Nothing
            Settings.Set("LaunchFolders", NewSetup.Join("|"c))

#End Region

            'If no folder is available, create .minecraft
            If Not CacheMcFolderList.Any() Then
                DirectoryUtils.Create(Paths.Base & ".minecraft\versions\")
                CacheMcFolderList.Add(New McFolder With {.Name = "当前文件夹", .Location = Paths.Base & ".minecraft\", .Type = McFolder.Types.Vanilla})
            End If

            For Each Folder As McFolder In CacheMcFolderList
                McFolderLauncherProfilesJsonCreate(Folder.Location)
            Next

            'Write back
            McFolderList = CacheMcFolderList

        Catch ex As Exception
            Logger.Error(ex, "加载 Minecraft 文件夹列表失败")
        End Try
    End Sub

    ''' <summary>
    ''' Creates the launcher_profiles.json file for a Minecraft folder.
    ''' </summary>
    Public Sub McFolderLauncherProfilesJsonCreate(Folder As String)
        Try
            If FileUtils.Exists(Folder & "launcher_profiles.json") Then Return
            Dim ResultJson As String =
"{
    ""profiles"":  {
        ""PCL"": {
            ""icon"": ""Grass"",
            ""name"": ""PCL"",
            ""lastVersionId"": ""latest-release"",
            ""type"": ""latest-release"",
            ""lastUsed"": """ & Date.Now.ToString("yyyy'-'MM'-'dd") & "T" & Date.Now.ToString("HH':'mm':'ss") & ".0000Z""
        }
    },
    ""selectedProfile"": ""PCL"",
    ""clientToken"": ""23323323323323323323323323323333""
}"
            FileUtils.Write(Folder & "launcher_profiles.json", ResultJson, encoding:=Encoding.GetEncoding("GB18030"))
            Logger.Info($"已创建 launcher_profiles.json：{Folder}")
        Catch ex As Exception
            Logger.Error(ex, $"创建 launcher_profiles.json 失败（{Folder}）")
        End Try
    End Sub

#End Region

#Region "Instance handling"

    Private _McInstanceSelected As McInstance
    ''' <summary>
    ''' The current Minecraft instance.
    ''' </summary>
    Public Property McInstanceSelected As McInstance
        Get
            Return _McInstanceSelected
        End Get
        Set(value As McInstance)
            Static _McInstanceSelectedLast As Object = 0 'Use 0 so it differs from Nothing, letting the UI initialize properly
            If ReferenceEquals(_McInstanceSelectedLast, value) Then Return
            _McInstanceSelected = value 'The value may be Nothing, which would prevent initialization, hence this roundabout approach
            _McInstanceSelectedLast = value
            'Update the Minecraft instance selected for this MC folder
            Logger.Info($"当前选择的 Minecraft 版本：{If(value?.PathVersion, "无")}")
            WriteIni(McFolderSelected & "PCL.ini", "Version", If(value?.Name, ""))
            If value Is Nothing Then Return
            'Reset the cached download folder
            PageDownloadResourceDetail.CachedFolder.Clear()
            'Re-check Nide8 auth
            If AniControlEnabled = 0 AndAlso
               Settings.Get(Of String)("VersionServerNide", Instance:=value) <> Settings.Get(Of String)("CacheNideServer") AndAlso
               Settings.Get(Of Integer)("VersionServerLogin", Instance:=value) = 3 Then
                Settings.Set("CacheNideAccess", "")
                Logger.Info("服务器改变，要求重新登录统一通行证")
            End If
            If Settings.Get(Of Integer)("VersionServerLogin", Instance:=value) = 3 Then
                Settings.Set("CacheNideServer", Settings.Get(Of String)("VersionServerNide", Instance:=value))
            End If
            'Re-check Authlib-Injector
            If AniControlEnabled = 0 AndAlso
               Settings.Get(Of String)("VersionServerAuthServer", Instance:=value) <> Settings.Get(Of String)("CacheAuthServerServer") AndAlso
               Settings.Get(Of Integer)("VersionServerLogin", Instance:=value) = 4 Then
                Settings.Set("CacheAuthAccess", "")
                Logger.Info("服务器改变，要求重新登录 Authlib-Injector")
            End If
            If Settings.Get(Of Integer)("VersionServerLogin", Instance:=value) = 4 Then
                Settings.Set("CacheAuthServerServer", Settings.Get(Of String)("VersionServerAuthServer", Instance:=value))
                Settings.Set("CacheAuthServerName", Settings.Get(Of String)("VersionServerAuthName", Instance:=value))
                Settings.Set("CacheAuthServerRegister", Settings.Get(Of String)("VersionServerAuthRegister", Instance:=value))
            End If
        End Set
    End Property

    Public Class McInstance

        Public Enum McInstanceState
            [Error]
            Original
            Snapshot
            Fool
            OptiFine
            Old
            Forge
            NeoForge
            LiteLoader
            Fabric
        End Enum

        ''' <summary>
        ''' Path of the instance folder, ending with \.
        ''' </summary>
        Public ReadOnly Property PathVersion As String
        ''' <summary>
        ''' The Minecraft root folder for this instance after applying version isolation, ending with \.
        ''' </summary>
        Public ReadOnly Property PathIndie As String
            Get
                If Not Settings.HasSaved("VersionArgumentIndieV2", Instance:=Me) Then
                    If Not IsLoaded Then Load()
                    'Decide whether this instance should be isolated
                    Dim ShouldBeIndie =
                    Function() As Boolean
                        'Migrate from the old per-instance setting: -1 undecided, 0 use global setting, 1 manually enabled, 2 manually disabled
                        If Settings.HasSaved("VersionArgumentIndie", Instance:=Me) AndAlso Settings.Get(Of Integer)("VersionArgumentIndie", Instance:=Me) > 0 Then
                            Logger.Info($"版本隔离初始化（{Name}）：从老的版本独立设置中迁移")
                            Return Settings.Get(Of Integer)("VersionArgumentIndie", Instance:=Me) = 1
                        End If
                        'Enable version isolation automatically if the instance folder contains a mods or saves folder
                        Dim ModFolder = DirectoryUtils.GetInfo(PathVersion & "mods\")
                        Dim SaveFolder = DirectoryUtils.GetInfo(PathVersion & "saves\")
                        If (ModFolder.Exists AndAlso ModFolder.EnumerateFiles.Any) OrElse (SaveFolder.Exists AndAlso SaveFolder.EnumerateDirectories.Any) Then
                            Logger.Info($"版本隔离初始化（{Name}）：版本文件夹下存在 mods 或 saves 文件夹，自动开启")
                            Return True
                        End If
                        'Decide based on the global default setting
                        Dim IsRelease As Boolean = State <> McInstanceState.Fool AndAlso State <> McInstanceState.Old AndAlso State <> McInstanceState.Snapshot
                        Logger.Info($"版本隔离初始化（{Name}）：从全局默认设置中（{Settings.Get(Of Integer)("LaunchArgumentIndieV2")}）判断，State {State}，IsRelease {IsRelease}，Modable {Modable}")
                        Select Case Settings.Get(Of Integer)("LaunchArgumentIndieV2")
                            Case 0 'Off
                                Return False
                            Case 1 'Isolate only instances that can install mods
                                Return Modable
                            Case 2 'Isolate only non-release instances
                                Return Not IsRelease
                            Case 3 'Isolate non-release instances and instances that can install mods
                                Return Not IsRelease OrElse Modable
                            Case Else 'Isolate all instances
                                Return True
                        End Select
                    End Function
                    Settings.Set("VersionArgumentIndieV2", ShouldBeIndie(), Instance:=Me)
                End If
                Return If(Settings.Get(Of Boolean)("VersionArgumentIndieV2", Instance:=Me), PathVersion, McFolderSelected)
            End Get
        End Property

        ''' <summary>
        ''' Folder name of this instance.
        ''' </summary>
        Public ReadOnly Property Name As String
            Get
                If _Name Is Nothing AndAlso Not PathVersion = "" Then _Name = PathUtils.GetLastPart(PathVersion)
                Return _Name
            End Get
        End Property
        Private _Name As String = Nothing

        ''' <summary>
        ''' Displayed description text.
        ''' </summary>
        Public Info As String = "该版本未被加载，请向作者反馈此问题"

        ''' <summary>
        ''' Raw result of the list check for this instance, unaffected by customization.
        ''' </summary>
        Public State As McInstanceState = McInstanceState.Error
        ''' <summary>
        ''' Displayed instance icon.
        ''' </summary>
        Public Logo As String
        ''' <summary>
        ''' Whether this instance is a favorite.
        ''' </summary>
        Public IsStar As Boolean = False
        ''' <summary>
        ''' Forced instance category: 0 disabled, 1 hidden, 2 and above other normal categories.
        ''' </summary>
        Public DisplayType As McInstanceCardType = McInstanceCardType.Auto
        ''' <summary>
        ''' Whether mods can be installed on this instance.
        ''' </summary>
        Public ReadOnly Property Modable As Boolean
            Get
                If Not IsLoaded Then Load()
                Return Version.ModLoaders.Flags.Any OrElse DisplayType = McInstanceCardType.API '#223
            End Get
        End Property
        ''' <summary>
        ''' Version info.
        ''' </summary>
        Public Property Version As McVersion
            Get
                If _Version IsNot Nothing Then Return _Version
                Try
                    Dim Result = New McVersion(Name, JsonObject, JsonVersion)
                    _Version = Result
                    If Result.VanillaName = "Unknown" Then Info = "PCL 无法识别该版本的 MC 版本号"
                Catch ex As Exception
                    Logger.Warn(ex, "识别 Minecraft 版本时出错")
                    _Version = New McVersion("Unknown")
                    Info = "无法识别：" & ex.Message
                End Try
                Return _Version
            End Get
            Set(value As McVersion)
                _Version = value
            End Set
        End Property
        Private _Version As McVersion = Nothing

        ''' <summary>
        ''' Release time of the version.
        ''' </summary>
        Public ReadOnly Property ReleaseTime As Date
            Get
                Return Version.ReleaseTime
            End Get
        End Property

        ''' <summary>
        ''' Gets the JSON path of this instance.
        ''' </summary>
        Public Function GetJsonPath() As String
            Dim JsonPath As String = PathVersion & Name & ".json"
            If Not FileUtils.Exists(JsonPath) Then
                'Try to find the JSON file
                JsonPath = Nothing
                For Each JsonCandidatePath In DirectoryUtils.EnumerateFiles(PathVersion, searchPattern:="*.json")
                    Try
                        Dim JsonCandidate As JObject = FileUtils.ReadAsJson(JsonCandidatePath)
                        If Not JsonCandidate.ContainsKey("mainClass") Then Continue For
                        If Not JsonCandidate.ContainsKey("type") Then Continue For
                        If Not JsonCandidate.ContainsKey("id") Then Continue For
                    Catch
                        Continue For
                    End Try
                    JsonPath = JsonCandidatePath
                    Exit For
                Next
                If JsonPath Is Nothing Then
                    Throw New Exception($"未找到版本 JSON 文件：{PathVersion}{Name}.json")
                Else
                    Logger.Warn($"未找到同名版本 JSON，自动换用 {JsonPath}")
                End If
            End If
            Return JsonPath
        End Function
        ''' <summary>
        ''' The JSON text of this instance. May throw.
        ''' </summary>
        Public Property JsonText As String
            Get
                'Quick check that the JSON starts with { and ends with }, ignoring whitespace
                Dim FastJsonCheck =
                Function(Json As String) As Boolean
                    Dim TrimedJson As String = Json.Trim()
                    Return TrimedJson.StartsWithF("{") AndAlso TrimedJson.EndsWithF("}")
                End Function
                If _JsonText Is Nothing Then
                    Dim JsonPath As String = GetJsonPath()
                    _JsonText = If(FileUtils.TryReadAsString(JsonPath), "")
                    'ReadFile returns an empty string on failure; this may be because the file is temporarily locked, so retry after a delay
                    If Not FastJsonCheck(_JsonText) Then
                        If RunInUi() Then
                            Logger.Warn("版本 JSON 文件为空或有误，由于代码在主线程运行，将不再进行重试")
                            _JsonText.DeserializeJson() 'Throws
                        Else
                            Logger.Warn($"版本 JSON 文件为空或有误，将在 2s 后重试读取（{JsonPath}）")
                            Thread.Sleep(2000)
                            _JsonText = FileUtils.ReadAsString(JsonPath)
                            If Not FastJsonCheck(_JsonText) Then _JsonText.DeserializeJson() 'Throws
                        End If
                    End If
                End If
                Return _JsonText
            End Get
            Set(value As String)
                _JsonText = value
            End Set
        End Property
        Private _JsonText As String = Nothing
        ''' <summary>
        ''' The JSON object of this instance.
        ''' Throws when getting this property if the JSON is invalid.
        ''' </summary>
        Public Property JsonObject As JObject
            Get
                If _JsonObject Is Nothing Then
                    Dim Text As String = JsonText 'Triggers the Get of JsonText
                    Try
                        _JsonObject = Text.DeserializeJson()
                        'Convert HMCL key entries
                        If _JsonObject.ContainsKey("patches") AndAlso Not _JsonObject.ContainsKey("time") Then
                            IsHmclFormatJson = True
                            'Merge JSON
                            Dim CurrentObject As JObject = Nothing
                            Dim SubjsonList As New List(Of JObject)
                            For Each Subjson As JObject In _JsonObject("patches")
                                SubjsonList.Add(Subjson)
                            Next
                            SubjsonList = SubjsonList.SortByComparison(
                                Function(Left, Right) Val(If(Left("priority"), "0").ToString) < Val(If(Right("priority"), "0").ToString))
                            For Each Subjson As JObject In SubjsonList
                                Dim Id As String = Subjson("id")
                                If Id IsNot Nothing Then
                                    'Merge JSON
                                    Logger.Info($"合并 HMCL 分支项：{Id}")
                                    If CurrentObject IsNot Nothing Then
                                        CurrentObject.Merge(Subjson)
                                    Else
                                        CurrentObject = Subjson
                                    End If
                                Else
                                    Logger.Info("存在为空的 HMCL 分支项")
                                End If
                            Next
                            _JsonObject = CurrentObject
                            'Modify additional entries
                            _JsonObject("id") = Name
                            If _JsonObject.ContainsKey("inheritsFrom") Then _JsonObject.Remove("inheritsFrom")
                        End If
                        'Merge with the inherited version
                        Dim InstanceNameInheritFrom = Nothing
                        Try
                            'Mutually inheriting versions would read JSON recursively forever, causing an uncatchable stack overflow
                            InheritDepth += 1
                            If InheritDepth > 16 Then Throw New Exception("版本依赖项出现循环引用：" & Name)
                            InstanceNameInheritFrom = If(_JsonObject("inheritsFrom") Is Nothing, "", _JsonObject("inheritsFrom").ToString)
                            If InstanceNameInheritFrom = Name Then
                                Logger.Warn($"自引用的继承版本：{Name}")
                                InstanceNameInheritFrom = ""
                                Exit Try
                            End If
                            Dim VisitedNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {Name}
Recheck:
                            If InstanceNameInheritFrom <> "" Then
                                If Not VisitedNames.Add(InstanceNameInheritFrom) Then Throw New Exception("版本依赖项出现循环引用：" & InstanceNameInheritFrom)
                                Dim InheritInstance As New McInstance(InstanceNameInheritFrom)
                                If InheritInstance.InheritName = InstanceNameInheritFrom Then Throw New Exception("版本依赖项出现嵌套：" & InstanceNameInheritFrom)
                                InstanceNameInheritFrom = InheritInstance.InheritName
                                'Merge Libraries: child version first, parent after (#5978: if several jars contain the same class, Java 8 and earlier pick the first one in -cp order)
                                Dim CurrentLib As JArray = _JsonObject("libraries").DeepClone()
                                For Each LibToken In InheritInstance.JsonObject("libraries")
                                    CurrentLib.Add(LibToken)
                                Next
                                'Merge other entries: child version takes precedence over parent
                                InheritInstance.JsonObject.Merge(_JsonObject)
                                _JsonObject = InheritInstance.JsonObject
                                _JsonObject("libraries") = CurrentLib
                                GoTo Recheck
                            End If
                        Catch ex As Exception
                            Logger.Warn(ex, $"合并版本依赖项 JSON 失败（{If(InstanceNameInheritFrom, "null")}）")
                        Finally
                            InheritDepth -= 1
                        End Try
                    Catch ex As Exception
                        Throw New Exception("初始化版本 JSON 时失败（" & If(Name, "null") & "）", ex)
                    End Try
                End If
                Return _JsonObject
            End Get
            Set(value As JObject)
                _JsonObject = value
            End Set
        End Property
        Private _JsonObject As JObject = Nothing
        <ThreadStatic> Private Shared InheritDepth As Integer
        ''' <summary>
        ''' Whether this uses the old JSON format.
        ''' </summary>
        Public ReadOnly Property IsOldJson As Boolean
            Get
                Return JsonObject("minecraftArguments") IsNot Nothing AndAlso JsonObject("minecraftArguments") <> ""
            End Get
        End Property
        ''' <summary>
        ''' Whether the JSON is in HMCL format.
        ''' </summary>
        Public Property IsHmclFormatJson As Boolean = False

        ''' <summary>
        ''' The version.json file object inside the version jar.
        ''' Returns Nothing if absent.
        ''' </summary>
        Public ReadOnly Property JsonVersion As JObject
            Get
                Static JsonVersionInited As Boolean = False
                If Not JsonVersionInited Then
                    JsonVersionInited = True
                    Try
                        If Not FileUtils.Exists($"{PathVersion}{Name}.jar") Then Exit Try
                        Using JarArchive = FileUtils.OpenZip(PathVersion & Name & ".jar")
                            Dim VersionJson As ZipArchiveEntry = JarArchive.GetEntry("version.json")
                            If VersionJson Is Nothing Then Exit Try
                            Using VersionJsonStream As New StreamReader(VersionJson.Open)
                                _JsonVersion = VersionJsonStream.ReadToEnd().DeserializeJson()
                            End Using
                        End Using
                    Catch ex As Exception
                        Logger.Warn(ex, $"从版本 jar 中读取 version.json 失败（{PathVersion}{Name}.jar）")
                    End Try
                End If
                Return _JsonVersion
            End Get
        End Property
        Private _JsonVersion As JObject = Nothing

        ''' <summary>
        ''' The version this instance depends on. Empty string if none.
        ''' </summary>
        Public ReadOnly Property InheritName As String
            Get
                If _InheritName Is Nothing Then
                    _InheritName = If(JsonObject("inheritsFrom"), "").ToString
                    'Very old LiteLoader versions (e.g. 1.5.2) have no Inherits, so the real inherited version must be determined manually
                    'Also, this loading happens before the instance type is determined, so LiteLoader must be detected manually
                    'If the instance provides a different jar, the required jar may have been changed, so skip the Inherit replacement
                    If JsonText.Contains("liteloader") AndAlso Version.VanillaName <> Name AndAlso Not JsonText.Contains("logging") Then
                        If If(JsonObject("jar"), Version.VanillaName).ToString = Version.VanillaName Then _InheritName = Version.VanillaName
                    End If
                    'HMCL instances have no JSON
                    If IsHmclFormatJson Then _InheritName = ""
                End If
                Return _InheritName
            End Get
        End Property
        Private _InheritName As String = Nothing

        Public Config As JsonConfigProvider
        Public Sub ResetSettingsCache()
            IniClearCache(PathIndie & "options.txt")
            IniClearCache(PathVersion & "PCL\Setup.ini")
            Config.DiscardCache()
            For Each Entry In Settings.Entries.Values
                If Entry.Source = Settings.Sources.Instance Then Entry.ValueCache.Remove(PathVersion)
            Next
        End Sub

        ''' <summary>
        ''' Initializes from the instance name, or the full path of the instance folder.
        ''' </summary>
        Public Sub New(NameOrFullPath As String)
            NameOrFullPath = PathUtils.RemoveExtendedPrefix(NameOrFullPath)
            PathVersion = PathUtils.AddSlashSuffix(If(NameOrFullPath.Contains(":"), "", McFolderSelected & "versions\") & NameOrFullPath)
            Config = New JsonConfigProvider(Path.Combine(PathVersion, "PCL\config.json"))
        End Sub

        ''' <summary>
        ''' Checks the Minecraft instance. If the check passes, State becomes Original and True is returned.
        ''' </summary>
        Public Function Check() As Boolean

            'Check the folder
            If Not DirectoryUtils.Exists(PathVersion) Then
                State = McInstanceState.Error
                Info = "未找到版本 " & Name
                Return False
            End If
            'Verify the JSON is usable
            Try
                If JsonObject("mainClass") Is Nothing Then Throw New Exception("版本 JSON 中没有 mainClass 项！") 'This also triggers the Get of JsonObject
            Catch ex As Exception
                Logger.Warn(ex, $"版本 JSON 可用性检查失败（{PathVersion}）")
                JsonText = ""
                JsonObject = Nothing
                Info = ex.Message
                State = McInstanceState.Error
                Return False
            End Try
            'Check that the version number can be obtained
            Try
                If String.IsNullOrEmpty(Version.VanillaName) Then Throw New Exception("无法获取版本号，结果为空")
            Catch ex As Exception
                Logger.Warn(ex, $"版本号获取失败（{Name}）")
                State = McInstanceState.Error
                Info = "版本号获取失败：" & ex.GetDisplay(False)
                Return False
            End Try
            'Check the dependency version
            Try
                If Not InheritName = "" Then
                    If Not FileUtils.Exists($"{PathUtils.RemoveLastPart(PathVersion)}\{InheritName}\{InheritName}.json") Then
                        State = McInstanceState.Error
                        Info = "需要安装 " & InheritName & " 作为前置版本"
                        Return False
                    End If
                End If
            Catch ex As Exception
                Logger.Warn(ex, $"依赖版本检查出错（{Name}）")
                State = McInstanceState.Error
                Info = "未知错误：" & ex.GetDisplay(False)
                Return False
            End Try

            State = McInstanceState.Original
            Return True
        End Function
        ''' <summary>
        ''' Loads detailed info of the Minecraft instance. Doesn't use the cache, and updates it.
        ''' </summary>
        Public Function Load() As McInstance
            Try
                'Check the instance; on error, skip the data resolution stage
                If Not Check() Then GoTo ExitDataLoad
#Region "Determine instance category"
                Select Case Version.VanillaName 'Loading is completed when the Version.Original object is fetched
                    Case "Unknown"
                        State = McInstanceState.Error
                    Case "Old"
                        State = McInstanceState.Old
                    Case Else
                        'April Fools and snapshot versions
                        If Version.IsFool Then
                            State = McInstanceState.Fool
                        ElseIf Version.IsSnapshot Then
                            State = McInstanceState.Snapshot
                        End If
                        'OptiFine
                        If Version.HasOptiFine Then
                            State = McInstanceState.OptiFine
                        End If
                        'LiteLoader
                        If Version.ModLoaders.HasFlagF(ModLoaders.LiteLoader) Then
                            State = McInstanceState.LiteLoader
                        End If
                        'Fabric, Forge, NeoForge
                        If Version.ModLoaders.HasFlagF(ModLoaders.Fabric) Then
                            State = McInstanceState.Fabric
                        ElseIf Version.ModLoaders.HasFlagF(ModLoaders.Forge) Then
                            State = McInstanceState.Forge
                        ElseIf Version.ModLoaders.HasFlagF(ModLoaders.NeoForge) Then
                            State = McInstanceState.NeoForge
                        End If
                End Select
#End Region
ExitDataLoad:
                'Determine the instance icon
                Logo = ReadIni(PathVersion & "PCL\Setup.ini", "Logo", "")
                If Logo = "" OrElse Not CType(ReadIni(PathVersion & "PCL\Setup.ini", "LogoCustom", False), Boolean) Then
                    Select Case State
                        Case McInstanceState.Original
                            Logo = PathImage & "Blocks/Grass.png"
                        Case McInstanceState.Snapshot
                            Logo = PathImage & "Blocks/CommandBlock.png"
                        Case McInstanceState.Old
                            Logo = PathImage & "Blocks/CobbleStone.png"
                        Case McInstanceState.Forge
                            Logo = PathImage & "Blocks/Anvil.png"
                        Case McInstanceState.NeoForge
                            Logo = PathImage & "Blocks/NeoForge.png"
                        Case McInstanceState.Fabric
                            Logo = PathImage & "Blocks/Fabric.png"
                        Case McInstanceState.OptiFine
                            Logo = PathImage & "Blocks/GrassPath.png"
                        Case McInstanceState.LiteLoader
                            Logo = PathImage & "Blocks/Egg.png"
                        Case McInstanceState.Fool
                            Logo = PathImage & "Blocks/GoldBlock.png"
                        Case Else
                            Logo = PathImage & "Blocks/RedstoneBlock.png"
                    End Select
                End If
                'Determine the instance description
                RefreshInfo()
                'Determine the favorite state
                IsStar = ReadIni(PathVersion & "PCL\Setup.ini", "IsStar", False)
                'Determine the display category
                DisplayType = ReadIni(PathVersion & "PCL\Setup.ini", "DisplayType", McInstanceCardType.Auto)
                'Write to cache
                If DirectoryUtils.Exists(PathVersion) Then
                    WriteIni(PathVersion & "PCL\Setup.ini", "State", State)
                    WriteIni(PathVersion & "PCL\Setup.ini", "Info", Info)
                    WriteIni(PathVersion & "PCL\Setup.ini", "Logo", Logo)
                End If
                If State <> McInstanceState.Error AndAlso Version.Reliable Then
                    WriteIni(PathVersion & "PCL\Setup.ini", "ReleaseTime", ReleaseTime.ToString("yyyy'-'MM'-'dd HH':'mm"))
                    WriteIni(PathVersion & "PCL\Setup.ini", "VersionFabric", If(Version.ModLoaders.HasFlagF(ModLoaders.Fabric), Version.ModLoaderVersions(ModLoaders.Fabric), Nothing))
                    WriteIni(PathVersion & "PCL\Setup.ini", "VersionOptiFine", If(Version.OptiFineAsMod, Nothing, Version.OptiFine))
                    WriteIni(PathVersion & "PCL\Setup.ini", "VersionLiteLoader", Version.ModLoaders.HasFlagF(ModLoaders.LiteLoader))
                    WriteIni(PathVersion & "PCL\Setup.ini", "VersionForge", If(Version.ModLoaders.HasFlagF(ModLoaders.Forge), Version.ModLoaderVersions(ModLoaders.Forge), Nothing))
                    WriteIni(PathVersion & "PCL\Setup.ini", "VersionNeoForge", If(Version.ModLoaders.HasFlagF(ModLoaders.NeoForge), Version.ModLoaderVersions(ModLoaders.NeoForge), Nothing))
                    WriteIni(PathVersion & "PCL\Setup.ini", "VersionVanillaName", Version.VanillaName)
                    WriteIni(PathVersion & "PCL\Setup.ini", "VersionVanilla", Version.Vanilla.ToString)
                End If
            Catch ex As Exception
                Info = ex.GetDisplay(False)
                Logo = PathImage & "Blocks/RedstoneBlock.png"
                State = McInstanceState.Error
                Logger.Warn(ex, $"加载版本失败（{Name}）")
            Finally
                IsLoaded = True
            End Try
            Return Me
        End Function
        Public IsLoaded As Boolean = False
        ''' <summary>
        ''' Refreshes the instance description based on the current component info.
        ''' </summary>
        Public Sub RefreshInfo()
            Dim CustomInfo As String = ReadIni(PathVersion & "PCL\Setup.ini", "CustomInfo")
            If CustomInfo <> "" Then
                Info = CustomInfo
            Else
                Info = VersionDisplayName()
                If State = McInstanceState.Fool Then
                    Info = GetMcFoolName(Version.VanillaName)
                ElseIf State <> McInstanceState.Error Then
                    If Settings.Get(Of Integer)("VersionServerLogin", Instance:=Me) = 3 Then Info += ", 统一通行证验证"
                    If Settings.Get(Of Integer)("VersionServerLogin", Instance:=Me) = 4 Then Info += ", Authlib 验证"
                End If
            End If
        End Sub
        ''' <summary>
        ''' Gets a short description of the version info.
        ''' E.g. "快照 16w01a", "原版 1.12.2, Forge 1.2.3", "愚人节版本 2.0".
        ''' </summary>
        Public Function VersionDisplayName() As String
            'Mod loader info
            Dim ModLoaderInfo As String = ""
            If Version.ModLoaders.HasFlagF(ModLoaders.Forge) Then ModLoaderInfo += ", Forge" & If(Version.ModLoaderVersions(ModLoaders.Forge) = "未知版本", "", " " & Version.ModLoaderVersions(ModLoaders.Forge))
            If Version.ModLoaders.HasFlagF(ModLoaders.NeoForge) Then ModLoaderInfo += ", NeoForge" & If(Version.ModLoaderVersions(ModLoaders.NeoForge) = "未知版本", "", " " & Version.ModLoaderVersions(ModLoaders.NeoForge))
            If Version.ModLoaders.HasFlagF(ModLoaders.Fabric) Then ModLoaderInfo += ", Fabric" & If(Version.ModLoaderVersions(ModLoaders.Fabric) = "未知版本", "", " " & Version.ModLoaderVersions(ModLoaders.Fabric))
            If Version.HasOptiFine Then ModLoaderInfo += ", OptiFine" & If(Version.OptiFine = "未知版本", "", " " & Version.OptiFine.Replace("-", " ").Replace("_", " "))
            If Version.ModLoaders.HasFlagF(ModLoaders.LiteLoader) Then ModLoaderInfo += ", LiteLoader"
            'Basic info
            Dim Info As String
            Select Case State
                Case McInstanceState.Snapshot, McInstanceState.Original, McInstanceState.Forge, McInstanceState.NeoForge, McInstanceState.Fabric, McInstanceState.OptiFine, McInstanceState.LiteLoader
                    If Version.VanillaName.ContainsIgnoreCase("pre") Then
                        Info = "预发布版 " & Version.VanillaName
                    ElseIf Version.VanillaName.ContainsIgnoreCase("rc") Then
                        Info = "发布候选 " & Version.VanillaName
                    ElseIf Version.VanillaName.ContainsIgnoreCase("experimental") Then
                        Info = "实验性快照" & Version.VanillaName
                    ElseIf Version.VanillaName = "pending" Then
                        Info = "实验性快照"
                    ElseIf Version.IsSnapshot Then
                        Info = If(Version.Reliable, "快照版 " & Version.VanillaName.Replace("-snapshot", ""), "快照版")
                    Else
                        Info = If(Version.Reliable, "正式版 " & Version.VanillaName, "正式版")
                    End If
                Case McInstanceState.Old
                    Info = "远古版本"
                Case McInstanceState.Fool
                    Info = "愚人节版本 " & Version.VanillaName
                Case McInstanceState.Error
                    Return Me.Info 'Already has an error message
                Case Else
                    Return "发生了未知错误，请向作者反馈此问题"
            End Select
            Return (Info & ModLoaderInfo).Replace("_", "-")
        End Function

        Public Function ToListItem(Optional ContentHandler As Action(Of MyListItem, EventArgs) = Nothing) As MyVirtualizingElement(Of MyListItem)
            Return New MyVirtualizingElement(Of MyListItem)(
            Function()
                Dim NewItem As New MyListItem With {
                    .Info = Info, .Height = 42, .Tag = Me, .SnapsToDevicePixels = True, .Type = MyListItem.CheckType.Clickable,
                    .ContentHandler = ContentHandler
                }
                'Title
                NewItem.Inlines.Clear()
                NewItem.Inlines.Add(New Run(Name))
                If ReadIni(PathVersion & "PCL\Setup.ini", "CustomInfo") <> "" Then 'If the instance has a custom description, show its version number in light gray after the title
                    NewItem.Inlines.Add(New Run("  |  " & VersionDisplayName()) With {.Foreground = New MyColor(215, 215, 215), .FontSize = 12})
                End If
                'Logo
                Try
                    If Logo.EndsWithF("PCL\Logo.png") Then
                        NewItem.Logo = PathVersion & "PCL\Logo.png" 'Fix a bug in old versions where custom logos were stored as full paths and failed to load after moving
                    Else
                        NewItem.Logo = Logo
                    End If
                Catch ex As Exception
                    Logger.Error(ex, "加载版本图标失败", LogBehavior.Toast)
                    NewItem.Logo = "pack://application:,,,/images/Blocks/RedstoneBlock.png"
                End Try
                Return NewItem
            End Function) With {.Height = 42}
        End Function

        ''' <summary>
        ''' Renames an MC instance.
        ''' </summary>
        Public Function Rename(NewName As String) As McInstance
            Dim OldName = Name
            Dim OldPath = PathVersion
            Dim NewPath = PathUtils.AddSlashSuffix(Path.Combine(PathUtils.RemoveLastPart(OldPath), NewName))
            'Re-read the instance JSON so entries already merged into JsonObject don't get saved again
            Dim JsonObject As JObject
            Dim OldJsonPath = GetJsonPath()
            Try
                JsonObject = FileUtils.ReadAsJson(OldJsonPath)
            Catch ex As Exception
                Logger.Warn(ex, "在重命名读取 JSON 时失败")
                JsonObject = Me.JsonObject
            End Try
            'Clear the cache
            Config.Save()
            ResetSettingsCache()
            'Rename the main folder
            DirectoryUtils.Move(OldPath, NewPath)
            'Rename the jar file and natives folder
            'Don't rename by iterating, otherwise other files may be hit by mistake when the instance name is very short (#6443)
            If DirectoryUtils.Exists($"{NewPath}{OldName}-natives") Then DirectoryUtils.Move($"{NewPath}{OldName}-natives", $"{NewPath}{NewName}-natives")
            If FileUtils.Exists($"{NewPath}{OldName}.jar") Then FileUtils.Move($"{NewPath}{OldName}.jar", $"{NewPath}{NewName}.jar")
            'Replace paths in the instance settings file
            If FileUtils.Exists(NewPath & "PCL\Setup.ini") Then
                FileUtils.Write(NewPath & "PCL\Setup.ini", FileUtils.ReadAsString(NewPath & "PCL\Setup.ini").Replace(OldPath, NewPath))
            End If
            'Change the selected instance
            If ReadIni(McFolderSelected & "PCL.ini", "Version") = OldName Then WriteIni(McFolderSelected & "PCL.ini", "Version", NewName)
            'Update the instance JSON
            Try
                JsonObject("id") = NewName
                FileUtils.Delete(NewPath & PathUtils.GetLastPart(OldJsonPath))
                FileUtils.Write(NewPath & NewName & ".json", JsonObject.ToString)
            Catch ex As Exception
                Logger.Warn(ex, "重命名版本 json 失败")
            End Try
            Return New McInstance(NewPath).Load()
        End Function

        'Operator support
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim version = TryCast(obj, McInstance)
            Return version IsNot Nothing AndAlso PathUtils.IsSame(PathVersion, version.PathVersion)
        End Function
        Public Shared Operator =(a As McInstance, b As McInstance) As Boolean
            If a Is Nothing AndAlso b Is Nothing Then Return True
            If a Is Nothing OrElse b Is Nothing Then Return False
            Return PathUtils.IsSame(a.PathVersion, b.PathVersion)
        End Operator
        Public Shared Operator <>(a As McInstance, b As McInstance) As Boolean
            Return Not (a = b)
        End Operator

    End Class

    ''' <summary>
    ''' Exclusive locks on MC instances.
    ''' </summary>
    Public NotInheritable Class McInstanceLock
        Private Shared ReadOnly LockedPaths As New ConcurrentDictionary(Of String, IWorker)(StringComparer.OrdinalIgnoreCase)
        Friend Shared ReadOnly Property IsAnyLocked As Boolean
            Get
                Return Not LockedPaths.IsEmpty
            End Get
        End Property
        ''' <summary>
        ''' Locks the given MC instance until <paramref name="Worker"/> finishes, then releases the lock automatically.
        ''' <para/> Returns false only if the instance is already locked, in which case the lock state is not changed.
        ''' </summary>
        Public Shared Function TryAcquire(Worker As IWorker, InstancePath As String) As Boolean
            InstancePath = PathUtils.ForCompare(InstancePath)
            If Not LockedPaths.TryAdd(InstancePath, Worker) Then Return False
            AddHandler Worker.Stopped, Sub() LockedPaths.TryRemove(InstancePath, Nothing)
            Return True
        End Function
        ''' <summary>
        ''' Checks whether an MC instance is currently locked.
        ''' </summary>
        Public Shared Function IsLocked(Instance As McInstance) As Boolean
            If Instance Is Nothing Then Return False
            Return LockedPaths.ContainsKey(PathUtils.ForCompare(Instance.PathVersion))
        End Function
    End Class

    ''' <summary>
    ''' Gets the April Fools version description for a version name. Returns an empty string for non-April Fools versions.
    ''' </summary>
    Public Function GetMcFoolName(Name As String) As String
        Name = Name.Lower
        If Name.StartsWithF("2.0") Then
            Return "2013 | 这个秘密计划了两年的更新将游戏推向了一个新高度！"
        ElseIf Name = "15w14a" Then
            Return "2015 | 作为一款全年龄向的游戏，我们需要和平，需要爱与拥抱。"
        ElseIf Name = "1.rv-pre1" Then
            Return "2016 | 是时候将现代科技带入 Minecraft 了！"
        ElseIf Name = "3d shareware v1.34" Then
            Return "2019 | 我们从地下室的废墟里找到了这个开发于 1994 年的杰作！"
        ElseIf Name.StartsWithF("20w14inf") OrElse Name = "20w14∞" Then
            Return "2020 | 我们加入了 20 亿个新的维度，让无限的想象变成了现实！"
        ElseIf Name = "22w13oneblockatatime" Then
            Return "2022 | 一次一个方块更新！迎接全新的挖掘、合成与骑乘玩法吧！"
        ElseIf Name = "23w13a_or_b" Then
            Return "2023 | 研究表明：玩家喜欢作出选择——越多越好！"
        ElseIf Name = "24w14potato" Then
            Return "2024 | 毒马铃薯一直都被大家忽视和低估，于是我们超级加强了它！"
        ElseIf Name = "25w14craftmine" Then
            Return "2025 | 你可以合成任何东西——包括合成你的世界！"
        ElseIf Name = "26w14a" Then
            Return "2026 | 为什么需要物品栏？让方块们跟着你走吧！"
        Else
            Return ""
        End If
    End Function

    ''' <summary>
    ''' All instance lists, grouped by card.
    ''' </summary>
    Public McInstanceList As New Dictionary(Of McInstanceCardType, List(Of McInstance))

#End Region

#Region "Instance list loading"

    ''' <summary>
    ''' Whether this load must force-refresh the instance list.
    ''' </summary>
    Public McInstanceListForceRefresh As Boolean = False
    ''' <summary>
    ''' Whether this is the first time the instance list is loaded since PCL was opened.
    ''' This deletes all .pclignore files instead of skipping the corresponding instances.
    ''' </summary>
    Private IsFirstMcInstanceListLoad As Boolean = True

    ''' <summary>
    ''' Loads the instance list of the Minecraft folder.
    ''' </summary>
    Public McInstanceListLoader As New LoaderTask(Of String, Integer)("Minecraft Instance List", AddressOf InitMcInstanceList) With {.ReloadTimeout = 1}
    Private Sub InitMcInstanceList(Loader As LoaderTask(Of String, Integer))
        'Start loading
        Dim PathMc As String = Loader.Input
        Try
            'Initialize
            McInstanceList = New Dictionary(Of McInstanceCardType, List(Of McInstance))

            'Check whether the cache needs updating
            Dim FolderList As New List(Of String)
            If DirectoryUtils.Exists(PathMc & "versions") Then 'Don't use CheckPermission; it changes the write time, which would force the folder to refresh
                Try
                    For Each Folder As DirectoryInfo In DirectoryUtils.GetInfo(PathMc & "versions").GetDirectories
                        FolderList.Add(Folder.Name)
                    Next
                Catch ex As Exception
                    Throw New Exception("无法读取版本文件夹，可能是由于没有权限（" & PathMc & "versions）", ex)
                End Try
            End If
            'Unavailable
            If Not FolderList.Any() Then
                WriteIni(PathMc & "PCL.ini", "InstanceCache", "") 'Clear the cache
                GoTo OnLoaded
            End If
            'Has available instances
            Dim FolderListCheck As Integer = (Versions.McInstanceCacheVersion & "#" & FolderList.Join("#"c)).GetStableHashCode() Mod (Integer.MaxValue - 1) 'Generate an identifier from the list of folder names
            If Not McInstanceListForceRefresh AndAlso Val(ReadIni(PathMc & "PCL.ini", "InstanceCache")) = FolderListCheck Then
                'Cache can be used
                Dim Result = InitMcInstanceListWithCache(PathMc)
                If Result Is Nothing Then
                    GoTo Reload
                Else
                    McInstanceList = Result
                End If
            Else
                'Folder list doesn't match
Reload:
                McInstanceListForceRefresh = False
                Logger.Info("文件夹列表变更，重载所有版本")
                WriteIni(PathMc & "PCL.ini", "InstanceCache", FolderListCheck)
                McInstanceList = InitMcInstanceListWithoutCache(PathMc)
            End If
            IsFirstMcInstanceListLoad = False

            'Change the currently selected instance
OnLoaded:
            If Loader.IsCanceled Then Return
            If McInstanceList.Any(Function(v) v.Key <> McInstanceCardType.Error) Then
                'Try to read the stored selection
                Dim SavedSelection As String = ReadIni(PathMc & "PCL.ini", "Version")
                If SavedSelection <> "" Then
                    For Each Card As KeyValuePair(Of McInstanceCardType, List(Of McInstance)) In McInstanceList
                        For Each Instance As McInstance In Card.Value
                            If Instance.Name = SavedSelection AndAlso Not Instance.State = McInstance.McInstanceState.Error Then
                                'Use the stored selection
                                McInstanceSelected = Instance
                                Logger.Info($"选择该文件夹储存的 Minecraft 版本：{McInstanceSelected.PathVersion}")
                                Return
                            End If
                        Next
                    Next
                End If
                If Not McInstanceList.First.Value(0).State = McInstance.McInstanceState.Error Then
                    'Automatically select the first item
                    McInstanceSelected = McInstanceList.First.Value(0)
                    Logger.Info($"自动选择 Minecraft 版本：{McInstanceSelected.PathVersion}")
                End If
            Else
                McInstanceSelected = Nothing
                Logger.Info("未找到可用 Minecraft 版本")
            End If
        Catch ex As Exception
            If Loader.IsCanceled OrElse ex.IsCanceled Then Return '#5617
            WriteIni(PathMc & "PCL.ini", "InstanceCache", "") 'Require reloading next time
            Logger.Error(ex, "加载 .minecraft 版本列表失败")
        End Try
    End Sub

    'Get the instance list
    Private Function InitMcInstanceListWithCache(Folder As String) As Dictionary(Of McInstanceCardType, List(Of McInstance))
        Dim Results As New Dictionary(Of McInstanceCardType, List(Of McInstance))
        Try
            Dim CardCount As Integer = ReadIni(Folder & "PCL.ini", "CardCount", -1)
            If CardCount = -1 Then Return Nothing
            For i = 0 To CardCount - 1
                Dim CardType As McInstanceCardType = ReadIni(Folder & "PCL.ini", "CardKey" & (i + 1), ":")
                Dim InstanceList As New List(Of McInstance)

                'Read instances in a loop
                For Each VersionName As String In ReadIni(Folder & "PCL.ini", "CardValue" & (i + 1), ":").Split(":")
                    If VersionName = "" Then Continue For
                    Dim FolderVersions As String = $"{Folder}versions\{VersionName}\"
                    If FileUtils.Exists(FolderVersions & ".pclignore") Then
                        If IsFirstMcInstanceListLoad Then
                            Logger.Info($"清理残留的忽略项目：{FolderVersions}") '#2781
                            FileUtils.Delete(FolderVersions & ".pclignore")
                        Else
                            Logger.Info($"跳过要求忽略的项目：{FolderVersions}")
                            Continue For
                        End If
                    End If
                    Try

                        'Read a single instance
                        Dim Instance As New McInstance(FolderVersions)
                        InstanceList.Add(Instance)
                        Instance.Info = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "CustomInfo", "")
                        If Instance.Info = "" Then Instance.Info = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "Info", Instance.Info)
                        Instance.Logo = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "Logo", Instance.Logo)
                        Instance.State = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "State", Instance.State)
                        Instance.IsStar = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "IsStar", False)
                        Instance.DisplayType = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "DisplayType", McInstanceCardType.Auto)
                        Dim Manifest As McInstallManifest = If(Instance.State = McInstance.McInstanceState.Error, Nothing, Configs.McInstallManifest.Get(Instance.Config))
                        'Check the manifest for outdated files and update them
                        If Manifest IsNot Nothing AndAlso Manifest.SchemaVersion <= Versions.McInstallManifestVersion AndAlso Manifest.ManagedFiles.Any(Function(File) File.Component IsNot Nothing) Then
                            Dim LatestManifest = Manifest.RefreshModComponents(Instance.PathIndie)
                            If LatestManifest IsNot Manifest Then
                                Manifest = LatestManifest
                                Configs.McInstallManifest.Set(Manifest, Instance.Config)
                                Instance.Config.Save()
                            End If
                        End If
                        Dim CachedVanillaName = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "VersionVanillaName", "Unknown")
                        If Instance.State <> McInstance.McInstanceState.Error AndAlso (Manifest IsNot Nothing OrElse CachedVanillaName <> "Unknown") Then
                            Dim Version As McVersion
                            If Manifest IsNot Nothing Then
                                'Read version info from the manifest
                                Version = New McVersion(Manifest.Components) With {
                                    .OptiFineAsMod = Manifest.ManagedFiles.Any(Function(File) File.Component = "OptiFine")}
                            Else
                                'Read version info from Setup.ini
                                Dim ReadCachedInfo =
                                Function(Key As String) As String
                                    Dim Value = ReadIni(Instance.PathVersion & "PCL\Setup.ini", Key, Nothing)
                                    Return If(Value = "", Nothing, Value) 'Compatibility with empty values in old Setup.ini files
                                End Function
                                Version = New McVersion(CachedVanillaName) With {.OptiFine = ReadCachedInfo("VersionOptiFine")}
                                For Each Loader In {ModLoaders.Fabric, ModLoaders.Forge, ModLoaders.NeoForge}
                                    Dim CachedVersion = ReadCachedInfo($"Version{Loader}")
                                    If CachedVersion IsNot Nothing Then Version.ModLoaderVersions(Loader) = CachedVersion
                                Next
                                If ReadIni(Instance.PathVersion & "PCL\Setup.ini", "VersionLiteLoader", False) Then Version.ModLoaderVersions(ModLoaders.LiteLoader) = "未知版本"
                            End If
                            Version.ReleaseTime = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "ReleaseTime", New Date(1970, 1, 1, 15, 0, 0))
                            Version.IsFool = Version.IsFool OrElse Instance.State = McInstance.McInstanceState.Fool
                            Version.IsSnapshot = Version.IsSnapshot OrElse Instance.State = McInstance.McInstanceState.Snapshot OrElse Instance.Name.ContainsIgnoreCase("combat")
                            Instance.Version = Version
                            If Manifest IsNot Nothing Then Instance.RefreshInfo()
                        Else
                            Instance.Load()
                        End If

                        'Re-check errored instances
                        If Instance.State = McInstance.McInstanceState.Error Then
                            'Retry getting the version info
                            Dim OldDesc As String = Instance.Info
                            Instance.State = McInstance.McInstanceState.Original
                            Instance.Check()
                            'Check whether the error reason has changed
                            Dim CustomInfo As String = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "CustomInfo")
                            If Instance.State = McInstance.McInstanceState.Original OrElse (CustomInfo = "" AndAlso Not OldDesc = Instance.Info) Then
                                Logger.Info($"版本 {Instance.Name} 的错误状态已变更，新的状态为：{Instance.Info}")
                                Return Nothing
                            End If
                        End If

                        'Check instances that haven't been loaded
                        If Instance.Logo = "" Then
                            Logger.Info($"版本 {Instance.Name} 未被加载")
                            Return Nothing
                        End If

                    Catch ex As Exception
                        Logger.Warn(ex, $"读取版本加载缓存失败（{VersionName}）")
                        Return Nothing
                    End Try
                Next

                If InstanceList.Any Then Results.Add(CardType, InstanceList)
            Next
            Return Results
        Catch ex As Exception
            Logger.Warn(ex, "读取版本缓存失败")
            Return Nothing
        End Try
    End Function
    Private Function InitMcInstanceListWithoutCache(Folder As String) As Dictionary(Of McInstanceCardType, List(Of McInstance))
        Dim InstanceList As New List(Of McInstance)

#Region "Load info for each instance in a loop"
        For Each VersionFolderInfo As DirectoryInfo In DirectoryUtils.GetInfo(Folder & "versions").GetDirectories
            If Not VersionFolderInfo.Exists OrElse Not VersionFolderInfo.EnumerateFiles.Any Then
                Logger.Info($"跳过空文件夹：{VersionFolderInfo.FullName}")
                Continue For
            End If
            If (VersionFolderInfo.Name = "cache" OrElse VersionFolderInfo.Name = "BLClient" OrElse VersionFolderInfo.Name = "PCL") AndAlso
                Not FileUtils.Exists(VersionFolderInfo.FullName & "\" & VersionFolderInfo.Name & ".json") Then
                Logger.Info($"跳过可能不是版本文件夹的项目：{VersionFolderInfo.FullName}")
                Continue For
            End If
            Dim VersionFolder As String = VersionFolderInfo.FullName & "\"
            If FileUtils.Exists(VersionFolder & ".pclignore") Then
                If IsFirstMcInstanceListLoad Then
                    Logger.Info($"清理残留的忽略项目：{VersionFolder}") '#2781
                    FileUtils.Delete(VersionFolder & ".pclignore")
                Else
                    Logger.Info($"跳过要求忽略的项目：{VersionFolder}")
                    Continue For
                End If
            End If
            Dim Instance As New McInstance(VersionFolder)
            InstanceList.Add(Instance)
            Instance.Load()
        Next
#End Region

        Dim Results As New Dictionary(Of McInstanceCardType, List(Of McInstance))

#Region "Sort instances into cards"
        Try

            'Instance list without customization
            Dim InstanceListOriginal As New Dictionary(Of McInstanceCardType, List(Of McInstance))

            'List favorite instances separately
            Dim InstanceStar As New List(Of McInstance)
            For Each Instance As McInstance In InstanceList.ToList
                If Not Instance.IsStar Then Continue For
                If Instance.DisplayType = McInstanceCardType.Hidden Then Continue For
                InstanceStar.Add(Instance)
                InstanceList.Remove(Instance)
            Next
            If InstanceStar.Any Then InstanceListOriginal.Add(McInstanceCardType.Star, InstanceStar)

            'Pre-filter April Fools and errored instances
            McInstanceFilter(InstanceList, InstanceListOriginal, {McInstance.McInstanceState.Error}, McInstanceCardType.Error)
            McInstanceFilter(InstanceList, InstanceListOriginal, {McInstance.McInstanceState.Fool}, McInstanceCardType.Fool)

            'Filter API instances
            McInstanceFilter(InstanceList, InstanceListOriginal, {McInstance.McInstanceState.Forge, McInstance.McInstanceState.NeoForge, McInstance.McInstanceState.LiteLoader, McInstance.McInstanceState.Fabric}, McInstanceCardType.API)

            'Pre-sort old versions into "rarely used", leaving only vanilla, snapshots and OptiFine
            Dim InstanceUseful As New List(Of McInstance)
            Dim InstanceRubbish As New List(Of McInstance)
            McInstanceFilter(InstanceList, {McInstance.McInstanceState.Old}, InstanceRubbish)

            'Find the latest version; if it's a snapshot, add it to the common list
            Dim LargestInstance As McInstance = InstanceList.
                Where(Function(v) v.State = McInstance.McInstanceState.Original OrElse v.State = McInstance.McInstanceState.Snapshot).
                MaxBy(Function(v) v.ReleaseTime)
            If LargestInstance IsNot Nothing AndAlso LargestInstance.State = McInstance.McInstanceState.Snapshot Then
                InstanceUseful.Add(LargestInstance)
                InstanceList.Remove(LargestInstance)
            End If

            'Move all remaining snapshots into the rarely used list
            McInstanceFilter(InstanceList, {McInstance.McInstanceState.Snapshot}, InstanceRubbish)

            'Get the latest vanilla and OptiFine instance for each Drop
            Dim NewerInstance As New Dictionary(Of String, McInstance)
            Dim ExistDrops As New List(Of Integer)
            For Each Instance As McInstance In InstanceList
                If Not Instance.Version.Valid Then Continue For
                If Not ExistDrops.Contains(Instance.Version.Drop) Then ExistDrops.Add(Instance.Version.Drop)
                Dim Key As String = Instance.Version.Drop & "-" & Instance.State
                If Not NewerInstance.ContainsKey(Key) Then
                    NewerInstance.Add(Key, Instance)
                    Continue For
                End If
                If Instance.State = McInstance.McInstanceState.OptiFine Then
                    If Instance.Version.OptiFineCode > NewerInstance(Key).Version.OptiFineCode Then NewerInstance(Key) = Instance 'OptiFine is compared by version number
                Else
                    If Instance.ReleaseTime > NewerInstance(Key).ReleaseTime Then NewerInstance(Key) = Instance 'Vanilla is compared by release time
                End If
            Next

            'Add the most regular instance of each Drop
            For Each Drop As Integer In ExistDrops
                If NewerInstance.ContainsKey(Drop & "-" & McInstance.McInstanceState.OptiFine) AndAlso NewerInstance.ContainsKey(Drop & "-" & McInstance.McInstanceState.Original) Then
                    'Both OptiFine and vanilla exist
                    Dim VanillaInstance As McInstance = NewerInstance(Drop & "-" & McInstance.McInstanceState.Original)
                    Dim OptiFineInstance As McInstance = NewerInstance(Drop & "-" & McInstance.McInstanceState.OptiFine)
                    If VanillaInstance.Version.Drop > OptiFineInstance.Version.Drop Then
                        'Only add vanilla if it's newer than OptiFine
                        InstanceUseful.Add(VanillaInstance)
                        InstanceList.Remove(VanillaInstance)
                    End If
                    InstanceUseful.Add(OptiFineInstance)
                    InstanceList.Remove(OptiFineInstance)
                ElseIf NewerInstance.ContainsKey(Drop & "-" & McInstance.McInstanceState.OptiFine) Then
                    'No vanilla, add OptiFine directly
                    InstanceUseful.Add(NewerInstance(Drop & "-" & McInstance.McInstanceState.OptiFine))
                    InstanceList.Remove(NewerInstance(Drop & "-" & McInstance.McInstanceState.OptiFine))
                ElseIf NewerInstance.ContainsKey(Drop & "-" & McInstance.McInstanceState.Original) Then
                    'No OptiFine, add vanilla directly
                    InstanceUseful.Add(NewerInstance(Drop & "-" & McInstance.McInstanceState.Original))
                    InstanceList.Remove(NewerInstance(Drop & "-" & McInstance.McInstanceState.Original))
                End If
            Next

            'Add whatever remains
            InstanceRubbish.AddRange(InstanceList)
            If InstanceUseful.Any Then InstanceListOriginal.Add(McInstanceCardType.OriginalLike, InstanceUseful)
            If InstanceRubbish.Any Then InstanceListOriginal.Add(McInstanceCardType.Rubbish, InstanceRubbish)

            'Re-add according to custom instance categories
            For Each InstancePair In InstanceListOriginal
                For Each Instance As McInstance In InstancePair.Value
                    Dim RealType = If(Instance.DisplayType = 0 OrElse InstancePair.Key = McInstanceCardType.Star, InstancePair.Key, Instance.DisplayType)
                    If Not Results.ContainsKey(RealType) Then Results.Add(RealType, New List(Of McInstance))
                    Results(RealType).Add(Instance)
                Next
            Next

        Catch ex As Exception
            Results.Clear()
            Logger.Error(ex, "分类版本列表失败")
        End Try
#End Region
#Region "Sort cards and instances"

        'Sort cards
        Dim SortedInstanceList As New Dictionary(Of McInstanceCardType, List(Of McInstance))
        For Each SortRule As String In {McInstanceCardType.Star, McInstanceCardType.API, McInstanceCardType.OriginalLike, McInstanceCardType.Rubbish, McInstanceCardType.Fool, McInstanceCardType.Error, McInstanceCardType.Hidden}
            If Results.ContainsKey(SortRule) Then SortedInstanceList.Add(SortRule, Results(SortRule))
        Next
        Results = SortedInstanceList

        'Sort instances
        For Each CardType In {McInstanceCardType.Star, McInstanceCardType.API, McInstanceCardType.OriginalLike, McInstanceCardType.Rubbish, McInstanceCardType.Fool}
            If Not Results.ContainsKey(CardType) Then Continue For
            Dim GetComponentCode =
            Function(Instance As McInstance) As Integer
                If Instance.Version.ForgelikeCode > 0 Then Return Instance.Version.ForgelikeCode
                If Instance.Version.HasOptiFine Then Return Instance.Version.OptiFineCode
                Return 0
            End Function
            Results(CardType) = Results(CardType).SortByComparison(
            Function(Left As McInstance, Right As McInstance)
                'Release time
                If (Left.ReleaseTime.Year >= 2000 OrElse Right.ReleaseTime.Year >= 2000) AndAlso
                    Left.ReleaseTime <> Right.ReleaseTime Then Return Left.ReleaseTime > Right.ReleaseTime
                'Add-on component type
                If Left.Version.ModLoaders.HasFlagF(ModLoaders.Fabric) <> Right.Version.ModLoaders.HasFlagF(ModLoaders.Fabric) Then Return Left.Version.ModLoaders.HasFlagF(ModLoaders.Fabric)
                If Left.Version.ModLoaders.HasFlagF(ModLoaders.NeoForge) <> Right.Version.ModLoaders.HasFlagF(ModLoaders.NeoForge) Then Return Left.Version.ModLoaders.HasFlagF(ModLoaders.NeoForge)
                If Left.Version.ModLoaders.HasFlagF(ModLoaders.Forge) <> Right.Version.ModLoaders.HasFlagF(ModLoaders.Forge) Then Return Left.Version.ModLoaders.HasFlagF(ModLoaders.Forge)
                If Left.Version.HasOptiFine <> Right.Version.HasOptiFine Then Return Left.Version.HasOptiFine
                If Left.Version.ModLoaders.HasFlagF(ModLoaders.LiteLoader) <> Right.Version.ModLoaders.HasFlagF(ModLoaders.LiteLoader) Then Return Left.Version.ModLoaders.HasFlagF(ModLoaders.LiteLoader)
                'Add-on component version
                If GetComponentCode(Left) <> GetComponentCode(Right) Then Return GetComponentCode(Left) > GetComponentCode(Right)
                'Name
                Return Left.Name > Right.Name
            End Function)
        Next

#End Region
#Region "Save card cache"
        WriteIni(Folder & "PCL.ini", "CardCount", Results.Count)
        For i = 0 To Results.Count - 1
            WriteIni(Folder & "PCL.ini", "CardKey" & (i + 1), Results.Keys(i))
            Dim Value As String = ""
            For Each Instance As McInstance In Results.Values(i)
                Value += Instance.Name & ":"
            Next
            WriteIni(Folder & "PCL.ini", "CardValue" & (i + 1), Value)
        Next
#End Region

        Return Results
    End Function
    ''' <summary>
    ''' Filters instances of a given type and adds them directly as a card.
    ''' </summary>
    ''' <param name="InstanceList">The list to filter.</param>
    ''' <param name="Formula">The instance type to filter. -2 means hidden instances.</param>
    ''' <param name="CardType">Name of the card.</param>
    Private Sub McInstanceFilter(ByRef InstanceList As List(Of McInstance), ByRef Target As Dictionary(Of McInstanceCardType, List(Of McInstance)), Formula As McInstance.McInstanceState(), CardType As McInstanceCardType)
        Dim KeepList = InstanceList.Where(Function(v) Formula.Contains(v.State)).ToList
        'Add to the instance list and remove from the remainder
        If KeepList.Any Then
            Target.Add(CardType, KeepList)
            InstanceList = InstanceList.Except(KeepList).ToList()
        End If
    End Sub
    ''' <summary>
    ''' Filters instances of a given type and adds them to an existing list.
    ''' </summary>
    ''' <param name="InstanceList">The list to filter.</param>
    ''' <param name="Formula">The instance type to filter. -2 means hidden instances.</param>
    ''' <param name="KeepList">The list to add to.</param>
    Private Sub McInstanceFilter(ByRef InstanceList As List(Of McInstance), Formula As McInstance.McInstanceState(), ByRef KeepList As List(Of McInstance))
        KeepList.AddRange(InstanceList.Where(Function(v) Formula.Contains(v.State)))
        'Add to the instance list and remove from the remainder
        If KeepList.Any Then
            InstanceList = InstanceList.Except(KeepList).ToList()
        End If
    End Sub
    Public Enum McInstanceCardType
        Star = -1
        Auto = 0 'Only used as "auto" for forced instance categories
        Hidden = 1
        API = 2
        OriginalLike = 3
        Rubbish = 4
        Fool = 5
        [Error] = 6
    End Enum

#End Region

#Region "Skins"

    Public Structure McSkinInfo
        Public IsSlim As Boolean
        Public LocalFile As String
        Public IsVaild As Boolean
    End Structure
    ''' <summary>
    ''' Asks the player to choose a skin file and validates it.
    ''' </summary>
    Public Function McSkinSelect() As McSkinInfo
        Dim FileName As String = Dialogs.SelectFile("选择皮肤文件", False, filter:={({"png", "jpg", "jpeg", "webp"}, "皮肤文件")}).FirstOrDefault()

        'Validate
        If String.IsNullOrEmpty(FileName) Then Return New McSkinInfo With {.IsVaild = False}
        Try
            Dim Image As New MyBitmap(FileName)
            If Image.Pic.Width <> 64 OrElse Not (Image.Pic.Height = 32 OrElse Image.Pic.Height = 64) Then
                Hint("皮肤图片大小应为 64x32 像素或 64x64 像素！", HintType.Red)
                Return New McSkinInfo With {.IsVaild = False}
            End If
            Dim FileInfo = FileUtils.GetInfo(FileName)
            If FileInfo.Length > 24 * 1024 Then
                Hint("皮肤文件大小需小于 24 KB，而所选文件大小为 " & Math.Round(FileInfo.Length / 1024, 2) & " KB", HintType.Red)
                Return New McSkinInfo With {.IsVaild = False}
            End If
        Catch ex As Exception
            Logger.Error(ex, "皮肤文件存在错误", LogBehavior.Toast)
            Return New McSkinInfo With {.IsVaild = False}
        End Try

        'Get the skin type
        Dim IsSlim As Integer = MyMsgBox("此皮肤为 Steve 模型（粗手臂）还是 Alex 模型（细手臂）？", "选择皮肤种类", "Steve 模型", "Alex 模型", "我不知道", HighLight:=False)
        If IsSlim = 3 Then
            Hint("请在皮肤下载页面确认皮肤种类后再使用此皮肤！")
            Return New McSkinInfo With {.IsVaild = False}
        End If

        Return New McSkinInfo With {.IsVaild = True, .IsSlim = IsSlim = 2, .LocalFile = FileName}
    End Function

    ''' <summary>
    ''' Gets the skin file URL for a UUID. Throws on failure.
    ''' </summary>
    Public Function McSkinGetAddress(UUID As String, Type As String) As String
        If UUID = "" Then Throw New Exception("UUID 为空。")
        If UUID.StartsWithF("00000") AndAlso Type <> "Auth" Then Throw New Exception("离线 UUID 无正版皮肤文件：" & UUID)
        'Try to read the cache
        Dim CacheSkinAddress As String = ReadIni(PathTemp & "Cache\Skin\Index" & Type & ".ini", UUID)
        If Not CacheSkinAddress = "" Then Return CacheSkinAddress
        'Get the skin URL
        Dim Url As String
        Select Case Type
            Case "Mojang", "Ms"
                Url = "https://sessionserver.mojang.com/session/minecraft/profile/"
            Case "Nide"
                Url = "https://auth.mc-user.com:233/" & If(McInstanceSelected Is Nothing, Settings.Get(Of String)("CacheNideServer"), Settings.Get(Of String)("VersionServerNide", Instance:=McInstanceSelected)) & "/sessionserver/session/minecraft/profile/"
            Case "Auth"
                Url = If(McInstanceSelected Is Nothing, Settings.Get(Of String)("CacheAuthServerServer"), Settings.Get(Of String)("VersionServerAuthServer", Instance:=McInstanceSelected)) & "/sessionserver/session/minecraft/profile/"
            Case Else
                Throw New ArgumentException("皮肤地址种类无效：" & If(Type, "null"))
        End Select
        Dim SkinString = NetRequestByClientRetry(Url & UUID, RequireJson:=True)
        If SkinString = "" Then Throw New Exception("皮肤返回值为空，可能是未设置自定义皮肤的用户")
        'Process the skin URL
        Dim SkinValue As String
        Try
            For Each SkinProperty In SkinString.DeserializeJson()("properties")
                If SkinProperty("name") = "textures" Then
                    SkinValue = SkinProperty("value")
                    Exit Try
                End If
            Next
            Throw New Exception("未从皮肤返回值中找到符合条件的 Property")
        Catch ex As Exception
            Logger.Warn(ex, $"无法完成解析的皮肤返回值，可能是未设置自定义皮肤的用户：{SkinString}")
            Throw New Exception("皮肤返回值中不包含皮肤数据项，可能是未设置自定义皮肤的用户", ex)
        End Try
        SkinString = Encoding.UTF8.GetString(Convert.FromBase64String(SkinValue))
        Dim SkinJson As JObject = SkinString.Lower.DeserializeJson()
        If SkinJson("textures") Is Nothing OrElse SkinJson("textures")("skin") Is Nothing OrElse SkinJson("textures")("skin")("url") Is Nothing Then
            Throw New Exception("用户未设置自定义皮肤")
        Else
            Dim SkinUrl As String = SkinJson("textures")("skin")("url").ToString
            SkinValue = If(SkinUrl.Contains("minecraft.net/"), SkinUrl.Replace("http://", "https://"), SkinUrl)
        End If
        'Save the cache
        WriteIni(PathTemp & "Cache\Skin\Index" & Type & ".ini", UUID, SkinValue)
        Logger.Info($"UUID {UUID} 对应的皮肤文件为 {SkinValue}")
        Return SkinValue
    End Function

    Private ReadOnly McSkinDownloadLock As New Object
    ''' <summary>
    ''' Downloads a skin from a URL, possibly using the cache. Returns the local file path. Throws on failure.
    ''' </summary>
    Public Function McSkinDownload(Address As String) As String
        Dim SkinName As String = PathUtils.GetLastPart(Address)
        Dim FileAddress As String = PathTemp & "Cache\Skin\" & Address.GetStableHashCode() & ".png"
        SyncLock McSkinDownloadLock
            If Not FileUtils.Exists(FileAddress) Then
                NetDownloadByClient(Address, FileAddress & ".PCLDownloading")
                FileUtils.Move(FileAddress & ".PCLDownloading", FileAddress)
                Logger.Info($"皮肤下载成功：{FileAddress}")
            End If
            Return FileAddress
        End SyncLock
    End Function

    ''' <summary>
    ''' Gets the default skin for a UUID; returns "Steve" or "Alex".
    ''' </summary>
    Public Function McSkinSex(Uuid As String) As String
        If Not Uuid.Length = 32 Then Return "Steve"
        Dim a = Integer.Parse(Uuid(7), Globalization.NumberStyles.AllowHexSpecifier)
        Dim b = Integer.Parse(Uuid(15), Globalization.NumberStyles.AllowHexSpecifier)
        Dim c = Integer.Parse(Uuid(23), Globalization.NumberStyles.AllowHexSpecifier)
        Dim d = Integer.Parse(Uuid(31), Globalization.NumberStyles.AllowHexSpecifier)
        Return If((a Xor b Xor c Xor d) Mod 2, "Alex", "Steve")
    End Function

#End Region

#Region "Library files"

    Public Class McLibEntry
        ''' <summary>
        ''' Full local path of the file.
        ''' </summary>
        Public LocalPath As String
        ''' <summary>
        ''' File size. 0 if no valid data.
        ''' </summary>
        Public Size As Long = 0
        ''' <summary>
        ''' Whether this is a natives file.
        ''' </summary>
        Public IsNatives As Boolean = False
        ''' <summary>
        ''' SHA1 of the file.
        ''' </summary>
        Public SHA1 As String = Nothing
        ''' <summary>
        ''' URL provided by the JSON, or Nothing if absent.
        ''' </summary>
        Public Property Url As String
            Get
                Return _Url
            End Get
            Set(value As String)
                'Forge authors like to write an empty string for libraries without a URL
                _Url = If(String.IsNullOrWhiteSpace(value), Nothing, value)
            End Set
        End Property
        Private _Url As String
        ''' <summary>
        ''' The part of the original JSON Name before the version number. May be Nothing.
        ''' </summary>
        Public ReadOnly Property Name As String
            Get
                If OriginalName Is Nothing Then Return Nothing
                Dim Splited As New List(Of String)(OriginalName.Split(":"))
                Splited.RemoveAt(2) 'In this Java format the version is always the third segment; the fourth may contain architecture, classifier, etc.
                Return Splited.Join(":"c)
            End Get
        End Property
        ''' <summary>
        ''' The Name entry in the original JSON.
        ''' </summary>
        Public OriginalName As String

        Public Overrides Function ToString() As String
            Return If(IsNatives, "[Native] ", "") & StringUtils.FormatByteSize(Size) & " | " & LocalPath
        End Function
    End Class

    ''' <summary>
    ''' Checks whether the Rules in the JSON are satisfied.
    ''' </summary>
    ''' <param name="RuleToken">The "rules" entry in the JSON.</param>
    Public Function McJsonRuleCheck(RuleToken As JToken) As Boolean
        If RuleToken Is Nothing Then Return True

        'Initialize
        Dim Required As Boolean = False
        For Each Rule As JToken In RuleToken

            'Evaluate a single rule
            Dim IsRightRule As Boolean = True 'Whether this is a valid rule
            If Rule("os") IsNot Nothing Then 'Operating system
                If Rule("os")("name") IsNot Nothing Then 'OS name
                    Dim OsName As String = Rule("os")("name").ToString
                    If OsName = "unknown" Then
                    ElseIf OsName = "windows" Then
                        If Rule("os")("version") IsNot Nothing Then 'OS version
                            IsRightRule = IsRightRule AndAlso OSVersion.RegexCheck(Rule("os")("version").ToString)
                        End If
                    Else
                        IsRightRule = False
                    End If
                End If
                If Rule("os")("arch") IsNot Nothing Then 'OS architecture
                    IsRightRule = IsRightRule AndAlso Rule("os")("arch").ToString <> "x86" AndAlso Not Rule("os")("arch").Contains("arm")
                End If
            End If
            If Not IsNothing(Rule("features")) Then 'Features
                IsRightRule = IsRightRule AndAlso IsNothing(Rule("features")("is_demo_user")) 'Inverted check for demo user
                If CType(Rule("features"), JObject).Children.Any(Function(j As JProperty) j.Name.Contains("quick_play")) Then
                    IsRightRule = False 'Don't enable Quick Play; let players add it themselves
                End If
            End If

            'Apply inversion
            If Rule("action").ToString = "allow" Then
                If IsRightRule Then Required = True 'allow
            Else
                If IsRightRule Then Required = False 'disallow
            End If

        Next
        Return Required
    End Function
    Private OSVersion As String = My.Computer.Info.OSVersion

    ''' <summary>
    ''' Recursively gets the full library list of a Minecraft instance.
    ''' </summary>
    Public Function McLibListGet(Instance As McInstance, IncludeMainJar As Boolean) As List(Of McLibEntry)

        'Get the current library list
        Logger.Info($"获取支持库列表：{Instance.Name}")
        Dim Result = McLibListGetWithJson(Instance.JsonObject)

        'The vanilla jar needs to be added
        If IncludeMainJar Then
            Dim RealInstance As McInstance
            Dim RequiredJar As String = Instance.JsonObject("jar")?.ToString
            If Instance.IsHmclFormatJson OrElse RequiredJar Is Nothing Then
                'HMCL entries use their own jar directly
                'Find the deepest version via Inherit
                Dim OriginalInstance As McInstance = Instance
                'Forge 1.17+ doesn't look up Inherit
                If Not ((Instance.Version.ModLoaders.HasFlagF(ModLoaders.Forge) OrElse Instance.Version.ModLoaders.HasFlagF(ModLoaders.NeoForge)) AndAlso Instance.Version.Vanilla.Major >= 17) Then
                    Do Until OriginalInstance.InheritName = ""
                        If OriginalInstance.InheritName = OriginalInstance.Name Then Exit Do
                        OriginalInstance = New McInstance(McFolderSelected & "versions\" & OriginalInstance.InheritName & "\")
                    Loop
                End If
                'Must create a new object, otherwise the later Check would revert McInstanceCurrent's State to Original
                'Repro: launch a snapshot instance
                RealInstance = New McInstance(OriginalInstance.PathVersion)
            Else
                'The JSON already provides a Jar field; use its info
                RealInstance = New McInstance(RequiredJar)
            End If
            Dim ClientUrl As String, ClientSHA1 As String
            'Check whether the required version exists
            'Can't call RealVersion.Check(); it may inexplicably trigger CheckPermission's "file in use by another process", wrongly concluding the prerequisite is missing
            If Not FileUtils.Exists(RealInstance.PathVersion & RealInstance.Name & ".json") Then
                RealInstance = Instance
                Logger.Warn($"可能缺少前置版本 {RealInstance.Name}，找不到对应的 json 文件")
            End If
            'Get detailed download info
            If RealInstance.JsonObject("downloads") IsNot Nothing AndAlso RealInstance.JsonObject("downloads")("client") IsNot Nothing Then
                ClientUrl = RealInstance.JsonObject("downloads")("client")("url")
                ClientSHA1 = RealInstance.JsonObject("downloads")("client")("sha1")
            Else
                ClientUrl = Nothing
                ClientSHA1 = Nothing
            End If
            'Add the required vanilla jar
            Result.Add(New McLibEntry With {.LocalPath = RealInstance.PathVersion & RealInstance.Name & ".jar", .Size = 0, .IsNatives = False, .Url = ClientUrl, .SHA1 = ClientSHA1})
        End If

        Return Result
    End Function
    ''' <summary>
    ''' Gets the library list of a Minecraft instance ignoring inheritance, i.e. without inherited entries.
    ''' </summary>
    Public Function McLibListGetWithJson(JsonObject As JObject, Optional KeepSameNameDifferentVersionResult As Boolean = False, Optional CustomMcFolder As String = Nothing) As List(Of McLibEntry)
        CustomMcFolder = If(CustomMcFolder, McFolderSelected)

        'Convert to LibToken
        Dim BasicArray As New List(Of McLibEntry)
        For Each Library As JObject In CType(JsonObject("libraries"), JArray).Children

            'Clean up null entries (BakaXL serializes missing entries as null, which Newtonsoft turns into a JValue so Is Nothing = false; this caused #409)
            For i = Library.Properties.Count - 1 To 0 Step -1
                If Library.Properties(i).Value.Type = JTokenType.Null Then Library.Remove(Library.Properties(i).Name)
            Next

            'Check whether it's needed (Rules)
            If Not McJsonRuleCheck(Library("rules")) Then Continue For

            'Get the url under the root node
            Dim RootUrl As String = Library("url")
            If RootUrl IsNot Nothing Then
                RootUrl += McLibGet(Library("name"), False, True, CustomMcFolder).Replace("\", "/")
            End If

            'Handle localization (natives)
            If Library("natives") Is Nothing Then 'No natives
                Dim LocalPath As String
                LocalPath = McLibGet(Library("name"), CustomMcFolder:=CustomMcFolder)
                Try
                    If Library("downloads") IsNot Nothing AndAlso Library("downloads")("artifact") IsNot Nothing Then
                        BasicArray.Add(New McLibEntry With {
                            .OriginalName = Library("name"),
                            .Url = If(Library("downloads")("artifact")("url"), RootUrl),
                            .LocalPath = If(Library("downloads")("artifact")("path") Is Nothing, McLibGet(Library("name"),
                                CustomMcFolder:=CustomMcFolder), CustomMcFolder & "libraries\" & Library("downloads")("artifact")("path").ToString.Replace("/", "\")),
                            .Size = Val(Library("downloads")("artifact")("size").ToString),
                            .IsNatives = False,
                            .SHA1 = Library("downloads")("artifact")("sha1")?.ToString})
                    Else
                        BasicArray.Add(New McLibEntry With {.OriginalName = Library("name"), .Url = RootUrl, .LocalPath = LocalPath, .Size = 0, .IsNatives = False, .SHA1 = Nothing})
                    End If
                Catch ex As Exception
                    Logger.Warn(ex, $"处理实际支持库列表失败（无 Natives，{If(Library("name"), "Nothing")}）")
                    BasicArray.Add(New McLibEntry With {.OriginalName = Library("name"), .Url = RootUrl, .LocalPath = LocalPath, .Size = 0, .IsNatives = False, .SHA1 = Nothing})
                End Try
            ElseIf Library("natives")("windows") IsNot Nothing Then 'Has Windows natives
                Dim NativeName As String = Library("natives")("windows")
                Try
                    If Library("downloads") IsNot Nothing AndAlso Library("downloads")("classifiers") IsNot Nothing AndAlso Library("downloads")("classifiers")(NativeName) IsNot Nothing Then
                        Dim NativeData = Library("downloads")("classifiers")(NativeName)
                        BasicArray.Add(New McLibEntry With {
                             .OriginalName = Library("name"),
                             .Url = If(NativeData("url"), RootUrl),
                             .LocalPath = If(NativeData("path") Is Nothing,
                                 McLibGet(Library("name"), CustomMcFolder:=CustomMcFolder).
                                    Replace(".jar", "-" & NativeName & ".jar").
                                    Replace("${arch}", If(Environment.Is64BitOperatingSystem, "64", "32")),
                                    $"{CustomMcFolder}libraries\{NativeData("path").ToString.Replace("/", "\")}"),
                             .Size = Val(NativeData("size").ToString),
                             .IsNatives = True,
                             .SHA1 = NativeData("sha1").ToString})
                    Else
                        BasicArray.Add(
                        New McLibEntry With {.OriginalName = Library("name"), .Url = RootUrl, .Size = 0, .IsNatives = True, .SHA1 = Nothing,
                            .LocalPath = McLibGet(Library("name"), CustomMcFolder:=CustomMcFolder).
                                 Replace(".jar", $"-{NativeName}.jar").
                                 Replace("${arch}", If(Environment.Is64BitOperatingSystem, "64", "32"))
                        })
                    End If
                Catch ex As Exception
                    Logger.Warn(ex, $"处理实际支持库列表失败（有 Natives，{If(Library("name"), "Nothing")}）")
                    BasicArray.Add(
                        New McLibEntry With {.OriginalName = Library("name"), .Url = RootUrl, .Size = 0, .IsNatives = True, .SHA1 = Nothing,
                             .LocalPath = McLibGet(Library("name"), CustomMcFolder:=CustomMcFolder).
                                 Replace(".jar", $"-{NativeName}.jar").
                                 Replace("${arch}", If(Environment.Is64BitOperatingSystem, "64", "32"))
                        })
                End Try
            End If

        Next

        'Deduplicate
        Dim ResultArray As New Dictionary(Of String, McLibEntry)
        Dim GetVersion =
        Function(Token As McLibEntry) As String
            'Test cases:
            'D:\Minecraft\test\libraries\net\neoforged\mergetool\2.0.0\mergetool-2.0.0-api.jar
            'D:\Minecraft\test\libraries\org\apache\commons\commons-collections4\4.2\commons-collections4-4.2.jar
            'D:\Minecraft\test\libraries\com\google\guava\guava\31.1-jre\guava-31.1-jre.jar
            Return PathUtils.GetLastPart(PathUtils.RemoveLastPart(Token.LocalPath))
        End Function
        For i = 0 To BasicArray.Count - 1
            Dim Key As String = BasicArray(i).Name & BasicArray(i).IsNatives.ToString
            If ResultArray.ContainsKey(Key) Then
                Dim BasicArrayVersion As String = GetVersion(BasicArray(i))
                Dim ResultArrayVersion As String = GetVersion(ResultArray(Key))
                If BasicArrayVersion <> ResultArrayVersion AndAlso KeepSameNameDifferentVersionResult Then
                    Logger.Info($"发现疑似重复的支持库：{BasicArray(i)} ({BasicArrayVersion}) 与 {ResultArray(Key)} ({ResultArrayVersion})")
                    ResultArray.Add(Key & GetUuid(), BasicArray(i))
                Else
                    Logger.Info($"发现重复的支持库：{BasicArray(i)} ({BasicArrayVersion}) 与 {ResultArray(Key)} ({ResultArrayVersion})，已忽略其中之一")
                    If McUtils.CompareVersionGE(BasicArrayVersion, ResultArrayVersion) Then
                        ResultArray(Key) = BasicArray(i)
                    End If
                End If
            Else
                ResultArray.Add(Key, BasicArray(i))
            End If
        Next
        Return ResultArray.Values.ToList
    End Function

    ''' <summary>
    ''' Gets the NetFiles for the library files required by the instance.
    ''' </summary>
    Public Function McLibNetFilesFromInstance(Instance As McInstance) As List(Of NetFile)
        If Not Instance.IsLoaded Then Instance.Load()
        Dim Result As New List(Of NetFile)

        'When updating this method, also update the automatic install method for new Forge versions!

        'Main jar file
        Try
            Dim MainJar As NetFile = DlClientJarGet(Instance, True)
            If MainJar IsNot Nothing Then Result.Add(MainJar)
        Catch ex As Exception
            Logger.Warn(ex, "版本缺失主 jar 文件所必须的信息")
        End Try

        'Library files
        Result.AddRange(McLibNetFilesFromTokens(McLibListGet(Instance, False)))

        'Nide8 auth files
        If Settings.Get(Of Integer)("VersionServerLogin", Instance:=Instance) = 3 Then
            Dim TargetFile As String = PathPure.Value & "nide8auth.jar"
            Try
                'Test URL: https://auth.mc-user.com:233/00000000000000000000000000000000/
                Logger.Info("开始获取统一通行证下载信息")
                Dim DownloadInfo As JObject = NetRequestByClientRetry("https://auth.mc-user.com:233/" & Settings.Get(Of String)("VersionServerNide", Instance:=Instance), RequireJson:=True).DeserializeJson()
                Result.Add(New NetFile(
                    Urls:={"https://login.mc-user.com:233/index/jar"},
                    LocalPath:=TargetFile,
                    Checker:=New FileChecker With {.Hash = DownloadInfo("jarHash").ToString}))
            Catch ex As Exception
                If FileUtils.Exists(TargetFile) Then
                    Logger.Warn(ex, "获取统一通行证下载信息失败")
                Else
                    Throw New Exception("获取统一通行证下载信息失败", ex)
                End If
            End Try
        End If

        'Authlib-Injector files
        If Settings.Get(Of Integer)("VersionServerLogin", Instance:=Instance) = 4 Then
            Dim TargetFile = PathPure.Value & "authlib-injector.jar"
            Try
                Logger.Info("开始获取 Authlib-Injector 下载信息")
                Dim DownloadInfo As JObject = NetRequestByClientRetry(
                        "https://authlib-injector.yushi.moe/artifact/latest.json",
                        BackupUrl:="https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/latest.json", RequireJson:=True).DeserializeJson()
                Dim DownloadAddress As String = DownloadInfo("download_url").ToString.
                        Replace("bmclapi2.bangbang93.com/mirrors/authlib-injector", "authlib-injector.yushi.moe")
                Result.Add(New NetFile({
                    DownloadAddress,
                    DownloadAddress.Replace("authlib-injector.yushi.moe", "bmclapi2.bangbang93.com/mirrors/authlib-injector")
                }, TargetFile, New FileChecker With {.Hash = DownloadInfo("checksums")("sha256").ToString}))
            Catch ex As Exception
                If FileUtils.Exists(TargetFile) Then
                    Logger.Warn(ex, "获取 Authlib-Injector 下载信息失败")
                Else
                    Throw New Exception("获取 Authlib-Injector 下载信息失败", ex)
                End If
            End Try
        End If

        'Skip verification
        If ShouldIgnoreFileCheck(Instance) Then
            Logger.Warn("设置要求尽量忽略文件检查，这可能会保留有误的文件")
            Result = Result.Where(
            Function(f)
                If FileUtils.Exists(f.LocalPath) Then
                    Logger.Warn($"跳过下载的支持库文件：{f.LocalPath}")
                    Return False
                Else
                    Return True
                End If
            End Function).ToList
        End If

        Return Result
    End Function
    ''' <summary>
    ''' Converts a list of McLibToken into NetFiles.
    ''' </summary>
    Public Function McLibNetFilesFromTokens(Libs As List(Of McLibEntry), Optional CustomMcFolder As String = Nothing) As List(Of NetFile)
        CustomMcFolder = If(CustomMcFolder, McFolderSelected)
        Dim Result As New List(Of NetFile)
        For Each Token As McLibEntry In Libs
            'Checker
            Dim Checker As FileChecker
            If Token.Name.ContainsIgnoreCase("labymod") Then
                Checker = New FileChecker 'Don't check LabyMod files; their verification info is wrong (#3225)
            Else
                Checker = New FileChecker With {.ActualSize = If(Token.Size = 0, -1, Token.Size), .Hash = Token.SHA1}
            End If
            'URL
            Dim Urls As New List(Of String)
            If Token.Url Is Nothing AndAlso Token.Name = "net.minecraftforge:forge:universal" Then 'Special-case fix for some Forge universal files missing a URL (#5455)
                Token.Url = "https://maven.minecraftforge.net" & Token.LocalPath.Replace(CustomMcFolder & "libraries", "").Replace("\", "/")
            End If
            If Token.Url IsNot Nothing Then
                'Get the real URL
                Urls.Add(Token.Url)
                If Token.Url.Contains("launcher.mojang.com/v1/objects") OrElse Token.Url.Contains("client.txt") OrElse
                   Token.Url.Contains(".tsrg") Then
                    Urls.AddRange(DlSourceLauncherOrMetaGet(Token.Url)) 'Mappings (#4425)
                End If
                If Token.Url.Contains("maven") Then
                    Dim BmclapiUrl As String =
                        Token.Url.Replace(Token.Url.Substring(0, Token.Url.IndexOfF("maven")), "https://bmclapi2.bangbang93.com/").Replace("maven.fabricmc.net", "maven").Replace("maven.minecraftforge.net", "maven").Replace("maven.neoforged.net/releases", "maven")
                    If DlSourcePreferMojang Then
                        Urls.Add(BmclapiUrl) 'Prefer the official source
                    Else
                        Urls.Insert(0, BmclapiUrl) 'Prefer the mirror source
                    End If
                End If
            End If
            If Token.LocalPath.Contains("transformer-discovery-service") Then
                'Extract the Transformer file
                If Not FileUtils.Exists(Token.LocalPath) Then ExtractResources(Token.LocalPath, "Transformer")
                Logger.Warn("已自动释放 Transformer Discovery Service")
                Continue For
            ElseIf Token.LocalPath.Contains("optifine\OptiFine") Then
                'OptiFine main jar
                Dim OptiFineBase As String = Token.LocalPath.Replace(CustomMcFolder & "libraries\optifine\OptiFine\", "").Split("_")(0) & "/" & PathUtils.GetLastPart(Token.LocalPath).Replace("-", "_")
                OptiFineBase = "/maven/com/optifine/" & OptiFineBase
                If OptiFineBase.Contains("_pre") Then OptiFineBase = OptiFineBase.Replace("com/optifine/", "com/optifine/preview_")
                Urls.Add("https://bmclapi2.bangbang93.com" & OptiFineBase)
            ElseIf Urls.Count <= 2 Then
                'Regular file
                Urls.AddRange(DlSourceLibraryGet("https://libraries.minecraft.net" & Token.LocalPath.Replace(CustomMcFolder & "libraries", "").Replace("\", "/")))
            End If
            Result.Add(New NetFile(Urls.Distinct, Token.LocalPath, Checker))
        Next
        'Deduplicate and return
        Return Result.DistinctBy(Function(a) a.LocalPath).ToList
    End Function
    ''' <summary>
    ''' Gets the path of the corresponding library file.
    ''' </summary>
    ''' <param name="Original">The original coordinate, e.g. com.mumfrey:liteloader:1.12.2-SNAPSHOT.</param>
    ''' <param name="WithHead">Whether to include the libraries folder prefix; if not, the path starts like com\xxx\.</param>
    Public Function McLibGet(Original As String, Optional WithHead As Boolean = True, Optional IgnoreLiteLoader As Boolean = False, Optional CustomMcFolder As String = Nothing) As String
        CustomMcFolder = If(CustomMcFolder, McFolderSelected)
        'Sometimes the original has four segments, e.g. net.minecraftforge:forge:1.21.4-54.0.34:client
        'The folder name should ignore the last segment (client), but the file name must keep it
        'See #7306 (#5376 was also caused by mishandling this)
        Dim Splited = Original.Split(":")
        Dim Result = If(WithHead, CustomMcFolder & "libraries\", "") &
            $"{Splited(0).Replace(".", "\")}\{Splited(1)}\{Splited(2)}\{Splited.Skip(1).Join("-")}.jar"
        Return Result
    End Function

    ''' <summary>
    ''' Checks the settings: should file checks be skipped?
    ''' </summary>
    Public Function ShouldIgnoreFileCheck(Instance As McInstance)
        Return Settings.Get(Of Boolean)("VersionAdvanceAssetsV2", Instance:=Instance) OrElse (Settings.Get(Of Integer)("VersionAdvanceAssets", Instance:=Instance) = 2)
    End Function

#End Region

#Region "Asset files"

    'Get the index
    ''' <summary>
    ''' Gets the JSON entry of an instance's asset index; see the assetIndex entry in the version JSON. Throws on failure.
    ''' </summary>
    Public Function McAssetsGetIndex(Instance As McInstance, Optional ReturnLegacyOnError As Boolean = False, Optional CheckURLEmpty As Boolean = False) As JToken
        Dim AssetsName As String
        Try
            Do While True
                Dim Index As JToken = Instance.JsonObject("assetIndex")
                If Index IsNot Nothing AndAlso Index("id") IsNot Nothing Then Return Index
                If Instance.JsonObject("assets") IsNot Nothing Then AssetsName = Instance.JsonObject("assets").ToString
                If CheckURLEmpty AndAlso Index("url") IsNot Nothing Then Return Index
                'Next version
                If Instance.InheritName = "" Then Exit Do
                Instance = New McInstance(McFolderSelected & "versions\" & Instance.InheritName)
            Loop
        Catch
        End Try
        'Couldn't get the download URL
        If ReturnLegacyOnError Then
            'Can't return just the assets file name: missing download URLs would make the whole download fail
            Logger.Info("无法获取资源文件索引下载地址，使用默认的 legacy 下载地址")
            Return "{
                ""id"": ""legacy"",
                ""sha1"": ""c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729"",
                ""size"": 134284,
                ""url"": ""https://launchermeta.mojang.com/mc-staging/assets/legacy/c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729/legacy.json"",
                ""totalSize"": 111220701
            }".DeserializeJson()
        Else
            Throw New Exception("该版本不存在资源文件索引信息")
        End If
    End Function
    ''' <summary>
    ''' Gets the asset index name of an instance, preferring assetIndex, then assets. Returns legacy on failure.
    ''' </summary>
    Public Function McAssetsGetIndexName(Instance As McInstance) As String
        Try
            Do While True
                If Instance.JsonObject("assetIndex") IsNot Nothing AndAlso Instance.JsonObject("assetIndex")("id") IsNot Nothing Then
                    Return Instance.JsonObject("assetIndex")("id").ToString
                End If
                If Instance.JsonObject("assets") IsNot Nothing Then
                    Return Instance.JsonObject("assets").ToString
                End If
                If Instance.InheritName = "" Then Exit Do
                Instance = New McInstance(McFolderSelected & "versions\" & Instance.InheritName)
            Loop
        Catch ex As Exception
            Logger.Warn(ex, "获取资源文件索引名失败")
        End Try
        Return "legacy"
    End Function

    'Get the list
    Private Structure McAssetsToken
        ''' <summary>
        ''' Full local path of the file.
        ''' </summary>
        Public LocalPath As String
        ''' <summary>
        ''' Source path as written in the JSON, e.g. minecraft/sounds/mob/stray/death2.ogg.
        ''' </summary>
        Public SourcePath As String
        ''' <summary>
        ''' File size. 0 if no valid data.
        ''' </summary>
        Public Size As Long
        ''' <summary>
        ''' Hash of the file.
        ''' </summary>
        Public Hash As String

        Public Overrides Function ToString() As String
            Return StringUtils.FormatByteSize(Size) & " | " & LocalPath
        End Function
    End Structure
    ''' <summary>
    ''' Gets the asset file list of Minecraft. Throws on failure.
    ''' </summary>
    Private Function McAssetsListGet(Instance As McInstance) As List(Of McAssetsToken)
        Dim IndexName = McAssetsGetIndexName(Instance)
        Try

            'Initialize
            If Not FileUtils.Exists($"{McFolderSelected}assets\indexes\{IndexName}.json") Then Throw New FileNotFoundException("未找到 Asset Index", McFolderSelected & "assets\indexes\" & IndexName & ".json")
            Dim Result As New List(Of McAssetsToken)
            Dim Json As JObject = FileUtils.ReadAsJson($"{McFolderSelected}assets\indexes\{IndexName}.json")

            'Read the list
            For Each File As JProperty In Json("objects").Children
                Dim LocalPath As String
                If Json("map_to_resources") IsNot Nothing AndAlso Json("map_to_resources").ToObject(Of Boolean) Then
                    'Remap
                    LocalPath = $"{Instance.PathIndie}resources\{File.Name}"
                ElseIf Json("virtual") IsNot Nothing AndAlso Json("virtual").ToObject(Of Boolean) Then
                    'Virtual
                    LocalPath = $"{McFolderSelected}assets\virtual\legacy\{File.Name}"
                Else
                    'Normal
                    LocalPath = $"{McFolderSelected}assets\objects\{Left(File.Value()("hash").ToString, 2)}\{File.Value()("hash")}"
                End If
                Result.Add(New McAssetsToken With {
                    .LocalPath = LocalPath.Replace("/", "\"),
                    .SourcePath = File.Name,
                    .Hash = File.Value()("hash").ToString,
                    .Size = File.Value()("size").ToString
                })
            Next
            Return Result

        Catch ex As Exception
            Logger.Warn(ex, $"获取资源文件列表失败：{IndexName}")
            Throw
        End Try
    End Function

    ''' <summary>
    ''' Gets the NetFiles for the instance's asset files.
    ''' </summary>
    Public Function McAssetsFixList(Instance As McInstance, CheckHash As Boolean, Optional ByRef ProgressFeed As LoaderBase = Nothing) As List(Of NetFile)
        'If the hash needs checking, defer it to download time so multithreading speeds up the check
        If CheckHash Then
            Return McAssetsListGet(Instance).
                Select(Function(Token As McAssetsToken) New NetFile(
                    DlSourceAssetsGet($"https://resources.download.minecraft.net/{Left(Token.Hash, 2)}/{Token.Hash}"),
                    LocalPath:=Token.LocalPath,
                    Checker:=New FileChecker With {.ActualSize = If(Token.Size = 0, -1, Token.Size), .Hash = Token.Hash})).ToList
        End If
        'If the hash isn't checked, process immediately
        Dim Result As New List(Of NetFile)
        Dim AssetsList As List(Of McAssetsToken)
        Try
            AssetsList = McAssetsListGet(Instance)
            Dim Token As McAssetsToken
            If ProgressFeed IsNot Nothing Then ProgressFeed.Progress = 0.04
            For i = 0 To AssetsList.Count - 1
                'Initialize
                Token = AssetsList(i)
                If ProgressFeed IsNot Nothing Then ProgressFeed.Progress = 0.05 + 0.94 * i / AssetsList.Count
                'Check whether the file exists
                Dim File = FileUtils.GetInfo(Token.LocalPath)
                If File.Exists AndAlso (Token.Size = 0 OrElse Token.Size = File.Length) Then Continue For
                'File doesn't exist, add a download
                Result.Add(New NetFile(DlSourceAssetsGet($"https://resources.download.minecraft.net/{Left(Token.Hash, 2)}/{Token.Hash}"), Token.LocalPath, New FileChecker With {.ActualSize = If(Token.Size = 0, -1, Token.Size), .Hash = Token.Hash}))
            Next
        Catch ex As Exception
            Logger.Warn(ex, "获取版本缺失的资源文件下载列表失败")
        End Try
        If ProgressFeed IsNot Nothing Then ProgressFeed.Progress = 0.99
        Return Result
    End Function

#End Region

    ''' <summary>
    ''' Masks the AccessToken in a string.
    ''' </summary>
    Public Function FilterAccessToken(Raw As String, FilterChar As Char) As String
        'Mask the content after "accessToken "
        If Raw.Contains("accessToken ") Then
            For Each Token In Raw.RegexSearch("(?<=accessToken ([^ ]{5}))[^ ]+(?=[^ ]{5})")
                Raw = Raw.Replace(Token, New String(FilterChar, Token.Count))
            Next
        End If
        'Mask the current login results
        Dim AccessToken As String = McLoginLoader.Output.AccessToken
        If AccessToken IsNot Nothing AndAlso AccessToken.Length >= 10 AndAlso Raw.ContainsIgnoreCase(AccessToken) AndAlso
            McLoginLoader.Output.Uuid <> McLoginLoader.Output.AccessToken Then 'Don't mask if the UUID equals the AccessToken
            Raw = Raw.Replace(AccessToken, Left(AccessToken, 5) & New String(FilterChar, AccessToken.Length - 10) & Right(AccessToken, 5))
        End If
        Return Raw
    End Function
    ''' <summary>
    ''' Masks the Windows user name in a string.
    ''' </summary>
    Public Function FilterUserName(Raw As String, FilterChar As Char) As String
        If Raw.Contains(":\Users\") Then
            Raw = RegularExpressions.Regex.Replace(Raw, "(?<=:\\Users\\)[^\\]+", Function(Token) New String(FilterChar, Token.Length))
        End If
        If Raw.Contains(":/Users/") Then
            Raw = RegularExpressions.Regex.Replace(Raw, "(?<=:/Users/)[^/]+", Function(Token) New String(FilterChar, Token.Length))
        End If
        Return Raw
    End Function

End Module
