Public Module ModWatcher

    'Watcher for all instances
    Public McWatcherList As New List(Of Watcher)
    Private IsWatcherRunning As Boolean = False
    Public HasRunningMinecraft As Boolean = False
    Private Sub WatcherStateChanged()
        Dim IsRunning As Boolean = False
        Dim TriggerLauncherShutdown As Boolean = True
        For Each Watcher In McWatcherList
            If Watcher.State = Watcher.MinecraftState.Loading OrElse Watcher.State = Watcher.MinecraftState.Running OrElse
               (Watcher.State = Watcher.MinecraftState.Crashed AndAlso Not Watcher.GameProcess.HasExited) Then
                IsRunning = True
                Exit For
            ElseIf Watcher.State = Watcher.MinecraftState.Crashed OrElse Watcher.State = Watcher.MinecraftState.Canceled Then
                TriggerLauncherShutdown = False
            End If
        Next
        If IsWatcherRunning = IsRunning Then Return
        IsWatcherRunning = IsRunning
        If IsWatcherRunning Then
            MinecraftStart()
        Else
            MinecraftStop(TriggerLauncherShutdown)
        End If
    End Sub
    Private Sub MinecraftStart()
        McLaunchLog("[全局] 出现运行中的 Minecraft")
        HasRunningMinecraft = True
        FrmMain.BtnExtraShutdown.ShowRefresh()
    End Sub
    Private Sub MinecraftStop(TriggerLauncherShutdown As Boolean)
        McLaunchLog("[全局] 已无运行中的 Minecraft")
        HasRunningMinecraft = False
        FrmMain.BtnExtraShutdown.ShowRefresh()
        'Launcher visibility
        Select Case Settings.Get(Of Integer)("LaunchArgumentVisible")
            Case 2
                'Close directly
                If TriggerLauncherShutdown Then
                    RunInUi(Sub() FrmMain.EndProgram(False))
                Else
                    RunInUi(Sub() FrmMain.Hidden = False)
                End If
            Case 3
                'Restore
                RunInUi(Sub() FrmMain.Hidden = False)
        End Select
    End Sub

    'Watcher for a single process
    Public Class Watcher

        'Initialize
        Public GameProcess As Process
        Public Instance As McInstance
        Private WindowTitle As String = ""
        Private PID As Integer
        Public Loader As LoaderTask(Of Process, Integer)
        Public Sub New(Loader As LoaderTask(Of Process, Integer), Instance As McInstance, WindowTitle As String)
            Me.Loader = Loader
            Me.Instance = Instance
            Me.WindowTitle = WindowTitle
            Me.PID = Loader.Input.Id
            WatcherLog("开始 Minecraft 日志监控")
            If Me.WindowTitle <> "" Then WatcherLog("要求窗口标题：" & WindowTitle)

            'Update the list
            Dim NewWatcherList As New List(Of Watcher)
            For Each Watch In McWatcherList
                If Watch.State = MinecraftState.Crashed OrElse Watch.State = MinecraftState.Ended OrElse Watch.State = MinecraftState.Canceled Then Continue For
                NewWatcherList.Add(Watch)
            Next
            NewWatcherList.Add(Me)
            McWatcherList = NewWatcherList
            WatcherStateChanged()

            'Initialize the process and log reading
            GameProcess = Loader.Input
            AddHandler GameProcess.OutputDataReceived, AddressOf LogReceived
            AddHandler GameProcess.ErrorDataReceived, AddressOf LogReceived
            GameProcess.BeginOutputReadLine()
            GameProcess.BeginErrorReadLine()

            'Initialize timers
            RunInNewThread(
            Sub()
                Try
                    Do Until State = MinecraftState.Ended OrElse State = MinecraftState.Canceled OrElse
                             (Loader.State = LoadState.Canceled AndAlso State <> MinecraftState.Crashed) OrElse
                             (State = MinecraftState.Crashed AndAlso GameProcess.HasExited)
                        TimerWindow()
                        TimerLog()
                        'Set window title
                        For i = 1 To 3
                            If IsWindowFinished AndAlso IsWindowAppeared AndAlso WindowTitle <> "" AndAlso State = MinecraftState.Running AndAlso Not GameProcess.HasExited Then
                                Dim RealTitle As String = WindowTitle.Replace("{date}", Date.Now.ToString("yyyy'/'M'/'d")).Replace("{time}", Date.Now.ToString("HH':'mm':'ss"))
                                SetWindowText(WindowHandle, RealTitle.ToCharArray)
                            End If
                            Thread.Sleep(64)
                        Next
                    Loop
                    If State = MinecraftState.Crashed Then WatcherStateChanged()
                    WatcherLog("Minecraft 日志监控已退出")
                Catch ex As Exception
                    Logger.Error(ex, "Minecraft 日志监控主循环出错")
                    State = MinecraftState.Ended
                End Try
            End Sub, "Minecraft Watcher PID " & PID)
        End Sub

        'State
        Private _State As MinecraftState = MinecraftState.Loading
        Public Property State As MinecraftState
            Get
                Return _State
            End Get
            Set(value As MinecraftState)
                If _State = value Then Return
                _State = value
                WatcherStateChanged()
            End Set
        End Property
        Public Enum MinecraftState
            Loading
            Running
            Crashed
            Ended
            Canceled
        End Enum

        'Log
        Public WaitingLog As New List(Of String)(1000)
        Private ReadOnly WaitingLogLock As New Object
        Private Sub LogReceived(sender As Object, e As DataReceivedEventArgs)
            SyncLock WaitingLogLock
                WaitingLog.Add(e.Data)
            End SyncLock
        End Sub
        Private Sub TimerLog()
            Try
                'The exit check must run every tick and cannot depend on new log lines arriving
                Dim HasExited As Boolean = GameProcess.HasExited
                'When the process exits, remaining pipe output may still be read; wait so crash analysis doesn't miss the last log lines
                If HasExited Then Thread.Sleep(300)
                'Output text
                Dim Copyed As List(Of String)
                SyncLock WaitingLogLock
                    Copyed = WaitingLog
                    WaitingLog = New List(Of String)(1000)
                End SyncLock
                For Each Str As String In Copyed
                    GameLog(Str)
                Next
                If Copyed.Any() AndAlso State = MinecraftState.Loading Then ProgressUpdate()
                'Game exit check
                If HasExited Then
                    WatcherLog("Minecraft 已退出，返回值：" & GameProcess.ExitCode)
                    If State = MinecraftState.Loading Then
                        'Window never appeared
                        WatcherLog("Minecraft 尚未加载完成，可能已崩溃", LogLevel.Error)
                        Crashed()
                    ElseIf GameProcess.ExitCode <> 0 AndAlso State = MinecraftState.Running AndAlso Instance.ReleaseTime.Year >= 2012 Then
                        'Non-zero exit code before finishing
                        WatcherLog("Minecraft 返回值异常，可能已崩溃", LogLevel.Error)
                        Crashed()
                    ElseIf State <> MinecraftState.Crashed Then
                        'Normal exit
                        State = MinecraftState.Ended
                    End If
                End If
            Catch ex As Exception
                Logger.Error(ex, "输出 Minecraft 日志失败")
            End Try
        End Sub
        Public LatestLog As New ConcurrentQueue(Of String)
        Private Sub GameLog(Text As String)
            'Preprocess
            If Text Is Nothing Then Return
            Text = Text.ReplaceLineEndings(vbCrLf)
            'Add to the pending buffer
            LatestLog.Enqueue(Text)
            If LatestLog.Count >= 501 Then LatestLog.TryDequeue(Nothing)
            Logger.Trace(Text)
            'Progress handling
            If LogProgress < 1 Then
                WatcherLog("日志 1/5：已出现日志输出")
                LogProgress = 1
            End If 'The very first line may already be a log line that needs checking below (repro: launch vanilla 1.15.2)
            If LogProgress < 2 AndAlso Text.Contains("Setting user:") Then
                WatcherLog("日志 2/5：游戏用户已设置") 'Only guaranteed for Minecraft 1.7+
                LogProgress = 2
            ElseIf LogProgress < 3 AndAlso Text.ContainsIgnoreCase("lwjgl version") Then
                WatcherLog("日志 3/5：LWJGL 版本已确认")
                LogProgress = 3
            ElseIf LogProgress < 4 AndAlso (Text.Contains("OpenAL initialized") OrElse Text.Contains("Starting up SoundSystem")) Then
                WatcherLog("日志 4/5：OpenAL 已加载") 'Only guaranteed for Minecraft 1.7+
                LogProgress = 4
            ElseIf LogProgress < 5 AndAlso ((Text.Contains("Created") AndAlso Text.Contains("textures") AndAlso Text.Contains("-atlas")) OrElse Text.Contains("Found animation info")) Then
                WatcherLog("日志 5/5：材质已加载") 'Only guaranteed for Minecraft 1.7+
                LogProgress = 5
            End If
            'Exit and crash detection
            If Not Text.Contains("[CHAT]") Then
                If Text.Contains("Someone is closing me!") OrElse Text.Contains("Restarting Minecraft with command") Then '#1258
                    WatcherLog("识别为关闭的 Log：" & Text)
                    State = MinecraftState.Ended
                ElseIf Text.Contains("Crash report saved to") OrElse Text.Contains("This crash report has been saved to:") Then
                    'Minecraft crashed, ignoring VanillaFix
                    WatcherLog("识别为崩溃的 Log：" & Text, LogLevel.Error)
                    Crashed()
                ElseIf Text.Contains("Could not save crash report to") Then
                    'Minecraft crashed, failed to save crash report
                    WatcherLog("识别为崩溃的 Log：" & Text, LogLevel.Error)
                    Crashed()
                ElseIf Text.Contains("/ERROR]: Unable to launch") OrElse Text.Contains("An exception was thrown, the game will display an error screen and halt.") Then
                    'Forge crash
                    WatcherLog("识别为崩溃的 Log：" & Text, LogLevel.Error)
                    Crashed()
                End If
                'Cannot use "Shutdown failure!" to detect crashes: closing the window via X also prints it
            End If
        End Sub
        Private Sub WatcherLog(Text As String, Optional Level As LogLevel = LogLevel.Info)
            McLaunchLog("[" & PID & "] " & Text, Level)
        End Sub

        'Progress update
        Private LogProgress As Integer = 0
        Private Sub ProgressUpdate()
            Dim CurrentProgress As Double
            If IsWindowAppeared OrElse LogProgress = 5 Then
                CurrentProgress = 0.95
                WatcherLog("Minecraft 加载已完成")
                State = MinecraftState.Running
            Else
                CurrentProgress = Math.Min(LogProgress, 3) / 3 * 0.9
            End If
            Loader.Progress = CurrentProgress
        End Sub

        'Window check
        Private IsWindowAppeared As Boolean = False
        ''' <summary>
        ''' Whether the window check has finished. This does not necessarily mean a window was found (if not, IsWindowAppeared stays False).
        ''' </summary>
        Private IsWindowFinished As Boolean = False
        Private WindowHandle As IntPtr
        Private Sub TimerWindow()
            Try
                If GameProcess.HasExited Then Return
                If IsWindowFinished Then Return
                'Enumerate all windows and check for new ones
                Dim MinecraftWindow As KeyValuePair(Of IntPtr, String)? = Nothing
                Try
                    MinecraftWindow = TryGetMinecraftWindow()
                Catch ex As ComponentModel.Win32Exception
                    'Access denied (#1062)
                    Logger.Error(ex, "由于反作弊或安全软件拦截，PCL 无法操作游戏窗口", LogBehavior.Toast)
                    IsWindowFinished = True
                End Try
                If MinecraftWindow Is Nothing Then Return
                Dim MinecraftWindowName = MinecraftWindow.Value.Value, MinecraftWindowHandle = MinecraftWindow.Value.Key
                'Window found
                If Not MinecraftWindowName.StartsWithF("FML") AndAlso Not MinecraftWindowName.StartsWithF("Quilt Loader") Then
                    'Minecraft window found
                    WindowHandle = MinecraftWindowHandle
                    WatcherLog($"Minecraft 窗口已加载：{MinecraftWindowName}（{MinecraftWindowHandle.ToInt64}）")
                    IsWindowFinished = True
                    'Maximize
                    If Settings.Get(Of Integer)("LaunchArgumentWindowType") = 4 Then
                        RunInNewThread(
                        Sub()
                            Try
                                'If maximizing causes a wrong render size, that's a Minecraft bug, not ours
                                '...but people keep reporting it anyway, so here we are
                                Thread.Sleep(2000)
                                ShowWindow(WindowHandle, 3)
                                WatcherLog($"已最大化 Minecraft 窗口：{MinecraftWindowHandle.ToInt64}")
                            Catch ex As Exception
                                Logger.Warn(ex, "最大化 Minecraft 窗口时出现错误")
                            End Try
                        End Sub, "MinecraftWindowMaximize")
                    End If
                ElseIf Not IsWindowAppeared Then
                    'Mod Loader window found
                    WatcherLog("Mod Loader 窗口已加载：" & MinecraftWindowName & "（" & MinecraftWindowHandle.ToInt64 & "）")
                End If
                IsWindowAppeared = True
            Catch ex As Exception
                Logger.Error(ex, "检查 Minecraft 窗口失败")
            End Try
        End Sub
        ''' <summary>
        ''' Gets the handle and title of the Minecraft window that probably belongs to the current process.
        ''' Nothing means not found.
        ''' </summary>
        Private Function TryGetMinecraftWindow() As KeyValuePair(Of IntPtr, String)?
            Dim Result = Nothing
            EnumWindows(
                Sub(hwnd As IntPtr, lParam As Integer)
                    If Result IsNot Nothing Then Return
                    'Check the class name
                    Dim str As New StringBuilder(512)
                    GetClassName(hwnd, str, str.Capacity)
                    Dim ClassName As String = str.ToString
                    If Not (ClassName = "GLFW30" OrElse ClassName = "SDL_app" OrElse ClassName = "LWJGL" OrElse ClassName = "SunAwtFrame") Then Return
                    'SDL creates a hidden window first and only shows it after initialization (#9073)
                    If ClassName = "SDL_app" AndAlso Not IsWindowVisible(hwnd) Then Return
                    'Get the window title
                    str = New StringBuilder(512)
                    GetWindowText(hwnd, str, str.Capacity)
                    Dim WindowText As String = str.ToString
                    'Some mods can change the window title, so checking for a "Minecraft" prefix is unreliable
                    'Some versions create a "GLFW message window", and newer ones also a "Hidden Utility Window"; exclude both
                    If String.IsNullOrWhiteSpace(WindowText) OrElse WindowText = "SDL_app" OrElse WindowText = "PopupMessageWindow" OrElse
                       WindowText.StartsWithF("GLFW") OrElse WindowText.EndsWithF("Hidden Utility Window") Then Return
                    'Get the process that owns the window
                    Dim ProcessId As Integer
                    GetWindowThreadProcessId(hwnd, ProcessId)
                    Try
                        Using WindowProcess = Process.GetProcessById(ProcessId)
                            If WindowProcess.StartTime < GameProcess.StartTime Then Return 'Must be a process started after the game process
                        End Using
                    Catch ex As Exception
                        Logger.Warn(ex, "枚举 Minecraft 窗口进程失败")
                        Return
                    End Try
                    Result = New KeyValuePair(Of IntPtr, String)(hwnd, WindowText)
                End Sub, 0)
            Return Result
        End Function
        Private Delegate Sub EnumWindowsSub(hwnd As IntPtr, lParam As Integer)
        Private Declare Function EnumWindows Lib "user32" (hWnd As EnumWindowsSub, lParam As Integer) As Boolean
        Private Declare Function GetClassName Lib "user32" Alias "GetClassNameA" (hWnd As Integer, str As StringBuilder, maxCount As Integer) As Integer
        Private Declare Function GetWindowText Lib "user32" Alias "GetWindowTextA" (hWnd As Integer, str As StringBuilder, maxCount As Integer) As Integer
        Private Declare Function SetWindowText Lib "user32" Alias "SetWindowTextA" (hWnd As Integer, str As String) As Boolean
        Private Declare Function ShowWindow Lib "user32" (hWnd As IntPtr, cmdWindow As UInteger) As Boolean
        Private Declare Function IsWindowVisible Lib "user32" (hWnd As IntPtr) As Boolean
        Private Declare Function GetWindowThreadProcessId Lib "user32" (hWnd As IntPtr, ByRef lpdwProcessId As Integer) As Integer

        'Crash handling
        Private Sub Crashed()
            If State = MinecraftState.Crashed OrElse State = MinecraftState.Ended Then Return
            State = MinecraftState.Crashed
            'Crash analysis
            WatcherLog("Minecraft 已崩溃，将在 2 秒后开始崩溃分析", LogLevel.Error)
            Hint("检测到 Minecraft 出现错误，错误分析已开始……", HintType.Red)
            WriteDiagnostics()
            RunInNewThread(
            Sub()
                Try
                    Thread.Sleep(2000)
                    WatcherLog("崩溃分析开始")
                    Dim Analyzer As New CrashAnalyzer(Instance)
                    Analyzer.Collect(Instance.PathIndie, LatestLog.ToList)
                    Analyzer.Prepare()
                    Analyzer.Analyze()
                    Analyzer.Output(False, New List(Of String) From
                        {Instance.PathVersion & Instance.Name & ".json", Paths.BaseThenName & "Log1.txt", Paths.Base & "PCL\LatestLaunch.bat"})
                Catch ex As Exception
                    Logger.Error(ex, "崩溃分析失败")
                End Try
            End Sub, "Crash Analyzer")
        End Sub

        'Force close
        Public Sub Kill()
            State = MinecraftState.Canceled
            WatcherLog("尝试强制结束 Minecraft 进程")
            Try
                If Not GameProcess.HasExited Then GameProcess.Kill()
                WatcherLog("已强制结束 Minecraft 进程")
            Catch ex As Exception
                Logger.Error(ex, "强制结束 Minecraft 进程失败", LogBehavior.Toast)
            End Try
        End Sub

    End Class

End Module
