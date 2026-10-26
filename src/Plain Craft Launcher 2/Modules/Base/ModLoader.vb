Public Module ModLoader

    'Various loaders
    ''' <summary>
    ''' Common base class for loaders.
    ''' </summary>
    Public MustInherit Class LoaderBase
        Implements ILoadingTrigger

        Public ReadOnly Property IsLoader As Boolean = True Implements ILoadingTrigger.IsLoader

        'Basic properties
        ''' <summary>
        ''' Identifier number of the loader.
        ''' </summary>
        Public Uuid As Integer = GetUuid()
        ''' <summary>
        ''' Name of the loader.
        ''' </summary>
        Public Name As String = "未命名任务 " & Uuid & "#"
        ''' <summary>
        ''' Sync lock used for state change detection.
        ''' </summary>
        Public ReadOnly LockState As New Object
        ''' <summary>
        ''' Parent loader.
        ''' </summary>
        Public Parent As LoaderBase = Nothing
        ''' <summary>
        ''' The topmost loader.
        ''' </summary>
        Public ReadOnly Property RealParent As LoaderBase
            Get
                Try
                    RealParent = Parent
                    While RealParent IsNot Nothing AndAlso RealParent.Parent IsNot Nothing
                        RealParent = RealParent.Parent
                    End While
                Catch ex As Exception
                    Logger.Error(ex, $"获取父加载器失败（{Name}）")
                    Return Nothing
                End Try
            End Get
        End Property

        Public Overridable Sub InitParent(Parent As LoaderBase)
            Me.Parent = Parent
        End Sub

        'Events

        ''' <summary>
        ''' Runs code on the worker thread when the state changes. After adding a handler, HasOnStateChangedThread must be set to True.
        ''' </summary>
        Public Event OnStateChangedThread(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
        Public HasOnStateChangedThread As Boolean = False

        ''' <summary>
        ''' Runs code on the UI thread when the state changes.
        ''' </summary>
        Public Event OnStateChangedUi(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
        ''' <summary>
        ''' A simple way to add handlers on the UI thread. Mainly used to bind events with With when creating a Loader, and for compatibility with old code.
        ''' </summary>
        Public WriteOnly Property OnStateChanged As Action(Of LoaderBase)
            Set(value As Action(Of LoaderBase))
                AddHandler OnStateChangedUi, Sub(Loader As LoaderBase, NewState As LoadState, OldState As LoadState) value(Loader)
            End Set
        End Property

        ''' <summary>
        ''' Called after the loader's target has finished, just before the state becomes Finish. Can be seen as an extension of the loader's target.
        ''' </summary>
        Public Event PreviewFinish(Loader As LoaderBase)
        Protected Sub RaisePreviewFinish()
            RaiseEvent PreviewFinish(Me)
        End Sub

        'State monitoring
        ''' <summary>
        ''' State of the loader.
        ''' </summary>
        Public Property State As LoadState
            Get
                Return _State
            End Get
            Set(value As LoadState)
                If _State = value Then Return
                Dim OldState = _State
                _State = value
                If value = LoadState.Canceled Then CancellationTokenSource?.Cancel()
                Logger.Log($"加载器 {[GetType].Name} {Name} 状态改变：{value}", If(value = LoadState.Failed, LogLevel.Error, LogLevel.Info))
                'Implements the ILoadingTrigger interface and the OnStateChanged callback
                RunInUi(
                Sub()
                    Select Case value
                        Case LoadState.Loading
                            LoadingState = MyLoading.MyLoadingState.Run
                        Case LoadState.Failed
                            LoadingState = MyLoading.MyLoadingState.Error
                        Case Else
                            LoadingState = MyLoading.MyLoadingState.Stop
                    End Select
                    RaiseEvent OnStateChangedUi(Me, value, OldState)
                End Sub)
                If HasOnStateChangedThread Then RunInThread(Sub() RaiseEvent OnStateChangedThread(Me, value, OldState))
            End Set
        End Property
        Private _State As LoadState = LoadState.Waiting
        Public Property LoadingState As MyLoading.MyLoadingState Implements ILoadingTrigger.LoadingState
            Get
                Return _LoadingState
            End Get
            Set(value As MyLoading.MyLoadingState)
                If _LoadingState = value Then Return
                Dim OldState = _LoadingState
                _LoadingState = value
                RaiseEvent LoadingStateChanged(value, OldState)
            End Set
        End Property
        Private _LoadingState As MyLoading.MyLoadingState = MyLoading.MyLoadingState.Stop
        Public Event LoadingStateChanged(NewState As MyLoading.MyLoadingState, OldState As MyLoading.MyLoadingState) Implements ILoadingTrigger.LoadingStateChanged
        ''' <summary>
        ''' If the loader failed, the exception available for external reference.
        ''' </summary>
        Public Property [Error] As Exception = Nothing
        ''' <summary>
        ''' When loaded via LoaderCombo, whether this task blocks subsequent tasks.
        ''' </summary>
        Public Block As Boolean = True
        ''' <summary>
        ''' Whether this loader is shown in the list.
        ''' </summary>
        Public Show As Boolean = True
        ''' <summary>
        ''' Whether the current loader was forcibly started via IsForceRestart.
        ''' This property does nothing by itself; it is for the loader's function, so that when one loader calls another, the force restart flag can be inherited.
        ''' </summary>
        Public IsForceRestarting As Boolean = False

        'Progress monitoring
        ''' <summary>
        ''' Execution progress of the loader, a decimal from 0 to 1.
        ''' </summary>
        Public Overridable Property Progress As Double
            Get
                Select Case State
                    Case LoadState.Waiting
                        Return 0
                    Case LoadState.Loading
                        Return If(_Progress = -1, 0, _Progress)
                    Case Else
                        Return 1
                End Select
            End Get
            Set(value As Double)
                If _Progress = value Then Return
                Dim OldValue = _Progress
                _Progress = value
                RaiseEvent ProgressChanged(value, OldValue)
            End Set
        End Property
        Private _Progress As Double = -1
        Public Event ProgressChanged(NewProgress As Double, OldProgress As Double) Implements ILoadingTrigger.ProgressChanged
        ''' <summary>
        ''' Creates a ProgressProvider whose progress is mapped one-way onto this loader whenever it changes.
        ''' This is the Loader's compatibility layer for the new framework.
        ''' </summary>
        Public Function CreateSyncProgressProvider(Optional FromPercentage As Double = 0, Optional ToPercentage As Double = 1) As ProgressProvider
            Dim Provider As New ProgressProvider
            AddHandler Provider.ProgressChanged,
            Sub(ChangedProgress)
                Dim RawProgress = Provider.GetRaw()
                Progress = FromPercentage + (RawProgress.actual + RawProgress.skiped) * (ToPercentage - FromPercentage)
            End Sub
            Return Provider
        End Function
        ''' <summary>
        ''' Weight when computing total progress. It should be the expected time (seconds).
        ''' </summary>
        Public Property ProgressWeight As Double = 1

        'State changes
        Public MustOverride Sub Start(Optional Input As Object = Nothing, Optional IsForceRestart As Boolean = False)
        Public MustOverride Sub Cancel()
        Public MustOverride Sub Failed(Ex As Exception)
        Private CancellationTokenSource As CancellationTokenSource
        ''' <summary>
        ''' Creates a cancellation token that is cancelled with this loader; cancelling the token also calls this loader's Cancel.
        ''' This is the Loader's compatibility layer for the new framework.
        ''' </summary>
        Public Function CreateCancellationToken() As CancellationToken
            SyncLock LockState
                If CancellationTokenSource Is Nothing OrElse CancellationTokenSource.IsCancellationRequested Then
                    CancellationTokenSource = New CancellationTokenSource
                    CancellationTokenSource.Token.Register(AddressOf Cancel)
                End If
                If State = LoadState.Canceled Then CancellationTokenSource.Cancel()
                Return CancellationTokenSource.Token
            End SyncLock
        End Function

        'Waiting for completion
        Public Const WaitForExitTimeoutMessage As String = "等待加载器执行超时。"

        ''' <summary>
        ''' Waits indefinitely for the loader to finish or throw. Starts the loader if it has not started yet.
        ''' </summary>
        Public Sub WaitForExit(Optional Input As Object = Nothing, Optional LoaderToSyncProgress As LoaderBase = Nothing, Optional IsForceRestart As Boolean = False,
                               Optional c As CancellationToken = Nothing, Optional p As ProgressProvider = Nothing)
            Start(Input, IsForceRestart)
            Do While State = LoadState.Loading
                If LoaderToSyncProgress IsNot Nothing Then LoaderToSyncProgress.Progress = Progress
                p?.Set(Progress)
                c.ThrowIfCancellationRequested()
                Thread.Sleep(10)
            Loop
            If State = LoadState.Finished Then
                Return
            ElseIf State = LoadState.Canceled Then
                Throw New OperationCanceledException("加载器执行已中断")
            ElseIf IsNothing([Error]) Then
                Throw New Exception("未知错误！")
            Else
                Throw New Exception([Error].Message, [Error]) 'Preserve the call stack without affecting message output and unit tests
            End If
        End Sub
        ''' <summary>
        ''' Waits for the loader to finish, throw, or time out. Starts the loader if it has not started yet.
        ''' </summary>
        ''' <param name="Timeout">Wait timeout in milliseconds.</param>
        ''' <param name="TimeoutMessage">Exception message thrown on timeout.</param>
        Public Sub WaitForExitTime(Timeout As Integer, Optional Input As Object = Nothing, Optional TimeoutMessage As String = WaitForExitTimeoutMessage, Optional LoaderToSyncProgress As Object = Nothing, Optional IsForceRestart As Boolean = False)
            Start(Input, IsForceRestart)
            Do While State = LoadState.Loading
                If LoaderToSyncProgress IsNot Nothing Then LoaderToSyncProgress.Progress = Progress
                Thread.Sleep(10)
                Timeout -= 10
                If Timeout < 0 Then Throw New TimeoutException(TimeoutMessage)
            Loop
            If State = LoadState.Finished Then
                Return
            ElseIf State = LoadState.Canceled Then
                Throw New OperationCanceledException("加载器执行已中断")
            ElseIf IsNothing([Error]) Then
                Throw New Exception("未知错误！")
            Else
                Throw [Error]
            End If
        End Sub

        'Equivalent overloads
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim base = TryCast(obj, LoaderBase)
            Return base IsNot Nothing AndAlso Uuid = base.Uuid
        End Function

    End Class
    ''' <summary>
    ''' A loader that runs and monitors a single function asynchronously.
    ''' </summary>
    Public Class LoaderTask(Of TIn, TOut)
        Inherits LoaderBase

        'Thread settings
        Protected Friend ThreadPriority As ThreadPriority
        'Execution event
        Protected Friend LoadDelegate As Action(Of LoaderTask(Of TIn, TOut))
        Protected Friend InputDelegate As Func(Of Object)
        'State indicators
        ''' <summary>
        ''' Whether the current execution thread should abort. Only check this on the loader's worker thread; not callable across threads.
        ''' </summary>
        Public ReadOnly Property IsCanceled As Boolean
            Get
                Return IsCanceledWithThread(Thread.CurrentThread)
            End Get
        End Property
        ''' <summary>
        ''' Whether the current execution thread should abort. The loader thread must be passed manually, for cross-thread checks.
        ''' </summary>
        Public Function IsCanceledWithThread(Thread As Thread) As Boolean
            Return LastRunningThread Is Nothing OrElse Not ReferenceEquals(Thread, LastRunningThread) OrElse State = LoadState.Canceled
        End Function
        ''' <summary>
        ''' Timeout in milliseconds for reusing the previous result when the input is the same.
        ''' </summary>
        Public ReloadTimeout As Integer = -1
        ''' <summary>
        ''' Time of the last completed load.
        ''' </summary>
        Public LastFinishedTime As Long = 0
        ''' <summary>
        ''' The thread that last ran the loader. May be Nothing, or the thread may have ended.
        ''' </summary>
        Public LastRunningThread As Thread = Nothing
        'Input and output
        Public Input As TIn = Nothing
        Public Output As TOut = Nothing

        'Get the input
        Public Function StartGetInput(Optional Input As TIn = Nothing, Optional InputDelegate As Func(Of TIn) = Nothing) As TIn 'InputDelegate may be called anonymously
            If InputDelegate Is Nothing Then InputDelegate = Me.InputDelegate
            Dim NewInput As TIn = Nothing 'If InputType cannot be Nothing, Input Is Nothing would always fail, so an extra check is needed
            If (Input Is Nothing OrElse (NewInput IsNot Nothing AndAlso Input.Equals(NewInput))) AndAlso InputDelegate IsNot Nothing Then
                RunInUiWait(Sub() Input = InputDelegate())
            End If
            Return Input
        End Function
        'Code execution
        Public Function ShouldStart(ByRef Input As TIn, Optional IsForceRestart As Boolean = False, Optional IgnoreReloadTimeout As Boolean = False) As Boolean
            'Get the input
            Try
                Input = StartGetInput(Input)
            Catch ex As Exception
                Logger.Error(ex, $"加载输入获取失败（{Name}）", LogBehavior.Toast)
                [Error] = ex
                SyncLock LockState
                    State = LoadState.Failed
                End SyncLock
                Return False
            End Try
            'Check the input to decide what to do
            If IsForceRestart Then Return True 'Restart is forced
            If ((Input Is Nothing) <> (Me.Input Is Nothing)) OrElse (Input IsNot Nothing AndAlso Not Input.Equals(Me.Input)) Then Return True 'Input differs

            If (State = LoadState.Loading OrElse State = LoadState.Finished) AndAlso 'Loading or already finished...
               (IgnoreReloadTimeout OrElse ReloadTimeout = -1 OrElse
               LastFinishedTime = 0 OrElse GetTimeMs() - LastFinishedTime < ReloadTimeout) Then '...and not timed out...
                Return False '...then do not retry
            Else
                Return True 'Otherwise a restart is needed
            End If
        End Function
        Public Overrides Sub Start(Optional Input As Object = Nothing, Optional IsForceRestart As Boolean = False)
            'Decide whether to start loading
            If ShouldStart(Input, IsForceRestart) Then
                'Input differs or failed, start loading
                If State = LoadState.Loading Then TriggerThreadInterrupt()
                Me.Input = Input
                SyncLock LockState
                    State = LoadState.Loading
                    Progress = -1
                End SyncLock
            Else
                Return
            End If

            LastRunningThread = New Thread(
            Sub()
                Try
                    IsForceRestarting = IsForceRestart
                    Logger.Trace(Function() $"加载线程 {Name} 已{If(IsForceRestarting, "强制", "")}启动")
                    LoadDelegate(Me)
                    If IsCanceled Then
                        Logger.Warn($"加载线程 {Name} 已中断但线程正常运行至结束，输出被弃用（最新线程：{If(LastRunningThread Is Nothing, -1, LastRunningThread.ManagedThreadId)}）")
                        Return
                    End If
                    Logger.Trace(Function() $"加载线程 {Name} 已完成")
                    RaisePreviewFinish()
                    State = LoadState.Finished
                    LastFinishedTime = GetTimeMs() 'Not aborted, this output is valid
                Catch ex As Exception
                    If ex.IsCanceled Then
                        'If the thread ended early because it detected IsCanceled, a new thread has been restarted, so the state should not be changed to Canceled
                        'If the thread threw a cancellation exception manually without IsCanceled, no thread was restarted; this usually means the user cancelled, so it should be changed to Canceled
                        Logger.Trace(ex, $"加载线程 {Name} 已取消，当前已完成 {Math.Round(Progress * 100)}%")
                        If Not IsCanceled Then State = LoadState.Canceled
                    Else
                        Failed(ex)
                    End If
                End Try
            End Sub) With {.Name = "L/" & Name, .Priority = ThreadPriority}
            Try
                LastRunningThread.Start() 'RunInNewThread cannot be used, or the thread may finish before the function returns and IsCanceled would be misjudged
            Catch ex As ThreadStateException 'On the occasional "thread is running or terminated" error, wait and retry
                Thread.Sleep(500)
                LastRunningThread.Start()
            End Try
        End Sub
        Public Overrides Sub Failed(ex As Exception)
            [Error] = ex
            SyncLock LockState
                If IsCanceled OrElse State >= LoadState.Finished Then Return
                State = LoadState.Failed
            End SyncLock
            Logger.Warn(ex, $"加载线程 {Name} 出错，已完成 {Math.Round(Progress * 100)}%")
            TriggerThreadInterrupt()
        End Sub
        Public Overrides Sub Cancel()
            SyncLock LockState
                If State <> LoadState.Loading Then Return
                State = LoadState.Canceled
            End SyncLock
            TriggerThreadInterrupt()
        End Sub
        Private Sub TriggerThreadInterrupt()
            If LastRunningThread Is Nothing Then Return
            If LastRunningThread.IsAlive Then
                LastRunningThread.Interrupt()
                Logger.Trace(Function() $"加载线程 {Name} 已中断")
            End If
            LastRunningThread = Nothing
        End Sub

        Public Sub New()
            'Only here to avoid some silly compiler errors (derived classes must override New)
        End Sub
        Public Sub New(Name As String, LoadDelegate As Action(Of LoaderTask(Of TIn, TOut)), Optional InputDelegate As Func(Of TIn) = Nothing, Optional Priority As ThreadPriority = ThreadPriority.Normal)
            Me.Name = Name
            Me.LoadDelegate = LoadDelegate
            Me.InputDelegate = InputDelegate
            ThreadPriority = Priority
        End Sub

    End Class

    ''' <summary>
    ''' Compatibility wrapper for using a new Worker as an old Loader.
    ''' </summary>
    Public Class LoaderWorker
        Inherits LoaderBase
        Public WithEvents Worker As IWorker
        Public Sub New(Name As String, Worker As IWorker)
            Me.Name = Name
            Me.Worker = Worker
            AddHandler Worker.Progress.ProgressChanged, Sub(Raw) Progress = Raw.actual + Raw.skiped
        End Sub

        Public Overrides Sub Start(Optional Input As Object = Nothing, Optional IsForceRestart As Boolean = False)
            SyncLock LockState
                State = LoadState.Loading
            End SyncLock
            Logger.Trace(Function() $"Worker {Name} 已启动")
            IsForceRestarting = IsForceRestart
            Worker.Start()
        End Sub
        Private Sub Worker_Succeeded() Handles Worker.Succeeded
            Logger.Trace(Function() $"Worker {Name} 已完成")
            RaisePreviewFinish()
            SyncLock LockState
                State = LoadState.Finished
            End SyncLock
        End Sub
        Public Overrides Sub Cancel() Handles Worker.Canceled
            SyncLock LockState
                If State <> LoadState.Loading Then Return
                State = LoadState.Canceled
            End SyncLock
            Logger.Trace(Function() $"Worker {Name} 已取消，当前已完成 {Math.Round(Progress * 100)}%")
            Worker.Cancel()
        End Sub
        Public Overrides Sub Failed(Ex As Exception) Handles Worker.Failed
            SyncLock LockState
                If State >= LoadState.Finished Then Return
                State = LoadState.Failed
            End SyncLock
            Logger.Warn(Ex, $"Worker {Name} 出错，已完成 {Math.Round(Progress * 100)}%")
            [Error] = Ex
            Worker.Cancel()
        End Sub
    End Class

    ''' <summary>
    ''' A composite loader that runs multiple loaders in sequence.
    ''' </summary>
    Public Class LoaderCombo(Of InputType)
        Inherits LoaderBase

        Public Overrides Property Progress() As Double
            Get
                Select Case State
                    Case LoadState.Waiting
                        Return 0
                    Case LoadState.Loading
                        Dim Total As Double = 0, Finished As Double = 0
                        For Each Loader In Loaders
                            Total += Loader.ProgressWeight
                            Finished += Loader.ProgressWeight * Loader.Progress
                        Next
                        If Total = 0 Then Return 0
                        Return Finished / Total
                    Case Else
                        Return 1
                End Select
            End Get
            Set(value As Double)
                Throw New Exception("多重加载器不支持设置进度")
            End Set
        End Property

        Public Loaders As New List(Of LoaderBase)
        Public Input As InputType
        Public Sub New(Name As String, Loaders As IEnumerable(Of LoaderBase))
            Me.Loaders.Clear()
            For Each Loader As LoaderBase In Loaders
                If Loader IsNot Nothing Then
                    Me.Loaders.Add(Loader)
                    AddHandler Loader.OnStateChangedThread, AddressOf SubTaskStateChanged
                    Loader.HasOnStateChangedThread = True
                End If
            Next
            InitParent(Nothing)
            Me.Name = Name
        End Sub
        Public Overrides Sub InitParent(Parent As LoaderBase)
            Me.Parent = Parent
            For Each Loader In Loaders
                Loader.InitParent(Me)
            Next
        End Sub
        Public Overrides Sub Start(Optional Input As Object = Nothing, Optional IsForceRestart As Boolean = False)
            IsForceRestarting = IsForceRestart
            'Change the state
            SyncLock LockState
                If State = LoadState.Loading Then
                    Return
                Else
                    State = LoadState.Loading
                End If
            End SyncLock
            'Start loading
            Me.Input = Input
            If IsForceRestart Then
                For Each Loader In Loaders
                    Loader.State = LoadState.Waiting
                Next
            End If
            RunInThread(AddressOf Update)
        End Sub
        Public Overrides Sub Cancel()
            'Change the state
            SyncLock LockState
                If State = LoadState.Loading OrElse State = LoadState.Waiting Then
                    State = LoadState.Canceled
                Else
                    Return
                End If
            End SyncLock
            RunInThread(
            Sub()
                'Abort the loaders
                For Each Loader In Loaders
                    Loader.Cancel()
                Next
            End Sub)
        End Sub
        Public Overrides Sub Failed(Ex As Exception)
            [Error] = Ex 'Set the error before changing the state, so the parent loader can get the exception
            SyncLock LockState
                If State >= LoadState.Finished Then Return
                State = LoadState.Failed
            End SyncLock
            For Each Loader In Loaders
                Loader.Cancel()
            Next
            FrmMain.BtnExtraDownload.ShowRefresh()
        End Sub

        ''' <summary>
        ''' Child task state changed.
        ''' </summary>
        Private Sub SubTaskStateChanged(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
            Select Case NewState
                Case LoadState.Loading
                    'Started, do nothing
                Case LoadState.Waiting
                    'A child loader may temporarily become Waiting due to external input changes, and will restart right away
                    'So do nothing
                Case LoadState.Finished
                    'Finished normally, trigger a refresh
                    Update()
                Case LoadState.Canceled
                    'Aborted, so abort this task too
                    Cancel()
                Case LoadState.Failed
                    'Failed
                    Failed(New Exception(Loader.Name & "失败", Loader.Error))
            End Select
        End Sub
        ''' <summary>
        ''' Triggers an update to start a new loader or finish.
        ''' </summary>
        Private Sub Update()
            If State = LoadState.Finished OrElse State = LoadState.Failed OrElse State = LoadState.Canceled Then Return
            Dim IsFinished As Boolean = True
            Dim Blocked As Boolean = False
            Dim Input As Object = Me.Input
            For Each Loader In Loaders
                Select Case Loader.State
                    Case LoadState.Finished
                        'Check whether LoaderTask needs a restart
                        If GeneralUtils.IsGenericInstanceOf(Loader, GetType(LoaderTask(Of ,))) Then
                            If CType(Loader, Object).ShouldStart(If(Input IsNot Nothing AndAlso Loader.GetType.GenericTypeArguments.First Is Input.GetType, Input, Nothing), IgnoreReloadTimeout:=True) Then
                                Logger.Info($"由于输入条件变更，重启已完成的加载器 {Loader.Name}")
                                GoTo Restart
                            End If
                            'Update the input of the next loader
                            Input = CType(Loader, Object).Output
                        End If
                        'If continuing is not allowed and a loader is already loading, do not start another
                        If Loader.Block AndAlso Not IsFinished Then Blocked = True
                    Case LoadState.Loading
                        'Check whether LoaderTask needs a restart
                        If GeneralUtils.IsGenericInstanceOf(Loader, GetType(LoaderTask(Of ,))) Then
                            If CType(Loader, Object).ShouldStart(If(Input IsNot Nothing AndAlso Loader.GetType.GenericTypeArguments.First Is Input.GetType, Input, Nothing), IgnoreReloadTimeout:=True) Then
                                Logger.Warn($"由于输入条件变更，重启进行中的加载器 {Loader.Name}")
                                GoTo Restart
                            End If
                        End If
                        'A loader is already loading, no need to start another
                        IsFinished = False
                        Blocked = True
                    Case Else
Restart:
                        'Not started, so start the loader
                        IsFinished = False
                        If Blocked Then Continue For
                        If Input IsNot Nothing Then
                            'Only continue if the input type matches the next loader
                            If GeneralUtils.IsGenericInstanceOf(Loader, GetType(LoaderTask(Of ,))) OrElse GeneralUtils.IsGenericInstanceOf(Loader, GetType(LoaderCombo(Of ))) Then
                                Loader.Start(If(Loader.GetType.GenericTypeArguments.First Is Input.GetType, Input, Nothing), IsForceRestarting)
                            ElseIf TypeOf Loader Is LoaderWorker Then
                                Loader.Start(IsForceRestart:=IsForceRestarting)
                            ElseIf TypeOf Loader Is LoaderDownload Then
                                Loader.Start(If(TypeOf Input Is List(Of NetFile), Input, Nothing), IsForceRestarting)
                            Else
                                Throw New Exception("未知的加载器类型（" & Loader.GetType.Name & "）")
                            End If
                        Else
                            Loader.Start(IsForceRestart:=IsForceRestarting)
                        End If
                        'Stop continuing
                        If Loader.Block Then Blocked = True
                End Select
            Next
            If IsFinished Then
                'Completed successfully
                RaisePreviewFinish()
                State = LoadState.Finished
                FrmMain.BtnExtraDownload.ShowRefresh()
            End If
        End Sub

        ''' <summary>
        ''' Gets the list of lowest-level loaders that should be shown to the user, appended to List.
        ''' </summary>
        Public Shared Sub GetLoaderList(Loader As Object, ByRef List As List(Of LoaderBase), Optional RequireShow As Boolean = True)
            For Each SubLoader In Loader.Loaders
                If SubLoader.Show OrElse Not RequireShow Then List.Add(SubLoader)
                If GeneralUtils.IsGenericInstanceOf(SubLoader, GetType(LoaderCombo(Of ))) Then GetLoaderList(SubLoader, List)
            Next
        End Sub
        ''' <summary>
        ''' Gets the list of lowest-level loaders that should be shown to the user, appended to List.
        ''' </summary>
        Public Sub GetLoaderList(ByRef List As List(Of LoaderBase), Optional RequireShow As Boolean = True)
            GetLoaderList(Me, List, RequireShow)
        End Sub
        ''' <summary>
        ''' Gets the list of lowest-level loaders that should be shown to the user.
        ''' </summary>
        Public Function GetLoaderList(Optional RequireShow As Boolean = True) As List(Of LoaderBase)
            Dim List As New List(Of LoaderBase)
            GetLoaderList(List, RequireShow)
            Return List
        End Function

    End Class

    'Taskbar progress bar
    Public LoaderTaskbar As New ConcurrentList(Of LoaderBase)
    Public LoaderTaskbarProgress As Double = 0 'Smoothed progress
    Private LoaderTaskbarProgressLast As Shell.TaskbarItemProgressState = Shell.TaskbarItemProgressState.None

    Public Sub LoaderTaskbarAdd(Of T)(Loader As LoaderCombo(Of T))
        If FrmSpeedLeft IsNot Nothing Then FrmSpeedLeft.TaskRemove(Loader)
        LoaderTaskbar.Add(Loader)
        Logger.Info($"{Loader.Name} 已加入任务列表")
    End Sub
    Public Sub LoaderTaskbarProgressRefresh()
        Try
            If Not LoaderTaskbar.Any() AndAlso LoaderTaskbarProgressLast = Shell.TaskbarItemProgressState.None AndAlso LoaderTaskbarProgress = 1 Then Return
            Dim Tasks = LoaderTaskbar.ToList()
            Dim HasLoading = Tasks.Any(Function(l) l.State = LoadState.Loading)
            Dim NewState As Shell.TaskbarItemProgressState
            Dim NewProgress As Double = If(Tasks.Count = 0, 1, Tasks.Average(Function(l) l.Progress).Clamp(0, 1))
            'Refresh and remove if a single task has aborted or all tasks have finished
            For Each Task In Tasks
                If Not HasLoading OrElse
                   (Task.State = LoadState.Waiting OrElse Task.State = LoadState.Canceled) Then
                    FrmSpeedLeft?.TaskRefresh(Task)
                    LoaderTaskbar.Remove(Task)
                    Logger.Info($"{Task.Name} 已移出任务列表")
                End If
            Next
            'Update the smoothed progress
            If NewProgress <= 0 OrElse NewProgress >= 1 OrElse LoaderTaskbarProgress > NewProgress Then
                LoaderTaskbarProgress = NewProgress
            Else
                LoaderTaskbarProgress = LoaderTaskbarProgress * 0.9 + NewProgress * 0.1
            End If
            RunInUi(Sub() FrmMain.BtnExtraDownload.Progress = LoaderTaskbarProgress)
            'Update the taskbar info
            If Not LoaderTaskbar.Any() OrElse LoaderTaskbarProgress = 1 Then
                NewState = Shell.TaskbarItemProgressState.None
            ElseIf LoaderTaskbarProgress < 0.015 Then
                NewState = Shell.TaskbarItemProgressState.Indeterminate
            Else
                NewState = Shell.TaskbarItemProgressState.Normal
                FrmMain.TaskbarItemInfo.ProgressValue = LoaderTaskbarProgress
            End If
            If LoaderTaskbarProgressLast <> NewState Then
                LoaderTaskbarProgressLast = NewState
                FrmMain.TaskbarItemInfo.ProgressState = NewState
                FrmMain.BtnExtraDownload.ShowRefresh()
            End If
        Catch ex As Exception
            Logger.Error(ex, "刷新任务栏进度显示失败")
        End Try
    End Sub
    Public Function LoaderTaskbarProgressGet() As Double
        Try
            If Not LoaderTaskbar.Any Then Return 1
            Return LoaderTaskbar.Select(Function(l) l.Progress).Average().Clamp(0, 1)
        Catch ex As Exception
            Logger.Error(ex, "获取任务栏进度出错")
            Return 0.5
        End Try
    End Function

    'Folder refresh delegates
    Private LoaderFolderDictionary As New Dictionary(Of LoaderBase, LoaderFolderDictionaryEntry)
    Private Structure LoaderFolderDictionaryEntry
        Public LastCheckTime As Date?
        Public FolderPath As String
        Public Overrides Function Equals(obj As Object) As Boolean
            If TypeOf obj IsNot LoaderFolderDictionaryEntry Then Return False
            Dim entry = DirectCast(obj, LoaderFolderDictionaryEntry)
            Return EqualityComparer(Of Date?).Default.Equals(LastCheckTime, entry.LastCheckTime) AndAlso
                   FolderPath = entry.FolderPath
        End Function
    End Structure
    Public Enum LoaderFolderRunType
        RunOnUpdated
        ForceRun
        UpdateOnly
    End Enum
    ''' <summary>
    ''' Runs a loader that takes folder detection as input. The loader must take a folder path as input.
    ''' Returns whether the loader was run.
    ''' </summary>
    ''' <param name="ExtraPath">Extra path used to check for folder changes. This path is not passed to the loader.</param>
    Public Function LoaderFolderRun(Loader As LoaderBase, FolderPath As String, Type As LoaderFolderRunType, Optional MaxDepth As Integer = 0, Optional ExtraPath As String = "", Optional WaitForExit As Boolean = False) As Boolean
        Dim Value As New LoaderFolderDictionaryEntry With {.FolderPath = FolderPath & ExtraPath, .LastCheckTime = Nothing}
        Try
            'Get the data
            Dim FolderInfo = DirectoryUtils.GetInfo(FolderPath & ExtraPath)
            Value.LastCheckTime = If(FolderInfo.Exists, GetActualLastWriteTimeUtc(FolderInfo, MaxDepth), DirectCast(Nothing, Date?))
            'Skip if already checked
            If Type = LoaderFolderRunType.RunOnUpdated AndAlso LoaderFolderDictionary.ContainsKey(Loader) Then
                If FolderInfo.Exists Then
                    If LoaderFolderDictionary(Loader).LastCheckTime IsNot Nothing AndAlso
                       Value.Equals(LoaderFolderDictionary(Loader)) Then Return False
                Else
                    If LoaderFolderDictionary(Loader).LastCheckTime Is Nothing Then Return False
                End If
            End If
        Catch ex As Exception
            Logger.Warn(ex, "文件夹加载器启动检测出错")
        End Try
        'Write the check data
        LoaderFolderDictionary(Loader) = Value
        'Start checking
        If Type = LoaderFolderRunType.UpdateOnly Then Return False
        If WaitForExit Then
            Loader.WaitForExit(FolderPath, IsForceRestart:=True)
        Else
            Loader.Start(FolderPath, IsForceRestart:=True)
        End If
        Return True
    End Function
    Private Function GetActualLastWriteTimeUtc(FolderInfo As DirectoryInfo, MaxDepth As Integer) As Date
        Dim Time As Date = FolderInfo.LastWriteTimeUtc
        If MaxDepth > 0 Then
            For Each Folder In FolderInfo.EnumerateDirectories
                Dim FolderTime As Date = GetActualLastWriteTimeUtc(Folder, MaxDepth - 1)
                If FolderTime > Time Then Time = FolderTime
            Next
        End If
        Return Time
    End Function

End Module
