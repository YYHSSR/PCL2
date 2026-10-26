Public Class PageSetupLaunch
    Private RamRefreshTimer As VisiblePageTimer

    Private Sub PageSetupLaunch_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If RamRefreshTimer Is Nothing Then RamRefreshTimer = New VisiblePageTimer(Me, Sub() RefreshRam(True))
        PanBack.ScrollToHome()
        RefreshRam(False)
        BtnSwitch.Visibility = If(McInstanceSelected Is Nothing, Visibility.Collapsed, Visibility.Visible)

        'Java may have been changed in the version settings, so always reload (the other Refresh calls are cheap anyway)
        AniControlEnabled += 1
        Refresh()
        AniControlEnabled -= 1

        'Runs only once
        Static Reloaded As Boolean = False
        If Reloaded Then Return
        Reloaded = True

    End Sub
    Public Sub Refresh()
        Try
            If Settings.Get(Of String)("LaunchAdvanceJvm") = LegacyDefaultJvmArgs Then Settings.Set("LaunchAdvanceJvm", "")
            SettingService.RefreshSettings(Me)
            UpdateRamType()
            UpdateJavaList()
        Catch ex As NullReferenceException
            Logger.Error(ex, "启动设置项存在异常，已被自动重置", LogBehavior.Alert)
            Reset()
        Catch ex As Exception
            Logger.Error(ex, "重载启动设置时出错")
        End Try
    End Sub
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Settings.Set("LaunchArgumentIndieV2", Settings.GetDefault("LaunchArgumentIndieV2"))
            Configs.JavaList.Reset()
            Configs.JavaRemovedList.Reset()
            JavaListRefreshWorker.Start()
            Hint("已初始化启动设置！", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "初始化启动设置失败", LogBehavior.Alert)
        End Try
        Refresh()
    End Sub

#Region "Game memory"

    Public Shared Sub UpdateRamType()
        If FrmSetupLaunch?.SliderRamCustom Is Nothing Then Return
        FrmSetupLaunch.SliderRamCustom.IsEnabled = Settings.Get(Of Integer)("LaunchRamType") = 1
    End Sub

    ''' <summary>
    ''' Refresh the RAM display on the UI.
    ''' </summary>
    Public Sub RefreshRam(ShowAnim As Boolean)
        If LabRamGame Is Nothing OrElse LabRamUsed Is Nothing OrElse FrmMain.PageCurrent <> FormMain.PageType.Setup OrElse FrmSetupLeft.PageID <> FormMain.PageSubType.SetupLaunch Then Return
        'Read memory usage
        Dim RamGame As Double = Math.Round(GetRam(McInstanceSelected, False), 5)
        Dim RamTotal As Double = Math.Round(My.Computer.Info.TotalPhysicalMemory / 1024 / 1024 / 1024, 1)
        Dim RamAvailable As Double = Math.Round(My.Computer.Info.AvailablePhysicalMemory / 1024 / 1024 / 1024, 1)
        Dim RamGameActual As Double = Math.Round(Math.Min(RamGame, RamAvailable), 5)
        Dim RamUsed As Double = Math.Round(RamTotal - RamAvailable, 5)
        Dim RamEmpty As Double = Math.Round((RamTotal - RamUsed - RamGame).Clamp(0, 1000), 1)
        'Set the max available memory
        If RamTotal <= 1.5 Then
            SliderRamCustom.MaxValue = Math.Max(Math.Floor((RamTotal - 0.3) / 0.1), 1)
        ElseIf RamTotal <= 8 Then
            SliderRamCustom.MaxValue = Math.Floor((RamTotal - 1.5) / 0.5) + 12
        ElseIf RamTotal <= 16 Then
            SliderRamCustom.MaxValue = Math.Floor((RamTotal - 8) / 1) + 25
        Else
            SliderRamCustom.MaxValue = Math.Floor((RamTotal - 16) / 2) + 33
        End If
        'Set the text
        LabRamGame.Text = If(RamGame = Math.Floor(RamGame), RamGame & ".0", RamGame) & " GB" &
                          If(RamGame <> RamGameActual, " (可用 " & If(RamGameActual = Math.Floor(RamGameActual), RamGameActual & ".0", RamGameActual) & " GB)", "")
        LabRamUsed.Text = If(RamUsed = Math.Floor(RamUsed), RamUsed & ".0", RamUsed) & " GB"
        LabRamTotal.Text = " / " & If(RamTotal = Math.Floor(RamTotal), RamTotal & ".0", RamTotal) & " GB"
        If ShowAnim Then
            'Width animation
            AniStart({
                AaGridLengthWidth(ColumnRamUsed, RamUsed - ColumnRamUsed.Width.Value, 800,, New AniEaseOutFluent(AniEasePower.Strong)),
                AaGridLengthWidth(ColumnRamGame, RamGameActual - ColumnRamGame.Width.Value, 800,, New AniEaseOutFluent(AniEasePower.Strong)),
                AaGridLengthWidth(ColumnRamEmpty, RamEmpty - ColumnRamEmpty.Width.Value, 800,, New AniEaseOutFluent(AniEasePower.Strong))
            }, "SetupLaunch Ram Grid")
        Else
            'Set the widths
            ColumnRamUsed.Width = New GridLength(RamUsed, GridUnitType.Star)
            ColumnRamGame.Width = New GridLength(RamGameActual, GridUnitType.Star)
            ColumnRamEmpty.Width = New GridLength(RamEmpty, GridUnitType.Star)
        End If
    End Sub
    Private Sub RefreshRam() Handles SliderRamCustom.Change, RadioRamType0.Check, RadioRamType1.Check
        RefreshRam(True)
    End Sub

    Private RamTextLeft As Integer = 2, RamTextRight As Integer = 1
    ''' <summary>
    ''' Refresh text positions on the UI.
    ''' </summary>
    Private Sub RefreshRamText() Handles RectRamGame.SizeChanged, RectRamEmpty.SizeChanged, LabRamGame.SizeChanged
        'Read width info
        Dim RectUsedWidth = RectRamUsed.ActualWidth
        Dim TotalWidth = PanRamDisplay.ActualWidth
        Dim LabGameWidth = LabRamGame.ActualWidth, LabUsedWidth = LabRamUsed.ActualWidth, LabTotalWidth = LabRamTotal.ActualWidth
        Dim LabGameTitleWidth = LabRamGameTitle.ActualWidth, LabUsedTitleWidth = LabRamUsedTitle.ActualWidth
        'Left side
        Dim Left As Integer
        If RectUsedWidth - 30 < LabUsedWidth OrElse RectUsedWidth - 30 < LabUsedTitleWidth Then
            'Nothing fits
            Left = 0
        ElseIf RectUsedWidth - 25 < (LabUsedWidth + LabTotalWidth) Then
            'The full figures do not fit
            Left = 1
        Else
            'Normal
            Left = 2
        End If
        If RamTextLeft <> Left Then
            RamTextLeft = Left
            Select Case Left
                Case 0
                    AniStart({
                            AaOpacity(LabRamUsed, -LabRamUsed.Opacity, 100),
                            AaOpacity(LabRamTotal, -LabRamTotal.Opacity, 100),
                            AaOpacity(LabRamUsedTitle, -LabRamUsedTitle.Opacity, 100)
                        }, "SetupLaunch Ram TextLeft")
                Case 1
                    AniStart({
                            AaOpacity(LabRamUsed, 1 - LabRamUsed.Opacity, 100),
                            AaOpacity(LabRamTotal, -LabRamTotal.Opacity, 100),
                            AaOpacity(LabRamUsedTitle, 0.7 - LabRamUsedTitle.Opacity, 100)
                        }, "SetupLaunch Ram TextLeft")
                Case 2
                    AniStart({
                            AaOpacity(LabRamUsed, 1 - LabRamUsed.Opacity, 100),
                            AaOpacity(LabRamTotal, 1 - LabRamTotal.Opacity, 100),
                            AaOpacity(LabRamUsedTitle, 0.7 - LabRamUsedTitle.Opacity, 100)
                        }, "SetupLaunch Ram TextLeft")
            End Select
        End If
        'Right side
        Dim Right As Integer
        If TotalWidth < LabGameWidth + 2 + RectUsedWidth OrElse TotalWidth < LabGameTitleWidth + 2 + RectUsedWidth Then
            'Pushed to the far right
            Right = 0
        Else
            'Normal case
            Right = 1
        End If
        If Right = 0 Then
            If AniControlEnabled = 0 AndAlso (RamTextRight <> Right OrElse AniIsRun("SetupLaunch Ram TextRight")) Then
                'Needs animation
                AniStart({
                        AaX(LabRamGame, TotalWidth - LabGameWidth - LabRamGame.Margin.Left, 100,, New AniEaseOutFluent(AniEasePower.Weak)),
                        AaX(LabRamGameTitle, TotalWidth - LabGameTitleWidth - LabRamGameTitle.Margin.Left, 100,, New AniEaseOutFluent(AniEasePower.Weak))
                }, "SetupLaunch Ram TextRight")
            Else
                'No animation needed
                AniStop("SetupLaunch Ram TextRight")
                LabRamGame.Margin = New Thickness(TotalWidth - LabGameWidth, 3, 0, 0)
                LabRamGameTitle.Margin = New Thickness(TotalWidth - LabGameTitleWidth, 0, 0, 5)
            End If
        Else
            If AniControlEnabled = 0 AndAlso (RamTextRight <> Right OrElse AniIsRun("SetupLaunch Ram TextRight")) Then
                'Needs animation
                AniStart({
                        AaX(LabRamGame, 2 + RectUsedWidth - LabRamGame.Margin.Left, 100,, New AniEaseOutFluent(AniEasePower.Weak)),
                        AaX(LabRamGameTitle, 2 + RectUsedWidth - LabRamGameTitle.Margin.Left, 100,, New AniEaseOutFluent(AniEasePower.Weak))
                }, "SetupLaunch Ram TextRight")
            Else
                'No animation needed
                AniStop("SetupLaunch Ram TextRight")
                LabRamGame.Margin = New Thickness(2 + RectUsedWidth, 3, 0, 0)
                LabRamGameTitle.Margin = New Thickness(2 + RectUsedWidth, 0, 0, 5)
            End If
        End If
        RamTextRight = Right
    End Sub

    ''' <summary>
    ''' Get the configured RAM value, in GB.
    ''' </summary>
    Public Shared Function GetRam(Instance As McInstance, UseVersionJavaSetup As Boolean) As Double

        '------------------------------------------
        'When changing the code below, also update PageInstanceSetup
        '------------------------------------------

        Dim RamGive As Double
        If Settings.Get(Of Integer)("LaunchRamType") = 0 Then
            'Automatic
            Dim RamAvailable As Double = Math.Round(My.Computer.Info.AvailablePhysicalMemory / 1024 / 1024 / 1024 * 10) / 10
            'Decide how much memory is needed
            Dim RamMininum As Double 'Minimum memory that must always be guaranteed
            Dim RamTarget1 As Double 'Memory that should just about run it
            Dim RamTarget2 As Double 'Memory that should be comfortable
            Dim RamTarget3 As Double 'Memory for a huge pile of resource packs, mods, and shaders
            If Instance IsNot Nothing AndAlso Not Instance.IsLoaded Then Instance.Load()
            If Instance IsNot Nothing AndAlso Instance.Modable Then
                'Versions that can install mods
                Dim ModCount = ModDirectoryCache.GetCount(Instance.PathIndie & "mods\", Not RunInUi())
                RamMininum = 0.5 + ModCount / 150
                RamTarget1 = 1.5 + ModCount / 90
                RamTarget2 = 2.7 + ModCount / 50
                RamTarget3 = 4.5 + ModCount / 25
            ElseIf Instance IsNot Nothing AndAlso Instance.Version.HasOptiFine Then
                'OptiFine versions
                RamMininum = 0.5
                RamTarget1 = 1.5
                RamTarget2 = 3
                RamTarget3 = 5
            Else
                'Vanilla versions
                RamMininum = 0.5
                RamTarget1 = 1.5
                RamTarget2 = 2.5
                RamTarget3 = 4
            End If
            Dim RamDelta As Double
            'Preallocate memory, stage 1, 0 to T1, 100%
            RamDelta = RamTarget1
            RamGive += Math.Min(RamAvailable, RamDelta)
            RamAvailable -= RamDelta
            If RamAvailable < 0.1 Then GoTo PreFin
            'Preallocate memory, stage 2, T1 to T2, 70%
            RamDelta = RamTarget2 - RamTarget1
            RamGive += Math.Min(RamAvailable * 0.7, RamDelta)
            RamAvailable -= RamDelta / 0.7
            If RamAvailable < 0.1 Then GoTo PreFin
            'Preallocate memory, stage 3, T2 to T3, 40%
            RamDelta = RamTarget3 - RamTarget2
            RamGive += Math.Min(RamAvailable * 0.4, RamDelta)
            RamAvailable -= RamDelta / 0.4
            If RamAvailable < 0.1 Then GoTo PreFin
            'Preallocate memory, stage 4, T3 to T3 * 2, 15%
            RamDelta = RamTarget3
            RamGive += Math.Min(RamAvailable * 0.15, RamDelta)
            RamAvailable -= RamDelta / 0.15
            If RamAvailable < 0.1 Then GoTo PreFin
PreFin:
            'Do not go below the minimum
            RamGive = Math.Round(Math.Max(RamGive, RamMininum), 1)
        Else
            'Manual
            Dim Value = Settings.Get(Of Integer)("LaunchRamCustom")
            If Value <= 12 Then
                RamGive = Value * 0.1 + 0.3
            ElseIf Value <= 25 Then
                RamGive = (Value - 12) * 0.5 + 1.5
            ElseIf Value <= 33 Then
                RamGive = (Value - 25) * 1 + 8
            Else
                RamGive = (Value - 33) * 2 + 16
            End If
        End If
        Return RamGive
    End Function

#End Region

#Region "Java selection"
    'Note: when changing this code, also update PageInstanceSetup.xaml.vb

    'Refresh the Java dropdown
    Public Sub UpdateJavaList()
        If ComboAdvanceJava Is Nothing OrElse LabAdvanceJava Is Nothing OrElse Not ComboAdvanceJava.IsLoaded Then Return
        ComboAdvanceJava.Items.Clear()
        'Still waiting for the search to finish
        If JavaListRefreshWorker.Running Then
            BtnAdvanceJavaSearch.IsEnabled = False
            ComboAdvanceJava.IsEnabled = False
            LabAdvanceJava.Text = "搜索中 …"
            Return
        End If
        '========================================== Show results ==========================================
        BtnAdvanceJavaSearch.IsEnabled = True
        ComboAdvanceJava.IsEnabled = True
        'Update the dropdown text
        Dim Count = Configs.JavaList.Get().Count
        LabAdvanceJava.Text = If(Count > 0, $"共有 {Count} 个 Java …", "未找到 Java，点击以导入已有的 Java")
        'Update the list
        Try
            Dim JavaEntries = Configs.JavaList.Get()
            For i = 0 To JavaEntries.Count - 1
                Dim JavaEntry = JavaEntries(i)
                Dim JavaItem As New MyListItem With {
                    .FontSize = 13, .Height = 24, .IsScaleAnimationEnabled = False, .Type = MyListItem.CheckType.Clickable,
                    .Tag = JavaEntry, .Title = JavaEntry.ToString}
                AddHandler JavaItem.MouseLeftButtonUp, Sub(sender As Object, e As MouseButtonEventArgs) e.Handled = True
                AddHandler JavaItem.Click, Sub() Hint("点击选项右侧的箭头可以进行排序，以控制 PCL 优先选择哪个 Java！")
                ComboAdvanceJava.Items.Add(JavaItem)
                Dim Buttons As New List(Of MyIconButton)
                'Move-up button
                Dim UpButton As New MyIconButton With {.Logo = Logo.IconButtonArrowUp, .LogoScale = 0.95, .Height = 24, .Width = 24}
                UpButton.IsEnabled = i > 0
                UpButton.ToolTip = "提高优先级"
                ToolTipService.SetShowOnDisabled(UpButton, True)
                AddHandler UpButton.Click, Sub() MoveJavaInList(JavaEntry, -1)
                Buttons.Add(UpButton)
                'Move-down button
                Dim DownButton As New MyIconButton With {.Logo = Logo.IconButtonArrowDown, .LogoScale = 0.95, .Height = 24, .Width = 24}
                DownButton.IsEnabled = i < JavaEntries.Count - 1
                DownButton.ToolTip = "降低优先级"
                ToolTipService.SetShowOnDisabled(DownButton, True)
                AddHandler DownButton.Click, Sub() MoveJavaInList(JavaEntry, 1)
                Buttons.Add(DownButton)
                'Remove-from-list button
                Dim IsOfficial As Boolean = PathUtils.IsParentOf($"{Paths.AppData}.minecraft\runtime\", JavaEntry.Folder)
                Dim DeleteButton As New MyIconButton With {.Logo = Logo.IconButtonStop, .Height = 24, .Width = 24}
                DeleteButton.IsEnabled = Not IsOfficial
                DeleteButton.ToolTip = If(DeleteButton.IsEnabled, "从列表中移除", "无法移除官方 Java")
                AddHandler DeleteButton.Click, Sub() ManuallyRemoveJava(JavaEntry)
                ToolTipService.SetShowOnDisabled(DeleteButton, True)
                Buttons.Add(DeleteButton)
                'Open-folder button
                Dim OpenButton As New MyIconButton With {.Logo = Logo.IconButtonOpen, .LogoScale = 1.1, .Height = 24, .Width = 24}
                OpenButton.ToolTip = "打开文件夹"
                AddHandler OpenButton.Click, Sub() OpenExplorer(JavaEntry.JavaExePath)
                Buttons.Add(OpenButton)
                JavaItem.Buttons = Buttons
            Next
            Dim ImportItem As New MyListItem With {
                .FontSize = 13, .Height = 24, .IsScaleAnimationEnabled = False, .Title = "导入电脑中已有的 Java…", .Type = MyListItem.CheckType.Clickable}
            AddHandler ImportItem.MouseLeftButtonUp,
            Sub(sender As Object, e As MouseButtonEventArgs)
                e.Handled = True
                ManuallyImportJava()
            End Sub
            ComboAdvanceJava.Items.Add(ImportItem)
        Catch ex As Exception
            Logger.Error(ex, "更新 Java 列表下拉框失败")
        End Try
    End Sub
    Private Sub MoveJavaInList(JavaEntry As Java, Offset As Integer)
        Configs.JavaList.Edit(
        Sub(ByRef List)
            Dim Index = List.IndexOf(JavaEntry)
            Dim NewIndex = Index + Offset
            If Index < 0 OrElse NewIndex < 0 OrElse NewIndex >= List.Count Then Return
            List.RemoveAt(Index)
            List.Insert(NewIndex, JavaEntry)
        End Sub)
        UpdateJavaLists()
    End Sub
    Private Sub BtnAdvanceJavaSearch_Click() Handles BtnAdvanceJavaSearch.Click
        ManuallySearchJava()
    End Sub

#End Region

#Region "Other options"

    Private Sub WindowTypeUIRefresh() Handles ComboArgumentWindowType.SelectionChanged
        Dim IsVisibie = (ComboArgumentWindowType.SelectedIndex = 3).ToVisibility
        TextArgumentWindowHeight.Visibility = IsVisibie
        LabArgumentWindowMiddle.Visibility = IsVisibie
        TextArgumentWindowWidth.Visibility = IsVisibie
    End Sub

    'Warning when visibility is set to close immediately
    Private Sub ComboArgumentVisibie_SizeChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboArgumentVisibie.SelectionChanged
        If AniControlEnabled <> 0 Then Return
        If ComboArgumentVisibie.SelectedIndex = 0 Then
            If MyMsgBox("若在游戏启动后立即关闭启动器，崩溃检测、更改游戏标题等功能将失效。" & vbCrLf &
                        "如果想保留这些功能，可以选择让启动器在游戏启动后隐藏，游戏退出后自动关闭。", "提醒", "继续", "取消") = 2 Then
                If e.RemovedItems.Count > 0 Then ComboArgumentVisibie.SelectedItem = e.RemovedItems(0)
            End If
        End If
    End Sub


    'Version isolation notice
    Private Sub ComboArgumentIndie_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboArgumentIndieV2.SelectionChanged
        If AniControlEnabled <> 0 Then Return
        MyMsgBox("本设置仅会对之后新安装的版本生效。" & vbCrLf & "如果要修改已安装的版本的隔离方式，请在它的版本独立设置中调整。")
    End Sub

#End Region

    'Switch to per-version settings
    Private Sub BtnSwitch_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSwitch.Click
        McInstanceSelected.Load()
        PageInstanceLeft.Instance = McInstanceSelected
        FrmMain.PageChange(FormMain.PageType.InstanceSetup, FormMain.PageSubType.InstanceSetup)
    End Sub

End Class
