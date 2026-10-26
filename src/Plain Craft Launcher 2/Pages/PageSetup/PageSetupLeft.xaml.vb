Public Class PageSetupLeft

    Private Sub PageSetupLeft_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If Not IsPageSwitched Then ItemLaunch.SetChecked(True, False, False)
    End Sub
    Private IsPageSwitched As Boolean
    Private Sub PageSetupLeft_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        IsPageSwitched = False
    End Sub

#Region "Page switching"

    ''' <summary>
    ''' Index of the current page, starting at 0 from left to right.
    ''' </summary>
    Public PageID As FormMain.PageSubType
    Public Sub New()
        InitializeComponent()
        PageID = FormMain.PageSubType.SetupLaunch
    End Sub

    Public Sub SelectPage(ID As FormMain.PageSubType, IsCurrent As Boolean)
        Dim Item = PanItem.Children.OfType(Of MyListItem).FirstOrDefault(Function(i) CInt(i.Tag) = CInt(ID))
        If Item Is Nothing OrElse Item.Visibility <> Visibility.Visible Then Throw New ArgumentOutOfRangeException(NameOf(ID))
        Item.SetChecked(True, True, IsCurrent)
    End Sub

    ''' <summary>
    ''' Check event that changes the page.
    ''' </summary>
    Private Sub PageCheck(sender As MyListItem, e As EventArgs) Handles ItemLaunch.Check, ItemSystem.Check
        'Before control properties are initialized, sender.Tag is Nothing, so the switch is skipped and PageID defaults to 0 (the first page)
        'Using IsLoaded would drop simulated clicks (IsLoaded is False when a simulated click switches the page)
        If sender.Tag IsNot Nothing Then PageChange(Val(sender.Tag))
    End Sub

    ''' <summary>
    ''' Get the right-hand page selected by the current navigation.
    ''' </summary>
    Public Function PageGet(Optional ID As FormMain.PageSubType = -1)
        If ID = -1 Then ID = PageID
        Select Case ID
            Case FormMain.PageSubType.SetupLaunch
                If FrmSetupLaunch Is Nothing Then FrmSetupLaunch = New PageSetupLaunch
                Return FrmSetupLaunch
            Case FormMain.PageSubType.SetupSystem
                If FrmSetupSystem Is Nothing Then FrmSetupSystem = New PageSetupSystem
                Return FrmSetupSystem
            Case Else
                Throw New Exception("未知的设置子页面种类：" & ID)
        End Select
    End Function

    ''' <summary>
    ''' Switch the current page.
    ''' </summary>
    Public Sub PageChange(ID As FormMain.PageSubType)
        If PageID = ID Then Return
        AniControlEnabled += 1
        IsPageSwitched = True
        Try

            Select Case ID
                Case FormMain.PageSubType.SetupLaunch
                    If IsNothing(FrmSetupLaunch) Then FrmSetupLaunch = New PageSetupLaunch
                    PageChangeRun(FrmSetupLaunch)
                Case FormMain.PageSubType.SetupSystem
                    If IsNothing(FrmSetupSystem) Then FrmSetupSystem = New PageSetupSystem
                    PageChangeRun(FrmSetupSystem)
                Case Else
                    Throw New Exception("未知的设置子页面种类：" & ID)
            End Select

            PageID = ID
        Catch ex As Exception
            Logger.Error(ex, $"切换分页面失败（ID {ID}）")
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub
    Private Shared Sub PageChangeRun(Target As MyPageRight)
        AniStop("FrmMain PageChangeRight") 'Stop the main page's right-page switch animation so it does not fire PageOnEnter again with this one
        If Target.Parent IsNot Nothing Then Target.SetValue(ContentPresenter.ContentProperty, Nothing)
        FrmMain.PageRight = Target
        CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnExit()
        AniStart({
            AaCode(
            Sub()
                CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnForceExit()
                FrmMain.PanMainRight.Child = FrmMain.PageRight
                FrmMain.PageRight.Opacity = 0
            End Sub, 130),
            AaCode(
            Sub()
                'Delay the shared page animation so controls loaded in the Loaded event can be handled
                FrmMain.PageRight.Opacity = 1
                FrmMain.PageRight.PageOnEnter()
            End Sub, 30, True)
        }, "PageLeft PageChange")
    End Sub

#End Region

    Public Sub Reset(sender As Object, e As EventArgs)
        Select Case Val(sender.Tag)
            Case FormMain.PageSubType.SetupLaunch
                If MyMsgBox("是否要初始化 启动 页面的所有设置？该操作不可撤销。", "初始化确认",, "取消", IsWarn:=True) = 1 Then
                    If IsNothing(FrmSetupLaunch) Then FrmSetupLaunch = New PageSetupLaunch
                    FrmSetupLaunch.Reset()
                    ItemLaunch.Checked = True
                End If
            Case FormMain.PageSubType.SetupSystem
                If MyMsgBox("是否要初始化 其他 页面的所有设置？该操作不可撤销。", "初始化确认",, "取消", IsWarn:=True) = 1 Then
                    If IsNothing(FrmSetupSystem) Then FrmSetupSystem = New PageSetupSystem
                    FrmSetupSystem.Reset()
                    ItemSystem.Checked = True
                End If
        End Select
    End Sub

End Class
