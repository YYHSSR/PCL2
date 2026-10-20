Public NotInheritable Class LaunchJvmPolicy
    'Leave heap region size and young-generation sizing to HotSpot ergonomics.
    Public Shared Function G1Arguments(JavaVersion As Version, Tune As Boolean, DeduplicateStrings As Boolean, DisablePerfSharedMemory As Boolean) As List(Of String)
        Dim Args As New List(Of String)
        If JavaVersion.Major < 7 Then Return Args
        If JavaVersion.Major = 7 Then Args.Add("-XX:+UnlockExperimentalVMOptions")
        Args.Add("-XX:+UseG1GC")
        If Tune Then
            Args.Add("-XX:MaxGCPauseMillis=100")
            If JavaVersion.Major >= 8 Then Args.Add("-XX:+ParallelRefProcEnabled")
        End If
        If DeduplicateStrings AndAlso (JavaVersion.Major > 8 OrElse JavaVersion >= New Version(8, 0, 20)) Then Args.Add("-XX:+UseStringDeduplication")
        If DisablePerfSharedMemory Then Args.Add("-XX:+PerfDisableSharedMem")
        Return Args
    End Function
End Class
