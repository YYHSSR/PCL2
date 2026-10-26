Public Class PageInstanceMod
    Implements IRefreshable

#Region "Initialization"

    Private IsLoad As Boolean = False
    Public Sub PageInstanceMod_Loaded() Handles Me.Loaded

        If FrmMain.PageLast.Page <> FormMain.PageType.ResourceDetail Then PanBack.ScrollToHome()
        AniControlEnabled += 1
        SelectedMods.Clear()
        ReloadModList()
        ChangeAllSelected(False)
        AniControlEnabled -= 1

        'One-time load
        If IsLoad Then Return
        IsLoad = True

        AddHandler FrmMain.KeyDown, AddressOf FrmMain_KeyDown
        'Tweak button margins (this cannot be set from XAML)
        For Each Btn As MyRadioButton In PanFilter.Children
            Btn.LabText.Margin = New Thickness(-2, 0, 8, 0)
        Next

    End Sub
    ''' <summary>
    ''' Refresh the mod list.
    ''' </summary>
    Public Sub ReloadModList(Optional ForceReload As Boolean = False)
        If Not LoaderRun(If(ForceReload, LoaderFolderRunType.ForceRun, LoaderFolderRunType.RunOnUpdated)) Then Return
        Logger.Info("已刷新 Mod 列表")
        Filter = FilterType.All
        PanBack.ScrollToHome()
        SearchBox.Text = ""
    End Sub
    'Force refresh
    Private Sub RefreshSelf() Implements IRefreshable.Refresh
        Refresh()
    End Sub
    Public Shared Sub Refresh()
        Try
            ResourceProject.Cache.Clear()
            ResourceVersion.ProjectFilesCache.Clear()
            FileUtils.Delete(PathTemp & "Cache\LocalMod.json")
            Logger.Info("由于点击刷新按钮，清理本地 Mod 信息缓存")
        Catch ex As Exception
            Logger.Warn(ex, "强制刷新时清理本地 Mod 信息缓存失败")
        End Try
        If FrmInstanceMod IsNot Nothing Then FrmInstanceMod.ReloadModList(True) 'No Else: nothing to refresh if the page is not loaded yet
        FrmInstanceLeft.ItemMod.Checked = True
        Hint("正在刷新……", Log:=False)
    End Sub

    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanAllBack, Nothing, LocalResourceLoader, AddressOf LoadUIFromLoaderOutput, AutoRun:=False)
    End Sub
    Private Sub Load_Click(sender As Object, e As MouseButtonEventArgs) Handles Load.Click
        If LocalResourceLoader.State = LoadState.Failed Then LoaderRun(LoaderFolderRunType.ForceRun)
    End Sub
    Public Function LoaderRun(Type As LoaderFolderRunType) As Boolean
        Return LoaderFolderRun(LocalResourceLoader, PageInstanceLeft.Instance.PathIndie & "mods\", Type)
    End Function

#End Region

#Region "Build UI"

    ''' <summary>
    ''' Loaded mod UI cache. Not guaranteed to match display order. Key is the mod's RawFileName.
    ''' </summary>
    Public ModItems As New ConcurrentDictionary(Of String, MyLocalModItem)
    ''' <summary>
    ''' Load the loader's mod list into the UI.
    ''' </summary>
    Private Sub LoadUIFromLoaderOutput()
        Try
            'Choose which page to show
            If LocalResourceLoader.Output.Any() Then
                PanBack.Visibility = Visibility.Visible
                PanEmpty.Visibility = Visibility.Collapsed
            Else
                PanEmpty.Visibility = Visibility.Visible
                PanBack.Visibility = Visibility.Collapsed
                Return
            End If
            'Update the cache
            ModItems.Clear()
            For Each ModEntity As LocalResourceFile In LocalResourceLoader.Output
                ModItems(ModEntity.EnabledName) = McModListItem(ModEntity)
            Next
            'Show the result
            Filter = FilterType.All
            SearchBox.Text = "" 'This refreshes the results, so it must run after ModItems is updated; see the video in #3124
            RefreshUI()
        Catch ex As Exception
            Logger.Error(ex, "加载 Mod 列表 UI 失败")
        End Try
    End Sub
    Private Function McModListItem(Entry As LocalResourceFile) As MyLocalModItem
        AniControlEnabled += 1
        Dim NewItem As New MyLocalModItem With {.SnapsToDevicePixels = True, .Entry = Entry,
            .ButtonHandler = AddressOf McModContent, .Checked = SelectedMods.Contains(Entry.EnabledName)}
        AddHandler Entry.OnUpdate, Sub() RunInUi(AddressOf NewItem.Refresh)
        AniControlEnabled -= 1
        Return NewItem
    End Function
    Private Sub McModContent(sender As MyLocalModItem, e As EventArgs)
        'Click events
        AddHandler sender.Changed, AddressOf CheckChanged
        AddHandler sender.Click, Sub(ss As MyLocalModItem, ee As EventArgs) ss.Checked = Not ss.Checked
        'Icon buttons
        Dim BtnOpen As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonOpen, .Tag = sender}
        BtnOpen.ToolTip = "打开文件位置"
        AddHandler BtnOpen.Click, AddressOf Open_Click
        Dim BtnCont As New MyIconButton With {.LogoScale = 1, .Logo = Logo.IconButtonInfo, .Tag = sender}
        BtnCont.ToolTip = "详情"
        AddHandler BtnCont.Click, AddressOf Info_Click
        AddHandler sender.MouseRightButtonUp, AddressOf Info_Click
        Dim BtnDelete As New MyIconButton With {.LogoScale = 1, .Logo = Logo.IconButtonDelete, .Tag = sender}
        BtnDelete.ToolTip = "删除"
        AddHandler BtnDelete.Click, AddressOf Delete_Click
        Dim BtnED As New MyIconButton With {.LogoScale = 1, .Logo = If(sender.Entry.IsEnabled, Logo.IconButtonStop, Logo.IconButtonCheck), .Tag = sender}
        BtnED.ToolTip = If(sender.Entry.IsEnabled, "禁用", "启用")
        AddHandler BtnED.Click, AddressOf ED_Click
        sender.Buttons = {BtnCont, BtnOpen, BtnED, BtnDelete}
    End Sub

    ''' <summary>
    ''' Refresh the whole UI.
    ''' </summary>
    Public Sub RefreshUI()
        If PanList Is Nothing Then Return
        Dim ShowingMods = If(IsSearching, SearchResult, If(LocalResourceLoader.Output, New List(Of LocalResourceFile))).Where(Function(m) CanPassFilter(m)).ToList
        'Rebuild the list
        AniControlEnabled += 1
        If ShowingMods.Any() Then
            PanList.Visibility = Visibility.Visible
            PanList.Children.Clear()
            For Each TargetMod In ShowingMods
                Dim Item As MyLocalModItem = Nothing
                If Not ModItems.TryGetValue(TargetMod.EnabledName, Item) Then Continue For
                Item.Checked = SelectedMods.Contains(TargetMod.EnabledName) 'Update the checked state
                PanList.Children.Add(Item)
            Next
        Else
            PanList.Visibility = Visibility.Collapsed
        End If
        AniControlEnabled -= 1
        SelectedMods = SelectedMods.Where(Function(m) ShowingMods.Any(Function(s) s.EnabledName = m)).ToList 'Drop mods that are no longer shown
        RefreshBars()
    End Sub

    ''' <summary>
    ''' Refresh the top and bottom bars.
    ''' </summary>
    Public Sub RefreshBars()

        '-----------------
        ' Top bar
        '-----------------

        'Counts
        Dim AnyCount As Integer = 0
        Dim EnabledCount As Integer = 0
        Dim UpdateCount As Integer = 0
        For Each ModItem In If(IsSearching, SearchResult, If(LocalResourceLoader.Output, New List(Of LocalResourceFile)))
            AnyCount += 1
            If ModItem.HasUpdate Then UpdateCount += 1
            If ModItem.IsEnabled Then EnabledCount += 1
        Next
        Dim DisabledCount As Integer = AnyCount - EnabledCount
        'Labels
        BtnFilterAll.Text = If(IsSearching, "搜索结果", "全部") & $" ({AnyCount})"
        BtnFilterCanUpdate.Text = $"可更新 ({UpdateCount})"
        BtnFilterCanUpdate.Visibility = If(Filter = FilterType.CanUpdate OrElse UpdateCount > 0, Visibility.Visible, Visibility.Collapsed)
        BtnFilterEnabled.Text = $"启用 ({EnabledCount})"
        BtnFilterEnabled.Visibility = If(Filter = FilterType.Enabled OrElse (EnabledCount > 0 AndAlso EnabledCount < AnyCount), Visibility.Visible, Visibility.Collapsed)
        BtnFilterDisabled.Text = $"禁用 ({DisabledCount})"
        BtnFilterDisabled.Visibility = If(Filter = FilterType.Disabled OrElse DisabledCount > 0, Visibility.Visible, Visibility.Collapsed)

        '-----------------
        ' Bottom bar
        '-----------------

        'Counts
        Dim NewCount As Integer = SelectedMods.Count
        Dim Selected = NewCount > 0
        If Selected Then LabSelect.Text = $"已选择 {NewCount} 个文件" 'Do not update the number when clearing the selection
        'Which buttons are enabled
        If Selected Then
            Dim HasUpdate As Boolean = False
            Dim HasEnabled As Boolean = False
            Dim HasDisabled As Boolean = False
            For Each ModEntity In LocalResourceLoader.Output
                If Not SelectedMods.Contains(ModEntity.EnabledName) Then Continue For
                If ModEntity.HasUpdate Then HasUpdate = True
                If ModEntity.IsEnabled Then HasEnabled = True
                If Not ModEntity.IsEnabled Then HasDisabled = True
            Next
            BtnSelectDisable.IsEnabled = HasEnabled
            BtnSelectEnable.IsEnabled = HasDisabled
            BtnSelectUpdate.IsEnabled = HasUpdate
        End If
        'Update visibility
        If AniControlEnabled = 0 Then
            PanListBack.Margin = New Thickness(0, 0, 0, If(Selected, 95, 15))
            If Selected Then
                'Play the show/bounce animation only when the count increases
                If BottomBarShownCount >= NewCount Then
                    BottomBarShownCount = NewCount
                    Return
                Else
                    BottomBarShownCount = NewCount
                End If
                'Show/bounce animation
                CardSelect.Visibility = Visibility.Visible
                AniStart({
                    AaOpacity(CardSelect, 1 - CardSelect.Opacity, 60),
                    AaTranslateY(CardSelect, -27 - TransSelect.Y, 120, Ease:=New AniEaseOutFluent(AniEasePower.Weak)),
                    AaTranslateY(CardSelect, 3, 150, 120, Ease:=New AniEaseInoutFluent(AniEasePower.Weak)),
                    AaTranslateY(CardSelect, -1, 90, 270, Ease:=New AniEaseInoutFluent(AniEasePower.Weak))
                }, "Mod Sidebar")
            Else
                'Do not replay the hide animation
                If BottomBarShownCount = 0 Then Return
                BottomBarShownCount = 0
                'Hide animation
                AniStart({
                    AaOpacity(CardSelect, -CardSelect.Opacity, 90),
                    AaTranslateY(CardSelect, -10 - TransSelect.Y, 90, Ease:=New AniEaseInFluent(AniEasePower.Weak)),
                    AaCode(Sub() CardSelect.Visibility = Visibility.Collapsed, After:=True)
                }, "Mod Sidebar")
            End If
        Else
            AniStop("Mod Sidebar")
            BottomBarShownCount = NewCount
            If Selected Then
                CardSelect.Visibility = Visibility.Visible
                CardSelect.Opacity = 1
                TransSelect.Y = -25
            Else
                CardSelect.Visibility = Visibility.Collapsed
                CardSelect.Opacity = 0
                TransSelect.Y = -10
            End If
        End If
    End Sub
    Private BottomBarShownCount As Integer = 0

#End Region

#Region "Management"

    ''' <summary>
    ''' Open the mods folder.
    ''' </summary>
    Private Sub BtnManageOpen_Click(sender As Object, e As EventArgs) Handles BtnManageOpen.Click, BtnHintOpen.Click
        Try
            DirectoryUtils.Create(PageInstanceLeft.Instance.PathIndie & "mods\")
            OpenExplorer(PageInstanceLeft.Instance.PathIndie & "mods\")
        Catch ex As Exception
            Logger.Error(ex, "打开 Mods 文件夹失败", LogBehavior.Alert)
        End Try
    End Sub

    ''' <summary>
    ''' Select all.
    ''' </summary>
    Private Sub BtnManageSelectAll_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnManageSelectAll.Click
        ChangeAllSelected(SelectedMods.Count < PanList.Children.Count)
    End Sub

    ''' <summary>
    ''' Install mods.
    ''' </summary>
    Private Sub BtnManageInstall_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnManageInstall.Click, BtnHintInstall.Click
        Dim FileList = Dialogs.SelectFile("选择要安装的 Mod", True, filter:={({"jar", "litemod", "disabled", "old"}, "Mod 文件")})
        If Not FileList.Any Then Return
        InstallMods(FileList)
    End Sub
    ''' <summary>
    ''' Try to install mods.
    ''' Returns whether an input file is a mod file. Used only to decide drag-and-drop behavior.
    ''' </summary>
    Public Shared Function InstallMods(FilePathList As IEnumerable(Of String)) As Boolean
        Dim Extension As String = PathUtils.GetExtension(FilePathList.First)
        'Check the file extension
        If Not {"jar", "litemod", "disabled", "old"}.Any(Function(t) t = Extension) Then Return False
        Logger.Info("文件为 jar/litemod 格式，尝试作为 Mod 安装")
        'Recycle Bin files have broken names
        If FilePathList.First.Contains(":\$RECYCLE.BIN\") Then
            Hint("请先将文件从回收站还原，再尝试安装！", HintType.Red)
            Return True
        End If
        'Get and check the target instance
        Dim Instance As McInstance = McInstanceSelected
        If FrmMain.PageCurrent = FormMain.PageType.InstanceSetup Then Instance = PageInstanceLeft.Instance
        If FrmMain.PageCurrent = FormMain.PageType.InstanceSelect OrElse Instance Is Nothing OrElse Not Instance.Modable Then
            'Choosing an instance, or this instance cannot install mods
            Hint("若要安装 Mod，请先选择一个可以安装 Mod 的版本！")
        ElseIf Not (FrmMain.PageCurrent = FormMain.PageType.InstanceSetup AndAlso FrmMain.PageCurrentSub = FormMain.PageSubType.InstanceMod) Then
            'Not on the mod management page
            If MyMsgBox($"是否要将这{If(FilePathList.IsSingle, "个", "些")}文件作为 Mod 安装到 {Instance.Name}？", "Mod 安装确认", "确定", "取消") = 1 Then GoTo Install
        Else
            'On the mod management page
Install:
            Try
                For Each ModFile In FilePathList
                    Dim NewFileName = (New LocalResourceFile(ModFile)).EnabledName
                    If Not NewFileName.Contains(".") Then NewFileName += ".jar" '#4227
                    NewFileName = Instance.PathIndie & "mods\" & NewFileName
                    If PathUtils.IsSame(ModFile, NewFileName) Then Continue For 'Skip the same file so installing it does not delete it
                    FileUtils.Delete(NewFileName, True)
                    FileUtils.Copy(ModFile, NewFileName)
                Next
                If FilePathList.IsSingle Then
                    Hint($"已安装 {(New LocalResourceFile(FilePathList.First)).EnabledName}！", HintType.Green)
                Else
                    Hint($"已安装 {FilePathList.Count} 个 Mod！", HintType.Green)
                End If
                'Refresh the list
                If FrmMain.PageCurrent = FormMain.PageType.InstanceSetup AndAlso FrmMain.PageCurrentSub = FormMain.PageSubType.InstanceMod Then
                    LoaderFolderRun(LocalResourceLoader, Instance.PathIndie & "mods\", LoaderFolderRunType.ForceRun)
                End If
            Catch ex As Exception
                Logger.Error(ex, "复制 Mod 文件失败", LogBehavior.Alert)
            End Try
        End If
        Return True
    End Function

    ''' <summary>
    ''' Download mods.
    ''' </summary>
    Private Sub BtnManageDownload_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnManageDownload.Click, BtnHintDownload.Click
        'Preset the current instance and search text
        PageResource.TargetInstance = PageInstanceLeft.Instance
        PageResource.TargetName = SearchBox.Text
        FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadMod)
    End Sub

#End Region

#Region "Selection"

    'Paths of selected mods (without .disabled and .old)
    Public SelectedMods As New List(Of String)

    'Toggle one item
    Public Sub CheckChanged(sender As MyLocalModItem, e As RouteEventArgs)
        If AniControlEnabled <> 0 Then Return
        'Update the selection
        Dim SelectedKey As String = sender.Entry.EnabledName
        If sender.Checked Then
            If Not SelectedMods.Contains(SelectedKey) Then SelectedMods.Add(SelectedKey)
        Else
            SelectedMods.Remove(SelectedKey)
        End If
        RefreshBars()
    End Sub

    'Toggle every item
    Private Sub ChangeAllSelected(Value As Boolean)
        AniControlEnabled += 1
        SelectedMods.Clear()
        For Each Item As MyLocalModItem In ModItems.Values
            '#4992: a mod may no longer match the filter but stay listed right after a state change, so decide from the list UI rather than the filter
            Dim ShouldSelected As Boolean = Value AndAlso PanList.Children.Contains(Item)
            Item.Checked = ShouldSelected
            If ShouldSelected Then SelectedMods.Add(Item.Entry.EnabledName)
        Next
        AniControlEnabled -= 1
        RefreshBars()
    End Sub
    Private Sub UnselectedAllWithAnimation() Handles Load.StateChanged, Me.PageExit
        Dim CacheAniControlEnabled = AniControlEnabled
        AniControlEnabled = 0
        ChangeAllSelected(False)
        AniControlEnabled += CacheAniControlEnabled
    End Sub
    Private Sub FrmMain_KeyDown(sender As Object, e As KeyEventArgs) 'Listening to our own events would miss keys until a right-hand control is clicked (#4311)
        If FrmMain.PageRight IsNot Me Then Return
        If My.Computer.Keyboard.CtrlKeyDown AndAlso e.Key = Key.A Then ChangeAllSelected(True)
    End Sub
    Private Sub SearchBox_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles SearchBox.PreviewKeyDown
        'The search box swallows Ctrl+A and blocks select-all, so move focus when Ctrl+A is pressed
        If SearchBox.Text.Any Then Return
        If My.Computer.Keyboard.CtrlKeyDown AndAlso e.Key = Key.A Then PanBack.Focus()
    End Sub

#End Region

#Region "Filters"

    Private _Filter As FilterType = FilterType.All
    Public Property Filter As FilterType
        Get
            Return _Filter
        End Get
        Set(value As FilterType)
            If _Filter = value Then Return
            _Filter = value
            Select Case value
                Case FilterType.All
                    BtnFilterAll.Checked = True
                Case FilterType.Enabled
                    BtnFilterEnabled.Checked = True
                Case FilterType.Disabled
                    BtnFilterDisabled.Checked = True
                Case Else
                    BtnFilterCanUpdate.Checked = True
            End Select
            RefreshUI()
        End Set
    End Property
    Public Enum FilterType As Integer
        All = 0
        Enabled = 1
        Disabled = 2
        CanUpdate = 3
    End Enum

    ''' <summary>
    ''' Whether this mod matches the current filter.
    ''' </summary>
    Private Function CanPassFilter(CheckingMod As LocalResourceFile) As Boolean
        Select Case Filter
            Case FilterType.All
                Return True
            Case FilterType.Enabled
                Return CheckingMod.IsEnabled
            Case FilterType.Disabled
                Return Not CheckingMod.IsEnabled
            Case FilterType.CanUpdate
                Return CheckingMod.HasUpdate
            Case Else
                Return False
        End Select
    End Function

    'Changing the filter by clicking an option
    Private Sub ChangeFilter(sender As MyRadioButton, raiseByMouse As Boolean) Handles BtnFilterAll.Check, BtnFilterCanUpdate.Check, BtnFilterDisabled.Check, BtnFilterEnabled.Check
        Filter = sender.Tag
        RefreshUI()
    End Sub

#End Region

#Region "Bottom bar"

    'Enable / disable
    Private Sub BtnSelectED_Click(sender As MyIconTextButton, e As RouteEventArgs) Handles BtnSelectEnable.Click, BtnSelectDisable.Click
        EDMods(LocalResourceLoader.Output.Where(Function(m) SelectedMods.Contains(m.EnabledName)),
               Not sender.Equals(BtnSelectDisable))
        ChangeAllSelected(False)
    End Sub
    Private Sub EDMods(ModList As IEnumerable(Of LocalResourceFile), IsEnable As Boolean)
        Dim IsSuccessful As Boolean = True
        For Each ModE In ModList.ToList
            Dim ModEntity = ModE 'Copy so the loop variable can be changed
            Dim NewPath As String = Nothing
            If ModEntity.IsEnabled AndAlso Not IsEnable Then
                'Disable
                NewPath = PathUtils.RemoveExtendedPrefix(ModEntity.File.FullName) & If(FileUtils.Exists(ModEntity.File.FullName & ".old"), ".old", ".disabled")
            ElseIf Not ModEntity.IsEnabled AndAlso IsEnable Then
                'Enable
                NewPath = ModEntity.EnabledFullName
            Else
                Continue For
            End If
            'Rename
            Try
                If FileUtils.Exists(NewPath) Then
                    If FileUtils.Exists(ModEntity.File.FullName) Then
                        'Both the enabled and disabled names exist
                        If CryptographyUtils.ComputeFileHash(ModEntity.File.FullName) <> CryptographyUtils.ComputeFileHash(NewPath) Then
                            MyMsgBox($"目前同时存在启用和禁用的两个 Mod 文件：{vbCrLf} - {PathUtils.GetLastPart(NewPath)}{vbCrLf} - {ModEntity.File.Name}{vbCrLf}{vbCrLf}注意，这两个文件的内容并不相同。{vbCrLf}在手动删除或重命名其中一个文件后，才能继续操作。", "存在文件冲突")
                            Continue For
                        End If
                    Else
                        'Already renamed
                        Logger.Warn("Mod 的状态已被切换")
                        Continue For
                    End If
                End If
                FileUtils.Move(ModEntity.File.FullName, NewPath)
            Catch ex As FileNotFoundException
                Logger.Error(ex, $"未找到需要重命名的 Mod（{If(ModEntity.File.FullName, "null")}）")
                ReloadModList(True)
                Return
            Catch ex As Exception
                Logger.Warn(ex, $"重命名 Mod 失败（{If(ModEntity.File.FullName, "null")}）")
                IsSuccessful = False
            End Try
            'Update the list in the loader
            Dim NewModEntity As New LocalResourceFile(NewPath)
            NewModEntity.FromJson(ModEntity.ToJson)
            If LocalResourceLoader.Output.Contains(ModEntity) Then
                Dim IndexOfLoader As Integer = LocalResourceLoader.Output.IndexOf(ModEntity)
                LocalResourceLoader.Output.RemoveAt(IndexOfLoader)
                LocalResourceLoader.Output.Insert(IndexOfLoader, NewModEntity)
            End If
            If SearchResult IsNot Nothing AndAlso SearchResult.Contains(ModEntity) Then '#4862
                Dim IndexOfResult As Integer = SearchResult.IndexOf(ModEntity)
                SearchResult.Remove(ModEntity)
                SearchResult.Insert(IndexOfResult, NewModEntity)
            End If
            'Update the list in the UI
            Dim NewItem As MyLocalModItem = McModListItem(NewModEntity)
            ModItems(ModEntity.EnabledName) = NewItem
            Dim IndexOfUi As Integer = PanList.Children.IndexOf(PanList.Children.OfType(Of MyLocalModItem).FirstOrDefault(Function(i) i.Entry Is ModEntity))
            If IndexOfUi = -1 Then Continue For 'The mod state was already switched, for an unknown reason
            PanList.Children.RemoveAt(IndexOfUi)
            PanList.Children.Insert(IndexOfUi, NewItem)
            NewItem.UpdateLayout() '#7861
        Next
        If IsSuccessful Then
            RefreshBars()
        Else
            Hint("由于文件被占用，Mod 的状态切换失败，请尝试关闭正在运行的游戏后再试！", HintType.Red)
            ReloadModList(True)
        End If
        LoaderRun(LoaderFolderRunType.UpdateOnly)
    End Sub

    'Update
    Private Sub BtnSelectUpdate_Click() Handles BtnSelectUpdate.Click
        Dim UpdateList As List(Of LocalResourceFile) = LocalResourceLoader.Output.Where(Function(m) SelectedMods.Contains(m.EnabledName) AndAlso m.HasUpdate).ToList()
        If Not UpdateList.Any() Then Return
        UpdateMods(UpdateList)
        ChangeAllSelected(False)
    End Sub
    ''' <summary>
    ''' Mods folder paths that currently have a mod update in progress.
    ''' </summary>
    Public Shared UpdatingInstanceModFolders As New List(Of String)
    Public Sub UpdateMods(ModList As IEnumerable(Of LocalResourceFile))
        'Warn before updating
        If Not Settings.Get(Of Boolean)("HintUpdateMod") OrElse ModList.Count >= 15 Then
            If MyMsgBox($"新版本 Mod 可能不兼容旧存档或者其他 Mod，这可能导致游戏崩溃，甚至永久损坏存档！{vbCrLf}如果你在游玩整合包，请千万不要自行更新 Mod！{vbCrLf}{vbCrLf}在更新前，请先备份存档，并检查 Mod 的更新日志。{vbCrLf}如果更新后出现问题，你也可以在回收站找回更新前的 Mod。", "Mod 更新警告", "我已了解风险，继续更新", "取消", IsWarn:=True) = 1 Then
                Settings.Set("HintUpdateMod", True)
            Else
                Return
            End If
        End If
        Try
            'Build the download info
            ModList = ModList.ToList() 'Copy so a refresh cannot break the iterator
            Dim FileList As New List(Of NetFile)
            Dim FileCopyList As New Dictionary(Of String, String)
            For Each Entry As LocalResourceFile In ModList
                Dim File As ResourceVersion = Entry.UpdateFile
                If Not File.DownloadAvailable Then Continue For
                'Decide the file name after the update
                Dim CurrentReplaceName = PathUtils.GetFileNameWithoutExtension(Entry.ProjectVersion.FileName)
                Dim NewestReplaceName = PathUtils.GetFileNameWithoutExtension(Entry.UpdateFile.FileName)
                Dim CurrentSegs = CurrentReplaceName.Split("-"c).ToList()
                Dim NewestSegs = NewestReplaceName.Split("-"c).ToList()
                Dim Shortened As Boolean = False
                Do While True 'Strip the shared prefix (do not strip every shared part: 1.2-forge-2 and 1.3-forge-3 would lose the middle forge and try to replace 1.2-2)
                    If Not CurrentSegs.Any() OrElse Not NewestSegs.Any() Then Exit Do
                    If CurrentSegs.First <> NewestSegs.First Then Exit Do
                    CurrentSegs.RemoveAt(0)
                    NewestSegs.RemoveAt(0)
                    Shortened = True
                Loop
                Do While True 'Strip the shared suffix
                    If Not CurrentSegs.Any() OrElse Not NewestSegs.Any() Then Exit Do
                    If CurrentSegs.Last <> NewestSegs.Last Then Exit Do
                    CurrentSegs.RemoveAt(CurrentSegs.Count - 1)
                    NewestSegs.RemoveAt(NewestSegs.Count - 1)
                    Shortened = True
                Loop
                If Shortened AndAlso CurrentSegs.Any() AndAlso NewestSegs.Any() Then
                    CurrentReplaceName = CurrentSegs.Join("-")
                    NewestReplaceName = NewestSegs.Join("-")
                End If
                'Add to the download list
                Dim TempAddress As String = PathTemp & "mods\" & Entry.File.Name.Replace(CurrentReplaceName, NewestReplaceName) 'Download into the mods folder so the duplicate-file check can recognize it
                Dim RealAddress As String = Path.Combine(PathUtils.RemoveLastPart(Entry.File.FullName), Entry.File.Name.Replace(CurrentReplaceName, NewestReplaceName))
                FileList.Add(File.ToNetFile(TempAddress, ResourceVersion.DownloadReason.Update, PageInstanceLeft.Instance.Version.VanillaName, PageInstanceLeft.Instance.Version.ModLoaders))
                FileCopyList(TempAddress) = RealAddress
            Next
            'Build the loader
            Dim InstallLoaders As New List(Of LoaderBase)
            Dim FinishedFileNames As New List(Of String)
            InstallLoaders.Add(New LoaderDownload("下载新版 Mod 文件", FileList) With {.ProgressWeight = ModList.Count * 1.5}) '1.5s per mod
            InstallLoaders.Add(New LoaderTask(Of Integer, Integer)("替换旧版 Mod 文件",
            Sub()
                Try
                    For Each Entry As LocalResourceFile In ModList
                        If FileUtils.Exists(Entry.File.FullName) Then
                            FileUtils.Delete(Entry.File.FullName, True)
                        Else
                            Logger.Warn($"未找到更新前的 Mod 文件，跳过对它的删除：{Entry.File.FullName}")
                        End If
                    Next
                    For Each Entry As KeyValuePair(Of String, String) In FileCopyList
                        If DirectoryUtils.Exists(PathUtils.RemoveLastPart(Entry.Value)) Then
                            FileUtils.Move(Entry.Key, Entry.Value)
                            FinishedFileNames.Add(PathUtils.GetLastPart(Entry.Value))
                        Else
                            Logger.Warn($"更新后的目标文件夹已被删除：{Entry.Value}")
                        End If
                    Next
                Catch ex As OperationCanceledException
                    Logger.Warn(ex, "替换旧版 Mod 文件时被主动取消")
                End Try
            End Sub))
            'Finish up
            Dim Loader As New LoaderCombo(Of IEnumerable(Of LocalResourceFile))("Mod 更新：" & PageInstanceLeft.Instance.Name, InstallLoaders)
            Dim PathMods As String = PageInstanceLeft.Instance.PathIndie & "mods\"
            Loader.OnStateChanged =
            Sub()
                'Result hint
                Select Case Loader.State
                    Case LoadState.Finished
                        Select Case FinishedFileNames.Count
                            Case 0 'Usually the mod file was locked and the player then canceled
                                Logger.Info($"没有 Mod 被成功更新")
                            Case 1
                                Hint($"已成功更新 {FinishedFileNames.Single}！", HintType.Green)
                            Case Else
                                Hint($"已成功更新 {FinishedFileNames.Count} 个 Mod！", HintType.Green)
                        End Select
                    Case LoadState.Failed
                        Hint("Mod 更新失败：" & Loader.Error.GetDisplay(False), HintType.Red)
                    Case LoadState.Canceled
                        Hint("Mod 更新已中止！", HintType.Blue)
                    Case Else
                        Return
                End Select
                Logger.Info($"已从正在进行 Mod 更新的文件夹列表移除：{PathMods}")
                UpdatingInstanceModFolders.Remove(PathMods)
                'Clean the cache
                RunInNewThread(
                Sub()
                    Try
                        For Each TempFile In FileCopyList.Keys
                            FileUtils.Delete(TempFile)
                        Next
                    Catch ex As Exception
                        Logger.Warn(ex, "清理 Mod 更新缓存失败")
                    End Try
                End Sub, "Clean Mod Update Cache", ThreadPriority.BelowNormal)
            End Sub
            'Start the loader
            Logger.Info($"开始更新 {ModList.Count} 个 Mod：{ModList.Select(Function(m) m.File.Name).Join("，")}")
            UpdatingInstanceModFolders.Add(PathMods)
            Loader.Start()
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
            ReloadModList(True)
        Catch ex As Exception
            Logger.Warn(ex, "初始化 Mod 更新失败")
        End Try
    End Sub

    'Delete
    Private Sub BtnSelectDelete_Click() Handles BtnSelectDelete.Click
        DeleteMods(LocalResourceLoader.Output.Where(Function(m) SelectedMods.Contains(m.EnabledName)))
        ChangeAllSelected(False)
    End Sub
    Private Sub DeleteMods(ModsToDelete As IEnumerable(Of LocalResourceFile))
        Try
            Dim IsSuccessful As Boolean = True
            Dim IsShiftPressed As Boolean = My.Computer.Keyboard.ShiftKeyDown
            'Confirm which files to delete
            Dim ModList = ModsToDelete.SelectMany(
            Function(Target As LocalResourceFile)
                If Target.IsEnabled Then
                    Return {Target.File.FullName, Target.File.FullName & If(FileUtils.Exists(Target.File.FullName & ".old"), ".old", ".disabled")}
                Else
                    Return {Target.File.FullName, Target.EnabledFullName}
                End If
            End Function).Distinct.Where(Function(m) FileUtils.Exists(m)).Select(Function(m) New LocalResourceFile(m)).ToList()
            'Delete the files
            For Each ModEntity In ModList
                Try
                    FileUtils.Delete(ModEntity.File.FullName, Not IsShiftPressed)
                Catch ex As OperationCanceledException
                    Logger.Warn(ex, "删除 Mod 被主动取消")
                    ReloadModList(True)
                    Return
                Catch ex As Exception
                    Logger.Error(ex, $"删除 Mod 失败（{ModEntity.File.FullName}）", LogBehavior.Alert)
                    IsSuccessful = False
                End Try
                'Clear the selection
                SelectedMods.Remove(ModEntity.EnabledName)
                'Update the lists in the loader and the UI
                LocalResourceLoader.Output.Remove(ModEntity)
                SearchResult?.Remove(ModEntity)
                ModItems.Remove(ModEntity.EnabledName)
                Dim IndexOfUi As Integer = PanList.Children.IndexOf(PanList.Children.OfType(Of MyLocalModItem).FirstOrDefault(Function(i) i.Entry.Equals(ModEntity)))
                If IndexOfUi >= 0 Then PanList.Children.RemoveAt(IndexOfUi)
            Next
            RefreshBars()
            If Not IsSuccessful Then
                Hint("由于文件被占用，Mod 删除失败，请尝试关闭正在运行的游戏后再试！", HintType.Red)
                ReloadModList(True)
            ElseIf PanList.Children.Count = 0 Then
                ReloadModList(True) 'Every file was deleted
            Else
                RefreshBars()
            End If
            'Show the result hint
            If Not IsSuccessful Then Return
            If ModList.IsSingle Then
                Hint($"已删除 {ModList.Single.File.Name}！", HintType.Green)
            Else
                Hint($"已删除 {ModList.Count} 个文件！", HintType.Green)
            End If
        Catch ex As OperationCanceledException
            Logger.Warn(ex, "删除 Mod 被主动取消")
            ReloadModList(True)
        Catch ex As Exception
            Logger.Error(ex, "删除 Mod 出现未知错误")
            ReloadModList(True)
        End Try
        LoaderRun(LoaderFolderRunType.UpdateOnly)
    End Sub

    'Clear selection
    Private Sub BtnSelectCancel_Click() Handles BtnSelectCancel.Click
        ChangeAllSelected(False)
    End Sub

#End Region

#Region "Single mod item"

    'Details
    Public Sub Info_Click(sender As Object, e As EventArgs)
        Try

            Dim ModEntry As LocalResourceFile = CType(If(TypeOf sender Is MyIconButton, sender.Tag, sender), MyLocalModItem).Entry
            'Load-failure info
            If ModEntry.Project IsNot Nothing Then
                'Open the mod download page
                FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.ResourceDetail,
                    .Additional = {ModEntry.Project, New List(Of String), PageInstanceLeft.Instance.Version.VanillaName, PageInstanceLeft.Instance.Version.ModLoaders, ResourceTypes.Mod}})
            Else
                'Gather details
                Dim ContentLines As New List(Of String)
                If ModEntry.Description IsNot Nothing Then ContentLines.Add(ModEntry.Description & vbCrLf)
                ContentLines.Add("文件：" & ModEntry.File.Name & "（" & StringUtils.FormatByteSize(ModEntry.File.Length) & "）")
                If ModEntry.Version IsNot Nothing Then ContentLines.Add("版本：" & ModEntry.Version)
                Dim DebugInfo As New List(Of String)
                If DebugInfo.Any Then
                    ContentLines.Add("")
                    ContentLines.AddRange(DebugInfo)
                End If
                'Build the mod name used for search
                Dim ModOriginalName As String = ModEntry.Display.Replace(" ", "+")
                Dim ModSearchName As String = ModOriginalName.Substring(0, 1)
                For i = 1 To ModOriginalName.Count - 1
                    Dim IsLastLower As Boolean = ModOriginalName(i - 1).ToString.Lower.Equals(ModOriginalName(i - 1).ToString)
                    Dim IsCurrentLower As Boolean = ModOriginalName(i).ToString.Lower.Equals(ModOriginalName(i).ToString)
                    If IsLastLower AndAlso Not IsCurrentLower Then
                        'Previous letter is lowercase and this one is uppercase
                        ModSearchName += "+"
                    End If
                    ModSearchName += ModOriginalName(i)
                Next
                ModSearchName = ModSearchName.Replace("++", "+").Replace("pti+Fine", "ptiFine")
                'Wiki search link
                If MyMsgBox(ContentLines.Join(vbCrLf), ModEntry.Display, "百科搜索", "返回") = 1 Then
                    OpenWebsite("https://www.mcmod.cn/s?key=" & ModSearchName & "&site=all&filter=0")
                End If
            End If
        Catch ex As Exception
            Logger.Error(ex, "获取 Mod 详情失败")
        End Try
    End Sub
    'Open the file location
    Public Sub Open_Click(sender As MyIconButton, e As EventArgs)
        Try
            Dim ListItem As MyLocalModItem = sender.Tag
            OpenExplorer(ListItem.Entry.File.FullName)
        Catch ex As Exception
            Logger.Error(ex, "打开 Mod 文件位置失败")
        End Try
    End Sub
    'Delete
    Public Sub Delete_Click(sender As MyIconButton, e As EventArgs)
        Dim ListItem As MyLocalModItem = sender.Tag
        DeleteMods({ListItem.Entry})
    End Sub
    'Enable / disable
    Public Sub ED_Click(sender As MyIconButton, e As EventArgs)
        Dim ListItem As MyLocalModItem = sender.Tag
        EDMods({ListItem.Entry}, Not ListItem.Entry.IsEnabled)
    End Sub

#End Region

#Region "Search"

    Public ReadOnly Property IsSearching As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(SearchBox.Text)
        End Get
    End Property
    Private SearchResult As List(Of LocalResourceFile)
    Public Sub SearchRun() Handles SearchBox.TextChanged
        If IsSearching Then
            'Build the query
            Dim QueryList As New List(Of SearchEntry(Of LocalResourceFile))
            For Each Entry As LocalResourceFile In LocalResourceLoader.Output
                Dim SearchSources As New List(Of SearchSource)
                SearchSources.Add(New SearchSource(Entry.Display, 1))
                SearchSources.Add(New SearchSource(Entry.File.Name, 1))
                If Entry.Version IsNot Nothing Then
                    SearchSources.Add(New SearchSource(Entry.Version, 0.2))
                End If
                If Entry.Description IsNot Nothing AndAlso Entry.Description <> "" Then
                    SearchSources.Add(New SearchSource(Entry.Description, 0.4))
                End If
                If Entry.Project IsNot Nothing Then
                    If Entry.Project.RawName <> Entry.Display Then SearchSources.Add(New SearchSource(Entry.Project.RawName, 1))
                    If Entry.Project.TranslatedName <> Entry.Project.RawName Then SearchSources.Add(New SearchSource(Entry.Project.TranslatedName, 1))
                    If Entry.Project.Description <> Entry.Description Then SearchSources.Add(New SearchSource(Entry.Project.Description, 0.4))
                    SearchSources.Add(New SearchSource(String.Join("", Entry.Project.Tags), 0.2))
                End If
                QueryList.Add(New SearchEntry(Of LocalResourceFile) With {.Item = Entry, .SearchSource = SearchSources})
            Next
            'Run the search
            SearchResult = Search(QueryList, SearchBox.Text, MaxBlurCount:=6, MinBlurSimilarity:=0.35).Select(Function(r) r.Item).ToList
        End If
        RefreshUI()
    End Sub

#End Region

End Class
