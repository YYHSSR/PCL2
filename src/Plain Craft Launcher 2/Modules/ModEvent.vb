

#Region "Attached Properties"

Imports PCL.FormMain

''' <summary>
''' Used to initialize list objects in XAML.
''' Attached properties cannot initialize a separate list object for each instance in XAML, so a wrapper class is needed and must be initialized explicitly in XAML.
''' </summary>
<Markup.ContentProperty("Events")>
Public Class CustomEventCollection
    Implements IEnumerable(Of CustomEvent)
    Dim _Events As New List(Of CustomEvent)
    Public ReadOnly Property Events As List(Of CustomEvent)
        Get
            Return _Events
        End Get
    End Property
    Public Function GetEnumerator() As IEnumerator(Of CustomEvent) Implements IEnumerable(Of CustomEvent).GetEnumerator
        Return DirectCast(Events, IEnumerable(Of CustomEvent)).GetEnumerator()
    End Function
    Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Return DirectCast(Events, IEnumerable).GetEnumerator()
    End Function
End Class

''' <summary>
''' Attached property that provides custom events.
''' </summary>
Public Class CustomEventService

    'Events
    Public Shared ReadOnly EventsProperty As DependencyProperty =
            DependencyProperty.RegisterAttached("Events", GetType(CustomEventCollection), GetType(CustomEventService), New PropertyMetadata(Nothing))
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Sub SetEvents(d As DependencyObject, value As CustomEventCollection)
        d.SetValue(EventsProperty, value)
    End Sub
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Function GetEvents(d As DependencyObject) As CustomEventCollection
        If d.GetValue(EventsProperty) Is Nothing Then d.SetValue(EventsProperty, New CustomEventCollection)
        Return d.GetValue(EventsProperty)
    End Function

    'EventType
    Public Shared ReadOnly EventTypeProperty As DependencyProperty =
            DependencyProperty.RegisterAttached("EventType", GetType(CustomEvent.EventType), GetType(CustomEventService), New PropertyMetadata(Nothing))
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Sub SetEventType(d As DependencyObject, value As CustomEvent.EventType)
        d.SetValue(EventTypeProperty, value)
    End Sub
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Function GetEventType(d As DependencyObject) As CustomEvent.EventType
        Return d.GetValue(EventTypeProperty)
    End Function

    'EventData
    Public Shared ReadOnly EventDataProperty As DependencyProperty =
            DependencyProperty.RegisterAttached("EventData", GetType(String), GetType(CustomEventService), New PropertyMetadata(Nothing))
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Sub SetEventData(d As DependencyObject, value As String)
        d.SetValue(EventDataProperty, value)
    End Sub
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Function GetEventData(d As DependencyObject) As String
        Return d.GetValue(EventDataProperty)
    End Function

End Class

Partial Public Module ModMain

    ''' <summary>
    ''' Raises the custom events on this control.
    ''' The events are executed in a new thread.
    ''' </summary>
    <Runtime.CompilerServices.Extension>
    Public Sub RaiseCustomEvent(Control As DependencyObject)
        'Collect the event list
        Dim Events = CustomEventService.GetEvents(Control).ToList
        Dim EventType = CustomEventService.GetEventType(Control)
        If EventType <> CustomEvent.EventType.None Then Events.Add(New CustomEvent(EventType, CustomEventService.GetEventData(Control)))
        If Not Events.Any Then Return
        RunInNewThread(
        Sub()
            For Each e In Events
                e.Raise()
            Next
        End Sub, "执行自定义事件 " & GetUuid())
    End Sub

End Module

#End Region

''' <summary>
''' Custom event.
''' </summary>
Public Class CustomEvent
    Inherits DependencyObject

#Region "Properties and Raising"

    Public Property Type As EventType
        Get
            Dim Value As EventType = EventType.None
            RunInUiWait(Sub() Value = GetValue(TypeProperty))
            Return Value
        End Get
        Set(value As EventType)
            SetValue(TypeProperty, value)
        End Set
    End Property
    Public Shared ReadOnly TypeProperty As DependencyProperty =
        DependencyProperty.Register("Type", GetType(EventType), GetType(CustomEvent), New PropertyMetadata(EventType.None))

    Public Property Data As String
        Get
            Dim Value As String = Nothing
            RunInUiWait(Sub() Value = GetValue(DataProperty))
            Return Value
        End Get
        Set(value As String)
            SetValue(DataProperty, value)
        End Set
    End Property
    Public Shared ReadOnly DataProperty As DependencyProperty =
        DependencyProperty.Register("Data", GetType(String), GetType(CustomEvent), New PropertyMetadata(Nothing))

    Public Sub New()
    End Sub
    Public Sub New(Type As EventType, Data As String)
        Me.Type = Type
        Me.Data = Data
    End Sub

    ''' <summary>
    ''' Raises this custom event on the current thread.
    ''' </summary>
    Public Sub Raise()
        Raise(Type, Data)
    End Sub

#End Region

    Public Enum EventType
        None = 0
        打开网页 = 1
        打开文件 = 2
        执行命令 = 4
        启动游戏 = 5
        复制文本 = 6
        刷新主页 = 7
        刷新页面 = 8
        弹出窗口 = 13
        弹出提示 = 14
        切换页面 = 15
        导入整合包 = 16
        安装整合包 = 17
        修改设置 = 19
        写入设置 = 20
        修改变量 = 21
        写入变量 = 22
    End Enum

    ''' <summary>
    ''' Raises a custom event on the current thread.
    ''' </summary>
    Public Shared Sub Raise(Type As EventType, Arg As String)
        If Type = EventType.None Then Return
        Logger.Info($"执行自定义事件：{Type}, {Arg}")
        If Arg Is Nothing Then Arg = ""
        Dim Args As String() = Arg.Split("|")
        Dim Arg0 As String = Arg.BeforeFirst("|")
        Dim Arg1 As String = Arg.AfterFirst("|")
        Try
            Select Case Type
                Case EventType.打开网页
                    Arg = Arg.Replace("\", "/")
                    If Not Arg.Contains("://") OrElse Arg.StartsWithF("file", True) Then 'Support more protocols (#2200)
                        MyMsgBox("EventData 必须为一个网址。" & vbCrLf & "如果想要启动程序，请将 EventType 改为 打开文件。", "事件执行失败")
                        Return
                    End If
                    Hint("正在开启，请稍候：" & Arg)
                    RunInThread(Sub() OpenWebsite(Arg))

                Case EventType.打开文件, EventType.执行命令
                    RunInThread(
                    Sub()
                        Try
                            'Resolve the actual path
                            Dim ActualPaths = GetAbsoluteUrls(Arg0, Type)
                            Dim Location = ActualPaths(0), WorkingDir = ActualPaths(1)
                            Logger.Info($"打开类自定义事件实际路径：{Location}，工作目录：{WorkingDir}")
                                If Not EventSafetyConfirm("即将执行：" & Location & If(Args.Length >= 2, " " & Arg1, "")) Then Return
                                Dim Info As New ProcessStartInfo With {
                                    .Arguments = If(Args.Length >= 2, Arg1, ""),
                                    .FileName = Location,
                                    .WorkingDirectory = WorkingDir
                                }
                                StartProcess(Info)
                        Catch ex As Exception
                            Logger.Error(ex, "执行打开类自定义事件失败", LogBehavior.Alert)
                        End Try
                    End Sub)

                Case EventType.启动游戏
                    If Arg0 = "\current" Then
                        If McInstanceSelected Is Nothing Then
                            Hint("请先选择一个 Minecraft 版本！", HintType.Red)
                            Return
                        Else
                            Arg0 = McInstanceSelected.Name
                        End If
                    End If
                    RunInUi(
                    Sub()
                        If McLaunchStart(New McLaunchOptions With
                                {.ServerIp = If(Args.Length >= 2, Args(1), Nothing), .Instance = New McInstance(Arg0)}) Then
                            Hint($"正在启动 {Arg0}……")
                        End If
                    End Sub)

                Case EventType.复制文本
                    ClipboardSet(Arg)

                Case EventType.刷新主页
                    RunInUi(
                    Sub()
                        FrmLaunchRight.ForceRefresh()
                        If String.IsNullOrEmpty(Arg) Then Hint("已刷新！", HintType.Green)
                    End Sub)

                Case EventType.刷新页面
                    If TypeOf FrmMain.PageRight Is IRefreshable Then
                        RunInUiWait(Sub() CType(FrmMain.PageRight, IRefreshable).Refresh())
                        If String.IsNullOrEmpty(Arg) Then Hint("已刷新！", HintType.Green)
                    Else
                        Hint("当前页面不支持刷新操作！", HintType.Red)
                    End If

                Case EventType.弹出窗口
                    If Args.Length = 1 Then Throw New Exception($"EventType {Type} 需要至少 2 个以 | 分割的参数，例如 弹窗标题|弹窗内容")
                    MyMsgBox(Arg1.BeforeLast("|").Replace("\n", vbCrLf), Arg0.Replace("\n", vbCrLf), If(Args.Length > 2, Arg1.AfterLast("|"), "确定"))

                Case EventType.弹出提示
                    Hint(Arg0.Replace("\n", vbCrLf), If(Args.Length = 1, HintType.Blue, EnumUtils.FromString(Of HintType)(Arg1)))

                Case EventType.切换页面
                    RunInUi(
                    Sub()
                        Dim TargetPage = EnumUtils.FromString(Of PageType)(Arg0)
                        If TargetPage = PageType.InstanceSetup Then
                            If McInstanceSelected Is Nothing Then
                                Hint("请先选择一个 Minecraft 版本！", HintType.Red)
                                Return
                            End If
                            If McInstanceLock.IsLocked(McInstanceSelected) Then
                                Hint("该版本正在被修改，暂时无法进入版本设置！", HintType.Red)
                                Return
                            End If
                            PageInstanceLeft.Instance = McInstanceSelected
                        End If
                        FrmMain.PageChange(TargetPage,
                            If(Args.Length = 1, PageSubType.Default, EnumUtils.FromString(Of PageSubType)(Arg1)))
                    End Sub)

                Case EventType.导入整合包, EventType.安装整合包
                    RunInUi(Sub() ModpackInstall())

                Case EventType.修改设置, EventType.写入设置
                    If Args.Length = 1 Then Throw New Exception($"EventType {Type} 需要至少 2 个以 | 分割的参数，例如 ToolDownloadThread|63")
                    Settings.SetSafe(Arg0, Args(1), Instance:=McInstanceSelected)
                    If Args.Length = 2 Then Hint($"已写入设置：{Arg0} → {Args(1)}", HintType.Green)

                Case EventType.修改变量, EventType.写入变量
                    If Args.Length = 1 Then Throw New Exception($"EventType {Type} 需要至少 2 个以 | 分割的参数，例如 VariableName|SomeValue")
                    Try
                        RegistryUtils.Write($"HKEY_CURRENT_USER\Software\{RegFolder}", $"CustomEvent{Arg0}", Args(1))
                        If Args.Length = 2 Then Hint($"已写入变量：{Arg0} → {Args(1)}", HintType.Green)
                    Catch ex As Exception
                        Logger.Warn(ex, $"写入变量失败：{Arg0} → {Args(1)}", LogBehavior.Toast)
                    End Try

                Case Else
                    MyMsgBox("未知的事件类型：" & Type & vbCrLf & "请检查事件类型填写是否正确，或者 PCL 是否为最新版本。", "事件执行失败")
            End Select
        Catch ex As Exception
            Logger.Error(ex, $"事件执行失败（{Type}, {Arg}）", LogBehavior.Alert)
        End Try
    End Sub

    ''' <summary>
    ''' Returns the absolute Url of the custom event. Actually returns {absolute Url, WorkingDir}.
    ''' Throws an exception on failure.
    ''' </summary>
    Public Shared Function GetAbsoluteUrls(RelativeUrl As String, Type As EventType) As String()
        Dim WorkingDir = Paths.Base & "PCL"
        Dim Location = RelativeUrl.Replace("/", "\")
        If Not Path.IsPathRooted(Location) AndAlso FileUtils.Exists(Path.Combine(WorkingDir, Location)) Then
            Location = Path.Combine(WorkingDir, Location)
        End If
        Return {Location, WorkingDir}
    End Function

    ''' <summary>
    ''' Shows a security confirmation dialog. Returns whether to continue.
    ''' </summary>
    Private Shared Function EventSafetyConfirm(Message As String) As Boolean
        If Settings.Get(Of Boolean)("HintCustomCommand") Then Return True
        Select Case MyMsgBox(Message & vbCrLf & "请在确认没有安全隐患后再继续。", "执行确认", "继续", "继续且今后不再要求确认", "取消")
            Case 1
                Return True
            Case 2
                Settings.Set("HintCustomCommand", True)
                Return True
            Case Else
                Return False
        End Select
    End Function

End Class
