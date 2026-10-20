Public NotInheritable Class ModDirectoryCache
    Private Class Entry
        Public Value As Integer
        Public Scan As Task(Of Integer)
        Public Started As Long
    End Class
    Private Shared ReadOnly Entries As New Dictionary(Of String, Entry)(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly EntriesLock As New Object

    'UI reads use a short-lived snapshot; launch workers wait for a fresh scan.
    Public Shared Function GetCount(Folder As String, Optional WaitForRefresh As Boolean = False) As Integer
        Dim Cached As Entry = Nothing
        SyncLock EntriesLock
            If Not Entries.TryGetValue(Folder, Cached) Then
                If Entries.Count >= 128 Then Entries.Remove(Entries.Keys.First())
                Cached = New Entry()
                Entries.Add(Folder, Cached)
            End If
        End SyncLock
        Dim Pending As Task(Of Integer)
        SyncLock Cached
            Dim NowTicks = Stopwatch.GetTimestamp()
            If Cached.Scan Is Nothing OrElse (Cached.Scan.IsCompleted AndAlso
                (WaitForRefresh OrElse NowTicks - Cached.Started >= Stopwatch.Frequency * 5L)) Then
                Cached.Started = NowTicks
                Cached.Scan = Task.Run(Function()
                                           Try
                                               Dim Count = Directory.EnumerateFiles(Folder).Count(Function(File)
                                                                                                     Select Case Path.GetExtension(File).ToLowerInvariant()
                                                                                                         Case ".jar", ".zip", ".litemod"
                                                                                                             Return True
                                                                                                         Case Else
                                                                                                             Return False
                                                                                                     End Select
                                                                                                 End Function)
                                               Volatile.Write(Cached.Value, Count)
                                               Return Count
                                           Catch ex As DirectoryNotFoundException
                                               Volatile.Write(Cached.Value, 0)
                                               Return 0
                                           Catch ex As IOException
                                               Logger.Warn(ex, "读取 Mod 文件夹失败")
                                           Catch ex As UnauthorizedAccessException
                                               Logger.Warn(ex, "读取 Mod 文件夹失败")
                                           End Try
                                           Return Volatile.Read(Cached.Value)
                                       End Function)
            End If
            Pending = Cached.Scan
        End SyncLock
        Return If(WaitForRefresh, Pending.GetAwaiter().GetResult(), Volatile.Read(Cached.Value))
    End Function
End Class
