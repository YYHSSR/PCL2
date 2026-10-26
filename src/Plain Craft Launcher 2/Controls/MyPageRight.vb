Public Class MyPageRight
    Inherits AdornerDecorator
    Public PageUuid As Integer = GetUuid()

    'Scroll area watched by the "back to top" button
    Public Property PanScroll As String 'The control can only be found after Loaded, so it cannot be bound directly with Binding (#6664)
        Get
            Return GetValue(PanScrollProperty)
        End Get
        Set(value As String)
            SetValue(PanScrollProperty, value)
        End Set
    End Property
    Private Shared ReadOnly PanScrollProperty =
        DependencyProperty.Register("PanScroll", GetType(String), GetType(MyPageRight), New PropertyMetadata(Nothing))

    'Current state
    Public Enum PageStates
        Empty 'Default state, page is completely empty
        LoaderWait 'Initial wait before showing the loading indicator
        LoaderEnter 'Loading indicator enter animation
        LoaderStayForce 'Loading indicator shown (forced wait)
        LoaderStay 'Loading indicator shown
        LoaderExit 'Loading indicator exit animation
        ContentEnter 'Content enter animation
        ContentStay 'Content shown
        ContentExit 'Full exit animation caused by a refresh, or by the page content leaving (sub-page change)
        PageExit 'Full exit animation caused by switching pages
    End Enum
    Private _PageState As PageStates = PageStates.Empty
    Public Property PageState As PageStates
        Get
            Return _PageState
        End Get
        Set(value As PageStates)
            If _PageState = value Then Return
            _PageState = value
        End Set
    End Property

#Region "Loader"

    Private PageLoader As LoaderBase
    Private PageLoaderInputInvoke
    Private PageLoaderUi As MyLoading
    Private PanLoader As FrameworkElement
    Private PanContent As FrameworkElement
    Private PanAlways As FrameworkElement
    Private PageLoaderAutoRun As Boolean

    'Initialization
    ''' <summary>
    ''' Indicates that the page has a loader that needs to run in the background.
    ''' </summary>
    ''' <param name="LoaderUi">The MyLoading control.</param>
    ''' <param name="PanLoader">The card that contains the MyLoading control.</param>
    ''' <param name="PanContent">The content container shown after loading finishes.</param>
    ''' <param name="PanAlways">A container that is always shown, whether loading or not. Can be Nothing.</param>
    ''' <param name="RealLoader">The loader that runs on a worker thread.</param>
    ''' <param name="FinishedInvoke">UI initialization callback invoked on the UI thread when the loader finishes.</param>
    Public Sub PageLoaderInit(Of TIn, TOut)(LoaderUi As MyLoading, PanLoader As FrameworkElement, PanContent As FrameworkElement, PanAlways As FrameworkElement,
                              RealLoader As LoaderTask(Of TIn, TOut),
                              Optional FinishedInvoke As Action(Of LoaderTask(Of TIn, TOut)) = Nothing, Optional InputInvoke As Func(Of TIn) = Nothing,
                              Optional AutoRun As Boolean = True)
        'Initialize parameters
        Me.PanLoader = PanLoader
        Me.PanContent = PanContent
        Me.PanAlways = PanAlways
        Me.PageLoader = RealLoader
        Me.PageLoaderUi = LoaderUi
        Me.PageLoaderInputInvoke = InputInvoke
        Me.PageLoaderAutoRun = AutoRun
        'Add the completion callback
        If FinishedInvoke IsNot Nothing Then
            AddHandler RealLoader.PreviewFinish,
            Sub()
                Do While PageState = MyPageRight.PageStates.PageExit OrElse PageState = MyPageRight.PageStates.ContentExit
                    Thread.Sleep(10) 'Do not run UI thread work during the exit animation, to avoid resetting it
                Loop
                RunInUiWait(Sub() FinishedInvoke(RealLoader))
                Thread.Sleep(20) 'Initializing many controls causes frame drops, so delay the State change event
            End Sub
        End If
        AddHandler RealLoader.OnStateChangedUi, Sub(Loader As LoaderBase, NewState As LoadState, OldState As LoadState) RunInUi(Sub() PageLoaderState(Loader, NewState, OldState))
        'Hide the UI
        PanLoader.Visibility = Visibility.Collapsed
        PanContent.Visibility = Visibility.Collapsed
        If PanAlways IsNot Nothing Then PanAlways.Visibility = Visibility.Collapsed
        'Run the loader for the first time
        If PageLoaderAutoRun Then
            'This is the only difference between the two overloads
            PageLoader.Start(RealLoader.StartGetInput(Nothing, PageLoaderInputInvoke))
        End If
        If PageLoader.State = LoadState.Finished AndAlso FinishedInvoke IsNot Nothing Then
            RunInUiWait(Sub() FinishedInvoke(RealLoader)) 'The loader already finished, raise the event directly
        End If
        'Set up the loading indicator
        PageLoaderUi.State = RealLoader
        AddHandler PageLoaderUi.Click, Sub() If RealLoader.State = LoadState.Failed Then PageLoaderRestart() 'Click to retry
    End Sub
    ''' <summary>
    ''' Indicates that the page has a loader that needs to run in the background.
    ''' </summary>
    ''' <param name="LoaderUi">The MyLoading control.</param>
    ''' <param name="PanLoader">The card that contains the MyLoading control.</param>
    ''' <param name="PanContent">The content container shown after loading finishes.</param>
    ''' <param name="PanAlways">A container that is always shown, whether loading or not. Can be Nothing.</param>
    ''' <param name="RealLoader">The loader that runs on a worker thread.</param>
    ''' <param name="FinishedInvoke">UI initialization callback invoked on the UI thread when the loader finishes.</param>
    Public Sub PageLoaderInit(Of TIn)(LoaderUi As MyLoading, PanLoader As FrameworkElement, PanContent As FrameworkElement, PanAlways As FrameworkElement,
                              RealLoader As LoaderCombo(Of TIn),
                              Optional FinishedInvoke As Action(Of LoaderCombo(Of TIn)) = Nothing, Optional InputInvoke As Func(Of TIn) = Nothing,
                              Optional AutoRun As Boolean = True)
        'Initialize parameters
        Me.PanLoader = PanLoader
        Me.PanContent = PanContent
        Me.PanAlways = PanAlways
        Me.PageLoader = RealLoader
        Me.PageLoaderUi = LoaderUi
        Me.PageLoaderInputInvoke = InputInvoke
        Me.PageLoaderAutoRun = AutoRun
        'Add the completion callback
        If FinishedInvoke IsNot Nothing Then
            AddHandler RealLoader.PreviewFinish,
            Sub()
                Do While PageState = MyPageRight.PageStates.PageExit OrElse PageState = MyPageRight.PageStates.ContentExit
                    Thread.Sleep(10) 'Do not run UI thread work during the exit animation, to avoid resetting it
                Loop
                RunInUiWait(Sub() FinishedInvoke(RealLoader))
                Thread.Sleep(20) 'Initializing many controls causes frame drops, so delay the State change event
            End Sub
        End If
        AddHandler RealLoader.OnStateChangedUi, Sub(Loader As LoaderBase, NewState As LoadState, OldState As LoadState) RunInUi(Sub() PageLoaderState(Loader, NewState, OldState))
        'Hide the UI
        PanLoader.Visibility = Visibility.Collapsed
        PanContent.Visibility = Visibility.Collapsed
        If PanAlways IsNot Nothing Then PanAlways.Visibility = Visibility.Collapsed
        'Run the loader for the first time
        If PageLoaderAutoRun Then
            'This is the only difference between the two overloads
            Dim Input = Nothing
            If PageLoaderInputInvoke IsNot Nothing Then Input = PageLoaderInputInvoke()
            PageLoader.Start(Input)
        End If
        If PageLoader.State = LoadState.Finished AndAlso FinishedInvoke IsNot Nothing Then
            RunInUiWait(Sub() FinishedInvoke(RealLoader)) 'The loader already finished, raise the event directly
        End If
        'Set up the loading indicator
        PageLoaderUi.State = RealLoader
        AddHandler PageLoaderUi.Click, Sub() If RealLoader.State = LoadState.Failed Then PageLoaderRestart() 'Click to retry
    End Sub
    'Retry
    Public Sub PageLoaderRestart(Optional Input As Object = Nothing, Optional IsForceRestart As Boolean = True) 'Retry called from outside
        If Not PageLoaderAutoRun Then Return
        If GeneralUtils.IsGenericInstanceOf(PageLoader, GetType(LoaderTask(Of ,))) OrElse TypeOf PageLoader Is LoaderWorker Then
            PageLoader.Start(CType(PageLoader, Object).StartGetInput(Input, PageLoaderInputInvoke), IsForceRestart:=IsForceRestart)
        Else
            If Input Is Nothing AndAlso PageLoaderInputInvoke IsNot Nothing Then Input = PageLoaderInputInvoke()
            PageLoader.Start(Input, IsForceRestart:=IsForceRestart)
        End If
    End Sub

#End Region

#Region "Events"

    'Externally triggered events
    ''' <summary>
    ''' Switching to this page is required, and the original Loaded event has finished.
    ''' Depending on the loader state, switch from Empty to ContentEnter, LoaderWait or LoaderEnter.
    ''' </summary>
    Public Sub PageOnEnter()
        RaiseEvent PageEnter()
        Select Case PageState
            Case PageStates.Empty
                If PageLoader Is Nothing OrElse PageLoader.State = LoadState.Finished OrElse PageLoader.State = LoadState.Waiting OrElse PageLoader.State = LoadState.Canceled Then
                    'If the loader does not start when entering the page (e.g. multiplayer), State = Waiting at this point
                    PageState = PageStates.ContentEnter
                    TriggerEnterAnimation(PanAlways, If(PanContent, Child))
                ElseIf PageLoader.State = LoadState.Loading Then
                    PageState = PageStates.LoaderWait
                    AniStart(AaCode(AddressOf PageOnLoaderWaitFinished, 400), "PageRight PageChange " & PageUuid)
                Else 'PageLoader.State = LoadState.Failed
                    PageState = PageStates.LoaderEnter
                    TriggerEnterAnimation(PanAlways, PanLoader)
                End If
            Case PageStates.ContentExit
                'Same as above, but ignoring PanAlways
                If PageLoader Is Nothing OrElse PageLoader.State = LoadState.Finished OrElse PageLoader.State = LoadState.Waiting OrElse PageLoader.State = LoadState.Canceled Then
                    PageState = PageStates.ContentEnter
                    TriggerEnterAnimation(If(PanContent, Child))
                ElseIf PageLoader.State = LoadState.Loading Then
                    PageState = PageStates.LoaderWait
                    AniStart(AaCode(AddressOf PageOnLoaderWaitFinished, 400), "PageRight PageChange " & PageUuid)
                Else 'PageLoader.State = LoadState.Failed
                    PageState = PageStates.LoaderEnter
                    TriggerEnterAnimation(PanLoader)
                End If
            Case PageStates.ContentEnter
                'PageOnEnter called repeatedly, ignore
            Case Else
                Throw New Exception("在状态为 " & PageState.ToString() & " 时触发了 PageOnEnter 事件。")
        End Select
    End Sub
    Public Event PageEnter()
    ''' <summary>
    ''' Switching to another page is required.
    ''' Must switch to PageExit or Empty immediately.
    ''' </summary>
    Public Sub PageOnExit()
        RaiseEvent PageExit()
        Select Case PageState
            Case PageStates.ContentEnter, PageStates.ContentStay
                PageState = PageStates.PageExit
                TriggerExitAnimation(PanAlways, If(PanContent, Child))
            Case PageStates.LoaderEnter, PageStates.LoaderStayForce, PageStates.LoaderStay
                PageState = PageStates.PageExit
                TriggerExitAnimation(PanAlways, PanLoader)
            Case PageStates.LoaderWait
                PageState = PageStates.PageExit
                TriggerExitAnimation(PanAlways)
            Case PageStates.LoaderExit, PageStates.ContentExit
                PageState = PageStates.PageExit
                If PanAlways IsNot Nothing Then TriggerExitAnimation(PanAlways, If(PanContent, Child))
            Case PageStates.PageExit, PageStates.Empty
        End Select
    End Sub
    Public Event PageExit()
    ''' <summary>
    ''' About to switch to another page, so the page state must be forcibly cleaned up.
    ''' Must switch to Empty immediately.
    ''' </summary>
    Public Sub PageOnForceExit()
        If PageState = PageStates.Empty Then Return
        PageState = PageStates.Empty
        AniStop("PageRight PageChange " & PageUuid)
        'The animation is forcibly stopped, so hide things manually
        If PageLoader Is Nothing Then
            Child.Visibility = Visibility.Collapsed
        Else
            PanContent.Visibility = Visibility.Collapsed
            PanLoader.Visibility = Visibility.Collapsed
            If PanAlways IsNot Nothing Then PanAlways.Visibility = Visibility.Collapsed
        End If
    End Sub
    ''' <summary>
    ''' The sub-page in PanContent changed: the current content must exit before the new content is shown.
    ''' The sub-pages to show must be determined in the PageEnter event.
    ''' </summary>
    Public Sub PageOnContentExit()
        If PageLoader IsNot Nothing AndAlso PageLoader.State = LoadState.Loading Then
            'The Loading loader may cause further changes, so it is hard to predict whether the sub-page animation or the loader-finished animation will run
            Throw New Exception("在调用 PageOnContentExit 时，加载器不能为 Loading 状态")
        End If
        Select Case PageState
            Case PageStates.ContentEnter, PageStates.ContentStay
                PageState = PageStates.ContentExit
                TriggerExitAnimation(PanContent)
            Case PageStates.LoaderExit
                PageState = PageStates.ContentExit
            Case PageStates.LoaderEnter, PageStates.LoaderStayForce, PageStates.LoaderStay
                PageState = PageStates.ContentExit
                TriggerExitAnimation(PanLoader)
            Case PageStates.LoaderWait
                PageOnEnter()
            Case PageStates.Empty
                'Another background thread may have called this after the page was left; PageOnEnter() must not be triggered then
                'E.g. after joining a multiplayer room, switching to another page, and then the room is disbanded
                If IsLoaded Then PageOnEnter()
        End Select
    End Sub

    'Internally triggered events
    ''' <summary>
    ''' The one-by-one enter animation has finished.
    ''' Depending on the current state, switch from ContentEnter to ContentStay, or from LoaderEnter to LoaderStayForce.
    ''' </summary>
    Private Sub PageOnEnterAnimationFinished()
        Select Case PageState
            Case PageStates.ContentEnter
                PageState = PageStates.ContentStay
            Case PageStates.LoaderEnter
                PageState = PageStates.LoaderStayForce
                AniStart(AaCode(AddressOf PageOnLoaderStayFinished, 400), "PageRight PageChange " & PageUuid)
            Case Else
                Throw New Exception("在状态为 " & PageState.ToString() & " 时触发了 PageOnEnterAnimationFinished 事件。")
        End Select
    End Sub
    ''' <summary>
    ''' The one-by-one exit animation has finished.
    ''' Depending on the current state, switch from AllExit to Empty, from LoaderExit to ContentEnter, or re-trigger PageOnEnter from ContentExit.
    ''' </summary>
    Private Sub PageOnExitAnimationFinished()
        Select Case PageState
            Case PageStates.PageExit
                PageState = PageStates.Empty
            Case PageStates.ContentExit
                PageOnEnter()
            Case PageStates.LoaderExit
                PageState = PageStates.ContentEnter
                TriggerEnterAnimation(PanContent)
            Case Else
                Throw New Exception("在状态为 " & PageState.ToString() & " 时触发了 PageOnExitAnimationFinished 事件。")
        End Select
    End Sub
    ''' <summary>
    ''' The initial wait of the loading indicator has ended.
    ''' Must switch from LoaderWait to LoaderEnter.
    ''' </summary>
    Private Sub PageOnLoaderWaitFinished()
        Select Case PageState
            Case PageStates.LoaderWait
                PageState = PageStates.LoaderEnter
                If PanAlways IsNot Nothing AndAlso PanAlways.Visibility = Visibility.Collapsed Then
                    TriggerEnterAnimation(PanAlways, PanLoader)
                Else
                    TriggerEnterAnimation(PanLoader)
                End If
            Case Else
                Throw New Exception("在状态为 " & PageState.ToString() & " 时触发了 PageOnLoaderWaitFinished 事件。")
        End Select
    End Sub
    ''' <summary>
    ''' The display wait of the loading indicator has ended.
    ''' Must switch from LoaderStayForce to LoaderStay or LoaderExit.
    ''' </summary>
    Private Sub PageOnLoaderStayFinished()
        Select Case PageState
            Case PageStates.LoaderStayForce
                If PageLoader.State = LoadState.Finished Then
                    PageState = PageStates.LoaderExit
                    TriggerExitAnimation(PanLoader)
                Else
                    PageState = PageStates.LoaderStay
                End If
            Case Else
                Throw New Exception("在状态为 " & PageState.ToString() & " 时触发了 PageOnLoaderWaitFinished 事件。")
        End Select
    End Sub

    ''' <summary>
    ''' The global loading state has changed.
    ''' </summary>
    Private Sub PageLoaderState(sender As Object, NewState As LoadState, OldState As LoadState)
        Select Case NewState
            Case LoadState.Failed, LoadState.Loading
                If OldState = LoadState.Failed OrElse OldState = LoadState.Loading Then Return
                '(Re)started running
                'Must switch from some states to ReloadExit
                Select Case PageState
                    Case PageStates.ContentEnter, PageStates.ContentStay
                        PageState = PageStates.ContentExit
                        TriggerExitAnimation(PanContent)
                    Case PageStates.LoaderExit
                        PageState = PageStates.ContentExit
                End Select
            Case LoadState.Finished, LoadState.Canceled, LoadState.Waiting
                If Not (OldState = LoadState.Failed OrElse OldState = LoadState.Loading) Then Return
                'Finished running
                'Must switch from LoaderWait to ContentEnter, or from LoaderStay to LoaderExit
                Select Case PageState
                    Case PageStates.LoaderWait
                        PageState = PageStates.ContentEnter
                        If PanAlways IsNot Nothing AndAlso PanAlways.Visibility = Visibility.Collapsed Then
                            TriggerEnterAnimation(PanAlways, PanContent)
                        Else
                            TriggerEnterAnimation(PanContent)
                        End If
                    Case PageStates.LoaderStay
                        PageState = PageStates.LoaderExit
                        TriggerExitAnimation(PanLoader)
                End Select
        End Select
    End Sub

#End Region

#Region "Animation"

    'One-by-one enter animation
    Public Sub TriggerEnterAnimation(ParamArray Elements As FrameworkElement())
        Dim RealElements = Elements.Where(Function(e) e IsNot Nothing)
        For Each Element In RealElements
            Element.Visibility = Visibility.Visible 'Pages are hidden by default
        Next
        Dim AniList As New List(Of AniData)
        Dim Delay As Integer = 0
        'Basic animation
        For Each Element In RealElements
            For Each Control As FrameworkElement In GetAllAnimControls(Element, True)
                'Revert the disappear animation of hidden cards
                Control.IsHitTestVisible = True
                If Control.RenderTransform IsNot Nothing AndAlso TypeOf Control.RenderTransform Is TranslateTransform Then Control.RenderTransform = Nothing
            Next
            For Each Control As FrameworkElement In GetAllAnimControls(Element)
                If TypeOf Control Is MyExtraTextButton Then
                    CType(Control, MyExtraTextButton).Show = True
                Else
                    Control.Opacity = 0
                    Control.RenderTransform = New TranslateTransform(0, -16)
                    AniList.Add(AaOpacity(Control, 1, 100, Delay, New AniEaseOutFluent(AniEasePower.Weak)))
                    AniList.Add(AaTranslateY(Control, 5, 250, Delay, New AniEaseOutFluent))
                    AniList.Add(AaTranslateY(Control, 11, 350, Delay, New AniEaseOutBack))
                    Delay += 25
                End If
            Next
        Next
        'Scroll bar animation
        Dim Scroll As MyScrollBar = GetFirstScrollViewer(RealElements)
        If Scroll IsNot Nothing Then
            If TypeOf Scroll.RenderTransform IsNot TranslateTransform Then Scroll.RenderTransform = New TranslateTransform(10, 0)
            AniList.Add(AaTranslateX(Scroll, -CType(Scroll.RenderTransform, TranslateTransform).X, 350, 0, New AniEaseOutFluent))
        End If
        'Finish
        AniList.Add(AaCode(Sub() PageOnEnterAnimationFinished(),, True))
        AniStart(AniList, "PageRight PageChange " & PageUuid, True)
    End Sub

    'One-by-one exit animation
    Public Sub TriggerExitAnimation(ParamArray Elements As FrameworkElement())
        Dim RealElements = Elements.Where(Function(e) e IsNot Nothing)
        Dim AniList As New List(Of AniData)
        Dim Delay As Integer = 0
        For Each Element In RealElements
            For Each Control As FrameworkElement In GetAllAnimControls(Element)
                If TypeOf Control Is MyExtraTextButton Then
                    CType(Control, MyExtraTextButton).Show = False
                Else
                    Control.IsHitTestVisible = False
                    AniList.Add(AaOpacity(Control, -1, 70, Delay))
                    AniList.Add(AaTranslateY(Control, -6, 70, Delay))
                    Delay += 15
                End If
            Next
        Next
        'Scroll bar animation
        Dim Scroll As MyScrollBar = GetFirstScrollViewer(RealElements)
        If Scroll IsNot Nothing Then
            If TypeOf Scroll.RenderTransform IsNot TranslateTransform Then Scroll.RenderTransform = New TranslateTransform
            AniList.Add(AaTranslateX(Scroll, 10 - CType(Scroll.RenderTransform, TranslateTransform).X, 90, 0, New AniEaseInFluent))
        End If
        'Finish
        AniList.Add(AaCode(
        Sub()
            For Each Element In RealElements
                Element.Visibility = Visibility.Collapsed
            Next
            PageOnExitAnimationFinished()
        End Sub,, True))
        AniStart(AniList, "PageRight PageChange " & PageUuid)
    End Sub

    ''' <summary>
    ''' Controls for which the page transition animation is disabled.
    ''' </summary>
    Public DisabledPageAnimControls As New List(Of FrameworkElement)
    ''' <summary>
    ''' Recursively collects all controls that need animation.
    ''' </summary>
    Friend Function GetAllAnimControls(Element As FrameworkElement, Optional IgnoreInvisibility As Boolean = False) As IEnumerable(Of FrameworkElement)
        Dim AllControls As New List(Of FrameworkElement)
        _GetAllAnimControls(Element, AllControls, IgnoreInvisibility)
        Return AllControls.Except(DisabledPageAnimControls)
    End Function
    Private Sub _GetAllAnimControls(Element As FrameworkElement, ByRef AllControls As List(Of FrameworkElement), IgnoreInvisibility As Boolean)
        If Not IgnoreInvisibility AndAlso Element.Visibility = Visibility.Collapsed Then Return
        If TypeOf Element Is MyCard OrElse TypeOf Element Is MyHint OrElse TypeOf Element Is MyExtraTextButton OrElse TypeOf Element Is TextBlock OrElse TypeOf Element Is MyTextButton Then
            AllControls.Add(Element)
        ElseIf TypeOf Element Is ContentControl Then
            Dim Content = CType(Element, ContentControl).Content
            If Content IsNot Nothing AndAlso TypeOf Content Is FrameworkElement Then _GetAllAnimControls(Content, AllControls, IgnoreInvisibility)
        ElseIf TypeOf Element Is Panel Then
            For Each Element2 In CType(Element, Panel).Children
                If TypeOf Element2 Is FrameworkElement Then _GetAllAnimControls(Element2, AllControls, IgnoreInvisibility)
            Next
        End If
    End Sub

    'Find the first scroll bar in the list
    Private Function GetFirstScrollViewer(Elements As IEnumerable(Of FrameworkElement)) As MyScrollBar
        Dim Viewer As MyScrollViewer = Nothing
        For Each Element In Elements
            If TypeOf Element Is MyScrollViewer Then
                Viewer = Element
                GoTo FindViewer
            End If
            For Each Control In LogicalTreeHelper.GetChildren(Element)
                If TypeOf Control Is MyScrollViewer Then
                    Viewer = Control
                    GoTo FindViewer
                End If
            Next
        Next
        Return Nothing
FindViewer:
        If Viewer.ComputedVerticalScrollBarVisibility <> Visibility.Visible Then Return Nothing
        Return Viewer.ScrollBar
    End Function

#End Region

End Class
