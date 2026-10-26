Imports System.Reflection
Imports System.Windows.Threading

Public Class Application

    'Startup
    Private Sub Application_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
        Try
            'Core initialization
            MeloongCore.Main.Init("PCL2Local")
            MeloongCore.Wpf.Main.Init()
            If Not e.Args.Contains("--gpu") Then 'When invoked from the command line, skip the single-instance check and log rotation (#9137)
                WaitingMutex(e)
                Logger.Instance = New PclLogger With {.logFolder = Paths.Base & "PCL\", .MinLevel = If(ModeDebug, LogLevel.Trace, LogLevel.Info)}
            End If
            'Raise the main thread priority
            Thread.CurrentThread.Priority = ThreadPriority.Highest
            'Check the .NET Framework version
            Try
                If CInt(RegistryUtils.TryRead("HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full", "Release", 999999)) < 528040 Then OldEnvironmentAssert()
            Catch ex As Exception
                Logger.Warn(ex, "检查 .NET Framework 版本失败")
            End Try
            'Fix WPF font loading (#3467)
            Try
                Dim _unused As New FormattedText("", Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Fonts.SystemTypefaces.First, 96, New MyColor, DPI)
            Catch ex As UriFormatException
                Environment.SetEnvironmentVariable("windir", Environment.GetEnvironmentVariable("SystemRoot"), EnvironmentVariableTarget.User)
                Dim _unused As New FormattedText("", Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Fonts.SystemTypefaces.First, 96, New MyColor, DPI)
            End Try
            'Handle command-line arguments
            If e.Args.Length > 0 Then
                If e.Args(0) = "--gpu" Then
                    'Adjust GPU settings
                    Try
                        SetGPUPreference(e.Args(1).Trim(""""))
                        Environment.Exit(ProcessReturnValues.TaskDone)
                    Catch ex As Exception
                        Environment.Exit(ProcessReturnValues.Fail)
                    End Try
                End If
            End If
            'Initialize the folder structure
            Try
                DirectoryUtils.Create(Paths.Base & "PCL\")
                CheckPermissionWithException(Paths.Base & "PCL\")
            Catch ex As Exception
                MsgBox($"PCL 无法使用当前文件夹（{Paths.Base}PCL\）。" & vbCrLf &
                  "错误原因：" & ex.GetDisplay(True) & vbCrLf & vbCrLf &
                  "请尝试：" & vbCrLf &
                  "1. 将 PCL 移动到其他文件夹" & If(PathUtils.GetDiskName(Paths.Base) = "C"c, "，例如 C 盘和桌面以外的其他位置。", "。") & vbCrLf &
                  "2. 删除当前目录中的 PCL 文件夹，然后再试。" & vbCrLf &
                  "3. 右键 PCL 选择属性，打开 兼容性 中的 以管理员身份运行此程序。",
                MsgBoxStyle.Critical, "运行环境错误")
                Environment.[Exit](ProcessReturnValues.Cancel)
            End Try
RetryCacheCheck:
            Try
                DirectoryUtils.Create(PathTemp)
                CheckPermissionWithException(PathTemp)
            Catch ex As Exception
                If PathUtils.IsSame(PathTemp, Path.GetTempPath() & "PCL2Local\") Then
                    MyMsgBox("PCL 无法访问缓存文件夹，可能导致程序出错或无法正常使用！" & vbCrLf & vbCrLf & "错误原因：" & ex.GetDisplay(True), "缓存文件夹不可用")
                Else
                    MyMsgBox("手动设置的缓存文件夹不可用，PCL 将使用默认缓存文件夹。" & vbCrLf & vbCrLf & "错误原因：" & ex.GetDisplay(True), "缓存文件夹不可用")
                    PathTemp = Path.GetTempPath() & "PCL2Local\"
                    GoTo RetryCacheCheck
                End If
            End Try
            DirectoryUtils.Create(PathTemp & "Cache\")
            DirectoryUtils.Create(Paths.AppDataThenName)
            'Set ToolTipService defaults
            ToolTipService.InitialShowDelayProperty.OverrideMetadata(GetType(DependencyObject), New FrameworkPropertyMetadata(100))
            'Set network configuration defaults
            ServicePointManager.Expect100Continue = False
            ServicePointManager.DefaultConnectionLimit = 10000
            ServicePointManager.UseNagleAlgorithm = False
            ServicePointManager.EnableDnsRoundRobin = True
            ServicePointManager.ReusePort = True
            If Environment.OSVersion.Version < New Version(6, 2) Then 'Windows 7 fallback (#8519)
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12
            End If
            'Set up the initial window
            'Basic info
            Logger.Info($"程序版本：{VersionDisplay} ({VersionCode}{If(CommitHash = "", "", $"，#{CommitHash}")})")
            If BuildType = BuildTypes.Snapshot Then
                Logger.Info($"识别码：{Identify}")
            Else
                Logger.Info($"识别码：{Identify}")
            End If
            Logger.Info($"程序路径：{PathExe}")
            Logger.Info($"系统编码：{Encoding.Default.HeaderName} ({Encoding.Default.CodePage}, GBK={IsGBKEncoding})")
            Logger.Info($"管理员权限：{WindowsUtils.HasAdminRole()}")
            'Detect abnormal environments
            If Paths.Base.Contains(Path.GetTempPath()) OrElse Paths.Base.Contains("AppData\Local\Temp\") Then
                MyMsgBox("请将 PCL 从压缩包中解压后再使用！" & vbCrLf & "如果不会解压，可以在网上寻找教程。", "需要解压！", "我知道了", IsWarn:=True, ForceWait:=True)
                FormMain.EndProgramForce(ProcessReturnValues.Cancel)
            End If
            If Not Environment.Is64BitOperatingSystem Then
                MyMsgBox("PCL 和新版 Minecraft 均不再支持 32 位系统，请重装为 64 位系统后再进行游戏！", "环境警告", "我知道了", IsWarn:=True, ForceWait:=True)
                FormMain.EndProgramForce(ProcessReturnValues.Cancel)
            End If
            'Timing
            Logger.Info($"第一阶段加载用时：{GetTimeMs() - ApplicationStartTick} ms")
            ApplicationStartTick = GetTimeMs()
            AniControlEnabled += 1
        Catch ex As Exception
            Dim FilePath As String = Nothing
            Try
                FilePath = PathExe
            Catch
            End Try
            MsgBox(ex.GetDisplay(True) & vbCrLf & "PCL 所在路径：" & If(String.IsNullOrEmpty(FilePath), "获取失败", FilePath), MsgBoxStyle.Critical, "PCL 初始化错误")
            FormMain.EndProgramForce(ProcessReturnValues.Exception)
        End Try
    End Sub
    ''' <summary>
    ''' Requires the program to run as a single instance.
    ''' If it is already running, bring its window to the front, play a sound, and exit the current process.
    ''' </summary>
    Private Shared Sub WaitingMutex(e As StartupEventArgs)
        If BuildType = BuildTypes.Debug Then Return
        Try
            Dim MutexCreatedNew As Boolean
            PclMutex = New Mutex(True, "PCL2Local_SingletonMutex", MutexCreatedNew)
            If MutexCreatedNew Then Return
            Logger.Warn("已有一个 PCL 实例正在运行")
            'Wait for the existing PCL to exit
            If e.Args.Length > 0 AndAlso e.Args(0) = "--wait" Then
                Try
                    If PclMutex.WaitOne(10000) Then Return 'Wait for at most 10 seconds
                Catch Ignored As AbandonedMutexException
                    Return 'The existing instance exited abnormally, continue starting
                End Try
            End If
            'Bring the existing PCL window to the front
            Dim WindowHwnd As IntPtr = FindWindow(Nothing, "Plain Craft Launcher　")
            If WindowHwnd = IntPtr.Zero Then WindowHwnd = FindWindow(Nothing, "Plain Craft Launcher 2　")
            If WindowHwnd <> IntPtr.Zero Then ShowWindowToTop(WindowHwnd)
            'Play a sound and exit
            Beep()
            Environment.[Exit](ProcessReturnValues.Cancel)
        Catch ex As Exception
            Logger.Warn(ex, "单例检查失败")
        End Try
    End Sub
    Private Shared PclMutex As Mutex

    'Exit
    Private Sub Application_SessionEnding(sender As Object, e As SessionEndingCancelEventArgs) Handles Me.SessionEnding
        FrmMain?.EndProgram(False)
    End Sub

    'Exceptions
    Private Sub Application_DispatcherUnhandledException(sender As Object, e As DispatcherUnhandledExceptionEventArgs) Handles Me.DispatcherUnhandledException
        On Error Resume Next
        'Forward to the page's Dispatcher
        If FrmMain?.PageLeft IsNot Nothing AndAlso TypeOf FrmMain.PageLeft Is IDispatcherUnhandledException Then
            CType(FrmMain.PageLeft, IDispatcherUnhandledException).DispatcherUnhandledException(sender, e)
            If e.Handled Then Return
        End If
        If FrmMain?.PageRight IsNot Nothing AndAlso TypeOf FrmMain.PageRight Is IDispatcherUnhandledException Then
            CType(FrmMain.PageRight, IDispatcherUnhandledException).DispatcherUnhandledException(sender, e)
            If e.Handled Then Return
        End If
        'Normal handling
        e.Handled = True
        If IsProgramEnding Then Return
        WriteDiagnostics()
        Dim Detail As String = e.Exception.GetDisplay(True)
        If Detail.Contains("System.Windows.Threading.Dispatcher.Invoke") OrElse Detail.Contains("MS.Internal.AppModel.ITaskbarList.HrInit") OrElse Detail.Contains("未能加载文件或程序集") OrElse
           Detail.Contains(".NET Framework") Then 'Analysis result of the automatic error diagnosis
            OldEnvironmentAssert(e.Exception)
        Else
            Logger.Error(e.Exception, "程序出现未知错误", LogBehavior.AlertThenCrash)
        End If
    End Sub
    Private Shared Sub OldEnvironmentAssert(Optional ex As Exception = Nothing)
        OpenWebsite("https://dotnet.microsoft.com/zh-cn/download/dotnet-framework/thank-you/net48-offline-installer")
        If ex Is Nothing Then
            Logger.Error($"你的 .NET Framework 版本过低或损坏，请下载并重新安装 .NET Framework 4.8！{vbCrLf}若安装失败，可以先卸载高版本的 .NET Framework 然后再试。", LogBehavior.AlertThenCrash)
        Else
            Logger.Error(ex, $"你的 .NET Framework 版本过低或损坏，请下载并重新安装 .NET Framework 4.8！{vbCrLf}若安装失败，可以先卸载高版本的 .NET Framework 然后再试。", LogBehavior.AlertThenCrash)
        End If
    End Sub

    'Dynamic DLL loading
    Private Sub New() 'Set up the Chinese locale and register embedded assembly resolution before using any third-party library.
        Dim Chinese = Globalization.CultureInfo.GetCultureInfo("zh-CN")
        Globalization.CultureInfo.DefaultThreadCurrentCulture = Chinese
        Globalization.CultureInfo.DefaultThreadCurrentUICulture = Chinese
        Thread.CurrentThread.CurrentCulture = Chinese
        Thread.CurrentThread.CurrentUICulture = Chinese
        FrameworkElement.LanguageProperty.OverrideMetadata(GetType(FrameworkElement), New FrameworkPropertyMetadata(Markup.XmlLanguage.GetLanguage("zh-CN")))
        Static Prefixes As String() = {"Newtonsoft.Json", "Ookii.Dialogs.Wpf", "Imazen.WebP", "CacheCow.Common", "CacheCow.Client.FileStore", "CacheCow.Client", "ThrottleDebounce", "System.Net.Http.Formatting", "Microsoft.Win32.Registry", "System.Security.AccessControl", "System.Security.Principal.Windows", "PCLCS", "MeloongCore.Wpf", "MeloongCore"}
        Static LoadedAssemblies As New ConcurrentDictionary(Of String, Lazy(Of Assembly))(StringComparer.Ordinal) 'Cache
        AddHandler AppDomain.CurrentDomain.AssemblyResolve,
        Function(sender As Object, Args As ResolveEventArgs) As Assembly
            Dim RequestedName = New AssemblyName(Args.Name).Name
            Dim Prefix As String = Prefixes.FirstOrDefault(Function(p) p.Equals(RequestedName, StringComparison.OrdinalIgnoreCase))
            If Prefix Is Nothing Then Return Nothing
            Dim LazyAssembly = LoadedAssemblies.GetOrAdd(Prefix, Function(p) New Lazy(Of Assembly)(
            Function()
                Dim LoadedAssembly = Assembly.Load(DirectCast(My.Resources.ResourceManager.GetObject(p), Byte()))
                If p = "Imazen.WebP" Then ExtractLibwebp() 'Special case for WebP
                Return LoadedAssembly
            End Function, LazyThreadSafetyMode.ExecutionAndPublication))
            Return LazyAssembly.Value
        End Function
    End Sub
    Private Shared Sub ExtractLibwebp() 'This method calls ModBase, which in turn calls MeloongCore, so it cannot be placed in New
        SetDllDirectory(PathUtils.RemoveSlashSuffix(PathPure.Value))
        Try
            ExtractResources(PathPure.Value & "libwebp.dll", "libwebp64")
        Catch ex As Exception
            Logger.Warn(ex, "写入 libwebp.dll 失败") 'Prevents a crash when several images load at once and write the file simultaneously, causing the file to be locked
        End Try
    End Sub
    Private Declare Function SetDllDirectory Lib "kernel32" Alias "SetDllDirectoryA" (lpPathName As String) As Boolean

    'Control template events
    Private Sub MyIconButton_Click(sender As Object, e As EventArgs)
        Select Case Settings.Get(Of McLoginType)("LoginType")
            Case McLoginType.Ms
                'Microsoft
                Dim MsJson As JObject = Settings.Get(Of String)("LoginMsJson").DeserializeJson()
                MsJson.Remove(sender.Tag)
                Settings.Set("LoginMsJson", MsJson.ToString(Newtonsoft.Json.Formatting.None))
                If FrmLoginMs.ComboAccounts.SelectedItem Is sender.Parent Then FrmLoginMs.ComboAccounts.SelectedIndex = 0
                FrmLoginMs.ComboAccounts.Items.Remove(sender.Parent)
            Case McLoginType.Legacy
                'Offline
                Dim Names As New List(Of String)
                Names.AddRange(Settings.Get(Of String)("LoginLegacyName").ToString.Split("¨"))
                Names.Remove(sender.Tag)
                Settings.Set("LoginLegacyName", Names.Join("¨"c))
                FrmLoginLegacy.ComboName.ItemsSource = Names
                FrmLoginLegacy.ComboName.Text = If(Names.Any, Names(0), "")
            Case Else
                'Third-party
                Dim Token As String = Settings.Get(Of McLoginType)("LoginType").ToString()
                Dim Dict As New Dictionary(Of String, String)
                Dim Names As New List(Of String)
                Dim Passs As New List(Of String)
                If Not Settings.Get(Of String)("Login" & Token & "Email") = "" Then Names.AddRange(Settings.Get(Of String)("Login" & Token & "Email").Split("¨"))
                If Not Settings.Get(Of String)("Login" & Token & "Pass") = "" Then Passs.AddRange(Settings.Get(Of String)("Login" & Token & "Pass").Split("¨"))
                For i = 0 To Names.Count - 1
                    Dict.Add(Names(i), Passs(i))
                Next
                Dict.Remove(sender.Tag)
                Settings.Set("Login" & Token & "Email", Dict.Keys.Join("¨"c))
                Settings.Set("Login" & Token & "Pass", Dict.Values.Join("¨"c))
                Select Case Token
                    Case "Nide"
                        FrmLoginNide.ComboName.ItemsSource = Dict.Keys
                        FrmLoginNide.ComboName.Text = If(Dict.Keys.Any, Dict.Keys(0), "")
                        FrmLoginNide.TextPass.Password = If(Dict.Values.Any, Dict.Values(0), "")
                    Case "Auth"
                        FrmLoginAuth.ComboName.ItemsSource = Dict.Keys
                        FrmLoginAuth.ComboName.Text = If(Dict.Keys.Any, Dict.Keys(0), "")
                        FrmLoginAuth.TextPass.Password = If(Dict.Values.Any, Dict.Values(0), "")
                End Select
        End Select
    End Sub

End Class
