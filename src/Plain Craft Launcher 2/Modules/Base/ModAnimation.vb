'Animation engine module
'Methods and properties start with Ani, and functions creating a single animation object start with Aa (for easier auto-completion)

Imports System.Windows.Media.Effects

Public Module ModAnimation

#Region "Declarations"

    Public Const AniSpeed As Double = 1
    ''' <summary>
    ''' List of animation groups.
    ''' </summary>
    Public AniGroups As New Dictionary(Of String, AniGroupEntry)
    Public Class AniGroupEntry
        Public Data As List(Of AniData)
        Public StartTick As Long
        Public Uuid As Integer = GetUuid()
    End Class
    ''' <summary>
    ''' Time of the last tick.
    ''' </summary>
    Private AniLastTick As Long
    ''' <summary>
    ''' Whether the animation module is running.
    ''' </summary>
    Public AniRunning As Boolean = False
    Private _AniControlEnabled As Integer = 0
    Private ReadOnly AniControlEnabledLock As New Object
    ''' <summary>
    ''' Whether control animations are enabled. +1 first, then -1.
    ''' </summary>
    Public Property AniControlEnabled() As Integer
        Get
            Return _AniControlEnabled
        End Get
        Set(value As Integer)
            SyncLock AniControlEnabledLock
                _AniControlEnabled = value
            End SyncLock
        End Set
    End Property

#End Region

#Region "Classes and Enums"

    ''' <summary>
    ''' A single animation object.
    ''' </summary>
    Public Structure AniData

        ''' <summary>
        ''' Animation type.
        ''' </summary>
        Public TypeMain As AniType
        ''' <summary>
        ''' Animation subtype.
        ''' </summary>
        Public TypeSub As AniTypeSub

        ''' <summary>
        ''' Total length of the animation.
        ''' </summary>
        Public TimeTotal As Integer
        ''' <summary>
        ''' Length of the animation already run. Negative means delay.
        ''' </summary>
        Public TimeFinished As Double
        ''' <summary>
        ''' Percentage completed.
        ''' </summary>
        Public TimePercent As Double

        ''' <summary>
        ''' Whether this is an "after" animation.
        ''' </summary>
        Public IsAfter As Boolean

        ''' <summary>
        ''' Easing function type.
        ''' </summary>
        Public Ease As AniEase
        ''' <summary>
        ''' The animated object.
        ''' </summary>
        Public Obj As Object
        ''' <summary>
        ''' Animation value.
        ''' </summary>
        Public Value As Object
        ''' <summary>
        ''' Animation value at the last run.
        ''' </summary>
        Public ValueLast As Object

        Public Overrides Function ToString() As String
            Return TypeMain.ToString() & " | " & TimeFinished & "/" & TimeTotal & "(" & Math.Round(TimePercent * 100) & "%)" & If(Obj Is Nothing, "", " | " & Obj.ToString & "(" & Obj.GetType.Name & ")")
        End Function

    End Structure

    ''' <summary>
    ''' Base animation types.
    ''' </summary>
    Public Enum AniType
        ''' <summary>
        ''' Animation of a single Double, including position, size, opacity etc. Requires a subtype.
        ''' </summary>
        Number
        ''' <summary>
        ''' Animation of a color property. Requires a subtype.
        ''' </summary>
        Color
        ''' <summary>
        ''' Scales the control size. More efficient than 4 DoubleAnimations.
        ''' </summary>
        Scale
        ''' <summary>
        ''' Text appears character by character.
        ''' </summary>
        TextAppear
        ''' <summary>
        ''' Runs code.
        ''' </summary>
        Code
        ''' <summary>
        ''' Scales the control the WPF way.
        ''' </summary>
        ScaleTransform
        ''' <summary>
        ''' Rotates the control the WPF way.
        ''' </summary>
        RotateTransform
    End Enum
    ''' <summary>
    ''' Extended animation types.
    ''' </summary>
    Public Enum AniTypeSub
        X
        Y
        Width
        Height
        Opacity
        Value
        Radius
        BorderThickness
        StrokeThickness
        TranslateX
        TranslateY
        [Double]
        DoubleParam
        GridLengthWidth
    End Enum

#End Region

#Region "Types"

    'DoubleAnimation

    ''' <summary>
    ''' Animation moving along the X axis.
    ''' </summary>
    ''' <param name="Obj">The animated object.</param>
    ''' <param name="Value">The amount to move.</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaX(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.X,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' Animation moving along the Y axis.
    ''' </summary>
    ''' <param name="Obj">The animated object.</param>
    ''' <param name="Value">The amount to move.</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaY(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.Y,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' Animation changing the width.
    ''' </summary>
    ''' <param name="Obj">The animated object.</param>
    ''' <param name="Value">The amount to change the width by.</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaWidth(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.Width,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' Animation changing the height.
    ''' </summary>
    ''' <param name="Obj">The animated object.</param>
    ''' <param name="Value">The amount to change the height by.</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaHeight(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.Height,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' Animation changing the opacity.
    ''' </summary>
    ''' <param name="Obj">The animated object.</param>
    ''' <param name="Value">The amount to change the opacity by.</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaOpacity(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.Opacity,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' Animation changing the object's Value property.
    ''' </summary>
    ''' <param name="Obj">The animated object.</param>
    ''' <param name="Value">The amount to change the Value property by.</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaValue(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.Value,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' Animation changing the object's Radius property.
    ''' </summary>
    ''' <param name="Obj">The animated object.</param>
    ''' <param name="Value">The amount to change the Radius property by.</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaRadius(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.Radius,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' Animation changing the object's BorderThickness property.
    ''' </summary>
    ''' <param name="Obj">The animated object.</param>
    ''' <param name="Value">The amount to change the BorderThickness property by.</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaBorderThickness(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.BorderThickness,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' Animation changing the object's StrokeThickness property.
    ''' </summary>
    ''' <param name="Obj">The animated object.</param>
    ''' <param name="Value">The amount to change the StrokeThickness property by.</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaStrokeThickness(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.StrokeThickness,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' Animation changing the GridLength of Width. Must be Star.
    ''' </summary>
    ''' <param name="Obj">The animated object.</param>
    ''' <param name="Value">The amount to change GridLength.Value by.</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaGridLengthWidth(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.GridLengthWidth,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function

    'DoubleAnimation（Obj, Prop, [Res]）

    ''' <summary>
    ''' Animation changing a numeric property.
    ''' </summary>
    ''' <param name="Obj">The animated object.</param>
    ''' <param name="Prop">The dependency property to animate.</param>
    ''' <param name="Value">The amount to change by.</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaDouble(Obj As Object, Prop As DependencyProperty, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.Double, .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = {Obj, Prop, ""}, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' Gets a numeric animation value.
    ''' </summary>
    ''' <param name="Value">The amount to change by.</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaDouble(Lambda As ParameterizedThreadStart, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.DoubleParam, .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Lambda, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function

    'ColorAnimation（Obj, Prop, [Res]）

    ''' <summary>
    ''' Animation changing a color property.
    ''' </summary>
    ''' <param name="Obj">The animated object.</param>
    ''' <param name="Prop">The dependency property to animate.</param>
    ''' <param name="Value">The amount to change the color by, computed with RGB addition/subtraction. Overflow is not a concern.</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaColor(Obj As FrameworkElement, Prop As DependencyProperty, Value As MyColor, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Color, .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = {Obj, Prop, ""}, .Value = Value, .IsAfter = After, .TimeFinished = -Delay, .ValueLast = New MyColor(0, 0, 0, 0)}
    End Function
    ''' <summary>
    ''' Animation changing a color property to a resource.
    ''' </summary>
    ''' <param name="Obj">The animated object.</param>
    ''' <param name="Prop">The dependency property to animate.</param>
    ''' <param name="Res">The resource whose value the color changes to.</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaColor(Obj As FrameworkElement, Prop As DependencyProperty, Res As String, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Color, .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = {Obj, Prop, Res}, .Value = New MyColor(Application.Current.FindResource(Res)) - New MyColor(Obj.GetValue(Prop)), .IsAfter = After, .TimeFinished = -Delay, .ValueLast = New MyColor(0, 0, 0, 0)}
    End Function

    'Scale

    ''' <summary>
    ''' Animation scaling a control.
    ''' </summary>
    ''' <param name="Obj">The animated object.</param>
    ''' <param name="Value">The size change as a percentage (e.g. -0.6) or a value.</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    ''' <param name="Absolute">Whether the size change is absolute. True means absolute pixels, False means a relative percentage.</param>
    Public Function AaScale(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False, Optional Absolute As Boolean = False) As AniData
        Dim ChangeRect As MyRect
        If Absolute Then
            ChangeRect = New MyRect(-0.5 * Value, -0.5 * Value, Value, Value)
        Else
            ChangeRect = New MyRect(-0.5 * Obj.ActualWidth * Value, -0.5 * Obj.ActualHeight * Value, Obj.ActualWidth * Value, Obj.ActualHeight * Value)
        End If
        Return New AniData With {.TypeMain = AniType.Scale, .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = ChangeRect, .IsAfter = After, .TimeFinished = -Delay}
    End Function

    'TextAppear

    ''' <summary>
    ''' Animation making text appear or disappear character by character.
    ''' </summary>
    ''' <param name="Obj">The animated object. Must be a Label or TextBlock.</param>
    ''' <param name="Hide">Whether to hide character by character. Defaults to False (appear character by character). The text must already exist.</param>
    ''' <param name="TimePerText">Whether the duration is determined by the text length.</param>
    ''' <param name="Time">Animation duration (ms). If TimePerText is True, this is the time per character.</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaTextAppear(Obj As Object, Optional Hide As Boolean = False, Optional TimePerText As Boolean = True, Optional Time As Integer = 70, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        'Are we cool yet？
        Return New AniData With {.TypeMain = AniType.TextAppear, .Ease = If(Ease, New AniEaseLinear), .TimeTotal = If(TimePerText, Time * If(TypeOf Obj Is TextBlock, Obj.Text, Obj.Context.ToString).ToString.Length, Time), .Obj = Obj, .Value = {If(TypeOf Obj Is TextBlock, Obj.Text, Obj.Context.ToString), Hide}, .IsAfter = After, .TimeFinished = -Delay}
    End Function

    'Code

    ''' <summary>
    ''' Runs code.
    ''' </summary>
    ''' <param name="Code">A ThreadStart, invoked on the main thread when run.</param>
    ''' <param name="Delay">Delay before the code runs (ms).</param>
    ''' <param name="After">Whether to wait for previous animations to finish before running.</param>
    Public Function AaCode(Code As ThreadStart, Optional Delay As Integer = 0, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Code,
                                   .TimeTotal = 1, .Value = Code, .IsAfter = After, .TimeFinished = -Delay}
    End Function

    'ScaleTransform

    ''' <summary>
    ''' Animation scaling a control the WPF way.
    ''' </summary>
    ''' <param name="Obj">The animated object. It must already have a single ScaleTransform value.</param>
    ''' <param name="Value">The size change as a percentage (e.g. -0.6).</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaScaleTransform(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.ScaleTransform, .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function

    'RotateTransform

    ''' <summary>
    ''' Animation rotating a control the WPF way.
    ''' </summary>
    ''' <param name="Obj">The animated object. It must already have a single ScaleTransform value.</param>
    ''' <param name="Value">Rotation angle (degrees).</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaRotateTransform(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.RotateTransform, .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function

    'TranslateTransform

    ''' <summary>
    ''' Animation moving along the X axis via TranslateTransform, which does not trigger a layout update.
    ''' </summary>
    ''' <param name="Obj">The animated object.</param>
    ''' <param name="Value">The amount to move.</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaTranslateX(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.TranslateX,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function
    ''' <summary>
    ''' Animation moving along the Y axis via TranslateTransform, which does not trigger a layout update.
    ''' </summary>
    ''' <param name="Obj">The animated object.</param>
    ''' <param name="Value">The amount to move.</param>
    ''' <param name="Time">Animation duration (ms).</param>
    ''' <param name="Delay">Delay before the animation starts (ms).</param>
    ''' <param name="Ease">Easing function type.</param>
    ''' <param name="After">Whether to wait for previous animations to finish before starting this one.</param>
    Public Function AaTranslateY(Obj As Object, Value As Double, Optional Time As Integer = 400, Optional Delay As Integer = 0, Optional Ease As AniEase = Nothing, Optional After As Boolean = False) As AniData
        Return New AniData With {.TypeMain = AniType.Number, .TypeSub = AniTypeSub.TranslateY,
                                   .TimeTotal = Time, .Ease = If(Ease, New AniEaseLinear), .Obj = Obj, .Value = Value, .IsAfter = After, .TimeFinished = -Delay}
    End Function

    'Special

    ''' <summary>
    ''' Shows the items in a StackPanel one after another.
    ''' </summary>
    Public Function AaStack(Stack As StackPanel, Optional Time As Integer = 100, Optional Delay As Integer = 25) As List(Of AniData)
        Dim Result = New List(Of AniData)
        Dim AniDelay As Integer = 0
        For Each Item In Stack.Children
            Item.Opacity = 0
            Result.Add(AaOpacity(Item, 1, Time, AniDelay))
            AniDelay += Delay
        Next
        Return Result
    End Function

#End Region

#Region "Easing Functions"

    'Base classes
    Public Enum AniEasePower As Integer
        Weak = 2
        Middle = 3
        Strong = 4
        ExtraStrong = 5
    End Enum
    ''' <summary>
    ''' Base class of easing functions.
    ''' </summary>
    Public MustInherit Class AniEase
        ''' <summary>
        ''' Gets the function value.
        ''' </summary>
        ''' <param name="t">Time percentage.</param>
        Public MustOverride Function GetValue(t As Double) As Double
        ''' <summary>
        ''' Gets the delta value.
        ''' </summary>
        ''' <param name="t1">The larger X.</param>
        ''' <param name="t0">The smaller X.</param>
        Public Overridable Function GetDelta(t1 As Double, t0 As Double) As Double
            Return GetValue(t1) - GetValue(t0)
        End Function
    End Class
    ''' <summary>
    ''' Combination of ease-in and ease-out.
    ''' </summary>
    Public Class AniEaseInout
        Inherits AniEase
        Private ReadOnly EaseIn As AniEase, EaseOut As AniEase, EaseInPercent As Double
        Public Sub New(EaseIn As AniEase, EaseOut As AniEase, Optional EaseInPercent As Double = 0.5)
            Me.EaseIn = EaseIn : Me.EaseOut = EaseOut : Me.EaseInPercent = EaseInPercent
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            If t < EaseInPercent Then
                Return EaseInPercent * EaseIn.GetValue(t / EaseInPercent)
            Else
                Return (1 - EaseInPercent) * EaseOut.GetValue((t - EaseInPercent) / (1 - EaseInPercent)) + EaseInPercent
            End If
        End Function
    End Class

    'Linear
    ''' <summary>
    ''' Linear, no easing.
    ''' </summary>
    Public Class AniEaseLinear
        Inherits AniEase
        Public Overrides Function GetValue(t As Double) As Double
            Return t.Clamp(0, 1)
        End Function
        Public Overrides Function GetDelta(t1 As Double, t0 As Double) As Double
            Return t1.Clamp(0, 1) - t0.Clamp(0, 1)
        End Function
    End Class

    'Fluent / smooth
    ''' <summary>
    ''' Smooth start.
    ''' </summary>
    Public Class AniEaseInFluent
        Inherits AniEase
        Private ReadOnly p As AniEasePower
        Public Sub New(Optional Power As AniEasePower = AniEasePower.Middle)
            p = Power
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            Return t.Clamp(0, 1) ^ p
        End Function
    End Class
    ''' <summary>
    ''' Smooth end.
    ''' </summary>
    Public Class AniEaseOutFluent
        Inherits AniEase
        Private ReadOnly p As AniEasePower
        Public Sub New(Optional Power As AniEasePower = AniEasePower.Middle)
            p = Power
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            Return 1 - (1 - t).Clamp(0, 1) ^ p
        End Function
    End Class
    ''' <summary>
    ''' Smooth start and end.
    ''' </summary>
    Public Class AniEaseInoutFluent
        Inherits AniEase
        Private Ease As AniEaseInout
        Public Sub New(Optional Power As AniEasePower = AniEasePower.Middle, Optional Middle As Double = 0.5)
            Ease = New AniEaseInout(New AniEaseInFluent(Power), New AniEaseOutFluent(Power), Middle)
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            Return Ease.GetValue(t)
        End Function
    End Class
    ''' <summary>
    ''' Smooth end that starts at a specific speed.
    ''' </summary>
    Public Class AniEaseOutFluentWithInitial
        Inherits AniEase
        Private ReadOnly alpha As Double '(initial speed / average speed) – 1
        ''' <param name="InitialPixelPerSecond">Initial speed, px/s</param>
        ''' <param name="TotalSecond">Total duration, s</param>
        ''' <param name="TotalDistance">Total distance, px</param>
        Public Sub New(InitialPixelPerSecond As Double, TotalSecond As Double, TotalDistance As Double)
            Dim v0_norm As Double = InitialPixelPerSecond * TotalSecond / TotalDistance 'Normalized initial speed
            alpha = v0_norm - 1.0
            If alpha < 0 Then alpha = 0 'Degrades to linear when the initial speed is below the average speed
        End Sub
        Public Overrides Function GetValue(percent As Double) As Double
            Dim p As Double = percent.Clamp(0, 1)
            If alpha = 0 Then Return p 'Degrade to linear
            Return (alpha + 1) * p / (1 + alpha * p)
        End Function
    End Class

    'Back / bounce back
    ''' <summary>
    ''' Bounce-back start. Effective for 1/3 of the time.
    ''' </summary>
    Public Class AniEaseInBack
        Inherits AniEase
        Private ReadOnly p As Double
        Public Sub New(Optional Power As AniEasePower = AniEasePower.Middle)
            p = 3 - Power * 0.5
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            t = t.Clamp(0, 1)
            Return t ^ p * Math.Cos(1.5 * Math.PI * (1 - t))
        End Function
    End Class
    ''' <summary>
    ''' Bounce-back end. Effective for 1/3 of the time.
    ''' </summary>
    Public Class AniEaseOutBack
        Inherits AniEase
        Private ReadOnly p As Double
        Public Sub New(Optional Power As AniEasePower = AniEasePower.Middle)
            p = 3 - Power * 0.5
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            t = t.Clamp(0, 1)
            Return 1 - (1 - t) ^ p * Math.Cos(1.5 * Math.PI * t)
        End Function
    End Class

    'Car / smooth-bounce back
    ''' <summary>
    ''' Bounce-back start, short smooth end.
    ''' </summary>
    Public Class AniEaseInCar
        Inherits AniEase
        Private Ease As AniEaseInout
        Public Sub New(Optional Middle As Double = 0.7, Optional Power As AniEasePower = AniEasePower.Middle)
            Ease = New AniEaseInout(New AniEaseInBack(Power), New AniEaseOutFluent(Power), Middle)
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            Return Ease.GetValue(t)
        End Function
    End Class
    ''' <summary>
    ''' Short smooth start, bounce-back end.
    ''' </summary>
    Public Class AniEaseOutCar
        Inherits AniEase
        Private Ease As AniEaseInout
        Public Sub New(Optional Middle As Double = 0.3, Optional Power As AniEasePower = AniEasePower.Middle)
            Ease = New AniEaseInout(New AniEaseInFluent(Power), New AniEaseOutBack(Power), Middle)
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            Return Ease.GetValue(t)
        End Function
    End Class

    'Elastic / spring
    ''' <summary>
    ''' Spring start. Reaches its minimum at about 60%.
    ''' </summary>
    Public Class AniEaseInElastic
        Inherits AniEase
        Private ReadOnly p As Integer '6~9
        Public Sub New(Optional Power As AniEasePower = AniEasePower.Middle)
            p = Power + 4
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            t = t.Clamp(0, 1)
            Return t ^ ((p - 1) * 0.25) * Math.Cos((p - 3.5) * Math.PI * (1 - t) ^ 1.5)
        End Function
    End Class
    ''' <summary>
    ''' Spring end. Reaches its maximum at about 40%.
    ''' </summary>
    Public Class AniEaseOutElastic
        Inherits AniEase
        Private ReadOnly p As Integer
        Public Sub New(Optional Power As AniEasePower = AniEasePower.Middle)
            p = Power + 4
        End Sub
        Public Overrides Function GetValue(t As Double) As Double
            t = 1 - t.Clamp(0, 1)
            Return 1 - t ^ ((p - 1) * 0.25) * Math.Cos((p - 3.5) * Math.PI * (1 - t) ^ 1.5)
        End Function
    End Class

#End Region

#Region "Interface (Start, Stop, Check)"

    ''' <summary>
    ''' Starts an animation group.
    ''' </summary>
    ''' <param name="AniGroup">A collection of AniData objects created by the Aa-prefixed functions.</param>
    ''' <param name="Name">Name of the animation group. An existing group with the same name is stopped immediately.</param>
    Public Sub AniStart(AniGroup As IList, Optional Name As String = "", Optional RefreshTime As Boolean = False)
        If Application.Current IsNot Nothing AndAlso Not Application.Current.Dispatcher.CheckAccess() Then
            RunInUiWait(Sub() AniStart(AniGroup, Name, RefreshTime))
            Return
        End If
        If RefreshTime OrElse AniGroups.Count = 0 Then AniLastTick = GetTimeMs() 'Prevent animations from ending abruptly if processing has already caused a huge delay
        'Add to the running animation groups
        Dim AllAnis = New List(Of AniData)
        For Each Element In AniGroup
            If TypeOf Element Is ICollection Then
                AllAnis.AddRange(Element)
            Else
                AllAnis.Add(Element)
            End If
        Next
        Dim NewEntry As New AniGroupEntry With {.Data = AllAnis, .StartTick = GetTimeMs()}
        If Name = "" Then
            Name = NewEntry.Uuid
        Else
            AniStop(Name)
        End If
        AniGroups.Add(Name, NewEntry)
        If AniRunning Then StartRendering()
    End Sub
    ''' <summary>
    ''' Starts an animation group.
    ''' </summary>
    Public Sub AniStart(AniGroup As AniData, Optional Name As String = "", Optional RefreshTime As Boolean = False)
        AniStart(New List(Of AniData) From {AniGroup}, Name, RefreshTime)
    End Sub
    ''' <summary>
    ''' Stops an animation group immediately.
    ''' </summary>
    ''' <param name="name">Name of the animation group to stop.</param>
    Public Sub AniStop(Name As String)
        If Application.Current IsNot Nothing AndAlso Not Application.Current.Dispatcher.CheckAccess() Then
            RunInUiWait(Sub() AniStop(Name))
            Return
        End If
        AniGroups.Remove(Name)
        If AniGroups.Count = 0 Then StopRendering()
    End Sub
    ''' <summary>
    ''' Gets whether the animation is in progress.
    ''' </summary>
    Public Function AniIsRun(Name As String) As Boolean
        If Application.Current IsNot Nothing AndAlso Not Application.Current.Dispatcher.CheckAccess() Then
            Return Application.Current.Dispatcher.Invoke(Function() AniIsRun(Name))
        End If
        Return AniGroups.ContainsKey(Name)
    End Function

#End Region

    Private AniFPSCounter As Integer = 0
    Private AniFPSTimer As Long = 0
    ''' <summary>
    ''' Current animation FPS.
    ''' </summary>
    Public AniFPS As Integer = 0

    ''' <summary>
    ''' Starts running animations.
    ''' </summary>
    Public Sub AniStart()
        If AniRunning Then Return
        AniRunning = True
        AniLastTick = GetTimeMs()
        AniFPSTimer = AniLastTick
        If AniGroups.Count > 0 Then StartRendering()
    End Sub

    Private RenderingSubscribed As Boolean
    Private RenderFallback As Threading.DispatcherTimer
    Private LastRenderTime As TimeSpan = TimeSpan.MinValue
    Private Sub StartRendering()
        If RenderingSubscribed Then Return
        RenderingSubscribed = True
        LastRenderTime = TimeSpan.MinValue
        AddHandler CompositionTarget.Rendering, AddressOf RenderFrame
        If RenderFallback Is Nothing Then
            RenderFallback = New Threading.DispatcherTimer(Threading.DispatcherPriority.Background) With {.Interval = TimeSpan.FromMilliseconds(100)}
            AddHandler RenderFallback.Tick, Sub()
                                                'Minimized/hidden windows may not receive render events; delayed actions must still finish.
                                                If GetTimeMs() - AniLastTick >= 100 Then AdvanceFrame()
                                            End Sub
        End If
        RenderFallback.Start()
    End Sub
    Private Sub StopRendering()
        If Not RenderingSubscribed Then Return
        RemoveHandler CompositionTarget.Rendering, AddressOf RenderFrame
        RenderFallback?.Stop()
        RenderingSubscribed = False
    End Sub
    Private Sub RenderFrame(Sender As Object, e As EventArgs)
        If AniGroups.Count = 0 OrElse IsProgramEnding Then
            StopRendering()
            Return
        End If
        Dim RenderTime = DirectCast(e, RenderingEventArgs).RenderingTime
        If RenderTime = LastRenderTime Then Return
        LastRenderTime = RenderTime
        AdvanceFrame()
    End Sub
    Private Sub AdvanceFrame()
        If AniGroups.Count = 0 OrElse IsProgramEnding Then
            StopRendering()
            Return
        End If
        Dim NowTick = GetTimeMs()
        Dim Delta = (NowTick - AniLastTick).Clamp(0, 100000)
        AniLastTick = NowTick
        AniTimer(Delta)
        If AniGroups.Count = 0 Then StopRendering()
        If ModeDebug Then
            AniFPSCounter += 1
            Dim Elapsed = NowTick - AniFPSTimer
            If Elapsed >= 500 Then
                AniFPS = CInt(AniFPSCounter * 1000L / Elapsed)
                AniFPSCounter = 0
                AniFPSTimer = NowTick
            End If
        End If
    End Sub

    Public Sub AniTimer(DeltaTick As Double)
        Try
            If DeltaTick / AniSpeed > 100 Then Logger.Info($"两个动画帧间隔 {DeltaTick} ms")
            'A frame snapshot lets callbacks replace or cancel groups without quadratic dictionary indexing.
            For Each Pair In AniGroups.ToArray()
                Dim Entry = Pair.Value
                Dim CurrentEntry As AniGroupEntry = Nothing
                If Not AniGroups.TryGetValue(Pair.Key, CurrentEntry) OrElse CurrentEntry.Uuid <> Entry.Uuid Then Continue For
                If Entry.StartTick > AniLastTick Then Continue For 'Skip animations that start after this tick
                Dim CanRemoveAfter = True 'Whether the "after" flag should be removed
                Dim ii = 0

                'Loop over each animation
                Do While ii < Entry.Data.Count
                    Dim Anim As AniData = Entry.Data(ii)
                    'By type
                    If Anim.IsAfter = False Then 'Before
                        CanRemoveAfter = False 'Cancel removing the "after" flag
                        'Increase the elapsed time
                        Anim.TimeFinished += DeltaTick
                        'Run the animation
                        If Anim.TimeFinished > 0 Then
                            Anim = AniRun(Anim)
                        End If
                        'If the current animation has finished
                        If Anim.TimeFinished >= Anim.TimeTotal Then
                            'If it is an animation to a color resource, set the reference
                            If Anim.TypeMain = AniType.Color AndAlso Not Anim.Obj(2) = "" Then Anim.Obj(0).SetResourceReference(Anim.Obj(1), Anim.Obj(2))
                            'Remove
                            Entry.Data.RemoveAt(ii)
                            GoTo NextAni
                        End If
                        Entry.Data(ii) = Anim
                    Else 'After
                        If CanRemoveAfter Then
                            'Change "after" to "before"
                            CanRemoveAfter = False
                            Anim.IsAfter = False
                            Entry.Data(ii) = Anim
                            'Loop this animation again
                            GoTo NextAni
                        Else
                            'The "after" flag cannot be removed, so end this animation group
                            Exit Do
                        End If
                    End If
                    ii += 1
NextAni:
                Loop

                If Entry.Data.Count = 0 AndAlso AniGroups.TryGetValue(Pair.Key, CurrentEntry) AndAlso CurrentEntry.Uuid = Entry.Uuid Then
                    AniGroups.Remove(Pair.Key)
                End If
            Next

        Catch ex As Exception
            Logger.Error(ex, "动画刻执行失败", LogBehavior.Toast)
        End Try
    End Sub

    ''' <summary>
    ''' Runs one animation.
    ''' </summary>
    ''' <param name="Ani">The animation object to run.</param>
    Private Function AniRun(Ani As AniData) As AniData
        Try
            Select Case Ani.TypeMain

                Case AniType.Number
                    Dim Delta As Double = MathUtils.Lerp(0, Ani.Value, Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, Ani.TimePercent))
                    If Delta <> 0 Then
                        Select Case Ani.TypeSub
                            Case AniTypeSub.X
                                If TypeOf Ani.Obj Is Window Then
                                    CType(Ani.Obj, Window).Left += Delta
                                Else
                                    Select Case Ani.Obj.HorizontalAlignment
                                        Case HorizontalAlignment.Left, HorizontalAlignment.Stretch
                                            Ani.Obj.Margin = New Thickness(Ani.Obj.Margin.Left + Delta, Ani.Obj.Margin.Top, Ani.Obj.Margin.Right, Ani.Obj.Margin.Bottom)
                                        Case HorizontalAlignment.Right
                                            Ani.Obj.Margin = New Thickness(Ani.Obj.Margin.Left, Ani.Obj.Margin.Top, Ani.Obj.Margin.Right - Delta, Ani.Obj.Margin.Bottom)
                                    End Select
                                End If
                            Case AniTypeSub.Y
                                If TypeOf Ani.Obj Is Window Then
                                    CType(Ani.Obj, Window).Top += Delta
                                Else
                                    Select Case Ani.Obj.VerticalAlignment
                                        Case VerticalAlignment.Top
                                            Ani.Obj.Margin = New Thickness(Ani.Obj.Margin.Left, Ani.Obj.Margin.Top + Delta, Ani.Obj.Margin.Right, Ani.Obj.Margin.Bottom)
                                        Case VerticalAlignment.Bottom
                                            Ani.Obj.Margin = New Thickness(Ani.Obj.Margin.Left, Ani.Obj.Margin.Top, Ani.Obj.Margin.Right, Ani.Obj.Margin.Bottom - Delta)
                                    End Select
                                End If
                            Case AniTypeSub.Opacity
                                If TypeOf Ani.Obj Is UIElement Then
                                    Dim Obj As UIElement = Ani.Obj
                                    Obj.Opacity = (Obj.Opacity + Delta).Clamp(0, 1)
                                ElseIf TypeOf Ani.Obj Is DropShadowEffect Then
                                    Dim Obj As DropShadowEffect = Ani.Obj
                                    Obj.Opacity = (Obj.Opacity + Delta).Clamp(0, 1)
                                End If
                            Case AniTypeSub.Width
                                Dim Obj As FrameworkElement = Ani.Obj
                                Obj.Width = Math.Max(If(Double.IsNaN(Obj.Width), Obj.ActualWidth, Obj.Width) + Delta, 0)
                            Case AniTypeSub.Height
                                Dim Obj As FrameworkElement = Ani.Obj
                                Obj.Height = Math.Max(If(Double.IsNaN(Obj.Height), Obj.ActualHeight, Obj.Height) + Delta, 0)
                            Case AniTypeSub.Value
                                Ani.Obj.Value += Delta
                            Case AniTypeSub.Radius
                                Ani.Obj.Radius += Delta
                            Case AniTypeSub.StrokeThickness
                                Ani.Obj.StrokeThickness = Math.Max(Ani.Obj.StrokeThickness + Delta, 0)
                            Case AniTypeSub.BorderThickness
                                Ani.Obj.BorderThickness = New Thickness(CType(Ani.Obj.BorderThickness, Thickness).Bottom + Delta)
                            Case AniTypeSub.TranslateX
                                If IsNothing(Ani.Obj.RenderTransform) OrElse TypeOf Ani.Obj.RenderTransform IsNot TranslateTransform Then Ani.Obj.RenderTransform = New TranslateTransform(0, 0)
                                CType(Ani.Obj.RenderTransform, TranslateTransform).X += Delta
                            Case AniTypeSub.TranslateY
                                If IsNothing(Ani.Obj.RenderTransform) OrElse TypeOf Ani.Obj.RenderTransform IsNot TranslateTransform Then Ani.Obj.RenderTransform = New TranslateTransform(0, 0)
                                CType(Ani.Obj.RenderTransform, TranslateTransform).Y += Delta
                            Case AniTypeSub.Double
                                Ani.Obj(0).SetValue(Ani.Obj(1), Ani.Obj(0).GetValue(Ani.Obj(1)) + Delta)
                            Case AniTypeSub.DoubleParam
                                CType(Ani.Obj, ParameterizedThreadStart)(Delta)
                            Case AniTypeSub.GridLengthWidth
                                Ani.Obj.Width = New GridLength(Math.Max(Ani.Obj.Width.Value + Delta, 0), GridUnitType.Star)
                        End Select
                    End If

                Case AniType.Color
                    'Last stores the remaining fractional value
                    Dim Delta As MyColor = MyColor.Lerp(New MyColor(0, 0, 0, 0), Ani.Value, Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, Ani.TimePercent)) + Ani.ValueLast
                    Dim Obj As FrameworkElement = Ani.Obj(0)
                    Dim Prop As DependencyProperty = Ani.Obj(1)
                    Dim NewColor As MyColor = New MyColor(Obj.GetValue(Prop)) + Delta
                    Obj.SetValue(Prop, If(Prop.PropertyType.Name = "Color", CType(NewColor, Color), CType(NewColor, SolidColorBrush)))
                    Ani.ValueLast = NewColor - New MyColor(Obj.GetValue(Prop))

                Case AniType.Scale
                    Dim Obj As FrameworkElement = Ani.Obj
                    Dim Delta As Double = Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, Ani.TimePercent)
                    Obj.Margin = New Thickness(Obj.Margin.Left + MathUtils.Lerp(0, Ani.Value.Left, Delta), Obj.Margin.Top + MathUtils.Lerp(0, Ani.Value.Top, Delta), Obj.Margin.Right + MathUtils.Lerp(0, Ani.Value.Left, Delta), Obj.Margin.Bottom + MathUtils.Lerp(0, Ani.Value.Top, Delta))
                    Obj.Width = Math.Max(Obj.Width + MathUtils.Lerp(0, Ani.Value.Width, Delta), 0)
                    Obj.Height = Math.Max(Obj.Height + MathUtils.Lerp(0, Ani.Value.Height, Delta), 0)

                Case AniType.TextAppear
                    Dim Text As String = Ani.Value(0)
                    Dim TextCount As Integer = If(Ani.Value(1), Text.Length, 0) +
                                               Math.Round(Text.Length * If(Ani.Value(1), -1, 1) * Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, 0))
                    Dim NewText As String = Text.Substring(0, TextCount)
                    'Append garbled characters
                    If TextCount < Text.Length Then
                        Dim NextText As String = Text.Substring(TextCount, 1)
                        If Convert.ToInt32(Convert.ToChar(NextText)) >= Convert.ToInt32(Convert.ToChar(128)) Then
                            NewText &= Encoding.GetEncoding("GB18030").GetString({RandomInteger(16 + 160, 87 + 160), RandomInteger(1 + 160, 89 + 160)})
                        Else
                            NewText &= RandomInteger(0, 85).ConvertRadix(86)
                        End If
                    End If
                    'Set the text
                    If TypeOf Ani.Obj Is TextBlock Then
                        Ani.Obj.Text = NewText
                    Else
                        Ani.Obj.Context = NewText
                    End If

                Case AniType.Code
                    Ani.TimeFinished = Ani.TimeTotal
                    CType(Ani.Value, ThreadStart)()

                Case AniType.ScaleTransform
                    Dim Obj As FrameworkElement = Ani.Obj
                    If TypeOf Obj.RenderTransform IsNot ScaleTransform Then
                        If Obj.RenderTransformOrigin = New Point(0, 0) Then Obj.RenderTransformOrigin = New Point(0.5, 0.5)
                        Obj.RenderTransform = New ScaleTransform(1, 1)
                    End If
                    Dim Delta As Double = MathUtils.Lerp(0, Ani.Value, Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, Ani.TimePercent))
                    CType(Obj.RenderTransform, ScaleTransform).ScaleX = Math.Max(CType(Obj.RenderTransform, ScaleTransform).ScaleX + Delta, 0)
                    CType(Obj.RenderTransform, ScaleTransform).ScaleY = Math.Max(CType(Obj.RenderTransform, ScaleTransform).ScaleY + Delta, 0)

                Case AniType.RotateTransform
                    Dim Obj As FrameworkElement = Ani.Obj
                    If TypeOf Obj.RenderTransform IsNot RotateTransform Then
                        If Obj.RenderTransformOrigin = New Point(0, 0) Then Obj.RenderTransformOrigin = New Point(0.5, 0.5)
                        Obj.RenderTransform = New RotateTransform(0)
                    End If
                    Dim Delta As Double = MathUtils.Lerp(0, Ani.Value, Ani.Ease.GetDelta(Ani.TimeFinished / Ani.TimeTotal, Ani.TimePercent))
                    CType(Obj.RenderTransform, RotateTransform).Angle = CType(Obj.RenderTransform, RotateTransform).Angle + Delta

            End Select
            Ani.TimePercent = Ani.TimeFinished / Ani.TimeTotal 'Update the completion percentage
        Catch ex As Exception
            Logger.Error(ex, $"执行动画失败：{Ani}", LogBehavior.Toast)
        End Try
        Return Ani
    End Function

End Module
