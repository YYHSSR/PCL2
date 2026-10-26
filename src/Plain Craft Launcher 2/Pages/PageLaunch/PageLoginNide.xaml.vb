Public Class PageLoginNide
    Private IsFirstLoad As Boolean = True
    ''' <summary>
    ''' Refresh everything shown on this page.
    ''' </summary>
    Public Sub Reload(KeepInput As Boolean)
        'Remember password
        CheckRemember.Checked = Settings.Get(Of Boolean)("LoginRemember")
        If KeepInput AndAlso Not IsFirstLoad Then 'Skip KeepInput on the first load, or the text box stays empty
            'Keep the input and only refresh the dropdown list
            Dim Input As String = ComboName.Text
            ComboName.ItemsSource = If(Settings.Get(Of String)("LoginNideEmail") = "", Nothing, Settings.Get(Of String)("LoginNideEmail").ToString.Split("¨"))
            ComboName.Text = Input
        Else
            'Drop the input, refresh the list, and select the first item
            If Settings.Get(Of String)("LoginNideEmail") = "" Then
                ComboName.ItemsSource = Nothing
            Else
                ComboName.ItemsSource = Settings.Get(Of String)("LoginNideEmail").Split("¨")
                ComboName.Text = Settings.Get(Of String)("LoginNideEmail").BeforeFirst("¨")
                If Settings.Get(Of Boolean)("LoginRemember") Then TextPass.Password = Settings.Get(Of String)("LoginNidePass").BeforeFirst("¨").Trim
            End If
        End If
        IsFirstLoad = False
    End Sub
    ''' <summary>
    ''' Get this page's login info.
    ''' </summary>
    Public Shared Function GetLoginData() As McLoginServer
        Dim Server As String = If(IsNothing(McInstanceSelected), Settings.Get(Of String)("CacheNideServer"), Settings.Get(Of String)("VersionServerNide", Instance:=McInstanceSelected))
        If FrmLoginNide Is Nothing Then
            Return New McLoginServer(McLoginType.Nide) With {.Token = "Nide", .UserName = "", .Password = "", .Description = "统一通行证", .Type = McLoginType.Nide, .BaseUrl = "https://auth.mc-user.com:233/" & Server & "/authserver"}
        Else
            Return New McLoginServer(McLoginType.Nide) With {.Token = "Nide", .UserName = FrmLoginNide.ComboName.Text.Replace("¨", "").Trim, .Password = FrmLoginNide.TextPass.Password.Replace("¨", "").Trim, .Description = "统一通行证", .Type = McLoginType.Nide, .BaseUrl = "https://auth.mc-user.com:233/" & Server & "/authserver"}
        End If
    End Function
    ''' <summary>
    ''' Whether this page's login info is valid.
    ''' </summary>
    Public Shared Function IsVaild(LoginData As McLoginServer) As String
        If LoginData.UserName = "" Then Return "账号不能为空！"
        If LoginData.Password = "" Then Return "密码不能为空！"
        Return ""
    End Function
    Public Function IsVaild() As String
        Return IsVaild(GetLoginData())
    End Function

    'Save the entered info
    Private Sub ComboName_TextChanged(sender As Object, e As TextChangedEventArgs) Handles ComboName.TextChanged
        If sender.Text = "" Then TextPass.Password = ""
        If AniControlEnabled = 0 Then Settings.Set("CacheNideAccess", "")  'Force it to skip Validate
    End Sub
    Private Sub TextPass_PasswordChanged(sender As Object, e As RoutedEventArgs) Handles TextPass.PasswordChanged
        If AniControlEnabled = 0 Then Settings.Set("CacheNideAccess", "")
    End Sub
    Private Sub ComboName_SelectionChanged(sender As MyComboBox, e As SelectionChangedEventArgs) Handles ComboName.SelectionChanged
        If sender.SelectedIndex = -1 OrElse Not Settings.Get(Of Boolean)("LoginRemember") Then
            TextPass.Password = ""
        Else
            TextPass.Password = Settings.Get(Of String)("LoginNidePass").Split("¨")(sender.SelectedIndex).Trim
        End If
    End Sub
    Private Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles CheckRemember.Change
        If AniControlEnabled = 0 Then Settings.Set(sender.Tag, sender.Checked)
    End Sub

    'Link handling
    Private Sub ComboName_TextChanged() Handles ComboName.TextChanged
        BtnLink.Content = If(ComboName.Text = "", "注册账号", "找回密码")
    End Sub
    Private Sub Btn_Click(sender As Object, e As EventArgs) Handles BtnLink.Click
        If BtnLink.Content = "注册账号" Then
            OpenWebsite("https://login.mc-user.com:233/" & Settings.Get(Of String)("VersionServerNide", Instance:=McInstanceSelected) & "/register")
        Else
            OpenWebsite("https://login.mc-user.com:233/account/login")
        End If
    End Sub

End Class
