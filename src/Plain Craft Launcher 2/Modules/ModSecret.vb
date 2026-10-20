Friend Module ModSecret

    '标注 PCL 的不同分支，仅用于替换标记
    Public Const VersionBranchMain As String = "Local"
    '保留旧开发版设置路径以支持账号缓存迁移
    Public ReadOnly RegFolder As String = GetSettingsProfile()
    '用于微软登录的 ClientId
    Public OAuthClientId As String = If(Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID"), "")

#Region "网络鉴权"

    Friend Function SecretCdnSign(UrlWithMark As String) As String
        If Not UrlWithMark.EndsWithF("{CDN}") Then Return UrlWithMark
        Return UrlWithMark.Replace("{CDN}", "").Replace(" ", "%20")
    End Function
    ''' <summary>
    ''' 设置请求标识与资源 API 配置。
    ''' </summary>
    Friend Sub SecretHeadersSign(Url As String, ByRef Req As HttpRequestMessage, Optional SimulateBrowserHeaders As Boolean = False)
        If Not Req.Headers.UserAgent.Any Then
            If Url.Contains("baidupcs.com") OrElse Url.Contains("baidu.com") Then
                Req.Headers.Add("User-Agent", "LogStatistic")  '#4951
            ElseIf SimulateBrowserHeaders Then
                Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}.{CInt(BuildType)} Mozilla/5.0 AppleWebKit/537.36 Chrome/63.0.3239.132 Safari/537.36")
            Else
                Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}.{CInt(BuildType)}")
            End If
        End If
        CurseForgeAccess.ApplyHeaders(New Uri(Url), Req)
    End Sub

#End Region

#Region "主题"

    Public Color1 As New MyColor(52, 61, 74)
    Public Color2 As New MyColor(11, 91, 203)
    Public Color3 As New MyColor(19, 112, 243)
    Public Color4 As New MyColor(72, 144, 245)
    Public Color5 As New MyColor(150, 192, 249)
    Public Color6 As New MyColor(213, 230, 253)
    Public Color7 As New MyColor(222, 236, 253)
    Public Color8 As New MyColor(234, 242, 254)
    Public ColorBg0 As New MyColor(150, 192, 249)
    Public ColorBg1 As New MyColor(190, Color7)
    Public ColorGray1 As New MyColor(64, 64, 64)
    Public ColorGray2 As New MyColor(115, 115, 115)
    Public ColorGray3 As New MyColor(140, 140, 140)
    Public ColorGray4 As New MyColor(166, 166, 166)
    Public ColorGray5 As New MyColor(204, 204, 204)
    Public ColorGray6 As New MyColor(235, 235, 235)
    Public ColorGray7 As New MyColor(240, 240, 240)
    Public ColorGray8 As New MyColor(245, 245, 245)
    Public ColorSemiTransparent As New MyColor(1, Color8)

    Public Sub ApplyDefaultAppearance()
        FrmMain.PanTitle.Background = New SolidColorBrush(Color3)
        FrmMain.PanTitle.Background.Freeze()
        FrmMain.PanForm.Background = New SolidColorBrush(Color7)
        FrmMain.PanForm.Background.Freeze()
    End Sub

#End Region

#Region "联网配置"

    ''' <summary>
    ''' 联网获取的配置信息。
    ''' 若获取失败或仍在获取中，可能为 Nothing。
    ''' </summary>
    Public ServerConfig As JObject


#End Region

#Region "兼容接口"
    Private Function GetSettingsProfile() As String
        Dim Profile = Environment.GetEnvironmentVariable("PCL_SETTINGS_PROFILE")
        If String.IsNullOrWhiteSpace(Profile) Then Return "PCL2Local"
        If Not System.Text.RegularExpressions.Regex.IsMatch(Profile, "^[A-Za-z0-9_-]{1,64}$") Then Throw New ArgumentException("Invalid PCL_SETTINGS_PROFILE")
        Return Profile
    End Function
    Friend Function GetIdentify() As String
        'Legacy decryption input, never used to encrypt new credentials.
        Return "0000-0000-0000-0000"
    End Function
#End Region

End Module
