Imports System.Windows.Markup

<ContentProperty("Inlines")>
Public Class MyCheckBox
    Implements ISettingControl

    'Basics

    Public Uuid As Integer = GetUuid()
    ''' <summary>
    ''' Raised when the checked state of the check box changes.
    ''' </summary>
    ''' <param name="user">Whether the checked state was changed manually by the user.</param>
    Public Event Change(sender As Object, user As Boolean)
    Public Event PreviewChange(sender As Object, e As RouteEventArgs)

    'Custom properties
    Public Property Checked As Boolean
        Get
            Return GetValue(CheckedProperty)
        End Get
        Set(value As Boolean)
            SetChecked(value, False)
        End Set
    End Property
    Public Shared ReadOnly CheckedProperty As DependencyProperty =
        DependencyProperty.Register("Checked", GetType(Boolean), GetType(MyCheckBox), New PropertyMetadata(False,
        Sub(d As MyCheckBox, e As DependencyPropertyChangedEventArgs)
            'Setting Checked in XAML does not call Checked_Set, so the UI change has to be triggered manually here
            If Not d.IsLoaded Then d.SyncUI()
        End Sub))

    Private Const AnimationTimeOfCheck As Integer = 150 'Duration of the checked state change animation
    ''' <summary>
    ''' Manually sets the Checked property.
    ''' </summary>
    ''' <param name="value">The new Checked value.</param>
    ''' <param name="user">Whether the change was caused by the user.</param>
    Public Sub SetChecked(value As Boolean, user As Boolean)
        Try
            If value = Checked Then Return

            'Preview event
            If value AndAlso user Then
                Dim e = New RouteEventArgs(user)
                RaiseEvent PreviewChange(Me, e)
                If e.Handled Then
                    MouseDowned = True
                    Checkbox_MouseLeave()
                    MouseDowned = False
                    Return
                End If
            End If

            SetValue(CheckedProperty, value)
            If IsLoaded Then RaiseEvent Change(Me, user)

            'Change animation
            SyncUI()
            RaiseCustomEvent()
        Catch ex As Exception
            Logger.Warn(ex, "设置 Checked 失败")
        End Try
    End Sub
    Private Sub SyncUI()
        If AniControlEnabled = 0 AndAlso IsLoaded Then 'Prevent default property changes from triggering animations
            AllowMouseDown = False
            If Checked Then
                'Unchecked to checked
                AniStart({
                      AaScale(ShapeBorder, 12 - ShapeBorder.Width, AnimationTimeOfCheck, , New AniEaseOutFluent, , True),
                      AaScaleTransform(ShapeCheck, 1 - CType(ShapeCheck.RenderTransform, ScaleTransform).ScaleX, AnimationTimeOfCheck * 2, AnimationTimeOfCheck * 0.7, New AniEaseOutBack(AniEasePower.Weak)),
                      AaScale(ShapeBorder, 6, AnimationTimeOfCheck * 2, AnimationTimeOfCheck * 0.7, New AniEaseOutBack, , True)
                 }, "MyCheckBox Scale " & Uuid)
                AniStart({
                      AaColor(ShapeBorder, Border.BorderBrushProperty, If(IsEnabled, If(IsMouseOver, "ColorBrush3", "ColorBrush2"), "ColorBrushGray5"), AnimationTimeOfCheck)
                 }, "MyCheckBox BorderColor " & Uuid)
                AniStart({
                      AaCode(Sub() AllowMouseDown = True, AnimationTimeOfCheck * 2)
                 }, "MyCheckBox AllowMouseDown " & Uuid)
            Else
                'Checked to unchecked
                AniStart({
                      AaScale(ShapeBorder, 12 - ShapeBorder.Width, AnimationTimeOfCheck, , New AniEaseOutFluent, , True),
                      AaScaleTransform(ShapeCheck, -CType(ShapeCheck.RenderTransform, ScaleTransform).ScaleX, AnimationTimeOfCheck * 0.9, , New AniEaseInFluent(AniEasePower.Weak)),
                      AaScale(ShapeBorder, 6, AnimationTimeOfCheck * 2, AnimationTimeOfCheck * 0.7, New AniEaseOutBack, , True)
                 }, "MyCheckBox Scale " & Uuid)
                AniStart({
                      AaColor(ShapeBorder, Border.BorderBrushProperty, If(IsEnabled, If(IsMouseOver, "ColorBrush3", "ColorBrush1"), "ColorBrushGray5"), AnimationTimeOfCheck)
                 }, "MyCheckBox BorderColor " & Uuid)
                AniStart({
                      AaCode(Sub() AllowMouseDown = True, AnimationTimeOfCheck * 2)
                 }, "MyCheckBox AllowMouseDown " & Uuid)
            End If
        Else
            'Without animation
            AniStop("MyCheckBox Scale " & Uuid)
            AniStop("MyCheckBox BorderColor " & Uuid)
            AniStop("MyCheckBox AllowMouseDown " & Uuid)
            If Checked Then
                CType(ShapeCheck.RenderTransform, ScaleTransform).ScaleX = 1
                CType(ShapeCheck.RenderTransform, ScaleTransform).ScaleY = 1
                ShapeBorder.SetResourceReference(Border.BorderBrushProperty, If(IsEnabled, "ColorBrush2", "ColorBrushGray5"))
            Else
                CType(ShapeCheck.RenderTransform, ScaleTransform).ScaleX = 0
                CType(ShapeCheck.RenderTransform, ScaleTransform).ScaleY = 0
                ShapeBorder.SetResourceReference(Border.BorderBrushProperty, If(IsEnabled, "ColorBrush1", "ColorBrushGray5"))
            End If
        End If
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
    Public Shared ReadOnly TextProperty As DependencyProperty = DependencyProperty.Register("Text", GetType(String), GetType(MyCheckBox), New PropertyMetadata(New PropertyChangedCallback(
    Sub(sender As DependencyObject, e As DependencyPropertyChangedEventArgs)
        If Not IsNothing(sender) Then CType(sender, MyCheckBox).LabText.Text = e.NewValue
    End Sub)))

    'Click event

    Private MouseDowned As Boolean = False
    Private AllowMouseDown As Boolean = True
    Private Sub Checkbox_MouseUp() Handles Me.MouseLeftButtonUp
        If Not MouseDowned Then Return
        MouseDowned = False
        SetChecked(Not Checked, True)
        AniStart(AaColor(ShapeBorder, Border.BackgroundProperty, "ColorBrushHalfWhite", 100), "MyCheckBox Background " & Uuid)
    End Sub
    Private Sub Checkbox_MouseDown() Handles Me.MouseLeftButtonDown
        If Not AllowMouseDown Then Return
        MouseDowned = True
        Focus()
        AniStart(AaColor(ShapeBorder, Border.BackgroundProperty, "ColorBrushBg1", 100), "MyCheckBox Background " & Uuid)
        If Checked Then
            AniStart({
                     AaScale(ShapeBorder, 16.5 - ShapeBorder.Width, 1000, , New AniEaseOutFluent(AniEasePower.Strong), Absolute:=True),
                     AaScaleTransform(ShapeCheck, 0.9 - CType(ShapeCheck.RenderTransform, ScaleTransform).ScaleX, 1000, , New AniEaseOutFluent(AniEasePower.Strong))
                 }, "MyCheckBox Scale " & Uuid)
        Else
            AniStart(AaScale(ShapeBorder, 16.5 - ShapeBorder.Width, 1000, , New AniEaseOutFluent(AniEasePower.Strong), Absolute:=True), "MyCheckBox Scale " & Uuid)
        End If
    End Sub
    Private Sub Checkbox_MouseLeave() Handles Me.MouseLeave
        If Not MouseDowned Then Return
        MouseDowned = False
        AniStart(AaColor(ShapeBorder, Border.BackgroundProperty, "ColorBrushHalfWhite", 100), "MyCheckBox Background " & Uuid)
        If Checked Then
            AniStart({
                     AaScale(ShapeBorder, 18 - ShapeBorder.Width, 400, , New AniEaseOutFluent(AniEasePower.Strong), Absolute:=True),
                     AaScaleTransform(ShapeCheck, 1 - CType(ShapeCheck.RenderTransform, ScaleTransform).ScaleX, 500, , New AniEaseOutFluent(AniEasePower.Strong))
                 }, "MyCheckBox Scale " & Uuid)
        Else
            AniStart(AaScale(ShapeBorder, 18 - ShapeBorder.Width, 400, , New AniEaseOutFluent(AniEasePower.Strong), Absolute:=True), "MyCheckBox Scale " & Uuid)
        End If
    End Sub

    'Hover animation

    Private Const AnimationTimeOfMouseIn As Integer = 100
    Private Const AnimationTimeOfMouseOut As Integer = 200
    Private Sub Checkbox_IsEnabledChanged() Handles Me.IsEnabledChanged
        If IsLoaded AndAlso AniControlEnabled = 0 Then 'Prevent default property changes from triggering animations
            'With animation
            If IsEnabled Then
                'Enabled
                Checkbox_MouseLeaveAnimation()
            Else
                'Disabled
                AniStart({
                         AaColor(ShapeBorder, Border.BorderBrushProperty, ColorGray5 - ShapeBorder.BorderBrush, AnimationTimeOfMouseOut)
                 }, "MyCheckBox BorderColor " & Uuid)
                AniStart({
                         AaColor(LabText, TextBlock.ForegroundProperty, ColorGray5 - LabText.Foreground, AnimationTimeOfMouseOut)
                 }, "MyCheckBox TextColor " & Uuid)
            End If
        Else
            'Without animation
            AniStop("MyCheckBox TextColor " & Uuid)
            AniStop("MyCheckBox BorderColor " & Uuid)
            LabText.SetResourceReference(TextBlock.ForegroundProperty, If(Me.IsEnabled, "ColorBrush1", "ColorBrushGray5"))
            ShapeBorder.SetResourceReference(Border.BorderBrushProperty, If(Me.IsEnabled, If(Checked, "ColorBrush2", "ColorBrush1"), "ColorBrushGray5"))
        End If
    End Sub
    Private Sub Checkbox_MouseEnterAnimation() Handles Me.MouseEnter
        AniStart({
                 AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", AnimationTimeOfMouseIn)
         }, "MyCheckBox TextColor " & Uuid)
        AniStart({
                 AaColor(ShapeBorder, Border.BorderBrushProperty, "ColorBrush3", AnimationTimeOfMouseIn)
         }, "MyCheckBox BorderColor " & Uuid)
    End Sub
    Private Sub Checkbox_MouseLeaveAnimation() Handles Me.MouseLeave
        If Not IsEnabled Then Return 'MouseLeave runs after IsEnabledChanged, so if a custom event changes IsEnabled the display would be wrong
        AniStart({
                 AaColor(LabText, TextBlock.ForegroundProperty, If(Me.IsEnabled, "ColorBrush1", "ColorBrushGray5"), AnimationTimeOfMouseOut)
         }, "MyCheckBox TextColor " & Uuid)
        AniStart({
                 AaColor(ShapeBorder, Border.BorderBrushProperty, If(Me.IsEnabled, If(Checked, "ColorBrush2", "ColorBrush1"), "ColorBrushGray5"), AnimationTimeOfMouseOut)
         }, "MyCheckBox BorderColor " & Uuid)
    End Sub

#Region "Settings"

    Private Sub RefreshSetting(NewValue As String) Implements ISettingControl.RefreshSetting
        Checked = Boolean.Parse(NewValue)
    End Sub

    Private Function GetCurrentSetting() As String Implements ISettingControl.GetCurrentSetting
        Return Checked.ToString
    End Function

    Private Sub SaveSetting() Handles Me.Change
        SettingService.SaveSetting(Me)
    End Sub

#End Region

End Class
