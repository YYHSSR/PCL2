Public NotInheritable Class VisiblePageTimer
    Private ReadOnly Owner As FrameworkElement
    Private ReadOnly Timer As Threading.DispatcherTimer
    Public Sub New(Owner As FrameworkElement, Refresh As Action)
        Me.Owner = Owner
        Timer = New Threading.DispatcherTimer(Threading.DispatcherPriority.Background, Owner.Dispatcher) With {.Interval = TimeSpan.FromSeconds(1)}
        AddHandler Timer.Tick, Sub()
                                   If Owner.IsLoaded AndAlso Owner.IsVisible Then
                                       Refresh()
                                   Else
                                       Timer.Stop()
                                   End If
                               End Sub
        AddHandler Owner.Loaded, Sub() UpdateState()
        AddHandler Owner.Unloaded, Sub() Timer.Stop()
        AddHandler Owner.IsVisibleChanged, Sub() UpdateState()
        UpdateState()
    End Sub
    Private Sub UpdateState()
        Timer.IsEnabled = Owner.IsLoaded AndAlso Owner.IsVisible
    End Sub
    Public ReadOnly Property IsRunning As Boolean
        Get
            Return Timer.IsEnabled
        End Get
    End Property
End Class
