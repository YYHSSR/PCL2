Public Module ModLaunch

    Public Const LegacyDefaultJvmArgs As String = "-XX:-OmitStackTraceInFastThrow -Djdk.lang.Process.allowAmbiguousCommands=True -Dfml.ignorePatchDiscrepancies=True"

#Region "Start"

    Public CurrentLaunchOptions As McLaunchOptions = Nothing
    Public Class McLaunchOptions
        ''' <summary>
        ''' Forces the server IP to join after launching.
        ''' Default: Nothing, which uses the instance setting.
        ''' </summary>
        Public ServerIp As String = Nothing
        ''' <summary>
        ''' Saves the launch script to this path and then cancels the launch. This also changes the launch prompts.
        ''' Default: Nothing, which does not save.
        ''' </summary>
        Public SaveBatch As String = Nothing
        ''' <summary>
        ''' Forces the Minecraft instance to launch.
        ''' Default: Nothing, which uses McInstanceCurrent.
        ''' </summary>
        Public Instance As McInstance = Nothing
        ''' <summary>
        ''' Extra launch arguments.
        ''' </summary>
        Public ExtraGameArgs As New List(Of String)
    End Class
    ''' <summary>
    ''' Tries to launch Minecraft. Must be called on the UI thread.
    ''' Returns whether the launch actually started (if not, an error prompt has always been shown).
    ''' </summary>
    Public Function McLaunchStart(Optional Options As McLaunchOptions = Nothing) As Boolean
        'Pre-checks
        If Not RunInUi() Then Throw New Exception("McLaunchStart 必须在 UI 线程调用！")
        If McLaunchLoader.State = LoadState.Loading Then
            Hint("已有游戏正在启动中！", HintType.Red)
            Return False
        End If
        'Must be assigned only after confirming the launch can proceed, otherwise it would overwrite the options of a launch in progress
        CurrentLaunchOptions = If(Options, New McLaunchOptions)
        'Force switching to the instance to launch
        If CurrentLaunchOptions.Instance IsNot Nothing AndAlso McInstanceSelected <> CurrentLaunchOptions.Instance Then
            McLaunchLog("在启动前切换到版本 " & CurrentLaunchOptions.Instance.Name)
            'Check that the instance exists
            Try
                CurrentLaunchOptions.Instance.GetJsonPath()
            Catch
                Hint($"无法启动 {CurrentLaunchOptions.Instance.Name}：当前 Minecraft 文件夹中尚未安装该版本！", HintType.Red)
                Return False
            End Try
            'Check the instance
            CurrentLaunchOptions.Instance.Load()
            If CurrentLaunchOptions.Instance.State = McInstance.McInstanceState.Error Then
                Hint($"无法启动 {CurrentLaunchOptions.Instance.Name}：{CurrentLaunchOptions.Instance.Info}", HintType.Red)
                Return False
            End If
            'Switch instance
            McInstanceSelected = CurrentLaunchOptions.Instance
            FrmLaunchLeft.RefreshButtonsUI()
            FrmLaunchLeft.RefreshPage(False, False)
        End If
        If McInstanceSelected IsNot Nothing AndAlso McInstanceLock.IsLocked(McInstanceSelected) Then
            Hint($"版本 {McInstanceSelected.Name} 正在被修改，请稍后再启动！", HintType.Red)
            Return False
        End If
        'Block the instance selection page (otherwise McInstanceSelected could be changed mid-launch)
        FrmMain.PageStack = FrmMain.PageStack.Where(Function(p) p.Page <> FormMain.PageType.InstanceSelect).ToList
        'Actually start the loader
        McLaunchLoader.Start(Options, IsForceRestart:=True)
        Return True
    End Function

    ''' <summary>
    ''' Writes a launch log entry.
    ''' </summary>
    Public Sub McLaunchLog(Text As String, Optional Level As LogLevel = LogLevel.Info)
        Text = FilterUserName(FilterAccessToken(Text, "*"), "*")
        If ModeDebug Then
            RunInUi(
            Sub()
                FrmLaunchRight.LabLog.AppendText($"[{Date.Now:HH':'mm':'ss'.'fff}] {Text}{vbCrLf}")
            End Sub)
        End If
        Logger.Log(Text, Level)
    End Sub

    'Launch state switching
    Public McLaunchLoader As New LoaderTask(Of McLaunchOptions, Object)("Loader Launch", AddressOf McLaunchStart) With {.OnStateChanged = AddressOf McLaunchState}
    Public McLaunchLoaderReal As LoaderCombo(Of Object)
    Public McLaunchProcess As Process
    Public McLaunchWatcher As Watcher
    Private Sub McLaunchState(Loader As LoaderTask(Of McLaunchOptions, Object))
        Select Case McLaunchLoader.State
            Case LoadState.Finished, LoadState.Failed, LoadState.Waiting, LoadState.Canceled
                FrmLaunchLeft.PageChangeToLogin()
            Case LoadState.Loading
                'Trigger the animation only after pre-checks finish
                FrmLaunchRight.LabLog.Text = ""
        End Select
    End Sub
    ''' <summary>
    ''' Hint text shown when the launch is aborted. Shown in green if not Nothing.
    ''' </summary>
    Private CanceledHint As String = Nothing

    'The actual launch method
    Private Sub McLaunchStart(Loader As LoaderTask(Of McLaunchOptions, Object))
        'Start animation
        RunInUiWait(AddressOf FrmLaunchLeft.PageChangeToLaunching)
        'Pre-checks (errors are thrown directly)
        Try
            McLaunchPrecheck()
            McLaunchLog("预检测已通过")
        Catch ex As Exception
            If Not ex.IsCanceled Then Hint(ex.Message, HintType.Red)
            Throw
        End Try
        Dim IsSavingBatch As Boolean = CurrentLaunchOptions?.SaveBatch IsNot Nothing

        'Main loading
        Try
            'Build the main loader
            Dim Loaders As New List(Of LoaderBase) From {
                New LoaderTask(Of Integer, Integer)("获取 Java", AddressOf McLaunchJava) With {.ProgressWeight = 4, .Block = False},
                McLoginLoader, '.ProgressWeight = 15, .Block = False
                New LoaderCombo(Of String)("补全文件", DlClientFix(McInstanceSelected, False, True)) With {.ProgressWeight = 15, .Show = False},
                New LoaderTask(Of String, Integer)("获取启动参数", AddressOf McLaunchArgumentMain) With {.ProgressWeight = 2},
                New LoaderTask(Of Integer, Integer)("解压文件", AddressOf McLaunchNatives) With {.ProgressWeight = 2},
                New LoaderTask(Of Integer, Integer)("预启动处理", AddressOf McLaunchPrerun) With {.ProgressWeight = 1},
                New LoaderTask(Of Integer, Integer)("执行自定义命令", AddressOf McLaunchCustom) With {.ProgressWeight = 1},
                New LoaderTask(Of Integer, Process)("启动进程", AddressOf McLaunchRun) With {.ProgressWeight = 2},
                New LoaderTask(Of Process, Integer)("等待游戏窗口出现", AddressOf McLaunchWait) With {.ProgressWeight = 1},
                New LoaderTask(Of Integer, Integer)("结束处理", AddressOf McLaunchEnd) With {.ProgressWeight = 1}
            }
            Dim LaunchLoader As New LoaderCombo(Of Object)("Minecraft 启动", Loaders) With {.Show = False}
            If McLoginLoader.State = LoadState.Finished Then McLoginLoader.State = LoadState.Waiting 'Ask the main login loader to restart; it decides by itself whether to start the sub-loaders
            'Wait for the loader to run and update the UI
            McLaunchLoaderReal = LaunchLoader
            CanceledHint = Nothing
            LaunchLoader.Start()
            'Taskbar progress bar
            LoaderTaskbarAdd(LaunchLoader)
            Do While LaunchLoader.State = LoadState.Loading
                FrmLaunchLeft.Dispatcher.Invoke(AddressOf FrmLaunchLeft.LaunchingRefresh)
                Thread.Sleep(100)
            Loop
            FrmLaunchLeft.Dispatcher.Invoke(AddressOf FrmLaunchLeft.LaunchingRefresh)
            'Success and failure handling
            Select Case LaunchLoader.State
                Case LoadState.Finished
                    Hint(McInstanceSelected.Name & " 启动成功！", HintType.Green)

                Case LoadState.Canceled
                    If CanceledHint Is Nothing Then
                        Hint(If(IsSavingBatch, "已取消导出启动脚本！", "已取消启动！"), HintType.Blue)
                    Else
                        Hint(CanceledHint, HintType.Green)
                    End If
                Case LoadState.Failed
                    Throw LaunchLoader.Error
                Case Else
                    Throw New Exception("错误的状态改变：" & LaunchLoader.State.ToString())
            End Select
        Catch ex As Exception
            ex.ThrowIfCanceled()
            Dim CurrentEx = ex
NextInner:
            If CurrentEx.Message.StartsWithF("$") Then
                'If an error message starts with $, use it for the prompt
                MyMsgBox(CurrentEx.Message.TrimStart("$"), If(IsSavingBatch, "导出启动脚本失败", "启动失败"))
                Throw
            ElseIf CurrentEx.InnerException IsNot Nothing Then
                'Check the inner exception
                CurrentEx = CurrentEx.InnerException
                GoTo NextInner
            Else
                'Error message without special handling
                McLaunchLog("错误：" & ex.GetDisplay(True))
                Logger.Error(ex, If(IsSavingBatch, "导出启动脚本失败", "Minecraft 启动失败"), LogBehavior.Alert)

                Throw
            End If
        End Try
    End Sub

#End Region

#Region "Pre-checks"

    Private Sub McLaunchPrecheck()
        If McInstanceSelected Is Nothing Then Throw New Exception("未选择 Minecraft 版本！")
        'Check path
        If McInstanceSelected.PathIndie.Contains("!") OrElse McInstanceSelected.PathIndie.Contains(";") Then Throw New Exception("游戏路径中不可包含 ! 或 ;（" & McInstanceSelected.PathIndie & "）")
        If McInstanceSelected.PathVersion.Contains("!") OrElse McInstanceSelected.PathVersion.Contains(";") Then Throw New Exception("游戏路径中不可包含 ! 或 ;（" & McInstanceSelected.PathVersion & "）")
        'Check instance
        McInstanceSelected.Load()
        If McInstanceSelected.State = McInstance.McInstanceState.Error Then Throw New Exception("Minecraft 存在问题：" & McInstanceSelected.Info)
        'Check input
        Dim CheckResult As String = ""
        RunInUiWait(Sub() CheckResult = McLoginAble(McLoginInput()))
        If CheckResult <> "" Then Throw New ArgumentException(CheckResult)
        'Prompt to buy the official game
        If CurrentLaunchOptions?.SaveBatch Is Nothing AndAlso 'No prompt when saving a script
           Not Settings.Get(Of Boolean)("HintBuy") AndAlso Settings.Get(Of McLoginType)("LoginType") <> McLoginType.Ms Then
            If Globalization.CultureInfo.CurrentCulture.Name.StartsWithF("zh") OrElse Globalization.CultureInfo.CurrentUICulture.Name.StartsWithF("zh") Then 'Chinese locale?
                RunInNewThread(
                Sub()
                    Select Case Settings.Get(Of Integer)("SystemLaunchCount")
                        Case 3, 8, 15, 30, 50, 70, 90, 110, 130, 180, 220, 280, 330, 380, 450, 550, 660, 750, 880, 950, 1100, 1300, 1500, 1700, 1900
                            If MyMsgBox("你已经启动了 " & Settings.Get(Of Integer)("SystemLaunchCount") & " 次 Minecraft 啦！" & vbCrLf &
                                "如果觉得 Minecraft 还不错，可以购买正版支持一下，毕竟开发游戏也真的很不容易……不要一直白嫖啦。" & vbCrLf & vbCrLf &
                                "在登录一次正版账号后，就不会再出现这个提示了！",
                                "考虑一下正版？", "支持正版游戏！", "下次一定") = 1 Then
                                OpenWebsite("https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj")
                            End If
                    End Select
                End Sub, "Buy Minecraft")
            ElseIf Settings.Get(Of McLoginType)("LoginType") = McLoginType.Legacy Then
                Select Case MyMsgBox("你必须先登录正版账号，才能进行离线登录！", "正版验证", "购买正版", "试玩", "返回",
                    Button1Action:=Sub() OpenWebsite("https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj"))
                    Case 2
                        Hint("游戏将以试玩模式启动！", HintType.Red)
                        CurrentLaunchOptions.ExtraGameArgs.Add("--demo")
                    Case 3
                        Throw New OperationCanceledException
                End Select
            End If
        End If
    End Sub

#End Region

#Region "Main Login Module"

    'Login type
    Public Enum McLoginType
        Legacy = 0
        Nide = 2
        Auth = 3
        Ms = 5
    End Enum

    'Data for each login type
    Public MustInherit Class McLoginData
        ''' <summary>
        ''' Login type.
        ''' </summary>
        Public Type As McLoginType
        Public Overrides Function Equals(obj As Object) As Boolean
            Return obj IsNot Nothing AndAlso obj.GetHashCode() = GetHashCode()
        End Function
    End Class
    Public Class McLoginServer
        Inherits McLoginData

        ''' <summary>
        ''' Login username.
        ''' </summary>
        Public UserName As String
        ''' <summary>
        ''' Login password.
        ''' </summary>
        Public Password As String
        ''' <summary>
        ''' Base URL of the login server.
        ''' </summary>
        Public BaseUrl As String
        ''' <summary>
        ''' Identifier used for login, currently only "Auth" or "Nide"; used for cache storage etc.
        ''' </summary>
        Public Token As String
        ''' <summary>
        ''' Description of the login type, e.g. "正版" or "统一通行证".
        ''' </summary>
        Public Description As String
        ''' <summary>
        ''' Whether to force the player to reselect a profile in this login. Currently only affects Authlib-Injector.
        ''' </summary>
        Public ForceReselectProfile As Boolean = False

        Public Sub New(Type As McLoginType)
            Me.Type = Type
        End Sub
        Public Overrides Function GetHashCode() As Integer
            Return (UserName & Password & BaseUrl & Token & Type).GetStableHashCode() Mod Integer.MaxValue
        End Function

    End Class
    Public Class McLoginMs
        Inherits McLoginData

        ''' <summary>
        ''' Cached OAuth refresh token. Empty string if none.
        ''' </summary>
        Public OAuthRefreshToken As String = ""
        Public AccessToken As String = ""
        Public Uuid As String = ""
        Public UserName As String = ""
        Public ProfileJson As String = ""

        Public Sub New()
            Type = McLoginType.Ms
        End Sub
        Public Overrides Function GetHashCode() As Integer
            'Not all fields are needed; these two are enough to ensure it is the same user during login input
            Return (OAuthRefreshToken & UserName).GetStableHashCode() Mod Integer.MaxValue
        End Function
    End Class
    Public Class McLoginLegacy
        Inherits McLoginData
        ''' <summary>
        ''' Login username.
        ''' </summary>
        Public UserName As String

        Public Sub New()
            Type = McLoginType.Legacy
        End Sub
        Public Overrides Function GetHashCode() As Integer
            Return (UserName & Type).GetStableHashCode() Mod Integer.MaxValue
        End Function
    End Class

    'Login result
    Public Structure McLoginResult
        Public Name As String
        Public Uuid As String
        Public AccessToken As String
        Public Type As String
        Public ClientToken As String
        ''' <summary>
        ''' Profile info returned by Microsoft login.
        ''' </summary>
        Public ProfileJson As String
    End Structure

    ''' <summary>
    ''' Gets the player's Minecraft username from the login info. Returns Nothing if unavailable.
    ''' </summary>
    Public Function McLoginName() As String
        'Prefer the current login type
        Select Case Settings.Get(Of McLoginType)("LoginType")
            Case McLoginType.Ms
                If Settings.Get(Of String)("CacheMsV2Name") <> "" Then Return Settings.Get(Of String)("CacheMsV2Name")
            Case McLoginType.Legacy
                If Settings.Get(Of String)("LoginLegacyName") <> "" Then Return Settings.Get(Of String)("LoginLegacyName").ToString.BeforeFirst("¨")
            Case McLoginType.Nide
                If Settings.Get(Of String)("CacheNideName") <> "" Then Return Settings.Get(Of String)("CacheNideName")
            Case McLoginType.Auth
                If Settings.Get(Of String)("CacheAuthName") <> "" Then Return Settings.Get(Of String)("CacheAuthName")
        End Select
        'Search all possible entries
        If Settings.Get(Of String)("CacheMsV2Name") <> "" Then Return Settings.Get(Of String)("CacheMsV2Name")
        If Settings.Get(Of String)("CacheNideName") <> "" Then Return Settings.Get(Of String)("CacheNideName")
        If Settings.Get(Of String)("CacheAuthName") <> "" Then Return Settings.Get(Of String)("CacheAuthName")
        If Settings.Get(Of String)("LoginLegacyName") <> "" Then Return Settings.Get(Of String)("LoginLegacyName").ToString.BeforeFirst("¨")
        Return Nothing
    End Function
    ''' <summary>
    ''' Whether login is currently possible. Returns the error reason if not.
    ''' </summary>
    Public Function McLoginAble() As String
        Select Case Settings.Get(Of McLoginType)("LoginType")
            Case McLoginType.Ms
                If Settings.Get(Of String)("CacheMsV2OAuthRefresh") = "" Then
                    Return FrmLoginMs.IsVaild()
                Else
                    Return ""
                End If
            Case McLoginType.Legacy
                Return FrmLoginLegacy.IsVaild()
            Case McLoginType.Nide
                If Settings.Get(Of String)("CacheNideAccess") = "" Then
                    Return FrmLoginNide.IsVaild()
                Else
                    Return ""
                End If
            Case McLoginType.Auth
                If Settings.Get(Of String)("CacheAuthAccess") = "" Then
                    Return FrmLoginAuth.IsVaild()
                Else
                    Return ""
                End If
            Case Else
                Return "未知的登录方式"
        End Select
    End Function
    ''' <summary>
    ''' Whether the login input can be used to log in. Returns the error reason if not.
    ''' </summary>
    Public Function McLoginAble(LoginData As McLoginData) As String
        Select Case LoginData.Type
            Case McLoginType.Ms
                Return PageLoginMs.IsVaild(LoginData)
            Case McLoginType.Legacy
                Return PageLoginLegacy.IsVaild(LoginData)
            Case McLoginType.Nide
                Return PageLoginNide.IsVaild(LoginData)
            Case McLoginType.Auth
                Return PageLoginAuth.IsVaild(LoginData)
            Case Else
                Return "未知的登录方式"
        End Select
    End Function

    'Main login module loader
    Public McLoginLoader As New LoaderTask(Of McLoginData, McLoginResult)("登录", AddressOf McLoginStart, AddressOf McLoginInput, ThreadPriority.BelowNormal) With
        {.ReloadTimeout = 1, .ProgressWeight = 15, .Block = False}
    Public Function McLoginInput() As McLoginData
        Dim LoginData As McLoginData = Nothing
        Dim LoginType = Settings.Get(Of McLoginType)("LoginType")
        Try
            Select Case LoginType
                Case McLoginType.Legacy
                    LoginData = PageLoginLegacy.GetLoginData()
                Case McLoginType.Ms
                    If Settings.Get(Of String)("CacheMsV2OAuthRefresh") = "" Then
                        LoginData = PageLoginMs.GetLoginData()
                    Else
                        LoginData = PageLoginMsSkin.GetLoginData()
                    End If
                Case McLoginType.Nide
                    If Settings.Get(Of String)("CacheNideAccess") = "" Then
                        LoginData = PageLoginNide.GetLoginData()
                    Else
                        LoginData = PageLoginNideSkin.GetLoginData()
                    End If
                Case McLoginType.Auth
                    If Settings.Get(Of String)("CacheAuthAccess") = "" Then
                        LoginData = PageLoginAuth.GetLoginData()
                    Else
                        LoginData = PageLoginAuthSkin.GetLoginData()
                    End If
            End Select
            Return LoginData
        Catch ex As Exception
            Throw New Exception($"获取登录输入信息失败（{LoginType}）", ex)
        End Try
    End Function
    Private Sub McLoginStart(Data As LoaderTask(Of McLoginData, McLoginResult))
        McLaunchLog("登录加载已开始")
        'Validate login info
        Dim CheckResult As String = McLoginAble(Data.Input)
        If Not CheckResult = "" Then Throw New ArgumentException(CheckResult)
        'Get the matching loader
        Dim Loader As LoaderBase = Nothing
        Select Case Data.Input.Type
            Case McLoginType.Ms
                Loader = McLoginMsLoader
            Case McLoginType.Legacy
                Loader = McLoginLegacyLoader
            Case McLoginType.Nide
                Loader = McLoginNideLoader
            Case McLoginType.Auth
                Loader = McLoginAuthLoader
        End Select
        'Try to load
        Loader.WaitForExit(Data.Input, McLoginLoader, Data.IsForceRestarting)
        Data.Output = CType(Loader, Object).Output
        RunInUi(Sub() FrmLaunchLeft.RefreshPage(True, False)) 'Refresh the autofill list
        McLaunchLog("登录加载已结束")
    End Sub

#End Region
#Region "Per-type Login Modules"

    'Main objects and input construction for each login type
    Public McLoginMsLoader As New LoaderTask(Of McLoginMs, McLoginResult)("Loader Login Ms", AddressOf McLoginMsStart) With {.ReloadTimeout = 1}
    Public McLoginLegacyLoader As New LoaderTask(Of McLoginLegacy, McLoginResult)("Loader Login Legacy", AddressOf McLoginLegacyStart)
    Public McLoginNideLoader As New LoaderTask(Of McLoginServer, McLoginResult)("Loader Login Nide", AddressOf McLoginServerStart) With {.ReloadTimeout = 1000 * 60 * 10}
    Public McLoginAuthLoader As New LoaderTask(Of McLoginServer, McLoginResult)("Loader Login Auth", AddressOf McLoginServerStart) With {.ReloadTimeout = 1000 * 60 * 10}

    'Main load function; returns all required login info
    Private Sub McLoginMsStart(Data As LoaderTask(Of McLoginMs, McLoginResult))
        Dim Input As McLoginMs = Data.Input
        Dim LogUsername As String = Input.UserName
        McLaunchLog("登录方式：正版（" & If(LogUsername = "", "尚未登录", LogUsername) & "）")
        Data.Progress = 0.05
        'Check whether login has already completed
        Dim ExpiresAt = Settings.Get(Of Long)("CacheMsV2Expires")
        If Not Data.IsForceRestarting AndAlso 'Not forced to restart
           ExpiresAt > 0 AndAlso ExpiresAt > ToUnixTimestampUtc(Date.UtcNow) AndAlso 'AccessToken not yet expired
           Input.UserName = Settings.Get(Of String)("CacheMsV2Name") Then
            Data.Output = New McLoginResult With {
                .Name = Input.UserName, .Type = "Microsoft",
                .AccessToken = Settings.Get(Of String)("CacheMsV2Access"),
                .Uuid = Settings.Get(Of String)("CacheMsV2Uuid"),
                .ClientToken = Settings.Get(Of String)("CacheMsV2Uuid"),
                .ProfileJson = Settings.Get(Of String)("CacheMsV2ProfileJson")
            }
            McLaunchLog("无需登录，AccessToken 尚未过期")
            GoTo SkipLogin
        End If
        'Try to log in
        Dim OAuthTokens As (OAuthAccessToken As String, OAuthRefreshToken As String)
        If Input.OAuthRefreshToken = "" Then
            'No refresh token
Relogin:
            OAuthTokens = MsLoginStep1New(Data)
        Else
            'Has refresh token
            OAuthTokens = MsLoginStep1Refresh(Input.OAuthRefreshToken)
            If OAuthTokens.OAuthAccessToken = "Relogin" Then GoTo Relogin 'Requires reopening the login web page to authenticate
        End If
        If Data.IsCanceled Then Throw New OperationCanceledException
        Data.Progress = 0.25
        If Data.IsCanceled Then Throw New OperationCanceledException
        Dim OAuthAccessToken As String = OAuthTokens.OAuthAccessToken
        Dim OAuthRefreshToken As String = OAuthTokens.OAuthRefreshToken
        Dim XBLToken As String = MsLoginStep2(OAuthAccessToken)
        Data.Progress = 0.4
        If Data.IsCanceled Then Throw New OperationCanceledException
        Dim Tokens = MsLoginStep3(XBLToken)
        Data.Progress = 0.55
        If Data.IsCanceled Then Throw New OperationCanceledException
        Dim LoginResult = MsLoginStep4(Tokens)
        ExpiresAt = LoginResult.ExpiresAt
        Data.Progress = 0.7
        If Data.IsCanceled Then Throw New OperationCanceledException
        MsLoginStep5(LoginResult.AccessToken)
        Data.Progress = 0.85
        If Data.IsCanceled Then Throw New OperationCanceledException
        Dim Result = MsLoginStep6(LoginResult.AccessToken)
        Data.Progress = 0.98
        'Output the login result
        Settings.Set("CacheMsV2OAuthRefresh", OAuthRefreshToken)
        Settings.Set("CacheMsV2Access", LoginResult.AccessToken)
        Settings.Set("CacheMsV2Uuid", Result.UUID)
        Settings.Set("CacheMsV2Name", Result.UserName)
        Settings.Set("CacheMsV2ProfileJson", Result.ProfileJson)
        Settings.Set("CacheMsV2Expires", LoginResult.ExpiresAt)
        Dim MsJson As JObject = Settings.Get(Of String)("LoginMsJson").DeserializeJson()
        MsJson.Remove(Input.UserName) 'If the player name was changed...
        MsJson(Result.UserName) = OAuthRefreshToken
        Settings.Set("LoginMsJson", MsJson.ToString(Newtonsoft.Json.Formatting.None))
        Data.Output = New McLoginResult With {.AccessToken = LoginResult.AccessToken, .Name = Result.UserName, .Uuid = Result.UUID, .Type = "Microsoft", .ClientToken = Result.UUID, .ProfileJson = Result.ProfileJson}
SkipLogin:
        McLaunchLog($"微软登录结束，AccessToken 将在 {DateTimeOffset.FromUnixTimeSeconds(ExpiresAt).LocalDateTime} 过期")
        Settings.Set("HintBuy", True) 'Disable the buy-the-game prompt
    End Sub
    Private Sub McLoginServerStart(Data As LoaderTask(Of McLoginServer, McLoginResult))
        Dim Input As McLoginServer = Data.Input
        Dim NeedRefresh As Boolean = False, WasRefreshed As Boolean = False
        McLaunchLog("登录方式：" & Input.Description)
        Data.Progress = 0.05
        'Try to log in
        If (Not Data.Input.ForceReselectProfile) AndAlso
            Settings.Get(Of String)("Cache" & Input.Token & "Username") = Data.Input.UserName AndAlso
            Settings.Get(Of String)("Cache" & Input.Token & "Pass") = Data.Input.Password AndAlso
            Settings.Get(Of String)("Cache" & Input.Token & "Access") <> "" AndAlso
            Settings.Get(Of String)("Cache" & Input.Token & "Client") <> "" AndAlso
            Settings.Get(Of String)("Cache" & Input.Token & "Uuid") <> "" AndAlso
            Settings.Get(Of String)("Cache" & Input.Token & "Name") <> "" Then
            'Try to validate the login
            Try
                If Data.IsCanceled Then Throw New OperationCanceledException
                McLoginRequestValidate(Data)
                GoTo LoginFinish
            Catch ex As Exception
                Logger.Warn(ex, "验证登录失败")
                If ex.IsBadNetwork Then Throw New Exception("$登录失败：你的网络环境不佳，导致难以连接到海外服务器。" & vbCrLf & "请稍后再试，或使用加速器、VPN 改善网络环境。")
            End Try
            Data.Progress = 0.25
            'Try to refresh the login
Refresh:
            Try
                If Data.IsCanceled Then Throw New OperationCanceledException
                McLoginRequestRefresh(Data, NeedRefresh)
                GoTo LoginFinish
            Catch ex As Exception
                McLaunchLog("刷新登录失败：" & ex.GetDisplay(True))
                If WasRefreshed Then Throw New Exception("二轮刷新登录失败", ex)
            End Try
            Data.Progress = If(NeedRefresh, 0.85, 0.45)
        End If
        'Try a normal login
        Try
            If Data.IsCanceled Then Throw New OperationCanceledException
            NeedRefresh = McLoginRequestLogin(Data)
        Catch ex As Exception
            McLaunchLog("登录失败：" & ex.GetDisplay(True))
            Throw
        End Try
        If NeedRefresh Then
            McLaunchLog("重新进行刷新登录")
            WasRefreshed = True
            Data.Progress = 0.65
            GoTo Refresh
        End If
LoginFinish:
        Data.Progress = 0.95
        'Save launch record
        Dim Dict As New Dictionary(Of String, String)
        Dim Emails As New List(Of String)
        Dim Passwords As New List(Of String)
        Try
            If Not Settings.Get(Of String)("Login" & Input.Token & "Email") = "" Then Emails.AddRange(Settings.Get(Of String)("Login" & Input.Token & "Email").Split("¨"))
            If Not Settings.Get(Of String)("Login" & Input.Token & "Pass") = "" Then Passwords.AddRange(Settings.Get(Of String)("Login" & Input.Token & "Pass").Split("¨"))
            For i = 0 To Emails.Count - 1
                Dict.Add(Emails(i), Passwords(i))
            Next
            Dict.Remove(Input.UserName)
            Emails = New List(Of String)(Dict.Keys)
            Emails.Insert(0, Input.UserName)
            Passwords = New List(Of String)(Dict.Values)
            Passwords.Insert(0, Input.Password)
            Settings.Set("Login" & Input.Token & "Email", Emails.Join("¨"c))
            Settings.Set("Login" & Input.Token & "Pass", Passwords.Join("¨"c))
        Catch ex As Exception
            Logger.Error(ex, "保存启动记录失败", LogBehavior.Toast)
            Settings.Set("Login" & Input.Token & "Email", "")
            Settings.Set("Login" & Input.Token & "Pass", "")
        End Try
    End Sub
    Private Sub McLoginLegacyStart(Data As LoaderTask(Of McLoginLegacy, McLoginResult))
        Dim Input As McLoginLegacy = Data.Input
        McLaunchLog("登录方式：离线（" & Input.UserName & "）")
        Data.Progress = 0.1
        With Data.Output
            .Name = Input.UserName
            .Uuid = McLoginLegacyUuid(Input.UserName)
            .Type = "Legacy"
        End With
        'Apply the result to all entries
        Data.Output.AccessToken = Data.Output.Uuid
        Data.Output.ClientToken = Data.Output.Uuid
        'Save launch record
        Dim Names As New List(Of String)
        If Not Settings.Get(Of String)("LoginLegacyName") = "" Then Names.AddRange(Settings.Get(Of String)("LoginLegacyName").ToString.Split("¨"))
        Names.Remove(Input.UserName)
        Names.Insert(0, Input.UserName)
        Settings.Set("LoginLegacyName", Names.Join("¨"c))
    End Sub

    'Server login: requests for the three validation methods
    Private Sub McLoginRequestValidate(ByRef Data As LoaderTask(Of McLoginServer, McLoginResult))
        McLaunchLog("验证登录开始（Validate, " & Data.Input.Token & "）")
        'Cache the info up front; otherwise logging out during the request clears the settings and leaves null values in the output
        Dim AccessToken As String = Settings.Get(Of String)("Cache" & Data.Input.Token & "Access")
        Dim ClientToken As String = Settings.Get(Of String)("Cache" & Data.Input.Token & "Client")
        Dim Uuid As String = Settings.Get(Of String)("Cache" & Data.Input.Token & "Uuid")
        Dim Name As String = Settings.Get(Of String)("Cache" & Data.Input.Token & "Name")
        'Send the login request
        Dim RequestData As New JObject(
            New JProperty("accessToken", AccessToken), New JProperty("clientToken", ClientToken), New JProperty("requestUser", True))
        NetRequestByClientRetry(
            Data.Input.BaseUrl & "/validate", HttpMethod.Post,
            Content:=RequestData.ToString(Newtonsoft.Json.Formatting.None),
            Headers:={{"Accept-Language", "zh-CN"}},
            ContentType:="application/json; charset=utf-8") 'Returns no content
        'Output the login result
        Data.Output.AccessToken = AccessToken
        Data.Output.ClientToken = ClientToken
        Data.Output.Uuid = Uuid
        Data.Output.Name = Name
        Data.Output.Type = Data.Input.Token
        'Leave the cache unchanged and finish
        McLaunchLog("验证登录成功（Validate, " & Data.Input.Token & "）")
    End Sub
    Private Sub McLoginRequestRefresh(ByRef Data As LoaderTask(Of McLoginServer, McLoginResult), RequestUser As Boolean)
        McLaunchLog("刷新登录开始（Refresh, " & Data.Input.Token & "）")
        Dim LoginJson As JObject = NetRequestByClientRetry(
               Data.Input.BaseUrl & "/refresh", HttpMethod.Post,
               Content:=New JObject(
                   New JProperty("selectedProfile", New JObject(
                       New JProperty("name", Settings.Get(Of String)($"Cache{Data.Input.Token}Name")),
                       New JProperty("id", Settings.Get(Of String)($"Cache{Data.Input.Token}Uuid"))
                   )),
                   New JProperty("accessToken", Settings.Get(Of String)($"Cache{Data.Input.Token}Access")),
                   New JProperty("requestUser", True)
               ).ToString(Newtonsoft.Json.Formatting.None),
               Headers:={{"Accept-Language", "zh-CN"}},
               ContentType:="application/json; charset=utf-8", RequireJson:=True).DeserializeJson()
        'Output the login result
        If LoginJson("selectedProfile") Is Nothing Then Throw New Exception("选择的角色 " & Settings.Get(Of String)("Cache" & Data.Input.Token & "Name") & " 无效！")
        Data.Output.AccessToken = LoginJson("accessToken").ToString
        Data.Output.ClientToken = LoginJson("clientToken").ToString
        Data.Output.Uuid = LoginJson("selectedProfile")("id").ToString
        Data.Output.Name = LoginJson("selectedProfile")("name").ToString
        Data.Output.Type = Data.Input.Token
        'Save cache
        Settings.Set("Cache" & Data.Input.Token & "Access", Data.Output.AccessToken)
        Settings.Set("Cache" & Data.Input.Token & "Client", Data.Output.ClientToken)
        Settings.Set("Cache" & Data.Input.Token & "Uuid", Data.Output.Uuid)
        Settings.Set("Cache" & Data.Input.Token & "Name", Data.Output.Name)
        Settings.Set("Cache" & Data.Input.Token & "Username", Data.Input.UserName)
        Settings.Set("Cache" & Data.Input.Token & "Pass", Data.Input.Password)
        McLaunchLog("刷新登录成功（Refresh, " & Data.Input.Token & "）")
    End Sub
    Private Function McLoginRequestLogin(ByRef Data As LoaderTask(Of McLoginServer, McLoginResult)) As Boolean
        Try
            Dim NeedRefresh As Boolean = False
            McLaunchLog("登录开始（Login, " & Data.Input.Token & "）")
            Dim RequestData As New JObject(
                New JProperty("agent", New JObject(New JProperty("name", "Minecraft"), New JProperty("version", 1))),
                New JProperty("username", Data.Input.UserName),
                New JProperty("password", Data.Input.Password),
                New JProperty("requestUser", True))
            Dim LoginJson As JObject = NetRequestByClientRetry(
                Data.Input.BaseUrl & "/authenticate", HttpMethod.Post,
                Content:=RequestData.ToString(Newtonsoft.Json.Formatting.None),
                Headers:={{"Accept-Language", "zh-CN"}},
                ContentType:="application/json; charset=utf-8", RequireJson:=True).DeserializeJson()
            'Check the login result
            If LoginJson("availableProfiles").Count = 0 Then
                If Data.Input.ForceReselectProfile Then Hint("你还没有创建角色，无法更换！", HintType.Red)
                Throw New Exception("$你还没有创建角色，请在创建角色后再试！")
            ElseIf Data.Input.ForceReselectProfile AndAlso LoginJson("availableProfiles").IsSingle Then
                Hint("你的账户中只有一个角色，无法更换！", HintType.Red)
            End If
            Dim SelectedName As String = Nothing
            Dim SelectedId As String = Nothing
            If (LoginJson("selectedProfile") Is Nothing OrElse Data.Input.ForceReselectProfile) AndAlso LoginJson("availableProfiles").Count > 1 Then
                'Profile selection required; prefer reading from cache
                NeedRefresh = True
                Dim CacheId As String = Settings.Get(Of String)("Cache" & Data.Input.Token & "Uuid")
                For Each Profile In LoginJson("availableProfiles")
                    If Profile("id").ToString = CacheId Then
                        SelectedName = Profile("name").ToString
                        SelectedId = Profile("id").ToString
                        McLaunchLog("根据缓存选择的角色：" & SelectedName)
                    End If
                Next
                'Cache invalid; ask the player to choose
                If SelectedName Is Nothing Then
                    McLaunchLog("要求玩家选择角色")
                    RunInUiWait(
                    Sub()
                        Dim SelectionControl As New List(Of IMyRadio)
                        Dim SelectionJson As New List(Of JToken)
                        For Each Profile In LoginJson("availableProfiles")
                            SelectionControl.Add(New MyRadioBox With {.Text = Profile("name").ToString})
                            SelectionJson.Add(Profile)
                        Next
                        Dim SelectedIndex As Integer = MyMsgBoxSelect(SelectionControl, "选择使用的角色")
                        SelectedName = SelectionJson(SelectedIndex)("name").ToString
                        SelectedId = SelectionJson(SelectedIndex)("id").ToString
                    End Sub)
                    McLaunchLog("玩家选择的角色：" & SelectedName)
                End If
            Else
                SelectedName = LoginJson("selectedProfile")("name").ToString
                SelectedId = LoginJson("selectedProfile")("id").ToString
            End If
            'Output the login result
            Data.Output.AccessToken = LoginJson("accessToken").ToString
            Data.Output.ClientToken = LoginJson("clientToken").ToString
            Data.Output.Name = SelectedName
            Data.Output.Uuid = SelectedId
            Data.Output.Type = Data.Input.Token
            'Save cache
            Settings.Set("Cache" & Data.Input.Token & "Access", Data.Output.AccessToken)
            Settings.Set("Cache" & Data.Input.Token & "Client", Data.Output.ClientToken)
            Settings.Set("Cache" & Data.Input.Token & "Uuid", Data.Output.Uuid)
            Settings.Set("Cache" & Data.Input.Token & "Name", Data.Output.Name)
            Settings.Set("Cache" & Data.Input.Token & "Username", Data.Input.UserName)
            Settings.Set("Cache" & Data.Input.Token & "Pass", Data.Input.Password)
            McLaunchLog("登录成功（Login, " & Data.Input.Token & "）")
            Return NeedRefresh
        Catch ex As Exception
            Dim AllMessage As String = ex.GetDisplay(False)
            Logger.Info(ex, "登录失败原始错误信息")
            'Read the error returned by the server
            If TypeOf ex Is HttpRequestCodeException Then
                Dim ErrorMessage As String = Nothing
                Try
                    Dim Response = DirectCast(ex, HttpRequestCodeException).Response
                    If Response.ContainsIgnoreCase("errorMessage") Then ErrorMessage = Response.DeserializeJson()("errorMessage").ToString()
                Catch
                End Try
                If Not String.IsNullOrWhiteSpace(ErrorMessage) Then
                    If ErrorMessage.Contains("密码错误") OrElse ErrorMessage.ContainsIgnoreCase("Incorrect username or password") Then
                        'Wrong password, log out (#5090)
                        McLaunchLog("密码错误，退出登录")
                        Select Case Data.Input.Type
                            Case McLoginType.Auth
                                RunInUi(AddressOf PageLoginAuthSkin.ExitLogin)
                            Case McLoginType.Nide
                                RunInUi(AddressOf PageLoginNideSkin.ExitLogin)
                        End Select
                    End If
                    Throw New Exception("$登录失败：" & ErrorMessage)
                End If
            End If
            'Generic keyword detection
            If AllMessage.Contains("(403)") Then
                Select Case Data.Input.Type
                    Case McLoginType.Auth
                        Throw New Exception("$登录失败，以下为可能的原因：" & vbCrLf &
                                            " - 输入的账号或密码错误。" & vbCrLf &
                                            " - 登录尝试过于频繁，导致被暂时屏蔽。请不要操作，等待 10 分钟后再试。" & vbCrLf &
                                            " - 只注册了账号，但没有在皮肤站新建角色。")
                    Case McLoginType.Nide
                        Throw New Exception("$登录失败，以下为可能的原因：" & vbCrLf &
                                            " - 输入的账号或密码错误。" & vbCrLf &
                                            " - 密码错误次数过多，导致被暂时屏蔽。请不要操作，等待 10 分钟后再试。" & vbCrLf &
                                            If(Data.Input.UserName.Contains("@"), "", " - 登录账号应为邮箱或统一通行证账号，而非游戏角色 ID。" & vbCrLf) &
                                            " - 只注册了账号，但没有加入对应服务器。")
                End Select
            ElseIf ex.IsBadNetwork() Then
                Throw New Exception("$登录失败：你的网络环境不佳，导致难以连接到海外服务器。" & vbCrLf & "请稍后再试，或使用加速器、VPN 改善网络环境。")
            ElseIf ex.Message.StartsWithF("$") Then
                Throw
            Else
                Throw New Exception("登录失败：" & ex.Message, ex)
            End If
            Return False
        End Try
    End Function

    'Microsoft login step 1, initial login: get a device code and open the login page
    Private Function MsLoginStep1New(Data As LoaderTask(Of McLoginMs, McLoginResult)) As (OAuthAccessToken As String, OAuthRefreshToken As String)
        'See: https://learn.microsoft.com/zh-cn/entra/identity-platform/v2-oauth2-device-code

        'Initial request
Retry:
        If String.IsNullOrWhiteSpace(OAuthClientId) Then Throw New InvalidOperationException("未配置微软登录应用标识，请设置 PCL_MS_CLIENT_ID 环境变量后重新启动。")
        McLaunchLog("开始微软登录步骤 1/6（原始登录）")
        Dim PrepareJson As JObject = NetRequestByClientRetry("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode", HttpMethod.Post,
            Content:=$"client_id={OAuthClientId}&tenant=/consumers&scope=XboxLive.signin%20offline_access",
            ContentType:="application/x-www-form-urlencoded", RequireJson:=True).DeserializeJson()
        McLaunchLog("网页登录地址：" & PrepareJson("verification_uri").ToString)

        'Dialog
        Dim Converter As New MyMsgBoxConverter With {.Content = PrepareJson, .ForceWait = True, .Type = MyMsgBoxType.Login}
        WaitingMyMsgBox.Enqueue(Converter)
        While Converter.Result Is Nothing
            Thread.Sleep(100)
        End While
        If TypeOf Converter.Result Is RestartException Then
            If MyMsgBox($"请在登录时选择 {vbLQ}其他登录方法{vbRQ}，然后选择 {vbLQ}使用我的密码{vbRQ}。{vbCrLf}如果没有该选项，请选择 {vbLQ}设置密码{vbRQ}，设置完毕后再登录。",
                "需要使用密码登录", "重新登录", "设置密码", "取消",
                Button2Action:=Sub() OpenWebsite("https://account.live.com/password/Change")) = 1 Then
                GoTo Retry
            Else
                Throw New OperationCanceledException
            End If
        ElseIf TypeOf Converter.Result Is Exception Then
            Throw CType(Converter.Result, Exception)
        Else
            Return (Converter.Result(0), Converter.Result(1))
        End If
    End Function
    'Microsoft login step 1, refresh login: get {OAuth AccessToken, OAuth RefreshToken} from the OAuth code or OAuth refresh token
    Private Function MsLoginStep1Refresh(Code As String) As (OAuthAccessToken As String, OAuthRefreshToken As String)
        If String.IsNullOrWhiteSpace(OAuthClientId) Then Throw New InvalidOperationException("未配置微软登录应用标识，请设置 PCL_MS_CLIENT_ID 环境变量后重新启动。")
        McLaunchLog("开始微软登录步骤 1/6（刷新登录）")

        Dim Result As String
        Try
            Result = NetRequestByClientMultiple("https://login.live.com/oauth20_token.srf", HttpMethod.Post,
                Content:=$"client_id={OAuthClientId}&refresh_token={StringUtils.FormUrlEscape(Code)}&grant_type=refresh_token&scope=XboxLive.signin%20offline_access",
                ContentType:="application/x-www-form-urlencoded",
                Headers:={{"Accept-Language", "en-US,en;q=0.5"}, {"X-Requested-With", "XMLHttpRequest"}},
                ThreadCount:=2)
        Catch ex As HttpRequestCodeException
            'When changing this error list, also check the matching code in MyMsgLogin.xaml.vb
            Dim Response = ex.Response
            If Response.ContainsIgnoreCase("must sign in again") OrElse Response.ContainsIgnoreCase("password expired") OrElse
               (Response.Contains("refresh_token") AndAlso Response.ContainsIgnoreCase("is not valid")) OrElse '#269
               Response.ContainsIgnoreCase("expired") Then '#8611
                Return ("Relogin", "")
            ElseIf Response.Contains("Account security interrupt") Then
                MyMsgBox("该账号由于安全问题无法登陆，请前往微软账户页获取更多信息。", "登录失败", "我知道了", IsWarn:=True)
                Throw New OperationCanceledException
            ElseIf Response.Contains("service abuse") Then
                MyMsgBox("非常抱歉，该账号已被微软封禁，无法登录。", "登录失败", "我知道了", IsWarn:=True)
                Throw New OperationCanceledException
            Else
                Throw
            End If
        End Try

        Dim ResultJson As JObject = Result.DeserializeJson()
        Dim AccessToken As String = ResultJson("access_token").ToString
        Dim RefreshToken As String = ResultJson("refresh_token").ToString
        Return (AccessToken, RefreshToken)
    End Function
    'Microsoft login step 2: get XBLToken from the OAuth AccessToken
    Private Function MsLoginStep2(AccessToken As String) As String
        McLaunchLog("开始微软登录步骤 2/6")

        Dim Request As String = "{
           ""Properties"": {
               ""AuthMethod"": ""RPS"",
               ""SiteName"": ""user.auth.xboxlive.com"",
               ""RpsTicket"": """ & If(AccessToken.StartsWithF("d="), "", "d=") & AccessToken & """
           },
           ""RelyingParty"": ""http://auth.xboxlive.com"",
           ""TokenType"": ""JWT""
        }"
        Return NetRequestByClientMultiple("https://user.auth.xboxlive.com/user/authenticate", HttpMethod.Post,
            Content:=Request, ContentType:="application/json", RequireJson:=True).DeserializeJson()("Token").ToString
    End Function
    'Microsoft login step 3: get {XSTSToken, UHS} from XBLToken
    Private Function MsLoginStep3(XBLToken As String) As (XSTSToken As String, UHS As String)
        McLaunchLog("开始微软登录步骤 3/6")

        Dim Request As String = "{
           ""Properties"": {
               ""SandboxId"": ""RETAIL"",
               ""UserTokens"": [""" & XBLToken & """]
           },
           ""RelyingParty"": ""rp://api.minecraftservices.com/"",
           ""TokenType"": ""JWT""
        }"
        Dim Result As String
        Try
            Result = NetRequestByClientMultiple("https://xsts.auth.xboxlive.com/xsts/authorize", HttpMethod.Post,
                Content:=Request, ContentType:="application/json", RequireJson:=True)
        Catch ex As HttpRequestCodeException
            'See https://github.com/PrismarineJS/prismarine-auth/blob/master/src/common/Constants.js
            If ex.Response.Contains("2148916227") Then
                MyMsgBox("该账号似乎已被微软封禁，无法登录。", "登录失败", "我知道了", IsWarn:=True)
                Throw New OperationCanceledException
            ElseIf ex.Response.Contains("2148916233") Then
                If MyMsgBox("你尚未注册 Xbox 账户，请在注册后再登录。", "登录提示", "注册", "取消") = 1 Then
                    OpenWebsite("https://signup.live.com/signup")
                End If
                Throw New OperationCanceledException
            ElseIf ex.Response.Contains("2148916235") Then
                MyMsgBox($"你的网络所在的国家或地区无法登录微软账号。{vbCrLf}请使用加速器或 VPN，然后再试。", "登录失败", "我知道了")
                Throw New OperationCanceledException
            ElseIf ex.Response.Contains("2148916238") Then
                If MyMsgBox("该账号年龄不足，你需要先修改出生日期，然后才能登录。" & vbCrLf &
                            "该账号目前填写的年龄是否在 13 岁以上？", "登录提示", "13 岁以上", "12 岁以下", "我不知道") = 1 Then
                    OpenWebsite("https://account.live.com/editprof.aspx")
                    MyMsgBox("请在打开的网页中修改账号的出生日期（至少改为 18 岁以上）。" & vbCrLf &
                             "在修改成功后等待一分钟，然后再回到 PCL，就可以正常登录了！", "登录提示")
                Else
                    OpenWebsite("https://support.microsoft.com/zh-cn/account-billing/如何更改-microsoft-帐户上的出生日期-837badbc-999e-54d2-2617-d19206b9540a")
                    MyMsgBox("请根据打开的网页的说明，修改账号的出生日期（至少改为 18 岁以上）。" & vbCrLf &
                             "在修改成功后等待一分钟，然后再回到 PCL，就可以正常登录了！", "登录提示")
                End If
                Throw New OperationCanceledException
            Else
                Throw
            End If
        End Try

        Dim ResultJson As JObject = Result.DeserializeJson()
        Return (
            ResultJson("Token").ToString(), 'XSTSToken
            ResultJson("DisplayClaims")("xui")(0)("uhs").ToString() 'UHS
        )
    End Function
    'Microsoft login step 4: get {Minecraft AccessToken, expiry (Unix timestamp)} from {XSTSToken, UHS}
    Private Function MsLoginStep4(Tokens As (XSTSToken As String, UHS As String)) As (AccessToken As String, ExpiresAt As Integer)
        McLaunchLog("开始微软登录步骤 4/6")

        Dim Request As String = New JObject(New JProperty("identityToken", $"XBL3.0 x={Tokens.UHS};{Tokens.XSTSToken}")).ToString(0)
        Dim Result As String
        Try
            Result = NetRequestByClientRetry("https://api.minecraftservices.com/authentication/login_with_xbox", HttpMethod.Post,
                Content:=Request, ContentType:="application/json", RequireJson:=True)
        Catch ex As HttpRequestCodeException
            If ex.StatusCode = 429 Then
                Logger.Warn(ex, "微软登录第 4 步汇报 429")
                Throw New Exception("$登录尝试太过频繁，请等待几分钟后再试！")
            ElseIf ex.StatusCode = HttpStatusCode.Forbidden Then
                Logger.Warn(ex, "微软登录第 4 步汇报 403")
                Throw New Exception("$当前 IP 的登录尝试异常。" & vbCrLf & "如果你使用了 VPN 或加速器，请把它们关掉或更换节点后再试！")
            ElseIf ex.StatusCode = HttpStatusCode.ServiceUnavailable Then
                Logger.Warn(ex, "微软登录第 4 步汇报 503")
                Throw New Exception("$Mojang 服务器出现问题，请稍后再试。" & vbCrLf & "你的网络是正常的，PCL 也是正常的，是 Mojang 出问题了……")
            ElseIf ex.Response?.Contains("ACCOUNT_SUSPENDED") Then '#8655
                MyMsgBox("该账号似乎已被微软封禁，无法登录。", "登录失败", "我知道了", IsWarn:=True)
                Throw New OperationCanceledException
            Else
                Throw
            End If
        End Try

        Dim ResultJson As JObject = Result.DeserializeJson()
        Return (
            ResultJson("access_token").ToString,
            ResultJson("expires_in").ToObject(Of Integer) + GetUnixTimestampUtc() - 3600) 'Treat as expired 1 hour early
    End Function
    'Microsoft login step 5: verify the Microsoft account owns Minecraft; this also refreshes XGP
    Private Sub MsLoginStep5(AccessToken As String)
        McLaunchLog("开始微软登录步骤 5/6")

        Dim Result As String = NetRequestByClientMultiple("https://api.minecraftservices.com/entitlements/mcstore",
            ContentType:="application/json", ThreadCount:=2, Headers:={{"Authorization", "Bearer " & AccessToken}}, RequireJson:=True)
        Try
            Dim ResultJson As JObject = Result.DeserializeJson()
            If Not (ResultJson.ContainsKey("items") AndAlso ResultJson("items").Any) Then
                Select Case MyMsgBox("你尚未购买正版 Minecraft，或者 Xbox Game Pass 已到期。", "登录失败", "购买 Minecraft", "取消")
                    Case 1
                        OpenWebsite("https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj")
                End Select
                Throw New OperationCanceledException
            End If
        Catch ex As Exception
            Logger.Warn(ex, $"微软登录第 5 步异常：{Result}")
            Throw
        End Try
    End Sub
    'Microsoft login step 6: get {UUID, UserName, ProfileJson} from the Minecraft AccessToken
    Private Function MsLoginStep6(AccessToken As String) As (UUID As String, UserName As String, ProfileJson As String)
        McLaunchLog("开始微软登录步骤 6/6")

        Dim Result As String
        Try
            Result = NetRequestByClientMultiple("https://api.minecraftservices.com/minecraft/profile",
                ContentType:="application/json", ThreadCount:=2, Headers:={{"Authorization", "Bearer " & AccessToken}}, RequireJson:=True)
        Catch ex As HttpRequestCodeException
            Select Case ex.StatusCode
                Case 429
                    Logger.Warn(ex, "微软登录第 6 步汇报 429")
                    Throw New Exception("$登录尝试太过频繁，请等待几分钟后再试！")
                Case HttpStatusCode.NotFound
                    Logger.Warn(ex, "微软登录第 6 步汇报 404")
                    RunInNewThread(
                    Sub()
                        Select Case MyMsgBox("请先创建 Minecraft 玩家档案，然后再重新登录。", "登录失败", "创建档案", "取消")
                            Case 1
                                OpenWebsite("https://www.minecraft.net/zh-hans/msaprofile/mygames/editprofile")
                        End Select
                    End Sub, "Login Failed: Create Profile")
                    Throw New OperationCanceledException
                Case Else
                    Throw
            End Select
        End Try
        Dim ResultJson As JObject = Result.DeserializeJson()
        Dim UUID As String = ResultJson("id").ToString
        Dim UserName As String = ResultJson("name").ToString
        Return (UUID, UserName, Result)
    End Function

    'Returns the UUID for a username; requires multithreading
    Public Function McLoginMojangUuid(Name As String, ThrowOnNotFound As Boolean)
        If Name.Trim.Length = 0 Then Return New String("0"c, 32)
        'Read from cache
        Dim Uuid As String = ReadIni(PathTemp & "Cache\Uuid\Mojang.ini", Name, "")
        If Len(Uuid) = 32 Then Return Uuid
        'Fetch from the official API
        Try
            Dim GotJson As JObject = NetRequestByClientRetry("https://api.mojang.com/users/profiles/minecraft/" & Name, RequireJson:=True).DeserializeJson()
            If GotJson Is Nothing Then Throw New FileNotFoundException("正版玩家档案不存在（" & Name & "）")
            Uuid = If(GotJson("id"), "")
        Catch ex As Exception
            Logger.Warn(ex, $"从官网获取正版 Uuid 失败（{Name}）")
            If Not ThrowOnNotFound AndAlso ex.GetType.Name = "FileNotFoundException" Then
                Uuid = McLoginLegacyUuid(Name) 'Player profile does not exist
            Else
                Throw New Exception("从官网获取正版 Uuid 失败", ex)
            End If
        End Try
        'Write to cache
        If Not Len(Uuid) = 32 Then Throw New Exception("获取的正版 Uuid 长度不足（" & Uuid & "）")
        WriteIni(PathTemp & "Cache\Uuid\Mojang.ini", Name, Uuid)
        Return Uuid
    End Function
    Public Function McLoginLegacyUuid(Name As String)
        Dim FullUuid As String = Name.Length.ToString("X").EnsureLength("0", 16) & Name.GetStableHashCode().ToString("X").EnsureLength("0", 16)
        Return FullUuid.Substring(0, 12) & "3" & FullUuid.Substring(13, 3) & "9" & FullUuid.Substring(17, 15)
    End Function

#End Region

#Region "Java Handling"

    Public McLaunchJavaSelected As Java = Nothing
    Private Sub McLaunchJava(Task As LoaderTask(Of Integer, Integer))
        McLaunchJavaSelected = SelectOrDownloadJava(McInstanceSelected, True, Task.CreateCancellationToken, Task.CreateSyncProgressProvider)
        If Task.IsCanceled Then Return
        If McLaunchJavaSelected Is Nothing Then Throw New OperationCanceledException 'SelectOrDownloadJava already shows an appropriate prompt
        If ModeDebug Then
            McLaunchLog($"预期 Java 版本范围：{GetJavaRequirement(McInstanceSelected).Range}")
            McLaunchLog($"Java 版本设置类别：{Settings.Get(Of Integer)("VersionArgumentJavaV2", McInstanceSelected)}")
        End If
        McLaunchLog("选择的 Java：" & McLaunchJavaSelected.ToString)
    End Sub

#End Region

#Region "Launch Arguments"
    Private McLaunchArgument As String
    Private McLaunchEncoding As Encoding

    'Main method
    Private Sub McLaunchArgumentMain(Loader As LoaderTask(Of String, Integer))
        'Get the base arguments
        Dim Arguments As New List(Of String)
        McLaunchLog("参数获取开始")
        Arguments.AddRange(McLaunchArgumentsJVM()) 'JVM arguments
        Arguments.Add(McInstanceSelected.JsonObject("mainClass")) 'mainClass
        Arguments.AddRange(McLaunchArgumentsGame()) 'Game arguments
        'Replace
        McLaunchLog("=================== 启动参数开始 ===================")
        Dim Replacements = McLaunchArgumentsReplace(Arguments).ToList
        Dim Argument As String = ""
        For Each Arg In Arguments
            'Apply replacement tokens
            For Each Replacement In Replacements
                Arg = Arg.Replace(Replacement.Key, Replacement.Value)
            Next
            'Quote arguments containing special characters
            If Arg.IndexOfAny({"&"c, "|"c, "<"c, ">"c, "^"c, " "c}) >= 0 AndAlso Not Arg.StartsWithF("""") AndAlso Not Arg.EndsWithF("""") Then
                Arg = $"""{Arg.Replace("""", "\""")}""" 'Escape double quotes inside the original argument
            End If
            McLaunchLog(Arg)
            Argument &= Arg & " "
        Next
        McLaunchLog("=================== 启动参数结束 ===================")
        McLaunchArgument = Argument.TrimEnd
    End Sub

    'JVM section
    Private Function McLaunchArgumentsJVM() As List(Of String)
        Dim Args As New List(Of String)

        'Minecraft arguments
        If McInstanceSelected.JsonObject("arguments") IsNot Nothing AndAlso McInstanceSelected.JsonObject("arguments")("jvm") IsNot Nothing Then
            'New Minecraft versions: get arguments from the version JSON
            Dim CurrentInstance As McInstance = McInstanceSelected
NextInstance:
            McLaunchLog($"新版 JVM 参数：从版本 JSON 获取（{CurrentInstance.Name}）")
            If CurrentInstance.JsonObject("arguments") IsNot Nothing AndAlso CurrentInstance.JsonObject("arguments")("jvm") IsNot Nothing Then
                For Each SubJson As JToken In CurrentInstance.JsonObject("arguments")("jvm")
                    Select Case SubJson.Type
                        Case JTokenType.String
                            Args.Add(SubJson.ToString.Trim)
                        Case JTokenType.Object
                            Dim Argument As JObject = CType(SubJson, JObject)
                            If Argument.ContainsKey("rules") AndAlso Not McJsonRuleCheck(Argument("rules")) Then Continue For 'Rules not satisfied
                            If Not Argument.ContainsKey("value") Then Continue For 'No value field
                            If Argument("value").Type = JTokenType.String Then
                                Args.Add(Argument("value").ToString.Trim)
                            Else
                                For Each value As JToken In Argument("value")
                                    Args.Add(value.ToString.Trim)
                                Next
                            End If
                    End Select
                Next
            End If
            If CurrentInstance.InheritName <> "" Then
                CurrentInstance = New McInstance(CurrentInstance.InheritName)
                GoTo NextInstance
            End If
            '1.17.1 has "-Dos.name=Windows 10"; without quoting it right away the "10" gets split off
            Args = Args.Select(Function(a) If(a.Contains(" ") AndAlso Not a.StartsWithF("""") AndAlso Not a.EndsWithF(""""), $"""{a.Replace("""", "\""")}""", a)).ToList
        Else
            'Old Minecraft versions: add arguments directly
            McLaunchLog($"旧版 JVM 参数")
            Args.Add("-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump")
            Args.Add("-Djava.library.path=${natives_directory}")
            Args.Add("-cp") : Args.Add("${classpath}")
        End If

        'Custom arguments from settings
        Dim CustomArg As String = Settings.Get(Of String)("VersionAdvanceJvm", Instance:=McInstanceSelected)
        Dim UsesGlobalJvm = CustomArg = ""
        If UsesGlobalJvm Then CustomArg = Settings.Get(Of String)("LaunchAdvanceJvm")
        If CustomArg = LegacyDefaultJvmArgs Then
            CustomArg = ""
            Settings.Set(If(UsesGlobalJvm, "LaunchAdvanceJvm", "VersionAdvanceJvm"), "", Instance:=McInstanceSelected)
        End If
        If CustomArg <> "" Then CustomArg = ArgumentReplace(CustomArg)
        CustomArg &= " -XX:-OmitStackTraceInFastThrow -Djdk.lang.Process.allowAmbiguousCommands=True"
        If Settings.Get(Of Boolean)("LaunchOptPreTouch") Then CustomArg &= " -XX:+AlwaysPreTouch"

        'Re-split the string and deduplicate
        Args = DeduplicateJavaArguments(SplitJavaArguments(Args.Join(" "c).Replace("McEmu= ", "McEmu=") & " " & CustomArg).ToList, True)

        'Nide8 auth
        If McLoginLoader.Output.Type = "Nide" Then
            Args.Add("-javaagent:""${pure_directory}\nide8auth.jar""=""" & Settings.Get(Of String)("VersionServerNide", Instance:=McInstanceSelected) & """")
        End If
        'Authlib-Injector
        If McLoginLoader.Output.Type = "Auth" Then
            If McLaunchJavaSelected.Version.Major >= 6 Then Args.Add("-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT") 'Trust the system root certificates (5252#)
            Dim Server As String = Settings.Get(Of String)("VersionServerAuthServer", Instance:=McInstanceSelected)
            Try
                McLaunchLog($"开始 Authlib Injector Prefetch")
                Dim Response As String = NetRequestByClientRetry(Server, Encoding:=Encoding.UTF8)
                Args.Add("-javaagent:""${pure_directory}\authlib-injector.jar""=""" & Server & """")
                Args.Add("-Dauthlibinjector.side=client")
                Args.Add("-Dauthlibinjector.yggdrasil.prefetched=" & Convert.ToBase64String(Encoding.UTF8.GetBytes(Response)))
            Catch ex As Exception
                Throw New Exception("无法连接到第三方登录服务器（" & If(Server, Nothing) & "）", ex)
            End Try
        End If

        'JLW
        'Garbled command-line arguments under non-GBK encodings (JDK-8272352)
        Dim UseJLW As Boolean =
            Settings.Get(Of Boolean)("LaunchOptFixUnicodePath") AndAlso Not Settings.Get(Of Boolean)("LaunchAdvanceDisableJLW") AndAlso Not Settings.Get(Of Boolean)("VersionAdvanceDisableJLW", McInstanceSelected) AndAlso
            Not IsGBKEncoding 'Even if the game folder name has no non-ASCII characters, the player name etc. may contain Chinese, so JLW is still needed (#8739)
        If UseJLW AndAlso CustomArg.Contains("-javaagent") Then
            UseJLW = False
            McLaunchLog("由于使用了自定义 javaagent，已禁用 JLW，这可能导致游戏崩溃！", LogLevel.Warn)
        End If
        If UseJLW Then
            If McLaunchJavaSelected.Version.Major >= 9 Then
                Args.Add("--add-exports") : Args.Add("cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED")
            End If
            Args.Add("-Doolloo.jlw.tmpdir=${pure_directory}") 'Must not end with \ here
        End If
        McLaunchLog("使用 JLW：" & UseJLW)

        'LUA
        Dim UseLUA As Boolean =
            Settings.Get(Of Boolean)("LaunchOptFixLwjgl") AndAlso Not Settings.Get(Of Boolean)("LaunchAdvanceDisableLUA") AndAlso Not Settings.Get(Of Boolean)("VersionAdvanceDisableLUA", McInstanceSelected) AndAlso
            McLibListGet(McInstanceSelected, False).Select(Function(e) e.OriginalName).Contains("org.lwjgl:lwjgl:3.4.1")
        If UseLUA Then Args.Add($"-javaagent:""{LaunchUtils.ExtractPatch("Launch\LwjglUnsafeAgent.jar", PathPure.Value)}""")
        McLaunchLog("使用 LUA：" & UseLUA)

#Region "Memory Management"

        McLaunchLog("当前剩余内存：" & Math.Round(My.Computer.Info.AvailablePhysicalMemory / 1024 / 1024 / 1024 * 10) / 10 & "G")
        Dim TargetRam As Integer = Math.Floor(PageInstanceSetup.GetRam(McInstanceSelected) * 1024)
        Args.Add("-Xmx" & TargetRam & "m")

        'Per-instance settings still use the old numeric option. Global settings are decided by individually toggleable optimizations.
        '0: generational ZGC on Java 21+, non-generational ZGC on 15~20, G1GC on 14-
        '1: generational ZGC on Java 21+, G1GC on 20-
        '2: G1GC only
        '3: unspecified, player-defined
        '4: tuned G1GC
        Dim InstanceGc As Integer = If(McInstanceSelected Is Nothing, 0, Settings.Get(Of Integer)("VersionAdvanceGC", McInstanceSelected))
        Dim SetupType As Integer = If(InstanceGc > 0, InstanceGc - 1, -1)
        If SetupType = -1 Then
            Dim JavaMajor = McLaunchJavaSelected.Version.Major
            Dim UseG1 = Settings.Get(Of Boolean)("LaunchOptUseG1")
            McLaunchLog($"GC 勾选：G1={UseG1}，Java {JavaMajor}")
            If UseG1 Then
                Args = Args.Where(Function(a) Not a.RegexCheck(
                    "( )*-XX:[+-]?(Use\w+GC|ZGenerational|UseCompactObjectHeaders|G1\w+Percent|G1\w+Size|(Max|Min)(GCPauseMillis|HeapFreeRatio))")).ToList
                Args.AddRange(LaunchJvmPolicy.G1Arguments(McLaunchJavaSelected.Version,
                    Settings.Get(Of Boolean)("LaunchOptG1Tune"),
                    Settings.Get(Of Boolean)("LaunchOptStringDedup"),
                    Settings.Get(Of Boolean)("LaunchOptDisableSharedMem") AndAlso Not ModeDebug))
            End If
        ElseIf SetupType <> 3 Then
            'Decide between G1GC and ZGC
            Dim UseG1GC As Boolean = False
            If (SetupType = 0 AndAlso McLaunchJavaSelected.Version.Major < 15) OrElse
               (SetupType = 1 AndAlso McLaunchJavaSelected.Version.Major < 21) OrElse
               (SetupType = 2 OrElse SetupType = 4) Then UseG1GC = True 'Check the Java version: ZGC is available on Java 15+, generational ZGC on Java 21+
            If Environment.OSVersion.Version.Major < 10 OrElse Environment.OSVersion.Version.Build < 17763 Then UseG1GC = True 'Check OS compatibility: ZGC requires Windows 10 1809+
            McLaunchLog($"GC 设置：{SetupType}，选取 {If(UseG1GC, "G1GC", "ZGC")}，Java 版本：{McLaunchJavaSelected.Version.Major}")
            'Remove existing GC arguments
            Args = Args.Where(Function(a) Not a.RegexCheck(
                "( )*-XX:[+-]?(Use\w+GC|ZGenerational|UseCompactObjectHeaders|G1\w+Percent|G1\w+Size|(Max|Min)(GCPauseMillis|HeapFreeRatio))")).ToList
            'Add GC arguments
            Args.Add("-XX:+UnlockExperimentalVMOptions")
            If McLaunchJavaSelected.Version.Major >= 24 Then Args.Add("-XX:+UseCompactObjectHeaders")
            If UseG1GC Then
                Args.AddRange(LaunchJvmPolicy.G1Arguments(McLaunchJavaSelected.Version,
                    SetupType = 4, False, SetupType = 4 AndAlso Not ModeDebug))
            Else
                'ZGC
                Args.Add("-XX:+UseZGC")
                If McLaunchJavaSelected.Version.Major = 21 OrElse McLaunchJavaSelected.Version.Major = 22 Then Args.Add("-XX:+ZGenerational") 'Generational ZGC is the default since Java 23, so the argument is no longer needed
            End If
        End If

        Args.Remove("-XX:MaxDirectMemorySize=256M") 'Cleanup for #3511

#End Region

        'Log4j vulnerability mitigation arguments
        Args.Add("-Dlog4j2.formatMsgNoLookups=true")

        'Encoding (#4700, #5892, #5909, #6934, #8950, #9241)
        Dim UseCompatEncoding = McLaunchJavaSelected.Version.Major >= 18
        Dim EncodingName = If(UseCompatEncoding,
                              McLaunchJavaSelected.NativeEncodingName, 'Java 18+ adds COMPAT, so Java falls back to native.encoding
                              McLaunchJavaSelected.FileEncodingName) 'Java 17- uses file.encoding directly
        Try
            McLaunchEncoding = Encoding.GetEncoding(If(EncodingName.Equals("GBK", StringComparison.OrdinalIgnoreCase), "GB18030", EncodingName))
        Catch ex As Exception
            Dim FallbackEncoding = Encoding.GetEncoding(Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage) 'Encoding selection behavior of old Java versions
            McLaunchLog($"无法识别 Java 编码 {If(EncodingName, "Nothing")}，已回退为 {FallbackEncoding.WebName}", LogLevel.Warn)
            McLaunchEncoding = FallbackEncoding
        End Try
        McLaunchLog($"使用编码：{McLaunchEncoding.WebName}")
        Dim LaunchEncodingName = If(McLaunchEncoding.CodePage = 54936, "GBK", McLaunchEncoding.WebName)
        Dim StdoutArgName = If(McLaunchJavaSelected.Version.Major < 19, "sun.stdout.encoding", "stdout.encoding") 'Old Java versions should set sun.std*
        Dim StderrArgName = If(McLaunchJavaSelected.Version.Major < 19, "sun.stderr.encoding", "stderr.encoding")
        If Not Args.Any(Function(a) a.StartsWithF($"-D{StdoutArgName}=")) Then Args.Add($"-D{StdoutArgName}={LaunchEncodingName}")
        If Not Args.Any(Function(a) a.StartsWithF($"-D{StderrArgName}=")) Then Args.Add($"-D{StderrArgName}={LaunchEncodingName}")
        If UseCompatEncoding AndAlso Not Args.Any(Function(a) a.StartsWithF("-Dfile.encoding=")) Then Args.Add("-Dfile.encoding=COMPAT") 'Prevent old Minecraft versions and some mods being incompatible with UTF-8

        'Add -jar for JLW; it must come last
        If UseJLW Then
            Args.Add("-jar") : Args.Add(LaunchUtils.ExtractPatch("Launch\JavaWrapper.jar", PathPure.Value))
        End If

        'Deduplicate again and output
        Return DeduplicateJavaArguments(Args, True)
    End Function
    'Game arguments section
    Private Function McLaunchArgumentsGame() As List(Of String)
        Dim Arg As String = ""

        'Minecraft arguments
        'Old Minecraft versions: add arguments directly
        If McInstanceSelected.JsonObject("minecraftArguments")?.ToString.Any Then
            Dim BasicString As String = McInstanceSelected.JsonObject("minecraftArguments").ToString
            McLaunchLog($"旧版游戏参数：" & BasicString)
            BasicString += " --height ${resolution_height} --width ${resolution_width}" 'Always add width/height options; deduplication later overrides the ones Minecraft provides
            Arg &= " " & BasicString
        End If
        'New Minecraft versions: get arguments from the version JSON
        If McInstanceSelected.JsonObject("arguments") IsNot Nothing AndAlso McInstanceSelected.JsonObject("arguments")("game") IsNot Nothing Then
            Dim CurrentInstance As McInstance = McInstanceSelected
            Dim AppendArg = Sub(a As String) Arg &= " " & If(a.Contains(" ") AndAlso Not a.StartsWithF("""") AndAlso Not a.EndsWithF(""""), $"""{a.Replace("""", "\""")}""", a)
NextInstance:
            McLaunchLog($"新版游戏参数：从版本 JSON 获取（{CurrentInstance.Name}）")
            If CurrentInstance.JsonObject("arguments") IsNot Nothing AndAlso CurrentInstance.JsonObject("arguments")("game") IsNot Nothing Then
                For Each SubJson As JToken In CurrentInstance.JsonObject("arguments")("game")
                    Select Case SubJson.Type
                        Case JTokenType.String
                            AppendArg(SubJson.ToString)
                        Case JTokenType.Object
                            Dim Argument As JObject = CType(SubJson, JObject)
                            If Argument.ContainsKey("rules") AndAlso Not McJsonRuleCheck(Argument("rules")) Then Continue For 'Rules not satisfied
                            If Not Argument.ContainsKey("value") Then Continue For 'No value field
                            If Argument("value").Type = JTokenType.String Then
                                AppendArg(Argument("value").ToString)
                            Else
                                For Each value As JToken In Argument("value")
                                    AppendArg(value.ToString)
                                Next
                            End If
                    End Select
                Next
            End If
            If CurrentInstance.InheritName <> "" Then
                CurrentInstance = New McInstance(CurrentInstance.InheritName)
                GoTo NextInstance
            End If
        End If

        'Custom arguments from settings
        Dim CustomArg As String = Settings.Get(Of String)("VersionAdvanceGame", Instance:=McInstanceSelected)
        If CustomArg = "" Then CustomArg = Settings.Get(Of String)("LaunchAdvanceGame")
        If CustomArg <> "" Then CustomArg = ArgumentReplace(CustomArg)
        Arg &= " " & CustomArg

        'Put OptiFineForgeTweaker at the end of the arguments
        If (McInstanceSelected.Version.ModLoaders.HasFlagF(ModLoaders.Forge) OrElse McInstanceSelected.Version.ModLoaders.HasFlagF(ModLoaders.LiteLoader)) AndAlso
                McInstanceSelected.Version.HasOptiFine AndAlso Not McInstanceSelected.Version.OptiFineAsMod Then
            If Arg.Contains("--tweakClass optifine.OptiFineForgeTweaker") Then
                McLaunchLog("发现正确的 OptiFineForge TweakClass，目前的游戏参数：" & Arg)
                Arg = Arg.Replace(" --tweakClass optifine.OptiFineForgeTweaker", "") & " --tweakClass optifine.OptiFineForgeTweaker"
            End If
            If Arg.Contains("--tweakClass optifine.OptiFineTweaker") Then
                McLaunchLog("发现错误的 OptiFineForge TweakClass，目前的游戏参数：" & Arg)
                Arg = Arg.Replace(" --tweakClass optifine.OptiFineTweaker", "") & " --tweakClass optifine.OptiFineForgeTweaker"
                Try
                    FileUtils.Write(
                        McInstanceSelected.GetJsonPath(),
                        FileUtils.ReadAsString(McInstanceSelected.GetJsonPath()).Replace("optifine.OptiFineTweaker", "optifine.OptiFineForgeTweaker"))
                Catch ex As Exception
                    Logger.Warn(ex, "替换 OptiFineForge TweakClass 失败")
                End Try
            End If
        End If

        'Re-split the string and deduplicate
        Dim Args As List(Of String) = DeduplicateJavaArguments(SplitJavaArguments(Arg).ToList, False)

        'Extra passed-in arguments
        Args.AddRange(CurrentLaunchOptions.ExtraGameArgs)

        'Fullscreen
        If Settings.Get(Of Integer)("LaunchArgumentWindowType") = 0 Then Args.Add("--fullscreen")

        'Auto-join server
        Dim Server As String = If(String.IsNullOrEmpty(CurrentLaunchOptions.ServerIp), Settings.Get(Of String)("VersionServerEnter", McInstanceSelected), CurrentLaunchOptions.ServerIp)
        If Server.Any Then
            If McInstanceSelected.ReleaseTime > New Date(2023, 4, 4) Then
                'QuickPlay
                Args.Add($"--quickPlayMultiplayer") : Args.Add(Server)
            Else
                'Old versions
                If Server.Contains(":") Then
                    'With port
                    Args.Add("--server") : Args.Add(Server.Split(":")(0))
                    Args.Add("--port") : Args.Add(Server.Split(":")(1))
                Else
                    'Without port
                    Args.Add("--server") : Args.Add(Server)
                    Args.Add("--port") : Args.Add("25565")
                End If
                If McInstanceSelected.Version.HasOptiFine Then Hint("OptiFine 与自动进入服务器可能不兼容，有概率导致材质丢失甚至游戏崩溃！", HintType.Red)
            End If
        End If

        'Deduplicate again and output
        Return DeduplicateJavaArguments(Args, False)
    End Function
    'Get replacement tokens
    Private Iterator Function McLaunchArgumentsReplace(Arguments As List(Of String)) As IEnumerable(Of (Key As String, Value As String))
        'Base arguments
        Yield ("${classpath_separator}", ";")
        Yield ("${natives_directory}", PathUtils.RemoveSlashSuffix(GetNativesFolder()))
        Yield ("${library_directory}", PathUtils.ToShortPath(McFolderSelected & "libraries"))
        Yield ("${libraries_directory}", PathUtils.ToShortPath(McFolderSelected & "libraries"))
        Yield ("${pure_directory}", PathUtils.RemoveSlashSuffix(PathPure.Value)) 'Added by PCL; lets paths be replaced after splitting and deduplicating, so special characters in paths don't break splitting/deduplication
        Yield ("${launcher_name}", "PCL")
        Yield ("${launcher_version}", VersionCode)
        Yield ("${version_name}", McInstanceSelected.Name)
        Yield ("${game_directory}", PathUtils.ToShortPath(Left(McInstanceSelected.PathIndie, McInstanceSelected.PathIndie.Count - 1)))
        Yield ("${assets_root}", PathUtils.ToShortPath(McFolderSelected & "assets"))
        Yield ("${user_properties}", "{}")
        Yield ("${auth_player_name}", McLoginLoader.Output.Name)
        Yield ("${auth_uuid}", McLoginLoader.Output.Uuid)
        Yield ("${auth_access_token}", McLoginLoader.Output.AccessToken)
        Yield ("${access_token}", McLoginLoader.Output.AccessToken)
        Yield ("${auth_session}", McLoginLoader.Output.AccessToken)
        Yield ("${user_type}", "msa") '#1221
        Yield ("${primary_jar}", PathUtils.ToShortPath(McInstanceSelected.PathVersion & McInstanceSelected.Name & ".jar")) '#6942
        Yield ("${game_assets}", PathUtils.ToShortPath(McFolderSelected & "assets\virtual\legacy")) 'The pre-1.6 asset index of 1.5.2 should be merged with legacy
        Yield ("${assets_index_name}", McAssetsGetIndexName(McInstanceSelected))

        'Custom info
        Dim ArgumentInfo As String
        ArgumentInfo = Settings.Get(Of String)("VersionArgumentInfo", Instance:=McInstanceSelected)
        If ArgumentInfo = "" Then ArgumentInfo = Settings.Get(Of String)("LaunchArgumentInfo")
        If ArgumentInfo = "" Then 'If the custom info is empty, the part must be removed from the arguments, otherwise Minecraft shows ${version_type} or an empty string
            If Arguments.Contains("--versionType") Then
                Dim Index As Integer = Arguments.IndexOf("--versionType")
                Arguments.RemoveAt(Index)
                If Index < Arguments.Count Then Arguments.RemoveAt(Index)
            End If
            Yield ("${version_type}", """""")
        Else
            Yield ("${version_type}", ArgumentReplace(ArgumentInfo))
        End If

        'Window size
        Dim TargetSize As Size
        Select Case Settings.Get(Of Integer)("LaunchArgumentWindowType")
            Case 2 'Same as launcher size
                Dim Result As Size
                RunInUiWait(Sub() Result = New Size(GetPixelSize(FrmMain.PanForm.ActualWidth), GetPixelSize(FrmMain.PanForm.ActualHeight)))
                TargetSize = Result
                TargetSize.Height -= 29.5 * DPI / 96 'Title bar height
            Case 3 'Custom
                TargetSize = New Size(Math.Max(100, Settings.Get(Of Integer)("LaunchArgumentWindowWidth")), Math.Max(100, Settings.Get(Of Integer)("LaunchArgumentWindowHeight")))
            Case Else
                TargetSize = New Size(854, 480)
        End Select
        If McInstanceSelected.Version.Vanilla.Major <= 12 AndAlso McLaunchJavaSelected.Version >= New Version(8, 0, 200) AndAlso McLaunchJavaSelected.Version <= New Version(8, 0, 321) AndAlso
                Not McInstanceSelected.Version.HasOptiFine AndAlso Not McInstanceSelected.Version.ModLoaders.HasFlagF(ModLoaders.Forge) Then 'Fix 1.12.2- with JRE 8u200~321 where the window size becomes DPI% of the configured size (#3463)
            McLaunchLog($"已应用窗口大小过大修复（Java 版本：{McLaunchJavaSelected.Version}）")
            TargetSize.Width /= DPI / 96
            TargetSize.Height /= DPI / 96
        End If
        Yield ("${resolution_width}", Math.Round(TargetSize.Width))
        Yield ("${resolution_height}", Math.Round(TargetSize.Height))

        'Libraries
        Dim Cps As New List(Of String)
        Dim OptiFineCp As String = Nothing
        For Each Library As McLibEntry In McLibListGet(McInstanceSelected, True)
            If Library.IsNatives Then Continue For
            If Library.Name IsNot Nothing AndAlso Library.Name = "optifine:OptiFine" Then
                OptiFineCp = Library.LocalPath
            Else
                Cps.Add(Library.LocalPath)
            End If
        Next
        If OptiFineCp IsNot Nothing Then Cps.Insert(Cps.Count - 2, OptiFineCp) 'OptiFine always has to be second to last
        Yield ("${classpath}", Cps.Select(Function(c) PathUtils.ToShortPath(c)).Join(";"c))
    End Function

    ''' <summary>
    ''' Splits a Java argument string.
    ''' </summary>
    Private Iterator Function SplitJavaArguments(Arg As String) As IEnumerable(Of String)
        Arg = Arg.ReplaceLineEndings(" ", mergeMultiple:=True).Trim 'Normalize line breaks
        If String.IsNullOrWhiteSpace(Arg) Then Return
        Dim Current As New StringBuilder
        Dim InQuotes As Boolean = False
        Dim i As Integer = 0
        While i < Arg.Length
            Dim c As Char = Arg(i)
            If c = "\"c AndAlso i + 1 < Arg.Length AndAlso Arg(i + 1) = """"c Then 'Detect \"
                'Escaped double quote: keep the backslash and quote, don't toggle InQuotes
                Current.Append("\""")
                i += 1 'Also skip the next character (the double quote)
            ElseIf c = """"c Then
                'Unescaped double quote: toggle InQuotes
                InQuotes = Not InQuotes
                Current.Append(c)
            ElseIf c = " "c AndAlso Not InQuotes Then
                'Space outside double quotes: end of an argument
                If Current.Length > 0 Then
                    Yield Current.ToString()
                    Current.Clear()
                End If
            Else
                'Regular character, append it
                Current.Append(c)
            End If
            i += 1
        End While
        If Current.Length > 0 Then Yield Current.ToString 'Add the remainder
    End Function
    ''' <summary>
    ''' Deduplicates Java arguments.
    ''' </summary>
    Private Function DeduplicateJavaArguments(Args As List(Of String), IsJVMArgs As Boolean) As List(Of String)
        Dim Result As New List(Of String)
        Dim i As Integer = 0
        While i < Args.Count
            Dim Key As String = Args(i)
            If Not Key.StartsWithF("-") OrElse
               (i + 1 >= Args.Count OrElse (Args(i + 1).StartsWithF("-") AndAlso Val(Args(i + 1).AfterFirst("-")) = 0)) Then 'Avoid treating the -50 in "-xPos 23 -xPos -50" as an argument
                'Single argument
                i += 1
                If Result.Contains(Key) Then
                    McLaunchLog($"已去重单个{If(IsJVMArgs, " JVM ", "游戏")}参数：{Key}")
                    Continue While
                End If
                Result.Add(Key)
            Else
                'Space-separated key-value pair (e.g. --width 233)
                Dim Value As String = Args(i + 1)
                i += 2
                For j = 0 To Result.Count - 2 'Search backwards for the same key
                    If Result(j) <> Key Then Continue For
                    'Only for game arguments other than tweakClass, overwrite the old value with the new one
                    'In testing, duplicate --width, --uuid etc. make both invalid, which causes a crash if it happens to a critical argument
                    If Not IsJVMArgs AndAlso Key <> "--tweakClass" Then
                        McLaunchLog($"已覆盖重复的{If(IsJVMArgs, " JVM ", "游戏")}参数：{Key} {Result(j + 1)} → {Key} {Value}")
                        Result(j + 1) = Value
                        Continue While
                    End If
                    'Key and value are identical, discard
                    If Result(j + 1) = Value Then
                        McLaunchLog($"已去重{If(IsJVMArgs, " JVM ", "游戏")}参数对：{Key} {Value}")
                        Continue While
                    End If
                Next
                Result.Add(Key)
                Result.Add(Value)
            End If
        End While
        Return Result
    End Function

#End Region

#Region "Extract Natives"

    Private Sub McLaunchNatives(Loader As LoaderTask(Of Integer, Integer))

        'Create folder
        Dim Target As String = GetNativesFolder()
        DirectoryUtils.Create(Target)

        'Extract files
        McLaunchLog("正在解压 Natives 文件")
        Dim ExistFiles As New List(Of String)
        For Each Native As McLibEntry In McLibListGet(McInstanceSelected, True)
            If Not Native.IsNatives Then Continue For
            Dim Zip As ZipArchive
            Try
                Zip = FileUtils.OpenZip(Native.LocalPath)
            Catch ex As InvalidDataException
                Logger.Warn(ex, $"打开 Natives 文件失败（{Native.LocalPath}）")
                FileUtils.Delete(Native.LocalPath)
                Throw New Exception("无法打开 Natives 文件（" & Native.LocalPath & "），该文件可能已损坏，请重新尝试启动游戏")
            End Try
            Using Zip
                For Each Entry In Zip.Entries
                    Dim FileName As String = Entry.FullName
                    If PathUtils.GetExtension(FileName) = "dll" Then
                        'The actual extraction step
                        Dim FilePath As String = Target & FileName
                        ExistFiles.Add(FilePath)
                        Dim OriginalFile = FileUtils.GetInfo(FilePath)
                        If OriginalFile.Exists Then
                            If OriginalFile.Length = Entry.Length Then
                                If ModeDebug Then McLaunchLog("无需解压：" & FilePath)
                                Continue For
                            End If
                            'Delete old files
                            Try
                                FileUtils.Delete(FilePath)
                            Catch ex As UnauthorizedAccessException
                                McLaunchLog("删除原 dll 访问被拒绝，这通常代表有一个 MC 正在运行，跳过解压：" & FilePath)
                                McLaunchLog("实际的错误信息：" & ex.GetDisplay(True))
                                Exit For
                            End Try
                        End If
                        'Extract new files
                        FileUtils.Write(FilePath, Entry.Open)
                        McLaunchLog("已解压：" & FilePath)
                    End If
                Next
            End Using
        Next

        'Delete extra files
        For Each FileName As String In DirectoryUtils.EnumerateFiles(Target)
            If ExistFiles.Contains(FileName) Then Continue For
            Try
                McLaunchLog("删除：" & FileName)
                FileUtils.Delete(FileName)
            Catch ex As UnauthorizedAccessException
                McLaunchLog("删除多余文件访问被拒绝，跳过删除步骤")
                McLaunchLog("实际的错误信息：" & ex.GetDisplay(True))
                Return
            End Try
        Next

    End Sub
    ''' <summary>
    ''' Gets the Natives folder path, ending with \.
    ''' </summary>
    Public Function GetNativesFolder(Optional Instance As McInstance = Nothing) As String
        If Instance Is Nothing Then Instance = McInstanceSelected
        Dim Result As String = PathUtils.AddSlashSuffix(PathUtils.ToShortPath(Instance.PathVersion)) & Instance.Name & "-natives\"
        If Not (IsGBKEncoding OrElse Result.IsAsciiOnly()) Then
            Result = Paths.AppData & ".minecraft\bin\natives\"
            If Not Result.IsAsciiOnly() Then
                Result = OsDrive & "ProgramData\PCL\natives\"
            End If
        End If
        DirectoryUtils.Create(Result) 'Create it up front so DirectoryUtils.Shorten returns a result (otherwise the first Forge launch crashes under long paths)
        Return PathUtils.AddSlashSuffix(PathUtils.ToShortPath(Result))
    End Function

#End Region

#Region "Launch and Pre/Post Processing"

    Private Sub McLaunchPrerun()

        'Use the high-performance GPU
        If Settings.Get(Of Boolean)("LaunchAdvanceGraphicCard") Then
            Try
                SetGPUPreference(McLaunchJavaSelected.JavaExePath)
                SetGPUPreference(PathExe)
            Catch ex As Exception
                If WindowsUtils.HasAdminRole() Then
                    Logger.Warn(ex, "直接调整显卡设置失败")
                Else
                    Logger.Warn(ex, "直接调整显卡设置失败，将以管理员权限重启 PCL 再次尝试")
                    Try
                        If RunAsAdmin($"--gpu ""{McLaunchJavaSelected.JavaExePath}""") = ProcessReturnValues.TaskDone Then
                            McLaunchLog("以管理员权限重启 PCL 并调整显卡设置成功")
                        Else
                            Throw New Exception("调整过程中出现异常")
                        End If
                    Catch exx As Exception
                        Logger.Error(exx, "调整显卡设置失败，Minecraft 可能会使用默认显卡运行", LogBehavior.Toast)
                    End Try
                End If
            End Try
        End If

        'Update launcher_profiles.json
        Try
            'Ensure it is usable
            If Not McLoginLoader.Output.Type = "Microsoft" Then Exit Try
            McFolderLauncherProfilesJsonCreate(McFolderSelected)
            'Build the JSON object to merge
            Dim ReplaceJsonString As String = "
            {
              ""authenticationDatabase"": {
                ""00000111112222233333444445555566"": {
                  ""username"": """ & McLoginLoader.Output.Name.Replace("""", "-") & """,
                  ""profiles"": {
                    ""66666555554444433333222221111100"": {
                        ""displayName"": """ & McLoginLoader.Output.Name & """
                    }
                  }
                }
              },
              ""clientToken"": """ & McLoginLoader.Output.ClientToken & """,
              ""selectedUser"": {
                ""account"": ""00000111112222233333444445555566"", 
                ""profile"": ""66666555554444433333222221111100""
              }
            }"
            Dim ReplaceJson As JObject = (ReplaceJsonString).DeserializeJson()
            'Update the file
            Dim Profiles As JObject = FileUtils.ReadAsJson(McFolderSelected & "launcher_profiles.json")
            Profiles.Merge(ReplaceJson)
            FileUtils.Write(McFolderSelected & "launcher_profiles.json", Profiles.ToString, encoding:=Encoding.GetEncoding("GB18030"))
            McLaunchLog("已更新 launcher_profiles.json")
        Catch ex As Exception
            Logger.Warn(ex, "更新 launcher_profiles.json 失败，将在删除文件后重试")
            Try
                FileUtils.Delete(McFolderSelected & "launcher_profiles.json")
                McFolderLauncherProfilesJsonCreate(McFolderSelected)
                'Build the JSON object to merge
                Dim ReplaceJsonString As String = "
                    {
                      ""authenticationDatabase"": {
                        ""00000111112222233333444445555566"": {
                          ""username"": """ & McLoginLoader.Output.Name.Replace("""", "-") & """,
                          ""profiles"": {
                            ""66666555554444433333222221111100"": {
                                ""displayName"": """ & McLoginLoader.Output.Name & """
                            }
                          }
                        }
                      },
                      ""clientToken"": """ & McLoginLoader.Output.ClientToken & """,
                      ""selectedUser"": {
                        ""account"": ""00000111112222233333444445555566"", 
                        ""profile"": ""66666555554444433333222221111100""
                      }
                    }"
                Dim ReplaceJson As JObject = ReplaceJsonString.DeserializeJson()
                'Update the file
                Dim Profiles As JObject = FileUtils.ReadAsJson(McFolderSelected & "launcher_profiles.json")
                Profiles.Merge(ReplaceJson)
                FileUtils.Write(McFolderSelected & "launcher_profiles.json", Profiles.ToString, encoding:=Encoding.GetEncoding("GB18030"))
                McLaunchLog("已在删除后更新 launcher_profiles.json")
            Catch exx As Exception
                Logger.Error(exx, "更新 launcher_profiles.json 失败")
            End Try
        End Try

        'Update options.txt
        Dim SetupFileAddress As String = McInstanceSelected.PathIndie & "options.txt"
        If Not FileUtils.Exists(SetupFileAddress) Then
            'Yosbr mod compatibility (#2385): https://www.curseforge.com/minecraft/mc-mods/yosbr
            Dim YosbrFileAddress As String = McInstanceSelected.PathIndie & "config\yosbr\options.txt"
            If FileUtils.Exists(YosbrFileAddress) Then
                McLaunchLog("将修改 Yosbr Mod 中的 options.txt")
                SetupFileAddress = YosbrFileAddress
                WriteIni(SetupFileAddress, "lang", "none") 'Ignore the default language
            End If
        End If
        Try
            'Language
            '1.0-     : no language option
            '1.1 ~ 5  : zh_CN works, zh_cn crashes (the last two letters must be uppercase, otherwise NPE crash)
            '1.6 ~ 10 : zh_CN works, zh_cn switches to English automatically
            '1.11 ~ 12: zh_cn works; zh_CN shows Chinese but the language setting wrongly shows English selected
            '1.13+    : zh_cn works, zh_CN switches to English automatically
            Dim CurrentLang As String = ReadIni(SetupFileAddress, "lang", "none")
            Dim RequiredLang As String = If(CurrentLang = "none" OrElse Not DirectoryUtils.Exists(McInstanceSelected.PathIndie & "saves"), '#3844, modpacks may already ship their own options.txt
                "zh_cn", CurrentLang.Lower)
            If Not McInstanceSelected.Version.Valid OrElse McInstanceSelected.Version.Vanilla.Major <= 10 Then
                'Uppercase the last two letters, keep the rest
                RequiredLang = RequiredLang.Substring(0, RequiredLang.Length - 2) & RequiredLang.Substring(RequiredLang.Length - 2).Upper
            End If
            If CurrentLang = RequiredLang Then
                McLaunchLog($"需要的语言为 {RequiredLang}，当前语言为 {CurrentLang}，无需修改")
            Else
                WriteIni(SetupFileAddress, "lang", "-") 'Trigger a cache change so stale cache doesn't remain after delete and re-download
                WriteIni(SetupFileAddress, "lang", RequiredLang)
                McLaunchLog($"已将语言从 {CurrentLang} 修改为 {RequiredLang}")
            End If
            'Window
            Select Case Settings.Get(Of Integer)("LaunchArgumentWindowType")
                Case 0 'Fullscreen
                    WriteIni(SetupFileAddress, "fullscreen", "true")
                Case 1 'Default
                Case Else 'Other
                    WriteIni(SetupFileAddress, "fullscreen", "false")
            End Select
        Catch ex As Exception
            Logger.Error(ex, "更新 options.txt 失败", LogBehavior.Toast)
        End Try

        Try
            Dim ZipFileAddress As String = McInstanceSelected.PathIndie & "resourcepacks\PCL2 Skin.zip"
            If FileUtils.Exists(ZipFileAddress) Then
                FileUtils.Delete(ZipFileAddress)
                IniClearCache(SetupFileAddress)
                Dim EnabledResourcePack As String = ReadIni(SetupFileAddress, "resourcePacks", "[]").TrimStart("[").TrimEnd("]")
                Dim EnabledResourcePacks As New List(Of String)(EnabledResourcePack.Split(","))
                EnabledResourcePacks.Remove("""file/PCL2 Skin.zip""")
                EnabledResourcePacks.Remove("""PCL2 Skin.zip""")
                WriteIni(SetupFileAddress, "resourcePacks", "[" & EnabledResourcePacks.Join(","c) & "]")
            End If
        Catch ex As Exception
            Logger.Warn(ex, "清理旧的自定义皮肤资源包失败")
        End Try

    End Sub
    Private Sub McLaunchCustom(Loader As LoaderTask(Of Integer, Integer))

        'Get custom commands
        Dim CustomCommandGlobal As String = Settings.Get(Of String)("LaunchAdvanceRun")
        If CustomCommandGlobal <> "" Then CustomCommandGlobal = ArgumentReplace(CustomCommandGlobal)
        Dim CustomCommandVersion As String = Settings.Get(Of String)("VersionAdvanceRun", Instance:=McInstanceSelected)
        If CustomCommandVersion <> "" Then CustomCommandVersion = ArgumentReplace(CustomCommandVersion)

        'Write the bat file
        Try
            Dim CmdString As String =
                $"chcp {If(McLaunchEncoding.CodePage = 54936 AndAlso IsGBKEncoding, 936, McLaunchEncoding.CodePage)}>nul" & vbCrLf &
                "@echo off" & vbCrLf &
                $"title 启动 - {McInstanceSelected.Name}" & vbCrLf &
                "echo 游戏正在启动，请稍候。" & vbCrLf &
                $"cd /D ""{PathUtils.ToShortPath(McInstanceSelected.PathIndie)}""" & vbCrLf &
                CustomCommandGlobal & vbCrLf &
                CustomCommandVersion & vbCrLf &
                $"""{McLaunchJavaSelected.JavaExePath}"" {McLaunchArgument}" & vbCrLf &
                "echo 游戏已退出。" & vbCrLf &
                "pause"
            FileUtils.Write(
                filePath:=If(CurrentLaunchOptions.SaveBatch, Paths.Base & "PCL\LatestLaunch.bat"),
                text:=FilterAccessToken(CmdString, "F").Replace("%", "%%"),
                encoding:=McLaunchEncoding)
            If CurrentLaunchOptions.SaveBatch IsNot Nothing Then
                McLaunchLog("导出启动脚本完成，强制结束启动过程")
                CanceledHint = "导出启动脚本成功！"
                OpenExplorer(CurrentLaunchOptions.SaveBatch)
                Loader.Parent.Cancel()
                Return 'Script export done
            End If
        Catch ex As Exception
            Logger.Warn(ex, "输出启动脚本失败")
            If CurrentLaunchOptions.SaveBatch IsNot Nothing Then Throw ex 'Fail the launch directly
        End Try

        'Run custom commands
        If CustomCommandGlobal <> "" Then
            McLaunchLog("正在执行全局自定义命令：" & CustomCommandGlobal)
            Dim CustomProcess As Process = Nothing
            Try
                CustomProcess = StartProcess(New ProcessStartInfo With {
                    .FileName = "cmd.exe",
                    .Arguments = $"/c ""{CustomCommandGlobal}""",
                    .WorkingDirectory = McFolderSelected,
                    .UseShellExecute = False,
                    .CreateNoWindow = True
                })
                If Settings.Get(Of Boolean)("LaunchAdvanceRunWait") Then
                    Do Until CustomProcess.HasExited OrElse Loader.IsCanceled
                        Thread.Sleep(10)
                    Loop
                End If
            Catch ex As Exception
                Logger.Error(ex, "执行全局自定义命令失败", LogBehavior.Toast)
            Finally
                If CustomProcess IsNot Nothing AndAlso Not CustomProcess.HasExited AndAlso Loader.IsCanceled Then
                    McLaunchLog("由于取消启动，已强制结束自定义命令 CMD 进程") '#1183
                    CustomProcess.Kill()
                End If
                CustomProcess?.Dispose()
            End Try
        End If
        If CustomCommandVersion <> "" Then
            McLaunchLog("正在执行版本自定义命令：" & CustomCommandVersion)
            Dim CustomProcess As Process = Nothing
            Try
                CustomProcess = StartProcess(New ProcessStartInfo With {
                    .FileName = "cmd.exe",
                    .Arguments = $"/c ""{CustomCommandVersion}""",
                    .WorkingDirectory = McFolderSelected,
                    .UseShellExecute = False,
                    .CreateNoWindow = True
                })
                If Settings.Get(Of Boolean)("VersionAdvanceRunWait", Instance:=McInstanceSelected) Then
                    Do Until CustomProcess.HasExited OrElse Loader.IsCanceled
                        Thread.Sleep(10)
                    Loop
                End If
            Catch ex As Exception
                Logger.Error(ex, "执行版本自定义命令失败", LogBehavior.Toast)
            Finally
                If CustomProcess IsNot Nothing AndAlso Not CustomProcess.HasExited AndAlso Loader.IsCanceled Then
                    McLaunchLog("由于取消启动，已强制结束自定义命令 CMD 进程") '#1183
                    CustomProcess.Kill()
                End If
                CustomProcess?.Dispose()
            End Try
        End If

    End Sub
    Private Sub McLaunchRun(Loader As LoaderTask(Of Integer, Process))

        'Startup info
        Dim StartInfo As New ProcessStartInfo(McLaunchJavaSelected.JavaExePath) 'Using javaw.exe causes #6263

        'Set environment variables
        Dim PathEnv As String = StartInfo.EnvironmentVariables("Path")
        Dim Paths As New List(Of String)
        If Not String.IsNullOrEmpty(PathEnv) Then Paths.AddRange(PathEnv.Split(";"))
        Paths.Add(PathUtils.ToShortPath(McLaunchJavaSelected.Folder))
        StartInfo.EnvironmentVariables("Path") = Paths.Distinct.Join(";"c)
        StartInfo.EnvironmentVariables("appdata") = PathUtils.ToShortPath(McFolderSelected)

        'Set other parameters
        StartInfo.WorkingDirectory = McInstanceSelected.PathIndie
        StartInfo.UseShellExecute = False
        StartInfo.StandardOutputEncoding = McLaunchEncoding
        StartInfo.StandardErrorEncoding = McLaunchEncoding
        StartInfo.RedirectStandardOutput = True
        StartInfo.RedirectStandardError = True
        StartInfo.CreateNoWindow = True
        StartInfo.Arguments = McLaunchArgument

        'Start the process
        Dim GameProcess = StartProcess(StartInfo)
        McLaunchLog("已启动游戏进程：" & McLaunchJavaSelected.JavaExePath)
        If Loader.IsCanceled Then
            McLaunchLog("由于取消启动，已强制结束游戏进程") '#1631
            GameProcess.Kill()
            Return
        End If
        Loader.Output = GameProcess
        McLaunchProcess = GameProcess

        'Process priority handling
        Try
            GameProcess.PriorityBoostEnabled = True
            Select Case Settings.Get(Of Integer)("LaunchArgumentPriority")
                Case 0 'High
                    GameProcess.PriorityClass = ProcessPriorityClass.AboveNormal
                Case 2 'Low
                    GameProcess.PriorityClass = ProcessPriorityClass.BelowNormal
                Case Else 'Normal
            End Select
        Catch ex As Exception
            Logger.Error(ex, "设置进程优先级失败")
        End Try

    End Sub
    Private Sub McLaunchWait(Loader As LoaderTask(Of Process, Integer))

        'Output info
        McLaunchLog("")
        McLaunchLog("~ 基础参数 ~")
        McLaunchLog("PCL 版本：" & VersionDisplay & " (" & VersionCode & ")")
        McLaunchLog($"游戏版本：{McInstanceSelected.VersionDisplayName}（{McInstanceSelected.Version.Vanilla}，Drop {McInstanceSelected.Version.Drop}{If(McInstanceSelected.Version.Reliable, "", "，无法完全确定")}）")
        McLaunchLog("资源版本：" & McAssetsGetIndexName(McInstanceSelected))
        McLaunchLog("版本继承：" & If(McInstanceSelected.InheritName = "", "无", McInstanceSelected.InheritName))
        McLaunchLog("分配的内存：" & PageInstanceSetup.GetRam(McInstanceSelected) & " GB（" & Math.Round(PageInstanceSetup.GetRam(McInstanceSelected) * 1024) & " MB）")
        McLaunchLog("MC 文件夹：" & McFolderSelected)
        McLaunchLog("版本文件夹：" & McInstanceSelected.PathVersion)
        McLaunchLog("版本隔离：" & (McInstanceSelected.PathIndie = McInstanceSelected.PathVersion))
        McLaunchLog("HMCL 格式：" & McInstanceSelected.IsHmclFormatJson)
        McLaunchLog("Java 信息：" & If(McLaunchJavaSelected IsNot Nothing, McLaunchJavaSelected.ToString, "无可用 Java"))
        McLaunchLog("Natives 文件夹：" & GetNativesFolder())
        McLaunchLog("")
        McLaunchLog("~ 登录参数 ~")
        McLaunchLog("玩家用户名：" & McLoginLoader.Output.Name)
        McLaunchLog("AccessToken：" & McLoginLoader.Output.AccessToken)
        McLaunchLog("ClientToken：" & McLoginLoader.Output.ClientToken)
        McLaunchLog("UUID：" & McLoginLoader.Output.Uuid)
        McLaunchLog("登录方式：" & McLoginLoader.Output.Type)
        McLaunchLog("")

        'Get window title
        Dim WindowTitle As String = Settings.Get(Of String)("VersionArgumentTitle", Instance:=McInstanceSelected)
        If WindowTitle = "" Then WindowTitle = Settings.Get(Of String)("LaunchArgumentTitle")
        WindowTitle = ArgumentReplace(WindowTitle, ReplaceTime:=False)

        'Initialize waiting
        Dim Watcher As New Watcher(Loader, McInstanceSelected, WindowTitle)
        McLaunchWatcher = Watcher

        'Wait
        Do While Watcher.State = Watcher.MinecraftState.Loading
            Thread.Sleep(100)
        Loop
        If Watcher.State = Watcher.MinecraftState.Crashed Then
            Throw New OperationCanceledException
        End If

    End Sub
    Private Sub McLaunchEnd()
        McLaunchLog("开始启动结束处理")

        'Launcher visibility
        McLaunchLog("启动器可见性：" & Settings.Get(Of Integer)("LaunchArgumentVisible"))
        Select Case Settings.Get(Of Integer)("LaunchArgumentVisible")
            Case 0
                'Close directly
                McLaunchLog("已根据设置，在启动后关闭启动器")
                RunInUi(Sub() FrmMain.EndProgram(False))
            Case 2, 3
                'Hide
                McLaunchLog("已根据设置，在启动后隐藏启动器")
                RunInUi(Sub() FrmMain.Hidden = True)
            Case 4
                'Minimize
                McLaunchLog("已根据设置，在启动后最小化启动器")
                RunInUi(Sub() FrmMain.WindowState = WindowState.Minimized)
            Case 5
                'Do nothing
        End Select

        'Launch count
        Settings.Set("SystemLaunchCount", Settings.Get(Of Integer)("SystemLaunchCount") + 1)

    End Sub

#End Region

End Module
