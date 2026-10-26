Imports System.Collections.ObjectModel
Imports System.Windows.Interop
Imports System.Windows.Threading

Public Module ModMain

#Region "Hints"

    ''' <summary>
    ''' Hint types.
    ''' This enum is used in custom events and is part of the public API.
    ''' </summary>
    Public Enum HintType
        Blue
        Green
        Red
    End Enum
    Private Structure HintMessage
        Public Text As String
        Public Type As HintType
        Public Log As Boolean
    End Structure

    ''' <summary>
    ''' Hints waiting to be shown. Stored as arrays in the form {String, HintType, Log As Boolean}.
    ''' </summary>
    Private HintWaiting As New ConcurrentQueue(Of HintMessage)
    ''' <summary>
    ''' Shows a hint text in the bottom-left corner of the window.
    ''' </summary>
    Public Sub Hint(Text As String, Optional Type As HintType = HintType.Blue, Optional Log As Boolean = True)
        HintWaiting.Enqueue(New HintMessage With {.Text = If(Text, ""), .Type = Type, .Log = Log})
    End Sub

    Private Sub HintTick()
        Try
            'Tag stores: { whether it can be reused, Uuid }
            Dim CurrentHint As HintMessage = Nothing
            Do While HintWaiting.TryDequeue(CurrentHint)
                'Remove line breaks
                CurrentHint.Text = CurrentHint.Text.ReplaceLineEndings(" ", mergeMultiple:=True)
                'Ignore excess hints
                If FrmMain.PanHint.Children.Count >= 20 Then GoTo EndHint
                'Check for duplicate hints
                Dim DoubleStack As Border = Nothing
                For Each stack As Border In FrmMain.PanHint.Children
                    If stack.Tag(0) AndAlso CType(stack.Child, TextBlock).Text = CurrentHint.Text Then DoubleStack = stack
                Next
                'Get the gradient colors
                Dim TargetColor0, TargetColor1 As MyColor
                Dim Percent As Double = 0.3
                Select Case CurrentHint.Type
                    Case HintType.Blue
                        TargetColor0 = New MyColor(215, 37, 155, 252)
                        TargetColor1 = New MyColor(215, 10, 142, 252)
                    Case HintType.Green
                        TargetColor0 = New MyColor(215, 33, 177, 33)
                        TargetColor1 = New MyColor(215, 29, 160, 29)
                    Case Else 'HintType.Red
                        TargetColor0 = New MyColor(215, 255, 53, 11)
                        TargetColor1 = New MyColor(215, 255, 43, 0)
                End Select
                If Not IsNothing(DoubleStack) Then
                    'A duplicate hint exists and its enter animation has already played
                    If Not AniIsRun("Hint Show " & DoubleStack.Tag(1)) Then
                        AniStop("Hint Hide " & DoubleStack.Tag(1))
                        Dim Delay As Double = (800 + CurrentHint.Text.Length.Clamp(5, 23) * 180) * AniSpeed
                        AniStart({
                            AaX(DoubleStack, -12 - DoubleStack.Margin.Left, 50,, New AniEaseOutFluent),
                            AaX(DoubleStack, -8, 50, 50, New AniEaseInFluent),
                            AaX(DoubleStack, 8, 50, 100, New AniEaseOutFluent),
                            AaX(DoubleStack, -8, 50, 150, New AniEaseInFluent),
                            AaDouble(Sub(i)
                                         Percent += i
                                         Dim Gradient As LinearGradientBrush = DoubleStack.Background
                                         Gradient.GradientStops(0).Color = TargetColor0 * Percent + New MyColor(255, 255, 255) * (1 - Percent)
                                         Gradient.GradientStops(1).Color = TargetColor1 * Percent + New MyColor(255, 255, 255) * (1 - Percent)
                                     End Sub, 0.7, 250),
                            AaX(DoubleStack, -50, 200, Delay, New AniEaseInFluent),
                            AaOpacity(DoubleStack, -1, 150, Delay),
                            AaCode(Sub() DoubleStack.Tag(0) = False, Delay),
                            AaHeight(DoubleStack, -26, 100,, New AniEaseOutFluent, True),
                            AaCode(Sub() FrmMain.PanHint.Children.Remove(DoubleStack), , True)
                      }, "Hint Hide " & DoubleStack.Tag(1))
                    End If
                Else
                    'Prepare the control
                    Dim NewHintControl As New Border With {.Tag = {True, GetUuid()}, .Margin = New Thickness(-70, 0, 20, 0), .Opacity = 0, .Height = 0, .HorizontalAlignment = HorizontalAlignment.Left, .CornerRadius = New CornerRadius(0, 6, 6, 0)}
                    NewHintControl.Background = New LinearGradientBrush(New GradientStopCollection(New List(Of GradientStop) From {
                        New GradientStop(TargetColor0 * Percent + New MyColor(255, 255, 255) * (1 - Percent), 0),
                        New GradientStop(TargetColor1 * Percent + New MyColor(255, 255, 255) * (1 - Percent), 1)}), 90)
                    NewHintControl.Child = New TextBlock With {.TextTrimming = TextTrimming.CharacterEllipsis, .FontSize = 13, .Text = CurrentHint.Text, .Foreground = New MyColor(255, 255, 255), .Margin = New Thickness(33, 5, 8, 5)}
                    FrmMain.PanHint.Children.Add(NewHintControl)
                    'Control animation
                    Dim Animations As New List(Of AniData)
                    If FrmMain.PanHint.Children.Count > 1 Then
                        'Other hints exist
                        Animations.Add(AaHeight(NewHintControl, 26, 150, , New AniEaseOutFluent))
                    Else
                        'This is the only hint
                        NewHintControl.Height = 26
                    End If
                    'Start animation
                    Animations.AddRange({
                        AaX(NewHintControl, 30, 400, , New AniEaseOutElastic(AniEasePower.Weak)),
                        AaX(NewHintControl, 20, 200, , New AniEaseOutFluent),
                        AaOpacity(NewHintControl, 1, 100),
                        AaDouble(Sub(i)
                                     Percent += i
                                     Dim Gradient As LinearGradientBrush = NewHintControl.Background
                                     Gradient.GradientStops(0).Color = TargetColor0 * Percent + New MyColor(255, 255, 255) * (1 - Percent)
                                     Gradient.GradientStops(1).Color = TargetColor1 * Percent + New MyColor(255, 255, 255) * (1 - Percent)
                                 End Sub, 0.7, 250, 100)
                    })
                    AniStart(Animations, "Hint Show " & NewHintControl.Tag(1))
                    'End animation
                    Dim Delay As Double = (800 + CurrentHint.Text.Length.Clamp(5, 23) * 180) * AniSpeed
                    AniStart({
                        AaX(NewHintControl, -50, 200, Delay, New AniEaseInFluent),
                        AaOpacity(NewHintControl, -1, 150, Delay),
                        AaCode(Sub() NewHintControl.Tag(0) = False, Delay),
                        AaHeight(NewHintControl, -26, 100,, New AniEaseOutFluent, True),
                        AaCode(Sub() FrmMain.PanHint.Children.Remove(NewHintControl), , True)
                    }, "Hint Hide " & NewHintControl.Tag(1))
                End If
                'Cleanup
EndHint:
                If CurrentHint.Log Then Logger.Info($"弹出提示：{CurrentHint.Text}")
            Loop
        Catch ex As Exception
            Logger.Info(ex, "显示弹出提示失败")
        End Try
    End Sub
#End Region

#Region "Message Boxes"

    ''' <summary>
    ''' Converter that stores message box info.
    ''' </summary>
    Public Class MyMsgBoxConverter
        Public Type As MyMsgBoxType
        Public Title As String
        Public Text As String
        ''' <summary>
        ''' Input mode: the text of the text box.
        ''' Select mode: the IEnumberable(Of IMyRadio) to put in.
        ''' Login mode: the JSON returned in login step 1.
        ''' </summary>
        Public Content As Object
        ''' <summary>
        ''' Input mode: input validation rules.
        ''' </summary>
        Public ValidateRules As Collection(Of Validate)
        ''' <summary>
        ''' Input mode: hint text.
        ''' </summary>
        Public HintText As String = ""
        ''' <summary>
        ''' Whether to highlight the first button when there are multiple buttons.
        ''' </summary>
        Public HighLight As Boolean
        Public Button1 As String = "确定"
        Public Button2 As String = ""
        Public Button3 As String = ""
        ''' <summary>
        ''' Clicking the first button runs this method without closing the message box.
        ''' </summary>
        Public Button1Action As Action = Nothing
        ''' <summary>
        ''' Clicking the second button runs this method without closing the message box.
        ''' </summary>
        Public Button2Action As Action = Nothing
        ''' <summary>
        ''' Clicking the third button runs this method without closing the message box.
        ''' </summary>
        Public Button3Action As Action = Nothing
        Public IsWarn As Boolean = False
        Public ForceWait As Boolean = False
        Public WaitFrame As New DispatcherFrame(True)
        ''' <summary>
        ''' Whether the message box has been closed.
        ''' </summary>
        Public IsExited As Boolean = False
        ''' <summary>
        ''' Input mode: the entered text. Nothing if a button other than the first was clicked.
        ''' Select mode: the index of the clicked button, starting from 1.
        ''' Login mode: a string array {AccessToken, RefreshToken} or an Exception.
        ''' </summary>
        Public Result As Object
    End Class
    Public Enum MyMsgBoxType
        Text
        [Select]
        Input
        Login
    End Enum

    ''' <summary>
    ''' Shows a message box and returns the index of the clicked button (starting from 1).
    ''' </summary>
    ''' <param name="Title">The title of the message box.</param>
    ''' <param name="Caption">The content of the message box.</param>
    ''' <param name="Button1">The first button to show. Defaults to "确定".</param>
    ''' <param name="Button2">The second button to show. Empty by default.</param>
    ''' <param name="Button3">The third button to show. Empty by default.</param>
    ''' <param name="Button1Action">Clicking the first button runs this method without closing the message box.</param>
    ''' <param name="Button2Action">Clicking the second button runs this method without closing the message box.</param>
    ''' <param name="Button3Action">Clicking the third button runs this method without closing the message box.</param>
    ''' <param name="IsWarn">Whether this is a warning message box. If True, its colors and background turn red.</param>
    Public Function MyMsgBox(Caption As String, Optional Title As String = "提示",
                             Optional Button1 As String = "确定", Optional Button2 As String = "", Optional Button3 As String = "",
                             Optional IsWarn As Boolean = False, Optional HighLight As Boolean = True, Optional ForceWait As Boolean = False,
                             Optional Button1Action As Action = Nothing, Optional Button2Action As Action = Nothing, Optional Button3Action As Action = Nothing) As Integer
        'Queue the message box
        Dim Converter As New MyMsgBoxConverter With {.Type = MyMsgBoxType.Text, .Button1 = Button1, .Button2 = Button2, .Button3 = Button3, .Text = Caption, .IsWarn = IsWarn, .Title = Title, .HighLight = HighLight, .ForceWait = True, .Button1Action = Button1Action, .Button2Action = Button2Action, .Button3Action = Button3Action}
        If Button2.Length > 0 OrElse ForceWait Then
            'Wait if there are multiple buttons
            If FrmMain Is Nothing OrElse FrmMain.PanMsg Is Nothing AndAlso RunInUi() Then
                'The main form is not loaded yet, use a plain old message box instead
                If Button2.Length > 0 Then
                    Dim RawResult As MsgBoxResult =
                        MsgBox(Caption, If(Button3.Length > 0, MsgBoxStyle.YesNoCancel, MsgBoxStyle.YesNo) + If(IsWarn, MsgBoxStyle.Critical, MsgBoxStyle.Question), Title)
                    Select Case RawResult
                        Case MsgBoxResult.Yes
                            Converter.Result = 1
                        Case MsgBoxResult.No
                            Converter.Result = 2
                        Case MsgBoxResult.Cancel
                            Converter.Result = 3
                    End Select
                Else
                    MsgBox(Caption, MsgBoxStyle.OkOnly + If(IsWarn, MsgBoxStyle.Critical, MsgBoxStyle.Question), Title)
                    Converter.Result = 1
                End If
                Logger.Warn($"主窗体加载完成前出现意料外的等待弹窗：{Button1},{Button2},{Button3}")
            Else
                WaitingMyMsgBox.Enqueue(Converter)
                Try
                    FrmMain.DragStop()
                    If RunInUi() Then MyMsgBoxTick()
                    ComponentDispatcher.PushModal()
                    Dispatcher.PushFrame(Converter.WaitFrame)
                Finally
                    ComponentDispatcher.PopModal()
                End Try
            End If
            Logger.Info($"普通弹框返回：{If(Converter.Result, "null")}")
            Return Converter.Result
        Else
            'Return directly without waiting
            WaitingMyMsgBox.Enqueue(Converter)
            Return 1
        End If
    End Function
    ''' <summary>
    ''' Shows an input box and returns the entered text. Returns Nothing if the second button is clicked.
    ''' </summary>
    ''' <param name="Title">The title of the message box.</param>
    ''' <param name="ValidateRules">Input validation for the text box.</param>
    ''' <param name="Text">The description text of the message box.</param>
    ''' <param name="DefaultInput">The default content of the text box.</param>
    ''' <param name="HintText">The hint text of the text box.</param>
    ''' <param name="Button1">The first button to show. Defaults to "确定".</param>
    ''' <param name="Button2">The second button to show. Defaults to "取消".</param>
    ''' <param name="IsWarn">Whether this is a warning message box. If True, its colors and background turn red.</param>
    Public Function MyMsgBoxInput(Title As String, Optional Text As String = Nothing, Optional DefaultInput As String = Nothing, Optional ValidateRules As Collection(Of Validate) = Nothing, Optional HintText As String = "", Optional Button1 As String = "确定", Optional Button2 As String = "取消", Optional IsWarn As Boolean = False) As String
        'Queue the message box
        Dim Converter As New MyMsgBoxConverter With {.Text = If(Text, ""), .HintText = If(HintText, ""), .Type = MyMsgBoxType.Input, .ValidateRules = If(ValidateRules, New Collection(Of Validate)), .Button1 = Button1, .Button2 = Button2, .Content = DefaultInput, .IsWarn = IsWarn, .Title = Title}
        WaitingMyMsgBox.Enqueue(Converter)
        'No idea what this is, but it works :)
        Try
            If FrmMain IsNot Nothing Then FrmMain.DragStop()
            If RunInUi() Then MyMsgBoxTick()
            ComponentDispatcher.PushModal()
            Dispatcher.PushFrame(Converter.WaitFrame)
        Finally
            ComponentDispatcher.PopModal()
        End Try
        Logger.Info($"输入弹框返回：{If(Converter.Result, "null")}")
        Return Converter.Result
    End Function
    ''' <summary>
    ''' Shows a selection box and returns the index of the selected item (starting from 0). Returns Nothing if the second button is clicked.
    ''' </summary>
    ''' <param name="Title">The title of the message box.</param>
    ''' <param name="Button1">The first button to show. Defaults to "确定".</param>
    ''' <param name="Button2">The second button to show. Empty by default.</param>
    ''' <param name="IsWarn">Whether this is a warning message box. If True, its colors and background turn red.</param>
    Public Function MyMsgBoxSelect(Selections As IEnumerable(Of IMyRadio), Optional Title As String = "提示", Optional Button1 As String = "确定", Optional Button2 As String = "", Optional IsWarn As Boolean = False) As Integer?
        'Queue the message box
        Dim Converter As New MyMsgBoxConverter With {.Type = MyMsgBoxType.Select, .Button1 = Button1, .Button2 = Button2, .Content = Selections, .IsWarn = IsWarn, .Title = Title}
        WaitingMyMsgBox.Enqueue(Converter)
        'No idea what this is, but it works :)
        Try
            If FrmMain IsNot Nothing Then FrmMain.DragStop()
            If RunInUi() Then MyMsgBoxTick()
            ComponentDispatcher.PushModal()
            Dispatcher.PushFrame(Converter.WaitFrame)
        Finally
            ComponentDispatcher.PopModal()
        End Try
        Logger.Info($"选择弹框返回：{If(Converter.Result, "null")}")
        Return Converter.Result
    End Function

    ''' <summary>
    ''' Message boxes waiting to be shown.
    ''' </summary>
    Public WaitingMyMsgBox As New ConcurrentQueue(Of MyMsgBoxConverter)
    Public Sub MyMsgBoxTick()
        Try
            If FrmMain?.PanMsg Is Nothing OrElse FrmMain.WindowState = WindowState.Minimized Then Return
            Dim TargetMsgBox As MyMsgBoxConverter = Nothing
            If FrmMain.PanMsg.Children.Count > 0 Then
                'A message box is showing
                FrmMain.PanMsg.Visibility = Visibility.Visible
            ElseIf WaitingMyMsgBox.TryDequeue(TargetMsgBox) Then
                'No message box is showing; show a waiting one
                FrmMain.PanMsg.Visibility = Visibility.Visible
                Select Case TargetMsgBox.Type
                    Case MyMsgBoxType.Input
                        FrmMain.PanMsg.Children.Add(New MyMsgInput(TargetMsgBox))
                    Case MyMsgBoxType.Select
                        FrmMain.PanMsg.Children.Add(New MyMsgSelect(TargetMsgBox))
                    Case MyMsgBoxType.Text
                        FrmMain.PanMsg.Children.Add(New MyMsgText(TargetMsgBox))
                    Case MyMsgBoxType.Login
                        FrmMain.PanMsg.Children.Add(New MyMsgLogin(TargetMsgBox))
                End Select
            Else
                'No message box is showing and none is waiting
                If FrmMain.PanMsg.Visibility <> Visibility.Collapsed Then FrmMain.PanMsg.Visibility = Visibility.Collapsed
            End If
        Catch ex As Exception
            Logger.Error(ex, "处理等待中的弹窗失败")
        End Try
    End Sub

#End Region

#Region "Page Declarations"
    'Pages are declared last to avoid colors not being loaded yet

    'Form declarations
    Public FrmMain As FormMain
    Public FrmStart As SplashScreen

    'Page declarations (the initialization page has been moved into FormMain for unit testing)
    Public FrmLaunchLeft As PageLaunchLeft
    Public FrmLaunchRight As PageLaunchRight
    Public FrmSelectLeft As PageSelectLeft
    Public FrmSelectRight As PageSelectRight
    Public FrmSpeedLeft As PageSpeedLeft
    Public FrmSpeedRight As PageSpeedRight

    'Download pages
    Public FrmDownloadLeft As PageDownloadLeft
    Public FrmDownloadInstall As PageDownloadInstall
    Public FrmDownloadMod As PageDownloadMod
    Public FrmDownloadPack As PageDownloadPack
    Public FrmDownloadDataPack As PageDownloadDataPack
    Public FrmDownloadShader As PageDownloadShader
    Public FrmDownloadResourcePack As PageDownloadResourcePack

    'Settings pages
    Public FrmSetupLeft As PageSetupLeft
    Public FrmSetupLaunch As PageSetupLaunch
    Public FrmSetupSystem As PageSetupSystem

    'Login pages
    Public FrmLoginLegacy As PageLoginLegacy
    Public FrmLoginNide As PageLoginNide
    Public FrmLoginNideSkin As PageLoginNideSkin
    Public FrmLoginAuth As PageLoginAuth
    Public FrmLoginAuthSkin As PageLoginAuthSkin
    Public FrmLoginMs As PageLoginMs
    Public FrmLoginMsSkin As PageLoginMsSkin

    'Instance settings pages
    Public FrmInstanceLeft As PageInstanceLeft
    Public FrmInstanceOverall As PageInstanceOverall
    Public FrmInstanceMod As PageInstanceMod
    Public FrmInstanceModDisabled As PageInstanceModDisabled
    Public FrmInstanceSetup As PageInstanceSetup
    Public FrmInstanceExport As PageInstanceExport
    Public FrmDownloadModify As PageDownloadModify

    'Resource info subpages
    Public FrmDownloadResourceDetail As PageDownloadResourceDetail

#End Region

#Region "System"

    ''' <summary>
    ''' Brings a PCL window to the front.
    ''' </summary>
    Public Sub ShowWindowToTop(Handle As IntPtr)
        Try
            PostMessage(Handle, 400 * 16 + 2, 0, 0)
            SetForegroundWindow(Handle) 'It has to be here, mysterious WinAPI, better not touch it
        Catch ex As Exception
            Logger.Error(ex, "设置窗口置顶失败", LogBehavior.Toast)
        End Try
    End Sub
    Public Declare Function FindWindow Lib "user32" Alias "FindWindowA" (ClassName As String, WindowName As String) As IntPtr
    Public Declare Function SetForegroundWindow Lib "user32" (hWnd As IntPtr) As Integer
    Private Declare Function PostMessage Lib "user32" Alias "PostMessageA" (hWnd As IntPtr, msg As UInteger, wParam As Long, lParam As Long) As Boolean

    ''' <summary>
    ''' Sets a program to launch with the high-performance GPU.
    ''' Throws an exception on failure.
    ''' </summary>
    Public Sub SetGPUPreference(Executeable As String)
        Const REG_KEY As String = "HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences"
        Const REG_VALUE As String = "GpuPreference=2;"
        'Check existing settings
        If RegistryUtils.Read(REG_KEY, Executeable).result?.ToString() = REG_VALUE Then
            Logger.Info($"无需调整显卡设置：{Executeable}")
            Return
        End If
        'Write new settings
        RegistryUtils.Write(REG_KEY, Executeable, REG_VALUE)
        Logger.Info($"已调整显卡设置：{Executeable}")
    End Sub

    ''' <summary>
    ''' Processes replacement markers. The replacement content is escaped with EscapeHandler.
    ''' </summary>
    Public Function ArgumentReplace(Text As String, Optional EscapeHandler As Func(Of String, String) = Nothing, Optional ReplaceTime As Boolean = True) As String
        If Text Is Nothing OrElse Not Text.Contains("{") Then Return Text
        'Folders must end with \
        Dim Replacer As Func(Of String, String) =
        Function(s As String) As String
            If s Is Nothing Then Return ""
            If EscapeHandler Is Nothing Then Return s
            If s.Contains(":\") Then
                Dim IsFolder = s.EndsWithF("\") OrElse s.EndsWithF("/")
                s = PathUtils.ToShortPath(s)
                If IsFolder Then s = PathUtils.AddSlashSuffix(s)
            End If
            Return EscapeHandler(s)
        End Function
        Text = Text.Replace("{pcl_version}", Replacer(VersionBaseName))
        Text = Text.Replace("{pcl_version_code}", Replacer(VersionCode))
        Text = Text.Replace("{pcl_version_branch}", Replacer(BuildTypeDisplay))
        Text = Text.Replace("{pcl_build_type}", Replacer(BuildType.ToString))
        Text = Text.Replace("{pcl_branch}", Replacer(VersionBranchMain))
        Text = Text.Replace("{identify}", Replacer(Identify))
        Text = Text.Replace("{path}", Replacer(Paths.Base))
        Text = Text.Replace("{path_with_name}", Replacer(PathExe))
        Text = Text.Replace("{path_temp}", Replacer(PathTemp))
        Text = Text.Replace("{pcl_md5}", Function() Replacer(CryptographyUtils.ComputeFileHash(PathExe, CryptographyUtils.HashMethod.Md5)))
        Text = Text.Replace("{pcl_sha1}", Function() Replacer(CryptographyUtils.ComputeFileHash(PathExe, CryptographyUtils.HashMethod.Sha1)))
        'Time
        If ReplaceTime Then 'In the window title, the time is replaced dynamically later, so it should not be replaced here
            Text = Text.Replace("{date}", Replacer(Date.Now.ToString("yyyy'/'M'/'d")))
            Text = Text.Replace("{time}", Replacer(Date.Now.ToString("HH':'mm':'ss")))
        End If
        'Minecraft
        Text = Text.Replace("{java}", Replacer(McLaunchJavaSelected?.Folder))
        Text = Text.Replace("{minecraft}", Replacer(McFolderSelected))
        If McInstanceSelected?.IsLoaded Then
            Text = Text.Replace("{version_path}", Replacer(McInstanceSelected.PathVersion)) : Text = Text.Replace("{verpath}", Replacer(McInstanceSelected.PathVersion))
            Text = Text.Replace("{version_indie}", Replacer(McInstanceSelected.PathIndie)) : Text = Text.Replace("{verindie}", Replacer(McInstanceSelected.PathIndie))
            Text = Text.Replace("{name}", Replacer(McInstanceSelected.Name))
            If {"unknown", "old", "pending"}.Contains(McInstanceSelected.Version.VanillaName.Lower) Then
                Text = Text.Replace("{version}", Replacer(McInstanceSelected.Name))
            Else
                Text = Text.Replace("{version}", Replacer(McInstanceSelected.Version.VanillaName))
            End If
            Text = Text.Replace("{forge}", Replacer(If(McInstanceSelected.Version.ModLoaders.HasFlagF(ModLoaders.Forge), McInstanceSelected.Version.ModLoaderVersions(ModLoaders.Forge), "")))
            Text = Text.Replace("{fabric}", Replacer(If(McInstanceSelected.Version.ModLoaders.HasFlagF(ModLoaders.Fabric), McInstanceSelected.Version.ModLoaderVersions(ModLoaders.Fabric), "")))
            Text = Text.Replace("{neoforge}", Replacer(If(McInstanceSelected.Version.ModLoaders.HasFlagF(ModLoaders.NeoForge), McInstanceSelected.Version.ModLoaderVersions(ModLoaders.NeoForge), "")))
            Text = Text.Replace("{liteloader}", Replacer(If(McInstanceSelected.Version.ModLoaders.HasFlagF(ModLoaders.LiteLoader), "true", "")))
        Else
            Text = Text.Replace("{version_path}", Replacer(Nothing)) : Text = Text.Replace("{verpath}", Replacer(Nothing))
            Text = Text.Replace("{version_indie}", Replacer(Nothing)) : Text = Text.Replace("{verindie}", Replacer(Nothing))
            Text = Text.Replace("{name}", Replacer(Nothing))
            Text = Text.Replace("{version}", Replacer(Nothing))
            Text = Text.Replace("{forge}", Replacer(Nothing))
            Text = Text.Replace("{fabric}", Replacer(Nothing))
            Text = Text.Replace("{neoforge}", Replacer(Nothing))
            Text = Text.Replace("{liteloader}", Replacer(Nothing))
        End If
        'Login info
        If McLoginLoader.State = LoadState.Finished Then
            Text = Text.Replace("{user}", Replacer(McLoginLoader.Output.Name))
            Text = Text.Replace("{uuid}", Replacer(McLoginLoader.Output.Uuid?.Lower))
            Select Case McLoginLoader.Input.Type
                Case McLoginType.Legacy
                    Text = Text.Replace("{login}", Replacer("离线"))
                Case McLoginType.Ms
                    Text = Text.Replace("{login}", Replacer("正版"))
                Case McLoginType.Nide
                    Text = Text.Replace("{login}", Replacer("统一通行证"))
                Case McLoginType.Auth
                    Text = Text.Replace("{login}", Replacer("Authlib-Injector"))
            End Select
        Else
            Text = Text.Replace("{user}", Replacer(Nothing))
            Text = Text.Replace("{uuid}", Replacer(Nothing))
            Text = Text.Replace("{login}", Replacer(Nothing))
        End If
        'Advanced
        Text = Text.RegexReplace("\{setup:([a-zA-Z0-9]+)\}", Function(m) Replacer(Settings.GetSafe(m.Groups(1).Value, McInstanceSelected)))
        Text = Text.RegexReplace("\{varible:([^:\}]+)(?::([^\}]+))?\}", Function(m) Replacer(RegistryUtils.TryRead($"HKEY_CURRENT_USER\Software\{RegFolder}", $"CustomEvent{m.Groups(1).Value}", m.Groups(2).Value)))
        Text = Text.RegexReplace("\{variable:([^:\}]+)(?::([^\}]+))?\}", Function(m) Replacer(RegistryUtils.TryRead($"HKEY_CURRENT_USER\Software\{RegFolder}", $"CustomEvent{m.Groups(1).Value}", m.Groups(2).Value)))
        Return Text
    End Function

#End Region

#Region "Task Cache"

    Private ReadOnly TaskTempLock As New Object
    Private IsTaskTempCleared As Boolean = False

    ''' <summary>
    ''' Tries to clean up the task cache folder.
    ''' Only actually cleans once per run; other threads wait for the cleanup to finish.
    ''' </summary>
    Public Sub TryClearTaskTemp()
        SyncLock TaskTempLock
            If IsTaskTempCleared Then Return
            IsTaskTempCleared = True
            Try
                Logger.Info("开始清理任务缓存文件夹")
                DirectoryUtils.Delete($"{OsDrive}ProgramData\PCL2Local\TaskTemp\")
                DirectoryUtils.Delete($"{PathTemp}TaskTemp\")
                Logger.Info("已清理任务缓存文件夹")
            Catch ex As Exception
                Logger.Warn(ex, "清理任务缓存文件夹失败")
            End Try
        End SyncLock
    End Sub

    ''' <summary>
    ''' Requests a temporary folder for task caching, ending with \. These folders do not need to be cleaned up afterwards.
    ''' Throws an exception if none of the cache locations are accessible.
    ''' </summary>
    ''' <param name="RequireNonSpace">Whether the path must not contain spaces.</param>
    Public Function RequestTaskTempFolder(Optional RequireNonSpace As Boolean = False) As String
        TryClearTaskTemp()
        Dim ResultFolder As String
        Try
            ResultFolder = $"{PathTemp}TaskTemp\{GetUuid()}-{RandomInteger(0, 1000000)}\"
            If RequireNonSpace AndAlso ResultFolder.Contains(" ") Then Exit Try 'Contains spaces
            DirectoryUtils.Create(ResultFolder)
            CheckPermissionWithException(ResultFolder)
            Return ResultFolder
        Catch
        End Try
        'Use a fallback path
        ResultFolder = $"{OsDrive}ProgramData\PCL2Local\TaskTemp\{GetUuid()}-{RandomInteger(0, 1000000)}\"
        DirectoryUtils.Create(ResultFolder)
        CheckPermission(ResultFolder)
        Return ResultFolder
    End Function

#End Region

#Region "Local Diagnostics"

    ''' <summary>
    ''' Writes system diagnostic info to the log.
    ''' </summary>
    Public Sub WriteDiagnostics()
        On Error Resume Next
        Logger.Warn($"诊断信息：{vbCrLf}" &
            "操作系统：" & My.Computer.Info.OSFullName & vbCrLf &
            "剩余内存：" & Int(My.Computer.Info.AvailablePhysicalMemory / 1024 / 1024) & " M / " & Int(My.Computer.Info.TotalPhysicalMemory / 1024 / 1024) & " M" & vbCrLf &
            "DPI：" & DPI & "（" & Math.Round(DPI / 96, 2) * 100 & "%）" & vbCrLf &
            "MC 文件夹：" & If(McFolderSelected, "Nothing") & vbCrLf &
            "文件位置：" & Paths.Base)
    End Sub


#End Region

    Public DragControl = Nothing
    Private Timer150Count As Integer = 0
    Private Sub TimerMain()
        Try
#Region "Code Run Every 50ms"
            HintTick()
            MyMsgBoxTick()
            FrmMain.DragTick()
            LoaderTaskbarProgressRefresh()
#End Region
        Catch ex As Exception
            Logger.Error(ex, "短程主时钟执行异常", LogBehavior.AlertThenCrash)
        End Try
        Timer150Count += 1
        If Timer150Count = 150 Then
            Timer150Count = 0
            Try
#Region "Code Run Every 7.5s"
                'Fix for the window being thrown aside for unknown reasons (Top, Left = -25600), and also #745
                RunInUi(
                Sub()
                    If Not FrmMain.Hidden Then
                        If FrmMain.Top < -9000 Then FrmMain.Top = 100
                        If FrmMain.Left < -9000 Then FrmMain.Left = 100 'When maximized, Left = -18.8
                    End If
                End Sub)
#End Region
            Catch ex As Exception
                Logger.Error(ex, "长程主时钟执行异常", LogBehavior.AlertThenCrash)
            End Try
        End If
    End Sub
    Private MainTimer As Threading.DispatcherTimer
    Public Sub TimerMainStart()
        If Not RunInUi() Then
            RunInUiWait(AddressOf TimerMainStart)
            Return
        End If
        If MainTimer IsNot Nothing Then Return
        MainTimer = New Threading.DispatcherTimer(Threading.DispatcherPriority.Background) With {.Interval = TimeSpan.FromMilliseconds(50)}
        AddHandler MainTimer.Tick, Sub()
                                       If IsProgramEnding Then
                                           MainTimer.Stop()
                                       Else
                                           TimerMain()
                                       End If
                                   End Sub
        MainTimer.Start()
    End Sub

End Module
