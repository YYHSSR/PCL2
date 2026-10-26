Public Class PageInstanceModDisabled

    Private Sub BtnDownload_Click(sender As Object, e As EventArgs) Handles BtnDownload.Click
        FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall)
    End Sub
    Private Sub BtnVersion_Click(sender As Object, e As EventArgs) Handles BtnVersion.Click
        FrmMain.PageChange(FormMain.PageType.Launch) 'Selecting a version on the instance picker only goes back one level, so anchor Launch first or it returns to this instance-settings page
        FrmMain.PageChange(FormMain.PageType.InstanceSelect)
    End Sub

    Public Sub BtnDownload_Loaded() Handles BtnDownload.Loaded
        Dim NewVisibility = If(If(FrmSelectRight Is Nothing, False, FrmSelectRight.ShowHidden), Visibility.Collapsed, Visibility.Visible)
        If BtnDownload.Visibility <> NewVisibility Then
            BtnDownload.Visibility = NewVisibility
            PanMain.TriggerForceResize()
        End If
    End Sub

End Class
