Imports System.Security.Cryptography

Public Class Settings

    Public Shared ReadOnly Entries As Dictionary(Of String, Setting) = (New List(Of Setting) From {
        New Setting("Identify", "", Source:=Sources.Registry),
        New Setting("WindowHeight", 550),
        New Setting("WindowWidth", 900),
        New Setting("HintDownloadThread", False, Source:=Sources.Registry),
        New Setting("ToolCurseForgeApiKey", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("HintDownload", 0, Source:=Sources.Registry),
        New Setting("HintBuy", False, Source:=Sources.Registry),
        New Setting("HintUpdateMod", False, Source:=Sources.Registry),
        New Setting("HintCustomCommand", False, Source:=Sources.Registry),
        New Setting("HintMoreAdvancedSetup", False, Source:=Sources.Registry),
        New Setting("HintIndieSetup", False, Source:=Sources.Registry),
        New Setting("HintExportConfig", False, Source:=Sources.Registry),
        New Setting("SystemCount", 0, Source:=Sources.Registry, Encrypted:=True),
        New Setting("SystemLaunchCount", 0, Source:=Sources.Registry, Encrypted:=True),
        New Setting("SystemLastVersionReg", 0, Source:=Sources.Registry, Encrypted:=True),
        New Setting("SystemHighestBetaVersionReg", 0, Source:=Sources.Registry, Encrypted:=True),
        New Setting("SystemHighestAlphaVersionReg", 0, Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheDrops", "", Source:=Sources.Registry),
        New Setting("CacheExportConfig", "", Source:=Sources.Registry),
        New Setting("CacheMsOAuthRefresh", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsAccess", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsProfileJson", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsUuid", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsName", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsV2Migrated", False, Source:=Sources.Registry),
        New Setting("CacheMsV2OAuthRefresh", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsV2Access", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsV2ProfileJson", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsV2Uuid", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsV2Name", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheMsV2Expires", 0L, Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheNideAccess", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheNideClient", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheNideUuid", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheNideName", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheNideUsername", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheNidePass", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheNideServer", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthAccess", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthClient", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthUuid", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthName", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthUsername", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthPass", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthServerServer", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthServerName", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("CacheAuthServerRegister", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("LoginRemember", True, Source:=Sources.Registry, Encrypted:=True),
        New Setting("LoginLegacyName", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("LoginMsJson", "{}", Source:=Sources.Registry, Encrypted:=True), '{UserName: OAuthToken, ...}
        New Setting("LoginNideEmail", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("LoginNidePass", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("LoginAuthEmail", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("LoginAuthPass", "", Source:=Sources.Registry, Encrypted:=True),
        New Setting("LoginType", McLoginType.Legacy, Source:=Sources.Registry),
        New Setting("LoginPageType", 0),
        New Setting("LaunchFolderSelect", ""),
        New Setting("LaunchFolders", "", Source:=Sources.Registry),
        New Setting("LaunchArgumentTitle", ""),
        New Setting("LaunchArgumentInfo", "PCL"),
        New Setting("LaunchArgumentJavaSelect", "", Source:=Sources.Registry),
        New Setting("LaunchArgumentJavaAll", "[]", Source:=Sources.Registry),
        New Setting("LaunchArgumentIndie", 0),
        New Setting("LaunchArgumentIndieV2", 4),
        New Setting("LaunchArgumentVisible", 5, Source:=Sources.Registry),
        New Setting("LaunchArgumentPriority", 1, Source:=Sources.Registry),
        New Setting("LaunchArgumentWindowWidth", 854),
        New Setting("LaunchArgumentWindowHeight", 480),
        New Setting("LaunchArgumentWindowType", 1),
        New Setting("LaunchAdvanceJvm", ""),
        New Setting("LaunchOptUseG1", True, Source:=Sources.Registry),
        New Setting("LaunchOptG1Tune", False, Source:=Sources.Registry),
        New Setting("LaunchOptPreTouch", False, Source:=Sources.Registry),
        New Setting("LaunchOptStringDedup", False, Source:=Sources.Registry),
        New Setting("LaunchOptDisableSharedMem", True, Source:=Sources.Registry),
        New Setting("LaunchOptFixUnicodePath", True, Source:=Sources.Registry),
        New Setting("LaunchOptFixLwjgl", True, Source:=Sources.Registry),
        New Setting("LaunchAdvanceGame", ""),
        New Setting("LaunchAdvanceRun", ""),
        New Setting("LaunchAdvanceRunWait", True),
        New Setting("LaunchAdvanceDisableJLW", False),
        New Setting("LaunchAdvanceDisableLUA", False),
        New Setting("LaunchAdvanceGraphicCard", True, Source:=Sources.Registry),
        New Setting("LaunchRamType", 0, OnChanged:=AddressOf PageSetupLaunch.UpdateRamType),
        New Setting("LaunchRamCustom", 15),
        New Setting("ToolDownloadThread", 63, Source:=Sources.Registry),
        New Setting("ToolDownloadSpeed", 42, Source:=Sources.Registry, OnChanged:=AddressOf ModNet.UpdateNetTaskSpeedLimitHigh),
        New Setting("ToolDownloadSource", 1, Source:=Sources.Registry),
        New Setting("ToolDownloadVersion", 1, Source:=Sources.Registry),
        New Setting("ToolDownloadTranslate", 0, Source:=Sources.Registry),
        New Setting("ToolDownloadTranslateV2", 1, Source:=Sources.Registry),
        New Setting("ToolDownloadMod", 2, Source:=Sources.Registry),
        New Setting("ToolModLocalNameStyle", 0, Source:=Sources.Registry),
        New Setting("VersionAdvanceJvm", "", Source:=Sources.Instance),
        New Setting("VersionAdvanceGame", "", Source:=Sources.Instance),
        New Setting("VersionAdvanceAssets", 0, Source:=Sources.Instance),
        New Setting("VersionAdvanceAssetsV2", False, Source:=Sources.Instance),
        New Setting("VersionAdvanceRun", "", Source:=Sources.Instance),
        New Setting("VersionAdvanceRunWait", True, Source:=Sources.Instance),
        New Setting("VersionAdvanceDisableJLW", False, Source:=Sources.Instance),
        New Setting("VersionAdvanceDisableLUA", False, Source:=Sources.Instance),
        New Setting("VersionAdvanceDisableModUpdate", False, Source:=Sources.Instance),
        New Setting("VersionAdvanceGC", 0, Source:=Sources.Instance),
        New Setting("VersionRamType", 2, Source:=Sources.Instance, OnChanged:=AddressOf PageInstanceSetup.OnVersionRamTypeChanged),
        New Setting("VersionRamCustom", 15, Source:=Sources.Instance),
        New Setting("VersionArgumentTitle", "", Source:=Sources.Instance),
        New Setting("VersionArgumentInfo", "", Source:=Sources.Instance),
        New Setting("VersionArgumentIndie", -1, Source:=Sources.Instance),
        New Setting("VersionArgumentIndieV2", False, Source:=Sources.Instance),
        New Setting("VersionArgumentJavaSelect", "使用全局设置", Source:=Sources.Instance),
        New Setting("VersionArgumentJavaV2", 0, Source:=Sources.Instance),
        New Setting("VersionArgumentJavaRange", "", Source:=Sources.Instance),
        New Setting("VersionServerEnter", "", Source:=Sources.Instance),
        New Setting("VersionServerLogin", 0, Source:=Sources.Instance, OnChanged:=AddressOf PageInstanceSetup.OnVersionServerLoginChanged),
        New Setting("VersionServerNide", "", Source:=Sources.Instance),
        New Setting("VersionServerAuthRegister", "", Source:=Sources.Instance),
        New Setting("VersionServerAuthName", "", Source:=Sources.Instance),
        New Setting("VersionServerAuthServer", "", Source:=Sources.Instance)
    }).ToDictionary(Function(e) e.Key)

    Public Enum Sources
        Normal
        Registry
        Instance
    End Enum
    Public Class Setting
        Public Key As String
        Public Encrypted As Boolean
        Public DefaultValue As Object
        Public Source As Sources
        ''' <summary>
        ''' Raised when the setting's value actually changes. The argument is the new value.
        ''' </summary>
        Public OnChanged As Action(Of Object)
        ''' <summary>
        ''' Actual type of this setting's value.
        ''' </summary>
        Public Type As Type
        Public Sub New(Key As String, Value As Object, Optional Source As Sources = Sources.Normal, Optional Encrypted As Boolean = False, Optional OnChanged As Action(Of Object) = Nothing)
            Try
                Me.Key = Key
                Me.DefaultValue = Value
                Me.Encrypted = Encrypted
                Me.Source = Source
                Me.Type = If(Value, "").GetType
                Me.OnChanged = OnChanged
            Catch ex As Exception
                Logger.Error(ex, "初始化设置项失败") '#5095 fallback
            End Try
        End Sub

        ''' <summary>
        ''' Cache of values that have been read. Nothing if never read.
        ''' For a per-version setting the key is the version path; otherwise the key is an empty string.
        ''' </summary>
        Public ValueCache As New ConcurrentDictionary(Of String, Object)
        Public Function GetCache(Instance As McInstance)
            Dim Key As String = If(Source = Sources.Instance, Instance.PathVersion, "")
            Dim Result = Nothing
            Return If(ValueCache.TryGetValue(Key, Result), Result, Nothing)
        End Function
        Public Sub [Set](Value As Object, Instance As McInstance)
            Dim Key As String = If(Source = Sources.Instance, Instance.PathVersion, "")
            ValueCache(Key) = Value
        End Sub

        ''' <summary>
        ''' Write the current Value to the matching registry key or file immediately.
        ''' </summary>
        Public Sub Save(Optional Instance As McInstance = Nothing)
            Dim Value As String = GetCache(Instance)
            Logger.Trace($"保存设置：{Key} {If(Encrypted, "", "= " & Value)}{If(Instance Is Nothing, "", $"（实例：{Instance?.PathVersion}）")}")
            If Encrypted Then Value = ProtectSetting(If(Value, ""), Key)
            Select Case Source
                Case Sources.Normal
                    WriteIni("Setup", Key, Value)
                Case Sources.Registry
                    Try
                        RegistryUtils.Write($"HKEY_CURRENT_USER\Software\{RegFolder}", Key, Value)
                    Catch ex As Exception
                        Logger.Warn(ex, $"写入注册表失败（{Key}）", LogBehavior.Toast)
                    End Try
                Case Sources.Instance
                    If Instance Is Nothing Then Throw New Exception($"保存版本独立设置 {Key} 时未提供目标版本")
                    WriteIni(Instance.PathVersion & "PCL\Setup.ini", Key, Value)
            End Select
        End Sub

    End Class

    ''' <summary>
    ''' Change a setting's value.
    ''' </summary>
    Public Shared Sub [Set](Key As String, Value As Object, Optional Instance As McInstance = Nothing)
        Dim Entry As Setting = Nothing
        If Not Entries.TryGetValue(Key, Entry) Then Throw New KeyNotFoundException("未找到设置项：" & Key)
        Try
            'Check whether the values match
            Value = CTypeDynamic(Value, Entry.Type)
            If Entry.GetCache(Instance) Is Nothing Then [Get](Key, Instance)
            If Entry.GetCache(Instance) = Value Then Return
            'Set the new value
            Dim PreviousValue = Entry.GetCache(Instance)
            Entry.Set(Value, Instance)
            Try
                Entry.Save(Instance)
            Catch
                Entry.Set(PreviousValue, Instance)
                Throw
            End Try
            'Raise the change event after saving, so handlers such as VersionServerLogin read the latest value
            If Entry.OnChanged IsNot Nothing Then Entry.OnChanged.Invoke(Value)
        Catch ex As Exception
            Logger.Error(ex, $"设置设置项时出错（{Key}）")
        End Try
    End Sub
    ''' <summary>
    ''' Write a setting that is not encrypted.
    ''' Throws if the setting is encrypted.
    ''' </summary>
    Public Shared Sub SetSafe(Key As String, Value As Object, Optional Instance As McInstance = Nothing)
        Dim Entry As Setting = Nothing
        If Not Entries.TryGetValue(Key, Entry) Then Throw New KeyNotFoundException("未找到设置项：" & Key)
        If Entry.Encrypted Then Throw New InvalidOperationException("禁止写入加密设置项：" & Key)
        [Set](Key, Value, Instance)
    End Sub

    ''' <summary>
    ''' Get a setting's value.
    ''' </summary>
    Public Shared Function [Get](Key As String, Optional Instance As McInstance = Nothing)
        Dim Entry As Setting = Nothing
        If Not Entries.TryGetValue(Key, Entry) Then Throw New KeyNotFoundException("未找到设置项：" & Key)
        Dim Value = Entry.GetCache(Instance)
        If Value IsNot Nothing Then Return Value
        'Normal read
        Try
            Dim GotValue As String = Nothing 'Store as String first to avoid a type conversion
            Dim DefaultValue As String = Entry.DefaultValue
            Dim NeedsMigration As Boolean = False
            Select Case Entry.Source
                Case Sources.Normal
                    GotValue = ReadIni("Setup", Key, DefaultValue)
                Case Sources.Registry
                    GotValue = RegistryUtils.TryRead($"HKEY_CURRENT_USER\Software\{RegFolder}", Key, DefaultValue)
                Case Sources.Instance
                    If Instance Is Nothing Then
                        Throw New Exception($"读取版本设置 {Key} 时未提供目标版本")
                    Else
                        GotValue = ReadIni(Instance.PathVersion & "PCL\Setup.ini", Key, DefaultValue)
                    End If
            End Select
            If Entry.Encrypted Then
                If GotValue = DefaultValue Then
                    GotValue = Entry.DefaultValue
                Else
                    Try
                        NeedsMigration = Not GotValue.StartsWith(ProtectedSettingPrefix, StringComparison.Ordinal)
                        GotValue = If(NeedsMigration, DesDecrypt(GotValue, "PCL" & Identify), UnprotectSetting(GotValue, Key))
                    Catch ex As Exception
                        Logger.Warn(ex, $"解密设置失败，保留原始缓存：{Key}")
                        GotValue = Entry.DefaultValue
                        NeedsMigration = False
                    End Try
                End If
            End If
            Entry.Set(CTypeDynamic(GotValue, Entry.Type), Instance)
            If NeedsMigration Then Entry.Save(Instance)
        Catch ex As Exception
            Logger.Error(ex, $"读取设置失败：{Key}", LogBehavior.Toast)
            Entry.Set(CTypeDynamic(Entry.DefaultValue, Entry.Type), Instance)
        End Try
        Return Entry.GetCache(Instance)
    End Function
    Public Shared Function [Get](Of T)(Key As String, Optional Instance As McInstance = Nothing) As T
        Return [Get](Key, Instance)
    End Function
    ''' <summary>
    ''' Get a setting that is not encrypted.
    ''' Throws if the setting is encrypted.
    ''' </summary>
    Public Shared Function GetSafe(Key As String, Optional Instance As McInstance = Nothing)
        Dim Entry As Setting = Nothing
        If Not Entries.TryGetValue(Key, Entry) Then Throw New KeyNotFoundException("未找到设置项：" & Key)
        If Entry.Encrypted Then Throw New InvalidOperationException("禁止读取加密设置项：" & Key)
        Return [Get](Key, Instance)
    End Function

    ''' <summary>
    ''' Reset a setting to its default.
    ''' </summary>
    Public Shared Sub Reset(Key As String, Optional Instance As McInstance = Nothing)
        Dim Entry As Setting = Nothing
        If Not Entries.TryGetValue(Key, Entry) Then Throw New KeyNotFoundException("未找到设置项：" & Key)
        Try
            'Check whether the values match
            If Entry.GetCache(Instance) Is Nothing Then [Get](Key, Instance)
            If Entry.GetCache(Instance) = Entry.DefaultValue Then Return
            'Set the new value
            Entry.Set(Entry.DefaultValue, Instance)
            Select Case Entry.Source
                Case Sources.Normal
                    DeleteIniKey("Setup", Key)
                Case Sources.Registry
                    Try
                        RegistryUtils.Delete($"HKEY_CURRENT_USER\Software\{RegFolder}", Key)
                    Catch ex As Exception
                        Logger.Warn(ex, $"删除注册表失败（{Key}）", LogBehavior.Toast)
                    End Try
                Case Sources.Instance
                    If Instance Is Nothing Then Throw New Exception($"重置版本设置 {Key} 时未提供目标版本")
                    DeleteIniKey(Instance.PathVersion & "PCL\Setup.ini", Key)
            End Select
            'Raise the change event
            If Entry.OnChanged IsNot Nothing Then Entry.OnChanged.Invoke(Entry.DefaultValue)
        Catch ex As Exception
            Logger.Error(ex, $"重置设置项时出错（{Key}）")
        End Try
    End Sub

    ''' <summary>
    ''' Get a setting's default value.
    ''' </summary>
    Public Shared Function GetDefault(Key As String) As String
        Return Entries(Key).DefaultValue
    End Function

    ''' <summary>
    ''' Whether the registry or file already stores this setting.
    ''' Matching the default was stored in 2.12.3 and earlier, and is not stored in 2.12.4 and later.
    ''' </summary>
    Public Shared Function HasSaved(Key As String, Optional Instance As McInstance = Nothing) As Boolean
        Select Case Entries(Key).Source
            Case Sources.Normal
                Return HasIniKey("Setup", Key)
            Case Sources.Registry
                Try
                    Return RegistryUtils.Has($"HKEY_CURRENT_USER\Software\{RegFolder}", Key)
                Catch ex As Exception
                    Logger.Warn(ex, $"判断注册表 {Key} 是否存在时出错")
                    Return False
                End Try
            Case Else 'Source.Instance
                If Instance Is Nothing Then Throw New Exception($"判断版本设置 {Key} 是否存在时未提供目标版本")
                Return HasIniKey(Instance.PathVersion & "PCL\Setup.ini", Key)
        End Select
    End Function

#Region "Credential protection and legacy cache migration"

    Private Shared ReadOnly desInitialVector As Byte() = Encoding.UTF8.GetBytes("95168702")

    ''' <summary>
    ''' Encrypt a string with the DES symmetric algorithm.
    ''' </summary>
    Private Const ProtectedSettingPrefix As String = "dpapi:v1:"

    Private Shared Function ProtectSetting(Value As String, Key As String) As String
        Dim Data = ProtectedData.Protect(Encoding.UTF8.GetBytes(Value), Encoding.UTF8.GetBytes("PCL2:" & Key), DataProtectionScope.CurrentUser)
        Return ProtectedSettingPrefix & Convert.ToBase64String(Data)
    End Function

    Private Shared Function UnprotectSetting(Value As String, Key As String) As String
        Dim Data = Convert.FromBase64String(Value.Substring(ProtectedSettingPrefix.Length))
        Return Encoding.UTF8.GetString(ProtectedData.Unprotect(Data, Encoding.UTF8.GetBytes("PCL2:" & Key), DataProtectionScope.CurrentUser))
    End Function

    ''' <summary>
    ''' Decrypt a string with the DES symmetric algorithm.
    ''' </summary>
    Private Shared Function DesDecrypt(encryptedString As String, Optional key As String = Nothing) As String
        key = If(key Is Nothing, "@;$ Abv2", key.GetStableHashCode().ToString().EnsureLength("X"c, 8).Substring(0, 8))
        Dim btKey As Byte() = Encoding.UTF8.GetBytes(key)
        Using des As New DESCryptoServiceProvider()
            Using ms As New MemoryStream()
                Using cs As New CryptoStream(ms, des.CreateDecryptor(btKey, desInitialVector), CryptoStreamMode.Write)
                    Dim inData As Byte() = Convert.FromBase64String(encryptedString)
                    cs.Write(inData, 0, inData.Length)
                    cs.FlushFinalBlock()
                    Return Encoding.UTF8.GetString(ms.ToArray())
                End Using
            End Using
        End Using
    End Function

#End Region

End Class
