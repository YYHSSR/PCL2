Imports System.Windows.Shapes

Public Class MyIconButton

    'Custom event
    Public Event Click(sender As Object, e As EventArgs)

    'Custom properties

    Public Uuid As Integer = GetUuid()
    Public Property Logo As String
        Get
            Return Path.Data.ToString
        End Get
        Set(value As String)
            Path.Data = (New GeometryConverter).ConvertFromString(value)
        End Set
    End Property

    Private _LogoScale As Double = 1
    Public Property LogoScale() As Double
        Get
            Return _LogoScale
        End Get
        Set(value As Double)
            _LogoScale = value
            If Not IsNothing(Path) Then Path.RenderTransform = New ScaleTransform With {.ScaleX = LogoScale, .ScaleY = LogoScale}
        End Set
    End Property

    Public Enum Themes
        Color
        White
        Black
        Red
        Custom
    End Enum
    Public Property Theme As Themes = Themes.Color

    Private _Foreground As New SolidColorBrush(Color.FromRgb(128, 128, 128))
    Public Property Foreground As SolidColorBrush
        Get
            Return _Foreground
        End Get
        Set(value As SolidColorBrush)
            _Foreground = value
            AniControlEnabled += 1
            RefreshAnim()
            AniControlEnabled -= 1
        End Set
    End Property

    'Mouse click detection (must be placed after the click event so that Button_MouseUp runs before Button_MouseLeave)
    Private IsMouseDown As Boolean = False
    Public Property CanClick As Boolean = True
    Private Sub Button_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonUp
        If Not CanClick Then Return
        If Not IsMouseDown Then Return
        RaiseEvent Click(sender, e)
        e.Handled = True
        Button_MouseUp()
        RaiseCustomEvent()
    End Sub
    Private Sub Button_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonDown
        If Not CanClick Then Return
        IsMouseDown = True
        Focus()
        'Hover
        AniStart(AaScaleTransform(PanBack, 0.8 - CType(PanBack.RenderTransform, ScaleTransform).ScaleX, 400,, New AniEaseOutFluent(AniEasePower.Strong)), "MyIconButton Scale " & Uuid)
    End Sub
    Private Sub Button_MouseUp() Handles Me.MouseLeftButtonUp
        If Not CanClick Then Return
        If IsMouseDown Then
            IsMouseDown = False
            AniStart({
                     AaScaleTransform(PanBack, 1.05 - CType(PanBack.RenderTransform, ScaleTransform).ScaleX, 250,, New AniEaseOutBack(AniEasePower.Weak)),
                     AaScaleTransform(PanBack, -0.05, 250,, New AniEaseOutFluent(AniEasePower.Strong))
                 }, "MyIconButton Scale " & Uuid)
        End If
        RefreshAnim() 'Refresh the color directly to determine whether MouseLeave has been raised
    End Sub
    Private Sub Button_MouseLeave() Handles Me.MouseLeave
        If Not CanClick Then Return
        IsMouseDown = False
        AniStart({
                     AaScaleTransform(PanBack, 1 - CType(PanBack.RenderTransform, ScaleTransform).ScaleX, 250,, New AniEaseOutFluent)
                 }, "MyIconButton Scale " & Uuid)
        RefreshAnim() 'Refresh the color directly to determine whether MouseLeave has been raised
    End Sub

    'Must be placed after IsMouseDown is updated
    Private Const AnimationColorIn As Integer = 120
    Private Const AnimationColorOut As Integer = 150
    Public Sub RefreshAnim() Handles Me.MouseEnter, Me.MouseLeave, Me.Loaded, Me.IsEnabledChanged
        Try
            If IsLoaded AndAlso AniControlEnabled = 0 Then 'Prevent default property changes from triggering animations
                If PanBack.Background Is Nothing Then PanBack.Background = New MyColor(0, 255, 255, 255)
                If Path.Fill Is Nothing Then
                    Select Case Theme
                        Case Themes.Red
                            Path.Fill = New MyColor(160, 255, 76, 76)
                        Case Themes.Black
                            Path.Fill = New MyColor(160, 0, 0, 0)
                        Case Themes.Custom
                            Path.Fill = New MyColor(160, Foreground)
                    End Select
                End If
                Dim AnimList As New List(Of AniData)
                If Not IsEnabled Then
                    AnimList.Add(AaColor(Path, Shape.FillProperty, "ColorBrushGray5", AnimationColorIn))
                ElseIf IsMouseOver Then 'Hover
                    Select Case Theme
                        Case Themes.Color
                            AnimList.Add(AaColor(Path, Shape.FillProperty, "ColorBrush2", AnimationColorIn))
                        Case Themes.White
                            AnimList.Add(AaColor(PanBack, BackgroundProperty, New MyColor(50, 255, 255, 255) - PanBack.Background, AnimationColorIn))
                        Case Themes.Red
                            AnimList.Add(AaColor(Path, Shape.FillProperty, New MyColor(255, 76, 76) - Path.Fill, AnimationColorIn))
                        Case Themes.Black
                            AnimList.Add(AaColor(Path, Shape.FillProperty, New MyColor(230, 0, 0, 0) - Path.Fill, AnimationColorIn))
                        Case Themes.Custom
                            AnimList.Add(AaColor(Path, Shape.FillProperty, New MyColor(255, Foreground) - Path.Fill, AnimationColorIn))
                    End Select
                Else 'Normal
                    Select Case Theme
                        Case Themes.Color
                            AnimList.Add(AaColor(Path, Shape.FillProperty, "ColorBrush4", AnimationColorOut))
                            PanBack.Background = New MyColor(0, 255, 255, 255)
                        Case Themes.White
                            AnimList.Add(AaColor(Path, Shape.FillProperty, "ColorBrush8", AnimationColorOut))
                            AnimList.Add(AaColor(PanBack, BackgroundProperty, New MyColor(0, 255, 255, 255) - PanBack.Background, AnimationColorOut))
                        Case Themes.Red
                            AnimList.Add(AaColor(Path, Shape.FillProperty, New MyColor(160, 255, 76, 76) - Path.Fill, AnimationColorOut))
                            PanBack.Background = New MyColor(0, 255, 255, 255)
                        Case Themes.Black
                            AnimList.Add(AaColor(Path, Shape.FillProperty, New MyColor(160, 0, 0, 0) - Path.Fill, AnimationColorOut))
                            PanBack.Background = New MyColor(0, 255, 255, 255)
                        Case Themes.Custom
                            AnimList.Add(AaColor(Path, Shape.FillProperty, New MyColor(160, Foreground) - Path.Fill, AnimationColorOut))
                            PanBack.Background = New MyColor(0, 255, 255, 255)
                    End Select
                End If
                AniStart(AnimList, "MyIconButton Color " & Uuid)
            Else
                AniStop("MyIconButton Color " & Uuid)
                If IsEnabled Then
                    Select Case Theme
                        Case Themes.Color
                            Path.SetResourceReference(Shape.FillProperty, "ColorBrush5")
                        Case Themes.White
                            Path.SetResourceReference(Shape.FillProperty, "ColorBrush8")
                        Case Themes.Red
                            Path.Fill = New MyColor(160, 255, 76, 76)
                        Case Themes.Black
                            Path.Fill = New MyColor(160, 0, 0, 0)
                        Case Themes.Custom
                            Path.Fill = New MyColor(160, Foreground)
                    End Select
                Else
                    Path.SetResourceReference(Shape.FillProperty, "ColorBrushGray5")
                End If
                PanBack.Background = New MyColor(0, 255, 255, 255)
            End If
        Catch ex As Exception
            Logger.Warn(ex, "刷新图标按钮动画状态出错")
        End Try
    End Sub

End Class
Partial Public Module ModAnimation
    Public Sub AniDispose(Control As MyIconButton, RemoveFromChildren As Boolean, Optional CallBack As ParameterizedThreadStart = Nothing)
        If Not Control.IsHitTestVisible Then Return
        Control.IsHitTestVisible = False
        AniStart({
                 AaScaleTransform(Control, -1.5, 200,, New AniEaseInFluent),
                 AaCode(Sub()
                            If RemoveFromChildren Then
                                CType(Control.Parent, Object).Children.Remove(Control)
                            Else
                                Control.Visibility = Visibility.Collapsed
                            End If
                            If CallBack IsNot Nothing Then CallBack(Control)
                        End Sub,, True)
        }, "MyIconButton Dispose " & Control.Uuid)
    End Sub
End Module
