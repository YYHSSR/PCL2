Public Class PageSpeedLeft
    Private Const WatcherInterval As Integer = 300

    'Initialization
    Private IsLoad As Boolean = False
    Private Sub Page_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        'Refresh the display as soon as the page opens
        Watcher()

        'If the download finished during the previous-page exit animation, try to go back immediately
        TryReturnToHome()

        If IsLoad Then Return
        IsLoad = True

        'Watcher timer
        Dim timer As New Threading.DispatcherTimer With {.Interval = New TimeSpan(0, 0, 0, 0, WatcherInterval)}
        AddHandler timer.Tick, AddressOf Watcher
        timer.Start()

        'Hide the thread-count rows outside debug mode
        If Not ModeDebug Then
            RowDefinitions(12).Height = New GridLength(0)
            RowDefinitions(13).Height = New GridLength(0)
            RowDefinitions(14).Height = New GridLength(0)
            RowDefinitions(15).Height = New GridLength(0)
        End If

    End Sub

    'Timer work
    Private ReadOnly RightCards As New Dictionary(Of String, MyCard)
    Private Sub Watcher()
        If Not FrmMain.PageCurrent = FormMain.PageType.TaskManager Then Return
        Try

#Region "Update the left bar"
            If Not LoaderTaskbar.Any() Then
                'No tasks
                LabProgress.Text = "100 %"
                LabSpeed.Text = "0 B/s"
                LabFile.Text = "0"
                LabThread.Text = "0 / " & NetTaskThreadLimit
            Else
                'Tasks are running; show the summary
                Dim Tasks = LoaderTaskbar.Where(Function(l) l.Show).ToList() 'Exclude Minecraft launch tasks (#6270)
                Dim RawPercent As Double = If(Tasks.Any, Tasks.Select(Function(l) l.Progress).Average().Clamp(0, 1), 1)
                Dim PredictText As String = Math.Floor(RawPercent * 100) & "." & Math.Floor((RawPercent * 100 - Math.Floor(RawPercent * 100)) * 100).ToString.EnsureLength("0", 2) & " %"
                LabProgress.Text = If(RawPercent > 0.999999, "100 %", PredictText)
                LabSpeed.Text = StringUtils.FormatByteSize(NetManager.Speed) & "/s"
                LabFile.Text = If(NetManager.FileRemain < 0, "0*", NetManager.FileRemain)
                LabThread.Text = NetTaskThreadCount & " / " & NetTaskThreadLimit
            End If
#End Region

        Catch ex As Exception
            Logger.Error(ex, "任务管理左栏监视出错")
        End Try
        If FrmSpeedRight Is Nothing OrElse FrmSpeedRight.PanMain Is Nothing Then Return
        Try
            For Each Loader In LoaderTaskbar.ToList
                TaskRefresh(Loader)
            Next
        Catch ex As Exception
            Logger.Error(ex, "任务管理右栏监视出错")
        End Try
    End Sub
    Public Sub TaskRefresh(Loader As LoaderBase)
        If Loader Is Nothing OrElse Not Loader.Show Then Return
        Try
            'Get the loaders that should be shown
            Dim LoaderList As List(Of LoaderBase) = CType(Loader, Object).GetLoaderList()
            If RightCards.ContainsKey(Loader.Name) Then
                'This card already exists
                Dim Card As Grid = RightCards(Loader.Name)
                Dim NewValue As Double = Loader.Progress + Loader.State
                If Val(Card.Tag) = NewValue Then Return
                Card.Tag = NewValue
                If Card.Children.Count <= 3 Then
                    Logger.Warn($"元素不足的卡片：{Loader.Name}")
                    Return
                End If
                Card = Card.Children(3)
                Try
                    Select Case Loader.State
                        Case LoadState.Failed
#Region "Failed: update the card"
                            Card.RowDefinitions.Clear()
                            Card.Children.Clear()
                            Card.Children.Add(GetObjectFromXML("<Path xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Stretch=""Uniform"" Tag=""Failed"" Data=""F1 M2.5,0 L0,2.5 7.5,10 0,17.5 2.5,20 10,12.5 17.5,20 20,17.5 12.5,10 20,2.5 17.5,0 10,7.5 2.5,0Z"" Height=""15"" Width=""15"" HorizontalAlignment=""Center"" Grid.Column=""0"" Grid.Row=""0"" Fill=""{DynamicResource ColorBrush3}"" Margin=""0,1,0,0"" VerticalAlignment=""Top""/>"))
                            Dim Tb As TextBlock = GetObjectFromXML("<TextBlock xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" TextWrapping=""Wrap"" HorizontalAlignment=""Left"" ToolTip=""单击复制错误详情"" Grid.Column=""1"" Grid.Row=""0"" Margin=""0,0,0,5"" />")
                            Tb.Text = Loader.Error.GetDisplay(True)
                            AddHandler Tb.MouseLeftButtonDown,
                            Sub(sender As TextBlock, e As EventArgs)
                                ClipboardSet(sender.Text, False)
                                Hint("已复制错误详情！", HintType.Green)
                            End Sub
                            Card.Children.Add(Tb)
#End Region
                        Case LoadState.Finished, LoadState.Canceled
#Region "Finished or canceled: remove the card and go back"
                            AniDispose(CType(Card.Parent, MyCard), True, AddressOf TryReturnToHome)
#End Region
                        Case LoadState.Loading, LoadState.Waiting
#Region "Progress changed: update the card"
                            Try
                                If Card.Children.Count < LoaderList.Count * 2 Then
                                    Logger.Warn($"刷新任务管理卡片 {Loader.Name} 失败：卡片中仅有 {Card.Children.Count} 个子项，要求至少有 {LoaderList.Count * 2} 个子项")
                                    Exit Try
                                End If
                                Dim Row As Integer = 0
                                For Each SubTask In LoaderList
                                    Select Case SubTask.State
                                        Case LoadState.Waiting
                                            If CType(Card.Children(Row * 2), FrameworkElement).Tag <> "Waiting" Then
                                                Card.Children.RemoveAt(Row * 2)
                                                Card.Children.Insert(Row * 2, GetObjectFromXML("<Path xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" xmlns:local=""clr-namespace:PCL;assembly=Plain Craft Launcher 2"" Stretch=""Uniform"" Tag=""Waiting"" Data=""F1 M5,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 Z"" Width=""18"" HorizontalAlignment=""Center"" Grid.Column=""0"" Grid.Row=""" & Row & """ Fill=""{DynamicResource ColorBrush3}"" Margin=""0,7,0,0"" VerticalAlignment=""Top"" Height=""6""/>"))
                                            End If
                                        Case LoadState.Loading
                                            If CType(Card.Children(Row * 2), FrameworkElement).Tag <> "Loading" Then
                                                Card.Children.RemoveAt(Row * 2)
                                                Card.Children.Insert(Row * 2, GetObjectFromXML("<TextBlock xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" xmlns:local=""clr-namespace:PCL;assembly=Plain Craft Launcher 2"" Text=""" & Math.Floor(SubTask.Progress * 100) & "%"" Tag=""Loading"" HorizontalAlignment=""Center"" Grid.Column=""0"" Grid.Row=""" & Row & """ Foreground=""{DynamicResource ColorBrush3}""/>"))
                                            Else
                                                CType(Card.Children(Row * 2), TextBlock).Text = Math.Floor(SubTask.Progress * 100) & "%"
                                            End If
                                        Case LoadState.Finished
                                            If CType(Card.Children(Row * 2), FrameworkElement).Tag <> "Finished" Then
                                                Card.Children.RemoveAt(Row * 2)
                                                Card.Children.Insert(Row * 2, GetObjectFromXML("<Path xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" xmlns:local=""clr-namespace:PCL;assembly=Plain Craft Launcher 2"" Stretch=""Uniform"" Tag=""Finished"" Data=""F1 M 23.7501,33.25L 34.8334,44.3333L 52.2499,22.1668L 56.9999,26.9168L 34.8334,53.8333L 19.0001,38L 23.7501,33.25 Z"" Height=""16"" Width=""15"" HorizontalAlignment=""Center"" Grid.Column=""0"" Grid.Row=""" & Row & """ Fill=""{DynamicResource ColorBrush3}"" Margin=""0,3,0,0"" VerticalAlignment=""Top""/>"))
                                            End If
                                    End Select
                                    Row += 1
                                Next
                            Catch ex As Exception
                                Logger.Error(ex, $"刷新任务管理卡片 {Loader.Name} 失败")
                            End Try
#End Region
                    End Select
                Catch ex As Exception
                    Logger.Error(ex, $"更新任务管理显示失败（{Loader.State}）")
                End Try
            ElseIf Not (Loader.State = LoadState.Canceled OrElse Loader.State = LoadState.Finished) Then
                Try
#Region "No card yet, and still running: add one"
                    Dim CardXAML As String = "
                        <local:MyCard xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" xmlns:local=""clr-namespace:PCL;assembly=Plain Craft Launcher 2""
                            Tag=""" & (Loader.Progress + Loader.State) & """ Title=""" & StringUtils.XmlEscape(Loader.Name) & """ Margin=""0,0,0,15"">
                            <Grid Margin=""14,40,15,10"">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width=""50""/>
                                    <ColumnDefinition/>
                                </Grid.ColumnDefinitions>
                                <Grid.RowDefinitions>"
                    For Each SubTask In LoaderList
                        CardXAML += "<RowDefinition Height=""26""/>"
                    Next
                    CardXAML += "</Grid.RowDefinitions>"
                    Dim Row As Integer = 0
                    For Each SubTask In LoaderList
                        Select Case SubTask.State
                            Case LoadState.Waiting
                                CardXAML += "<Path Stretch=""Uniform"" Tag=""Waiting"" Data=""F1 M5,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 m15,0 a5,5 360 1 0 0,0.0001 Z"" Width=""18"" HorizontalAlignment=""Center"" Grid.Column=""0"" Grid.Row=""" & Row & """ Fill=""{DynamicResource ColorBrush3}"" Margin=""0,7,0,0"" VerticalAlignment=""Top"" Height=""6""/>"
                            Case LoadState.Loading
                                CardXAML += "<TextBlock Text=""" & Math.Floor(SubTask.Progress * 100) & "%"" Tag=""Loading"" HorizontalAlignment=""Center"" Grid.Column=""0"" Grid.Row=""" & Row & """ Foreground=""{DynamicResource ColorBrush3}"" />"
                            Case LoadState.Finished
                                CardXAML += "<Path Stretch=""Uniform"" Tag=""Finished"" Data=""F1 M 23.7501,33.25L 34.8334,44.3333L 52.2499,22.1668L 56.9999,26.9168L 34.8334,53.8333L 19.0001,38L 23.7501,33.25 Z"" Height=""16"" Width=""15"" HorizontalAlignment=""Center"" Grid.Column=""0"" Grid.Row=""" & Row & """ Fill=""{DynamicResource ColorBrush3}"" Margin=""0,3,0,0"" VerticalAlignment=""Top""/>"
                            Case Else
                                CardXAML += "<Path Stretch=""Uniform"" Tag=""Failed"" Data=""F1 M2.5,0 L0,2.5 7.5,10 0,17.5 2.5,20 10,12.5 17.5,20 20,17.5 12.5,10 20,2.5 17.5,0 10,7.5 2.5,0Z"" Height=""15"" Width=""15"" HorizontalAlignment=""Center"" Grid.Column=""0"" Grid.Row=""" & Row & """ Fill=""{DynamicResource ColorBrush3}"" Margin=""0,1,0,0"" VerticalAlignment=""Top""/>"
                        End Select
                        CardXAML += "<TextBlock Text=""" & StringUtils.XmlEscape(SubTask.Name) & """ HorizontalAlignment=""Left"" Grid.Column=""1"" Grid.Row=""" & Row & """/>"
                        Row += 1
                    Next
                    CardXAML += "</Grid></local:MyCard>"
                    'Create the control
                    Dim Card As MyCard
                    Try
                        Card = GetObjectFromXML(CardXAML)
                    Catch ex As Exception
                        Logger.Warn(ex, "新建任务管理卡片失败")
                        Logger.Info($"出错的卡片内容：{vbCrLf}{CardXAML}")
                        Throw
                    End Try
                    FrmSpeedRight.PanMain.Children.Insert(0, Card)
                    RightCards.Add(Loader.Name, Card)
                    Logger.Info($"新建任务管理卡片：{Loader.Name}")
                    'Add the cancel button
                    Dim Cancel As New MyIconButton With {.Name = "BtnCancel", .Logo = "F1 M2,0 L0,2 8,10 0,18 2,20 10,12 18,20 20,18 12,10 20,2 18,0 10,8 2,0Z", .Height = 20, .Margin = New Thickness(0, 10, 10, 0), .LogoScale = 1.1, .HorizontalAlignment = HorizontalAlignment.Right, .VerticalAlignment = VerticalAlignment.Top}
                    Card.Children.Add(Cancel)
                    AddHandler Cancel.Click,
                    Sub(sender As MyIconButton, e As EventArgs)
                        AniDispose(sender, False)
                        AniDispose(Card, True, Sub() If FrmSpeedRight.PanMain.Children.Count = 0 AndAlso FrmMain.PageCurrent = FormMain.PageType.TaskManager Then FrmMain.PageBack())
                        RightCards.Remove(Loader.Name)
                        LoaderTaskbar.Remove(Loader)
                        Logger.Info($"关闭任务管理卡片：{Loader.Name}，且移出任务列表")
                        RunInThread(Sub() Loader.Cancel())
                    End Sub
                    'If it has already failed, refresh once more so the card shows the failure
                    If Loader.State = LoadState.Failed Then
                        Card.Tag = Nothing 'Clear the tag so the refresh is not treated as a duplicate
                        TaskRefresh(Loader)
                    End If
#End Region
                Catch ex As Exception
                    Logger.Error(ex, "添加任务管理卡片失败")
                End Try
            End If
        Catch ex As Exception
            Logger.Error(ex, "刷新任务管理显示失败")
        End Try
    End Sub
    Public Sub TaskRemove(Loader As Object)
        If RightCards.ContainsKey(Loader.Name) Then
            RunInUiWait(
            Sub()
                'Remove the existing card
                Dim Card As Grid = RightCards(Loader.Name)
                FrmSpeedRight.PanMain.Children.Remove(Card)
                RightCards.Remove(Loader.Name)
                Logger.Info($"移除任务管理卡片：{Loader.Name}")
            End Sub)
        End If
    End Sub

    ''' <summary>
    ''' If there are no tasks, try to return to the home page.
    ''' </summary>
    Private Sub TryReturnToHome()
        If FrmSpeedRight.PanMain.Children.Count = 0 AndAlso FrmMain.PageCurrent = FormMain.PageType.TaskManager Then
            FrmMain.PageBack()
        End If
    End Sub

End Class
