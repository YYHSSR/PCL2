Public NotInheritable Class CurseForgeAccess
    Public Const MissingKeyMessage As String = "未配置 CurseForge API Key。官方接口会直接拒绝访问（403）。请在 设置 → 其他 → 社区资源 中保存自己的有效 Key，或选择 Modrinth。"

    Public Shared Function GetApiKey() As String
        Dim Key = Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY")
        If String.IsNullOrWhiteSpace(Key) Then Key = Settings.Get(Of String)("ToolCurseForgeApiKey")
        Return If(Key, "").Trim()
    End Function

    Public Shared Function IsAuthenticatedHost(Address As Uri) As Boolean
        Return Address.Host.Equals("api.curseforge.com", StringComparison.OrdinalIgnoreCase) OrElse
            Address.Host.Equals("edge.forgecdn.net", StringComparison.OrdinalIgnoreCase)
    End Function

    Public Shared Sub ApplyHeaders(Address As Uri, Request As HttpRequestMessage)
        If Not IsAuthenticatedHost(Address) Then Return
        If Address.Scheme <> Uri.UriSchemeHttps Then Throw New InvalidOperationException("CurseForge 鉴权请求必须使用 HTTPS。")
        Dim Key = GetApiKey()
        If String.IsNullOrWhiteSpace(Key) Then Throw New InvalidOperationException(MissingKeyMessage)
        If Key.Contains(vbCr) OrElse Key.Contains(vbLf) Then Throw New InvalidOperationException("CurseForge API Key 格式错误，请重新保存。")
        Request.Headers.Remove("x-api-key")
        Request.Headers.Add("x-api-key", Key)
    End Sub

    Public Shared Function GetFailureHint(ErrorInfo As Exception) As String
        Dim SawForbidden = False
        Dim SawMirrorDown = False
        Dim Pending As New Queue(Of Exception)
        Pending.Enqueue(ErrorInfo)
        While Pending.Any()
            Dim Current = Pending.Dequeue()
            If Current Is Nothing Then Continue While
            If TypeOf Current Is HttpRequestCodeException Then
                Dim CodeError = DirectCast(Current, HttpRequestCodeException)
                Select Case CodeError.StatusCode
                    Case HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden
                        SawForbidden = True
                    Case CType(429, HttpStatusCode)
                        Return "CurseForge 请求过于频繁（429），请稍后重试。"
                    Case HttpStatusCode.ServiceUnavailable, HttpStatusCode.BadGateway, HttpStatusCode.GatewayTimeout
                        If CodeError.Message.Contains("mcimirror") Then SawMirrorDown = True
                End Select
            ElseIf Current.Message IsNot Nothing AndAlso Current.Message.Contains("mcimirror") AndAlso
                   (Current.Message.Contains("503") OrElse Current.Message.Contains("502") OrElse Current.Message.Contains("504")) Then
                SawMirrorDown = True
            End If
            If TypeOf Current Is AggregateException Then
                For Each Inner In DirectCast(Current, AggregateException).InnerExceptions
                    Pending.Enqueue(Inner)
                Next
            ElseIf Current.InnerException IsNot Nothing Then
                Pending.Enqueue(Current.InnerException)
            End If
        End While
        If String.IsNullOrWhiteSpace(GetApiKey()) Then
            If SawMirrorDown Then Return MissingKeyMessage & "备用镜像当前也不可用，所以 CurseForge 暂时连不上。"
            Return MissingKeyMessage
        End If
        If SawForbidden Then Return "CurseForge 拒绝了访问（401/403）。请检查 API Key 是否有效，以及是否获准访问 Minecraft 资源。"
        If SawMirrorDown Then Return "CurseForge 备用镜像暂时不可用，官方接口也没有成功。请确认 API Key 后重试，或改用 Modrinth。"
        Return "无法连接到 CurseForge，请检查网络或代理设置后重试。"
    End Function
End Class
