Public Class MyTextButton
    Inherits Label

    Public Event Click(sender As Object, e As EventArgs)

    'Basics

    Public Uuid As Integer = GetUuid()
    Public Sub New()
        SetResourceReference(ForegroundProperty, "ColorBrush1")
        Background = ColorSemiTransparent
    End Sub

    'Text

    Public Property Text As String
        Get
            Return GetValue(TextProperty)
        End Get
        Set(value As String)
            SetValue(TextProperty, value)
        End Set
    End Property
    Public Shared ReadOnly TextProperty As DependencyProperty =
        DependencyProperty.Register("Text", GetType(String), GetType(MyTextButton), New PropertyMetadata("",
        Sub(sender As MyTextButton, e As DependencyPropertyChangedEventArgs)
            If e.OldValue = e.NewValue Then Return
            AniStart({
                AaOpacity(sender, -sender.Opacity, 50),
                AaCode(Sub() sender.Content = e.NewValue,, True),
                AaOpacity(sender, 1, 170)
            }, "MyTextButton Text " & sender.Uuid)
        End Sub))

    'Mouse events

    Public IsMouseDown As Boolean = False
    Private Sub MyTextButton_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs) Handles Me.PreviewMouseLeftButtonDown
        IsMouseDown = True
        e.Handled = True
    End Sub
    Private Sub MyTextButton_MouseLeave() Handles Me.MouseLeave
        IsMouseDown = False
    End Sub
    Private Sub MyTextButton_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles Me.PreviewMouseLeftButtonUp
        If Not IsMouseDown Then Return
        IsMouseDown = False
        RaiseEvent Click(Me, Nothing)
        RaiseCustomEvent()
        e.Handled = True
    End Sub

    'Hover animation

    Private Const AnimationTimeIn As Integer = 100
    Private Const AnimationTimeOut As Integer = 200
    Private ColorName As String
    Private Sub RefreshColor() Handles Me.MouseEnter, Me.MouseLeave, Me.IsEnabledChanged, Me.MouseLeftButtonDown, Me.MouseLeftButtonUp
        'Determine the current color
        Dim ForeName As String
        Dim Time As Integer
        If IsMouseDown Then
            ForeName = "ColorBrush4"
            Time = 30
        ElseIf IsMouseOver Then
            ForeName = "ColorBrush3"
            Time = AnimationTimeIn
        Else
            ForeName = "ColorBrush1"
            Time = AnimationTimeOut
        End If
        'Skip if unchanged
        If ColorName = ForeName Then Return
        ColorName = ForeName
        'Trigger the color animation
        If IsLoaded AndAlso AniControlEnabled = 0 Then 'Prevent default property changes from triggering animations
            'With animation
            AniStart(AaColor(Me, ForegroundProperty, ForeName, Time), "MyTextButton Color " & Uuid)
        Else
            'Without animation
            AniStop("MyTextButton Color " & Uuid)
            SetResourceReference(ForegroundProperty, ForeName)
        End If
    End Sub

End Class
