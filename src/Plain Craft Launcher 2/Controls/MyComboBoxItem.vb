Public Class MyComboBoxItem
    Inherits ComboBoxItem

    'Basics

    Public Uuid As Integer = GetUuid()
    Public Sub New()
        Style = FindResource("MyComboBoxItem")
    End Sub

    'Hover animation

    Private Const AnimationTimeIn As Integer = 100
    Private Const AnimationTimeOut As Integer = 300
    Private BackColorName As String, FontOpacity As Double

    Private Sub RefreshColor() Handles Me.Unselected, Me.MouseMove, Me.MouseLeave, Me.Selected, Me.IsEnabledChanged
        'Determine the current color
        Dim NewBackColorName As String, NewFontOpacity As Double
        Dim Time As Integer
        If IsSelected Then
            NewBackColorName = "ColorBrush6"
            NewFontOpacity = 1
            Time = AnimationTimeIn
        ElseIf IsMouseOver Then
            NewBackColorName = "ColorBrush8"
            NewFontOpacity = 1
            Time = AnimationTimeIn
        ElseIf IsEnabled Then
            NewBackColorName = "ColorBrushTransparent"
            NewFontOpacity = 1
            Time = AnimationTimeOut
        Else
            NewBackColorName = "ColorBrushTransparent"
            NewFontOpacity = 0.4
            Time = AnimationTimeOut
        End If
        If BackColorName = NewBackColorName AndAlso FontOpacity = NewFontOpacity Then Return
        BackColorName = NewBackColorName
        FontOpacity = NewFontOpacity
        'Trigger the color animation
        If IsLoaded AndAlso AniControlEnabled = 0 Then 'Prevent default property changes from triggering animations
            'With animation
            AniStart({
                AaColor(Me, BackgroundProperty, BackColorName, Time),
                AaOpacity(Me, FontOpacity - Opacity, Time)
            }, "ComboBoxItem Color " & Uuid)
        Else
            'Without animation
            AniStop("ComboBoxItem Color " & Uuid)
            SetResourceReference(BackgroundProperty, BackColorName)
            Opacity = FontOpacity
        End If
    End Sub

    Public Overrides Function Tostring() As String
        Return Content.ToString
    End Function
    Public Shared Widening Operator CType(Value As MyComboBoxItem) As String
        Return Value.Content.ToString
    End Operator

End Class
