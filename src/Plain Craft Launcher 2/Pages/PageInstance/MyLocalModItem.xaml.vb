Imports System.Windows.Forms

Public Class MyLocalModItem

#Region "Basic properties"
    Public Uuid As Integer = GetUuid()

    'Title
    Private _Title As String
    Public Property Title As String
        Get
            Return _Title
        End Get
        Set(value As String)
            Dim RawValue = value
            LabTitle.TextDecorations = If(Entry.IsEnabled, Nothing, TextDecorations.Strikethrough)
            If LabTitle.Text = value Then Return
            LabTitle.Text = value
            _Title = RawValue
        End Set
    End Property

    'Subtitle
    Public Property SubTitle As String
        Get
            Return If(LabSubtitle?.Text, "")
        End Get
        Set(value As String)
            If LabSubtitle.Text = value Then Return
            LabSubtitle.Text = value
            LabSubtitle.Visibility = If(value = "", Visibility.Collapsed, Visibility.Visible)
        End Set
    End Property

    'Description
    Public Property Description As String
        Get
            Return LabInfo.Text
        End Get
        Set(value As String)
            If LabInfo.Text = value Then Return
            LabInfo.Text = value
        End Set
    End Property

    'Tag
    Public WriteOnly Property Tags As List(Of String)
        Set(Tags As List(Of String))
            PanTags.Children.Clear()
            PanTags.Visibility = If(Tags.Any(), Visibility.Visible, Visibility.Collapsed)
            For Each TagText In Tags
                Dim BorderTag As New Border With {
                    .Background = New MyColor("#0A000000"), .Padding = New Thickness(3, 1, 3, 1), .CornerRadius = New CornerRadius(3),
                    .Margin = New Thickness(0, 0, 3, 0), .SnapsToDevicePixels = True, .UseLayoutRounding = False}
                Dim LabTag As New TextBlock With {
                    .Text = TagText, .Foreground = New MyColor("#B0B0B0"), .FontSize = 11}
                BorderTag.Child = LabTag
                PanTags.Children.Add(BorderTag)
            Next
        End Set
    End Property

    'Associated mod
    Public Property Entry As LocalResourceFile
        Get
            Return Tag
        End Get
        Set(value As LocalResourceFile)
            Tag = value
        End Set
    End Property

#End Region

#Region "Click and check"

    'Raise the click event
    Public Event Click(sender As Object, e As MouseButtonEventArgs)
    Private Sub Button_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles Me.PreviewMouseLeftButtonUp
        If IsMouseDown Then
            RaiseEvent Click(sender, e)
            If e.Handled Then Return
        End If
    End Sub

    'Mouse click detection
    Private IsMouseDown As Boolean = False
    Private Sub Button_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles Me.PreviewMouseLeftButtonDown
        If Not IsMouseDirectlyOver Then Return
        IsMouseDown = True
        If ButtonStack IsNot Nothing Then ButtonStack.IsHitTestVisible = False
    End Sub
    Private Sub Button_MouseLeave(sender As Object, e As Object) Handles Me.MouseLeave, Me.PreviewMouseLeftButtonUp
        IsMouseDown = False
        If ButtonStack IsNot Nothing Then ButtonStack.IsHitTestVisible = True
    End Sub

    'Swipe to select
    Private Shared SwipeStart As Integer, SwipeEnd As Integer
    Private Shared _Swiping As Boolean = False
    Private Shared Property Swiping As Boolean
        Get
            Return _Swiping
        End Get
        Set(value As Boolean)
            _Swiping = value
            FrmInstanceMod.CardSelect.IsHitTestVisible = Not value
        End Set
    End Property
    Private Shared SwipToState As Boolean 'Items swiped over should set Checked to this value
    Private Sub Button_MouseSwipeStart(sender As Object, e As Object) Handles Me.MouseLeftButtonDown
        If Parent Is Nothing Then Return 'The mod may already have been deleted (#3824)
        'Start the swipe
        Dim Index = CType(Parent, StackPanel).Children.IndexOf(Me)
        SwipeStart = Index
        SwipeEnd = Index
        Swiping = True
        SwipToState = Not Checked
    End Sub
    Private Sub Button_MouseSwipe(sender As Object, e As Object) Handles Me.MouseEnter, Me.MouseLeave, Me.MouseLeftButtonUp
        If Parent Is Nothing Then Return 'The mod may already have been deleted (#3824)
        'End the swipe
        If Mouse.LeftButton <> MouseButtonState.Pressed OrElse
           TypeOf Mouse.DirectlyOver IsNot MyLocalModItem Then '#5771
            Swiping = False
            Return
        End If
        'Compute the swipe range
        Dim Elements = CType(Parent, StackPanel).Children
        Dim Index As Integer = Elements.IndexOf(Me)
        SwipeStart = Math.Min(SwipeStart, Index).Clamp(0, Elements.Count - 1)
        SwipeEnd = Math.Max(SwipeEnd, Index).Clamp(0, Elements.Count - 1)
        'Check every item in the range
        If SwipeStart = SwipeEnd Then Return
        For i = SwipeStart To SwipeEnd
            Dim Item As MyLocalModItem = Elements(i)
            Item.InitLate(Item, e)
            Item.Checked = SwipToState
        Next
    End Sub

    'Checked state
    Public Event Check(sender As Object, e As RouteEventArgs)
    Public Event Changed(sender As Object, e As RouteEventArgs)
    Private _Checked As Boolean = False
    Public Property Checked As Boolean
        Get
            Return _Checked
        End Get
        Set(value As Boolean)
            Try
                'Apply the property change
                Dim RawValue = _Checked
                If value = _Checked Then Return
                _Checked = value
                Dim ChangedEventArgs As New RouteEventArgs(False)
                If IsInitialized Then
                    RaiseEvent Changed(Me, ChangedEventArgs)
                    If ChangedEventArgs.Handled Then
                        _Checked = RawValue
                        Return
                    End If
                End If
                If value Then
                    Dim CheckEventArgs As New RouteEventArgs(False)
                    RaiseEvent Check(Me, CheckEventArgs)
                    If CheckEventArgs.Handled Then Return
                End If
                'Update the animation
                If IsVisibleInForm() Then
                    Dim Anim As New List(Of AniData)
                    If Checked Then
                        'From hidden to shown
                        Dim Delta = 32 - RectCheck.ActualHeight
                        Anim.Add(AaHeight(RectCheck, Delta * 0.4, 200,, New AniEaseOutFluent(AniEasePower.Weak)))
                        Anim.Add(AaHeight(RectCheck, Delta * 0.6, 300,, New AniEaseOutBack(AniEasePower.Weak)))
                        Anim.Add(AaOpacity(RectCheck, 1 - RectCheck.Opacity, 30))
                        RectCheck.VerticalAlignment = VerticalAlignment.Center
                        RectCheck.Margin = New Thickness(-3, 0, 0, 0)
                        Anim.Add(AaColor(LabTitle, TextBlock.ForegroundProperty, If(Entry.IsEnabled, "ColorBrush2", "ColorBrush5"), 200))
                    Else
                        'From shown to hidden
                        Anim.Add(AaHeight(RectCheck, -RectCheck.ActualHeight, 120,, New AniEaseInFluent(AniEasePower.Weak)))
                        Anim.Add(AaOpacity(RectCheck, -RectCheck.Opacity, 70, 40))
                        RectCheck.VerticalAlignment = VerticalAlignment.Center
                        Anim.Add(AaColor(LabTitle, TextBlock.ForegroundProperty, If(LabTitle.TextDecorations Is Nothing, "ColorBrush1", "ColorBrushGray4"), 120))
                    End If
                    AniStart(Anim, "MyLocalModItem Checked " & Uuid)
                Else
                    'Set directly when not on screen
                    RectCheck.VerticalAlignment = VerticalAlignment.Center
                    RectCheck.Margin = New Thickness(-3, 0, 0, 0)
                    If Checked Then
                        RectCheck.Height = 32
                        RectCheck.Opacity = 1
                        LabTitle.SetResourceReference(TextBlock.ForegroundProperty, If(Entry.IsEnabled, "ColorBrush2", "ColorBrush5"))
                    Else
                        RectCheck.Height = 0
                        RectCheck.Opacity = 0
                        LabTitle.SetResourceReference(TextBlock.ForegroundProperty, If(Entry.IsEnabled, "ColorBrush1", "ColorBrushGray4"))
                    End If
                    AniStop("MyLocalModItem Checked " & Uuid)
                End If
            Catch ex As Exception
                Logger.Warn(ex, "设置 Checked 失败")
            End Try
        End Set
    End Property


#End Region

#Region "Late-loaded content"

    'Status icon at the bottom-right
    Private ImgState As Image

    'Background element
    Private _RectBack As Border = Nothing
    Public ReadOnly Property RectBack As Border
        Get
            If _RectBack Is Nothing Then
                Dim Rect As New Border With {
                    .Name = "RectBack",
                    .CornerRadius = New CornerRadius(3),
                    .RenderTransform = New ScaleTransform(0.8, 0.8),
                    .RenderTransformOrigin = New Point(0.5, 0.5),
                    .BorderThickness = New Thickness(GetWPFSize(1)),
                    .SnapsToDevicePixels = True,
                    .IsHitTestVisible = False,
                    .Opacity = 0
                }
                Rect.SetResourceReference(Border.BackgroundProperty, "ColorBrush7")
                Rect.SetResourceReference(Border.BorderBrushProperty, "ColorBrush6")
                SetColumnSpan(Rect, 999)
                SetRowSpan(Rect, 999)
                Children.Insert(0, Rect)
                _RectBack = Rect
            End If
            Return _RectBack
        End Get
    End Property

    'Buttons
    Public ButtonHandler As Action(Of MyLocalModItem, EventArgs)
    Public ButtonStack As FrameworkElement
    Private _Buttons As IEnumerable(Of MyIconButton)
    Public Property Buttons As IEnumerable(Of MyIconButton)
        Get
            Return _Buttons
        End Get
        Set(value As IEnumerable(Of MyIconButton))
            _Buttons = value
            'Remove the old stack
            If ButtonStack IsNot Nothing Then
                Children.Remove(ButtonStack)
                ButtonStack = Nothing
            End If
            If Not value.Any() Then Return
            'Add a new stack
            ButtonStack = New StackPanel With {.Opacity = 0, .Margin = New Thickness(0, 0, 5, 0), .SnapsToDevicePixels = False, .Orientation = Orientation.Horizontal,
                .HorizontalAlignment = HorizontalAlignment.Right, .VerticalAlignment = VerticalAlignment.Center, .UseLayoutRounding = False}
            SetColumnSpan(ButtonStack, 10) : SetRowSpan(ButtonStack, 10)
            'Build the buttons
            For Each Btn As MyIconButton In value
                If Btn.Height.Equals(Double.NaN) Then Btn.Height = 25
                If Btn.Width.Equals(Double.NaN) Then Btn.Width = 25
                CType(ButtonStack, StackPanel).Children.Add(Btn)
            Next
            Children.Add(ButtonStack)
        End Set
    End Property

    'Check bar
    Private _RectCheck As Border
    Public ReadOnly Property RectCheck As Border
        Get
            If _RectCheck Is Nothing Then
                _RectCheck = New Border With {.Width = 5, .Height = If(Checked, Double.NaN, 0), .CornerRadius = New CornerRadius(2, 2, 2, 2),
                    .VerticalAlignment = If(Checked, VerticalAlignment.Stretch, VerticalAlignment.Center),
                    .HorizontalAlignment = HorizontalAlignment.Left, .UseLayoutRounding = False, .SnapsToDevicePixels = False,
                    .Margin = If(Checked, New Thickness(-3, 6, 0, 6), New Thickness(-3, 0, 0, 0))}
                _RectCheck.SetResourceReference(Border.BackgroundProperty, "ColorBrush3")
                SetRowSpan(_RectCheck, 10)
                Children.Add(_RectCheck)
            End If
            Return _RectCheck
        End Get
    End Property

#End Region

    Private Function GetUpdateCompareDescription() As String
        Dim CurrentName = If(Entry.ProjectVersion.Version, Entry.ProjectVersion.Display).Replace(".jar", "")
        Dim NewestName = If(Entry.UpdateFile.Version, Entry.UpdateFile.Display).Replace(".jar", "")
        'Shorten the names before comparing
        Dim CurrentSegs = CurrentName.Split("-_+ ".ToCharArray).ToList()
        Dim NewestSegs = NewestName.Split("-_+ ".ToCharArray).ToList()
        Dim Shortened As Boolean = False
        For Each Seg In CurrentSegs.ToList()
            If Not NewestSegs.Contains(Seg) Then Continue For
            CurrentSegs.Remove(Seg)
            NewestSegs.Remove(Seg)
            Shortened = True
        Next
        If Shortened AndAlso CurrentSegs.Any() AndAlso NewestSegs.Any() Then
            CurrentName = CurrentSegs.Join("-")
            NewestName = NewestSegs.Join("-")
            Entry._Version = CurrentName 'Use the online info as the displayed version
        End If
        Return $"当前版本：{CurrentName}（{StringUtils.FormatTimeSpan(Entry.ProjectVersion.ReleaseDate - Date.Now, False)}）{vbCrLf}最新版本：{NewestName}（{StringUtils.FormatTimeSpan(Entry.UpdateFile.ReleaseDate - Date.Now, False)}）"
    End Function

    'Lazy load
    Public Sub New()
        InitializeComponent()
        LazyLoadBehavior.OnFirstEnterScrollViewerViewport(Me, AddressOf FirstEnterScrollViewerViewport)
    End Sub
    Private HasEnteredViewport As Boolean = False
    Private Sub FirstEnterScrollViewerViewport()
        HasEnteredViewport = True
        Refresh()
    End Sub
    Public Sub Refresh()
        If Not HasEnteredViewport Then Return 'Skip loading when a resource update arrives before the item is shown
        'Update
        If Entry.HasUpdate Then
            BtnUpdate.Visibility = Visibility.Visible
            BtnUpdate.ToolTip = $"{GetUpdateCompareDescription()}{vbCrLf}点击以更新，右键查看更新日志。"
        Else
            BtnUpdate.Visibility = Visibility.Collapsed
        End If
        'Title and description
        Dim DescFileName As String = PathUtils.GetFileNameWithoutExtension(Entry.EnabledName)
        Dim NewDescription As String
        If Settings.Get(Of Integer)("ToolModLocalNameStyle") = 1 Then
            'Title shows the file name; details show the translated name
            'Title
            Title = DescFileName
            SubTitle = ""
            'Description
            If Entry.Project Is Nothing Then
                NewDescription = Entry.Display
            Else
                Dim Titles = Entry.Project.GetControlTitle(False)
                NewDescription = Titles.Title & Titles.SubTitle
            End If
            NewDescription = NewDescription.Replace("  |  ", " / ")
            If Entry.Version IsNot Nothing Then NewDescription &= $" ({Entry.Version})"
        Else
            'Title shows the translated name; details show the file name
            'Title
            If Entry.Project Is Nothing Then
                Title = Entry.Display
                SubTitle = If(Entry.Version Is Nothing, "", "  |  " & Entry.Version)
            Else
                Dim Titles = Entry.Project.GetControlTitle(False)
                Title = Titles.Title
                SubTitle = Titles.SubTitle & If(Entry.Version Is Nothing, "", "  |  " & Entry.Version)
            End If
            'Description
            NewDescription = DescFileName
        End If
        If Entry.Project IsNot Nothing Then
            NewDescription += ": " & Entry.Project.Description.ReplaceLineEndings("")
        ElseIf Entry.Description IsNot Nothing Then
            NewDescription += ": " & Entry.Description.ReplaceLineEndings("")
        End If
        Description = NewDescription
        If Checked Then
            LabTitle.SetResourceReference(TextBlock.ForegroundProperty, If(Entry.IsEnabled, "ColorBrush2", "ColorBrush5"))
        Else
            LabTitle.SetResourceReference(TextBlock.ForegroundProperty, If(Entry.IsEnabled, "ColorBrush1", "ColorBrushGray4"))
        End If
        'Main logo
        If Entry.Project IsNot Nothing Then
            Entry.Project.ApplyLogoToMyImage(PathLogo)
        Else
            PathLogo.Source = PathImage & "Icons/NoIcon.png"
        End If
        'Logo at the bottom-right of the icon
        If Entry.IsEnabled Then
            If ImgState IsNot Nothing Then
                Children.Remove(ImgState)
                ImgState = Nothing
            End If
        Else
            If ImgState Is Nothing Then
                ImgState = New Image With {
                    .Width = 20, .Height = 20, .Margin = New Thickness(0, 0, -5, -3), .IsHitTestVisible = False,
                    .HorizontalAlignment = HorizontalAlignment.Right, .VerticalAlignment = VerticalAlignment.Bottom
                }
                RenderOptions.SetBitmapScalingMode(ImgState, BitmapScalingMode.HighQuality)
                SetColumn(ImgState, 1) : SetRow(ImgState, 1) : SetRowSpan(ImgState, 2)
                Children.Add(ImgState)
            End If
            ImgState.Source = New MyBitmap(PathImage & $"Icons/Disabled.png")
        End If
        'Tags
        If Entry.Project IsNot Nothing Then Tags = Entry.Project.Tags
    End Sub

    Public Sub RefreshColor(sender As Object, e As EventArgs) Handles Me.MouseEnter, Me.MouseLeave, Me.MouseLeftButtonDown, Me.MouseLeftButtonUp, Me.Changed
        InitLate(sender, e)
        'Play the color animation
        Dim Time As Integer = If(IsMouseOver, 120, 180)
        Dim Ani As New List(Of AniData)
        'ButtonStack
        If ButtonStack IsNot Nothing Then
            If IsMouseOver Then
                Ani.Add(AaOpacity(ButtonStack, 1 - ButtonStack.Opacity, Time * 0.7, Time * 0.3))
                Ani.Add(AaDouble(Sub(i) ColumnPaddingRight.Width = New GridLength(Math.Max(0, ColumnPaddingRight.Width.Value + i)),
                    5 + Buttons.Count * 25 - ColumnPaddingRight.Width.Value, Time * 0.3, Time * 0.7))
            Else
                Ani.Add(AaOpacity(ButtonStack, -ButtonStack.Opacity, Time * 0.4))
                Ani.Add(AaDouble(Sub(i) ColumnPaddingRight.Width = New GridLength(Math.Max(0, ColumnPaddingRight.Width.Value + i)),
                    4 - ColumnPaddingRight.Width.Value, Time * 0.4))
            End If
        End If
        'RectBack
        If IsMouseOver OrElse Checked Then
            Ani.AddRange({
                AaColor(RectBack, Border.BackgroundProperty, If(IsMouseDown, "ColorBrush6", "ColorBrushBg1"), Time),
                AaOpacity(RectBack, 1 - RectBack.Opacity, Time,, New AniEaseOutFluent)
            })
            If IsMouseDown Then
                Ani.Add(AaScaleTransform(RectBack, 0.996 - CType(RectBack.RenderTransform, ScaleTransform).ScaleX, Time * 1.2,, New AniEaseOutFluent))
            Else
                Ani.Add(AaScaleTransform(RectBack, 1 - CType(RectBack.RenderTransform, ScaleTransform).ScaleX, Time * 1.2,, New AniEaseOutFluent))
            End If
        Else
            Ani.AddRange({
                AaOpacity(RectBack, -RectBack.Opacity, Time),
                AaScaleTransform(RectBack, 0.996 - CType(RectBack.RenderTransform, ScaleTransform).ScaleX, Time,, New AniEaseOutFluent),
                AaScaleTransform(RectBack, -0.196, 1,,, True)
            })
        End If
        AniStart(Ani, "LocalModItem Color " & Uuid)
    End Sub

    'Materialize virtualized content
    Private Sub InitLate(sender As Object, e As EventArgs)
        If ButtonHandler IsNot Nothing Then
            ButtonHandler(sender, e)
            ButtonHandler = Nothing
        End If
    End Sub

    'Show the changelog
    Private Sub BtnUpdate_PreviewMouseRightButtonUp(sender As Object, e As MouseButtonEventArgs) Handles BtnUpdate.PreviewMouseRightButtonUp
        e.Handled = True
        ShowUpdateLog()
    End Sub
    Private Sub ShowUpdateLog()
        Dim CurseForgeUrl As String = Entry.ChangelogUrls.FirstOrDefault(Function(x) x.Contains("curseforge.com"))
        Dim ModrinthUrl As String = Entry.ChangelogUrls.FirstOrDefault(Function(x) x.Contains("modrinth.com"))
        If CurseForgeUrl Is Nothing OrElse ModrinthUrl Is Nothing Then
            OpenWebsite(Entry.ChangelogUrls.First)
        Else
            Select Case MyMsgBox("要在哪个网站上查看更新日志？", "查看更新日志", "Modrinth", "CurseForge", "取消")
                Case 1
                    OpenWebsite(ModrinthUrl)
                Case 2
                    OpenWebsite(CurseForgeUrl)
            End Select
        End If
    End Sub

    'Start an update
    Private Sub BtnUpdate_Click(sender As Object, e As EventArgs) Handles BtnUpdate.Click
        Select Case MyMsgBox($"是否要更新 {Entry.Display}？{vbCrLf}{vbCrLf}{GetUpdateCompareDescription()}", "Mod 更新确认", "更新", "查看更新日志", "取消")
            Case 1 'Update ("更新")
                FrmInstanceMod.UpdateMods({Entry})
            Case 2 'View changelog ("查看更新日志")
                ShowUpdateLog()
            Case 3 'Cancel ("取消")
        End Select
    End Sub

    'Adaptive layout (#4465)
    Private Sub PanTitle_SizeChanged() Handles PanTitle.SizeChanged
        '0: fully expanded: Auto - Auto - (Auto) - 1*
        '1: compress Subtitle: Auto - 1* - (Auto) - 0
        '2: also compress Title: 1* - 0 - (Auto) - 0
        Dim CurrentCompressLevel As Integer =
            If(ColumnExtend.Width.IsStar, 0, If(ColumnTitle.Width.IsStar, 2, 1)) 'Subtitle may be Collapsed
        Dim NewCompressLevel As Integer
        Select Case CurrentCompressLevel
            Case 0
                If ColumnExtend.ActualWidth < 0.5 Then
                    NewCompressLevel = If(LabSubtitle.Visibility = Visibility.Collapsed, 2, 1)
                Else
                    Return
                End If
            Case 1
                If ColumnSubtitle.ActualWidth < 0.5 Then
                    NewCompressLevel = 2
                ElseIf Not LabSubtitle.IsTextTrimmed Then
                    NewCompressLevel = 0
                Else
                    Return
                End If
            Case 2
                If Not LabTitle.IsTextTrimmed Then
                    NewCompressLevel = If(LabSubtitle.Visibility = Visibility.Collapsed, 0, 1)
                Else
                    Return
                End If
        End Select
        Select Case NewCompressLevel
            Case 0
                'Fully expanded: Auto - Auto - (Auto) - 1*
                ColumnTitle.Width = GridLength.Auto
                ColumnSubtitle.Width = GridLength.Auto
                ColumnExtend.Width = New GridLength(1, GridUnitType.Star)
            Case 1
                'Compress Subtitle: Auto - 1* - (Auto) - 0
                ColumnTitle.Width = GridLength.Auto
                ColumnSubtitle.Width = New GridLength(1, GridUnitType.Star)
                ColumnExtend.Width = New GridLength(0, GridUnitType.Pixel)
            Case 2
                'Also compress Title: 1* - 0 - (Auto) - 0
                ColumnTitle.Width = New GridLength(1, GridUnitType.Star)
                ColumnSubtitle.Width = New GridLength(0, GridUnitType.Pixel)
                ColumnExtend.Width = New GridLength(0, GridUnitType.Pixel)
        End Select
    End Sub

End Class
