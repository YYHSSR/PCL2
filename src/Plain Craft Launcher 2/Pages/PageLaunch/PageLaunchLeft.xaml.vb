Imports System.Windows.Threading

Public Class PageLaunchLeft

    'Load the current version
    Private IsLoad As Boolean = False
    Private IsLoadFinished As Boolean = False
    Public Sub PageLaunchLeft_Loaded() Handles Me.Loaded
        If IsLoad Then RefreshPage(True, False)
        If IsLoad Then Return
        IsLoad = True
        AniControlEnabled += 1

        'Launch button
        AddHandler McInstanceListLoader.LoadingStateChanged, AddressOf RefreshButtonsUI
        AddHandler McFolderListLoader.LoadingStateChanged, AddressOf RefreshButtonsUI
        RefreshButtonsUI()

        'Load versions
        RunInNewThread(
        Sub()
            'Auto modpack install: prepare
            Dim PackInstallPath As String = Nothing
            If FileUtils.Exists(Paths.Base & "modpack.zip") Then PackInstallPath = Paths.Base & "modpack.zip"
            If FileUtils.Exists(Paths.Base & "modpack.mrpack") Then PackInstallPath = Paths.Base & "modpack.mrpack"
            If PackInstallPath IsNot Nothing Then
                Logger.Warn($"需自动安装整合包：{PackInstallPath}")
                PageSelectLeft.CreateMcFolderInCurrentPath()
                McFolderListLoader.WaitForExit()
            End If
            'Ensure the Minecraft folder exists
            If McFolderSelected = "" OrElse Not DirectoryUtils.Exists(McFolderSelected) Then
                'Invalid folder
                If McFolderSelected = "" Then
                    Logger.Info("没有已储存的 Minecraft 文件夹")
                Else
                    Logger.Warn($"Minecraft 文件夹无效，该文件夹已不存在：{McFolderSelected}")
                End If
                McFolderListLoader.WaitForExit(IsForceRestart:=True)
                McFolderSelected = McFolderList.First.Location
            End If
            'Auto modpack install
            If PackInstallPath IsNot Nothing Then
                Try
                    Dim InstallLoader = ModpackInstall(PackInstallPath)
                    Logger.Info($"自动安装整合包已开始：{PackInstallPath}")
                    InstallLoader.WaitForExit()
                    If InstallLoader.State = LoadState.Finished Then
                        Logger.Info($"自动安装整合包成功，清理安装包：{PackInstallPath}")
                        FileUtils.Delete(PackInstallPath)
                    End If
                Catch ex As Exception
                    If ex.IsCanceled Then
                        Logger.Info($"自动安装整合包已取消：{PackInstallPath}")
                    Else
                        Logger.Error(ex, $"自动安装整合包失败：{PackInstallPath}", LogBehavior.Alert)
                    End If
                End Try
            End If
            'Ensure the Minecraft version exists
            Dim Selection As String = ReadIni(McFolderSelected & "PCL.ini", "Version")
            Dim Instance As McInstance = If(Selection = "", Nothing, New McInstance(Selection))
            If Instance Is Nothing OrElse Not PathUtils.IsParentOf(McFolderSelected, Instance.PathVersion) OrElse Not Instance.Check() Then
                'Invalid version
                Logger.Info($"当前选择的 Minecraft 版本无效：{If(Instance Is Nothing, "null", Instance.PathVersion)}", If(IsNothing(Instance), LogBehavior.None, LogBehavior.ToastIfDebug))
                If Not McInstanceListLoader.State = LoadState.Finished Then LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\", WaitForExit:=True)
                If Not McInstanceList.Any() OrElse McInstanceList.First.Value(0).Logo.Contains("RedstoneBlock") Then
                    Instance = Nothing
                    Logger.Info("无可用 Minecraft 版本")
                Else
                    Instance = McInstanceList.First.Value(0)
                    Logger.Info($"自动选择 Minecraft 版本：{Instance.PathVersion}")
                End If
            End If
            RunInUi(
            Sub()
                McInstanceSelected = Instance 'This detour avoids McVersionCheck firing a second version change
                IsLoadFinished = True
                RefreshButtonsUI()
                RefreshPage(False, False) 'The selected version may have changed, so refresh again
                If McLoginAble() = "" Then McLoginLoader.Start() 'Auto login
                'Used by automated tests to check that the generated program runs
                If Environment.CommandLine.Contains("--test") Then
                    RunInThread(
                    Sub()
                        Thread.Sleep(500)
                        RunInUi(Sub() FrmMain.PageChange(FormMain.PageType.Download))
                        Thread.Sleep(500)
                        RunInUi(Sub() FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadMod))
                        Thread.Sleep(500)
                        RunInUi(Sub() FrmMain.PageChange(FormMain.PageType.Setup))
                        Thread.Sleep(500)
                        RunInUi(Sub() BtnVersion_Click())
                        Thread.Sleep(500)
                        RunInUi(Sub() BtnMore_Click())
                        Thread.Sleep(500)
                        FormMain.EndProgramForce(123)
                    End Sub)
                End If
            End Sub)
        End Sub, "Version Check", ThreadPriority.AboveNormal)

        'Change page
        Dim LoginType = Settings.Get(Of McLoginType)("LoginType")
        If LoginType = McLoginType.Legacy OrElse LoginType = McLoginType.Ms Then CType(FindName("RadioLoginType" & LoginType), MyRadioButton).Checked = True
        RefreshPage(False, False)

        AniControlEnabled -= 1
    End Sub

#Region "Switch main page"

    ''' <summary>
    ''' Switch to the launching page.
    ''' </summary>
    Public Sub PageChangeToLaunching()
        'Update the login method label
        Select Case Settings.Get(Of McLoginType)("LoginType")
            Case McLoginType.Legacy
                LabLaunchingMethod.Text = "离线登录"
            Case McLoginType.Ms
                LabLaunchingMethod.Text = "正版登录"
            Case McLoginType.Nide
                LabLaunchingMethod.Text = "统一通行证"
            Case McLoginType.Auth
                LabLaunchingMethod.Text = "Authlib-Injector"
        End Select
        'Initialize the page
        LabLaunchingName.Text = McInstanceSelected.Name
        LabLaunchingStage.Text = "初始化"
        LabLaunchingTitle.Text = If(CurrentLaunchOptions?.SaveBatch Is Nothing, "正在启动游戏", "正在导出启动脚本")
        LabLaunchingProgress.Text = "0.00 %"
        LabLaunchingProgress.Opacity = 1
        LabLaunchingDownload.Visibility = Visibility.Visible
        LabLaunchingProgressLeft.Opacity = 0.6
        LabLaunchingDownload.Visibility = Visibility.Visible
        LabLaunchingDownload.Text = "0 B/s"
        LabLaunchingDownload.Opacity = 0
        LabLaunchingDownload.Visibility = Visibility.Collapsed
        LabLaunchingDownloadLeft.Opacity = 0
        LabLaunchingDownloadLeft.Visibility = Visibility.Collapsed
        ProgressLaunchingFinished.Width = New GridLength(0, GridUnitType.Star)
        ProgressLaunchingUnfinished.Width = New GridLength(1, GridUnitType.Star)
        PanLaunchingHint.Opacity = 0
        PanLaunchingHint.Visibility = Visibility.Collapsed
        PanLaunchingInfo.Width = Double.NaN 'Reset the width-change animation
        McLaunchProcess = Nothing
        McLaunchWatcher = Nothing
        'Fetch the “你知道吗” tip
        LabLaunchingHint.Text = "请耐心等待游戏启动。"
        'Initialize the other pages
        PanInput.IsHitTestVisible = False
        PanLaunching.IsHitTestVisible = False
        LoadLaunching.State.LoadingState = MyLoading.MyLoadingState.Run
        PanLaunching.Visibility = Visibility.Visible
        AniStart({
                AaOpacity(PanInput, 0, 50), 'Short delay so a failed pre-check does not flash a stray animation
                AaOpacity(PanInput, -PanInput.Opacity, 110, , New AniEaseInFluent, True),
                AaScaleTransform(PanInput, 1.2 - CType(PanInput.RenderTransform, ScaleTransform).ScaleX, 160),
                AaOpacity(PanLaunching, 1 - PanLaunching.Opacity, 150, 100),
                AaScaleTransform(PanLaunching, 1 - CType(PanLaunching.RenderTransform, ScaleTransform).ScaleX, 500, 100, New AniEaseOutBack(AniEasePower.Weak)),
                AaCode(Sub() PanLaunching.IsHitTestVisible = True, 150)
            }, "Launch State Page")
    End Sub
    ''' <summary>
    ''' Switch to the login page.
    ''' </summary>
    Public Sub PageChangeToLogin()
        PageGet(PageCurrent).Reload(KeepInput:=False)
        PanInput.IsHitTestVisible = False
        PanLaunching.IsHitTestVisible = False
        LoadLaunching.State.LoadingState = MyLoading.MyLoadingState.Stop
        PanInput.Visibility = Visibility.Visible
        AniStart({
            AaOpacity(PanLaunching, -PanLaunching.Opacity, 150),
            AaScaleTransform(PanLaunching, 0.8 - CType(PanLaunching.RenderTransform, ScaleTransform).ScaleX, 150,, New AniEaseOutFluent(AniEasePower.Weak)),
            AaOpacity(PanInput, 1 - PanInput.Opacity, 250, 50),
            AaScaleTransform(PanInput, 1 - CType(PanInput.RenderTransform, ScaleTransform).ScaleX, 300, 50, New AniEaseOutBack(AniEasePower.Weak)),
            AaCode(Sub() PanInput.IsHitTestVisible = True, 200)
        }, "Launch State Page", True)
    End Sub

#End Region

#Region "Switch login page"

    Private Enum PageType
        None
        Legacy
        Nide
        NideSkin
        Auth
        AuthSkin
        Ms
        MsSkin
    End Enum
    ''' <summary>
    ''' Kind of the current page.
    ''' </summary>
    Private PageCurrent As PageType = PageType.None

    Private Function PageGet(Type As PageType)
        Select Case Type
            Case PageType.Legacy
                If IsNothing(FrmLoginLegacy) Then FrmLoginLegacy = New PageLoginLegacy
                Return FrmLoginLegacy
            Case PageType.Nide
                If IsNothing(FrmLoginNide) Then FrmLoginNide = New PageLoginNide
                Return FrmLoginNide
            Case PageType.NideSkin
                If IsNothing(FrmLoginNideSkin) Then FrmLoginNideSkin = New PageLoginNideSkin
                Return FrmLoginNideSkin
            Case PageType.Auth
                If IsNothing(FrmLoginAuth) Then FrmLoginAuth = New PageLoginAuth
                Return FrmLoginAuth
            Case PageType.AuthSkin
                If IsNothing(FrmLoginAuthSkin) Then FrmLoginAuthSkin = New PageLoginAuthSkin
                Return FrmLoginAuthSkin
            Case PageType.Ms
                If IsNothing(FrmLoginMs) Then FrmLoginMs = New PageLoginMs
                Return FrmLoginMs
            Case PageType.MsSkin
                If IsNothing(FrmLoginMsSkin) Then FrmLoginMsSkin = New PageLoginMsSkin
                Return FrmLoginMsSkin
            Case Else
                Throw New ArgumentOutOfRangeException("Type", "即将切换的登录分页编号越界")
        End Select
    End Function
    ''' <summary>
    ''' Switch the current login page kind and return the new page instance.
    ''' </summary>
    ''' <param name="Type">Kind of the new page.</param>
    ''' <param name="Anim">Whether to show the animation.</param>
    Private Function PageChange(Type As PageType, Anim As Boolean)
        Dim PageNew As Object = FrmLoginMs 'Seed a value so an exception during the switch does not cascade
        Try

#Region "Resolve and instantiate the target page"
            If PageCurrent = Type Then Return PageNew
            PageNew = PageGet(Type)
#End Region

#Region "Switch page"
            AniStop("FrmLogin PageChange")
            'Clear the page parent link
            If Not IsNothing(PageNew) AndAlso Not IsNothing(PageNew.Parent) Then PageNew.SetValue(ContentPresenter.ContentProperty, Nothing)
            If Anim Then
                'Animated
                Dispatcher.Invoke(
                Sub()
                    'Run the animation
                    AniStart({
                        AaOpacity(PanLogin, -PanLogin.Opacity, 100,, New AniEaseOutFluent),
                        AaCode(
                        Sub()
                            AniControlEnabled += 1
                            PanLogin.Children.Clear()
                            PanLogin.Children.Add(PageNew)
                            AniControlEnabled -= 1
                        End Sub, 100),
                        AaOpacity(PanLogin, 1, 100, 120, New AniEaseInFluent)
                    }, "FrmLogin PageChange")
                End Sub, DispatcherPriority.Render)
            Else
                'No animation
                AniControlEnabled += 1
                PanLogin.Children.Clear()
                PanLogin.Children.Add(PageNew)
                AniControlEnabled -= 1
            End If
#End Region

            PageCurrent = Type
            Return PageNew
        Catch ex As Exception
            Logger.Error(ex, $"切换登录分页失败（{Type}）")
            Return PageNew
        End Try
    End Function

    ''' <summary>
    ''' Ensure the visible subpage is correct and refresh it.
    ''' </summary>
    Public Sub RefreshPage(KeepInput As Boolean, Anim As Boolean)
        'Get the available page kind and write it back to the cache
        Dim Type As PageType
        Dim LoginPageType As Integer
        If McInstanceSelected IsNot Nothing Then
            LoginPageType = Settings.Get(Of Integer)("VersionServerLogin", Instance:=McInstanceSelected)
            'Cache this version's page kind so the next open still loads the right page when McInstanceSelected is empty
            Settings.Set("LoginPageType", LoginPageType)
        Else
            LoginPageType = Settings.Get(Of Integer)("LoginPageType")
        End If
        Select Case LoginPageType
            Case 0 'Microsoft or offline
UnknownType:
                If RadioLoginType5.Checked Then
                    If Settings.Get(Of String)("CacheMsV2Access") = "" Then
                        Type = PageType.Ms
                    Else
                        Type = PageType.MsSkin
                    End If
                    Settings.Set("LoginType", McLoginType.Ms)
                Else
                    Type = PageType.Legacy
                    Settings.Set("LoginType", McLoginType.Legacy)
                End If
                PanType.Visibility = Visibility.Visible
                PanTypeOne.Visibility = Visibility.Collapsed
                RadioLoginType5.Visibility = Visibility.Visible
                RadioLoginType0.Visibility = Visibility.Visible
            Case 1 'Microsoft only
                If Settings.Get(Of String)("CacheMsV2Access") = "" Then
                    Type = PageType.Ms
                Else
                    Type = PageType.MsSkin
                End If
                Settings.Set("LoginType", McLoginType.Ms)
                PanType.Visibility = Visibility.Collapsed
                PanTypeOne.Visibility = Visibility.Visible
                PathTypeOne.Data = (New GeometryConverter).ConvertFromString(Logo.IconButtonShield)
                LabTypeOne.Text = "正版登录"
                RadioLoginType5.Visibility = Visibility.Visible
                RadioLoginType0.Visibility = Visibility.Collapsed
            Case 2 'Offline only
                Type = PageType.Legacy
                Settings.Set("LoginType", McLoginType.Legacy)
                PanType.Visibility = Visibility.Collapsed
                PanTypeOne.Visibility = Visibility.Visible
                PathTypeOne.Data = (New GeometryConverter).ConvertFromString(Logo.IconButtonOffline)
                LabTypeOne.Text = "离线登录"
            Case 3 'Unified pass
                If Settings.Get(Of String)("CacheNideAccess") = "" Then
                    Type = PageType.Nide
                Else
                    Type = PageType.NideSkin
                End If
                Settings.Set("LoginType", McLoginType.Nide)
                PanType.Visibility = Visibility.Collapsed
                PanTypeOne.Visibility = Visibility.Visible
                PathTypeOne.Data = (New GeometryConverter).ConvertFromString(Logo.IconButtonCard)
                LabTypeOne.Text = "统一通行证登录"
            Case 4 'Authlib-Injector
                If Settings.Get(Of String)("CacheAuthAccess") = "" Then
                    Type = PageType.Auth
                Else
                    Type = PageType.AuthSkin
                End If
                Settings.Set("LoginType", McLoginType.Auth)
                PanType.Visibility = Visibility.Collapsed
                PanTypeOne.Visibility = Visibility.Visible
                PathTypeOne.Data = (New GeometryConverter).ConvertFromString(Logo.IconButtonCard)
                LabTypeOne.Text = If(McInstanceSelected Is Nothing, Settings.Get(Of String)("CacheAuthServerName"), Settings.Get(Of String)("VersionServerAuthName", Instance:=McInstanceSelected))
                If LabTypeOne.Text = "" Then LabTypeOne.Text = "第三方登录"
            Case Else
                Logger.Error($"未知的登录页面：{LoginPageType}", LogBehavior.Toast)
                GoTo UnknownType
        End Select
        'Refresh the page
        If PageCurrent = Type Then Return
        PageChange(Type, Anim).Reload(KeepInput)
        Dim Control As MyRadioButton = FindName("RadioLoginType" & Settings.Get(Of McLoginType)("LoginType"))
        If Control IsNot Nothing Then Control.Checked = True
    End Sub
    Private Sub RadioLoginType_Change(sender As Object, raiseByMouse As Boolean) Handles RadioLoginType0.Check, RadioLoginType5.Check
        If raiseByMouse Then RefreshPage(True, True)
    End Sub

#End Region

#Region "Skins"

    'Microsoft skin
    Public Shared SkinMs As New LoaderTask(Of (String, String), String)("Loader Skin Ms", AddressOf SkinMsLoad, AddressOf SkinMsInput, ThreadPriority.AboveNormal)
    Private Shared Function SkinMsInput() As (String, String)
        'Get the name
        Return (Settings.Get(Of String)("CacheMsV2Name"), Settings.Get(Of String)("CacheMsV2Uuid"))
    End Function
    Private Shared Sub SkinMsLoad(Data As LoaderTask(Of (String, String), String))
        'Clear the existing skin
        'Clearing the skin while setting input skips Load when the input is unchanged, so the skin never loads
        RunInUi(Sub() If FrmLoginMsSkin IsNot Nothing AndAlso FrmLoginMsSkin.Skin IsNot Nothing Then FrmLoginMsSkin.Skin.Clear())
        'Get the URL
        Dim UserName As String = Data.Input.Item1
        Dim Uuid As String = Data.Input.Item2
        If UserName = "" Then
            Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(UserName)) & ".png"
            Logger.Info("获取微软正版皮肤失败，ID 为空")
            GoTo Finish
        End If
        Try
            Dim Result As String = McSkinGetAddress(Uuid, "Ms")
            If Data.IsCanceled Then Throw New OperationCanceledException("当前任务已取消：" & UserName)
            Result = McSkinDownload(Result)
            If Data.IsCanceled Then Throw New OperationCanceledException("当前任务已取消：" & UserName)
            Data.Output = Result
        Catch ex As Exception
            If ex.IsCanceled Then
                Data.Output = ""
                Return
            ElseIf ex.GetDisplay(False).Contains("(429)") Then
                Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(UserName)) & ".png"
                Logger.Error($"获取正版皮肤失败（{UserName}）：获取皮肤太过频繁，请 5 分钟后再试！", LogBehavior.Toast)
            ElseIf ex.GetDisplay(False).Contains("未设置自定义皮肤") Then
                Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(UserName)) & ".png"
                Logger.Info("用户未设置自定义皮肤，跳过皮肤加载")
            Else
                Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(UserName)) & ".png"
                Logger.Error(ex, $"获取微软正版皮肤失败（{UserName}）", LogBehavior.Toast)
            End If
        End Try
Finish:
        'Refresh the display
        If FrmLoginMsSkin IsNot Nothing Then
            RunInUi(AddressOf FrmLoginMsSkin.Skin.Load)
        ElseIf Not Data.IsCanceled Then 'If already canceled, Input is cleared too, so do not refresh again
            Data.Input = Nothing 'Clear the input: the skin was not rendered, and Start skips rendering when the input is unchanged
        End If
    End Sub

    Public Shared SkinLegacy As New LoaderTask(Of (String, String), String)("Loader Skin Legacy", AddressOf SkinLegacyLoad, AddressOf SkinLegacyInput, ThreadPriority.AboveNormal)
    Private Shared Function SkinLegacyInput() As (String, String)
        If FrmLoginLegacy?.IsReloaded Then
            Return ("0", If(FrmLoginLegacy.ComboName.Text.Trim, ""))
        ElseIf Settings.Get(Of String)("LoginLegacyName") = "" Then
            Return ("0", "")
        Else
            Return ("0", If(Settings.Get(Of String)("LoginLegacyName").ToString.BeforeFirst("¨"), ""))
        End If
    End Function
    Private Shared Sub SkinLegacyLoad(Data As LoaderTask(Of (String, String), String))
        RunInUi(Sub() If FrmLoginLegacy IsNot Nothing AndAlso FrmLoginLegacy.Skin IsNot Nothing Then FrmLoginLegacy.Skin.Clear())
        Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(Data.Input.Item2)) & ".png"
        If FrmLoginLegacy IsNot Nothing Then
            RunInUi(AddressOf FrmLoginLegacy.Skin.Load)
        ElseIf Not Data.IsCanceled Then
            Data.Input = Nothing
        End If
    End Sub

    'Unified-pass skin
    Public Shared SkinNide As New LoaderTask(Of (String, String), String)("Loader Skin Nide", AddressOf SkinNideLoad, AddressOf SkinNideInput, ThreadPriority.AboveNormal)
    Private Shared Function SkinNideInput() As (String, String)
        'Get the name
        Return (Settings.Get(Of String)("CacheNideName"), Settings.Get(Of String)("CacheNideUuid"))
    End Function
    Private Shared Sub SkinNideLoad(Data As LoaderTask(Of (String, String), String))
        'Clear the existing skin
        'Clearing the skin while setting input skips Load when the input is unchanged, so the skin never loads
        RunInUi(Sub() If FrmLoginNideSkin IsNot Nothing AndAlso FrmLoginNideSkin.Skin IsNot Nothing Then FrmLoginNideSkin.Skin.Clear())
        'Get the URL
        Dim UserName As String = Data.Input.Item1
        Dim Uuid As String = Data.Input.Item2
        If UserName = "" Then
            Data.Output = PathImage & "Skins/" & McSkinSex(McLoginLegacyUuid(UserName)) & ".png"
            Logger.Info("获取统一通行证皮肤失败，ID 为空")
            GoTo Finish
        End If
        Try
            Dim Result As String = McSkinGetAddress(Uuid, "Nide")
            If Data.IsCanceled Then Throw New OperationCanceledException("当前任务已取消：" & UserName)
            Result = McSkinDownload(Result)
            If Data.IsCanceled Then Throw New OperationCanceledException("当前任务已取消：" & UserName)
            Data.Output = Result
        Catch ex As Exception
            If ex.IsCanceled Then
                Data.Output = ""
                Return
            ElseIf ex.GetDisplay(False).Contains("(429)") Then
                Data.Output = PathImage & "Skins/Steve.png"
                Logger.Error($"获取统一通行证皮肤失败（{UserName}）：获取皮肤太过频繁，请 5 分钟后再试！", LogBehavior.Toast)
            ElseIf ex.GetDisplay(False).Contains("未设置自定义皮肤") Then
                Data.Output = PathImage & "Skins/Steve.png"
                Logger.Info("用户未设置自定义皮肤，跳过皮肤加载")
            Else
                Data.Output = PathImage & "Skins/Steve.png"
                Logger.Error(ex, $"获取统一通行证皮肤失败（{UserName}）", LogBehavior.Toast)
            End If
        End Try
Finish:
        'Refresh the display
        If FrmLoginNideSkin IsNot Nothing Then
            RunInUi(AddressOf FrmLoginNideSkin.Skin.Load)
        ElseIf Not Data.IsCanceled Then 'If already canceled, Input is cleared too, so do not refresh again
            Data.Input = Nothing 'Clear the input: the skin was not rendered, and Start skips rendering when the input is unchanged
        End If
    End Sub

    'Authlib-Injector skin
    Public Shared SkinAuth As New LoaderTask(Of (String, String), String)("Loader Skin Auth", AddressOf SkinAuthLoad, AddressOf SkinAuthInput, ThreadPriority.AboveNormal)
    Private Shared Function SkinAuthInput() As (String, String)
        'Get the name
        Return (Settings.Get(Of String)("CacheAuthName"), Settings.Get(Of String)("CacheAuthUuid"))
    End Function
    Private Shared Sub SkinAuthLoad(Data As LoaderTask(Of (String, String), String))
        'Clear the existing skin
        'Clearing the skin while setting input skips Load when the input is unchanged, so the skin never loads
        RunInUi(Sub() If FrmLoginAuthSkin IsNot Nothing AndAlso FrmLoginAuthSkin.Skin IsNot Nothing Then FrmLoginAuthSkin.Skin.Clear())
        'Get the URL
        Dim UserName As String = Data.Input.Item1
        Dim UUID As String = Data.Input.Item2
        If UserName = "" Then
            Data.Output = PathImage & "Skins/Steve.png"
            Logger.Info("获取 Authlib-Injector 皮肤失败，ID 为空")
            GoTo Finish
        End If
        Try
            Dim Result As String = McSkinGetAddress(UUID, "Auth")
            If Data.IsCanceled Then Throw New OperationCanceledException("当前任务已取消：" & UserName)
            Result = McSkinDownload(Result)
            If Data.IsCanceled Then Throw New OperationCanceledException("当前任务已取消：" & UserName)
            Data.Output = Result
        Catch ex As Exception
            If ex.IsCanceled Then
                Data.Output = ""
                Return
            ElseIf ex.GetDisplay(False).Contains("(429)") Then
                Data.Output = PathImage & "Skins/Steve.png"
                Logger.Error($"获取 Authlib-Injector 皮肤失败（{UserName}）：获取皮肤太过频繁，请 5 分钟后再试！", LogBehavior.Toast)
            ElseIf ex.GetDisplay(False).Contains("未设置自定义皮肤") Then
                Data.Output = PathImage & "Skins/Steve.png"
                Logger.Info("用户未设置自定义皮肤，跳过皮肤加载")
            Else
                Data.Output = PathImage & "Skins/Steve.png"
                Logger.Error(ex, $"获取 Authlib-Injector 皮肤失败（{UserName}）", LogBehavior.Toast)
            End If
        End Try
Finish:
        'Refresh the display
        If FrmLoginAuthSkin IsNot Nothing Then
            RunInUi(AddressOf FrmLoginAuthSkin.Skin.Load)
        ElseIf Not Data.IsCanceled Then 'If already canceled, Input is cleared too, so do not refresh again
            Data.Input = Nothing 'Clear the input: the skin was not rendered, and Start skips rendering when the input is unchanged
        End If
    End Sub

    'All skin loaders
    'Must be declared after its items, or loading it earlier makes it mysteriously become Nothing
    Public Shared SkinLoaders As New List(Of LoaderTask(Of (String, String), String)) From {SkinMs, SkinLegacy, SkinNide, SkinAuth}

#End Region

    'Version select button
    Private Sub BtnVersion_Click() Handles BtnVersion.Click
        If McLaunchLoader.State = LoadState.Loading Then Return
        FrmMain.PageChange(FormMain.PageType.InstanceSelect)
    End Sub
    'Launch button
    Public Sub LaunchButtonClick() Handles BtnLaunch.Click
        If McLaunchLoader.State = LoadState.Loading OrElse Not BtnLaunch.IsEnabled OrElse
            (FrmMain.PageRight IsNot Nothing AndAlso FrmMain.PageRight.PageState <> MyPageRight.PageStates.ContentStay AndAlso FrmMain.PageRight.PageState <> MyPageRight.PageStates.ContentEnter) Then Return
        'Perform the launch
        If BtnLaunch.Text = "启动游戏" Then
            McLaunchStart()
        ElseIf BtnLaunch.Text = "下载游戏" Then
            FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall)
        End If
    End Sub
    Private BtnLaunchState As Integer = 0
    Private BtnLaunchInstance As McInstance = Nothing
    Public Sub RefreshButtonsUI() Handles BtnLaunch.Loaded
        If Not BtnLaunch.IsLoaded Then Return
        'Read the current state
        Dim CurrentState As Integer
        If (Not IsLoadFinished) OrElse McInstanceListLoader.State = LoadState.Loading OrElse McFolderListLoader.State = LoadState.Loading Then
            CurrentState = 0
        Else
            If McInstanceSelected Is Nothing Then
                CurrentState = 2
            Else
                CurrentState = 3
            End If
        End If
        'Update the state
        If CurrentState = BtnLaunchState AndAlso
           If(McInstanceSelected Is Nothing, "", McInstanceSelected.PathVersion) = If(BtnLaunchInstance Is Nothing, "", BtnLaunchInstance.PathVersion) Then GoTo ExitRefresh
        BtnLaunchInstance = McInstanceSelected
        BtnLaunchState = CurrentState
        Select Case CurrentState
            Case 0
                Logger.Info("启动按钮：正在加载 Minecraft 版本")
                FrmLaunchLeft.BtnLaunch.Text = "正在加载"
                FrmLaunchLeft.BtnLaunch.IsEnabled = False
                FrmLaunchLeft.LabVersion.Text = "正在加载中，请稍候"
                FrmLaunchLeft.BtnVersion.IsEnabled = False
                FrmLaunchLeft.BtnMore.Visibility = Visibility.Collapsed
            Case 2
                Logger.Info("启动按钮：无 Minecraft 版本，要求下载")
                FrmLaunchLeft.BtnLaunch.Text = "下载游戏"
                FrmLaunchLeft.BtnLaunch.IsEnabled = True
                FrmLaunchLeft.LabVersion.Text = "未找到可用的游戏版本"
                FrmLaunchLeft.BtnVersion.IsEnabled = True
                FrmLaunchLeft.BtnMore.Visibility = Visibility.Collapsed
            Case 3
                Logger.Info($"启动按钮：Minecraft 版本：{McInstanceSelected.PathVersion}")
                FrmLaunchLeft.BtnLaunch.Text = "启动游戏"
                FrmLaunchLeft.BtnVersion.IsEnabled = True
                FrmLaunchLeft.BtnLaunch.IsEnabled = True
                FrmLaunchLeft.LabVersion.Text = McInstanceSelected.Name
        End Select
ExitRefresh:
        FrmLaunchLeft.BtnVersion.Visibility = Visibility.Visible
        If CurrentState = 3 Then
            FrmLaunchLeft.BtnMore.Visibility = FrmLaunchLeft.BtnVersion.Visibility
        End If
    End Sub
    'Cancel button
    Public Sub BtnCancel_Click() Handles BtnCancel.Click
        If McLaunchLoaderReal IsNot Nothing Then
            McLaunchLoaderReal.Cancel()
            McLaunchLog("已取消启动")
            Try
                If McLaunchWatcher IsNot Nothing Then
                    McLaunchWatcher.Kill()
                ElseIf McLaunchProcess IsNot Nothing Then
                    If Not McLaunchProcess.HasExited Then McLaunchProcess.Kill()
                End If
            Catch ex As Exception
                Logger.Error(ex, "取消启动结束进程失败", LogBehavior.Toast)
            End Try
        End If
    End Sub
    'Version settings button
    Private Sub BtnMore_Click() Handles BtnMore.Click
        If McLaunchLoader.State = LoadState.Loading Then Return
        If McInstanceLock.IsLocked(McInstanceSelected) Then
            Hint("该版本正在被修改，暂时无法进入版本设置！", HintType.Red)
            Return
        End If
        McInstanceSelected.Load()
        PageInstanceLeft.Instance = McInstanceSelected
        FrmMain.PageChange(FormMain.PageType.InstanceSetup, 0)
    End Sub
    ''' <summary>
    ''' Runs every 0.1s to refresh the launch data UI.
    ''' </summary>
    Public Sub LaunchingRefresh()
        Try
            If McLaunchLoaderReal.State = LoadState.Canceled Then Return
            'Read the stage state
            Dim IsLaunched As Boolean = False 'Whether the game has launched and is only waiting for the window
            Try
                For Each Loader In McLaunchLoaderReal.GetLoaderList(False)
                    If Loader.State = LoadState.Loading OrElse Loader.State = LoadState.Waiting Then
                        LabLaunchingStage.Text = Loader.Name
                        IsLaunched = Loader.Name = "等待游戏窗口出现" OrElse Loader.Name = "结束处理"
                        Exit Try
                    End If
                Next
                LabLaunchingStage.Text = "已完成"
            Catch ex As Exception
                Logger.Warn(ex, "获取是否启动完成失败，可能是由于启动状态改变导致集合已修改")
                Return
            End Try
            LabLaunchingTitle.Text = If(IsLaunched OrElse McLaunchLoaderReal.State = LoadState.Finished,
                "已启动游戏",
                If(CurrentLaunchOptions.SaveBatch Is Nothing, "正在启动游戏", "正在导出启动脚本"))
            If AniIsRun("Launch State Page") Then IsLaunched = False 'Wait for the page-switch animation to finish
            'Update progress
            Dim ActualProgress = McLaunchLoaderReal.Progress
            If ActualProgress >= ShowProgress Then ShowProgress += (ActualProgress - ShowProgress) * 0.1 + 0.0025 'Ease a bit toward the actual progress
            If ActualProgress <= ShowProgress Then ShowProgress = ActualProgress 'If it was or became ahead of the actual progress, snap back
            If IsLaunched Then ShowProgress = 1 'If already finished, skip the tease and jump to the end
            LabLaunchingProgress.Text = (ShowProgress * 100).ToString("0.00") & " %"
            'Update the download speed
            Dim HasLaunchDownloader As Boolean = False
            Try
                If NetManager.Speed = 0 AndAlso LabLaunchingDownload.Visibility = Visibility.Collapsed Then
                    'May only be checking files, with no real download
                    HasLaunchDownloader = False
                Else
                    For Each Loader In NetManager.Tasks
                        If Loader.RealParent IsNot Nothing AndAlso Loader.RealParent.Name = "Minecraft 启动" AndAlso Loader.State = LoadState.Loading Then HasLaunchDownloader = True
                    Next
                End If
            Catch ex As Exception
                Logger.Warn(ex, "获取 Minecraft 启动下载器失败，可能是因为启动被取消")
                HasLaunchDownloader = False
            End Try
            LabLaunchingDownload.Text = StringUtils.FormatByteSize(NetManager.Speed) & "/s"
            'Progress-change animation
            Dim AnimList As New List(Of AniData) From {
                 AaGridLengthWidth(ProgressLaunchingFinished, ShowProgress - ProgressLaunchingFinished.Width.Value, 130,, New AniEaseOutFluent),
                 AaGridLengthWidth(ProgressLaunchingUnfinished, 1 - ShowProgress - ProgressLaunchingUnfinished.Width.Value, 130,, New AniEaseOutFluent)
            }
            If HasLaunchDownloader = (LabLaunchingDownload.Visibility = Visibility.Collapsed) Then 'IsDownloadStateChanged
                LabLaunchingDownload.Visibility = Visibility.Visible
                LabLaunchingDownloadLeft.Visibility = Visibility.Visible
                AnimList.AddRange({
                    AaOpacity(LabLaunchingDownload, If(HasLaunchDownloader, 1, 0) - LabLaunchingDownload.Opacity, 90),
                    AaOpacity(LabLaunchingDownloadLeft, If(HasLaunchDownloader, 0.5, 0) - LabLaunchingDownloadLeft.Opacity, 90),
                    AaCode(
                    Sub()
                        If Not HasLaunchDownloader Then
                            LabLaunchingDownload.Visibility = Visibility.Collapsed
                            LabLaunchingDownloadLeft.Visibility = Visibility.Collapsed
                        End If
                    End Sub, 110)
                })
            End If
            If (Not IsLaunched) = (LabLaunchingProgress.Visibility = Visibility.Collapsed) Then 'IsProgressStateChanged
                LabLaunchingProgress.Visibility = Visibility.Visible
                LabLaunchingProgressLeft.Visibility = Visibility.Visible
                If IsLaunched Then PanLaunchingHint.Visibility = Visibility.Visible
                AnimList.AddRange({
                    AaOpacity(LabLaunchingProgress, If(Not IsLaunched, 1, 0) - LabLaunchingProgress.Opacity, 90),
                    AaOpacity(LabLaunchingProgressLeft, If(Not IsLaunched, 0.5, 0) - LabLaunchingProgressLeft.Opacity, 90),
                    AaOpacity(PanLaunchingHint, If(IsLaunched, 1, 0) - PanLaunchingHint.Opacity, 90)
                })
            End If
            AniStart(AnimList, "Launching Progress")
        Catch ex As Exception
            Logger.Error(ex, "刷新启动信息失败")
        End Try
    End Sub
    Private ShowProgress As Double = 0
    'Size-change animation
    Private IsWidthAnimating As Boolean = False
    Private ActualUsedWidth As Double
    Private Sub PanLaunchingInfo_SizeChangedW(sender As Object, e As SizeChangedEventArgs) Handles PanLaunchingInfo.SizeChanged
        Dim DeltaWidth As Double = e.NewSize.Width - e.PreviousSize.Width
        If e.PreviousSize.Width = 0 OrElse IsWidthAnimating OrElse Math.Abs(DeltaWidth) < 1 OrElse PanLaunchingInfo.ActualWidth = 0 Then Return
        AniStart({
            AaWidth(PanLaunchingInfo, DeltaWidth, 180,, New AniEaseOutFluent),
            AaCode(Sub()
                       IsWidthAnimating = False
                       PanLaunchingInfo.Width = ActualUsedWidth
                   End Sub,, True)
        }, "Launching Info Width")
        IsWidthAnimating = True
        ActualUsedWidth = PanLaunchingInfo.Width
        PanLaunchingInfo.Width = e.PreviousSize.Width
    End Sub
    Private IsHeightAnimating As Boolean = False
    Private ActualUsedHeight As Double
    Private Sub PanLaunchingInfo_SizeChangedH(sender As Object, e As SizeChangedEventArgs) Handles PanLaunchingInfo.SizeChanged
        Dim DeltaHeight As Double = e.NewSize.Height - e.PreviousSize.Height
        If e.PreviousSize.Height = 0 OrElse IsHeightAnimating OrElse Math.Abs(DeltaHeight) < 1 OrElse PanLaunchingInfo.ActualHeight = 0 Then Return
        AniStart({
            AaHeight(PanLaunchingInfo, DeltaHeight, 180,, New AniEaseOutFluent),
            AaCode(Sub()
                       IsHeightAnimating = False
                       PanLaunchingInfo.Height = ActualUsedHeight
                   End Sub,, True)
        }, "Launching Info Height")
        IsHeightAnimating = True
        ActualUsedHeight = PanLaunchingInfo.Height
        PanLaunchingInfo.Height = e.PreviousSize.Height
    End Sub

End Class
