Public Class ResourceSearcher
    Private Const RESULT_PAGE_SIZE = 40
    Private Const MSG_NO_CHINESE_RESULT As String = "无搜索结果，请尝试搜索其英文名称"

    'Input
    Public Class SearchRequest

        'Result requirements

        ''' <summary>
        ''' Result storage; the pages to load etc. are read from here.
        ''' Results should also be written here after loading.
        ''' </summary>
        Public Storage As SearchResult
        ''' <summary>
        ''' The number of results to try to reach.
        ''' </summary>
        Public TargetResultCount As Integer
        ''' <summary>
        ''' Whether more content can still be fetched, based on the load position records.
        ''' </summary>
        Public ReadOnly Property CanContinue As Boolean
            Get
                If Tag.StartsWithF("/") OrElse Not Sources.HasFlagF(ResourcePlatforms.CurseForge) Then Storage.CurseForgeTotal = 0
                If Tag.EndsWithF("/") OrElse Not Sources.HasFlagF(ResourcePlatforms.Modrinth) Then Storage.ModrinthTotal = 0
                If Storage.CurseForgeTotal = -1 OrElse Storage.ModrinthTotal = -1 Then Return True
                Return Storage.CurseForgeOffset < Storage.CurseForgeTotal OrElse Storage.ModrinthOffset < Storage.ModrinthTotal
            End Get
        End Property

        'Input content

        ''' <summary>
        ''' Filters the resource type.
        ''' </summary>
        Public Type As ResourceTypes
        ''' <summary>
        ''' Filters the resource tag. An empty string means no restriction. Format example: "406/worldgen", the CurseForge and Modrinth IDs respectively.
        ''' </summary>
        Public Tag As String = ""
        ''' <summary>
        ''' Filters the mod loader type.
        ''' <see cref="ModLoaders.None"/> means no filtering.
        ''' Multiple enum values are supported for Modrinth requests.
        ''' </summary>
        Public ModLoaders As ModLoaders = ModLoaders.None
        ''' <summary>
        ''' Filters the MC version.
        ''' </summary>
        Public GameVersion As String = Nothing
        ''' <summary>
        ''' The initial search content.
        ''' </summary>
        Public SearchText As String = Nothing
        ''' <summary>
        ''' Allowed sources.
        ''' </summary>
        Public Sources As ResourcePlatforms = ResourcePlatforms.Any
        Public Sub New(Type As ResourceTypes, Storage As SearchResult, TargetResultCount As Integer)
            Me.Type = Type
            Me.Storage = Storage
            Me.TargetResultCount = TargetResultCount
        End Sub

        'Build requests

        ''' <summary>
        ''' Gets the corresponding CurseForge API request URL.
        ''' </summary>
        Public Function GetCurseForgeAddress(SearchText As String, IgnoreModLoaderFilter As Boolean) As String
            If Tag.StartsWithF("/") Then Storage.CurseForgeTotal = 0
            'Apply filter parameters
            Dim Address As String = $"https://api.curseforge.com/v1/mods/search?gameId=432&sortField=2&sortOrder=desc&pageSize={RESULT_PAGE_SIZE}"
            Select Case Type
                Case ResourceTypes.Mod
                    Address += "&classId=6"
                Case ResourceTypes.ModPack
                    Address += "&classId=4471"
                Case ResourceTypes.DataPack
                    Address += "&classId=6945"
                Case ResourceTypes.Shader
                    Address += "&classId=6552"
                Case ResourceTypes.ResourcePack
                    Address += "&classId=12"
            End Select
            If Tag <> "" Then Address += "&categoryId=" & Tag.BeforeFirst("/")
            If ModLoaders <> ModLoaders.None AndAlso Not IgnoreModLoaderFilter Then Address += "&modLoaderType=" & Resource.ToCurseForgeModLoaderType(ModLoaders.Flags.First)
            If Not String.IsNullOrEmpty(GameVersion) Then Address += "&gameVersion=" & GameVersion
            If Not String.IsNullOrEmpty(SearchText) Then Address += "&searchFilter=" & StringUtils.UrlEscape(SearchText)
            If Storage.CurseForgeOffset > 0 Then Address += "&index=" & Storage.CurseForgeOffset
            Return Address
        End Function
        ''' <summary>
        ''' Gets the corresponding Modrinth API request URL.
        ''' </summary>
        Public Function GetModrinthAddress(SearchText As String, IgnoreModLoaderFilter As Boolean) As String
            If Tag.EndsWithF("/") Then Storage.ModrinthTotal = 0
            'Apply filter parameters
            Dim Address As String = $"https://api.modrinth.com/v2/search?limit={RESULT_PAGE_SIZE}&index=relevance"
            If Not String.IsNullOrEmpty(SearchText) Then Address += "&query=" & StringUtils.UrlEscape(SearchText)
            If Storage.ModrinthOffset > 0 Then Address += "&offset=" & Storage.ModrinthOffset
            'facets=[["categories:'game-mechanics'"],["categories:forge","categories:fabric"],["versions:1.19.3"],["project_type:mod"]]
            Dim Facets As New List(Of String)
            Facets.Add($"[""project_type:{Type.ToString.Lower}""]")
            If Not String.IsNullOrEmpty(Tag) Then Facets.Add($"[""categories:'{Tag.AfterLast("/")}'""]")
            If ModLoaders <> ModLoaders.None AndAlso Not IgnoreModLoaderFilter Then
                Facets.Add($"[""categories:{ModLoaders.Flags.Select(Function(f) f.ToString.Lower).Join(""",""categories:")}""]")
            End If
            If Not String.IsNullOrEmpty(GameVersion) Then Facets.Add($"[""versions:'{GameVersion}'""]")
            Address += "&facets=[" & String.Join(",", Facets) & "]"
            Return Address
        End Function

        'Equality check
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim request = TryCast(obj, SearchRequest)
            Return request IsNot Nothing AndAlso
                Type = request.Type AndAlso TargetResultCount = request.TargetResultCount AndAlso
                Tag = request.Tag AndAlso ModLoaders = request.ModLoaders AndAlso Sources = request.Sources AndAlso
                GameVersion = request.GameVersion AndAlso SearchText = request.SearchText
        End Function
        Public Shared Operator =(left As SearchRequest, right As SearchRequest) As Boolean
            Return EqualityComparer(Of SearchRequest).Default.Equals(left, right)
        End Operator
        Public Shared Operator <>(left As SearchRequest, right As SearchRequest) As Boolean
            Return Not left = right
        End Operator
        Public Overrides Function GetHashCode() As Integer
            Return (Type, Tag, ModLoaders, GameVersion, SearchText, Sources).GetHashCode()
        End Function

    End Class

    'Output
    Public Class SearchResult

        'Load position records

        Public CurseForgeOffset As Integer = 0
        Public CurseForgeTotal As Integer = -1

        Public ModrinthOffset As Integer = 0
        Public ModrinthTotal As Integer = -1

        'Result list

        ''' <summary>
        ''' List of all projects available for display.
        ''' </summary>
        Public Results As New List(Of ResourceProject)
        ''' <summary>
        ''' The current error info. Nothing if there is none.
        ''' </summary>
        Public ErrorMessage As String = Nothing

    End Class

    ''' <summary>
    ''' Gets a list of projects for the search request.
    ''' </summary>
    Public Shared Function GetLoader(InputDelegate As Func(Of SearchRequest)) As LoaderTask(Of SearchRequest, Integer)
        Return New LoaderTask(Of SearchRequest, Integer)("社区资源获取", AddressOf Run, InputDelegate) With {.ReloadTimeout = 60 * 1000}
    End Function
    Private Shared Sub Run(Task As LoaderTask(Of SearchRequest, Integer))
        Dim Request As SearchRequest = Task.Input
        Dim Storage = Request.Storage 'Prevent other threads from reassigning Request.Storage

#Region "Pre-checks"

        If Storage.Results.Count >= Request.TargetResultCount Then
            Logger.Info($"已有 {Storage.Results.Count} 个结果，多于所需的 {Request.TargetResultCount} 个结果，结束处理")
            Return
        ElseIf Not Request.CanContinue Then
            If Not Storage.Results.Any() Then
                Throw New Exception("没有符合条件的结果")
            Else
                Logger.Info($"已有 {Storage.Results.Count} 个结果，少于所需的 {Request.TargetResultCount} 个结果，但无法继续获取，结束处理")
                Return
            End If
        End If

#End Region

#Region "Chinese Search"

        Dim RawSearchText As String = If(Request.SearchText, "").Trim
        RawSearchText = RawSearchText.Lower
        Logger.Info($"工程列表搜索原始文本：{RawSearchText}")
        RawSearchText = StrConv(RawSearchText, VbStrConv.SimplifiedChinese) 'Traditional to Simplified Chinese

        Dim IsChineseSearch As Boolean =
            RawSearchText.RegexCheck("[\u4e00-\u9fbb]") AndAlso Not String.IsNullOrEmpty(RawSearchText) AndAlso
            (Request.Type = ResourceTypes.Mod OrElse Request.Type = ResourceTypes.DataPack) 'Chinese search is currently only done for mods and data packs; note modpack names may already be in Chinese
        Dim CurseForgeAltSearchText As String = Nothing, ModrinthAltSearchText As String = Nothing, ModrinthSlugs As New List(Of String) 'Alternative English search content converted from Chinese
        If IsChineseSearch Then
            'Helper: extract possible English words from a search entry
            Dim ExtractWords =
            Function(Result As SearchEntry(Of WikiEntry), Source As ResourcePlatforms) As IEnumerable(Of String)
                'Extract candidates from each possible source
                Dim Candidates As New List(Of String)
                If Result.Item.Slugs.ContainsKey(Source) Then
                    Candidates.Add(Result.Item.Slugs(Source).
                                   Replace("-", " ").Replace("/", " "))
                End If
                If Result.Item.ChineseName IsNot Nothing Then
                    Candidates.Add(Result.Item.ChineseName.
                                   AfterLast(" (").TrimEnd(") ").BeforeFirst(" - ").Replace("-", " ").Replace("/", " ").Replace(":", " ").Replace("(", " ").Replace(")", ""))
                End If
                'Tokenize, clean and deduplicate
                Candidates = Candidates.
                    SelectMany(Function(c) c.Split(" ")).
                    Select(Function(w) w.TrimStart("{[(").TrimEnd("}])").Lower).
                    Where(
                    Function(w)
                        If w.Length <= 1 Then Return False 'Single character or whitespace
                        If {"the", "of", "mod", "and", "forge", "fabric", "for", "quilt", "neoforge"}.Contains(w) Then Return False 'Common words
                        If Val(w) > 0 Then Return False 'Numbers
                        If Not w.IsAsciiOnly Then Return False 'Not pure English
                        Return True
                    End Function).Distinct.ToList
                'Remove a word if it can be composed of other words (e.g. drop enderio from "ender io enderio", keeping only ender io)
                Dim CanForm As Func(Of String, Boolean) = Nothing
                CanForm = Function(s) Candidates.Contains(s) OrElse Candidates.Any(Function(c) s.StartsWith(c) AndAlso CanForm(s.Substring(c.Length)))
                Candidates = Candidates.Where(Function(w) Not Candidates.Any(Function(c) c.Length < w.Length AndAlso w.StartsWith(c) AndAlso CanForm(w.Substring(c.Length)))).ToList()
                Return Candidates
            End Function
            Dim GetSearchEntries =
            Iterator Function(Source As ResourcePlatforms)
                For Each Entry In WikiEntry.All.Value
                    If Not Entry.Slugs.ContainsKey(Source) Then Continue For
                    If Entry.ChineseName IsNot Nothing Then
                        Yield New SearchEntry(Of WikiEntry) With {.Item = Entry, .SearchSource = New List(Of SearchSource) From {
                            New SearchSource(StrConv(Entry.ChineseName.BeforeFirst(" ("), VbStrConv.SimplifiedChinese).Split("/"c, True), 1), 'Some mods have aliases
                            New SearchSource(StrConv(Entry.ChineseName.AfterFirst(" ("), VbStrConv.SimplifiedChinese) & Entry.Slugs(Source), 0.5)
                        }}
                    Else
                        Yield New SearchEntry(Of WikiEntry) With {.Item = Entry, .SearchSource = New List(Of SearchSource) From {
                            New SearchSource(Entry.Slugs(Source), 0.5)
                        }}
                    End If
                Next
            End Function
            'CurseForge
            If Request.Sources.HasFlagF(ResourcePlatforms.CurseForge) Then
                'Database search
                Static CurseForgeSearchEntries As List(Of SearchEntry(Of WikiEntry)) = GetSearchEntries(ResourcePlatforms.CurseForge).ToList()
                Dim CurseForgeSearchResults = Search(CurseForgeSearchEntries, RawSearchText, 100, 0.25)
                If CurseForgeSearchResults.Any Then
                    'Pick the target (CurseForge requires every word to match, so only one mod can be searched for)
                    Dim CurseForgeTarget =
                        If(CurseForgeSearchResults.First.AbsoluteRight,
                            CurseForgeSearchResults.Where(Function(s) s.AbsoluteRight).ToList, 'Prefer all exact matches
                            CurseForgeSearchResults.MaxByAll(Function(s) s.Similarity)). 'Then all with the highest similarity
                        MaxBy(Function(s) s.Item.Popularity) 'Then pick the most popular one among them
                    'Post-processing
                    CurseForgeAltSearchText = ExtractWords(CurseForgeTarget, ResourcePlatforms.CurseForge).Join(" ")
                    Logger.Warn($"中文搜索关键词（CurseForge）：{CurseForgeAltSearchText}")
                End If
            End If
            'Modrinth
            If Request.Sources.HasFlagF(ResourcePlatforms.Modrinth) Then
                'Database search
                Static ModrinthSearchEntries As List(Of SearchEntry(Of WikiEntry)) = GetSearchEntries(ResourcePlatforms.Modrinth).ToList()
                Dim ModrinthSearchResults = Search(ModrinthSearchEntries, RawSearchText, 100, 0.25)
                If ModrinthSearchResults.Any Then
                    'Tokenize
                    Dim WordWeights As New Dictionary(Of String, Double) 'Each word and its weight
                    For Each Result In ModrinthSearchResults
                        For Each Word In ExtractWords(Result, ResourcePlatforms.Modrinth)
                            If Not WordWeights.ContainsKey(Word) Then WordWeights.Add(Word, 0)
                            Dim Similarity = If(Result.SearchSource.Any(Function(s) s.Aliases.Contains(RawSearchText)), 1000, Result.Similarity) 'An exact match counts as 1000
                            WordWeights(Word) += Similarity * Result.Item.Popularity 'Weight += similarity * popularity
                        Next
                    Next
                    ModrinthAltSearchText = WordWeights.MaxBy(Function(w) w.Value).Key
                    Logger.Warn($"中文搜索关键词（Modrinth）：{ModrinthAltSearchText}")
                    'Request the project directly
                    ModrinthSlugs = ModrinthSearchResults.Take(100).Select(Function(r) r.Item.Slugs(ResourcePlatforms.Modrinth)).ToList
                End If
            End If
            'Finish
            If String.IsNullOrEmpty(CurseForgeAltSearchText) AndAlso String.IsNullOrEmpty(ModrinthAltSearchText) AndAlso Not ModrinthSlugs.Any Then
                Throw New Exception(MSG_NO_CHINESE_RESULT)
            End If
        End If
        Task.Progress = 0.05

#End Region

        Dim RealResults As New List(Of ResourceProject)
NextPage:
        Dim RawResults As New List(Of ResourceProject)

#Region "Get result lists from CurseForge and Modrinth, stored in RawResults"

        Dim WorkThreads As New List(Of Thread)
        Dim Errors As New ConcurrentBag(Of (Ex As Exception, Source As ResourcePlatforms))

        'On 1.14-, some old mods do not declare supported loaders, so filtering by Forge would miss them
        'So the loader is not filtered in the request, and non-Forge mods are filtered out from the results instead
        Dim IgnoreModLoaderFilter = Request.ModLoaders = ModLoaders.Forge AndAlso McVersion.VersionToDrop(Request.GameVersion) < 140
        Dim SearchResults As New ConcurrentBag(Of ResourceProject)
        Try

            'CurseForge search
            If Request.Sources.HasFlagF(ResourcePlatforms.CurseForge) AndAlso
               Not (Storage.CurseForgeTotal > -1 AndAlso Storage.CurseForgeTotal <= Storage.CurseForgeOffset) AndAlso 'Not enough remaining undisplayed results
               (Not IsChineseSearch OrElse (IsChineseSearch AndAlso Not String.IsNullOrEmpty(CurseForgeAltSearchText))) Then 'For Chinese search, only continue if there is a corresponding search keyword
                WorkThreads.Add(RunInNewThread(
                Sub()
                    Try
                        'Get the project list
                        Dim CurseForgeUrl As String = Request.GetCurseForgeAddress(If(CurseForgeAltSearchText, RawSearchText), IgnoreModLoaderFilter)
                        Logger.Info($"开始 CurseForge 搜索：{CurseForgeUrl}")
                        Dim RequestResult As JObject = DlModRequest(CurseForgeUrl)
                        Dim ProjectList As New List(Of ResourceProject)
                        For Each JsonEntry As JObject In RequestResult("data")
                            Dim Project As New ResourceProject(JsonEntry)
                            If Request.Type = ResourceTypes.ResourcePack AndAlso Project.Tags.Contains("数据包") Then Continue For 'CurseForge classifies some data packs as resource packs
                            If Request.Type <> ResourceTypes.Any AndAlso Not Project.Types.HasFlagF(Request.Type) Then Continue For 'Filter out results whose section does not match (#8265)
                            ProjectList.Add(Project)
                        Next
                        'Update results
                        ProjectList.ForEach(Sub(p) SearchResults.Add(p))
                        Storage.CurseForgeOffset += RequestResult("data").Count
                        Storage.CurseForgeTotal = RequestResult("pagination")("totalCount").ToObject(Of Integer)
                        Logger.Info($"从 CurseForge 搜索到了 {ProjectList.Count} 个工程（总计已获取 {Storage.CurseForgeOffset} 个，共 {Storage.CurseForgeTotal} 个）")
                    Catch ex As Exception
                        Logger.Warn(ex, "CurseForge 搜索失败")
                        Storage.CurseForgeTotal = -1
                        Errors.Add((ex, ResourcePlatforms.CurseForge))
                    End Try
                    If Task.Progress < 0.75 Then Task.Progress += 0.25 'Multiple pages may be loaded repeatedly, so it cannot be given directly
                End Sub, "CurseForge Search"))
            End If

            'Modrinth search
            If Request.Sources.HasFlagF(ResourcePlatforms.Modrinth) AndAlso
               Not (Storage.ModrinthTotal > -1 AndAlso Storage.ModrinthTotal <= Storage.ModrinthOffset) AndAlso 'Not enough remaining undisplayed results
               (Not IsChineseSearch OrElse (IsChineseSearch AndAlso Not String.IsNullOrEmpty(ModrinthAltSearchText))) Then 'For Chinese search, only continue if there is a corresponding search keyword
                WorkThreads.Add(RunInNewThread(
                Sub()
                    Try
                        Dim ModrinthUrl As String = Request.GetModrinthAddress(If(ModrinthAltSearchText, RawSearchText), IgnoreModLoaderFilter)
                        Logger.Info($"开始 Modrinth 搜索：{ModrinthUrl}")
                        Dim RequestResult As JObject = DlModRequest(ModrinthUrl)
                        Dim ProjectList As New List(Of ResourceProject)
                        For Each JsonEntry As JObject In RequestResult("hits")
                            ProjectList.Add(New ResourceProject(JsonEntry))
                        Next
                        'Update results
                        ProjectList.ForEach(Sub(p) SearchResults.Add(p))
                        Storage.ModrinthOffset += RequestResult("hits").Count
                        Storage.ModrinthTotal = RequestResult("total_hits").ToObject(Of Integer)
                        Logger.Info($"从 Modrinth 搜索到了 {ProjectList.Count} 个工程（总计已获取 {Storage.ModrinthOffset} 个，共 {Storage.ModrinthTotal} 个）")
                    Catch ex As Exception
                        Logger.Warn(ex, "Modrinth 搜索失败")
                        Storage.ModrinthTotal = -1
                        Errors.Add((ex, ResourcePlatforms.Modrinth))
                    End Try
                    If Task.Progress < 0.75 Then Task.Progress += 0.25 'Multiple pages may be loaded repeatedly, so it cannot be given directly
                End Sub, "Modrinth Search"))
            End If

            'Get projects directly from Modrinth
            If Request.Sources.HasFlagF(ResourcePlatforms.Modrinth) AndAlso
               Not (Storage.ModrinthTotal > -1 AndAlso Storage.ModrinthTotal <= Storage.ModrinthOffset) AndAlso 'Not enough remaining undisplayed results
               ModrinthSlugs.Any Then 'There is a slug to fetch directly
                WorkThreads.Add(RunInNewThread(
                Sub()
                    Try
                        Dim ModrinthUrl As String = $"https://api.modrinth.com/v2/projects?ids=[""{ModrinthSlugs.Join(""",""")}""]"
                        Logger.Info($"开始 Modrinth 直接获取：{ModrinthUrl}")
                        Dim ProjectList As New List(Of ResourceProject)
                        For Each JsonEntry As JObject In DlModRequest(ModrinthUrl)
                            Dim Project As New ResourceProject(JsonEntry)
                            'Apply filters
                            If Request.Type <> ResourceTypes.Any AndAlso Not Project.Types.HasFlagF(Request.Type) Then Continue For
                            If Not String.IsNullOrEmpty(Request.Tag) AndAlso
                                Not JsonEntry("categories").Any(Function(c) c.ToString = Request.Tag.AfterLast("/")) Then Continue For 'Project.Tags have already been converted to Chinese, so we can only check the json
                            If Request.ModLoaders <> ModLoaders.None AndAlso Not IgnoreModLoaderFilter AndAlso
                                Not Project.ModLoaders.Flags.Intersect(Request.ModLoaders.Flags).Any() Then Continue For
                            If Not String.IsNullOrEmpty(Request.GameVersion) AndAlso
                                Not Project.UnsafeGameVersions.Any(Function(d) d = Request.GameVersion) Then Continue For
                            ProjectList.Add(Project)
                        Next
                        If ProjectList.Any AndAlso Request.ModLoaders <> ModLoaders.None AndAlso Not IgnoreModLoaderFilter AndAlso
                                Not String.IsNullOrEmpty(Request.GameVersion) Then
                            Dim ProjectIds = ProjectList.Select(Function(p) p.Id).Join(""",""project_id:")
                            Dim Loaders = Request.ModLoaders.Flags.Select(Function(f) f.ToString.Lower).Join(""",""categories:")
                            Dim FilterUrl = $"https://api.modrinth.com/v2/search?limit={ProjectList.Count}&facets=" &
                                $"[[""project_id:{ProjectIds}""],[""categories:{Loaders}""],[""versions:{Request.GameVersion}""]]"
                            Dim MatchedIds = DlModRequest(FilterUrl)("hits").Select(Function(h) h("project_id").ToString).ToList
                            ProjectList.KeepIf(Function(p) MatchedIds.Contains(p.Id))
                        End If
                        'Update results
                        ProjectList.ForEach(Sub(p) SearchResults.Add(p))
                        Logger.Info($"从 Modrinth 直接获取到了 {ProjectList.Count} 个工程")
                        ModrinthSlugs.Clear() 'Prevent fetching again on retry / loading the next page
                    Catch ex As Exception
                        Logger.Warn(ex, "Modrinth 直接获取失败")
                        Errors.Add((ex, ResourcePlatforms.Modrinth))
                    End Try
                    If Task.Progress < 0.75 Then Task.Progress += 0.25 'Multiple pages may be loaded repeatedly, so it cannot be given directly
                End Sub, "Modrinth Get"))
            End If

            'Wait for the threads to finish
            For Each Thread In WorkThreads
                Thread.Join()
                If Task.IsCanceled Then Return 'Finally is triggered automatically to clean up the threads
            Next
            RawResults = New List(Of ResourceProject)(SearchResults)

            'Keep only Forge-compatible mods, or mods in old versions that declare no mod loader
            If IgnoreModLoaderFilter Then
                RawResults.KeepIf(Function(p) p.ModLoaders = ModLoaders.None OrElse p.ModLoaders.HasFlagF(ModLoaders.Forge))
            End If

            'Make sure there are results
            Storage.ErrorMessage = Nothing
            If Not RawResults.Any() Then
                If Errors.Any() Then
                    Dim FirstError = Errors.First
                    If FirstError.Source = ResourcePlatforms.CurseForge Then
                        Throw New Exception(CurseForgeAccess.GetFailureHint(FirstError.Ex), FirstError.Ex)
                    End If
                    Throw FirstError.Ex
                Else
                    If IsChineseSearch AndAlso Not (Request.Type = ResourceTypes.Mod OrElse Request.Type = ResourceTypes.DataPack) Then
                        Throw New Exception(MSG_NO_CHINESE_RESULT)
                    ElseIf Request.Sources = ResourcePlatforms.CurseForge AndAlso Request.Tag.StartsWithF("/") Then
                        Throw New Exception("CurseForge 不兼容所选的类型")
                    ElseIf Request.Sources = ResourcePlatforms.Modrinth AndAlso Request.Tag.EndsWithF("/") Then
                        Throw New Exception("Modrinth 不兼容所选的类型")
                    Else
                        Throw New Exception("没有搜索结果")
                    End If
                End If
            ElseIf Errors.Any() Then
                'There are results but also errors
                If Errors.Any(Function(e) e.Source = ResourcePlatforms.CurseForge) Then
                    Storage.ErrorMessage = CurseForgeAccess.GetFailureHint(Errors.First(Function(e) e.Source = ResourcePlatforms.CurseForge).Ex) & vbCrLf & "目前仅显示来自 Modrinth 的内容，搜索结果可能不全。"
                Else
                    Storage.ErrorMessage = $"无法连接到 Modrinth，所以目前仅显示了来自 CurseForge 的内容，搜索结果可能不全。{vbCrLf}请稍后再试，或使用 VPN 改善网络环境。"
                End If
            End If

        Finally
            For Each Thread In WorkThreads
                If Thread.IsAlive Then Thread.Interrupt()
            Next
        End Try

#End Region

#Region "Extract unique items, stored in RealResults"

        'Put CurseForge before Modrinth so that a different load completion order does not change the ranking
        RawResults = RawResults.Where(Function(x) x.Platform = ResourcePlatforms.CurseForge).Concat(RawResults.Where(Function(x) x.Platform <> ResourcePlatforms.CurseForge)).ToList
        'Deduplicate
        RawResults = RawResults.Distinct(Function(a, b) a.IsLike(b)).ToList
        'Deduplicate against existing content
        RawResults = RawResults.Where(Function(r) Not RealResults.Any(Function(b) r.IsLike(b)) AndAlso
                                                  Not Storage.Results.Any(Function(b) r.IsLike(b))).ToList
        'Add to the list
        RealResults.AddRange(RawResults)
        Logger.Info($"去重、筛选后累计新增结果 {RealResults.Count} 个（目前已有结果 {Storage.Results.Count} 个）")

#End Region

#Region "Check the result count; if insufficient and possible, load the next page"

        If RealResults.Count + Storage.Results.Count < Request.TargetResultCount Then
            Logger.Info($"总结果数需求最少 {Request.TargetResultCount} 个，仅获得了 {RealResults.Count + Storage.Results.Count} 个")
            If Request.CanContinue AndAlso Not Errors.Any Then 'Do not retry if any source failed, as retrying could cause an infinite loop
                Logger.Info("将继续尝试加载下一页")
                GoTo NextPage
            Else
                Logger.Info("无法继续加载，将强制结束")
            End If
        End If

#End Region

#Region "Sort and Add Results"

        Dim Scores As New Dictionary(Of ResourceProject, Double) 'Sort score
        Dim GetDownloadCountMult =
        Function(Project As ResourceProject) As Double
            Select Case Request.Type
                Case ResourceTypes.Mod, ResourceTypes.ModPack
                    Return If(Project.Platform = ResourcePlatforms.CurseForge, 1, 5)
                Case ResourceTypes.DataPack
                    Return If(Project.Platform = ResourcePlatforms.CurseForge, 10, 1)
                Case ResourceTypes.ResourcePack, ResourceTypes.Shader
                    Return If(Project.Platform = ResourcePlatforms.CurseForge, 1, 4)
                Case Else
                    Return 1
            End Select
        End Function
        If String.IsNullOrEmpty(RawSearchText) Then
            'Without search text, sort results by download count
            For Each Result As ResourceProject In RealResults
                Scores(Result) = Result.DownloadCount * GetDownloadCountMult(Result)
            Next
        Else
            'With search text, sort results by relevance
            'Sort score = relative search similarity (1) + download weight (logarithmic, 1 at one billion) + has a Chinese name (0.2)
            Dim SearchEntries As New List(Of SearchEntry(Of ResourceProject))
            For Each Result As ResourceProject In RealResults
                Scores(Result) = If(Result.WikiId > 0, 0.2, 0) +
                           Math.Log10(Math.Max(Result.DownloadCount, 1) * GetDownloadCountMult(Result)) / 9
                SearchEntries.Add(New SearchEntry(Of ResourceProject) With {.Item = Result, .SearchSource = New List(Of SearchSource) From {
                    New SearchSource(If(IsChineseSearch, Result.TranslatedName, Result.RawName).Split("/"c, True), 1),
                    New SearchSource(Result.Description, 0.05)}})
            Next
            Dim SearchResult = Search(SearchEntries, RawSearchText, 10000, -1)
            For Each OneResult In SearchResult
                Scores(OneResult.Item) +=
                    If(OneResult.AbsoluteRight, 10, OneResult.Similarity) /
                    If(SearchResult.First.AbsoluteRight, 10, SearchResult.First.Similarity) 'Similarity score, up to 1
            Next
        End If
        'Add results based on the sort score
        If Task.IsCanceled Then Throw New OperationCanceledException '#8246
        Storage.Results.AddRange(
            Scores.OrderByDescending(Function(s) s.Value).Select(Function(r) r.Key))

#End Region

    End Sub

End Class
