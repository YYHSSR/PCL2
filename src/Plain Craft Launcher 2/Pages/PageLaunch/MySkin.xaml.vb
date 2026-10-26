Public Class MySkin

    'Events
    Public Event Click(sender As Object, e As MouseButtonEventArgs)

    'Skin storage
    Private _Address As String
    Public Property Address As String
        Get
            Return _Address
        End Get
        Set(value As String)
            _Address = value
            ToolTip = If(_Address = "", "加载中", "点击更换皮肤（右键查看更多选项）")
        End Set
    End Property
    Public Loader As LoaderTask(Of (String, String), String)

    'Control animation
    Private Sub PanSkin_MouseEnter(sender As Object, e As MouseEventArgs) Handles Me.MouseEnter
        AniStart(AaOpacity(ShadowSkin, 0.8 - ShadowSkin.Opacity, 200, 100), "Skin Shadow")
    End Sub
    Private Sub PanSkin_MouseLeave(sender As Object, e As MouseEventArgs) Handles Me.MouseLeave
        AniStart(AaOpacity(ShadowSkin, 0.2 - ShadowSkin.Opacity, 200), "Skin Shadow")
        IsSkinMouseDown = False
        AniStart(AaScaleTransform(Me, 1 - CType(Me.RenderTransform, ScaleTransform).ScaleX, 60,, New AniEaseOutFluent), "Skin Scale")
    End Sub

    'Click
    Private IsSkinMouseDown As Boolean = False
    Private Sub PanSkin_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonDown
        IsSkinMouseDown = True
        AniStart(AaScaleTransform(Me, 0.9 - CType(Me.RenderTransform, ScaleTransform).ScaleX, 60,, New AniEaseOutFluent), "Skin Scale")
    End Sub
    Private Sub PanSkin_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonUp
        AniStart(AaScaleTransform(Me, 1 - CType(Me.RenderTransform, ScaleTransform).ScaleX, 60,, New AniEaseOutFluent), "Skin Scale")
        If IsSkinMouseDown Then
            IsSkinMouseDown = False
            RaiseEvent Click(sender, e)
        End If
    End Sub

    'Save skin
    Public Sub BtnSkinSave_Click() Handles BtnSkinSave.Click
        Save(Loader)
    End Sub
    Public Shared Sub Save(Loader As LoaderTask(Of (String, String), String))
        Dim Address = Loader.Output
        If Not Loader.State = LoadState.Finished Then
            Hint("皮肤正在获取中，请稍候！", HintType.Red)
            If Not Loader.State = LoadState.Loading Then Loader.Start()
            Return
        End If
        Try
            Dim FileAddress As String = Dialogs.SaveFile("选取保存皮肤的位置", PathUtils.GetLastPart(Address), filter:={("png", "皮肤图片文件")})
            If FileAddress Is Nothing Then Return
            If FileAddress.Contains("\") Then
                FileUtils.Delete(FileAddress)
                If Address.StartsWithF(PathImage) Then
                    Dim Image As New MyBitmap(Address)
                    Image.Save(FileAddress)
                Else
                    FileUtils.Copy(Address, FileAddress)
                End If
                Hint("皮肤保存成功！", HintType.Green)
            End If
        Catch ex As Exception
            Logger.Error(ex, "保存皮肤失败", LogBehavior.Toast)
        End Try
    End Sub
    Private Sub BtnSkinSave_Checked(sender As MyMenuItem, e As RoutedEventArgs) Handles BtnSkinSave.Checked
        sender.IsEnabled = Address = ""
    End Sub

    ''' <summary>
    ''' Load the skin.
    ''' </summary>
    Public Sub Load()
        Try
            'Check that the file exists
            Address = Loader.Output
            If String.IsNullOrEmpty(Address) Then Throw New Exception("皮肤加载器 " & Loader.Name & " 没有输出")
            If Not Address.StartsWithF(PathImage) AndAlso Not FileUtils.Exists(Address) Then Throw New FileNotFoundException("皮肤文件未找到", Address)
            'Load
            Dim Image As MyBitmap
            Try
                Image = New MyBitmap(Address)
            Catch ex As Exception '#2272
                Logger.Error(ex, $"皮肤文件已损坏：{Address}", LogBehavior.Toast)
                FileUtils.Delete(Address)
                Return
            End Try
            ImgBack.Tag = Address
            'Size check
            Dim Scale As Integer = Image.Pic.Width / 64
            If Image.Pic.Width < 32 OrElse Image.Pic.Height < 32 Then
                ImgFore.Source = Nothing : ImgBack.Source = Nothing
                Throw New Exception("图片大小不足，长为 " & Image.Pic.Height & "，宽为 " & Image.Pic.Width)
            End If
            'Hair layer (overlay)
            If Image.Pic.Width >= 64 AndAlso Image.Pic.Height >= 32 Then
                If (Image.Pic.GetPixel(1, 1).A = 0 OrElse 'If the image has any transparent pixel (skip a solid white background)
                    Image.Pic.GetPixel(Image.Pic.Width - 1, Image.Pic.Height - 1).A = 0 OrElse
                    Image.Pic.GetPixel(Image.Pic.Width - 2, Image.Pic.Height / 2 - 2).A = 0) OrElse
                   (Image.Pic.GetPixel(1, 1) <> Image.Pic.GetPixel(Scale * 41, Scale * 9) AndAlso 'Or the head color differs from the transparent area
                    Image.Pic.GetPixel(Image.Pic.Width - 1, Image.Pic.Height - 1) <> Image.Pic.GetPixel(Scale * 41, Scale * 9) AndAlso
                    Image.Pic.GetPixel(Image.Pic.Width - 2, Image.Pic.Height / 2 - 2) <> Image.Pic.GetPixel(Scale * 41, Scale * 9)) Then
                    ImgFore.Source = Image.Clip(Scale * 40, Scale * 8, Scale * 8, Scale * 8)
                Else
                    ImgFore.Source = Nothing
                End If
            Else
                ImgFore.Source = Nothing
            End If
            'Face layer
            ImgBack.Source = Image.Clip(Scale * 8, Scale * 8, Scale * 8, Scale * 8)
            Logger.Info($"载入头像成功：{Loader.Name}")
        Catch ex As Exception
            Logger.Error(ex, $"载入头像失败（{If(Address, "null")},{Loader.Name}）", LogBehavior.Toast)
        End Try
    End Sub
    ''' <summary>
    ''' Clear the skin.
    ''' </summary>
    Public Sub Clear()
        Address = ""
        ImgFore.Source = Nothing
        ImgBack.Source = Nothing
    End Sub

    'Refresh the cache
    Public Sub RefreshClick() Handles BtnSkinRefresh.Click
        RefreshCache(Loader)
    End Sub
    ''' <summary>
    ''' Refresh the skin cache.
    ''' </summary>
    Public Shared Sub RefreshCache(Optional sender As LoaderTask(Of (String, String), String) = Nothing)
        Dim HasLoaderRunning As Boolean = False
        For Each SkinLoader In PageLaunchLeft.SkinLoaders
            If SkinLoader.State = LoadState.Loading Then
                HasLoaderRunning = True : Exit For
            End If
        Next
        If FrmLaunchLeft IsNot Nothing AndAlso HasLoaderRunning Then
            'Cancel is not immediate, so the file stays locked and delete would fail; abort instead
            Hint("有正在获取中的皮肤，请稍后再试！", HintType.Blue)
        Else
            RunInThread(
            Sub()
                Try
                    Hint("正在刷新皮肤……")
                    'Clear the cache
                    Logger.Info("正在清空皮肤缓存")
                    If DirectoryUtils.Exists(PathTemp & "Cache\Skin") Then DirectoryUtils.Delete(PathTemp & "Cache\Skin")
                    If DirectoryUtils.Exists(PathTemp & "Cache\Uuid") Then DirectoryUtils.Delete(PathTemp & "Cache\Uuid")
                    IniClearCache(PathTemp & "Cache\Skin\IndexMs.ini")
                    IniClearCache(PathTemp & "Cache\Skin\IndexNide.ini")
                    IniClearCache(PathTemp & "Cache\Skin\IndexAuth.ini")
                    IniClearCache(PathTemp & "Cache\Uuid\Mojang.ini")
                    'Refresh the controls
                    For Each SkinLoader In If(sender IsNot Nothing, {sender}, {PageLaunchLeft.SkinLegacy, PageLaunchLeft.SkinMs})
                        SkinLoader.WaitForExit(IsForceRestart:=True)
                    Next
                    Hint("已刷新皮肤！", HintType.Green)
                Catch ex As Exception
                    Logger.Error(ex, "刷新皮肤缓存失败", LogBehavior.Alert)
                End Try
            End Sub)
        End If
    End Sub
    ''' <summary>
    ''' After changing the Microsoft skin, refresh it.
    ''' </summary>
    ''' <param name="SkinAddress">Full URL of the new Microsoft skin.</param>
    Public Shared Sub ReloadCache(SkinAddress As String)
        RunInThread(
        Sub()
            Try
                'Update the cache
                WriteIni(PathTemp & "Cache\Skin\IndexMs.ini", Settings.Get(Of String)("CacheMsV2Uuid"), SkinAddress)
                Logger.Info($"已写入皮肤地址缓存 {Settings.Get(Of String)("CacheMsV2Uuid")} -> {SkinAddress}")
                'Refresh the controls
                For Each SkinLoader In {PageLaunchLeft.SkinMs, PageLaunchLeft.SkinLegacy}
                    SkinLoader.WaitForExit(IsForceRestart:=True)
                Next
                'Show the success hint
                Hint("更改皮肤成功！", HintType.Green)
            Catch ex As Exception
                Logger.Error(ex, "更改正版皮肤后刷新皮肤失败")
            End Try
        End Sub)
    End Sub

    'Cape
    Public Property HasCape As Boolean
        Get
            Return BtnSkinCape.Visibility = Visibility.Collapsed
        End Get
        Set(value As Boolean)
            If value Then
                BtnSkinCape.Visibility = Visibility.Visible
            Else
                BtnSkinCape.Visibility = Visibility.Collapsed
            End If
        End Set
    End Property
    Private IsChanging As Boolean = False
    Public Sub BtnSkinCape_Click() Handles BtnSkinCape.Click
        'Check conditions and get the new cape
        If IsChanging Then
            Hint("正在更改披风中，请稍候！")
            Return
        End If
        If McLoginMsLoader.State = LoadState.Failed Then
            Hint("登录失败，无法更改披风！", HintType.Red)
            Return
        End If
        If McLoginMsLoader.State <> LoadState.Finished Then Hint("正在获取披风列表，请稍候……")
        IsChanging = True
        'Start the actual fetch
        RunInNewThread(
        Sub()
Retry:
            Try
                'Get the login info
                If McLoginMsLoader.State <> LoadState.Finished Then McLoginMsLoader.WaitForExit(PageLoginMsSkin.GetLoginData())
                If McLoginMsLoader.State <> LoadState.Finished Then
                    Hint("登录失败，无法更改披风！", HintType.Red)
                    Return
                End If
                Dim AccessToken As String = McLoginMsLoader.Output.AccessToken
                Dim Uuid As String = McLoginMsLoader.Output.Uuid
                Dim SkinData As JObject = McLoginMsLoader.Output.ProfileJson.DeserializeJson()
                'Get all of the player's capes
                Dim SelectedIndex As Integer? = Nothing
                RunInUiWait(
                Sub()
                    Try
                        Dim CapeNames As New Dictionary(Of String, String) From {
                            {"Migrator", "迁移者披风"}, {"MapMaker", "Realms 地图制作者披风"}, {"Moderator", "Mojira 管理员披风"},
                            {"Translator-Chinese", "Crowdin 中文翻译者披风"}, {"Translator", "Crowdin 翻译者披风"}, {"Cobalt", "Cobalt 披风"},
                            {"Vanilla", "原版披风"}, {"Minecon2011", "Minecon 2011 参与者披风"}, {"Minecon2012", "Minecon 2012 参与者披风"},
                            {"Minecon2013", "Minecon 2013 参与者披风"}, {"Minecon2015", "Minecon 2015 参与者披风"}, {"Minecon2016", "Minecon 2016 参与者披风"},
                            {"Cherry Blossom", "樱花披风"}, {"15th Anniversary", "15 周年纪念披风"}, {"Purple Heart", "紫色心形披风"},
                            {"Follower's", "追随者披风"}, {"MCC 15th Year", "MCC 15 周年披风"}, {"Minecraft Experience", "村民救援披风"},
                            {"Mojang Office", "Mojang 办公室披风"}, {"Home", "家园披风"}, {"Menace", "入侵披风"}, {"Yearn", "渴望披风"},
                            {"Common", "普通披风"}, {"Pan", "薄煎饼披风"}, {"Founder's", "创始人披风"}, {"Copper", "铜披风"}, {"Zombie Horse", "僵尸马披风"}, {"Builder", "建造者披风"}, {"Crafter", "工匠披风"},
                            {"Aurora", "极光披风"}
                        }
                        Dim SelectionControl As New List(Of IMyRadio) From {
                            New MyRadioBox With {.Text = "无披风", .Checked = Not SkinData("capes").Any(Function(c) c("state")?.ToString = "ACTIVE")}}
                        For Each Cape In SkinData("capes")
                            Dim CapeName As String = Cape("alias").ToString
                            If CapeNames.ContainsKey(CapeName) Then CapeName = CapeNames(CapeName)
                            SelectionControl.Add(New MyRadioBox With {.Text = CapeName, .Checked = Cape("state")?.ToString = "ACTIVE"})
                        Next
                        SelectedIndex = MyMsgBoxSelect(SelectionControl, "选择披风", "确定", "取消")
                    Catch ex As Exception
                        Logger.Error(ex, "获取玩家皮肤列表失败")
                    End Try
                End Sub)
                If SelectedIndex Is Nothing Then Return
                'Send the request
                Dim Result As String = NetRequestByClientRetry("https://api.minecraftservices.com/minecraft/profile/capes/active",
                    If(SelectedIndex = 0, HttpMethod.Delete, HttpMethod.Put),
                    Content:=If(SelectedIndex = 0, "", New JObject(New JProperty("capeId", SkinData("capes")(SelectedIndex - 1)("id"))).ToString(0)),
                    ContentType:="application/json",
                    Headers:={{"Authorization", "Bearer " & AccessToken}})
                If Result.Contains("""errorMessage""") Then
                    Hint("更改披风失败：" & Result.DeserializeJson()("errorMessage").ToString, HintType.Red)
                    Return
                Else
                    Hint("更改披风成功！", HintType.Green)
                    'Update the currently selected cape
                    For Each Cape In SkinData("capes")
                        Cape("state") = "INACTIVE"
                    Next
                    If SelectedIndex > 0 Then SkinData("capes")(SelectedIndex - 1)("state") = "ACTIVE"
                    McLoginMsLoader.Output.ProfileJson = SkinData.ToString()
                    Settings.Set("CacheMsV2ProfileJson", McLoginMsLoader.Output.ProfileJson)
                End If
            Catch ex As Exception
                If TypeOf ex Is HttpRequestCodeException Then
                    Dim requestException As HttpRequestCodeException = CType(ex, HttpRequestCodeException)
                    Select Case requestException.StatusCode
                        Case HttpStatusCode.BadRequest
                            Logger.Warn(ex, "更改披风时遭遇 400 错误")
                            If requestException.Response?.Contains("""error""") Then
                                Hint("更改披风失败：" & requestException.Response.DeserializeJson()("error").ToString, HintType.Red)
                                Return
                            ElseIf requestException.Response?.Contains("""errorMessage""") Then
                                Hint("更改披风失败：" & requestException.Response.DeserializeJson()("errorMessage").ToString, HintType.Red)
                                Return
                            End If
                        Case HttpStatusCode.Unauthorized
                            Logger.Warn(ex, "更改披风时遭遇 401 错误")
                            Hint("正在重新登录，将在登录后自动更改披风……")
                            McLoginMsLoader.Start(PageLoginMsSkin.GetLoginData(), IsForceRestart:=True)
                            GoTo Retry
                    End Select
                ElseIf ex.IsBadNetwork Then
                    Hint("更改披风失败：连接 Mojang 服务器超时，请稍后再试，或使用 VPN 改善网络环境", HintType.Red)
                Else
                    Logger.Error(ex, "更改披风失败", LogBehavior.Toast)
                End If
            Finally
                IsChanging = False
            End Try
        End Sub, "Cape Change")
    End Sub

End Class
