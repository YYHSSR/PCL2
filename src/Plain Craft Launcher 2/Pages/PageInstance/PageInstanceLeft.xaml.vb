Public Class PageInstanceLeft
    Implements IRefreshable

    ''' <summary>
    ''' The MC instance whose settings are currently shown.
    ''' </summary>
    Public Shared Instance As McInstance = Nothing

    Private Sub PageInstanceLeft_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
    End Sub

    Public Sub RefreshModDisabled() Handles Me.Loaded
        If Instance IsNot Nothing AndAlso Instance.Modable Then
            ItemMod.Visibility = Visibility.Visible
            ItemModDisabled.Visibility = Visibility.Collapsed
        Else
            ItemMod.Visibility = Visibility.Collapsed
            ItemModDisabled.Visibility = Visibility.Visible
        End If
    End Sub

#Region "Page switching"

    ''' <summary>
    ''' Current page index. Starts at 0.
    ''' </summary>
    Public PageID As FormMain.PageSubType = FormMain.PageSubType.Default

    ''' <summary>
    ''' Check event changes the page.
    ''' </summary>
    Private Sub PageCheck(sender As FrameworkElement, e As RouteEventArgs) Handles ItemOverall.Check, ItemMod.Check, ItemModDisabled.Check, ItemSetup.Check, ItemModify.Check, ItemExport.Check
        'Before control properties are initialized, sender.Tag is Nothing and would switch to page 0
        'IsLoaded would skip simulated clicks (it is False when a simulated click switches pages)
        If sender.Tag IsNot Nothing Then PageChange(Val(sender.Tag))
    End Sub

    Public Function PageGet(Optional ID As FormMain.PageSubType = -1)
        If ID = -1 Then ID = PageID
        Select Case ID
            Case FormMain.PageSubType.InstanceOverall
                If FrmInstanceOverall Is Nothing Then FrmInstanceOverall = New PageInstanceOverall
                Return FrmInstanceOverall
            Case FormMain.PageSubType.InstanceMod
                If FrmInstanceMod Is Nothing Then FrmInstanceMod = New PageInstanceMod
                Return FrmInstanceMod
            Case FormMain.PageSubType.InstanceModDisabled
                If FrmInstanceModDisabled Is Nothing Then FrmInstanceModDisabled = New PageInstanceModDisabled
                Return FrmInstanceModDisabled
            Case FormMain.PageSubType.InstanceSetup
                If FrmInstanceSetup Is Nothing Then FrmInstanceSetup = New PageInstanceSetup
                Return FrmInstanceSetup
            Case FormMain.PageSubType.InstanceExport
                If FrmInstanceExport Is Nothing Then FrmInstanceExport = New PageInstanceExport
                Return FrmInstanceExport
            Case FormMain.PageSubType.InstanceModify
                If FrmDownloadModify Is Nothing Then FrmDownloadModify = New PageDownloadModify
                Return FrmDownloadModify
            Case Else
                Throw New Exception("未知的版本设置子页面种类：" & ID)
        End Select
    End Function

    ''' <summary>
    ''' Switch to an existing page.
    ''' </summary>
    Public Sub PageChange(ID As FormMain.PageSubType)
        If PageID = ID Then Return
        If Instance IsNot Nothing AndAlso McInstanceLock.IsLocked(Instance) Then
            Hint("该版本正在被修改，请稍后再切换页面！", HintType.Red)
            Return
        End If
        AniControlEnabled += 1
        Try
            PageChangeRun(PageGet(ID))
            PageID = ID
        Catch ex As Exception
            Logger.Error(ex, $"切换分页面失败（ID {ID}）")
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub
    Private Shared Sub PageChangeRun(Target As MyPageRight)
        AniStop("FrmMain PageChangeRight") 'Stop the main window's right-page animation so it does not fire PageOnEnter again with this one
        If Target.Parent IsNot Nothing Then Target.SetValue(ContentPresenter.ContentProperty, Nothing)
        FrmMain.PageRight = Target
        CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnExit()
        AniStart({
            AaCode(Sub()
                       CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnForceExit()
                       FrmMain.PanMainRight.Child = FrmMain.PageRight
                       FrmMain.PageRight.Opacity = 0
                   End Sub, 130),
            AaCode(Sub()
                       'Delay the shared page animation so controls loaded in Loaded can finish handling it
                       FrmMain.PageRight.Opacity = 1
                       FrmMain.PageRight.PageOnEnter()
                   End Sub, 30, True)
        }, "PageLeft PageChange")
    End Sub

#End Region

    Public Sub Refresh_Click(sender As Object, e As EventArgs) 'Called anonymously by a sidebar button
        Refresh(Val(sender.Tag))
    End Sub
    Public Sub Refresh() Implements IRefreshable.Refresh
        Refresh(FrmMain.PageCurrentSub)
    End Sub
    Public Sub Refresh(SubType As FormMain.PageSubType)
        If Instance IsNot Nothing AndAlso McInstanceLock.IsLocked(Instance) Then
            Hint("该版本正在被修改，请稍后再刷新！", HintType.Red)
            Return
        End If
        Select Case SubType
            Case FormMain.PageSubType.InstanceMod
                PageInstanceMod.Refresh()
                ItemMod.Checked = True
            Case FormMain.PageSubType.InstanceExport
                FrmInstanceExport?.RefreshAll()
                ItemExport.Checked = True
            Case FormMain.PageSubType.InstanceModify
                FrmDownloadModify?.PageLoaderRestart()
                ItemModify.Checked = True
        End Select
    End Sub

    Public Sub Reset(sender As Object, e As EventArgs)
        If MyMsgBox("是否要初始化该版本的版本独立设置？该操作不可撤销。", "初始化确认",, "取消", IsWarn:=True) = 1 Then
            If IsNothing(FrmInstanceSetup) Then FrmInstanceSetup = New PageInstanceSetup
            FrmInstanceSetup.Reset()
            ItemSetup.Checked = True
        End If
    End Sub

    'Load the Java this instance will select
    Public Shared CurrentJavaWorker As New RedoableWorker(Of Java)(Function(c, p) SelectOrDownloadJava(PageInstanceLeft.Instance, False, c, p))
    Public Shared Sub ReloadCurrentJava() Handles Me.Loaded
        If AniControlEnabled <> 0 Then Return
        If PageInstanceLeft.Instance Is Nothing Then Return
        CurrentJavaWorker.Start()
    End Sub

End Class
