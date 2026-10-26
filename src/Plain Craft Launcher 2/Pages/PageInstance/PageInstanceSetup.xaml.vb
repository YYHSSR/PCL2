Public Class PageInstanceSetup
    Private RamRefreshTimer As VisiblePageTimer

    Private LoadedFirst As Boolean = False
    Private Sub PageSetupSystem_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        If RamRefreshTimer Is Nothing Then RamRefreshTimer = New VisiblePageTimer(Me, Sub() RefreshRam(True))
        PanBack.ScrollToHome()
        RefreshRam(False)

        'Each instance differs, so this must reload every time
        AniControlEnabled += 1
        Reload()
        AniControlEnabled -= 1

        'One-time load
        If LoadedFirst Then Return
        LoadedFirst = True

        'Sync with the Java loader
        AddHandler PageInstanceLeft.CurrentJavaWorker.Started, AddressOf UpdateJavaRelatedUi
        AddHandler PageInstanceLeft.CurrentJavaWorker.Stopped, AddressOf UpdateJavaRelatedUi

        'Version range validation
        TextArgumentJavaRange.ValidateRules = New ObjectModel.Collection(Of Validate) From {New ValidateFunc(
        Function(Str As String)
            Try
                If String.IsNullOrWhiteSpace(Str) Then Return "不能为空"
                Dim Range = ValueRange(Of Version).FromString(Str, Function(s) StringUtils.ParseVersionWithDefaults(s))
                If Range.IsEmpty Then Return "范围下限的版本号比上限更高，导致该范围无法匹配到任何版本"
                If Range.HasLower AndAlso Range.Lower.Major <= 1 AndAlso (Range.Lower.Minor > 0 OrElse Range.Lower.Build > 0 OrElse Range.Lower.Revision > 0) Then Return "不应使用 1.x 格式进行匹配（例如，若想匹配 Java 17，应填写 17 而非 1.17）"
                If Range.HasUpper AndAlso Range.Upper.Major <= 1 AndAlso (Range.Upper.Minor > 0 OrElse Range.Upper.Build > 0 OrElse Range.Upper.Revision > 0) Then Return "不应使用 1.x 格式进行匹配（例如，若想匹配 Java 17，应填写 17 而非 1.17）"
                If Range.HasUpper AndAlso Range.Upper.Major <= 4 Then Return "该范围所要求的 Java 版本过低"
                If Range.HasUpper AndAlso Range.IsUpperInclusive AndAlso Range.Upper.Minor <= 0 AndAlso Range.Upper.Build <= 0 Then Return $"右侧闭区间的意图并不明确。如果不想允许 Java {Range.Upper.Major}，请改为 {vbLQ}{Range.Upper.Major}){vbRQ}。如果想允许 Java {Range.Upper.Major}，请改为 {vbLQ}{Range.Upper.Major + 1}){vbRQ}。"
                If Range.Intersect(ValueRange(Of Version).OpenClosed(New Version(5, 0), New Version(99, 0))).IsEmpty() Then Return "该范围无法匹配到任何常见的 Java"
                Return ""
            Catch ex As Exception
                Return ex.Message
            End Try
        End Function)}


    End Sub
    Public Sub Reload()
        Try

            'Launch arguments
            Dim _unused = PageInstanceLeft.Instance.PathIndie 'Trigger automatic detection
            ComboArgumentIndieV2.SelectedIndex = If(Settings.Get(Of Boolean)("VersionArgumentIndieV2", Instance:=PageInstanceLeft.Instance), 0, 1)
            UpdateJavaList()

            'Server
            ComboServerLogin.SelectedIndex = Settings.Get(Of Integer)("VersionServerLogin", Instance:=PageInstanceLeft.Instance)
            ComboServerLoginLast = ComboServerLogin.SelectedIndex
            UpdateServerLoginUI()

            'Advanced settings
            If Settings.Get(Of Integer)("VersionAdvanceAssets", Instance:=PageInstanceLeft.Instance) = 2 Then
                Logger.Info("已迁移老版本的关闭文件校验设置")
                Settings.Reset("VersionAdvanceAssets", Instance:=PageInstanceLeft.Instance)
                Settings.Set("VersionAdvanceAssetsV2", True, Instance:=PageInstanceLeft.Instance)
            End If

            SettingService.RefreshSettings(Me)

            'Game memory
            OnVersionRamTypeChanged(Settings.Get(Of Integer)("VersionRamType", Instance:=PageInstanceLeft.Instance))

        Catch ex As Exception
            Logger.Error(ex, "重载版本独立设置时出错")
        End Try
    End Sub

    'Initialize
    Public Sub Reset()
        Try

            SettingService.ResetSettings(Me)

            Settings.Reset("VersionServerLogin", Instance:=PageInstanceLeft.Instance)
            Settings.Reset("VersionArgumentIndieV2", Instance:=PageInstanceLeft.Instance)
            Settings.Reset("VersionAdvanceAssets", Instance:=PageInstanceLeft.Instance)
            Configs.JavaForced.Reset(PageInstanceLeft.Instance.Config)
            JavaListRefreshWorker.Start()

            Logger.Info("已初始化版本独立设置")
            Hint("已初始化版本独立设置！", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "初始化版本独立设置失败", LogBehavior.Alert)
        End Try

        Reload()
    End Sub

#Region "Game memory"

    Public Shared Sub OnVersionRamTypeChanged(Type As Integer)
        If FrmInstanceSetup Is Nothing Then Return
        FrmInstanceSetup.RamType(Type)
    End Sub

    Public Sub RamType(Type As Integer)
        If SliderRamCustom Is Nothing Then Return
        SliderRamCustom.IsEnabled = (Type = 1)
    End Sub

    ''' <summary>
    ''' Refresh the RAM display on the UI.
    ''' </summary>
    Public Sub RefreshRam(ShowAnim As Boolean)
        If LabRamGame Is Nothing OrElse LabRamUsed Is Nothing OrElse FrmMain.PageCurrent <> FormMain.PageType.InstanceSetup OrElse FrmInstanceLeft.PageID <> FormMain.PageSubType.InstanceSetup Then Return
        'Read memory usage
        Dim RamGame As Double = Math.Round(GetRam(PageInstanceLeft.Instance), 5)
        Dim RamTotal As Double = Math.Round(My.Computer.Info.TotalPhysicalMemory / 1024 / 1024 / 1024, 1)
        Dim RamAvailable As Double = Math.Round(My.Computer.Info.AvailablePhysicalMemory / 1024 / 1024 / 1024, 1)
        Dim RamGameActual As Double = Math.Round(Math.Min(RamGame, RamAvailable), 5)
        Dim RamUsed As Double = Math.Round(RamTotal - RamAvailable, 5)
        Dim RamEmpty As Double = Math.Round((RamTotal - RamUsed - RamGame).Clamp(0, 1000), 1)
        'Set the maximum available memory
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
            'Animate the widths
            AniStart({
                AaGridLengthWidth(ColumnRamUsed, RamUsed - ColumnRamUsed.Width.Value, 800,, New AniEaseOutFluent(AniEasePower.Strong)),
                AaGridLengthWidth(ColumnRamGame, RamGameActual - ColumnRamGame.Width.Value, 800,, New AniEaseOutFluent(AniEasePower.Strong)),
                AaGridLengthWidth(ColumnRamEmpty, RamEmpty - ColumnRamEmpty.Width.Value, 800,, New AniEaseOutFluent(AniEasePower.Strong))
            }, "VersionSetup Ram Grid")
        Else
            'Set the widths
            ColumnRamUsed.Width = New GridLength(RamUsed, GridUnitType.Star)
            ColumnRamGame.Width = New GridLength(RamGameActual, GridUnitType.Star)
            ColumnRamEmpty.Width = New GridLength(RamEmpty, GridUnitType.Star)
        End If
    End Sub
    Private Sub RefreshRam() Handles SliderRamCustom.Change, RadioRamType0.Check, RadioRamType1.Check, RadioRamType2.Check
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
            'Fits normally
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
                        }, "VersionSetup Ram TextLeft")
                Case 1
                    AniStart({
                            AaOpacity(LabRamUsed, 1 - LabRamUsed.Opacity, 100),
                            AaOpacity(LabRamTotal, -LabRamTotal.Opacity, 100),
                            AaOpacity(LabRamUsedTitle, 0.7 - LabRamUsedTitle.Opacity, 100)
                        }, "VersionSetup Ram TextLeft")
                Case 2
                    AniStart({
                            AaOpacity(LabRamUsed, 1 - LabRamUsed.Opacity, 100),
                            AaOpacity(LabRamTotal, 1 - LabRamTotal.Opacity, 100),
                            AaOpacity(LabRamUsedTitle, 0.7 - LabRamUsedTitle.Opacity, 100)
                        }, "VersionSetup Ram TextLeft")
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
            If AniControlEnabled = 0 AndAlso (RamTextRight <> Right OrElse AniIsRun("VersionSetup Ram TextRight")) Then
                'Needs animation
                AniStart({
                        AaX(LabRamGame, TotalWidth - LabGameWidth - LabRamGame.Margin.Left, 100,, New AniEaseOutFluent(AniEasePower.Weak)),
                        AaX(LabRamGameTitle, TotalWidth - LabGameTitleWidth - LabRamGameTitle.Margin.Left, 100,, New AniEaseOutFluent(AniEasePower.Weak))
                }, "VersionSetup Ram TextRight")
            Else
                'No animation needed
                LabRamGame.Margin = New Thickness(TotalWidth - LabGameWidth, 3, 0, 0)
                LabRamGameTitle.Margin = New Thickness(TotalWidth - LabGameTitleWidth, 0, 0, 5)
            End If
        Else
            If AniControlEnabled = 0 AndAlso (RamTextRight <> Right OrElse AniIsRun("VersionSetup Ram TextRight")) Then
                'Needs animation
                AniStart({
                        AaX(LabRamGame, 2 + RectUsedWidth - LabRamGame.Margin.Left, 100,, New AniEaseOutFluent(AniEasePower.Weak)),
                        AaX(LabRamGameTitle, 2 + RectUsedWidth - LabRamGameTitle.Margin.Left, 100,, New AniEaseOutFluent(AniEasePower.Weak))
                }, "VersionSetup Ram TextRight")
            Else
                'No animation needed
                LabRamGame.Margin = New Thickness(2 + RectUsedWidth, 3, 0, 0)
                LabRamGameTitle.Margin = New Thickness(2 + RectUsedWidth, 0, 0, 5)
            End If
        End If
        RamTextRight = Right
    End Sub

    ''' <summary>
    ''' Get the configured RAM value, in GB.
    ''' </summary>
    Public Shared Function GetRam(Instance As McInstance) As Double
        'Follow the global setting
        If Settings.Get(Of Integer)("VersionRamType", Instance:=Instance) = 2 Then
            Return PageSetupLaunch.GetRam(Instance, True)
        End If

        '------------------------------------------
        ' When changing the code below, also change PageSetupLaunch
        '------------------------------------------

        'Use this instance's setting
        Dim RamGive As Double
        If Settings.Get(Of Integer)("VersionRamType", Instance:=Instance) = 0 Then
            'Automatic
            Dim RamAvailable As Double = Math.Round(My.Computer.Info.AvailablePhysicalMemory / 1024 / 1024 / 1024 * 10) / 10
            'Decide how much memory is needed
            Dim RamMininum As Double 'Minimum that must always be guaranteed
            Dim RamTarget1 As Double 'Enough to barely run
            Dim RamTarget2 As Double 'Enough to run comfortably
            Dim RamTarget3 As Double 'Needed when too many addons are installed
            If Instance IsNot Nothing AndAlso Not Instance.IsLoaded Then Instance.Load()
            If Instance IsNot Nothing AndAlso Instance.Modable Then
                'Instances that can install mods
                Dim ModCount = ModDirectoryCache.GetCount(Instance.PathIndie & "mods\", Not RunInUi())
                RamMininum = 0.5 + ModCount / 150
                RamTarget1 = 1.5 + ModCount / 90
                RamTarget2 = 2.7 + ModCount / 50
                RamTarget3 = 4.5 + ModCount / 25
            ElseIf Instance IsNot Nothing AndAlso Instance.Version.HasOptiFine Then
                'OptiFine instance
                RamMininum = 0.5
                RamTarget1 = 1.5
                RamTarget2 = 3
                RamTarget3 = 5
            Else
                'Normal instance
                RamMininum = 0.5
                RamTarget1 = 1.5
                RamTarget2 = 2.5
                RamTarget3 = 4
            End If
            Dim RamDelta As Double
            'Preallocate, stage 1, 0 ~ T1, 100%
            RamDelta = RamTarget1
            RamGive += Math.Min(RamAvailable, RamDelta)
            RamAvailable -= RamDelta
            If RamAvailable < 0.1 Then GoTo PreFin
            'Preallocate, stage 2, T1 ~ T2, 70%
            RamDelta = RamTarget2 - RamTarget1
            RamGive += Math.Min(RamAvailable * 0.7, RamDelta)
            RamAvailable -= RamDelta / 0.7
            If RamAvailable < 0.1 Then GoTo PreFin
            'Preallocate, stage 3, T2 ~ T3, 40%
            RamDelta = RamTarget3 - RamTarget2
            RamGive += Math.Min(RamAvailable * 0.4, RamDelta)
            RamAvailable -= RamDelta / 0.4
            If RamAvailable < 0.1 Then GoTo PreFin
            'Preallocate, stage 4, T3 ~ T3 * 2, 15%
            RamDelta = RamTarget3
            RamGive += Math.Min(RamAvailable * 0.15, RamDelta)
            RamAvailable -= RamDelta / 0.15
            If RamAvailable < 0.1 Then GoTo PreFin
PreFin:
            'Do not go below the minimum
            RamGive = Math.Round(Math.Max(RamGive, RamMininum), 1)
        Else
            'Manual
            Dim Value As Integer = Settings.Get(Of Integer)("VersionRamCustom", Instance:=Instance)
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

#Region "Server"

    'When third-party login changes, clear the instance list cache so categories update
    'TODO: does dragging to change third-party login leave the instance list cache stale?
    Public Shared Sub OnVersionServerLoginChanged(Type As Integer)
        If FrmInstanceSetup Is Nothing Then Return
        WriteIni(McFolderSelected & "PCL.ini", "InstanceCache", "")
        If PageInstanceLeft.Instance Is Nothing Then Return
        PageInstanceLeft.Instance = New McInstance(PageInstanceLeft.Instance.Name).Load()
        LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
    End Sub

    'Auto-replace punctuation
    Private Sub TextServerEnter_Change() Handles TextServerEnter.TextChanged
        Dim NewText = TextServerEnter.Text.Replace("：", ":").Replace("。", ".")
        If NewText = TextServerEnter.Text Then Return
        Dim CurrentPosition = TextServerEnter.SelectionStart 'Restore the caret
        TextServerEnter.Text = NewText
        TextServerEnter.SelectionStart = CurrentPosition
    End Sub

    'Global login
    Private ComboServerLoginLast As Integer
    Private Sub ComboServerLogin_Changed() Handles ComboServerLogin.SelectionChanged, TextServerNide.ValidatedTextChanged, TextServerAuthServer.ValidatedTextChanged, TextServerAuthRegister.ValidatedTextChanged
        If AniControlEnabled <> 0 Then Return
        UpdateServerLoginUI()
        'Apply the setting only when the input is valid
        If ComboServerLogin.SelectedIndex = 3 AndAlso Not TextServerNide.IsValidated Then Return
        If ComboServerLogin.SelectedIndex = 4 AndAlso Not TextServerAuthServer.IsValidated Then Return
        'Skip when the result has not changed
        If ComboServerLoginLast = ComboServerLogin.SelectedIndex Then Return
        ComboServerLoginLast = ComboServerLogin.SelectedIndex
        Settings.Set("VersionServerLogin", ComboServerLogin.SelectedIndex, Instance:=PageInstanceLeft.Instance)
    End Sub
    Private Sub UpdateServerLoginUI()
        If LabServerNide Is Nothing Then Return
        Dim Type = ComboServerLogin.SelectedIndex
        LabServerNide.Visibility = If(Type = 3, Visibility.Visible, Visibility.Collapsed)
        TextServerNide.Visibility = If(Type = 3, Visibility.Visible, Visibility.Collapsed)
        PanServerNide.Visibility = If(Type = 3, Visibility.Visible, Visibility.Collapsed)
        LabServerAuthName.Visibility = If(Type = 4, Visibility.Visible, Visibility.Collapsed)
        TextServerAuthName.Visibility = If(Type = 4, Visibility.Visible, Visibility.Collapsed)
        LabServerAuthRegister.Visibility = If(Type = 4, Visibility.Visible, Visibility.Collapsed)
        TextServerAuthRegister.Visibility = If(Type = 4, Visibility.Visible, Visibility.Collapsed)
        LabServerAuthServer.Visibility = If(Type = 4, Visibility.Visible, Visibility.Collapsed)
        TextServerAuthServer.Visibility = If(Type = 4, Visibility.Visible, Visibility.Collapsed)
        BtnServerAuthLittle.Visibility = If(Type = 4, Visibility.Visible, Visibility.Collapsed)
        CardServer.TriggerForceResize()
    End Sub

    'LittleSkin
    Private Sub BtnServerAuthLittle_Click(sender As Object, e As EventArgs) Handles BtnServerAuthLittle.Click
        If TextServerAuthServer.Text <> "" AndAlso TextServerAuthServer.Text <> "https://littleskin.cn/api/yggdrasil" AndAlso
            MyMsgBox("即将把第三方登录设置覆盖为 LittleSkin 登录。" & vbCrLf & "除非你是服主，或者服主要求你这样做，否则请不要继续。" & vbCrLf & vbCrLf & "是否确实需要覆盖当前设置？",
                     "设置覆盖确认", "继续", "取消") = 2 Then Return
        TextServerAuthServer.Text = "https://littleskin.cn/api/yggdrasil"
        TextServerAuthRegister.Text = "https://littleskin.cn/auth/register"
        TextServerAuthName.Text = "LittleSkin 登录"
    End Sub

#End Region

#Region "Java category and preview"

    Private Sub ReloadCurrentJava() Handles ComboArgumentJava.SelectionChanged, TextArgumentJavaRange.ValidatedTextChanged
        PageInstanceLeft.ReloadCurrentJava()
    End Sub

    Private Sub UpdateJavaRelatedUi() Handles ComboArgumentJava.SelectionChanged, TextArgumentJavaRange.TextChanged
        RunInUi(AddressOf _UpdateJavaRelatedUi)
    End Sub
    Private Sub _UpdateJavaRelatedUi()
        Dim JavaWorker = PageInstanceLeft.CurrentJavaWorker
        Select Case ComboArgumentJava.SelectedIndex
            Case 0, 2
                HintArgumentJava.Visibility = Visibility.Visible
                TextArgumentJavaRange.Visibility = Visibility.Collapsed
                PanArgumentJavaSelect.Visibility = Visibility.Collapsed
                'Show the Java that will be selected
                If Not JavaWorker.LastSucceeded OrElse JavaWorker.Running Then
                    If Not JavaWorker.Running Then JavaWorker.Start()
                    HintArgumentJava.Text = $"正在查找 Java……"
                    HintArgumentJava.Theme = MyHint.Themes.Blue
                ElseIf JavaWorker.LastResult Is Nothing Then
                    If ComboArgumentJava.SelectedIndex = 0 Then
                        HintArgumentJava.Text = $"你的电脑上没有可供该版本使用的 Java，PCL 会在启动游戏时自动下载。"
                        HintArgumentJava.Theme = MyHint.Themes.Yellow
                    Else
                        HintArgumentJava.Text = $"该版本的版本文件夹中没有发现任何 Java！{vbCrLf}请点击此处打开版本文件夹，然后将 Java 文件夹复制进去。"
                        HintArgumentJava.Theme = MyHint.Themes.Red
                    End If
                Else
                    HintArgumentJava.Text = $"将会使用：{JavaWorker.LastResult}"
                    HintArgumentJava.Theme = MyHint.Themes.Blue
                End If
            Case 1
                HintArgumentJava.Visibility = Visibility.Visible
                TextArgumentJavaRange.Visibility = Visibility.Visible
                PanArgumentJavaSelect.Visibility = Visibility.Collapsed
                'First-time setup
                If Not Settings.HasSaved("VersionArgumentJavaRange", PageInstanceLeft.Instance) Then
                    Dim DefaultRange As String = GetJavaRequirement(PageInstanceLeft.Instance).Range.ToString.Replace("-∞", "").Replace("+∞", "")
                    Settings.Set("VersionArgumentJavaRange", DefaultRange, PageInstanceLeft.Instance)
                    TextArgumentJavaRange.Text = DefaultRange
                End If
                'The entered range is invalid
                If Not String.IsNullOrEmpty(TextArgumentJavaRange.ValidateResult) Then
                    HintArgumentJava.Text = $"Java 版本范围错误：{TextArgumentJavaRange.ValidateResult}"
                    HintArgumentJava.Theme = MyHint.Themes.Red
                    Return
                End If
                'Show the Java that will be selected
                If Not JavaWorker.LastSucceeded OrElse JavaWorker.Running Then
                    If Not JavaWorker.Running Then JavaWorker.Start()
                    HintArgumentJava.Text = $"正在查找 Java……"
                    HintArgumentJava.Theme = MyHint.Themes.Blue
                ElseIf JavaWorker.LastResult Is Nothing Then
                    HintArgumentJava.Text = $"你的电脑上没有任何 Java 符合该版本范围！{vbCrLf}如果 Mojang 提供了对应版本的 Java，PCL 会在启动游戏时自动下载。"
                    HintArgumentJava.Theme = MyHint.Themes.Yellow
                Else
                    HintArgumentJava.Text = $"将会使用：{JavaWorker.LastResult}"
                    HintArgumentJava.Theme = MyHint.Themes.Blue
                End If
            Case 3
                HintArgumentJava.Visibility = Visibility.Collapsed
                TextArgumentJavaRange.Visibility = Visibility.Collapsed
                PanArgumentJavaSelect.Visibility = Visibility.Visible
        End Select
    End Sub
    Private Sub HintArgumentJava_Click(sender As Object, e As MouseButtonEventArgs) Handles HintArgumentJava.Click
        If ComboArgumentJava.SelectedIndex = 2 Then PageInstanceOverall.OpenInstanceFolder(PageInstanceLeft.Instance)
    End Sub

#End Region

#Region "Dropdown for a specific Java"
    'Note: when changing this code, also change PageSetupLaunch.xaml.vb

    'Refresh the Java dropdown
    Public Sub UpdateJavaList()
        If ComboArgumentJavaSelect Is Nothing OrElse LabArgumentJava Is Nothing OrElse Not ComboArgumentJavaSelect.IsLoaded Then Return
        ComboArgumentJavaSelect.Items.Clear()
        'Still waiting for the search to finish
        If JavaListRefreshWorker.Running Then
            BtnArgumentJavaSearch.IsEnabled = False
            ComboArgumentJavaSelect.IsEnabled = False
            LabArgumentJava.Text = "搜索中 …"
            Return
        End If
        '========================================== Show results ==========================================
        BtnArgumentJavaSearch.IsEnabled = True
        ComboArgumentJavaSelect.IsEnabled = True
        Dim SelectedJava As Java = Configs.JavaForced.Get(PageInstanceLeft.Instance.Config)
        'Update the dropdown text
        If Not Configs.JavaList.Get().Any() Then
            LabArgumentJava.Text = "未找到 Java，点击以导入已有的 Java"
        ElseIf SelectedJava Is Nothing Then
            LabArgumentJava.Text = "点击选择 Java …"
        Else
            LabArgumentJava.Text = SelectedJava.ToString
        End If
        'Update the list
        Try
            For Each JavaEntry In Configs.JavaList.Get()
                Dim JavaItem As New MyListItem With {
                    .FontSize = 13, .Height = 24, .IsScaleAnimationEnabled = False, .Type = MyListItem.CheckType.Clickable,
                    .Tag = JavaEntry, .Title = JavaEntry.ToString}
                AddHandler JavaItem.MouseLeftButtonUp, Sub(sender As Object, e As MouseButtonEventArgs) e.Handled = True
                AddHandler JavaItem.Click,
                Sub()
                    Configs.JavaForced.Set(JavaEntry, PageInstanceLeft.Instance.Config)
                    UpdateJavaList()
                    ComboArgumentJavaSelect.IsDropDownOpen = False
                End Sub
                ComboArgumentJavaSelect.Items.Add(JavaItem)
                Dim Buttons As New List(Of MyIconButton)
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
                .FontSize = 13, .Height = 24, .IsScaleAnimationEnabled = False, .Title = " 导入电脑中已有的 Java…", .Type = MyListItem.CheckType.Clickable}
            AddHandler ImportItem.MouseLeftButtonUp,
            Sub(sender As Object, e As MouseButtonEventArgs)
                e.Handled = True
                ManuallyImportJava()
            End Sub
            ComboArgumentJavaSelect.Items.Add(ImportItem)
        Catch ex As Exception
            Logger.Error(ex, "更新 Java 下拉框失败")
        End Try
    End Sub
    Private Sub BtnAdvanceJavaSearch_Click() Handles BtnArgumentJavaSearch.Click
        ManuallySearchJava()
    End Sub

#End Region

    'Version isolation
    Private Sub ComboArgumentIndieV2_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboArgumentIndieV2.SelectionChanged
        If AniControlEnabled <> 0 Then Return
        Static IsReverting As Boolean = False
        If IsReverting Then Return
        If MyMsgBox("调整版本隔离设置后，你需要游戏存档、Mod 等文件手动迁移到新的游戏文件夹中。" & vbCrLf &
                    "如果发现存档消失，把这项设置改回来就能恢复。" & vbCrLf &
                    "如果你不会迁移存档，不建议修改这项设置！",
                    "警告", "我知道我在做什么", "取消", IsWarn:=True) = 2 Then
            IsReverting = True
            If e.RemovedItems.Count > 0 Then ComboArgumentIndieV2.SelectedItem = e.RemovedItems(0)
            IsReverting = False
        End If
        'Apply the setting
        Settings.Set("VersionArgumentIndieV2", sender.SelectedIndex = 0, Instance:=PageInstanceLeft.Instance)
    End Sub

    'Command to run before launch
    Private Sub TextAdvanceRun_TextChanged(sender As Object, e As TextChangedEventArgs) Handles TextAdvanceRun.TextChanged
        CheckAdvanceRunWait.Visibility = (TextAdvanceRun.Text <> "").ToVisibility
    End Sub

    'Switch to global settings
    Private Sub BtnSwitch_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSwitch.Click
        FrmMain.PageChange(FormMain.PageType.Setup, FormMain.PageSubType.SetupLaunch)
    End Sub

    'Strip line breaks from arguments
    Private Sub ReplaceEnter(sender As MyTextBox, e As TextChangedEventArgs) Handles TextAdvanceJvm.TextChanged, TextAdvanceGame.TextChanged
        Dim NewText = sender.Text.ReplaceLineEndings(" ", mergeMultiple:=True)
        If NewText = sender.Text Then Return
        Dim CaretIndex = sender.CaretIndex
        sender.Text = NewText
        sender.CaretIndex = Math.Max(0, CaretIndex - 1)
    End Sub

End Class
