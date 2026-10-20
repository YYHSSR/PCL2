Public Class PclLogger
    Inherits FileLogger
    Public Overrides Function Format(Text As String, Level As LogLevel, FilePath As String, Ex As Exception) As String
        Return FilterUserName(FilterAccessToken(MyBase.Format(Text, Level, FilePath, Ex), "*"c), "*"c)
    End Function
    Public Overrides Sub HandleBehavior(RawMessage As String, FormattedMessage As String, Behavior As LogBehavior, Ex As Exception)
        If IsProgramEnding Then Return
        MyBase.HandleBehavior(RawMessage, FormattedMessage, Behavior, Ex)
        Dim BriefText = If(Ex Is Nothing, RawMessage, If(RawMessage Is Nothing, "", $"{RawMessage}：") & Ex.GetDisplay(False))
        BriefText = FilterUserName(FilterAccessToken(BriefText, "*"c), "*"c)
        Dim DetailText = If(Ex Is Nothing, RawMessage, If(RawMessage Is Nothing, "", $"{RawMessage}：") & Ex.GetDisplay(True))
        DetailText = FilterUserName(FilterAccessToken(DetailText, "*"c), "*"c)
        Select Case Behavior
            Case LogBehavior.ToastIfDebug
                If ModeDebug Then Hint("[开发者模式] " & BriefText, HintType.Blue, False)
            Case LogBehavior.Toast
                Hint(BriefText, HintType.Red, False)
            Case LogBehavior.Alert
                MyMsgBox(DetailText, "错误", IsWarn:=True)
            Case LogBehavior.AlertThenCrash
                Static FirstTrigger As Boolean = True
                If FirstTrigger Then
                    FirstTrigger = False
                    MsgBox(DetailText & vbCrLf & vbCrLf & "详细信息已写入本地日志。", MsgBoxStyle.Critical, "错误")
                Else
                    Thread.Sleep(2000)
                End If
                FormMain.EndProgramForce(ProcessReturnValues.Exception)
        End Select
    End Sub
End Class
