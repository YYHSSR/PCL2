Public Class MyMenuItem
    Inherits MenuItem

    Private Sub MyMenuItem_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If Icon IsNot Nothing Then
            Dim IconControl As Shapes.Path = GetTemplateChild("Icon")
            If IconControl IsNot Nothing Then IconControl.Data = (New GeometryConverter).ConvertFromString(Icon)
        End If
        'Set the opacity on the parent
        If TypeOf Parent Is ContextMenu Then
            CType(Parent, ContextMenu).Opacity = 600 / 1000 + 0.4
        End If
    End Sub

    'Basics

    Public Uuid As Integer = GetUuid()

    'Hover animation

    Private Const AnimationTimeIn As Integer = 100
    Private Const AnimationTimeOut As Integer = 200
    Private ColorName As String
    Private Sub RefreshColor() Handles Me.MouseEnter, Me.MouseLeave, Me.IsEnabledChanged
        'Determine the current color
        Dim BackName As String, ForeName As String
        Dim Time As Integer
        If Not IsEnabled Then
            BackName = "ColorBrushTransparent"
            ForeName = "ColorBrushGray5"
            Time = AnimationTimeOut
        ElseIf IsMouseOver Then
            BackName = "ColorBrush6"
            ForeName = "ColorBrush2"
            Time = AnimationTimeIn
        Else
            BackName = "ColorBrushTransparent"
            ForeName = "ColorBrush1"
            Time = AnimationTimeOut
        End If
        'Skip if unchanged
        If ColorName = BackName Then Return
        ColorName = BackName
        'Trigger the color animation
        If IsLoaded AndAlso AniControlEnabled = 0 Then 'Prevent default property changes from triggering animations
            'With animation
            AniStart({
                     AaColor(Me, BackgroundProperty, BackName, Time),
                     AaColor(Me, ForegroundProperty, ForeName, Time)
                 }, "MyMenuItem Color " & Uuid)
        Else
            'Without animation
            AniStop("MyMenuItem Color " & Uuid)
            SetResourceReference(BackgroundProperty, BackName)
            SetResourceReference(ForegroundProperty, ForeName)
        End If
    End Sub

    Private Sub MyMenuItem_Click(sender As Object, e As RoutedEventArgs) Handles Me.Click
        RaiseCustomEvent()
    End Sub
End Class
