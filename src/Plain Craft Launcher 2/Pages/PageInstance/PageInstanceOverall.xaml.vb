Public Class PageInstanceOverall

    Private Sub PageSetupLaunch_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PanBack.ScrollToHome()
        Reload()
    End Sub

    Public InstanceHeaderItem As MyListItem
    ''' <summary>
    ''' 确保当前页面上的信息已正确显示。
    ''' </summary>
    Private Sub Reload()
        BtnFolderMods.Visibility = If(PageInstanceLeft.Instance.Modable, Visibility.Visible, Visibility.Collapsed)
        PanDisplayItem.Children.Clear()
        InstanceHeaderItem = PageInstanceLeft.Instance.ToListItem().Init()
        InstanceHeaderItem.IsHitTestVisible = False
        PanDisplayItem.Children.Add(InstanceHeaderItem)
        FrmMain.PageNameRefresh()
    End Sub

#Region "卡片：快捷方式"

    '版本文件夹
    Private Sub BtnFolderVersion_Click() Handles BtnFolderVersion.Click
        OpenInstanceFolder(PageInstanceLeft.Instance)
    End Sub
    Public Shared Sub OpenInstanceFolder(Instance As McInstance)
        OpenExplorer(Instance.PathVersion)
    End Sub

    '存档文件夹
    Private Sub BtnFolderSaves_Click() Handles BtnFolderSaves.Click
        Dim Folder As String = PageInstanceLeft.Instance.PathIndie & "saves\"
        DirectoryUtils.Create(Folder)
        OpenExplorer(Folder)
    End Sub

    'Mod 文件夹
    Private Sub BtnFolderMods_Click() Handles BtnFolderMods.Click
        Dim Folder As String = PageInstanceLeft.Instance.PathIndie & "mods\"
        DirectoryUtils.Create(Folder)
        OpenExplorer(Folder)
    End Sub

    '截图文件夹
    Private Sub BtnFolderScreen_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnFolderScreen.Click
        Dim Folder As String = PageInstanceLeft.Instance.PathIndie & "screenshots\"
        DirectoryUtils.Create(Folder)
        OpenExplorer(Folder)
    End Sub

#End Region

#Region "卡片：管理"

    '导出启动脚本
    Private Sub BtnManageScript_Click() Handles BtnManageScript.Click
        Try
            '弹窗要求指定脚本的保存位置
            Dim SavePath As String = Dialogs.SaveFile("选择脚本保存位置", "启动 " & PageInstanceLeft.Instance.Name & ".bat", filter:={("bat", "批处理文件")})
            If SavePath Is Nothing Then Return
            '检查中断（等玩家选完弹窗指不定任务就结束了呢……）
            If McLaunchLoader.State = LoadState.Loading Then
                Hint("请在当前启动任务结束后再试！", HintType.Red)
                Return
            End If
            '生成脚本
            If McLaunchStart(New McLaunchOptions With {.SaveBatch = SavePath, .Instance = PageInstanceLeft.Instance}) Then
                If Settings.Get(Of McLoginType)("LoginType") = McLoginType.Legacy Then
                    Hint("正在导出启动脚本……")
                Else
                    Hint("正在导出启动脚本……（注意，使用脚本启动可能会导致登录失效！）")
                End If
            End If
        Catch ex As Exception
            Logger.Error(ex, $"导出启动脚本失败（{PageInstanceLeft.Instance.Name}）", LogBehavior.Alert)
        End Try
    End Sub

    '补全文件
    Private Sub BtnManageCheck_Click(sender As Object, e As EventArgs) Handles BtnManageCheck.Click
        Try
            If McInstanceLock.IsLocked(PageInstanceLeft.Instance) Then
                Hint("该版本正在被修改，请稍后再补全文件！", HintType.Red)
                Return
            End If
            '忽略文件检查提示
            If ShouldIgnoreFileCheck(PageInstanceLeft.Instance) Then
                Hint("请先关闭 [版本设置 → 设置 → 高级启动选项 → 关闭文件校验]，然后再尝试补全文件！", HintType.Blue)
                Return
            End If
            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar
                If OngoingLoader.Name <> PageInstanceLeft.Instance.Name & " 文件补全" Then Continue For
                Hint("正在处理中，请稍候！", HintType.Red)
                Return
            Next
            '启动
            Dim Loader As New LoaderCombo(Of String)(PageInstanceLeft.Instance.Name & " 文件补全", DlClientFix(PageInstanceLeft.Instance, True, False))
            Loader.OnStateChanged =
            Sub()
                Select Case Loader.State
                    Case LoadState.Finished
                        Hint(Loader.Name & "成功！", HintType.Green)
                    Case LoadState.Failed
                        Hint(Loader.Name & "失败：" & Loader.Error.GetDisplay(False), HintType.Red)
                    Case LoadState.Canceled
                        Hint(Loader.Name & "已取消！", HintType.Blue)
                End Select
            End Sub
            Loader.Start(PageInstanceLeft.Instance.Name)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
        Catch ex As Exception
            Logger.Error(ex, $"尝试补全文件失败（{PageInstanceLeft.Instance.Name}）", LogBehavior.Alert)
        End Try
    End Sub

    '删除版本
    '修改此代码时，同时修改 PageSelectRight 中的代码
    Private Sub BtnManageDelete_Click(sender As Object, e As EventArgs) Handles BtnManageDelete.Click
        Try
            If McInstanceLock.IsLocked(PageInstanceLeft.Instance) Then
                Hint("该版本正在被修改，请稍后再删除！", HintType.Red)
                Return
            End If
            Dim IsShiftPressed As Boolean = My.Computer.Keyboard.ShiftKeyDown
            Dim IsHintIndie As Boolean = PageInstanceLeft.Instance.State <> McInstance.McInstanceState.Error AndAlso PageInstanceLeft.Instance.PathIndie <> McFolderSelected
            Dim SavesFolder = DirectoryUtils.GetInfo(PageInstanceLeft.Instance.PathIndie & "saves\")
            Dim SaveEntries = If(IsHintIndie AndAlso SavesFolder.Exists, SavesFolder.EnumerateDirectories.OrderByDescending(Function(Save) Save.LastWriteTime).Select(Function(Save) $"{Save.Name}（上次修改：{StringUtils.FormatTimeSpan(Save.LastWriteTime - Date.Now, False)}）").ToList, New List(Of String))
            Select Case MyMsgBox($"你确定要{If(IsShiftPressed, "永久", "")}删除版本 {PageInstanceLeft.Instance.Name} 吗？" &
                        If(IsHintIndie, vbCrLf & "该版本对应的存档、资源包、Mod 等文件也将被一并删除！" &
                            If(SaveEntries.Any, vbCrLf & vbCrLf & "这会删除以下存档：" & vbCrLf & "· " & SaveEntries.Join(vbCrLf & "· "), ""), ""),
                        "版本删除确认", , "取消",, IsHintIndie OrElse IsShiftPressed)
                Case 1
                    PageInstanceLeft.Instance.ResetSettingsCache()
                    DirectoryUtils.Delete(PageInstanceLeft.Instance.PathVersion, Not IsShiftPressed)
                    Hint("已删除版本 " & PageInstanceLeft.Instance.Name & "！", HintType.Green)
                Case 2
                    Return
            End Select
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            FrmMain.PageBack()
        Catch ex As OperationCanceledException
            Logger.Warn(ex, $"删除版本 {PageInstanceLeft.Instance.Name} 被主动取消")
        Catch ex As Exception
            Logger.Error(ex, $"删除版本 {PageInstanceLeft.Instance.Name} 失败", LogBehavior.Alert)
        End Try
    End Sub

#End Region

End Class
