Imports System.Windows.Markup

<ContentProperty("Inlines")>
Public Class MyRadioButton

    'Basics

    Public Uuid As Integer = GetUuid()
    Public Event Check(sender As Object, raiseByMouse As Boolean)

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

    Private _Checked As Boolean = False 'Whether checked
    Public Property Checked As Boolean
        Get
            Return _Checked
        End Get
        Set(value As Boolean)
            SetChecked(value, False, True)
        End Set
    End Property
    ''' <summary>
    ''' Manually sets the Checked property.
    ''' </summary>
    ''' <param name="value">The new Checked value.</param>
    ''' <param name="raiseByMouse">Whether the change was caused by the user.</param>
    ''' <param name="anime">Whether to play the animation.</param>
    Public Sub SetChecked(value As Boolean, raiseByMouse As Boolean, anime As Boolean)
        Try

            'Custom property basics

            Dim IsChanged As Boolean = False
            If _Checked <> value Then
                _Checked = value
                IsChanged = True
            End If

            'Make sure only one radio button is checked

            If Parent Is Nothing Then Return
            Dim RadioboxList As New List(Of MyRadioButton)
            Dim CheckedCount As Integer = 0
            'Collect the control list and the checked count
            For Each Control In CType(Parent, Object).Children
                If TypeOf Control Is MyRadioButton Then
                    RadioboxList.Add(Control)
                    If Control.Checked Then CheckedCount += 1
                End If
            Next
            'Check the selection state
            Select Case CheckedCount
                Case 0
                    'No radio button is checked, check the first one
                    RadioboxList(0).Checked = True
                Case Is > 1
                    'More than one item is checked
                    If Me.Checked Then
                        'If this control is checked, uncheck all the others
                        For Each Control As MyRadioButton In RadioboxList
                            If Control.Checked AndAlso Not Control.Equals(Me) Then Control.Checked = False
                        Next
                    Else
                        'If this control is not checked, keep only the first checked control
                        Dim FirstChecked = False
                        For Each Control As MyRadioButton In RadioboxList
                            If Control.Checked Then
                                If FirstChecked Then
                                    Control.Checked = False 'Setting Checked raises the Change event automatically, so no need to raise it again
                                Else
                                    FirstChecked = True
                                End If
                            End If
                        Next
                    End If
            End Select

            'Change animation

            If Not IsChanged Then Return
            RefreshColor(Nothing, anime)

            'Raise the event
            If Checked Then RaiseEvent Check(Me, raiseByMouse)
            RaiseCustomEvent()

        Catch ex As Exception
            Logger.Error(ex, "单选按钮勾选改变错误", LogBehavior.Toast)
        End Try
    End Sub

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
    Public Shared ReadOnly TextProperty As DependencyProperty = DependencyProperty.Register("Text", GetType(String), GetType(MyRadioButton), New PropertyMetadata(New PropertyChangedCallback(
    Sub(sender, e) If sender IsNot Nothing Then CType(sender, MyRadioButton).LabText.Text = e.NewValue)))
    Public Enum ColorState
        White
        Highlight
    End Enum
    Private _ColorType As ColorState = ColorState.White
    Public Property ColorType As ColorState
        Get
            Return _ColorType
        End Get
        Set(value As ColorState)
            _ColorType = value
            RefreshColor()
        End Set
    End Property 'Color type

    'Click event

    Public Event PreviewClick(sender As Object, e As RouteEventArgs)
    Private IsMouseDown As Boolean = False
    Private Sub Radiobox_MouseUp() Handles Me.MouseLeftButtonUp
        If Checked Then Return
        If Not IsMouseDown Then Return
        IsMouseDown = False
        Dim e As New RouteEventArgs(True)
        RaiseEvent PreviewClick(Me, e)
        If e.Handled Then Return
        SetChecked(True, True, True)
    End Sub
    Private Sub Radiobox_MouseDown() Handles Me.MouseLeftButtonDown
        If Checked Then Return
        IsMouseDown = True
        RefreshColor()
    End Sub
    Private Sub Radiobox_MouseLeave() Handles Me.MouseLeave
        IsMouseDown = False
    End Sub

    'Animation

    Private Const AnimationTimeOfMouseIn As Integer = 90 'Mouse hover animation duration
    Private Const AnimationTimeOfMouseOut As Integer = 150 'Mouse leave animation duration
    Private Const AnimationTimeOfCheck As Integer = 120 'Duration of the checked state change animation
    Private Sub RefreshColor(Optional obj = Nothing, Optional e = Nothing) Handles Me.MouseEnter, Me.MouseLeave, Me.Loaded
        Try
            If IsLoaded AndAlso AniControlEnabled = 0 AndAlso Not False.Equals(e) Then 'Prevent default property changes from triggering animations; e is False when animation is forcibly skipped

                Select Case ColorType
                    Case ColorState.White
                        If Checked Then
                            'Checked
                            AniStart({
                                         AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrush3", AnimationTimeOfCheck),
                                         AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", AnimationTimeOfCheck)
                                    }, "MyRadioButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, New MyColor(255, 255, 255) - Background, AnimationTimeOfCheck), "MyRadioButton Color " & Uuid)
                        ElseIf IsMouseDown Then
                            'Pressed
                            AniStart(AaColor(Me, BackgroundProperty, New MyColor(120, Color8) - Background, 60), "MyRadioButton Color " & Uuid)
                        ElseIf IsMouseOver Then
                            'Hover
                            AniStart({
                                 AaColor(ShapeLogo, Shapes.Path.FillProperty, New MyColor(255, 255, 255) - ShapeLogo.Fill, AnimationTimeOfMouseIn),
                                 AaColor(LabText, TextBlock.ForegroundProperty, New MyColor(255, 255, 255) - LabText.Foreground, AnimationTimeOfMouseIn)
                            }, "MyRadioButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, New MyColor(50, Color8) - Background, AnimationTimeOfMouseIn), "MyRadioButton Color " & Uuid)
                        Else
                            'Normal
                            AniStart({
                                 AaColor(ShapeLogo, Shapes.Path.FillProperty, New MyColor(255, 255, 255) - ShapeLogo.Fill, AnimationTimeOfMouseOut),
                                 AaColor(LabText, TextBlock.ForegroundProperty, New MyColor(255, 255, 255) - LabText.Foreground, AnimationTimeOfMouseOut)
                            }, "MyRadioButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, ColorSemiTransparent - Background, AnimationTimeOfMouseOut), "MyRadioButton Color " & Uuid)
                        End If
                    Case ColorState.Highlight
                        If Checked Then
                            'Checked
                            AniStart({
                                         AaColor(ShapeLogo, Shapes.Path.FillProperty, New MyColor(255, 255, 255) - ShapeLogo.Fill, AnimationTimeOfCheck),
                                         AaColor(LabText, TextBlock.ForegroundProperty, New MyColor(255, 255, 255) - LabText.Foreground, AnimationTimeOfCheck)
                                    }, "MyRadioButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, "ColorBrush3", AnimationTimeOfCheck), "MyRadioButton Color " & Uuid)
                        ElseIf IsMouseDown Then
                            'Pressed
                            AniStart(AaColor(Me, BackgroundProperty, "ColorBrush6", AnimationTimeOfMouseIn), "MyRadioButton Color " & Uuid)
                        ElseIf IsMouseOver Then
                            'Hover
                            AniStart({
                                         AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrush3", AnimationTimeOfMouseIn),
                                         AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", AnimationTimeOfMouseIn)
                                    }, "MyRadioButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, "ColorBrush7", AnimationTimeOfMouseIn), "MyRadioButton Color " & Uuid)
                        Else
                            'Normal
                            AniStart({
                                         AaColor(ShapeLogo, Shapes.Path.FillProperty, "ColorBrush3", AnimationTimeOfMouseOut),
                                         AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", AnimationTimeOfMouseOut)
                                    }, "MyRadioButton Checked " & Uuid)
                            AniStart(AaColor(Me, BackgroundProperty, ColorSemiTransparent - Background, AnimationTimeOfMouseOut), "MyRadioButton Color " & Uuid)
                        End If
                End Select

            Else

                'Without animation
                AniStop("MyRadioButton Checked " & Uuid)
                AniStop("MyRadioButton Color " & Uuid)
                Select Case ColorType
                    Case ColorState.White
                        If Checked Then
                            Background = New MyColor(255, 255, 255)
                            ShapeLogo.SetResourceReference(Shapes.Path.FillProperty, "ColorBrush3")
                            LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3")
                        Else
                            Background = ColorSemiTransparent
                            ShapeLogo.Fill = New MyColor(255, 255, 255)
                            LabText.Foreground = New MyColor(255, 255, 255)
                        End If
                    Case ColorState.Highlight
                        If Checked Then
                            SetResourceReference(BackgroundProperty, "ColorBrush3")
                            ShapeLogo.Fill = New MyColor(255, 255, 255)
                            LabText.Foreground = New MyColor(255, 255, 255)
                        Else
                            Background = ColorSemiTransparent
                            ShapeLogo.SetResourceReference(Shapes.Path.FillProperty, "ColorBrush3")
                            LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3")
                        End If
                End Select

            End If
        Catch ex As Exception
            Logger.Warn(ex, "刷新单选按钮颜色出错")
        End Try
    End Sub

End Class
