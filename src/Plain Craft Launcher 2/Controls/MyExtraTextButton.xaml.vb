Imports System.Windows.Markup

<ContentProperty("Inlines")>
Public Class MyExtraTextButton

    'Declarations
    Public Event Click(sender As Object, e As MouseButtonEventArgs) 'Custom event

    'Custom properties
    Public Uuid As Integer = GetUuid()
    Private _Logo As String = ""
    Public Property Logo As String
        Get
            Return _Logo
        End Get
        Set(value As String)
            If value = _Logo Then Return
            _Logo = value
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
            If Path IsNot Nothing Then Path.RenderTransform = New ScaleTransform With {.ScaleX = LogoScale, .ScaleY = LogoScale}
        End Set
    End Property
    'Display text
    Public ReadOnly Property Inlines As InlineCollection
        Get
            Return LabText.Inlines
        End Get
    End Property
    Public Property Text As String
        Get
            Return GetValue(TextProperty)
        End Get
        Set(value As String)
            SetValue(TextProperty, value)
        End Set
    End Property
    Public Shared ReadOnly TextProperty As DependencyProperty = DependencyProperty.Register("Text", GetType(String), GetType(MyExtraTextButton), New PropertyMetadata(New PropertyChangedCallback(
    Sub(sender As DependencyObject, e As DependencyPropertyChangedEventArgs)
        If sender IsNot Nothing Then CType(sender, MyExtraTextButton).LabText.Text = e.NewValue
    End Sub)))

    'Animation
    Private _Show As Boolean = False
    Public Property Show As Boolean
        Get
            Return _Show
        End Get
        Set(value As Boolean)
            If _Show = value Then Return
            _Show = value
            RunInUi(
            Sub()
                If value Then
                    'Shown
                    Opacity = 0
                    AniStart({
                        AaOpacity(Me, 1 - Opacity, 80, 50),
                        AaScaleTransform(Me, 0.15 - CType(RenderTransform, ScaleTransform).ScaleX, 400, 50, New AniEaseOutBack),
                        AaScaleTransform(Me, 0.85, 160, 50, New AniEaseOutFluent(AniEasePower.Middle))
                    }, "MyExtraTextButton MainScale " & Uuid)
                Else
                    'Hidden
                    AniStart({
                        AaOpacity(Me, -Opacity, 50, 50),
                        AaScaleTransform(Me, -CType(RenderTransform, ScaleTransform).ScaleX, 100,, New AniEaseInFluent(AniEasePower.Weak))
                    }, "MyExtraTextButton MainScale " & Uuid)
                End If
                IsHitTestVisible = value 'Prevent clicking it during the scale animation
            End Sub)
        End Set
    End Property

    'Raise the click event
    Private Sub Button_LeftMouseUp(sender As Object, e As MouseButtonEventArgs) Handles PanClick.MouseLeftButtonUp
        If Not IsLeftMouseHeld Then Return
        RaiseEvent Click(sender, e)
        e.Handled = True
        RaiseCustomEvent()
        Button_LeftMouseUp()
    End Sub

    'Mouse click detection (must be placed after the click event so that Button_MouseUp runs before Button_MouseLeave)
    Private IsLeftMouseHeld As Boolean = False
    Private Sub Button_LeftMouseDown(sender As Object, e As MouseButtonEventArgs) Handles PanClick.MouseLeftButtonDown
        If Not IsLeftMouseHeld Then
            AniStart({
                AaScaleTransform(PanScale, 0.85 - CType(PanScale.RenderTransform, ScaleTransform).ScaleX, 800,, New AniEaseOutFluent(AniEasePower.Strong)),
                AaScaleTransform(PanScale, -0.05, 60,, New AniEaseOutFluent)
            }, "MyExtraTextButton Scale " & Uuid)
        End If
        IsLeftMouseHeld = True
        Focus()
    End Sub
    Private Sub Button_LeftMouseUp() Handles PanClick.MouseLeftButtonUp
        AniStart({
            AaScaleTransform(PanScale, 1 - CType(PanScale.RenderTransform, ScaleTransform).ScaleX, 300,, New AniEaseOutBack)
        }, "MyExtraTextButton Scale " & Uuid)
        IsLeftMouseHeld = False
        RefreshColor() 'Refresh the color directly to determine whether MouseLeave has been raised
    End Sub
    Private Sub Button_RightMouseUp() Handles PanClick.MouseRightButtonUp
        If Not IsLeftMouseHeld Then
            AniStart({
                AaScaleTransform(PanScale, 1 - CType(PanScale.RenderTransform, ScaleTransform).ScaleX, 300,, New AniEaseOutBack)
            }, "MyExtraTextButton Scale " & Uuid)
        End If
        RefreshColor() 'Refresh the color directly to determine whether MouseLeave has been raised
    End Sub
    Private Sub Button_MouseLeave() Handles PanClick.MouseLeave
        IsLeftMouseHeld = False
        AniStart({
            AaScaleTransform(PanScale, 1 - CType(PanScale.RenderTransform, ScaleTransform).ScaleX, 500,, New AniEaseOutFluent)
        }, "MyExtraTextButton Scale " & Uuid)
        RefreshColor() 'Refresh the color directly to determine whether MouseLeave has been raised
    End Sub

    'Custom event
    'Must be placed after IsMouseDown is updated
    Private Const AnimationColorIn As Integer = 120
    Private Const AnimationColorOut As Integer = 150
    Public Sub RefreshColor() Handles PanClick.MouseEnter, PanClick.MouseLeave, Me.Loaded, Me.IsEnabledChanged
        Try
            If IsLoaded AndAlso AniControlEnabled = 0 Then 'Prevent default property changes from triggering animations

                If Not IsEnabled Then
                    'Disabled
                    AniStart(AaColor(PanColor, BackgroundProperty, "ColorBrushGray4", AnimationColorIn), "MyExtraTextButton Color " & Uuid)
                ElseIf IsMouseOver Then
                    'Hover
                    AniStart(AaColor(PanColor, BackgroundProperty, "ColorBrush4", AnimationColorIn), "MyExtraTextButton Color " & Uuid)
                Else
                    'Normal
                    AniStart(AaColor(PanColor, BackgroundProperty, "ColorBrush3", AnimationColorOut), "MyExtraTextButton Color " & Uuid)
                End If

            Else

                AniStop("MyExtraTextButton Color " & Uuid)
                If Not IsEnabled Then
                    PanColor.SetResourceReference(BackgroundProperty, "ColorBrushGray4")
                ElseIf IsMouseOver Then
                    PanColor.SetResourceReference(BackgroundProperty, "ColorBrush4")
                Else
                    PanColor.SetResourceReference(BackgroundProperty, "ColorBrush3")
                End If

            End If
        Catch ex As Exception
            Logger.Warn(ex, "刷新附加图标按钮颜色出错")
        End Try
    End Sub

End Class
