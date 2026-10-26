'Provides various input validation methods; names start with Validate
Public Module ModValidate

    ''' <summary>
    ''' Validates the input and returns the error reason.
    ''' Returns an empty string if there is no error.
    ''' </summary>
    Public Function Validate(Text As String, ValidateRules As IEnumerable(Of Validate)) As String
        Dim Result As String = ""
        For Each ValidateRule As Validate In ValidateRules
            Result = ValidateRule.Validate(Text)
            If Result Is Nothing Then Return ""
            If Result <> "" Then Return Result
        Next
        Return Result
    End Function

End Module

''' <summary>
''' Base class for input validation rules. To see all rules, type Validate and check the auto-completion.
''' </summary>
Public MustInherit Class Validate
    ''' <summary>
    ''' Checks whether a string meets the requirement. Returns an empty string if it does, or the error reason if it does not; returns Nothing to stop checking and pass directly.
    ''' </summary>
    Public MustOverride Function Validate(Str As String) As String
End Class

''' <summary>
''' Passes directly if empty.
''' </summary>
Public Class ValidateNullable
    Inherits Validate
    Public Sub New()
    End Sub
    Public Overrides Function Validate(Str As String) As String
        If IsNothing(Str) OrElse String.IsNullOrEmpty(Str) Then Return Nothing
        Return ""
    End Function
End Class

''' <summary>
''' Must not be Nothing or an empty string (whitespace-only is not checked).
''' </summary>
Public Class ValidateNullOrEmpty
    Inherits Validate
    Public Sub New()
    End Sub
    Public Overrides Function Validate(Str As String) As String
        If IsNothing(Str) OrElse String.IsNullOrEmpty(Str) Then Return "输入内容不能为空！"
        Return ""
    End Function
End Class

''' <summary>
''' Must not be Nothing or an empty string (whitespace-only is also rejected).
''' </summary>
Public Class ValidateNullOrWhiteSpace
    Inherits Validate
    Public Sub New()
    End Sub
    Public Overrides Function Validate(Str As String) As String
        If IsNothing(Str) OrElse String.IsNullOrWhiteSpace(Str) Then Return "输入内容不能为空！"
        Return ""
    End Function
End Class

''' <summary>
''' Custom Validate.
''' </summary>
Public Class ValidateFunc
    Inherits Validate
    Private ReadOnly _func As Func(Of String, String)
    Public Sub New(F As Func(Of String, String))
        _func = F
    End Sub
    Public Overrides Function Validate(Str As String) As String
        Return _func(Str)
    End Function
End Class

''' <summary>
''' Must match the regular expression.
''' </summary>
Public Class ValidateRegex
    Inherits Validate
    Public Property Regex As String
    Public Property ErrorDescription As String = "正则检查失败！"
    Public Sub New()
    End Sub 'For XAML initialization
    Public Sub New(Regex As String, Optional ErrorDescription As String = "正则检查失败！")
        Me.Regex = Regex
        Me.ErrorDescription = ErrorDescription
    End Sub
    Public Overrides Function Validate(Str As String) As String
        If Not Str.RegexCheck(Regex) Then Return ErrorDescription
        Return ""
    End Function
End Class

''' <summary>
''' Must be a complete URL.
''' </summary>
Public Class ValidateHttp
    Inherits Validate
    Public Sub New()
    End Sub 'For XAML initialization
    Public Overrides Function Validate(Str As String) As String
        If Str.EndsWithF("/") Then Str = Str.Substring(0, Str.Length - 1)
        If Not Str.RegexCheck("^(http[s]?)\://") Then Return "输入的网址无效！"
        Return ""
    End Function
End Class

''' <summary>
''' Must be an integer.
''' </summary>
Public Class ValidateInteger
    Inherits Validate
    Public Property Min As Integer
    Public Property Max As Integer = Integer.MaxValue
    Public Sub New()
    End Sub 'For XAML initialization
    Public Sub New(Min As Integer, Max As Integer)
        Me.Min = Min
        Me.Max = Max
    End Sub
    Public Overrides Function Validate(Str As String) As String
        If Str.Length > 9 Then Return "请输入一个大小合理的数字！"
        Dim Valed As Integer
        If Not Integer.TryParse(Str, Valed) Then Return "请输入一个整数！"
        If Valed > Max Then Return "不可超过 " & Max & "！"
        If Valed < Min Then Return "不可低于 " & Min & "！"
        Return ""
    End Function
End Class

''' <summary>
''' Length limit.
''' </summary>
Public Class ValidateLength
    Inherits Validate
    Public Property Min As Integer = 0
    Public Property Max As Integer = Integer.MaxValue
    Public Sub New()
    End Sub 'For XAML initialization
    Public Sub New(Min As Integer, Optional Max As Integer = Integer.MaxValue)
        Me.Min = Min
        Me.Max = Max
    End Sub
    Public Overrides Function Validate(Str As String) As String
        If Len(Str) <> Max AndAlso Max = Min Then Return $"长度必须为 {Max} 个字符！"
        If Len(Str) > Max Then Return $"长度最长为 {Max} 个字符！"
        If Len(Str) < Min Then Return $"长度至少需 {Min} 个字符！"
        Return ""
    End Function
End Class

''' <summary>
''' Must not contain certain strings. Case-insensitive.
''' </summary>
Public Class ValidateExcept
    Inherits Validate
    Public Property Excepts As New ObjectModel.Collection(Of String)
    Public Property ErrorMessage As String
    Public Sub New()
        ErrorMessage = "输入内容不能包含 %"
    End Sub 'For XAML initialization
    Public Sub New(Excepts As ObjectModel.Collection(Of String), Optional ErrorMessage As String = "输入内容不能包含 %")
        Me.Excepts = Excepts
        Me.ErrorMessage = ErrorMessage
    End Sub
    Public Sub New(Excepts As IEnumerable(Of String), Optional ErrorMessage As String = "输入内容不能包含 %")
        Me.New(
            New ObjectModel.Collection(Of String)(New List(Of String)(Excepts)),
            ErrorMessage)
    End Sub
    Public Sub New(Excepts As IEnumerable(Of Char), Optional ErrorMessage As String = "输入内容不能包含 %")
        Me.New(
            New ObjectModel.Collection(Of String)(Excepts.Select(Function(c) c.ToString).ToList),
            ErrorMessage)
    End Sub
    Public Overrides Function Validate(Str As String) As String
        For Each Ch As String In Excepts
            If Str.IndexOfF(Ch, True) >= 0 Then
                If IsNothing(ErrorMessage) Then ErrorMessage = ""
                Return ErrorMessage.Replace("%", Ch)
            End If
        Next
        Return ""
    End Function

End Class

''' <summary>
''' Must not equal certain strings.
''' </summary>
Public Class ValidateExceptSame
    Inherits Validate
    Public Property Excepts As New ObjectModel.Collection(Of String)
    Public Property ErrorMessage As String
    Public Property IgnoreCase As Boolean = False
    Public Sub New()
    End Sub
    Public Sub New(Excepts As ObjectModel.Collection(Of String), Optional ErrorMessage As String = "输入内容不能为 %", Optional IgnoreCase As Boolean = False)
        Me.Excepts = Excepts
        Me.ErrorMessage = ErrorMessage
        Me.IgnoreCase = IgnoreCase
    End Sub
    Public Sub New(Excepts As IEnumerable(Of String), Optional ErrorMessage As String = "输入内容不能为 %", Optional IgnoreCase As Boolean = False)
        Me.New(
            New ObjectModel.Collection(Of String)(New List(Of String)(Excepts)),
            ErrorMessage, IgnoreCase)
    End Sub
    Public Sub New(Except As String, Optional ErrorMessage As String = "输入内容不能为 %", Optional IgnoreCase As Boolean = False)
        Me.New(
            New ObjectModel.Collection(Of String) From {Except},
            ErrorMessage, IgnoreCase)
    End Sub
    Public Overrides Function Validate(Str As String) As String
        If Str Is Nothing Then Return ErrorMessage.Replace("%", "null")
        For Each Ch As String In Excepts
            If IgnoreCase Then
                If Str.Lower = Ch.Lower Then Return ErrorMessage.Replace("%", Ch)
            Else
                If Str.Equals(Ch) Then Return ErrorMessage.Replace("%", Ch) 'Not sure whether = ignores case
            End If
        Next
        Return ""
    End Function

End Class

''' <summary>
''' Must be a specific string.
''' </summary>
Public Class ValidateSame
    Inherits Validate
    Public Property Required As String
    Public Property ErrorMessage As String
    Public Property IgnoreCase As Boolean = False
    Public Sub New()
    End Sub
    Public Sub New(Required As String, Optional ErrorMessage As String = "输入内容有误", Optional IgnoreCase As Boolean = False)
        Me.Required = Required
        Me.ErrorMessage = ErrorMessage
        Me.IgnoreCase = IgnoreCase
    End Sub
    Public Overrides Function Validate(Str As String) As String
        Return If(String.Compare(Str, Required, IgnoreCase) = 0, "", ErrorMessage)
    End Function

End Class

''' <summary>
''' Rough specialized validation of a folder name.
''' </summary>
Public Class ValidateFolderName
    Inherits Validate
    Public Property Folder As String
    Public Property UseMinecraftCharCheck As Boolean = True
    Private ReadOnly PathIgnore As IEnumerable(Of String)
    Private ReadOnly IgnoreList As List(Of String)
    Public Sub New()
    End Sub
    Public Sub New(Folder As String, Optional UseMinecraftCharCheck As Boolean = True, Optional IgnoreList As List(Of String) = Nothing)
        Me.Folder = Folder
        Me.UseMinecraftCharCheck = UseMinecraftCharCheck
        On Error Resume Next
        PathIgnore = DirectoryUtils.EnumerateDirectories(Folder)
        Me.IgnoreList = IgnoreList
    End Sub
    Public Overrides Function Validate(Str As String) As String
        Try
            'Check for empty
            Dim LengthCheck As String = New ValidateNullOrWhiteSpace().Validate(Str)
            If LengthCheck <> "" Then Return LengthCheck
            'Check for spaces
            If Str.StartsWithF(" ") Then Return "文件夹名不能以空格开头！"
            If Str.EndsWithF(" ") Then Return "文件夹名不能以空格结尾！"
            'Check length
            LengthCheck = New ValidateLength(1, 100).Validate(Str)
            If LengthCheck <> "" Then Return LengthCheck
            'Check for a trailing dot
            If Str.EndsWithF(".") Then Return "文件夹名不能以小数点结尾！"
            'Check for special characters
            Dim CharactCheck As String = New ValidateExcept(Path.GetInvalidFileNameChars() & If(UseMinecraftCharCheck, "!;", ""), "文件夹名不可包含 % 字符！").Validate(Str)
            If CharactCheck <> "" Then Return CharactCheck
            'Check for special strings
            Dim InvalidStrCheck As String = New ValidateExceptSame({"CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"}, "文件夹名不可为 %！", True).Validate(Str)
            If InvalidStrCheck <> "" Then Return InvalidStrCheck
            'Check for NTFS 8.3 file names (#4505)
            If Str.RegexCheck(".{2,}~\d") Then Return "文件夹名不能包含这一特殊格式！"
            'Check for duplicate folder names
            Dim Arr As New List(Of String)
            Dim folderName As String
            If PathIgnore IsNot Nothing Then
                For Each FolderPath In PathIgnore
                    folderName = PathUtils.GetLastPart(FolderPath)
                    If IgnoreList Is Nothing OrElse Not IgnoreList.Contains(folderName, StringComparer.OrdinalIgnoreCase) Then Arr.Add(folderName)
                Next
            End If
            Dim SameNameCheck = New ValidateExceptSame(Arr, "不可与现有文件夹重名！", True).Validate(Str)
            If Not SameNameCheck = "" Then Return SameNameCheck
            Return ""
        Catch ex As Exception
            Logger.Warn(ex, "检查文件夹名出错")
            Return "错误：" & ex.Message
        End Try
    End Function
End Class

''' <summary>
''' Rough specialized validation of a file name.
''' </summary>
Public Class ValidateFileName
    Inherits Validate
    Public Property Name As String
    Public Property UseMinecraftCharCheck As Boolean = True
    Public Property IgnoreCase As Boolean = True
    Public Property ParentFolder As String = Nothing
    Public Property RequireParentFolderExists = True
    Public Sub New()
    End Sub
    Public Sub New(Name As String, Optional UseMinecraftCharCheck As Boolean = True, Optional IgnoreCase As Boolean = True)
        Me.Name = Name
        Me.IgnoreCase = IgnoreCase
        Me.UseMinecraftCharCheck = UseMinecraftCharCheck
    End Sub
    Public Overrides Function Validate(Str As String) As String
        Try
            'Check for empty
            Dim LengthCheck As String = New ValidateNullOrWhiteSpace().Validate(Str)
            If Not LengthCheck = "" Then Return LengthCheck
            'Check for spaces
            If Str.StartsWithF(" ") Then Return "文件名不能以空格开头！"
            If Str.EndsWithF(" ") Then Return "文件名不能以空格结尾！"
            'Check length
            LengthCheck = New ValidateLength(1, 253).Validate(Str & If(ParentFolder, ""))
            If LengthCheck <> "" Then Return LengthCheck
            'Check for a trailing dot
            If Str.EndsWithF(".") Then Return "文件名不能以小数点结尾！"
            'Check for special characters
            Dim CharactCheck As String = New ValidateExcept(Path.GetInvalidFileNameChars() & If(UseMinecraftCharCheck, "!;", ""), "文件名不可包含 % 字符！").Validate(Str)
            If CharactCheck <> "" Then Return CharactCheck
            'Check for special strings
            Dim InvalidStrCheck As String = New ValidateExceptSame({"CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"}, "文件名不可为 %！", True).Validate(Str)
            If InvalidStrCheck <> "" Then Return InvalidStrCheck
            'Check for NTFS 8.3 file names (#4505)
            If Str.RegexCheck(".{2,}~\d") Then Return "文件名不能包含这一特殊格式！"
            'Check for duplicate file names
            If ParentFolder IsNot Nothing Then
                Dim DirInfo = DirectoryUtils.GetInfo(ParentFolder)
                If DirInfo.Exists Then
                    Dim SameNameCheck = New ValidateExceptSame(DirInfo.EnumerateFiles("*").Select(Function(f) f.Name),
                                                               "不可与现有文件重名！", IgnoreCase).Validate(Str)
                    If Not SameNameCheck = "" Then Return SameNameCheck
                Else
                    If RequireParentFolderExists Then Return $"父文件夹不存在：{ParentFolder}"
                End If
            End If
            Return ""
        Catch ex As Exception
            Logger.Warn(ex, "检查文件名出错")
            Return "错误：" & ex.Message
        End Try
    End Function
End Class

''' <summary>
''' Requires a usable folder path.
''' </summary>
Public Class ValidateFolderPath
    Inherits Validate
    Public Property UseMinecraftCharCheck As Boolean = True
    Public Sub New()
    End Sub
    Public Sub New(UseMinecraftCharCheck As Boolean)
        Me.UseMinecraftCharCheck = UseMinecraftCharCheck
    End Sub
    Public Overrides Function Validate(Str As String) As String
        'Strip trailing slashes and normalize to \
        Str = Str.Replace("/", "\")
        If Not PathUtils.RemoveSlashSuffix(Str).EndsWithF(":") Then Str = PathUtils.RemoveSlashSuffix(Str)
        'Check for empty
        Dim LengthCheck As String = New ValidateNullOrWhiteSpace().Validate(Str)
        If LengthCheck <> "" Then Return LengthCheck
        'Check length
        LengthCheck = New ValidateLength(1, 254).Validate(Str)
        If LengthCheck <> "" Then Return LengthCheck
        'Check for NTFS 8.3 file names (#4505)
        Dim Match = Str.RegexSeek(".{2,}~\d")
        If Match IsNot Nothing Then Return $"文件夹名不能包含 {Match} 这一特殊格式！"
        'Check the beginning
        If Str.StartsWithF("\\Mac\") Then GoTo Fin
        For Each Drive In Environment.GetLogicalDrives().Where(Function(p) DirectoryUtils.Exists(p))
            Dim DriveName As String = Drive.Upper.First & ":\"
            If Str.Upper = DriveName Then Return ""
            If Str.StartsWithF(DriveName, True) Then GoTo Fin
        Next
        Return "文件夹路径头存在错误！"
Fin:
        'Check the path beyond the first level
        For i = If(Str.StartsWithF("\\Mac\"), 2, 1) To Str.Split("\").Count - 1
            Dim SubStr As String = Str.Split("\")(i)
            'Check for empty
            Dim SubLengthCheck As String = New ValidateNullOrWhiteSpace().Validate(SubStr)
            If Not SubLengthCheck = "" Then Return "文件夹路径存在错误！"
            'Check for special characters
            Dim CharactCheck As String = New ValidateExcept(Path.GetInvalidFileNameChars() & If(UseMinecraftCharCheck, "!;", ""), "路径中存在无效字符！").Validate(SubStr)
            If Not CharactCheck = "" Then Return CharactCheck
            'Check for leading spaces
            If SubStr.StartsWithF(" ") Then Return "文件夹名不能以空格开头！"
            If SubStr.EndsWithF(" ") Then Return "文件夹名不能以空格结尾！"
            'Check for a trailing dot
            If SubStr.EndsWithF(".") Then Return "文件夹名不能以小数点结尾！"
            'Check for special strings
            Dim InvalidStrCheck As String = New ValidateExceptSame({"CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"}, "文件夹名不可为 %！").Validate(SubStr)
            If Not InvalidStrCheck = "" Then Return InvalidStrCheck
        Next
        Return ""
    End Function
End Class
