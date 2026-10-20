Public Class PageLaunchRight
    Implements IRefreshable

    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
        PanLog.Visibility = ModeDebug.ToVisibility
    End Sub
    Public Sub ForceRefresh() Implements IRefreshable.Refresh
        PanBack.ScrollToHome()
    End Sub
End Class
