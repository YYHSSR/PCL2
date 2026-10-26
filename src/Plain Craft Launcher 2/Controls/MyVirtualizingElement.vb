Public Class MyVirtualizingElement(Of T As FrameworkElement)
    Inherits FrameworkElement

    Private Initializer As Func(Of T)
    Private Realized As T
    Public Sub New(Initializer As Func(Of T))
        Me.Initializer = Initializer
        LazyLoadBehavior.OnFirstEnterScrollViewerViewport(Me, AddressOf Init)
    End Sub

    ''' <summary>
    ''' Instantiates this control.
    ''' </summary>
    Public Function Init() As T
        If Realized Is Nothing Then
            Realized = Initializer()
            Initializer = Nothing
        End If
        Dim Element As T = Realized
        If Parent IsNot Nothing Then
            If TypeOf Parent IsNot Panel Then Throw New Exception("MyVirtualizingElement 的父级必须是一个 Panel")
            Dim ParentPanel As Panel = Parent
            Dim CurrentIndex As Integer = ParentPanel.Children.IndexOf(Me)
            ParentPanel.Children.RemoveAt(CurrentIndex)
            ParentPanel.Children.Insert(CurrentIndex, Element)
        End If
        Return Element
    End Function
    Public Shared Widening Operator CType(Virtualized As MyVirtualizingElement(Of T)) As T
        Return Virtualized.Init()
    End Operator

End Class

'Non-generic version
Public Class MyVirtualizingElement
    Inherits FrameworkElement

    Private Initializer As Func(Of FrameworkElement)
    Private Realized As FrameworkElement
    Public Sub New(Initializer As Func(Of FrameworkElement))
        Me.Initializer = Initializer
        LazyLoadBehavior.OnFirstEnterScrollViewerViewport(Me, AddressOf Init)
    End Sub

    ''' <summary>
    ''' Instantiates this control.
    ''' </summary>
    Public Function Init() As FrameworkElement
        If Realized Is Nothing Then
            Realized = Initializer()
            Initializer = Nothing
        End If
        Dim Element As FrameworkElement = Realized
        If Parent IsNot Nothing Then
            If TypeOf Parent IsNot Panel Then Throw New Exception("MyVirtualizingElement 的父级必须是一个 Panel")
            Dim ParentPanel As Panel = Parent
            Dim CurrentIndex As Integer = ParentPanel.Children.IndexOf(Me)
            ParentPanel.Children.RemoveAt(CurrentIndex)
            ParentPanel.Children.Insert(CurrentIndex, Element)
        End If
        Return Element
    End Function

    ''' <summary>
    ''' Gets the instantiated control.
    ''' If the control has not been instantiated, it is instantiated immediately.
    ''' If the type does not match, the original value is returned.
    ''' </summary>
    Public Shared Function TryInit(Element As FrameworkElement) As FrameworkElement
        If GeneralUtils.IsGenericInstanceOf(Element, GetType(MyVirtualizingElement(Of ))) Then
            Return CType(Element, Object).Init()
        ElseIf TypeOf Element Is MyVirtualizingElement Then
            Return CType(Element, MyVirtualizingElement).Init()
        Else
            Return Element
        End If
    End Function

End Class
