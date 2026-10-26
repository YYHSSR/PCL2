Public Class PageSetupSystem


    Private Sub PageSetupSystem_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        'Runs on every load
        PanBack.ScrollToHome()


        'Runs only once
        Static IsLoaded As Boolean = False
        If IsLoaded Then Return
        IsLoaded = True

        AniControlEnabled += 1
        Reload()
        SliderLoad()
        AniControlEnabled -= 1

    End Sub
    Public Sub Reload()
        SettingService.RefreshSettings(Me)
        RefreshCurseForgeStatus()
    End Sub
    Private Sub RefreshCurseForgeStatus()
        Dim HasEnvironmentKey = Not String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY"))
        Dim SavedKey = CurseForgeAccess.GetApiKey()
        If HasEnvironmentKey Then
            LabCurseForgeStatus.Visibility = Visibility.Visible
            LabCurseForgeStatus.Text = "正在使用环境变量中的 Key。下面的输入框不会改掉它。"
        ElseIf String.IsNullOrWhiteSpace(SavedKey) Then
            LabCurseForgeStatus.Visibility = Visibility.Visible
            LabCurseForgeStatus.Text = "尚未配置。官方接口没有 Key 会返回 403；备用镜像不可用时就连不上 CurseForge。"
            PasswordCurseForge.Clear()
        Else
            LabCurseForgeStatus.Text = ""
            LabCurseForgeStatus.Visibility = Visibility.Collapsed
            PasswordCurseForge.Password = SavedKey
        End If
    End Sub
    Private Sub SaveCurseForgeKey() Handles BtnCurseForgeSave.Click
        Dim Key = PasswordCurseForge.Password.Trim()
        If String.IsNullOrWhiteSpace(Key) OrElse Key.Contains(vbCr) OrElse Key.Contains(vbLf) Then
            Hint("请输入有效的 CurseForge API Key。", HintType.Red)
            Return
        End If
        Settings.Set("ToolCurseForgeApiKey", Key)
        RefreshCurseForgeStatus()
        Hint("已加密保存。重新搜索即可生效；环境变量中的 Key 优先。", HintType.Green)
    End Sub
    Private Sub ClearCurseForgeKey() Handles BtnCurseForgeClear.Click
        Settings.Set("ToolCurseForgeApiKey", "")
        RefreshCurseForgeStatus()
        Hint("已清除本地 Key。", HintType.Green)
    End Sub
    Private Sub OpenCurseForgeDocs() Handles BtnCurseForgeDocs.Click
        OpenWebsite("https://console.curseforge.com/")
    End Sub
    Private Sub TestCurseForge() Handles BtnCurseForgeTest.Click
        Hint("正在测试 CurseForge……")
        RunInNewThread(Sub()
                           Try
                               DlModRequest("https://api.curseforge.com/v1/games/432")
                               RunInUi(Sub() Hint("CurseForge 连接成功。", HintType.Green))
                           Catch ex As Exception
                               Dim Message = CurseForgeAccess.GetFailureHint(ex)
                               Logger.Warn(ex, "CurseForge 连接测试失败")
                               RunInUi(Sub() Hint(Message, HintType.Red))
                           End Try
                       End Sub, "CurseForge Test")
    End Sub
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Logger.Info("已初始化下载与资源设置")
            Hint("已初始化下载与资源设置！", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "初始化下载与资源设置失败", LogBehavior.Alert)
        End Try
        Reload()
    End Sub

    'Sliders
    Private Sub SliderLoad()
        SliderDownloadThread.GetHintText = Function(v) v + 1
        SliderDownloadSpeed.GetHintText =
        Function(v)
            Select Case v
                Case Is <= 14
                    Return (v + 1) * 0.1 & " M/s"
                Case Is <= 31
                    Return (v - 11) * 0.5 & " M/s"
                Case Is <= 41
                    Return (v - 21) & " M/s"
                Case Else
                    Return "无限制"
            End Select
        End Function
    End Sub
    Private Sub SliderDownloadThread_PreviewChange(sender As Object, e As RouteEventArgs) Handles SliderDownloadThread.PreviewChange
        If SliderDownloadThread.Value < 100 Then Return
        If Not Settings.Get(Of Boolean)("HintDownloadThread") Then
            Settings.Set("HintDownloadThread", True)
            MyMsgBox("如果设置过多的下载线程，可能会导致下载时出现非常严重的卡顿。" & vbCrLf &
                     "一般设置 64 线程即可满足大多数下载需求，除非你知道你在干什么，否则不建议设置更多的线程数！", "警告", "我知道了", IsWarn:=True)
        End If
    End Sub

End Class
