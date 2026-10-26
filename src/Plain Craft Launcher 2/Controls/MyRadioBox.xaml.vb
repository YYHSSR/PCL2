Imports System.Windows.Markup
Imports System.Windows.Shapes

<ContentProperty("Inlines")>
Public Class MyRadioBox
    Implements IMyRadio, ISettingControl

    'Basics

    Public Uuid As Integer = GetUuid()
    Public Event PreviewCheck(sender As Object, e As RouteEventArgs)
    Public Event PreviewChange(sender As Object, e As RouteEventArgs)
    Public Event Check(sender As Object, e As RouteEventArgs) Implements IMyRadio.Check
    Public Event Changed(sender As Object, e As RouteEventArgs) Implements IMyRadio.Changed

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
        DependencyProperty.Register("Checked", GetType(Boolean), GetType(MyRadioBox), New PropertyMetadata(False,
        Sub(d As MyRadioBox, e As DependencyPropertyChangedEventArgs)
            'Setting Checked in XAML does not call Checked_Set, so the UI change has to be triggered manually here
            If Not d.IsLoaded Then d.SyncUI()
        End Sub))

    ''' <summary>
    ''' Manually sets the Checked property.
    ''' </summary>
    ''' <param name="value">The new Checked value.</param>
    ''' <param name="user">Whether the change was caused by the user.</param>
    Public Sub SetChecked(value As Boolean, user As Boolean)
        Try
            'Preview event
            If value AndAlso user Then
                Dim e = New RouteEventArgs(user)
                RaiseEvent PreviewCheck(Me, e)
                If e.Handled Then
                    Radiobox_MouseLeave()
                    Return
                End If
            End If

            'Custom property basics
            Dim IsChanged As Boolean = False
            If IsLoaded AndAlso Not value = Checked Then RaiseEvent PreviewChange(Me, New RouteEventArgs(user))
            If value <> Checked Then
                SetValue(CheckedProperty, value)
                IsChanged = True
            End If

            'Make sure at most one radio box is checked
            If Parent Is Nothing Then Return
            Dim RadioboxList As New List(Of MyRadioBox)
            Dim CheckedCount As Integer = 0
            For Each Control In CType(Parent, Object).Children 'Collect the control list and the checked count
                If TypeOf Control Is MyRadioBox Then
                    RadioboxList.Add(Control)
                    If Control.Checked Then CheckedCount += 1
                End If
            Next
            If CheckedCount > 1 Then
                If Checked Then
                    'If this control is checked, uncheck all the others
                    For Each Control As MyRadioBox In RadioboxList
                        If Control.Checked AndAlso Not Control.Equals(Me) Then Control.Checked = False
                    Next
                Else
                    'If this control is not checked, keep only the first checked control
                    Dim FirstChecked = False
                    For Each Control As MyRadioBox In RadioboxList
                        If Control.Checked Then
                            If FirstChecked Then
                                Control.Checked = False 'Setting Checked raises the Change event automatically, so no need to raise it again
                            Else
                                FirstChecked = True
                            End If
                        End If
                    Next
                End If
            End If

            If IsChanged Then
                'Raise the event
                If Checked Then RaiseEvent Check(Me, New RouteEventArgs(user))
                RaiseEvent Changed(Me, New RouteEventArgs(user))
                RaiseCustomEvent()
                'Change animation
                SyncUI()
            End If

        Catch ex As Exception
            Logger.Error(ex, "单选框勾选改变错误", LogBehavior.Toast)
        End Try
    End Sub
    Private Sub SyncUI()
        If AniControlEnabled = 0 AndAlso IsLoaded Then 'Prevent default property changes from triggering animations
            If Checked Then
                'Unchecked to checked
                If ShapeDot.Opacity < 0.01 Then ShapeDot.Opacity = 1
                AniStart({
                          AaScale(ShapeBorder, 10 - ShapeBorder.Width, AnimationTimeOfCheck, , New AniEaseOutFluent(AniEasePower.Weak), , True),
                          AaScale(ShapeBorder, 8, AnimationTimeOfCheck * 2, AnimationTimeOfCheck * 0.6, New AniEaseOutBack, , True)
                     }, "MyRadioBox Border " & Uuid)
                AniStart({
                          AaScale(ShapeDot, 9 - ShapeDot.Width, AnimationTimeOfCheck * 2.6,, New AniEaseOutBack(AniEasePower.Weak), , True),
                          AaOpacity(ShapeDot, 1 - ShapeDot.Opacity, AnimationTimeOfCheck * 0.5, AnimationTimeOfCheck * 0.6)
                     }, "MyRadioBox Dot " & Uuid)
                AniStart({
                          AaColor(ShapeBorder, Ellipse.StrokeProperty, If(IsMouseOver, "ColorBrush3", If(IsEnabled, "ColorBrush2", "ColorBrushGray5")), AnimationTimeOfCheck)
                     }, "MyRadioBox BorderColor " & Uuid)
            Else
                'Checked to unchecked
                AniStart({
                          AaScale(ShapeBorder, 18 - ShapeBorder.Width, AnimationTimeOfCheck, , New AniEaseOutFluent, , True)
                     }, "MyRadioBox Border " & Uuid)
                AniStart({
                          AaScale(ShapeDot, -ShapeDot.Width, AnimationTimeOfCheck, , New AniEaseInFluent, , True),
                          AaOpacity(ShapeDot, -ShapeDot.Opacity, AnimationTimeOfCheck * 0.5, AnimationTimeOfCheck * 0.2)
                     }, "MyRadioBox Dot " & Uuid)
                AniStart({
                          AaColor(ShapeBorder, Ellipse.StrokeProperty, If(IsMouseOver, "ColorBrush3", If(IsEnabled, "ColorBrush1", "ColorBrushGray5")), AnimationTimeOfCheck)
                     }, "MyRadioBox BorderColor " & Uuid)
            End If
        Else
            'Without animation
            AniStop("MyRadioBox Border " & Uuid)
            AniStop("MyRadioBox Dot " & Uuid)
            AniStop("MyRadioBox BorderColor " & Uuid)
            If Checked Then
                ShapeDot.Width = 9
                ShapeDot.Height = 9
                ShapeDot.Opacity = 1
                ShapeDot.Margin = New Thickness(5.5, 0, 0, 0)
                ShapeBorder.SetResourceReference(Ellipse.StrokeProperty, If(IsEnabled, "ColorBrush2", "ColorBrushGray5"))
            Else
                ShapeDot.Width = 0
                ShapeDot.Height = 0
                ShapeDot.Opacity = 0
                ShapeDot.Margin = New Thickness(10, 0, 0, 0)
                ShapeBorder.SetResourceReference(Ellipse.StrokeProperty, If(IsEnabled, "ColorBrush1", "ColorBrushGray5"))
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
    Public Shared ReadOnly TextProperty As DependencyProperty = DependencyProperty.Register("Text", GetType(String), GetType(MyRadioBox), New PropertyMetadata(New PropertyChangedCallback(
    Sub(sender, e) If sender IsNot Nothing Then CType(sender, MyRadioBox).LabText.Text = e.NewValue)))

    'Click event

    Private MouseDowned As Boolean = False
    Private AllowMouseDown As Boolean = True
    Private Sub Radiobox_MouseUp() Handles Me.MouseLeftButtonUp
        If Not MouseDowned Then Return
        SetChecked(True, True)
        MouseDowned = False
        AniStart(AaColor(ShapeBorder, Ellipse.FillProperty, "ColorBrushHalfWhite", 100), "MyRadioBox Background " & Uuid)
    End Sub
    Private Sub Radiobox_MouseDown() Handles Me.MouseLeftButtonDown
        MouseDowned = True
        Focus()
        AniStart(AaColor(ShapeBorder, Ellipse.FillProperty, "ColorBrushBg1", 100), "MyRadioBox Background " & Uuid)
        If Not Checked Then
            AniStart(AaScale(ShapeBorder, 16.5 - ShapeBorder.Width, 1000, , New AniEaseOutFluent(AniEasePower.Strong), Absolute:=True), "MyRadioBox Border " & Uuid)
        End If
    End Sub
    Private Sub Radiobox_MouseLeave() Handles Me.MouseLeave
        If Not MouseDowned Then Return
        MouseDowned = False
        AniStart(AaColor(ShapeBorder, Ellipse.FillProperty, "ColorBrushHalfWhite", 100), "MyRadioBox Background " & Uuid)
        If Not Checked Then
            AniStart(AaScale(ShapeBorder, 18 - ShapeBorder.Width, 400, , New AniEaseOutFluent(AniEasePower.Strong), Absolute:=True), "MyRadioBox Border " & Uuid)
        End If
    End Sub

    'Hover animation

    Private Const AnimationTimeOfMouseIn As Integer = 100 'Mouse hover animation duration
    Private Const AnimationTimeOfMouseOut As Integer = 200 'Mouse leave animation duration
    Private Const AnimationTimeOfCheck As Integer = 150 'Duration of the checked state change animation
    Private Sub Radiobox_IsEnabledChanged() Handles Me.IsEnabledChanged
        If Me.IsLoaded AndAlso AniControlEnabled = 0 Then 'Prevent default property changes from triggering animations
            'With animation
            If Me.IsEnabled Then
                'Enabled
                Radiobox_MouseLeaveAnimation()
            Else
                'Disabled
                AniStart(AaColor(ShapeBorder, Ellipse.StrokeProperty, ColorGray5 - ShapeBorder.Stroke, AnimationTimeOfMouseOut), "MyRadioBox BorderColor " & Uuid)
                AniStart(AaColor(LabText, TextBlock.ForegroundProperty, ColorGray5 - LabText.Foreground, AnimationTimeOfMouseOut), "MyRadioBox TextColor " & Uuid)
            End If
        Else
            'Without animation
            AniStop("MyRadioBox BorderColor " & Uuid)
            AniStop("MyRadioBox TextColor " & Uuid)
            LabText.SetResourceReference(TextBlock.ForegroundProperty, If(Me.IsEnabled, "ColorBrush1", "ColorBrushGray5"))
            ShapeBorder.SetResourceReference(Ellipse.StrokeProperty, If(Me.IsEnabled, If(Checked, "ColorBrush2", "ColorBrush1"), "ColorBrushGray5"))
        End If
    End Sub
    Private Sub Radiobox_MouseEnterAnimation() Handles Me.MouseEnter
        AniStart(AaColor(ShapeBorder, Ellipse.StrokeProperty, "ColorBrush3", AnimationTimeOfMouseIn), "MyRadioBox BorderColor " & Uuid)
        AniStart(AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", AnimationTimeOfMouseIn), "MyRadioBox TextColor " & Uuid)
    End Sub
    Private Sub Radiobox_MouseLeaveAnimation() Handles Me.MouseLeave
        If Not Me.IsEnabled Then Return 'MouseLeave runs after IsEnabledChanged, so if a custom event changes IsEnabled the display would be wrong
        If IsLoaded AndAlso AniControlEnabled = 0 Then
            AniStart(AaColor(ShapeBorder, Ellipse.StrokeProperty, If(IsEnabled, If(Checked, "ColorBrush2", "ColorBrush1"), "ColorBrushGray5"), AnimationTimeOfMouseOut), "MyRadioBox BorderColor " & Uuid)
            AniStart(AaColor(LabText, TextBlock.ForegroundProperty, If(IsEnabled, "ColorBrush1", "ColorBrushGray5"), AnimationTimeOfMouseOut), "MyRadioBox TextColor " & Uuid)
        Else
            AniStop("MyRadioBox BorderColor " & Uuid)
            AniStop("MyRadioBox TextColor " & Uuid)
            ShapeBorder.SetResourceReference(Ellipse.StrokeProperty, If(IsEnabled, If(Checked, "ColorBrush2", "ColorBrush1"), "ColorBrushGray5"))
            LabText.SetResourceReference(TextBlock.ForegroundProperty, If(IsEnabled, "ColorBrush1", "ColorBrushGray5"))
        End If
    End Sub

#Region "Settings"

    Private Sub RefreshSetting(NewValue As String) Implements ISettingControl.RefreshSetting
        Checked = NewValue = SettingService.GetValue(Me)
    End Sub

    Private Function GetCurrentSetting() As String Implements ISettingControl.GetCurrentSetting
        Return If(Checked, SettingService.GetValue(Me), Nothing)
    End Function

    Private Sub SaveSetting() Handles Me.Check
        SettingService.SaveSetting(Me)
    End Sub

#End Region

End Class
