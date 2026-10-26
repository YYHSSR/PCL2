Imports System.Windows.Markup

<ContentProperty("Inlines")>
Public Class MyIconTextButton

    'Basics

    Public Uuid As Integer = GetUuid()
    Public Event Check(sender As Object, raiseByMouse As Boolean)
    Public Event Change(sender As Object, raiseByMouse As Boolean)

    'Custom properties

    Public Property Logo As String
        Get
            Return ShapeLogo.Data.ToString
        End Get
        Set(value As String)
            ShapeLogo.Data = (New GeometryConverter).ConvertFromString(value)
        End Set
    End Property
    Private _LogoScale As Double = 1
    Public Property LogoScale() As Double
        Get
            Return _LogoScale
        End Get
        Set(value As Double)
            _LogoScale = value
            If Not IsNothing(ShapeLogo) Then ShapeLogo.RenderTransform = New ScaleTransform With {.ScaleX = LogoScale, .ScaleY = LogoScale}
        End Set
    End Property

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
    End Property 'Content
    Public Shared ReadOnly TextProperty As DependencyProperty = DependencyProperty.Register("Text", GetType(String), GetType(MyIconTextButton), New PropertyMetadata(New PropertyChangedCallback(
    Sub(sender As DependencyObject, e As DependencyPropertyChangedEventArgs)
        If Not IsNothing(sender) Then CType(sender, MyIconTextButton).LabText.Text = e.NewValue
    End Sub)))
    Public Enum ColorState
        Black
        Highlight
    End Enum
    Public Property ColorType As ColorState
        Get
            Return GetValue(ColorTypeProperty)
        End Get
        Set(value As ColorState)
            If ColorType = value Then Return
            SetValue(ColorTypeProperty, value)
            RefreshColor()
        End Set
    End Property 'Color type
    Public Shared ReadOnly ColorTypeProperty As DependencyProperty =
        DependencyProperty.Register("ColorType", GetType(ColorState), GetType(MyIconTextButton), New PropertyMetadata(ColorState.Black))

    'Click event

    Public Event Click(sender As Object, e As RouteEventArgs)
    Private IsMouseDown As Boolean = False
    Private Sub MyIconTextButton_MouseUp() Handles Me.MouseLeftButtonUp
        If Not IsMouseDown Then Return
        IsMouseDown = False
        RaiseEvent Click(Me, New RouteEventArgs(True))
        RaiseCustomEvent()
        RefreshColor()
    End Sub
    Private Sub MyIconTextButton_MouseDown() Handles Me.MouseLeftButtonDown
        IsMouseDown = True
        RefreshColor()
    End Sub
    Private Sub MyIconTextButton_MouseLeave() Handles Me.MouseLeave
        IsMouseDown = False
        RefreshColor()
    End Sub

    'Animation

    Private Const AnimationTimeOfMouseIn As Integer = 100 'Mouse hover animation duration
    Private Const AnimationTimeOfMouseOut As Integer = 150 'Mouse leave animation duration
    Private Sub RefreshColor(Optional obj = Nothing, Optional e = Nothing) Handles Me.MouseEnter, Me.Loaded, Me.IsEnabledChanged
        Try
            If IsLoaded AndAlso AniControlEnabled = 0 AndAlso Not False.Equals(e) Then 'Prevent default property changes from triggering animations; e is False when animation is forcibly skipped

                Select Case ColorType
                    Case ColorState.Black
                        If IsMouseDown Then
                            'Pressed
                            AniStart(AaColor(Me, BackgroundProperty, "ColorBrush6", 70), "MyIconTextButton Color " & Uuid)
                        ElseIf IsMouseOver Then
                            'Hover
                            AniStart({
                                AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrush3", AnimationTimeOfMouseIn),
                                AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", AnimationTimeOfMouseIn)
                            }, "MyIconTextButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, "ColorBrushBg1", AnimationTimeOfMouseIn), "MyIconTextButton Color " & Uuid)
                        ElseIf IsEnabled Then
                            'Normal
                            AniStart({
                                AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrush1", AnimationTimeOfMouseOut),
                                AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush1", AnimationTimeOfMouseOut)
                            }, "MyIconTextButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, ColorSemiTransparent - Background, AnimationTimeOfMouseOut), "MyIconTextButton Color " & Uuid)
                        Else
                            'Disabled
                            AniStart({
                                AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrushGray5", 100),
                                AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrushGray5", 100)
                            }, "MyIconTextButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, ColorSemiTransparent - Background, AnimationTimeOfMouseOut), "MyIconTextButton Color " & Uuid)
                        End If
                    Case ColorState.Highlight
                        If IsMouseDown Then
                            'Pressed
                            AniStart(AaColor(Me, BackgroundProperty, "ColorBrush6", 70), "MyIconTextButton Color " & Uuid)
                        ElseIf IsMouseOver Then
                            'Hover
                            AniStart({
                                AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrush3", AnimationTimeOfMouseIn),
                                AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", AnimationTimeOfMouseIn)
                            }, "MyIconTextButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, "ColorBrushBg1", AnimationTimeOfMouseIn), "MyIconTextButton Color " & Uuid)
                        ElseIf IsEnabled Then
                            'Normal
                            AniStart({
                                AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrush3", AnimationTimeOfMouseOut),
                                AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", AnimationTimeOfMouseOut)
                            }, "MyIconTextButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, ColorSemiTransparent - Background, AnimationTimeOfMouseOut), "MyIconTextButton Color " & Uuid)
                        Else
                            'Disabled
                            AniStart({
                                AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrushGray5", 100),
                                AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrushGray5", 100)
                            }, "MyIconTextButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, ColorSemiTransparent - Background, AnimationTimeOfMouseOut), "MyIconTextButton Color " & Uuid)
                        End If
                End Select

            Else

                'Without animation
                AniStop("MyIconTextButton Checked " & Uuid)
                AniStop("MyIconTextButton Color " & Uuid)
                Select Case ColorType
                    Case ColorState.Black
                        Background = ColorSemiTransparent
                        ShapeLogo.SetResourceReference(Shapes.Path.FillProperty, If(IsEnabled, "ColorBrush1", "ColorBrushGray5"))
                        LabText.SetResourceReference(TextBlock.ForegroundProperty, If(IsEnabled, "ColorBrush1", "ColorBrushGray5"))
                    Case ColorState.Highlight
                        Background = ColorSemiTransparent
                        ShapeLogo.SetResourceReference(Shapes.Path.FillProperty, If(IsEnabled, "ColorBrush3", "ColorBrushGray5"))
                        LabText.SetResourceReference(TextBlock.ForegroundProperty, If(IsEnabled, "ColorBrush3", "ColorBrushGray5"))
                End Select

            End If
        Catch ex As Exception
            Logger.Warn(ex, "刷新带图标按钮颜色出错")
        End Try
    End Sub

End Class
