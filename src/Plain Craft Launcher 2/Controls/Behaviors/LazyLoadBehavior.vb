Imports System.Runtime.CompilerServices

Public Class LazyLoadBehavior
    Private Shared ReadOnly Observers As New ConditionalWeakTable(Of ScrollViewer, ViewportObserver)
    Private Shared ReadOnly RegistrationProperty As DependencyProperty = DependencyProperty.RegisterAttached("Registration", GetType(Registration), GetType(LazyLoadBehavior))
    Private Shared ReadOnly IsInViewportProperty As DependencyProperty = DependencyProperty.RegisterAttached("IsInViewport", GetType(Action), GetType(LazyLoadBehavior), New PropertyMetadata(Nothing, AddressOf OnIsInViewportChanged))

    Public Shared Sub OnFirstEnterScrollViewerViewport(obj As DependencyObject, value As Action)
        obj.SetValue(IsInViewportProperty, value)
    End Sub

    Private Shared Sub OnIsInViewportChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim Element = TryCast(d, FrameworkElement)
        If Element Is Nothing Then Return
        DirectCast(Element.GetValue(RegistrationProperty), Registration)?.Detach()
        If e.NewValue Is Nothing Then
            Element.ClearValue(RegistrationProperty)
            Return
        End If
        Dim Item As New Registration(Element, DirectCast(e.NewValue, Action))
        Element.SetValue(RegistrationProperty, Item)
        Item.Attach()
    End Sub

    Private Class Registration
        Public ReadOnly Element As FrameworkElement
        Private Callback As Action
        Private Observer As ViewportObserver
        Public Sub New(Element As FrameworkElement, Callback As Action)
            Me.Element = Element
            Me.Callback = Callback
        End Sub
        Public Sub Attach()
            AddHandler Element.Loaded, AddressOf OnLoaded
            AddHandler Element.Unloaded, AddressOf OnUnloaded
            If Element.IsLoaded Then OnLoaded(Nothing, Nothing)
        End Sub
        Private Sub OnLoaded(Sender As Object, e As RoutedEventArgs)
            If Callback Is Nothing OrElse Observer IsNot Nothing Then Return
            Dim Parent As DependencyObject = Element
            While Parent IsNot Nothing AndAlso Not TypeOf Parent Is ScrollViewer
                Parent = VisualTreeHelper.GetParent(Parent)
            End While
            If Parent Is Nothing Then Return
            Observer = Observers.GetValue(DirectCast(Parent, ScrollViewer), Function(Scroll) New ViewportObserver(Scroll))
            Observer.Add(Me)
        End Sub
        Private Sub OnUnloaded(Sender As Object, e As RoutedEventArgs)
            Observer?.Remove(Me)
            Observer = Nothing
        End Sub
        Public Sub Detach()
            OnUnloaded(Nothing, Nothing)
            RemoveHandler Element.Loaded, AddressOf OnLoaded
            RemoveHandler Element.Unloaded, AddressOf OnUnloaded
        End Sub
        Public Sub Complete()
            Dim Action = Callback
            Callback = Nothing
            Detach()
            Element.ClearValue(RegistrationProperty)
            Element.ClearValue(IsInViewportProperty)
            Action?.Invoke()
        End Sub
    End Class

    Private Class ViewportObserver
        Private ReadOnly Scroll As ScrollViewer
        Private ReadOnly Pending As New HashSet(Of Registration)
        Private CheckQueued As Boolean
        Public Sub New(Scroll As ScrollViewer)
            Me.Scroll = Scroll
        End Sub
        Public Sub Add(Item As Registration)
            If Pending.Count = 0 Then
                AddHandler Scroll.ScrollChanged, AddressOf OnScrollChanged
                AddHandler Scroll.SizeChanged, AddressOf OnSizeChanged
                AddHandler Scroll.LayoutUpdated, AddressOf OnLayoutUpdated
            End If
            Pending.Add(Item)
            RequestCheck()
        End Sub
        Public Sub Remove(Item As Registration)
            Pending.Remove(Item)
            If Pending.Count <> 0 Then Return
            RemoveHandler Scroll.ScrollChanged, AddressOf OnScrollChanged
            RemoveHandler Scroll.SizeChanged, AddressOf OnSizeChanged
            RemoveHandler Scroll.LayoutUpdated, AddressOf OnLayoutUpdated
        End Sub
        Private Sub OnScrollChanged(Sender As Object, e As ScrollChangedEventArgs)
            RequestCheck()
        End Sub
        Private Sub OnSizeChanged(Sender As Object, e As SizeChangedEventArgs)
            RequestCheck()
        End Sub
        Private Sub OnLayoutUpdated(Sender As Object, e As EventArgs)
            RequestCheck()
        End Sub
        Private Sub RequestCheck()
            If CheckQueued OrElse Pending.Count = 0 Then Return
            CheckQueued = True
            Scroll.Dispatcher.BeginInvoke(Threading.DispatcherPriority.Background, New Action(AddressOf CheckViewport))
        End Sub
        Private Sub CheckViewport()
            CheckQueued = False
            If Pending.Count = 0 OrElse Scroll.ViewportWidth <= 0 OrElse Scroll.ViewportHeight <= 0 Then Return
            Dim Viewport As New Rect(0, 0, Scroll.ViewportWidth, Scroll.ViewportHeight)
            Dim Started = Stopwatch.GetTimestamp()
            For Each Item In Pending.ToArray()
                If Not Pending.Contains(Item) OrElse Not Item.Element.IsVisible OrElse Item.Element.RenderSize.Width <= 0 OrElse Not Scroll.IsAncestorOf(Item.Element) Then Continue For
                Dim Bounds = Item.Element.TransformToAncestor(Scroll).TransformBounds(New Rect(New Point(0, 0), Item.Element.RenderSize))
                If Not Viewport.IntersectsWith(Bounds) Then Continue For
                Item.Complete()
                'Spread expensive control creation across dispatcher turns instead of blocking input.
                If Stopwatch.GetTimestamp() - Started >= Stopwatch.Frequency * 0.008 Then
                    RequestCheck()
                    Exit For
                End If
            Next
        End Sub
    End Class
End Class
