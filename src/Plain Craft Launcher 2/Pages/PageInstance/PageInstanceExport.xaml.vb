Public Class ExportOption
    Public Property Title As String
    Public Property Description As String
    Public Property Rules As String
    ''' <summary>
    ''' If Rules is empty, ShowRules decides whether to show this checkbox.
    ''' If ShowRules is also empty, always show it.
    ''' </summary>
    Public Property ShowRules As String
    Public Property DefaultChecked As Boolean
    Public Property RequireModLoader As Boolean = False
    Public Property RequireOptiFine As Boolean = False
    Public Property RequireModLoaderOrOptiFine As Boolean = False
End Class

Public Class PageInstanceExport
    Implements IRefreshable

    Private CurrentInstance As String = ""
    Private Sub PageInstanceExport_Initialized(sender As Object, e As EventArgs) Handles Me.Initialized
        AddHandler PageInstanceLeft.CurrentJavaWorker.Stopped, Sub() RunInUi(AddressOf RefreshJavaInfo)
    End Sub
    Private Sub PageInstanceExport_Loaded() Handles Me.Loaded
        AniControlEnabled += 1
        If Not PathUtils.IsSame(CurrentInstance, PageInstanceLeft.Instance.PathVersion) Then RefreshAll() 'Switched to another instance; reset the page
        AniControlEnabled -= 1
    End Sub
    Public Sub RefreshAll() Implements IRefreshable.Refresh
        Logger.Info("刷新导出页面")
        HintOptiFine.Visibility = If(PageInstanceLeft.Instance.Version.HasOptiFine AndAlso Not PageInstanceLeft.Instance.Version.OptiFineAsMod, Visibility.Visible, Visibility.Collapsed)
        CurrentInstance = PageInstanceLeft.Instance.PathVersion
        TextExportName.Text = ""
        TextExportName.HintText = PageInstanceLeft.Instance.Name
        TextExportVersion.Text = ""
        TextExportVersion.HintText = "1.0.0"
        CheckAdvancedInclude.Checked = False
        CheckAdvancedModrinth.Checked = False
        GetExportOption(CheckOptionsBasic).Description = PageInstanceLeft.Instance.VersionDisplayName()
        ResetConfigOverrides()
        ReloadAllSubOptions()
        RefreshAllOptionsUI()
        RefreshJavaInfo()
        PanBack.ScrollToHome()
    End Sub

#Region "Sub-options"
    Private SubOptionBlackList As String() = {"Quark Programmer Art.zip", "+ EuphoriaPatches_", "PCL2 Skin.zip"}

    ''' <summary>
    ''' Build options for subfolders such as resource packs and saves.
    ''' </summary>
    Private Sub ReloadAllSubOptions()
        ReloadSubOptions(PanOptionsResourcePacks, True, True, "resourcepacks", "texturepacks")
        ReloadSubOptions(PanOptionsSaves, False, True, "saves")
        ReloadSubOptions(PanOptionsShaderPacks, True, True, "shaderpacks")
    End Sub

    Private Sub ReloadSubOptions(Panel As StackPanel, AcceptCompressedFile As Boolean, AcceptFolder As Boolean, ParamArray Folders As String())
        Panel.Children.Clear()
        For Each Folder In Folders
            Dim TargetFolder = DirectoryUtils.GetInfo(PageInstanceLeft.Instance.PathIndie & Folder)
            If Not TargetFolder.Exists() Then Continue For
            'Find matching items in the folder
            If AcceptCompressedFile Then
                For Each File In TargetFolder.EnumerateFiles("*.zip").Concat(TargetFolder.EnumerateFiles("*.rar"))
                    If SubOptionBlackList.Any(Function(b) File.Name.Contains(b)) Then Continue For
                    Panel.Children.Add(New MyCheckBox With {. _
                        Tag = New ExportOption With {.Title = File.Name, .DefaultChecked = True, .Rules = StringUtils.LikePatternEscape($"{Folder}/{File.Name}")}})
                Next
            End If
            If AcceptFolder Then
                For Each SubFolder In TargetFolder.EnumerateDirectories().OrderByDescending(Function(f) f.LastWriteTime)
                    If SubOptionBlackList.Any(Function(b) SubFolder.Name.Contains(b)) Then Continue For
                    If Not SubFolder.EnumerateFileSystemInfos().Any() Then Continue For
                    Dim NewCheckBox As New MyCheckBox With {. _
                        Tag = New ExportOption With {.Title = SubFolder.Name, .DefaultChecked = True, .Rules = StringUtils.LikePatternEscape($"{Folder}/{SubFolder.Name}/")}}
                    If AcceptCompressedFile Then GetExportOption(NewCheckBox).Description = "文件夹"
                    If Panel Is PanOptionsSaves Then GetExportOption(NewCheckBox).Description = SubFolder.LastWriteTime.ToString("yyyy'/'MM'/'dd HH':'mm")
                    Panel.Children.Add(NewCheckBox)
                Next
            End If
        Next
    End Sub

#End Region

#Region "Options"

    ''' <summary>
    ''' Recheck which options should be shown and sync ExportOption to the UI.
    ''' </summary>
    Private Sub RefreshAllOptionsUI()
        'Collect every file and folder up to two levels deep
        Dim AllEntries As New List(Of String)
        Dim IsValidDirectory = 'Folder is not empty
        Function(Folder As DirectoryInfo) As Boolean
            Try
                Return Folder.Exists AndAlso
                    Folder.EnumerateFileSystemInfos().Any(Function(i) Not SubOptionBlackList.Any(Function(b) i.Name.Contains(b)))
            Catch 'Usually inaccessible, or a link to a folder that no longer exists (for example a resource folder link made with mklink)
                Return False
            End Try
        End Function
        Dim PathInfo = DirectoryUtils.GetInfo(PageInstanceLeft.Instance.PathIndie)
        AllEntries.AddRange(PathInfo.EnumerateFiles().Select(Function(f) f.Name))
        For Each SubFolder In PathInfo.EnumerateDirectories().Where(IsValidDirectory)
            AllEntries.Add($"{SubFolder.Name}\")
            AllEntries.AddRange(SubFolder.EnumerateFiles().Select(Function(f) $"{SubFolder.Name}\{f.Name}"))
            AllEntries.AddRange(SubFolder.EnumerateDirectories().Where(IsValidDirectory).Select(Function(d) $"{SubFolder.Name}\{d.Name}\"))
        Next
        Logger.Info($"共发现 {AllEntries.Count} 个可行的二级文件/文件夹")
        'Decide whether the option should be shown
        Dim IsVisible =
        Function(TargetOption As ExportOption) As Boolean
            'Require OptiFine (for example shaders) or a mod loader (for example mods/)
            If TargetOption.RequireOptiFine AndAlso Not PageInstanceLeft.Instance.Version.HasOptiFine Then Return False
            If TargetOption.RequireModLoader AndAlso Not PageInstanceLeft.Instance.Modable Then Return False
            If TargetOption.RequireModLoaderOrOptiFine AndAlso Not PageInstanceLeft.Instance.Version.HasOptiFine AndAlso Not PageInstanceLeft.Instance.Modable Then Return False
            'Roughly check whether any file or folder might match
            Return StandardizeLines(If(TargetOption.Rules, TargetOption.ShowRules).Split("|"c), True).Any(
            Function(Rule As String)
                If Rule.StartsWithF("!") Then Return False 'Only positive rules
                'Check the first two levels
                Try
                    If AllEntries.Any(Function(Entry) Entry.Lower Like Rule.Lower) Then Return True
                Catch ex As Exception
                    Logger.Error(ex, $"错误的规则：{Rule}", LogBehavior.Toast)
                    Return False
                End Try
                'Roughly check every level
                Rule = Rule.Trim("*?".ToCharArray)
                If Rule.Split("\"c, True).Count >= 3 Then
                    If Rule.EndsWithF("\") Then
                        Return IsValidDirectory(DirectoryUtils.GetInfo(PageInstanceLeft.Instance.PathIndie & Rule)) 'Folder is valid
                    Else
                        Return FileUtils.Exists(PageInstanceLeft.Instance.PathIndie & Rule) 'File is valid
                    End If
                Else
                    Return False
                End If
            End Function)
        End Function
        'Check each option
        For Each CheckBox In GetAllOptions(True)
            Dim TargetOption = GetExportOption(CheckBox)
            'Visibility and default check
            If String.IsNullOrEmpty(TargetOption.Rules) AndAlso String.IsNullOrEmpty(TargetOption.ShowRules) Then
                CheckBox.Visibility = Visibility.Visible
                CheckBox.Checked = TargetOption.DefaultChecked
            Else
                Dim Pass As Boolean = IsVisible(TargetOption)
                CheckBox.Visibility = If(Pass, Visibility.Visible, Visibility.Collapsed)
                CheckBox.Checked = TargetOption.DefaultChecked AndAlso Pass
            End If
            'Name and description
            CheckBox.Inlines.Clear()
            If CheckBox.Visibility <> Visibility.Visible Then Continue For
            CheckBox.Inlines.Add(New Run(TargetOption.Title))
            If Not String.IsNullOrEmpty(TargetOption.Description) Then
                CheckBox.Inlines.Add(New Run("   " & TargetOption.Description) With {.Foreground = ColorGray5})
            End If
        Next
    End Sub

    ''' <summary>
    ''' Normalize text lines so they can be matched with Like.
    ''' </summary>
    Private Iterator Function StandardizeLines(Raw As IEnumerable(Of String), AddSuffixStarToFolderPath As Boolean) As IEnumerable(Of String)
        For Each IgnoreLine In Raw
            IgnoreLine = IgnoreLine.Trim
            If IgnoreLine = "" OrElse IgnoreLine.StartsWithF("#") OrElse IgnoreLine.StartsWithF("=") Then Continue For
            IgnoreLine = IgnoreLine.Replace("/", "\")
            Yield IgnoreLine & If(IgnoreLine.EndsWithF("\") AndAlso AddSuffixStarToFolderPath, "*", "")
        Next
    End Function

    ''' <summary>
    ''' Get every CheckBox that can be an option.
    ''' </summary>
    Private Iterator Function GetAllOptions(IncludeHidden As Boolean) As IEnumerable(Of MyCheckBox)
        For Each Element In PanOptions.Children
            If Not IncludeHidden AndAlso Element.Visibility <> Visibility.Visible Then Continue For
            If TypeOf Element Is MyCheckBox Then
                Yield Element
            ElseIf TypeOf Element Is StackPanel Then
                For Each SubElement In DirectCast(Element, StackPanel).Children
                    If Not IncludeHidden AndAlso SubElement.Visibility <> Visibility.Visible Then Continue For
                    If TypeOf SubElement Is MyCheckBox Then Yield SubElement
                Next
            End If
        Next
    End Function

    ''' <summary>
    ''' Get the ExportOption for this CheckBox.
    ''' </summary>
    Private Function GetExportOption(CheckBox As MyCheckBox) As ExportOption
        Return CType(CheckBox.Tag, ExportOption)
    End Function

#End Region

#Region "Config file"
    Private Const Sperator As String = "=============================================================="

    ' ================ Exported content section ================

    ''' <summary>
    ''' Rules read from the config file.
    ''' When not Nothing, they override the checked rules and disable that UI.
    ''' </summary>
    Private Property RulesOverrides As List(Of String)
        Get
            Return _RulesOverrides
        End Get
        Set(value As List(Of String))
            _RulesOverrides = value
            If value Is Nothing Then
                BtnOverrideCancel.Visibility = Visibility.Collapsed
                PanOptions.Visibility = Visibility.Visible
                CardOptions.Inlines.Clear()
                CardOptions.Inlines.Add(New Run("导出内容列表") With {.FontWeight = FontWeights.Bold})
            Else
                BtnOverrideCancel.Visibility = Visibility.Visible
                PanOptions.Visibility = Visibility.Collapsed
                CardOptions.Inlines.Clear()
                CardOptions.Inlines.Add(New Run("导出内容列表:    ") With {.FontWeight = FontWeights.Bold})
                CardOptions.Inlines.Add(New Run("从配置文件中读取") With {.FontWeight = FontWeights.Normal})
            End If
        End Set
    End Property
    Private _RulesOverrides As List(Of String) = Nothing
    ''' <summary>
    ''' Get every rule that currently applies.
    ''' </summary>
    Private Iterator Function GetAllRules() As IEnumerable(Of String)
        If RulesOverrides IsNot Nothing Then
            'Yield the override list
            For Each Rule In RulesOverrides
                Yield Rule
            Next
        Else
            'Take every rule line from the currently checked options
            Yield ""
            Yield "# 修改下方的规则以控制需要导出的内容。"
            Yield "# 以 ! 开头以反选。可以使用 *、?、[] 通配符。靠后的行覆盖靠前的。"
            Yield ""
            For Each CheckBox In GetAllOptions(False)
                If Not CheckBox.Checked Then Continue For
                Dim TargetOption = GetExportOption(CheckBox)
                Dim Rules = If(TargetOption.Rules?.Split("|"c).ToList(), New List(Of String))

                'Dynamic rules
                If CheckBox Is CheckOptionsShaderPackSettings Then 'Shader pack settings: export settings only for checked shader packs
                    For Each ShaderPackCheckBox In PanOptionsShaderPacks.Children.OfType(Of MyCheckBox)()
                        If Not ShaderPackCheckBox.Checked Then Continue For
                        Dim ShaderPackOption = GetExportOption(ShaderPackCheckBox)
                        Dim SettingsFile = $"shaderpacks/{ShaderPackOption.Title}.txt"
                        If Not FileUtils.Exists(Path.Combine(PageInstanceLeft.Instance.PathIndie, SettingsFile)) Then Continue For
                        Rules.Add(StringUtils.LikePatternEscape(SettingsFile))
                    Next
                End If

                If Not Rules.Any() Then Continue For
                Yield $"# {TargetOption.Title}"
                For Each Rule In Rules
                    Yield Rule
                Next
                Yield ""
            Next
            Yield "# 排除的文件"
            Yield "!*.log"
            Yield "!*.dat_old"
            Yield "!*.BakaCoreInfo"
            Yield "!hmclversion.cfg"
            Yield "!log4j2.xml"
            Yield ""
        End If
    End Function

    ' ================ Extra files section ================

    Private ExtraFiles As List(Of String) = Nothing
    ''' <summary>
    ''' Get the extra files that currently apply.
    ''' </summary>
    Private Iterator Function GetExtraFileLines() As IEnumerable(Of String)
        If ExtraFiles IsNot Nothing Then
            'Yield the override list
            For Each File In ExtraFiles
                Yield File
            Next
        Else
            'Take every rule line from the currently checked options
            Yield ""
            Yield "# 如果想将额外的文件自动放到压缩包根目录中，可以将它们的路径写在下方。"
            Yield "# 必须是完整路径。每行中，若以 \ 结尾则代表是文件夹，不以 \ 结尾则代表是文件。"
            Yield ""
        End If
    End Function

    ' ================ Reset ================

    ''' <summary>
    ''' Clear the effects of the config file.
    ''' </summary>
    Private Sub ResetConfigOverrides()
        RulesOverrides = Nothing
        ConfigPackPath = Nothing
        ExtraFiles = Nothing
        PanBack.ScrollToHome()
    End Sub
    Private Sub CardOptions_MouseLeftButtonDown() Handles CardOptions.MouseLeftButtonDown
        If RulesOverrides Is Nothing Then Return
        ResetConfigOverrides()
    End Sub

    ' ================ Save / load ================

    'Save the config file
    Private Sub ExportConfig() Handles BtnAdvancedExport.Click
        Try
            Dim ConfigPath As String = Dialogs.SaveFile("选择文件位置", "export_config.txt", Settings.Get(Of String)("CacheExportConfig"), {("txt", "整合包导出配置")})
            If ConfigPath Is Nothing Then Return
            Settings.Set("CacheExportConfig", ConfigPath)
            Dim ConfigLines As New List(Of String)
            'ini section
            ConfigLines.Add("Name:" & TextExportName.Text)
            ConfigLines.Add("Version:" & TextExportVersion.Text)
            ConfigLines.Add("")
            ConfigLines.Add($"# 当版本设置中选择了 {vbLQ}使用版本文件夹中的 Java{vbRQ} 时，是否打包版本文件夹中的 Java。")
            ConfigLines.Add("IncludeJava:" & CheckOptionsJava.Checked)
            ConfigLines.Add("")
            ConfigLines.Add("# 是否打包正式版 PCL，以便没有启动器的玩家安装整合包。")
            ConfigLines.Add("IncludeLauncher:" & CheckOptionsPcl.Checked)
            ConfigLines.Add("")
            ConfigLines.Add("")
            ConfigLines.Add("# 是否将 Mod、资源包、光影包的文件直接放入整合包中，这样在导入时就无需联网下载它们。")
            ConfigLines.Add("# 建议仅在无法稳定连接 CurseForge 或 Modrinth 时才考虑启用。")
            ConfigLines.Add("# 二次分发可能违反使用协议，请尽量不要公开发布包含资源文件的整合包！")
            ConfigLines.Add("DontCheckHostedAssets:" & CheckAdvancedInclude.Checked)
            ConfigLines.Add("")
            ConfigLines.Add("# 若资源未在 Modrinth 上托管，则将其原始文件直接放入整合包中。")
            ConfigLines.Add("# 如果你想将整合包上传到 Modrinth，这能帮助你分辨哪些文件未在 Modrinth 上托管。")
            ConfigLines.Add("# 此选项与 DontCheckHostedAssets 冲突。")
            ConfigLines.Add("ModrinthUploadMode:" & CheckAdvancedModrinth.Checked)
            ConfigLines.Add("")
            ConfigLines.Add("# 导出的文件的存放位置。")
            ConfigLines.Add("# 若设置了此项，在导出时会直接将文件放到此路径，不会弹窗要求选择。")
            ConfigLines.Add("# 若 IncludeLauncher 为 True，应以 .zip 结尾；若为 False，应以 .mrpack 结尾。")
            ConfigLines.Add("PackPath:" & If(ConfigPackPath, ""))
            ConfigLines.Add("")
            'Exported content section
            ConfigLines.Add(Sperator)
            ConfigLines.AddRange(GetAllRules())
            'Extra files section
            ConfigLines.Add(Sperator)
            ConfigLines.AddRange(GetExtraFileLines())
            FileUtils.Write(ConfigPath, ConfigLines.Join(vbCrLf))
            Hint("已保存配置文件：" & ConfigPath, HintType.Green)
            OpenExplorer(ConfigPath)
        Catch ex As Exception
            Logger.Error(ex, "保存配置失败", LogBehavior.Alert)
        End Try
    End Sub
    'Load the config file
    Private Sub ImportConfig() Handles BtnAdvancedImport.Click
        Try
            Dim ConfigPath As String = Dialogs.SelectFile("选择配置文件", False, Settings.Get(Of String)("CacheExportConfig"), {({"txt"}, "整合包导出配置")}).FirstOrDefault()
            If String.IsNullOrEmpty(ConfigPath) Then Return
            Settings.Set("CacheExportConfig", ConfigPath)
            Dim Segments As String() = FileUtils.ReadAsString(ConfigPath).Split(Sperator)
            'ini section
            Dim Ini As New Dictionary(Of String, String)
            For Each Line In Segments(0).SplitLines(True)
                Line = Line.Trim
                If Line = "" OrElse Line.StartsWithF("#") OrElse Line.StartsWithF("=") Then Continue For
                Dim Index As Integer = Line.IndexOfF(":")
                If Index > 0 Then Ini(Line.Substring(0, Index)) = Line.Substring(Index + 1)
            Next
            TextExportName.Text = Ini.GetOrDefault("Name", "")
            TextExportVersion.Text = Ini.GetOrDefault("Version", "")
            CheckOptionsJava.Checked = Ini.GetOrDefault("IncludeJava", True)
            CheckOptionsPcl.Checked = Ini.GetOrDefault("IncludeLauncher", True)
            CheckAdvancedInclude.Checked = Ini.GetOrDefault("DontCheckHostedAssets", False)
            If Not CheckAdvancedInclude.Checked Then CheckAdvancedModrinth.Checked = Ini.GetOrDefault("ModrinthUploadMode", False) '#7979, special case
            ConfigPackPath = Ini.GetOrDefault("PackPath", Nothing)
            'Exported content section
            RulesOverrides = Segments(1).SplitLines(True).ToList
            'Extra files section
            If Segments.Length > 2 Then
                ExtraFiles = Segments(2).SplitLines(True).ToList
            Else
                ExtraFiles = Nothing
            End If
            Hint("已读取配置文件：" & ConfigPath, HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "读取配置失败", LogBehavior.Alert)
        End Try
    End Sub

#End Region

#Region "Export"

    ''' <summary>
    ''' Export path specified in the config file.
    ''' </summary>
    Private ConfigPackPath As String = Nothing

    ''' <summary>
    ''' Start the export.
    ''' </summary>
    Private Sub StartExport() Handles BtnExport.Click
        Dim PackName As String = If(String.IsNullOrEmpty(TextExportName.Text), TextExportName.HintText, TextExportName.Text)
        Dim PackVersion As String = If(String.IsNullOrEmpty(TextExportVersion.Text), "1.0.0", TextExportVersion.Text)

        'Duplicate task check
        Dim LoaderName As String = $"导出整合包：" & PackName
        For Each OngoingLoader In LoaderTaskbar
            If OngoingLoader.Name <> LoaderName Then Continue For
            FrmMain.PageChange(FormMain.PageType.TaskManager)
            Return
        Next

        'Confirm the export path
        Dim PackPath As String = Nothing
        If Not String.IsNullOrWhiteSpace(ConfigPackPath) AndAlso
           (Not ConfigPackPath.EndsWithF("\") AndAlso Not ConfigPackPath.EndsWithF("/")) Then
            Try
                DirectoryUtils.Create(PathUtils.RemoveLastPart(ConfigPackPath))
                PackPath = ConfigPackPath
                Logger.Info($"使用配置文件中指定的导出路径：{ConfigPackPath}")
            Catch ex As Exception
                Logger.Warn(ex, $"无法使用配置文件中指定的导出路径（{ConfigPackPath}）")
                If MyMsgBox($"指定的路径：{ConfigPackPath}{vbCrLf}{vbCrLf}{ex.GetDisplay(True)}", "无法使用配置文件中指定的导出路径", "确定", "取消") = 2 Then Return
            End Try
        End If
        If PackPath Is Nothing Then
            Dim Extensions As New List(Of (Extension As String, Display As String))
            If Not CheckOptionsPcl.Checked AndAlso CheckAdvancedModrinth.Checked Then Extensions.Add(("mrpack", "Modrinth 整合包文件"))
            Extensions.Add(("zip", "压缩文件"))
            If Not CheckOptionsPcl.Checked AndAlso Not CheckAdvancedModrinth.Checked Then Extensions.Add(("mrpack", "Modrinth 整合包文件"))
            PackPath = Dialogs.SaveFile("选择导出位置",
                PackName & If(String.IsNullOrEmpty(TextExportVersion.Text), "", " " & TextExportVersion.Text), filter:=Extensions)
            Logger.Info($"手动指定的导出路径：{PackPath}")
        End If
        If PackPath Is Nothing Then Return

        'Cache the parameters needed
        Dim CacheFolder = RequestTaskTempFolder()
        Dim OverridesFolder = CacheFolder & "modpack\overrides\"
        Dim TargetInstance = PageInstanceLeft.Instance
        Dim IncludeJava As Boolean =
            (RulesOverrides Is Nothing AndAlso CheckOptionsJava.Visibility = Visibility.Visible AndAlso CheckOptionsJava.Checked) OrElse
            (RulesOverrides IsNot Nothing AndAlso CheckOptionsJava.Checked AndAlso Settings.Get(Of Integer)("VersionArgumentJavaV2", TargetInstance) = 2)
        Dim PathIndie As String = TargetInstance.PathIndie
        Dim CheckHostedAssets As Boolean = Not CheckAdvancedInclude.Checked
        Dim ModrinthUploadMode As Boolean = CheckAdvancedModrinth.Checked
        Dim IncludePCL As Boolean = CheckOptionsPcl.Checked
        Dim AllRules = StandardizeLines(GetAllRules(), True).ToList()
        Dim AllExtraFiles = StandardizeLines(GetExtraFileLines(), False).ToList()
        Logger.Info($"准备导出整合包，共有 {AllRules.Count} 条规则，{AllExtraFiles.Count} 条追加内容行")

        'Build the step loaders
        Dim Loaders As New List(Of LoaderBase)

#Region "Prepare PCL files"

        If IncludePCL AndAlso Not FileUtils.Exists(PathExe) Then Throw New FileNotFoundException("未找到当前启动器，无法导出。", PathExe)

#End Region

#Region "Copy files"

        Loaders.Add(New LoaderTask(Of Integer, List(Of LocalResourceFile))("复制导出内容",
        Sub(Loader As LoaderTask(Of Integer, List(Of LocalResourceFile)))
            Loader.Output = New List(Of LocalResourceFile)
            'Copy instance files
            Dim Progress As Integer = 0
            Dim SearchFolder As Action(Of DirectoryInfo)
            SearchFolder =
            Sub(Folder As DirectoryInfo)
                'Folder: search further
                For Each SubFolder In Folder.EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                    'Skip bulky folders that are useless here to speed up the search
                    If {"assets", "versions", "libraries"}.Contains(SubFolder.Name) AndAlso PathUtils.IsSame(PathIndie, Folder.FullName) Then Continue For
                    If {"structureCacheV1", ".fabric", ".git", "avatar-cache", "cosmetic-cache"}.Contains(SubFolder.Name) Then Continue For
                    SearchFolder(SubFolder)
                Next
                'File: check the rules and copy
                For Each Entry In Folder.EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                    Dim RelativePath As String = Entry.FullName.AfterFirst(PathIndie)
                    'Check the rules
                    Dim ShouldKeep As Boolean = False
                    For Each Rule In AllRules
                        Dim Revert = Rule.StartsWithF("!")
                        If RelativePath.Lower Like Rule.TrimStart("!").Lower Then ShouldKeep = Not Revert
                    Next
                    If Not ShouldKeep Then Continue For
                    Dim TargetPath As String = OverridesFolder & RelativePath
                    FileUtils.Copy(Entry.FullName, TargetPath)
                    'For an archive, consider looking up a download URL online
                    If CheckHostedAssets AndAlso
                       {".zip", ".rar", ".jar", ".disabled", ".old"}.Contains(Entry.Extension.Lower) AndAlso
                       {"mods", "packs", "openloader", "resource"}.Any(Function(s) RelativePath.Contains(s)) Then
                        Dim ModFile As New LocalResourceFile(TargetPath)
                        Dim Unused = ModFile.ModrinthHash 'Compute the hash ahead of time
                        Unused = ModFile.CurseForgeHash
                        Loader.Output.Add(ModFile)
                    End If
                    'Update progress (inaccurate; mainly to show it has not frozen)
                    Progress += 1
                    If Progress = 25 Then
                        Loader.Progress += (0.84 - Loader.Progress) * 0.012
                        Progress = 0
                    End If
                Next
            End Sub
            SearchFolder(DirectoryUtils.GetInfo(PathIndie))
            Logger.Info($"复制 overrides 文件完成，有 {Loader.Output.Count} 个文件需要联网检查")
            Loader.Progress = 0.85
            'Copy Java
            If IncludeJava Then
                Dim JavaToExport = SelectOrDownloadJava(TargetInstance, False, Loader.CreateCancellationToken, Loader.CreateSyncProgressProvider(0.85, 0.93))
                If JavaToExport Is Nothing Then Throw New FileNotFoundException("版本文件夹中没有找到可导出的 Java。")
                Dim JavaFolder = PathUtils.RemoveLastPart(JavaToExport.Folder)
                Dim DestFolder As String = Path.Combine(OverridesFolder, JavaFolder.AfterFirst(TargetInstance.PathVersion, True))
                DirectoryUtils.Copy(JavaFolder, DestFolder)
                Logger.Info($"已复制版本文件夹中的 Java：{JavaFolder} → {DestFolder}")
            End If
            Loader.Progress = 0.95
            'Copy extra files to the root
            Dim BaseFolder As String = If(IncludePCL, CacheFolder, CacheFolder & "modpack\")
            For Each Line In AllExtraFiles
                If Line.EndsWithF("\") OrElse Line.EndsWithF("/") Then
                    If DirectoryUtils.Exists(Line) Then
                        DirectoryUtils.Copy(Line, BaseFolder & PathUtils.GetLastPart(Line) & "\")
                    Else
                        Hint($"未找到配置文件中指定的文件夹：{Line}", HintType.Red)
                    End If
                Else
                    If FileUtils.Exists(Line) Then
                        FileUtils.Copy(Line, BaseFolder & PathUtils.GetLastPart(Line))
                    Else
                        Hint($"未找到配置文件中指定的单个文件：{Line}", HintType.Red)
                    End If
                End If
            Next
            Loader.Progress = 0.97
            'Copy PCL instance settings
            If DirectoryUtils.Exists(TargetInstance.PathVersion & "PCL\") Then DirectoryUtils.Copy(TargetInstance.PathVersion & "PCL\", OverridesFolder & "PCL\")
            WriteIni(OverridesFolder & "PCL\Setup.ini", "IsStar", False)
            'Copy the PCL executable (release build)
            If IncludePCL Then FileUtils.Copy(PathExe, CacheFolder & "Plain Craft Launcher.exe")
        End Sub) With {.ProgressWeight = 5})

#End Region

#Region "Online check"

        Loaders.Add(New LoaderTask(Of List(Of LocalResourceFile), Dictionary(Of LocalResourceFile, List(Of String)))("联网获取文件信息",
        Sub(Loader As LoaderTask(Of List(Of LocalResourceFile), Dictionary(Of LocalResourceFile, List(Of String))))
            Loader.Output = New Dictionary(Of LocalResourceFile, List(Of String))
            If Not CheckHostedAssets Then Logger.Info($"要求跳过联网获取步骤") : Return
            If Not Loader.Input.Any Then Logger.Info($"没有需要联网检查的文件，跳过联网获取步骤") : Return

            'Get download URLs per platform
            Dim EndedThreadCount As Integer = 0, FailedExceptions As New List(Of Exception)

            'Get info from Modrinth
            RunInNewThread(
            Sub()
                Try
                    Dim ModrinthHashes = Loader.Input.Select(Function(m) m.ModrinthHash)
                    Dim ModrinthRaw As JObject = DlModRequest("https://api.modrinth.com/v2/version_files", HttpMethod.Post,
                        $"{{""hashes"": [""{ModrinthHashes.Join(""",""")}""], ""algorithm"": ""sha1""}}", "application/json")
                    For Each ModFile In Loader.Input
                        'Find the matching file
                        If Not ModrinthRaw.ContainsKey(ModFile.ModrinthHash) Then Continue For
                        If ModrinthRaw(ModFile.ModrinthHash)?("files")?(0)("hashes")?("sha1") <> ModFile.ModrinthHash Then Continue For
                        'Write the download URL
                        Loader.Output.AddIntoValueCollection(ModFile, ModrinthRaw(ModFile.ModrinthHash)("files")(0)("url"))
                    Next
                    Logger.Info($"从 Modrinth 获取到 {ModrinthRaw.Count} 个本地资源项的对应信息")
                Catch ex As Exception
                    Logger.Warn(ex, "从 Modrinth 获取本地 Mod 信息失败")
                    FailedExceptions.Add(ex)
                Finally
                    EndedThreadCount += 1
                    Loader.Progress += 0.45
                End Try
            End Sub, "Modrinth - " & LoaderName)

            'Get info from CurseForge
            RunInNewThread(
            Sub()
                Try
                    If ModrinthUploadMode Then Return 'In Modrinth upload mode, do not get info from CurseForge
                    Dim CurseForgeHashes = Loader.Input.Select(Function(m) m.CurseForgeHash)
                    Dim CurseForgeRaw As JContainer = DlModRequest("https://api.curseforge.com/v1/fingerprints/432", HttpMethod.Post,
                        $"{{""fingerprints"": [{CurseForgeHashes.Join(",")}]}}", "application/json")("data")("exactMatches")
                    For Each ResultJson As JObject In CurseForgeRaw
                        If Not ResultJson.ContainsKey("file") Then Continue For
                        Dim File As JObject = ResultJson("file")
                        If String.IsNullOrEmpty(File("downloadUrl")) Then Continue For
                        'Find the matching file
                        Dim ModFile As LocalResourceFile = Loader.Input.FirstOrDefault(Function(m) m.CurseForgeHash = File("fileFingerprint").ToObject(Of UInteger))
                        If ModFile Is Nothing Then Continue For
                        'Write the download URL
                        For Each Address In ResourceVersion.ParseCurseForgeDownloadUrls(File("downloadUrl").ToString)
                            Loader.Output.AddIntoValueCollection(ModFile, Address)
                        Next
                    Next
                    Logger.Info($"从 CurseForge 获取到 {CurseForgeRaw.Count} 个本地资源项的对应信息")
                Catch ex As Exception
                    Logger.Warn(ex, "从 CurseForge 获取本地 Mod 信息失败")
                    FailedExceptions.Add(ex)
                Finally
                    EndedThreadCount += 1
                    Loader.Progress += 0.45
                End Try
            End Sub, "CurseForge - " & LoaderName)

            'Wait for the threads to finish
            Do Until EndedThreadCount = 2
                If Loader.IsCanceled Then Return
                Thread.Sleep(10)
            Loop

            'If it failed, ask whether to continue
            If FailedExceptions.Count = 1 Then
                If MyMsgBox("联网获取部分文件信息失败，是否继续导出？" & vbCrLf & vbCrLf &
                            "若继续，无法获取信息的文件将被直接打包。" & vbCrLf &
                            "由于二次分发可能违反使用协议，请尽量不要公开发布导出的整合包！",
                            "部分文件信息获取失败", "继续", "取消") = 2 Then Throw FailedExceptions.First
            ElseIf FailedExceptions.Count > 1 Then
                If MyMsgBox("联网获取文件信息失败，是否继续导出？" & vbCrLf & vbCrLf &
                            "若继续，所有文件都将被直接打包。" & vbCrLf &
                            "由于二次分发可能违反使用协议，请尽量不要公开发布导出的整合包！",
                            "文件信息获取失败", "继续", "取消") = 2 Then Throw FailedExceptions.First
            End If
        End Sub) With {.Show = CheckHostedAssets, .ProgressWeight = If(CheckHostedAssets, 2, 0.01)})

#End Region

#Region "Create the archive"

        Loaders.Add(New LoaderTask(Of Dictionary(Of LocalResourceFile, List(Of String)), Integer)("生成压缩包",
        Sub(Loader As LoaderTask(Of Dictionary(Of LocalResourceFile, List(Of String)), Integer))
            'Build the file list
            Dim Files As New JArray
            For Each Pair In Loader.Input
                Dim ModFile As LocalResourceFile = Pair.Key
                Files.Add(New JObject From {
                    {"path", PathUtils.RemoveExtendedPrefix(ModFile.File.FullName).AfterFirst(OverridesFolder).Replace("\", "/")},
                    {"hashes", New JObject From {{"sha1", ModFile.ModrinthHash}, {"sha512", CryptographyUtils.ComputeFileHash(ModFile.File.FullName, CryptographyUtils.HashMethod.Sha512)}}},
                    {"downloads", New JArray(Pair.Value.OrderBy(Function(u) u.Contains("modrinth.com")))}, 'Do not prefer Modrinth
                    {"fileSize", ModFile.File.Length}
                })
                ModFile.File.Delete()
            Next
            Loader.Progress = 0.2
            'Write the final JSON file
            Dim Dependencies As New JObject From {{"minecraft", TargetInstance.Version.VanillaName}}
            If TargetInstance.Version.ModLoaders.HasFlagF(ModLoaders.Forge) Then Dependencies.Add("forge", TargetInstance.Version.ModLoaderVersions(ModLoaders.Forge))
            If TargetInstance.Version.ModLoaders.HasFlagF(ModLoaders.Fabric) Then Dependencies.Add("fabric-loader", TargetInstance.Version.ModLoaderVersions(ModLoaders.Fabric))
            If TargetInstance.Version.ModLoaders.HasFlagF(ModLoaders.NeoForge) Then Dependencies.Add("neoforge", TargetInstance.Version.ModLoaderVersions(ModLoaders.NeoForge))
            Dim ResultJson As New JObject From {
                {"game", "minecraft"},
                {"formatVersion", 1},
                {"versionId", PackVersion},
                {"name", PackName},
                {"summary", TargetInstance.Info},
                {"files", Files},
                {"dependencies", Dependencies}
            }
            FileUtils.Write(CacheFolder & "modpack\modrinth.index.json", ResultJson.ToString(Newtonsoft.Json.Formatting.Indented))
            'Pack
            If IncludePCL Then
                'First zip of the modpack
                FileUtils.CreateZipFromDirectory(CacheFolder & "modpack.mrpack", CacheFolder & "modpack\")
                Loader.Progress = 0.5
                DirectoryUtils.Delete(CacheFolder & "modpack\")
                Loader.Progress = 0.6
                'Second zip of the modpack
                FileUtils.CreateZipFromDirectory(PackPath, CacheFolder)
                Loader.Progress = 0.9
            Else
                'Zip the modpack directly
                FileUtils.CreateZipFromDirectory(PackPath, CacheFolder & "modpack\")
                Loader.Progress = 0.8
            End If
            OpenExplorer(PackPath)
        End Sub) With {.ProgressWeight = 6})

#End Region

        Dim MainLoader As New LoaderCombo(Of String)(LoaderName, Loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
        MainLoader.Start()
        LoaderTaskbarAdd(MainLoader)
        FrmMain.BtnExtraDownload.ShowRefresh()
        FrmMain.BtnExtraDownload.Ribble()
        FrmMain.PageChange(FormMain.PageType.TaskManager)
    End Sub

#End Region

    Private Sub RefreshJavaInfo() Handles Me.Loaded
        Dim JavaType As Integer = Settings.Get(Of Integer)("VersionArgumentJavaV2", PageInstanceLeft.Instance)
        Dim JavaWorker = PageInstanceLeft.CurrentJavaWorker
        'Export option
        Dim JavaToExport = If(JavaType = 2 AndAlso JavaWorker.LastSucceeded AndAlso Not JavaWorker.Running, JavaWorker.LastResult, Nothing)
        If JavaToExport Is Nothing Then
            CheckOptionsJava.Visibility = Visibility.Collapsed
        Else
            CheckOptionsJava.Visibility = Visibility.Visible
            CheckOptionsJava.Inlines.Clear()
            CheckOptionsJava.Inlines.Add(New Run(
                GetExportOption(CheckOptionsJava).Title))
            CheckOptionsJava.Inlines.Add(New Run("   " &
                $"Java {JavaToExport.Version.Major} ({JavaToExport.Version})：{ _
                PathUtils.AddSlashSuffix(PathUtils.RemoveLastPart(JavaToExport.Folder.Replace(PathUtils.RemoveLastPart(PathUtils.RemoveLastPart(PageInstanceLeft.Instance.PathVersion)), ""))).TrimStart("\")}") With {.Foreground = ColorGray5})
        End If
        'Warning bar
        If JavaType = 3 Then
            BtnExport.IsEnabled = True
            HintJava.Visibility = Visibility.Visible
            HintJava.Theme = MyHint.Themes.Yellow
            HintJava.Text = $"该版本设置要求 {vbLQ}使用指定的 Java{vbRQ}，这在导出后很可能会失效。建议修改为 {vbLQ}使用版本文件夹中的 Java{vbRQ}。"
        ElseIf JavaType = 2 AndAlso JavaWorker.HasSucceeded AndAlso JavaWorker.LastResult Is Nothing Then
            BtnExport.IsEnabled = False
            HintJava.Visibility = Visibility.Visible
            HintJava.Theme = MyHint.Themes.Red
            HintJava.Text = $"该版本设置要求 {vbLQ}使用版本文件夹中的 Java{vbRQ}，但版本文件夹中没能找到任何 Java。"
        Else
            BtnExport.IsEnabled = True
            HintJava.Visibility = Visibility.Collapsed
        End If
    End Sub

    'Autofill the modpack name
    Private Sub TextExportName_GotFocus() Handles TextExportName.GotFocus
        If TextExportName.Text = "" Then
            TextExportName.Text = TextExportName.HintText
            TextExportName.SelectionStart = TextExportName.Text.Length
        End If
    End Sub

    Private Sub CheckAdvancedInclude_Change(sender As Object, user As Boolean) Handles CheckAdvancedInclude.Change
        'When packing resource files, do not allow Modrinth-only mode
        If CheckAdvancedInclude.Checked Then CheckAdvancedModrinth.Checked = False
        CheckAdvancedModrinth.IsEnabled = Not CheckAdvancedInclude.Checked
    End Sub
    Private Sub CheckAdvancedModrinth_Change(sender As Object, user As Boolean) Handles CheckAdvancedModrinth.Change, CheckOptionsPcl.Change
        HintModrinthFormat.Visibility = (CheckAdvancedModrinth.Checked AndAlso CheckOptionsPcl.Checked).ToVisibility
    End Sub

End Class
