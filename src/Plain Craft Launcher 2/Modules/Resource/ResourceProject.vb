''' <summary>
''' Project info of a community resource.
''' </summary>
Public Class ResourceProject

    'Source info

    ''' <summary>
    ''' The platform this project info comes from.
    ''' </summary>
    Public ReadOnly Platform As ResourcePlatforms
    ''' <summary>
    ''' The type of files contained in the project.
    ''' For a Modrinth project, may be Mod or data pack.
    ''' </summary>
    Public ReadOnly Types As ResourceTypes
    ''' <summary>
    ''' Short name of the project, e.g. technical-enchant.
    ''' </summary>
    Public ReadOnly Slug As String
    ''' <summary>
    ''' Numeric ID of a CurseForge project, or the random-looking ID of a Modrinth project.
    ''' </summary>
    Public ReadOnly Id As String
    ''' <summary>
    ''' Numeric ID of the CurseForge file list. Not used for Modrinth projects.
    ''' </summary>
    Public ReadOnly CurseForgeFileIds As List(Of Integer)

    'Descriptive info

    ''' <summary>
    ''' The original English name.
    ''' </summary>
    Public ReadOnly RawName As String
    ''' <summary>
    ''' English description.
    ''' </summary>
    Public ReadOnly Description As String
    ''' <summary>
    ''' URL of the project page on the source website. Guaranteed to be in a standard format.
    ''' CurseForge：https://www.curseforge.com/minecraft/mc-mods/jei
    ''' Modrinth：https://modrinth.com/mod/technical-enchant
    ''' </summary>
    Public ReadOnly Website As String
    ''' <summary>
    ''' Time of the last update.
    ''' </summary>
    Public ReadOnly LastUpdate As Date
    ''' <summary>
    ''' Download count. Note: this count is from a single source only and does not reflect the combined downloads!
    ''' </summary>
    Public ReadOnly DownloadCount As Integer
    ''' <summary>
    ''' All supported mod loaders. May be <see cref="ModLoaders.None"/>.
    ''' </summary>
    Public ReadOnly ModLoaders As ModLoaders = ModLoaders.None
    ''' <summary>
    ''' Content of the descriptive tags. Already converted to Chinese.
    ''' </summary>
    Public ReadOnly Tags As List(Of String)
    ''' <summary>
    ''' Download URL of the logo image.
    ''' Nothing if there is none; guaranteed not to be an empty string.
    ''' </summary>
    Public LogoUrl As String = Nothing
    ''' <summary>
    ''' Supported drop numbers, sorted from high to low; never Nothing.
    ''' For example: 261 (26.1.x), 180 (1.18.x).
    ''' </summary>
    Public ReadOnly Drops As List(Of Integer)
    ''' <summary>
    ''' The raw version list returned by the Modrinth API.
    ''' Only used for secondary filtering of Modrinth projects; not cached and not preprocessed.
    ''' Nothing if not fetched from Modrinth.
    ''' </summary>
    Public ReadOnly UnsafeGameVersions As List(Of String)

    'Database info

    Private LoadedDatabase As Boolean = False
    Private _WikiEntry As WikiEntry = Nothing
    ''' <summary>
    ''' The associated database entry. Nothing if there is none.
    ''' </summary>
    Private Property WikiEntry As WikiEntry
        Get
            If Not LoadedDatabase Then
                LoadedDatabase = True
                If Types.HasFlagF(ResourceTypes.Mod) OrElse Types.HasFlagF(ResourceTypes.DataPack) Then
                    _WikiEntry = WikiEntry.All.Value.FirstOrDefault(Function(c) c.Slugs.GetOrDefault(Platform) = Slug)
                End If
            End If
            Return _WikiEntry
        End Get
        Set(value As WikiEntry)
            LoadedDatabase = True
            _WikiEntry = value
        End Set
    End Property
    ''' <summary>
    ''' Page ID on MCMOD. 0 if there is none.
    ''' </summary>
    Public ReadOnly Property WikiId As Integer
        Get
            Return If(WikiEntry Is Nothing, 0, WikiEntry.Id)
        End Get
    End Property
    ''' <summary>
    ''' The translated Chinese name. Equal to RawName if the database has none.
    ''' </summary>
    Public ReadOnly Property TranslatedName As String
        Get
            Return If(WikiEntry?.ChineseName, RawName)
        End Get
    End Property

    'Instantiation

    ''' <summary>
    ''' Initializes the instance from the project JSON. Throws an exception on error.
    ''' </summary>
    Public Sub New(Data As JObject)
        If Data.ContainsKey("Tags") Then
#Region "Cache JSON"
            Platform = Data("Platform").ToObject(Of Integer)
            Types = Data("Types").ToObject(Of Integer)
            Slug = Data("Slug")
            Id = Data("Id")
            If Data.ContainsKey("CurseForgeFileIds") Then CurseForgeFileIds = CType(Data("CurseForgeFileIds"), JArray).Select(Function(t) t.ToObject(Of Integer)).ToList
            RawName = Data("RawName")
            Description = Data("Description")
            Website = Data("Website")
            LastUpdate = Data("LastUpdate")
            DownloadCount = Data("DownloadCount")
            If Data.ContainsKey("ModLoaders") Then ModLoaders = Data("ModLoaders").ToObject(Of Integer)
            Tags = CType(Data("Tags"), JArray).Select(Function(t) t.ToString).ToList
            If Data.ContainsKey("LogoUrl") Then LogoUrl = Data("LogoUrl")
            If Data.ContainsKey("Drops") Then
                Drops = CType(Data("Drops"), JArray).Select(Function(t) t.ToObject(Of Integer)).ToList
            Else
                Drops = New List(Of Integer)
            End If
#End Region
        Else
            Platform = If(Data.ContainsKey("summary"), ResourcePlatforms.CurseForge, ResourcePlatforms.Modrinth)
            If Platform = ResourcePlatforms.CurseForge Then
#Region "CurseForge"
                'Simple info
                Id = Data("id")
                Slug = Data("slug")
                RawName = Data("name")
                Description = Data("summary")
                Website = Data("links")("websiteUrl").ToString.TrimEnd("/")
                LastUpdate = Data("dateReleased") '#1194
                DownloadCount = Data("downloadCount")
                If Data("logo").Count > 0 Then
                    If Data("logo")("thumbnailUrl") Is Nothing OrElse Data("logo")("thumbnailUrl") = "" Then
                        LogoUrl = Data("logo")("url")
                    Else
                        LogoUrl = Data("logo")("thumbnailUrl")
                    End If
                End If
                If LogoUrl = "" Then LogoUrl = Nothing
                'Type
                If Website.Contains("/mc-mods/") OrElse Website.Contains("/mod/") Then
                    Types = ResourceTypes.Mod
                ElseIf Website.Contains("/modpacks/") Then
                    Types = ResourceTypes.ModPack
                ElseIf Website.Contains("/resourcepacks/") Then
                    Types = ResourceTypes.ResourcePack
                ElseIf Website.Contains("/texture-packs/") Then
                    Types = ResourceTypes.ResourcePack
                ElseIf Website.Contains("/shaders/") Then
                    Types = ResourceTypes.Shader
                Else
                    Types = ResourceTypes.DataPack
                End If
                'FileIndexes / VanillaMajorVersions / ModLoaders
                ModLoaders = ModLoaders.None
                Dim Files As New List(Of KeyValuePair(Of Integer, List(Of String))) 'FileId, GameVersions
                For Each File In If(Data("latestFiles"), New JArray)
                    Dim NewFile = ResourceVersion.FromPlatformJson(File, Types)
                    If Not NewFile.DownloadAvailable Then Continue For
                    ModLoaders = ModLoaders Or NewFile.ModLoaders
                    Dim GameVersions = File("gameVersions").ToObject(Of List(Of String))
                    If Not GameVersions.Any(Function(v) McVersion.IsStandardFormat(v)) Then Continue For
                    Files.Add(New KeyValuePair(Of Integer, List(Of String))(File("id"), GameVersions))
                Next
                For Each File In If(Data("latestFilesIndexes"), New JArray) 'These two contain different files, see #3599
                    If Not McVersion.IsStandardFormat(File("gameVersion")) Then Continue For
                    Files.Add(New KeyValuePair(Of Integer, List(Of String))(File("fileId"), {File("gameVersion").ToString}.ToList))
                Next
                CurseForgeFileIds = Files.Select(Function(f) f.Key).Distinct.ToList
                Drops = Files.SelectMany(Function(f) f.Value).
                    Select(Function(v) McVersion.VersionToDrop(v)).Where(Function(v) v <> 209).Distinct.OrderByDescending(Function(v) v).ToList
                'Tags
                Tags = New List(Of String)
                For Each Category In If(Data("categories"), New JArray). 'Mirror source APIs may drop this field (4267#issuecomment-2254590831)
                    Select(Of Integer)(Function(t) t("id")).Distinct.OrderByDescending(Function(c) c)
                    Select Case Category
                            'Mod
                        Case 406 : Tags.Add("世界元素")
                        Case 407 : Tags.Add("生物群系")
                        Case 410 : Tags.Add("维度")
                        Case 408 : Tags.Add("矿物/资源")
                        Case 409 : Tags.Add("天然结构")
                        Case 412 : Tags.Add("科技")
                        Case 415 : Tags.Add("管道/物流")
                        Case 4843 : Tags.Add("自动化")
                        Case 417 : Tags.Add("能源")
                        Case 4558 : Tags.Add("红石")
                        Case 436 : Tags.Add("食物/烹饪")
                        Case 416 : Tags.Add("农业")
                        Case 414 : Tags.Add("运输")
                        Case 420 : Tags.Add("仓储")
                        Case 419 : Tags.Add("魔法")
                        Case 422 : Tags.Add("冒险")
                        Case 424 : Tags.Add("装饰")
                        Case 411 : Tags.Add("生物")
                        Case 434 : Tags.Add("装备")
                        Case 6814 : Tags.Add("性能优化")
                        Case 9026 : Tags.Add("创造模式")
                        Case 423 : Tags.Add("信息显示")
                        Case 435 : Tags.Add("服务器")
                        Case 5191 : Tags.Add("改良")
                        Case 421 : Tags.Add("支持库")
                            'Modpack
                        Case 4484 : Tags.Add("多人")
                        Case 4479 : Tags.Add("硬核")
                        Case 4483 : Tags.Add("战斗")
                        Case 4478 : Tags.Add("任务")
                        Case 4472 : Tags.Add("科技")
                        Case 4473 : Tags.Add("魔法")
                        Case 4475 : Tags.Add("冒险")
                        Case 4476 : Tags.Add("探索")
                        Case 4477 : Tags.Add("小游戏")
                        Case 4474 : Tags.Add("科幻")
                        Case 4736 : Tags.Add("空岛")
                        Case 5128 : Tags.Add("原版改良")
                        Case 4487 : Tags.Add("FTB")
                        Case 4480 : Tags.Add("基于地图")
                        Case 4481 : Tags.Add("轻量")
                        Case 4482 : Tags.Add("大型")
                            'Resource pack
                        Case 403 : Tags.Add("原版风")
                        Case 400 : Tags.Add("写实风")
                        Case 401 : Tags.Add("现代风")
                        Case 402 : Tags.Add("中世纪")
                        Case 399 : Tags.Add("蒸汽朋克")
                        Case 5244 : Tags.Add("含字体")
                        Case 404 : Tags.Add("动态效果")
                        Case 4465 : Tags.Add("兼容 Mod")
                        Case 393 : Tags.Add("16x")
                        Case 394 : Tags.Add("32x")
                        Case 395 : Tags.Add("64x")
                        Case 396 : Tags.Add("128x")
                        Case 397 : Tags.Add("256x")
                        Case 398 : Tags.Add("超高清")
                        Case 5193 : Tags.Add("数据包") 'Items with this tag are removed from resource pack requests
                            'Shader pack
                        Case 6553 : Tags.Add("写实风")
                        Case 6554 : Tags.Add("幻想风")
                        Case 6555 : Tags.Add("原版风")
                            'Data pack
                        Case 6948 : Tags.Add("冒险")
                        Case 6949 : Tags.Add("幻想")
                        Case 6950 : Tags.Add("支持库")
                        Case 6952 : Tags.Add("魔法")
                        Case 6946 : Tags.Add("Mod 相关")
                        Case 6951 : Tags.Add("科技")
                        Case 6953 : Tags.Add("实用")
                    End Select
                Next
#End Region
            Else
#Region "Modrinth"
                'Simple info
                Id = If(Data("project_id"), Data("id")) 'The two APIs return different keys
                Slug = Data("slug")
                RawName = Data("title")
                Description = Data("description")
                LastUpdate = If(Data("date_modified"), Data("updated"))
                DownloadCount = Data("downloads")
                LogoUrl = Data("icon_url")
                If LogoUrl = "" Then LogoUrl = Nothing
                Website = $"https://modrinth.com/{Data("project_type")}/{Slug}"
                'GameVersions
                'The key is versions in search results and game_versions when fetching a specific project
                UnsafeGameVersions = If(CType(If(Data("game_versions"), Data("versions")), JArray), New JArray).Select(Function(v) v.ToString).Distinct.ToList
                Drops = UnsafeGameVersions.Select(Function(v) McVersion.VersionToDrop(v)).Where(Function(v) v <> 209).Distinct.OrderByDescending(Function(v) v).ToList
                'Type
                Select Case Data("project_type").ToString
                    Case "modpack" : Types = ResourceTypes.ModPack
                    Case "resourcepack" : Types = ResourceTypes.ResourcePack
                    Case "shader" : Types = ResourceTypes.Shader
                    Case Else : Types = ResourceTypes.Mod 'Modrinth marks data packs as Mod, but the categories field contains datapack
                End Select
                'Tags & ModLoaders
                Tags = New List(Of String)
                ModLoaders = ModLoaders.None
                For Each Category In If(Data("loaders")?.Select(Function(t) t.ToString), {}).Concat(Data("categories").Select(Function(t) t.ToString))
                    For Each Loader In EnumUtils.GetAllFlags(Of ModLoaders)()
                        If Category = Loader.ToString.Lower Then
                            ModLoaders = ModLoaders Or Loader
                            Exit For
                        End If
                    Next
                Next
                For Each Category In Data("categories").Select(Function(t) t.ToString)
                    Select Case Category
                        Case "datapack" : Types = ResourceTypes.DataPack
                            'Shared
                        Case "technology" : Tags.Add("科技")
                        Case "magic" : Tags.Add("魔法")
                        Case "adventure" : Tags.Add("冒险")
                        Case "utility" : Tags.Add("实用")
                        Case "optimization" : Tags.Add("性能优化")
                        Case "vanilla-like" : Tags.Add("原版风")
                        Case "realistic" : Tags.Add("写实风")
                            'Mod / data pack
                        Case "worldgen" : Tags.Add("世界元素")
                        Case "food" : Tags.Add("食物/烹饪")
                        Case "game-mechanics" : Tags.Add("游戏机制")
                        Case "transportation" : Tags.Add("运输")
                        Case "storage" : Tags.Add("仓储")
                        Case "decoration" : If Types <> ResourceTypes.ResourcePack Then Tags.Add("装饰")
                        Case "mobs" : If Types <> ResourceTypes.ResourcePack Then Tags.Add("生物")
                        Case "equipment" : If Types <> ResourceTypes.ResourcePack Then Tags.Add("装备")
                        Case "social" : Tags.Add("服务器")
                        Case "library" : Tags.Add("支持库")
                            'Modpack
                        Case "multiplayer" : Tags.Add("多人")
                        Case "challenging" : Tags.Add("硬核")
                        Case "combat" : Tags.Add("战斗")
                        Case "quests" : Tags.Add("任务")
                        Case "kitchen-sink" : Tags.Add("水槽包")
                        Case "lightweight" : Tags.Add("轻量")
                            'Resource pack
                        Case "simplistic" : Tags.Add("简洁")
                        Case "combat" : Tags.Add("战斗")
                        Case "tweaks" : Tags.Add("改良")

                        Case "8x-" : Tags.Add("极简")
                        Case "16x" : Tags.Add("16x")
                        Case "32x" : Tags.Add("32x")
                        Case "48x" : Tags.Add("48x")
                        Case "64x" : Tags.Add("64x")
                        Case "128x" : Tags.Add("128x")
                        Case "256x" : Tags.Add("256x")
                        Case "512x+" : Tags.Add("超高清")

                        Case "audio" : Tags.Add("含声音")
                        Case "fonts" : Tags.Add("含字体")
                        Case "models" : Tags.Add("含模型")
                        Case "gui" : Tags.Add("含 UI")
                        Case "locale" : Tags.Add("含语言")
                        Case "core-shaders" : Tags.Add("核心着色器")
                        Case "modded" : Tags.Add("兼容 Mod")
                            'Shader pack
                        Case "fantasy" : Tags.Add("幻想风")
                        Case "semi-realistic" : Tags.Add("半写实风")
                        Case "cartoon" : Tags.Add("卡通风")
                            'Performance load tags are not added for now
                        Case "colored-lighting" : Tags.Add("彩色光照")
                        Case "path-tracing" : Tags.Add("路径追踪")
                        Case "pbr" : Tags.Add("PBR")
                        Case "reflections" : Tags.Add("反射")

                        Case "iris" : Tags.Add("Iris")
                        Case "optifine" : Tags.Add("OptiFine")
                        Case "vanilla" : Tags.Add("原版可用")
                    End Select
                Next
                If Types = ResourceTypes.DataPack AndAlso ModLoaders <> ModLoaders.None Then Types = ResourceTypes.DataPack Or ResourceTypes.Mod
#End Region
            End If
            If Not Tags.Any() Then Tags.Add("其他")
            Tags.Sort()
        End If
        'Save the cache
        Cache(Id) = Me
    End Sub
    ''' <summary>
    ''' Fetches the project info online from the project ID.
    ''' </summary>
    Public Shared Function FromProjectId(ProjectId As String, Platform As ResourcePlatforms) As ResourceProject
        If Not Platform.Flags.IsSingle Then Throw New ArgumentException($"必须指定单一平台，当前值为 {Platform}")
        If Cache.ContainsKey(ProjectId) Then 'Cache exists
            Return Cache(ProjectId)
        ElseIf Platform = ResourcePlatforms.CurseForge Then 'CurseForge
            Return New ResourceProject(DlModRequest("https://api.curseforge.com/v1/mods/" & ProjectId)("data"))
        Else 'Modrinth
            Return New ResourceProject(DlModRequest("https://api.modrinth.com/v2/project/" & ProjectId))
        End If
    End Function
    ''' <summary>
    ''' Fetches project info online from a list of project IDs; results are not in the original order.
    ''' </summary>
    Public Shared Iterator Function FromProjectIds(ProjectIds As IEnumerable(Of String), Platform As ResourcePlatforms) As IEnumerable(Of ResourceProject)
        If Not Platform.Flags.IsSingle Then Throw New ArgumentException($"必须指定单一平台，当前值为 {Platform}")
        'Get the cache
        Dim MissingProjectIds As New List(Of String)
        For Each ProjectId In ProjectIds
            If Cache.ContainsKey(ProjectId) Then
                Yield Cache(ProjectId)
            Else
                MissingProjectIds.Add(ProjectId)
            End If
        Next
        If Not MissingProjectIds.Any() Then Return
        'Fetch the remaining project objects online
        Dim Projects As JArray
        If Platform.HasFlagF(ResourcePlatforms.CurseForge) Then
            Projects = DlModRequest("https://api.curseforge.com/v1/mods",
                HttpMethod.Post, "{""modIds"": [" & MissingProjectIds.Join(","c) & "]}", "application/json")("data")
        Else
            Projects = DlModRequest($"https://api.modrinth.com/v2/projects?ids=[""{MissingProjectIds.Join(""",""")}""]")
        End If
        For Each Project In Projects
            Yield New ResourceProject(Project)
        Next
    End Function

    ''' <summary>
    ''' Converts the current instance to JSON for saving to the cache.
    ''' </summary>
    Public Function ToJson() As JObject
        Dim Json As New JObject
        Json("Platform") = CInt(Platform)
        Json("Types") = CInt(Types)
        Json("Slug") = Slug
        Json("Id") = Id
        If CurseForgeFileIds IsNot Nothing Then Json("CurseForgeFileIds") = New JArray(CurseForgeFileIds)
        Json("RawName") = RawName
        Json("Description") = Description
        Json("Website") = Website
        Json("LastUpdate") = LastUpdate
        Json("DownloadCount") = DownloadCount
        If ModLoaders <> ModLoaders.None Then Json("ModLoaders") = CInt(ModLoaders)
        Json("Tags") = New JArray(Tags)
        If LogoUrl IsNot Nothing Then Json("LogoUrl") = LogoUrl
        If Drops.Any Then Json("Drops") = New JArray(Drops)
        Json("CacheTime") = Date.Now 'Used to check the cache time
        Return Json
    End Function

    ''' <summary>
    ''' Creates a control from the current project info.
    ''' </summary>
    Public Function ToResourceItem(ShowMcVersionDesc As Boolean, ShowLoaderDesc As Boolean) As MyVirtualizingElement(Of MyResourceItem)
        'Get the version description
        Dim GameVersionDescription As String
        If Drops Is Nothing OrElse Not Drops.Any() Then
            GameVersionDescription = "仅快照版本" '#5412
        Else
            Dim Segments As New List(Of String)
            Dim IsOld As Boolean = False
            For i = 0 To Drops.Count - 1 'Version numbers are always in descending order
                'Get the current contiguous version ranges
                Dim StartDrop As Integer = Drops(i), EndDrop As Integer = Drops(i)
                If StartDrop < 120 Then 'If newer versions are supported, do not show 1.11-
                    If Segments.Any() AndAlso Not IsOld Then
                        Exit For
                    Else
                        IsOld = True
                    End If
                End If
                For ii = i + 1 To Drops.Count - 1
                    If AllDrops Is Nothing OrElse AllDrops.IndexOf(Drops(ii)) <> AllDrops.IndexOf(EndDrop) + 1 Then Exit For
                    EndDrop = Drops(ii)
                    i = ii
                Next
                'Convert the version ranges to description text
                Dim StartName = McVersion.DropToVersion(StartDrop)
                Dim EndName = McVersion.DropToVersion(EndDrop)
                If StartDrop = EndDrop Then
                    Segments.Add(StartName)
                ElseIf AllDrops IsNot Nothing AndAlso StartDrop >= AllDrops.First Then
                    If EndDrop < 120 Then
                        Segments.Clear()
                        Segments.Add("全版本")
                        Exit For
                    Else
                        Segments.Add(EndName & "+")
                    End If
                ElseIf EndDrop < 120 Then
                    Segments.Add(StartName & "-")
                    Exit For
                ElseIf AllDrops Is Nothing OrElse AllDrops.IndexOf(EndDrop) - AllDrops.IndexOf(StartDrop) = 1 Then
                    Segments.Add(StartName & ", " & EndName)
                Else
                    Segments.Add(StartName & "~" & EndName)
                End If
            Next
            GameVersionDescription = Segments.Join(", ")
        End If
        'Get the mod loader description
        Dim ModLoaderDescriptionFull As String, ModLoaderDescriptionPart As String
        Dim ModLoadersForDesc = ModLoaders.Flags().ToList()
        Select Case ModLoadersForDesc.Count
            Case 0
                If ModLoadersForDesc.IsSingle Then
                    ModLoaderDescriptionFull = "仅 " & ModLoadersForDesc.Single.ToString
                    ModLoaderDescriptionPart = ModLoadersForDesc.Single.ToString
                Else
                    ModLoaderDescriptionFull = "未知"
                    ModLoaderDescriptionPart = ""
                End If
            Case 1
                ModLoaderDescriptionFull = "仅 " & ModLoadersForDesc.Single.ToString
                ModLoaderDescriptionPart = ModLoadersForDesc.Single.ToString
            Case Else
                Dim NewestDrop As Integer = If(Drops.Any, Drops.First, 9999)
                If ModLoadersForDesc.Contains(ModLoaders.Forge) AndAlso
                   (NewestDrop < 140 OrElse ModLoadersForDesc.Contains(ModLoaders.Fabric)) AndAlso
                   (NewestDrop < 200 OrElse ModLoadersForDesc.Contains(ModLoaders.NeoForge)) Then
                    ModLoaderDescriptionFull = "任意"
                    ModLoaderDescriptionPart = ""
                Else
                    ModLoaderDescriptionFull = ModLoadersForDesc.Join(" / ")
                    ModLoaderDescriptionPart = ModLoadersForDesc.Join(" / ")
                End If
        End Select
        'Create the UI
        Return New MyVirtualizingElement(Of MyResourceItem)(
        Function()
            Dim NewItem As New MyResourceItem With {.Tag = Me}
            ApplyLogoToMyImage(NewItem.PathLogo)
            Dim Titles = GetControlTitle(True)
            NewItem.Title = Titles.Title
            If Titles.SubTitle = "" Then
                CType(NewItem.LabTitleRaw.Parent, StackPanel).Children.Remove(NewItem.LabTitleRaw)
            Else
                NewItem.SubTitle = Titles.SubTitle
            End If
            NewItem.Tags = Tags
            NewItem.Description = Description.ReplaceLineEndings("")
            'Bottom bar
            If Not ShowMcVersionDesc AndAlso Not ShowLoaderDesc Then
                'Hide all
                CType(NewItem.PathVersion.Parent, Grid).Children.Remove(NewItem.PathVersion)
                CType(NewItem.LabVersion.Parent, Grid).Children.Remove(NewItem.LabVersion)
                NewItem.ColumnVersion1.Width = New GridLength(0)
                NewItem.ColumnVersion2.MaxWidth = 0
                NewItem.ColumnVersion3.Width = New GridLength(0)
            ElseIf ShowMcVersionDesc AndAlso ShowLoaderDesc Then
                'Show all
                NewItem.LabVersion.Text = If(ModLoaderDescriptionPart = "", "", ModLoaderDescriptionPart & " ") & GameVersionDescription
            ElseIf ShowMcVersionDesc Then
                'Show versions only
                NewItem.LabVersion.Text = GameVersionDescription
            Else
                'Show mod loaders only
                NewItem.LabVersion.Text = ModLoaderDescriptionFull
            End If
            NewItem.LabSource.Text = Platform.ToString
            NewItem.LabTime.Text = StringUtils.FormatTimeSpan(LastUpdate - Date.Now, True)
            NewItem.LabDownload.Text =
                If(DownloadCount > 100000000, Math.Round(DownloadCount / 100000000, 2) & " 亿",
                If(DownloadCount > 100000, Math.Floor(DownloadCount / 10000) & " 万", DownloadCount))
            Return NewItem
        End Function) With {.Height = 64}
    End Function
    Public Sub ApplyLogoToMyImage(Img As MyImage)
        If String.IsNullOrEmpty(LogoUrl) Then
            Img.Source = PathImage & "Icons/NoIcon.png"
        Else
            Img.Source = LogoUrl
            Img.FallbackSource = DlSourceModGet(LogoUrl)
        End If
    End Sub
    Public Function GetControlTitle(HasModLoaderDescription As Boolean) As (Title As String, SubTitle As String)
        'See the test cases in #1567 when reviewing the code below
        Dim Title As String = RawName
        Dim SubtitleList As List(Of String)
        If TranslatedName = RawName Then
            'No Chinese translation
            'Split all names into segments
            Dim NameLists = TranslatedName.Split({" | ", " - ", "(", ")", "[", "]", "{", "}"}, True).
                Select(Function(s) s.Trim(" /\".ToCharArray)).Where(Function(w) Not String.IsNullOrEmpty(w))
            If NameLists.IsSingle Then GoTo NoSubtitle
            'Find abbreviations and Forge/Fabric version markers among them
            SubtitleList = New List(Of String)
            Dim NormalNameList = New List(Of String)
            For Each Name In NameLists
                Dim LowerName As String = Name.Lower
                If Name = Name.Upper AndAlso Name <> "FPS" AndAlso Name <> "HUD" Then
                    'Abbreviation
                    SubtitleList.Add(Name)
                ElseIf {"neoforge", "forge", "fabric", "quilt"}.Any(Function(l) LowerName.Contains(l)) AndAlso
                    Not LowerName.Replace("neoforge", "").Replace("forge", "").Replace("fabric", "").Replace("quilt", "").RegexCheck("[a-z]+") Then 'No other letters remain after removing the keywords
                    'Forge/Fabric version marker
                    SubtitleList.Add(Name)
                Else
                    'Other parts
                    NormalNameList.Add(Name)
                End If
            Next
            'Process according to the classified results
            If Not NormalNameList.Any() OrElse Not SubtitleList.Any() Then GoTo NoSubtitle
            'Contains both NormalName and Subtitle
            Title = NormalNameList.Join(" - ")
        Else
            'Has a Chinese translation
            'Try to split the text into three parts: Title (EnglishName) - Suffix
            'When checking, mind Carpet (no Chinese name but has a Suffix) and "汤姆存储 - 星的奇妙优化 (Tom's balabala)"
            Title = If(TranslatedName.Contains(" ("), TranslatedName.BeforeFirst(" ("), TranslatedName.BeforeLast(" - "))
            Dim Suffix As String = ""
            If TranslatedName.AfterLast(")").Contains(" - ") Then Suffix = TranslatedName.AfterLast(")").AfterLast(" - ")
            Dim EnglishName As String = TranslatedName
            If Suffix <> "" Then EnglishName = EnglishName.Replace(" - " & Suffix, "")
            EnglishName = EnglishName.Replace(Title, "").Trim("("c, ")"c, " "c)
            'Extract extra info from the middle part
            SubtitleList = EnglishName.Split({" | ", " - ", "(", ")", "[", "]", "{", "}"}, True).
                    Select(Function(s) s.Trim(" /".ToCharArray)).Where(Function(w) Not String.IsNullOrEmpty(w)).ToList
            If SubtitleList.Count > 1 AndAlso
               Not SubtitleList.Any(Function(s) s.Lower.Contains("forge") OrElse s.Lower.Contains("fabric") OrElse s.Lower.Contains("quilt")) AndAlso 'Not a "XX 版" marker
               Not (SubtitleList.Count = 2 AndAlso SubtitleList.Last.Upper = SubtitleList.Last) Then 'Not an abbreviation
                SubtitleList = New List(Of String) From {EnglishName} 'Use the original name
            End If
            'Add the suffix
            If Suffix <> "" Then SubtitleList.Add(Suffix)
        End If
        SubtitleList = SubtitleList.Distinct.ToList()
        'Set the title and description
        Dim Subtitle As String = ""
        If SubtitleList.Any Then
            For Each Ex In SubtitleList
                Dim IsModLoaderDescription As Boolean =
                    Ex.Lower.Contains("neoforge") OrElse Ex.Lower.Contains("forge") OrElse Ex.Lower.Contains("fabric") OrElse Ex.Lower.Contains("quilt")
                'Whether to show ModLoader info
                If Not HasModLoaderDescription AndAlso IsModLoaderDescription Then Continue For
                'Remove the meaningless "Forge/Fabric" hint
                If Ex.Length < 16 AndAlso Ex.Lower.Contains("fabric") AndAlso Ex.Lower.Contains("forge") Then Continue For
                'Change hints like "Forge" to "Forge 版"
                If IsModLoaderDescription AndAlso Not Ex.Contains("版") AndAlso
                    Ex.Lower.Replace("neoforge", "").Replace("forge", "").Replace("fabric", "").Replace("quilt", "").Length <= 3 Then
                    Ex = Ex.Replace("Edition", "").Replace("edition", "").Trim.Capitalize & " 版"
                End If
                'Capitalize words like "forge"
                Ex = Ex.Replace("neoforge", "NeoForge").Replace("forge", "Forge").Replace("neo", "Neo").Replace("fabric", "Fabric").Replace("quilt", "Quilt")
                Subtitle &= "  |  " & Ex.Trim
            Next
        Else
NoSubtitle:
            Subtitle = ""
        End If
        Return (Title, Subtitle)
    End Function

    ''' <summary>
    ''' Cache of project instances by project ID. Not saved to a file.
    ''' </summary>
    Protected Friend Shared Cache As New Dictionary(Of String, ResourceProject)

    ''' <summary>
    ''' Checks whether this is the same project as another Project, just on a different website.
    ''' </summary>
    Public Function IsLike(Project As ResourceProject) As Boolean
        If Id = Project.Id Then Return True 'Same instance
        'Extract letters and digits from the string
        Dim GetRaw =
        Function(Data As String) As String
            Dim Result As New StringBuilder()
            For Each r As Char In Data.Where(Function(c) Char.IsLetterOrDigit(c))
                Result.Append(r)
            Next
            Return Result.ToString.Lower
        End Function
        'From different websites
        If Platform = Project.Platform Then Return False
        'Same mod loader
        If ModLoaders <> Project.ModLoaders Then Return False
        'Unless it is a shader, require the same MC version
        If Types <> ResourceTypes.Shader AndAlso Drops.Ordered.SequenceEqual(Project.Drops.Ordered) Then Return False
        'Last update times are within a week of each other
        If Math.Abs((LastUpdate - Project.LastUpdate).TotalDays) > 7 Then Return False
        'MCMOD translated name / original name / description / English part of the slug are the same
        If TranslatedName = Project.TranslatedName OrElse
           RawName = Project.RawName OrElse Description = Project.Description OrElse
           GetRaw(Slug) = GetRaw(Project.Slug) Then
            Logger.Info($"将 {RawName} ({Slug}) 与 {Project.RawName} ({Project.Slug}) 认定为相似工程")
            'If only one has a DatabaseEntry, assign it to the other
            If WikiEntry Is Nothing AndAlso Project.WikiEntry IsNot Nothing Then WikiEntry = Project.WikiEntry
            If WikiEntry IsNot Nothing AndAlso Project.WikiEntry Is Nothing Then Project.WikiEntry = WikiEntry
            Return True
        End If
        Return False
    End Function

    'Helper functions
    Public Overrides Function ToString() As String
        Return $"{Id} ({Slug}): {RawName}"
    End Function
    Public Overrides Function Equals(obj As Object) As Boolean
        Dim project = TryCast(obj, ResourceProject)
        Return project IsNot Nothing AndAlso Id = project.Id
    End Function
    Public Shared Operator =(left As ResourceProject, right As ResourceProject) As Boolean
        Return EqualityComparer(Of ResourceProject).Default.Equals(left, right)
    End Operator
    Public Shared Operator <>(left As ResourceProject, right As ResourceProject) As Boolean
        Return Not left = right
    End Operator

End Class
