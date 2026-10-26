Public Class MyScrollViewer
    Inherits ScrollViewer

    Public Property DeltaMult As Double = 1

    Private RealOffset As Double
    Private Sub MyScrollViewer_PreviewMouseWheel(sender As Object, e As MouseWheelEventArgs) Handles Me.PreviewMouseWheel
        If e.Delta = 0 OrElse ActualHeight = 0 OrElse ScrollableHeight = 0 Then Return
        Dim SourceType = e.Source.GetType
        If Content.TemplatedParent Is Nothing AndAlso (
                (GetType(ComboBox).IsAssignableFrom(SourceType) AndAlso CType(e.Source, ComboBox).IsDropDownOpen) OrElse
                (GetType(TextBox).IsAssignableFrom(SourceType) AndAlso CType(e.Source, TextBox).AcceptsReturn) OrElse
                GetType(ComboBoxItem).IsAssignableFrom(SourceType) OrElse
                (TypeOf e.Source Is Visual AndAlso
                    PresentationSource.FromVisual(e.Source)?.RootVisual?.GetType().FullName = "System.Windows.Controls.Primitives.PopupRoot")) Then
            'Do not take over when scrolling over a combo box or text box that has its own scroll bar
            Return
        End If
        e.Handled = True
        PerformVerticalOffsetDelta(-e.Delta)
    End Sub
    Public Sub PerformVerticalOffsetDelta(Delta As Double)
        AniStart(
            AaDouble(
            Sub(AnimDelta As Double)
                RealOffset = (RealOffset + AnimDelta).Clamp(0, ExtentHeight - ActualHeight)
                ScrollToVerticalOffset(RealOffset)
            End Sub, Delta * DeltaMult, 300,, New AniEaseOutFluent(6)))
    End Sub
    Private Sub MyScrollViewer_ScrollChanged(sender As Object, e As ScrollChangedEventArgs) Handles Me.ScrollChanged
        RealOffset = VerticalOffset
        If FrmMain IsNot Nothing AndAlso (e.VerticalChange OrElse e.ViewportHeightChange) Then FrmMain.BtnExtraBack.ShowRefresh()
    End Sub
    Private Sub MyScrollViewer_IsVisibleChanged(sender As Object, e As DependencyPropertyChangedEventArgs) Handles Me.IsVisibleChanged
        FrmMain.BtnExtraBack.ShowRefresh()
    End Sub

    Public ScrollBar As MyScrollBar
    Private Sub Load() Handles Me.Loaded
        ScrollBar = GetTemplateChild("PART_VerticalScrollBar")
    End Sub

    Private Sub MyScrollViewer_PreviewGotKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs) Handles Me.PreviewGotKeyboardFocus
        If e.NewFocus IsNot Nothing AndAlso TypeOf e.NewFocus Is MySlider Then e.Handled = True '#3854: prevent automatic scrolling when getting focus
    End Sub
End Class
