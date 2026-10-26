Imports System.Net.Sockets

Public Module ModNet

#Region "Network Requests"

    ''' <summary>
    ''' Sends a network request and gets the response content.
    ''' </summary>
    Public Function NetRequestByClient(Url As String, Optional Method As HttpMethod = Nothing,
            Optional Content As Object = Nothing, Optional ContentType As String = Nothing, Optional Accept As String = "*/*",
            Optional Timeout As Integer = 25000, Optional Headers As String(,) = Nothing, Optional RequireJson As Boolean = False,
            Optional Encoding As Encoding = Nothing, Optional SimulateBrowserHeaders As Boolean = False, Optional MakeLog As Boolean = True, Optional RequestCancellation As CancellationToken = Nothing) As String
        If Method Is Nothing Then Method = HttpMethod.Get
        If MakeLog Then Logger.Info($"发起网络请求（{Method.Method}，{Url}），最大超时 {Timeout}")
        Try
            Dim HeaderDictionary = If(Headers, {}).ToDictionary
            If ContentType IsNot Nothing Then HeaderDictionary("Content-Type") = ContentType
            If Accept IsNot Nothing Then HeaderDictionary("Accept") = Accept
            Dim Result = SendRequest(Url, Method, Content, HeaderDictionary, Timeout:=Timeout, Encoding:=Encoding, SimulateBrowserHeaders:=SimulateBrowserHeaders, RequestCancellation:=RequestCancellation).GetString(Encoding)
            'Check whether the result is complete JSON (it may be empty in #6683, or incomplete when truncated by the GFW)
            'Checking and throwing here lets methods like NetRequestByClientRetry retry
            If RequireJson Then
                Result = Result.Trim((vbCrLf & " ").ToCharArray) 'Authlib-Injector results end with an extra blank line
                If Not (Result.StartsWithF("{") AndAlso Result.EndsWithF("}")) AndAlso
                   Not (Result.StartsWithF("[") AndAlso Result.EndsWithF("]")) Then
                    Throw New FormatException("返回结果并非 JSON 格式：" &
                    If(Result.Length > 2000, Result.Substring(0, 1000) & "..." & Result.Substring(Result.Length - 1000), Result))
                End If
            End If
            Return Result
        Catch ex As FormatException
            Throw
        Catch ex As Exception
            If MakeLog Then Logger.Warn(ex, $"网络请求失败（{Method.Method}，{Url}）")
            Throw
        End Try
    End Function
    ''' <summary>
    ''' Sends a network request and gets the response content.
    ''' Tries up to 3 times within 45 seconds, allowing a timeout of up to 30s.
    ''' </summary>
    ''' <param name="BackupUrl">Backup URL to use if the first attempt fails.</param>
    Public Function NetRequestByClientRetry(Url As String, Optional Method As HttpMethod = Nothing, Optional BackupUrl As String = Nothing,
            Optional Content As Object = Nothing, Optional ContentType As String = Nothing, Optional Accept As String = "*/*",
            Optional Headers As String(,) = Nothing, Optional RequireJson As Boolean = False,
            Optional Encoding As Encoding = Nothing, Optional SimulateBrowserHeaders As Boolean = False) As String
        Dim RetryCount As Integer = 0
        Dim RetryException As Exception = Nothing
        Dim StartTime As Long = GetTimeMs()
        Dim RequestUrl As String = Url
        Try
Retry:
            RequestUrl = If(RetryCount > 0 AndAlso Not String.IsNullOrWhiteSpace(BackupUrl), BackupUrl, Url)
            Select Case RetryCount
                Case 0 'Normal attempt
                    Return NetRequestByClient(RequestUrl, Method, Content, ContentType, Accept, 10000, Headers, RequireJson, Encoding, SimulateBrowserHeaders)
                Case 1 'Slow retry
                    Thread.Sleep(500)
                    Return NetRequestByClient(RequestUrl, Method, Content, ContentType, Accept, 30000, Headers, RequireJson, Encoding, SimulateBrowserHeaders)
                Case Else 'Fast retry
                    If GetTimeMs() - StartTime > 5500 Then
                        'Only retry if the first two attempts took more than 5 seconds
                        Thread.Sleep(500)
                        Return NetRequestByClient(RequestUrl, Method, Content, ContentType, Accept, 4000, Headers, RequireJson, Encoding, SimulateBrowserHeaders)
                    Else
                        Throw RetryException
                    End If
            End Select
        Catch ex As Exception
            ex.ThrowIfCanceled()
            If TypeOf ex Is HttpRequestCodeException Then
                If CType(ex, HttpRequestCodeException).StatusCode = HttpStatusCode.Forbidden Then Throw
                If CType(ex, HttpRequestCodeException).StatusCode = HttpStatusCode.NotFound AndAlso (RetryCount > 0 OrElse String.IsNullOrWhiteSpace(BackupUrl)) Then Throw
                If CType(ex, HttpRequestCodeException).StatusCode = 429 Then Thread.Sleep(10000) 'Too Many Requests
            End If
            'Retry
            Select Case RetryCount
                Case 0
                    RetryException = ex
                    RetryCount += 1
                    GoTo Retry
                Case 1
                    RetryCount += 1
                    GoTo Retry
                Case Else
                    Throw
            End Select
        End Try
    End Function
    ''' <summary>
    ''' Starts several threads that send network requests simultaneously and gets the response content.
    ''' Each thread starts with a slight delay.
    ''' </summary>
    Public Function NetRequestByClientMultiple(Url As String, Optional Method As HttpMethod = Nothing, Optional ThreadCount As Integer = 3,
            Optional Content As Object = Nothing, Optional ContentType As String = Nothing, Optional Accept As String = "*/*",
            Optional Headers As String(,) = Nothing, Optional RequireJson As Boolean = False, Optional Timeout As Integer = 25000,
            Optional Encoding As Encoding = Nothing, Optional SimulateBrowserHeaders As Boolean = False) As String
        If ThreadCount < 1 OrElse ThreadCount > 64 Then Throw New ArgumentOutOfRangeException(NameOf(ThreadCount))
        If Timeout <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(Timeout))
        If Method Is Nothing Then Method = HttpMethod.Get
        'Do not send duplicate refresh/login POST requests.
        If Method <> HttpMethod.Get AndAlso Method <> HttpMethod.Head Then ThreadCount = 1
        Using Cancellation As New CancellationTokenSource(Timeout)
            Dim Token = Cancellation.Token
            Dim Requests As New List(Of Task(Of String))
            For i = 0 To ThreadCount - 1
                Dim DelayMs = i * 250
                Requests.Add(Task.Run(Async Function() As Task(Of String)
                                          Await Task.Delay(DelayMs, Token).ConfigureAwait(False)
                                          Return NetRequestByClient(Url, Method, Content, ContentType, Accept, Timeout, Headers, RequireJson, Encoding, SimulateBrowserHeaders, RequestCancellation:=Token)
                                      End Function))
            Next
            Dim LastError As Exception = Nothing
            Try
                While Requests.Count > 0
                    Dim Completed = Task.WhenAny(Requests).GetAwaiter().GetResult()
                    Requests.Remove(Completed)
                    Try
                        Return Completed.GetAwaiter().GetResult()
                    Catch ex As Exception
                        If Cancellation.IsCancellationRequested Then Throw New TimeoutException("并发网络请求已超过整体超时。", ex)
                        LastError = ex
                    End Try
                End While
                Throw If(LastError, New HttpRequestException("所有网络请求均失败。"))
            Finally
                Cancellation.Cancel()
                'Observe faults from requests that lost the race.
                For Each Pending In Requests
                    Pending.ContinueWith(Sub(t)
                                             Dim Observed = t.Exception
                                         End Sub, TaskContinuationOptions.OnlyOnFaulted)
                Next
            End Try
        End Using
    End Function

    ''' <summary>
    ''' Gets the content by downloading the web file with multiple threads.
    ''' Cache negotiation is not supported; use NetRequestByClient if it is needed.
    ''' </summary>
    Public Function NetRequestByLoader(Url As String, Optional Timeout As Integer = 45000, Optional IsJson As Boolean = False, Optional SimulateBrowserHeaders As Boolean = False) As String
        Return NetRequestByLoader({Url}, Timeout, IsJson, SimulateBrowserHeaders)
    End Function
    ''' <summary>
    ''' Gets the content by downloading the web file with multiple threads.
    ''' Cache negotiation is not supported; use NetRequestByClient if it is needed.
    ''' </summary>
    Public Function NetRequestByLoader(Urls As IEnumerable(Of String), Optional Timeout As Integer = 45000, Optional IsJson As Boolean = False, Optional SimulateBrowserHeaders As Boolean = False) As String
        Dim Temp As String = RequestTaskTempFolder() & "download.txt"
        Dim NewTask As New LoaderDownload("源码获取 " & GetUuid() & "#", New List(Of NetFile) From {New NetFile(Urls, Temp, New FileChecker With {.IsJson = IsJson}, SimulateBrowserHeaders)})
        Try
            NewTask.WaitForExitTime(Timeout, TimeoutMessage:="连接服务器超时（第一下载源：" & Urls.First & "）")
            Dim Result = FileUtils.ReadAsString(Temp)
            FileUtils.Delete(Temp)
            Return Result
        Finally
            NewTask.Cancel()
        End Try
    End Function

#End Region

#Region "File Download"

    ''' <summary>
    ''' Downloads a small file directly via a network request, overwriting any existing file.
    ''' Not recommended for large files.
    ''' </summary>
    Public Sub NetDownloadByClient(Url As String, LocalPath As String, Optional SimulateBrowserHeaders As Boolean = False)
        Logger.Info($"通过网络请求直接下载小文件：{Url} → {LocalPath}")
        Dim RetryCount As Integer = 0
Retry:
        Try
            FileUtils.Write(LocalPath, SendRequest(Url, HttpMethod.Get, SimulateBrowserHeaders:=SimulateBrowserHeaders))
        Catch ex As Exception
            FileUtils.Delete(LocalPath)
            ex.ThrowIfCanceled()
            If RetryCount < 2 Then
                RetryCount += 1
                GoTo Retry
            End If
            Throw New HttpRequestException($"通过网络请求直接下载小文件失败（{LocalPath}）", ex)
        End Try
    End Sub

    ''' <summary>
    ''' Downloads a file via the multi-threaded download engine.
    ''' </summary>
    Public Sub NetDownloadByLoader(Url As String, LocalPath As String, Optional LoaderToSyncProgress As LoaderBase = Nothing, Optional Check As FileChecker = Nothing, Optional SimulateBrowserHeaders As Boolean = False)
        NetDownloadByLoader({Url}, LocalPath, LoaderToSyncProgress, Check, SimulateBrowserHeaders)
    End Sub
    ''' <summary>
    ''' Downloads a file via the multi-threaded download engine.
    ''' </summary>
    ''' <param name="Urls">List of Urls of the file.</param>
    ''' <param name="LocalPath">Local download path.</param>
    Public Sub NetDownloadByLoader(Urls As IEnumerable(Of String), LocalPath As String, Optional LoaderToSyncProgress As LoaderBase = Nothing, Optional Check As FileChecker = Nothing, Optional SimulateBrowserHeaders As Boolean = False)
        Dim NewTask As New LoaderDownload($"文件下载 {GetUuid()}#", New List(Of NetFile) From {New NetFile(Urls, LocalPath, Check, SimulateBrowserHeaders)})
        Try
            NewTask.WaitForExit(LoaderToSyncProgress:=LoaderToSyncProgress)
        Catch ex As Exception
            Throw New WebException($"多线程直接下载文件失败（第一下载源：" & Urls.First() & "）", ex)
        Finally
            NewTask.Cancel()
        End Try
    End Sub

#End Region

#Region "Basic Request Engine"

    'Basic request functions
    Private Function SendRequest(Url As String, Method As HttpMethod,
            Optional Content As Object = Nothing, Optional Headers As Dictionary(Of String, String) = Nothing,
            Optional SimulateBrowserHeaders As Boolean = False, Optional Timeout As Integer = 25000, Optional Encoding As Encoding = Nothing, Optional RequestCancellation As CancellationToken = Nothing) As Byte()
        If RunInUi() AndAlso Not Url.Contains("//127.") Then Throw New Exception("在 UI 线程执行了网络请求")
        Dim Request As HttpRequestMessage = Nothing, CancelToken As CancellationTokenSource = Nothing, Response As HttpResponseMessage = Nothing
        Try
            'Build the RequestClient
            Directory.CreateDirectory(PathTemp & "Cache\Http\")
            SyncLock RequestClientLock
                If RequestClient Is Nothing Then 'Lazy initialization, to avoid DLL load failures from loading CacheCow before the program starts
                    RequestClient = CacheCow.Client.ClientExtensions.CreateClient(New CacheCow.Client.FileCacheStore.FileStore(PathTemp & "Cache/Http/"), New HttpClientHandler With {
                        .AutomaticDecompression = DecompressionMethods.Deflate Or DecompressionMethods.GZip,
                        .UseCookies = False 'Must be False to pass Cookies manually via the Header
                    })
                End If
            End SyncLock
            'Build the request
            Url = SecretCdnSign(Url)
            Request = New HttpRequestMessage(Method, Url)
            If Content IsNot Nothing AndAlso Request.Method <> HttpMethod.Get AndAlso Request.Method <> HttpMethod.Head Then 'Write the Content
                If TypeOf Content Is HttpContent Then
                    Request.Content = DirectCast(Content, HttpContent).Clone()
                ElseIf TypeOf Content Is Byte() Then
                    Request.Content = New ByteArrayContent(DirectCast(Content, Byte()))
                ElseIf Content IsNot Nothing Then
                    Request.Content = New ByteArrayContent(If(Encoding, Encoding.UTF8).GetBytes(Content.ToString()))
                End If
            End If
            For Each Header In If(Headers, New Dictionary(Of String, String)) 'Write the Headers
                If Header.Key.Lower = "content-type" Then
                    Request.Content?.Headers.TryAddWithoutValidation(Header.Key, Header.Value)
                Else
                    Request.Headers.TryAddWithoutValidation(Header.Key, Header.Value)
                End If
            Next
            SecretHeadersSign(Url, Request, SimulateBrowserHeaders)
            'Send the request
            'If the file is locked when CacheCow reads the cache, it calls the nonexistent System.Net.Http.Formatting.resources and throws (#8150)
            Retrier.Attempt(delay:=Function() TimeSpan.FromMilliseconds(200), isRetryAllowed:=Function(ex) TypeOf ex Is FileNotFoundException OrElse TypeOf ex Is IOException, action:=
            Sub(Attempt)
                CancelToken?.Dispose() : CancelToken = CancellationTokenSource.CreateLinkedTokenSource(RequestCancellation)
                CancelToken.CancelAfter(Timeout)
                Dim ClonedRequest As HttpRequestMessage = Request.Clone() 'HttpRequestMessage can only be sent once, so it must be cloned on retry
                Try
                    Response = If(CurseForgeAccess.IsAuthenticatedHost(ClonedRequest.RequestUri),
                        CurseForgeAccess.SendAsync(AuthenticatedResourceClient, ClonedRequest, CancelToken.Token),
                        RequestClient.SendAsync(ClonedRequest, HttpCompletionOption.ResponseHeadersRead, CancelToken.Token)).GetResultWithTimeout(CancelToken, Timeout)
                Finally
                    ClonedRequest.Dispose()
                End Try
            End Sub)
            'Read the response
            Dim ResponseBytes As Byte()
            CancelToken.Dispose() : CancelToken = CancellationTokenSource.CreateLinkedTokenSource(RequestCancellation)
            CancelToken.CancelAfter(Timeout)
            Using ResponseStream = Response.Content.ReadAsStreamAsync().GetResultWithTimeout(CancelToken, Timeout)
                Using Stream As New MemoryStream
                    ResponseStream.CopyToAsync(Stream, 81920, CancelToken.Token).GetResultWithTimeout(CancelToken, Timeout)
                    ResponseBytes = Stream.ToArray()
                End Using
            End Using
            If Not Response.IsSuccessStatusCode Then
                Dim ResponseMessage = ResponseBytes.GetString(Encoding)
                Throw New HttpRequestCodeException(
                    $"错误码 {Response.StatusCode} ({CInt(Response.StatusCode)})，{Method}，{Url}" &
                    If(String.IsNullOrEmpty(ResponseMessage), "", vbCrLf & ResponseMessage), Response.StatusCode, ResponseMessage)
            End If
            Return ResponseBytes
        Catch ex As HttpRequestException
            Throw
        Catch ex As TimeoutException
            Throw New WebException($"连接服务器超时，请稍后再试，或使用 VPN 改善网络环境（{Method}, {Url}）", WebExceptionStatus.Timeout)
        Catch ex As Exception
            RequestCancellation.ThrowIfCancellationRequested()
            If TypeOf ex IsNot TaskCanceledException Then ex.ThrowIfCanceled()
            If ex.IsBadNetwork Then
                Throw New WebException($"网络请求失败，请稍后再试，或使用 VPN 改善网络环境（{Method}, {Url}）", WebExceptionStatus.Timeout)
            Else
                Throw New Exception($"网络请求出现意外异常（{Method}, {Url}）", ex)
            End If
        Finally
            Request?.Dispose()
            CancelToken?.Dispose()
            Response?.Dispose()
        End Try
    End Function
    Private RequestClient As HttpClient = Nothing
    Private ReadOnly AuthenticatedResourceClient As New HttpClient(New HttpClientHandler With {
        .AutomaticDecompression = DecompressionMethods.Deflate Or DecompressionMethods.GZip,
        .UseCookies = False, .AllowAutoRedirect = False
    }) With {.Timeout = Timeout.InfiniteTimeSpan}
    Private RequestClientLock As New Object

#End Region

#Region "Multi-threaded Download Engine"

    Private ThreadClient As New HttpClient(New HttpClientHandler With {
        .AutomaticDecompression = DecompressionMethods.Deflate Or DecompressionMethods.GZip
    })

    ''' <summary>
    ''' Maximum thread count.
    ''' </summary>
    Public ReadOnly Property NetTaskThreadLimit As Integer
        Get
            Return Settings.Get(Of Integer)("ToolDownloadThread") + 1
        End Get
    End Property
    ''' <summary>
    ''' Speed lower bound.
    ''' </summary>
    Public NetTaskSpeedLimitLow As Long = 256 * 1024L '256K/s
    ''' <summary>
    ''' Speed upper bound. -1 if unlimited.
    ''' </summary>
    Public ReadOnly Property NetTaskSpeedLimitHigh As Long
        Get
            If _NetTaskSpeedLimitHigh Is Nothing Then
                Dim Setting As Integer = Settings.Get(Of Integer)("ToolDownloadSpeed")
                If Setting <= 14 Then
                    _NetTaskSpeedLimitHigh = (Setting + 1) * 0.1 * 1024 * 1024L
                ElseIf Setting <= 31 Then
                    _NetTaskSpeedLimitHigh = (Setting - 11) * 0.5 * 1024 * 1024L
                ElseIf Setting <= 41 Then
                    _NetTaskSpeedLimitHigh = (Setting - 21) * 1024 * 1024L
                Else
                    _NetTaskSpeedLimitHigh = -1
                End If
            End If
            Return _NetTaskSpeedLimitHigh.Value
        End Get
    End Property
    Private _NetTaskSpeedLimitHigh As Long? = Nothing
    Public Sub UpdateNetTaskSpeedLimitHigh(Value As Integer)
        _NetTaskSpeedLimitHigh = Nothing
    End Sub
    ''' <summary>
    ''' Remaining amount that can be downloaded under the speed limit.
    ''' </summary>
    Public NetTaskSpeedLimitLeft As Long = -1
    ''' <summary>
    ''' Number of running threads.
    ''' </summary>
    Public NetTaskThreadCount As Integer = 0

    ''' <summary>
    ''' Download source.
    ''' </summary>
    Public Class NetSource
        Public Id As Integer
        Public Url As String
        Public FailCount As Integer
        Public Ex As Exception
        ''' <summary>
        ''' If this source is being downloaded in forced single-thread mode, marks that single thread.
        ''' </summary>
        Public SingleThread As NetThread
        Public IsFailed As Boolean
        Public Overrides Function ToString() As String
            Return Url
        End Function
    End Class
    ''' <summary>
    ''' Download progress states.
    ''' </summary>
    Public Enum NetState
        ''' <summary>
        ''' The existence check has not been done yet.
        ''' </summary>
        WaitingToCheck = -1
        ''' <summary>
        ''' Not started yet.
        ''' </summary>
        WaitingToDownload = 0
        ''' <summary>
        ''' Connecting; file size not obtained yet.
        ''' </summary>
        Connecting = 1
        ''' <summary>
        ''' Connected; trying to receive the first data packet.
        ''' </summary>
        Reading = 2
        ''' <summary>
        ''' Downloading.
        ''' </summary>
        Downloading = 3
        ''' <summary>
        ''' Merging files.
        ''' </summary>
        Merging = 4
        ''' <summary>
        ''' Finished.
        ''' </summary>
        Finished = 5
        ''' <summary>
        ''' Failed or aborted.
        ''' </summary>
        Canceled = 6
    End Enum
    ''' <summary>
    ''' Pre-download check behavior.
    ''' </summary>
    Public Enum NetPreDownloadBehaviour
        ''' <summary>
        ''' If the file already exists, show a hint asking the user whether to continue downloading.
        ''' </summary>
        HintWhileExists
        ''' <summary>
        ''' If the file already exists or is being downloaded, exit the download function directly without prompting the user.
        ''' </summary>
        ExitWhileExistsOrDownloading
        ''' <summary>
        ''' Do not check whether the file exists.
        ''' </summary>
        IgnoreCheck
    End Enum

    ''' <summary>
    ''' Download thread.
    ''' </summary>
    Public Class NetThread
        Implements IEnumerable(Of NetThread), IEquatable(Of NetThread)

        ''' <summary>
        ''' The corresponding download task.
        ''' </summary>
        Public Task As NetFile
        ''' <summary>
        ''' The corresponding thread.
        ''' </summary>
        Public Thread As Thread
        ''' <summary>
        ''' The next thread in the linked list.
        ''' </summary>
        Public NextThread As NetThread
        Private ReadOnly Iterator Property [Next]() As IEnumerable(Of NetThread)
            Get
                Dim CurrentChain As NetThread = Me
                While CurrentChain IsNot Nothing
                    Yield CurrentChain
                    CurrentChain = CurrentChain.NextThread
                End While
            End Get
        End Property
        Public Function GetEnumerator() As IEnumerator(Of NetThread) Implements IEnumerable(Of NetThread).GetEnumerator
            Return [Next].GetEnumerator()
        End Function
        Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
            Return [Next].GetEnumerator()
        End Function

        ''' <summary>
        ''' Number assigned to each thread in the task (whether or not it failed).
        ''' </summary>
        Public Uuid As Integer
        ''' <summary>
        ''' Whether this is the first thread.
        ''' </summary>
        Public ReadOnly Property IsFirstThread As Boolean
            Get
                Return DownloadStart = 0 AndAlso Task.FileSize = -2
            End Get
        End Property
        ''' <summary>
        ''' Cache file of this thread.
        ''' </summary>
        Public Temp As String

        ''' <summary>
        ''' Download start position of the thread.
        ''' </summary>
        Public DownloadStart As Long
        ''' <summary>
        ''' Download end position of the thread.
        ''' </summary>
        Public ReadOnly Property DownloadEnd As Long
            Get
                SyncLock Task.LockChain
                    If NextThread Is Nothing Then
                        If Task.IsUnknownSize Then
                            Return 1000 * 1000 * 1000 * 1000L 'About 1T
                        Else
                            Return Task.FileSize - 1
                        End If
                    Else
                        Return NextThread.DownloadStart - 1
                    End If
                End SyncLock
            End Get
        End Property
        ''' <summary>
        ''' Size not yet downloaded by the thread.
        ''' </summary>
        Public ReadOnly Property DownloadUndone As Long
            Get
                Return DownloadEnd - (DownloadStart + DownloadDone) + 1
            End Get
        End Property
        ''' <summary>
        ''' Size already downloaded by the thread.
        ''' </summary>
        Public DownloadDone As Long = 0

        ''' <summary>
        ''' Time of the last speed measurement.
        ''' </summary>
        Private SpeedLastTime As Long = GetTimeMs()
        ''' <summary>
        ''' Downloaded size at the last speed measurement.
        ''' </summary>
        Private SpeedLastDone As Long = 0
        ''' <summary>
        ''' Current download speed in bytes per second.
        ''' </summary>
        Public ReadOnly Property Speed As Long
            Get
                If GetTimeMs() - SpeedLastTime > 200 Then
                    Dim DeltaTime As Long = GetTimeMs() - SpeedLastTime
                    _Speed = (DownloadDone - SpeedLastDone) / (DeltaTime / 1000)
                    SpeedLastDone = DownloadDone
                    SpeedLastTime += DeltaTime
                End If
                Return _Speed
            End Get
        End Property
        Private _Speed As Long = 0

        ''' <summary>
        ''' Time when the thread was initialized.
        ''' </summary>
        Public InitTime As Long = GetTimeMs()
        ''' <summary>
        ''' Time when valid data was last received; -1 means no valid data yet.
        ''' </summary>
        Public LastReceiveTime As Long = -1

        ''' <summary>
        ''' State of the current thread.
        ''' </summary>
        Public State As NetState = NetState.WaitingToDownload
        ''' <summary>
        ''' Whether it has ended.
        ''' </summary>
        Public ReadOnly Property IsEnded As Boolean
            Get
                Return State = NetState.Finished OrElse State = NetState.Canceled
            End Get
        End Property

        ''' <summary>
        ''' Which Url is currently selected.
        ''' </summary>
        Public Source As NetSource

        'Allow UUID comparison
        Public Overloads Function Equals(other As NetThread) As Boolean Implements IEquatable(Of NetThread).Equals
            Return other IsNot Nothing AndAlso Uuid = other.Uuid
        End Function
        Public Overrides Function Equals(obj As Object) As Boolean
            Return Equals(TryCast(obj, NetThread))
        End Function
        Public Shared Operator =(left As NetThread, right As NetThread) As Boolean
            Return EqualityComparer(Of NetThread).Default.Equals(left, right)
        End Operator
        Public Shared Operator <>(left As NetThread, right As NetThread) As Boolean
            Return Not left = right
        End Operator
    End Class
    ''' <summary>
    ''' Downloads a single file.
    ''' </summary>
    Public Class NetFile

#Region "Properties"

        ''' <summary>
        ''' The file list task this belongs to.
        ''' </summary>
        Public Tasks As New ConcurrentList(Of LoaderDownload)
        ''' <summary>
        ''' All download sources.
        ''' </summary>
        Public Sources As List(Of NetSource)
        ''' <summary>
        ''' Used to switch download sources when the first thread fails.
        ''' </summary>
        Private FirstThreadSource As Integer = 0
        ''' <summary>
        ''' All sources that have been marked failed but not fully tried, and do not allow resuming.
        ''' </summary>
        Public SourcesOnce As New ConcurrentList(Of NetSource)
        ''' <summary>
        ''' Only when merging fails or the first download fails are all sources re-marked as not allowing resuming and retried one by one.
        ''' This handles cases where some sources return a wrong file, and sources that misbehave with multi-threaded downloads.
        ''' </summary>
        Private Retried As Boolean = False
        ''' <summary>
        ''' Gets the first available source starting from a given source.
        ''' </summary>
        Private Function GetSource(Optional Id As Integer = 0) As NetSource
            If Id >= Sources.Count OrElse Id < 0 Then Id = 0
            SyncLock LockSource
                If HasAvailableSource(False) Then
                    'A multi-thread capable source exists
                    Dim CurrentSource As NetSource = Sources(Id)
                    While CurrentSource.IsFailed
                        Id += 1
                        If Id >= Sources.Count Then Id = 0
                        CurrentSource = Sources(Id)
                    End While
                    Return CurrentSource
                ElseIf SourcesOnce.Any Then
                    'Only single-thread capable sources exist
                    Return SourcesOnce.First
                Else
                    'No available source
                    Return Nothing
                End If
            End SyncLock
        End Function
        ''' <summary>
        ''' Whether an available source exists.
        ''' </summary>
        Public Function HasAvailableSource(Optional AllowOnceSource As Boolean = True) As Boolean
            SyncLock LockSource
                If Sources.Any(Function(s) Not s.IsFailed) Then Return True 'A multi-thread capable source exists
                If AllowOnceSource AndAlso SourcesOnce.Any Then Return True 'A single-thread capable source exists
            End SyncLock
            Return False
        End Function

        ''' <summary>
        ''' Local path including the file name.
        ''' </summary>
        Public LocalPath As String = Nothing
        ''' <summary>
        ''' Local file name.
        ''' </summary>
        Public LocalName As String = Nothing

        ''' <summary>
        ''' Current download state.
        ''' </summary>
        Public State As NetState = NetState.WaitingToCheck
        ''' <summary>
        ''' The reason the download failed.
        ''' </summary>
        Public Ex As New List(Of Exception)

        ''' <summary>
        ''' Linked list of threads that make up the file.
        ''' May be Nothing if there are no threads.
        ''' </summary>
        Public Threads As NetThread

        ''' <summary>
        ''' Total size of the file. -2 if not obtained yet, -1 if the exact size cannot be obtained.
        ''' </summary>
        Public FileSize As Long = -2
        ''' <summary>
        ''' Whether the exact size of this file cannot be obtained.
        ''' </summary>
        Public IsUnknownSize As Boolean = False
        ''' <summary>
        ''' Whether this file does not need to be split.
        ''' </summary>
        Public ReadOnly Property IsNoSplit As Boolean
            Get
                Return IsUnknownSize OrElse FileSize < 1024 * 1024 'Files smaller than 1MB are not split
            End Get
        End Property
        ''' <summary>
        ''' Temporary in-memory storage for small files that do not need splitting.
        ''' </summary>
        Private Cache As MemoryStream

        ''' <summary>
        ''' Downloaded size of the file.
        ''' </summary>
        Public DownloadDone As Long = 0
        ''' <summary>
        ''' Validation rule of the file.
        ''' </summary>
        Public Check As FileChecker
        ''' <summary>
        ''' Whether to mimic a browser's UserAgent and Referer.
        ''' </summary>
        Public SimulateBrowserHeaders As Boolean

        ''' <summary>
        ''' Time of the last speed measurement.
        ''' </summary>
        Private SpeedLastTime As Long = GetTimeMs()
        ''' <summary>
        ''' Downloaded size at the last speed measurement.
        ''' </summary>
        Private SpeedLastDone As Long = 0
        ''' <summary>
        ''' Current download speed in bytes per second.
        ''' </summary>
        Public ReadOnly Property Speed As Long
            Get
                If GetTimeMs() - SpeedLastTime > 200 Then
                    Dim DeltaTime As Long = GetTimeMs() - SpeedLastTime
                    _Speed = (DownloadDone - SpeedLastDone) / (DeltaTime / 1000)
                    SpeedLastDone = DownloadDone
                    SpeedLastTime += DeltaTime
                End If
                Return _Speed
            End Get
        End Property
        Private _Speed As Long = 0

        ''' <summary>
        ''' Whether this file was completed by copying a local file directly.
        ''' </summary>
        Public IsCopy As Boolean = False
        ''' <summary>
        ''' Display progress of this file.
        ''' </summary>
        Public ReadOnly Property Progress As Double
            Get
                Select Case State
                    Case NetState.WaitingToCheck
                        Return 0
                    Case NetState.WaitingToDownload
                        Return 0.01
                    Case NetState.Connecting
                        Return 0.02
                    Case NetState.Reading
                        Return 0.04
                    Case NetState.Downloading
                        'Downloading, maps to 5% ~ 98%
                        Dim OriginalProgress As Double = If(IsUnknownSize, 0.5, DownloadDone / Math.Max(FileSize, 1))
                        OriginalProgress = 1 - (1 - OriginalProgress) ^ 0.9
                        Return OriginalProgress * 0.93 + 0.05
                    Case NetState.Merging
                        Return 0.99
                    Case NetState.Finished, NetState.Canceled
                        Return 1
                    Case Else
                        Throw New ArgumentOutOfRangeException("文件状态未知：" & State)
                End Select
            End Get
        End Property

        ''' <summary>
        ''' Total number of successful connections across threads.
        ''' </summary>
        Private ConnectCount As Integer = 0
        ''' <summary>
        ''' Total time of successful connections across threads.
        ''' </summary>
        Private ConnectTime As Long = 0
        ''' <summary>
        ''' Average time of successful connections across threads in ms; -1 means no successful connection yet.
        ''' </summary>
        Private ReadOnly Property ConnectAverage As Integer
            Get
                Dim CurrentConnectCount As Integer = ConnectCount 'Prevent race conditions
                Return If(CurrentConnectCount = 0, -1, ConnectTime / CurrentConnectCount)
            End Get
        End Property

        Private Const FilePieceLimit As Long = 256 * 1024
        Public ReadOnly LockState As New Object
        Public ReadOnly LockChain As New Object
        Public ReadOnly LockSource As New Object

        Public ReadOnly Uuid As Integer = GetUuid()
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim file = TryCast(obj, NetFile)
            Return file IsNot Nothing AndAlso Uuid = file.Uuid
        End Function

#End Region

        ''' <summary>
        ''' Creates a new file to download.
        ''' </summary>
        ''' <param name="LocalPath">Local path including the file name.</param>
        Public Sub New(Urls As IEnumerable(Of String), LocalPath As String, Optional Checker As FileChecker = Nothing, Optional SimulateBrowserHeaders As Boolean = False)
            Dim Sources As New List(Of NetSource)
            Dim Count As Integer = 0
            Urls = Urls.Distinct.ToArray
            For Each Source As String In Urls
                Sources.Add(New NetSource With {.FailCount = 0, .Url = SecretCdnSign(Source.ReplaceLineEndings("").Trim), .Id = Count, .IsFailed = False, .Ex = Nothing})
                Count += 1
            Next
            Me.Sources = Sources
            Me.LocalPath = LocalPath
            Me.Check = Checker
            Me.SimulateBrowserHeaders = SimulateBrowserHeaders
            Me.LocalName = PathUtils.GetLastPart(LocalPath)
        End Sub

        ''' <summary>
        ''' Tries to start a new download thread.
        ''' Returns Nothing on failure.
        ''' </summary>
        Public Function TryBeginThread() As NetThread
            Try

                Dim StartPosition As Long, StartSource As NetSource = Nothing
                Dim Th As Thread, ThreadInfo As NetThread
                'Condition checks
                SyncLock LockSource
                    If NetTaskThreadCount >= NetTaskThreadLimit OrElse Not HasAvailableSource() OrElse
                        (IsNoSplit AndAlso Threads IsNot Nothing AndAlso Threads.State <> NetState.Canceled) Then Return Nothing
                    If State >= NetState.Merging OrElse State = NetState.WaitingToCheck Then Return Nothing
                    SyncLock LockState
                        If State < NetState.Connecting Then State = NetState.Connecting
                    End SyncLock
                    'Initialize parameters
                    'Get the thread start point and download source
                    'No splitting
                    If IsNoSplit Then GoTo StartSingleThreadDownload
                    'Only single-thread capable sources remain
                    If Not HasAvailableSource(False) Then
                        If SourcesOnce.First.SingleThread IsNot Nothing AndAlso SourcesOnce.First.SingleThread.State <> NetState.Canceled Then Return Nothing
StartSingleThreadDownload:
                        Reset()
                        State = NetState.Reading
                    End If
                End SyncLock
                SyncLock LockChain
                    'First start point
                    If Threads Is Nothing Then
                        StartPosition = 0
                        StartSource = GetSource(FirstThreadSource)
                        FirstThreadSource = StartSource.Id + 1
                        GoTo StartThread
                    End If
                    'Find a failed point
                    For Each Thread As NetThread In Threads
                        If Thread.State = NetState.Canceled AndAlso Thread.DownloadUndone > 0 Then
                            StartPosition = Thread.DownloadStart + Thread.DownloadDone
                            StartSource = GetSource(Thread.Source.Id + 1)
                            GoTo StartThread
                        End If
                    Next
                    'Whether multi-threading is disabled, and the chunk size
                    Dim Source As NetSource = GetSource()
                    If Source Is Nothing Then Return Nothing
                    Dim TargetUrl As String = Source.Url
                    If TargetUrl.Contains("bmclapi") OrElse TargetUrl.Contains("github.com") OrElse
                       TargetUrl.Contains("optifine.net") OrElse TargetUrl.Contains("momot.rs") Then Return Nothing
                    'Find the largest chunk
                    'FUTURE: redo the download engine; compute the average connection time of sources and thread speeds, and start threads to save the most time
                    Dim FilePieceMax As NetThread = Threads
                    For Each Thread As NetThread In Threads
                        If Thread.DownloadUndone > FilePieceMax.DownloadUndone Then FilePieceMax = Thread
                    Next
                    If FilePieceMax Is Nothing OrElse FilePieceMax.DownloadUndone < FilePieceLimit Then Return Nothing
                    StartPosition = FilePieceMax.DownloadEnd - FilePieceMax.DownloadUndone * 0.4
                    StartSource = GetSource()

                    'Start the thread
StartThread:
                    If (StartPosition > FileSize AndAlso FileSize >= 0 AndAlso Not IsUnknownSize) OrElse StartPosition < 0 OrElse IsNothing(StartSource) Then Return Nothing
                    'Build the thread
                    Dim ThreadUuid As Integer = GetUuid()
                    If Not Tasks.Any() Then Return Nothing 'No task is available anymore due to an abort
                    Th = New Thread(AddressOf Thread) With {.Name = $"下载 {Tasks(0).Uuid}/{Uuid}/{ThreadUuid}#", .Priority = ThreadPriority.BelowNormal}
                    ThreadInfo = New NetThread With {.Uuid = ThreadUuid, .DownloadStart = StartPosition, .Thread = Th, .Source = StartSource, .Task = Me, .State = NetState.WaitingToDownload}
                    'Linked list handling
                    If ThreadInfo.IsFirstThread OrElse Threads Is Nothing Then
                        Threads = ThreadInfo
                    Else
                        Dim CurrentChain As NetThread = Threads
                        While CurrentChain.DownloadEnd <= StartPosition
                            CurrentChain = CurrentChain.NextThread
                        End While
                        ThreadInfo.NextThread = CurrentChain.NextThread
                        CurrentChain.NextThread = ThreadInfo
                    End If

                End SyncLock
                'Start the thread
                Interlocked.Increment(NetTaskThreadCount)
                SyncLock LockSource
                    If Not HasAvailableSource(False) Then SourcesOnce.First.SingleThread = ThreadInfo
                End SyncLock
                Th.Start(ThreadInfo)
                Return ThreadInfo

            Catch ex As Exception
                Fail(New Exception($"尝试开始下载线程失败（{LocalName}）", ex))
                Return Nothing
            End Try
        End Function
        ''' <summary>
        ''' Code run by each download thread.
        ''' </summary>
        Private Sub Thread(Th As NetThread)
            Logger.Log($"{LocalName}：开始，起始点 {Th.DownloadStart}，{Th.Source.Url}", If(Th.DownloadStart = 0, LogLevel.Info, LogLevel.Trace))
            Dim ResultStream As Stream = Nothing, HttpRequest As HttpRequestMessage = Nothing,
                Response As HttpResponseMessage = Nothing, ResponseStream As Stream = Nothing,
                CancelToken As CancellationTokenSource = Nothing
            'Some sources are really slow and only need one request (e.g. a 20s ping); if the timeout grows too slowly, it causes huge delays like 2.5s 5s 7.5s 10s 12.5s...
            'Too long a delay keeps some extremely slow connections from being killed
            Dim Timeout As Integer = Math.Min(Math.Max(ConnectAverage, 15000) * (1 + Th.Source.FailCount), 30000)
            Th.State = NetState.Connecting
            Try
                Dim HttpDataCount As Integer = 0
                If SourcesOnce.Contains(Th.Source) AndAlso Th <> Th.Source.SingleThread Then GoTo SourceBreak
                HttpRequest = New HttpRequestMessage(HttpMethod.Get, Th.Source.Url)
                SecretHeadersSign(Th.Source.Url, HttpRequest, SimulateBrowserHeaders)
                CancelToken = New CancellationTokenSource(Timeout)
                If Not Th.IsFirstThread Then HttpRequest.Headers.Range = New Headers.RangeHeaderValue(Th.DownloadStart, Nothing)
                Dim ContentLength As Long = 0
                Response = If(CurseForgeAccess.IsAuthenticatedHost(HttpRequest.RequestUri),
                    CurseForgeAccess.SendAsync(AuthenticatedResourceClient, HttpRequest, CancelToken.Token),
                    ThreadClient.SendAsync(HttpRequest, HttpCompletionOption.ResponseHeadersRead, CancelToken.Token)).GetResultWithTimeout(CancelToken, Timeout)
                If Not Response.IsSuccessStatusCode Then 'Status code check
                    Throw New HttpRequestCodeException($"错误码 {Response.StatusCode} ({CInt(Response.StatusCode)})，{Th.Source.Url}", Response.StatusCode, Nothing)
                End If
                If State = NetState.Canceled Then GoTo SourceBreak 'Fast abort
                If Response.RequestMessage.RequestUri.ToString <> Th.Source.Url Then Logger.Trace(Function() $"{LocalName}：重定向至 {Response.RequestMessage.RequestUri}")
                'File size check
                ContentLength = Response.Content.Headers.ContentLength.GetValueOrDefault(-1)
                If ContentLength < 0 Then
                    If FileSize > 1 Then
                        If Th.DownloadStart = 0 Then
                            Logger.Info($"{LocalName}：文件大小未知，但已从其他下载源获取，不作处理")
                        Else
                            Logger.Info($"{LocalName}：ContentLength 返回了 {ContentLength}，无法确定是否支持分段下载，视作不支持")
                            GoTo NotSupportRange
                        End If
                    Else
                        FileSize = -1 : IsUnknownSize = True
                        Logger.Info($"{LocalName}：文件大小未知")
                    End If
                ElseIf Th.IsFirstThread Then
                    If Check IsNot Nothing Then
                        If ContentLength < Check.MinSize AndAlso Check.MinSize > 0 Then
                            Throw New Exception($"文件大小不足，获取结果为 {ContentLength}，要求至少为 {Check.MinSize}。")
                        End If
                        If ContentLength <> Check.ActualSize AndAlso Check.ActualSize > 0 Then
                            Throw New Exception($"文件大小不一致，获取结果为 {ContentLength}，要求必须为 {Check.ActualSize}。")
                        End If
                    End If
                    FileSize = ContentLength : IsUnknownSize = False
                    Logger.Info($"{LocalName}：文件大小 {ContentLength}（{StringUtils.FormatByteSize(ContentLength)}）")
                    'If the file is larger than 50 M, check the remaining disk space
                    If ContentLength > 50 * 1024 * 1024 Then
                        For Each Drive As DriveInfo In DriveInfo.GetDrives
                            Dim DriveName As String = Drive.Name.First.ToString
                            Dim RequiredSpace = If(PathTemp.StartsWithF(DriveName), ContentLength * 1.1, 0) +
                                                If(LocalPath.StartsWithF(DriveName), ContentLength + 5 * 1024 * 1024, 0)
                            If RequiredSpace > 0 AndAlso Drive.TotalFreeSpace < RequiredSpace Then
                                Throw New Exception(DriveName & " 盘空间不足，无法进行下载。" & vbCrLf & "需要至少 " & StringUtils.FormatByteSize(RequiredSpace) & " 空间，但当前仅剩余 " & StringUtils.FormatByteSize(Drive.TotalFreeSpace) & "。" &
                                                    If(PathTemp.StartsWithF(DriveName), vbCrLf & vbCrLf & "下载时需要与文件同等大小的空间存放缓存，你可以在设置中调整缓存文件夹的位置。", ""))
                            End If
                        Next
                    End If
                ElseIf FileSize < 0 Then
                    Throw New Exception("非首线程运行时，尚未获取文件大小")
                ElseIf Th.DownloadStart > 0 AndAlso ContentLength >= FileSize Then
NotSupportRange:
                    SyncLock LockSource
                        If SourcesOnce.Contains(Th.Source) Then GoTo SourceBreak
                    End SyncLock
                    Throw New RangeNotSupportedException($"该下载源不支持分段下载：Range 起始于 {Th.DownloadStart}，预期 ContentLength 为 {FileSize - Th.DownloadStart}，返回 ContentLength 为 {ContentLength}，总文件大小 {FileSize}")
                ElseIf Not FileSize - Th.DownloadStart = ContentLength Then
                    Throw New RangeNotSupportedException($"获取到的分段大小不一致：Range 起始于 {Th.DownloadStart}，预期 ContentLength 为 {FileSize - Th.DownloadStart}，返回 ContentLength 为 {ContentLength}，总文件大小 {FileSize}")
                End If
                Th.State = NetState.Reading
                SyncLock LockState
                    If State < NetState.Reading Then State = NetState.Reading
                End SyncLock
                'Create the cache file
                If IsNoSplit Then
                    Th.Temp = Nothing
                    Cache = New MemoryStream
                    ResultStream = Cache
                Else
                    Th.Temp = $"{PathTemp}Download\{Uuid}_{Th.Uuid}_{RandomInteger(0, 999999)}.tmp"
                    ResultStream = FileUtils.CreateAsStream(Th.Temp)
                End If
                'Start downloading
                CancelToken.Dispose() : CancelToken = New CancellationTokenSource() 'Reset the timeout so the CancellationToken no longer fires after a fixed time (#8488)
                ResponseStream = Response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
                Dim ResponseBytes As Byte() = New Byte(16384) {}
                HttpDataCount = ResponseStream.ReadAsync(ResponseBytes, 0, 16384, CancelToken.Token).GetResultWithTimeout(CancelToken, Timeout)
                While (IsUnknownSize OrElse Th.DownloadUndone > 0) AndAlso 'Check whether the download is complete
                            HttpDataCount > 0 AndAlso Not IsProgramEnding AndAlso State < NetState.Merging AndAlso (Not Th.Source.IsFailed OrElse Th.Source.SingleThread = Th)
                    'Speed limit
                    While NetTaskSpeedLimitHigh > 0 AndAlso NetTaskSpeedLimitLeft <= 0
                        Threading.Thread.Sleep(16)
                    End While
                    Dim RealDataCount As Integer = If(IsUnknownSize, HttpDataCount, Math.Min(HttpDataCount, Th.DownloadUndone))
                    If NetTaskSpeedLimitHigh > 0 Then Interlocked.Add(NetTaskSpeedLimitLeft, -RealDataCount)
                    Dim DeltaTime = GetTimeMs() - Th.LastReceiveTime
                    If DeltaTime > 100000000 Then DeltaTime = 1 'Avoid huge values caused by the tick going backwards
                    If RealDataCount > 0 Then
                        'Data received
                        If Th.DownloadDone = 0 Then
                            'First data received
                            Th.State = NetState.Downloading
                            SyncLock LockState
                                If State < NetState.Downloading Then State = NetState.Downloading
                            End SyncLock
                            Interlocked.Increment(ConnectCount)
                            Interlocked.Add(ConnectTime, GetTimeMs() - Th.InitTime)
                        End If
                        Th.Source.FailCount = 0
                        For Each Task In Tasks
                            Task.FailCount = 0
                        Next
                        Interlocked.Add(NetManager.DownloadDone, RealDataCount)
                        Interlocked.Add(DownloadDone, RealDataCount)
                        Th.DownloadDone += RealDataCount
                        ResultStream.Write(ResponseBytes, 0, RealDataCount)
                        'Finished
                        If Th.DownloadUndone = 0 AndAlso Not IsUnknownSize Then Exit While
                        'Check whether the speed is too slow
                        If Th.LastReceiveTime > 0 AndAlso DeltaTime > 5000 AndAlso DeltaTime > RealDataCount AndAlso 'Packet interval over 5s and speed below 1K/s
                                    Th.Source.SingleThread Is Nothing Then 'And not a single-thread download
                            Throw New TimeoutException("由于速度过慢断开链接，下载 " & RealDataCount & " B，消耗 " & DeltaTime & " ms。")
                        End If
                        Th.LastReceiveTime = GetTimeMs()
                    ElseIf Th.LastReceiveTime > 0 AndAlso DeltaTime > Timeout Then
                        'No data, and timed out
                        Throw New TimeoutException("操作超时，无数据。")
                    End If
                    HttpDataCount = ResponseStream.ReadAsync(ResponseBytes, 0, 16384, CancelToken.Token).GetResultWithTimeout(CancelToken, Timeout)
                End While
SourceBreak:
                Dim IsInterrupted As Boolean = State = NetState.Canceled OrElse (Th.Source.IsFailed AndAlso Th.Source.SingleThread <> Th)
                If Not IsInterrupted AndAlso ResponseStream IsNot Nothing AndAlso HttpDataCount = 0 AndAlso Th.DownloadUndone > 0 AndAlso
                   Not IsUnknownSize AndAlso Not IsProgramEnding AndAlso State < NetState.Merging Then
                    'The server disconnected early: must count as a source failure, or it will retry forever
                    Throw New Exception($"返回的 ContentLength 过多：ContentLength 为 {ContentLength}，但获取到的总数据量仅为 {Th.DownloadDone}（全文件总数据量 {DownloadDone}）")
                ElseIf IsInterrupted OrElse (Th.DownloadUndone > 0 AndAlso Not IsUnknownSize) Then
                    'Aborted externally
                    Th.State = NetState.Canceled
                    Logger.Info($"{LocalName}：中断")
                Else
                    'This thread is done
                    Th.State = NetState.Finished
                    Logger.Trace(Function() $"{LocalName}：完成，已下载 {Th.DownloadDone}（{Th.DownloadStart}~{Th.DownloadEnd}）")
                End If
            Catch ex As Exception
                If ex.IsBadNetwork OrElse TypeOf ex Is TaskCanceledException Then
                    Logger.Info(New TimeoutException($"{LocalName}：超时（{Timeout}ms），{ex.Message}"))
                Else
                    Logger.Info(ex, $"{LocalName}：出错")
                End If
                SourceFail(Th, ex, False)
            Finally
                'Release resources
                HttpRequest?.Dispose()
                Response?.Dispose()
                ResponseStream?.Dispose()
                CancelToken?.Dispose()
                If ResultStream IsNot Cache Then ResultStream?.Dispose()
                'Update counters
                Interlocked.Decrement(NetTaskThreadCount)
                'Merge
                If ((FileSize >= 0 AndAlso DownloadDone >= FileSize) OrElse (FileSize = -1 AndAlso DownloadDone > 0)) AndAlso
                    State < NetState.Merging AndAlso Th.State <> NetState.Canceled Then Merge(Th)
            End Try
        End Sub
        Private Sub SourceFail(Th As NetThread, ex As Exception, IsMergeFailure As Boolean)
            'State change
            Interlocked.Increment(Th.Source.FailCount)
            For Each Task In Tasks
                Interlocked.Increment(Task.FailCount)
            Next
            Th.State = NetState.Canceled
            Th.Source.Ex = ex
            'Decide whether to disable the source for multi-threading (too many consecutive errors, or resuming not supported)
            Dim IsRangeNotSupported As Boolean = TypeOf ex Is RangeNotSupportedException OrElse ex.Message.Contains("(416)")
            If IsMergeFailure OrElse IsRangeNotSupported OrElse
               ex.Message.Contains("(502)") OrElse ex.Message.Contains("(404)") OrElse
               ex.Message.Contains("未能解析") OrElse ex.Message.Contains("无返回数据") OrElse ex.Message.Contains("空间不足") OrElse
               ((ex.Message.Contains("(403)") OrElse ex.Message.Contains("(429)")) AndAlso Not Th.Source.Url.Contains("bmclapi")) OrElse 'Some BMCLAPI sources return 403/429 under high request rates, so the source should not be disabled for that
               (Th.Source.FailCount >= NetTaskThreadLimit.Clamp(5, 30) AndAlso DownloadDone < 1) OrElse Th.Source.FailCount > NetTaskThreadLimit + 2 Then
                'When multiple threads are downloading from one source, only pick one of them for follow-up handling
                Dim IsThisFail As Boolean = False
                SyncLock LockSource
                    If Not Th.Source.IsFailed OrElse Th.Source.SingleThread = Th Then
                        IsThisFail = True
                        Th.Source.IsFailed = True
                    End If
                End SyncLock
                '...follow-up handling
                If IsThisFail Then
                    Logger.Info($"{LocalName}：下载源被禁用（{Th.Source.Id}，Range 问题：{IsRangeNotSupported}）：{Th.Source.Url}")
                    Logger.Warn(ex, $"{If(SourcesOnce.FirstOrDefault?.SingleThread Is Nothing, "", "单线程")}下载源 {Th.Source.Id} 已被禁用")
                    SyncLock LockSource
                        SourcesOnce.Remove(Th.Source)
                    End SyncLock
                    If ex.Message.Contains("空间不足") Then
                        'Out of disk space: force failure
                        Fail(ex)
                    ElseIf HasAvailableSource() AndAlso Not IsMergeFailure Then
                        'The current source failed but other sources remain: continue normally
                    ElseIf Not Retried Then
                        'Merge failed or first download failed, not retried yet: re-mark all sources as not allowing resuming and retry them one by one
                        'Also retries here if no source supports Range
                        If Not IsRangeNotSupported Then Logger.Warn($"{LocalName}：文件下载失败，正在自动重试……")
                        Retried = True
                        SyncLock LockSource
                            SourcesOnce.Clear()
                            For Each Source In Sources
                                SourcesOnce.Add(Source)
                                Source.IsFailed = True
                            Next
                        End SyncLock
                        Reset()
                        SyncLock LockState
                            State = NetState.WaitingToDownload
                        End SyncLock
                    ElseIf HasAvailableSource() AndAlso IsMergeFailure Then
                        'Merge failed and a single source failed: continue with the next source
                        Reset()
                        SyncLock LockState
                            State = NetState.WaitingToDownload
                        End SyncLock
                    Else
                        'Failed
                        Logger.Info($"{LocalName}：已无可用下载源，下载失败")
                        Dim ExampleEx As Exception = Nothing
                        SyncLock LockSource
                            For Each Source As NetSource In Sources
                                Logger.Info($"已禁用的下载源：{Source.Url}")
                                If Source.Ex IsNot Nothing Then
                                    ExampleEx = Source.Ex
                                    Logger.Warn(Source.Ex, "下载源禁用原因")
                                End If
                            Next
                        End SyncLock
                        Fail(ExampleEx)
                    End If
                End If
            End If
            'Clean up the content downloaded so far
            If FileSize = -2 Then Reset()
        End Sub
        Private Sub Reset()
            FileSize = -2
            Cache?.Dispose() : Cache = Nothing
            SyncLock LockChain
                Threads = Nothing
            End SyncLock
            Interlocked.Add(NetManager.DownloadDone, -DownloadDone)
            DownloadDone = 0
            SpeedLastDone = 0
        End Sub

        'Final wrap-up event

        ''' <summary>
        ''' Download finished. Merges the files.
        ''' </summary>
        Private Sub Merge(Th As NetThread)
            'State check
            SyncLock LockState
                If State < NetState.Merging Then
                    State = NetState.Merging
                Else
                    Return
                End If
            End SyncLock
            Dim RetryCount As Integer = 0
            Try
Retry:
                'Create the folder
                DirectoryUtils.Create(PathUtils.RemoveLastPart(LocalPath))
                FileUtils.Delete(LocalPath)
                SyncLock LockChain
                    'Merge the files
                    If IsNoSplit Then
                        'Only one thread, write out from memory
                        Logger.Trace(Function() $"{LocalName}：下载结束，从内存输出文件")
                        FileUtils.Write(LocalPath, Cache)
                    ElseIf Threads.DownloadDone = DownloadDone AndAlso Threads.Temp IsNot Nothing Then
                        'Only one file, copy directly
                        Logger.Trace(Function() $"{LocalName}：下载结束，仅有一个文件，无需合并")
                        FileUtils.Copy(Threads.Temp, LocalPath)
                    Else
                        'Multiple threads, merge
                        Logger.Trace(Function() $"{LocalName}：下载结束，开始合并文件")
                        Using MergeFile = FileUtils.CreateAsStream(LocalPath)
                            Dim Buffer(81919) As Byte
                            For Each Thread As NetThread In Threads
                                If Thread.DownloadDone = 0 OrElse Thread.Temp Is Nothing Then Continue For
                                Using fs = FileUtils.ReadAsStream(Thread.Temp)
                                    'Copy in chunks to avoid overflow when a single segment exceeds 2G
                                    Dim Remain As Long = Thread.DownloadDone
                                    Do While Remain > 0
                                        Dim Count As Integer = fs.Read(Buffer, 0, CInt(Math.Min(Buffer.Length, Remain)))
                                        If Count <= 0 Then Throw New EndOfStreamException($"缓存文件长度不足：{Thread.Temp}")
                                        MergeFile.Write(Buffer, 0, Count)
                                        Remain -= Count
                                    Loop
                                End Using
                            Next
                        End Using
                    End If
                    'Check the file
                    Dim CheckResult As String = Check?.Check(LocalPath)
                    If CheckResult Is Nothing AndAlso Not IsUnknownSize AndAlso Check IsNot Nothing Then
                        If Check.ActualSize = -1 Then
                            CheckResult = (New FileChecker With {.ActualSize = FileSize}).Check(LocalPath) 'Do not modify the original Checker, to avoid polluting the original instance
                        ElseIf Check.ActualSize <> FileSize Then
                            CheckResult = $"文件大小不一致：任务校验要求为 {Check.ActualSize}，请求结果为 {FileSize}"
                        End If
                    End If
                    If CheckResult IsNot Nothing Then
                        Logger.Info($"{LocalName} 文件校验失败，下载线程细节：")
                        For Each T As NetThread In Threads
                            Logger.Info($"- {T.Uuid}#，状态 {T.State}，范围 {T.DownloadStart}~{T.DownloadEnd}，已下载 {T.DownloadDone}，未下载 {T.DownloadUndone}")
                        Next
                        Throw New Exception(CheckResult)
                    End If
                    'Post-processing
                    If Not IsNoSplit Then
                        For Each Thread As NetThread In Threads
                            If Thread.Temp IsNot Nothing Then FileUtils.Delete(Thread.Temp)
                        Next
                    End If
                    Finish()
                End SyncLock
            Catch ex As Exception
                RetryCount += 1
                If State > NetState.Merging Then Return 'Usually because it was aborted during merging
                Logger.Warn(ex, $"合并文件出错，第 {RetryCount} 次尝试（{LocalName}）")
                'Retry merging
                If RetryCount < 3 Then
                    Threading.Thread.Sleep(500 * RetryCount)
                    GoTo Retry
                End If
                'Failed: disable the current source and restart the download
                Try
                    FileUtils.Delete(LocalPath)
                Catch exx As Exception
                    Logger.Warn(ex, $"删除合并失败的文件出错（{LocalPath}）")
                End Try
                SourceFail(Th, ex, True)
            Finally
                Cache?.Dispose()
            End Try
        End Sub
        ''' <summary>
        ''' Download failed.
        ''' </summary>
        Private Sub Fail(Optional RaiseEx As Exception = Nothing)
            SyncLock LockState
                If State >= NetState.Finished Then Return
                If RaiseEx IsNot Nothing Then Ex.Add(RaiseEx)
                'Doomed
                Logger.Info($"{LocalName}：已失败，当前状态 {State}")
                State = NetState.Canceled
            End SyncLock
            CancelInternal()
            For Each Task In Tasks
                Task.OnFileFail(Me)
            Next
        End Sub
        ''' <summary>
        ''' Cancels the download.
        ''' </summary>
        Public Sub Cancel(CanceledTask As LoaderDownload)
            'Remove it from the given task; if it still belongs to other tasks, keep downloading
            Tasks.Remove(CanceledTask)
            If Tasks.Any Then Return
            'Confirm the abort
            SyncLock LockState
                If State >= NetState.Finished Then Return
                Logger.Info($"{LocalName}：已中断，当前状态 {State}")
                State = NetState.Canceled
            End SyncLock
            CancelInternal()
        End Sub
        Private Sub CancelInternal()
            On Error Resume Next
            Reset()
            Interlocked.Decrement(NetManager.FileRemain)
        End Sub

        'State change interface
        ''' <summary>
        ''' Marks this file as downloaded.
        ''' </summary>
        Public Sub Finish(Optional PrintLog As Boolean = True)
            SyncLock LockState
                If State >= NetState.Finished Then Return
                State = NetState.Finished
            End SyncLock
            Interlocked.Decrement(NetManager.FileRemain)
            If PrintLog Then Logger.Info($"{LocalName}：已完成")
            For Each Task In Tasks
                Task.OnFileFinish(Me)
            Next
        End Sub

    End Class
    Private Class RangeNotSupportedException
        Inherits HttpRequestException
        Public Sub New(Message As String)
            MyBase.New(Message)
        End Sub
    End Class
    ''' <summary>
    ''' Loader that downloads a list of files.
    ''' </summary>
    Public Class LoaderDownload
        Inherits LoaderBase

#Region "Properties"

        ''' <summary>
        ''' Files to download.
        ''' </summary>
        Public Files As ConcurrentQueue(Of NetFile)
        ''' <summary>
        ''' Number of remaining unfinished files. (Used to reduce contention on FilesLock)
        ''' </summary>
        Private FileRemain As Integer
        Private ReadOnly FileRemainLock As New Object

        ''' <summary>
        ''' Percentage progress for display.
        ''' </summary>
        Public Overrides Property Progress As Double
            Get
                If State >= LoadState.Finished Then Return 1
                If Not Files.Any() Then Return 0 'Must return 0, otherwise it may look like the download already finished while fetching the list
                Return _Progress
            End Get
            Set(value As Double)
                Throw New Exception("文件下载不允许指定进度")
            End Set
        End Property
        Private _Progress As Double = 0

        ''' <summary>
        ''' Consecutive failure count of files in the task.
        ''' </summary>
        Public Property FailCount As Integer
            Get
                Return _FailCount
            End Get
            Set(value As Integer)
                _FailCount = value
                If State = LoadState.Loading AndAlso value >= Math.Min(10000, Math.Max(FileRemain * 8, NetTaskThreadLimit * 8 + 3)) Then
                    Logger.Error($"由于同加载器中失败次数过多引发强制失败：连续失败了 {value} 次", LogBehavior.ToastIfDebug)
                    On Error Resume Next
                    Dim ExList As New List(Of Exception)
                    For Each File In Files
                        For Each Source In File.Sources
                            If Source.Ex IsNot Nothing Then
                                ExList.Add(Source.Ex)
                                If ExList.Count > 10 Then GoTo FinishExCatch
                            End If
                        Next
                    Next
FinishExCatch:
                    OnFail(ExList)
                End If
            End Set
        End Property
        Private _FailCount As Integer = 0

#End Region

        ''' <summary>
        ''' Refreshes the public properties. Called by NetManager every 0.1 seconds.
        ''' </summary>
        Public Sub RefreshStat()
            'Compute the progress
            Dim NewProgress As Double = 0
            Dim TotalProgress As Double = 0
            For Each File In Files
                If File.IsCopy Then
                    NewProgress += File.Progress * 0.2
                    TotalProgress += 0.2
                Else
                    NewProgress += File.Progress
                    TotalProgress += 1
                End If
            Next
            If TotalProgress > 0 AndAlso Not Double.IsNaN(TotalProgress) Then NewProgress /= TotalProgress
            'Refresh the progress
            _Progress = NewProgress
        End Sub

        Public Sub New(Name As String, FileTasks As IEnumerable(Of NetFile))
            Me.Name = Name
            Files = New ConcurrentQueue(Of NetFile)(FileTasks)
        End Sub
        Public Overrides Sub Start(Optional Input As Object = Nothing, Optional IsForceRestart As Boolean = False)
            If Input IsNot Nothing Then Files = New ConcurrentQueue(Of NetFile)(CType(Input, IEnumerable(Of NetFile)))
            'Deduplicate
            Files = New ConcurrentQueue(Of NetFile)(Files.DistinctBy(Function(a) PathUtils.ForCompare(a.LocalPath).Lower()))
            'Set the remaining file count
            SyncLock FileRemainLock
                FileRemain += Files.Where(Function(f) f.State <> NetState.Finished).Count
            End SyncLock
            State = LoadState.Loading
            'Start execution
            RunInNewThread(
            Sub()
                Try
                    'Input checks
                    If Not Files.Any() Then
                        OnFinish()
                        Return
                    End If
                    For Each File As NetFile In Files
                        If File Is Nothing Then Throw New ArgumentException("存在空文件请求！")
                        For Each Source As NetSource In File.Sources
                            If Not (Source.Url.StartsWithF("https://", True) OrElse Source.Url.StartsWithF("http://", True)) Then
                                Source.Ex = New ArgumentException("输入的下载链接不正确：" & Source.Url)
                                Source.IsFailed = True
                            End If
                        Next
                        If Not File.HasAvailableSource() Then Throw New ArgumentException("输入的下载链接不正确！")
                        File.LocalPath = File.LocalPath.Replace("/", "\")
                        If Not File.LocalPath.Contains(":\") Then Throw New ArgumentException("输入的本地文件地址不正确：" & File.LocalPath)
                        If File.LocalPath.EndsWithF("\") Then Throw New ArgumentException("请输入含文件名的完整文件路径：" & File.LocalPath)
                        DirectoryUtils.Create(PathUtils.RemoveLastPart(File.LocalPath)) 'Create the target folder
                    Next
                    'Hook into the download manager and get the list of newly started files
                    Dim NewFiles = NetManager.Start(Me)


                    '====================================
                    ' Look up existing files
                    '====================================

                    'Collect the files allowed to be looked up
                    Dim FilesToCheck As New List(Of NetFile)
                    For Each File In NewFiles
                        If File.Check IsNot Nothing Then
                            FilesToCheck.Add(File)
                        Else 'No validation info, start downloading directly
                            SyncLock LockState
                                File.State = NetState.WaitingToDownload
                                File.IsCopy = False
                            End SyncLock
                        End If
                    Next
                    If Not FilesToCheck.Any Then Return
                    'Get the list of MC folders
                    Dim Folders As New List(Of String)
                    Folders.Add(Paths.AppData & ".minecraft\") 'Always add the official launcher folder, since HMCL stores all files there
                    Folders.AddRange(McFolderList.Select(Function(f) f.Location))
                    Folders = Folders.Distinct.Where(Function(f) DirectoryUtils.Exists(f)).ToList
                    'Distribute evenly across multiple checking threads
                    Dim ThreadCount As Integer = (FilesToCheck.Count \ 40).Clamp(1, 8) 'At least 40 files per thread, at most 8 threads
                    If ThreadCount = 1 Then 'Only one thread, run directly
                        CheckExistingFiles(FilesToCheck, Folders)
                    Else
                        Dim BaseSize = FilesToCheck.Count \ ThreadCount
                        Dim Remainder = FilesToCheck.Count Mod ThreadCount
                        Dim Index = 0
                        For i = 0 To ThreadCount - 1
                            Dim Size = BaseSize + If(i < Remainder, 1, 0)
                            Dim ThreadFiles = FilesToCheck.GetRange(Index, Size)
                            Index += Size
                            RunInNewThread(Sub() CheckExistingFiles(ThreadFiles, Folders), $"下载 文件复制 {Uuid}/{GetUuid()}")
                        Next
                    End If
                Catch ex As Exception
                    OnFail(New List(Of Exception) From {New Exception("下载初始化失败", ex)})
                End Try
            End Sub, "L/下载 " & Uuid)
        End Sub
        Private Sub CheckExistingFiles(Files As List(Of NetFile), FolderList As List(Of String))
            Try
                Logger.Trace(Function() $"文件检查开始，本线程负责 {Files.Count} 个文件，首个文件为 {Files.FirstOrDefault?.LocalName}")
                'List the version folders in each MC folder
                Dim VersionFolders As New List(Of String)
                For Each McFolder In FolderList
                    For Each VersionFolder In DirectoryUtils.EnumerateDirectories(McFolder & "versions\")
                        VersionFolders.Add(PathUtils.AddSlashSuffix(VersionFolder))
                    Next
                Next
                'Process each file
                For Each File As NetFile In Files
                    Dim Target As String = CheckExistingFile(FolderList, VersionFolders, File)
                    Logger.Trace(Function() $"本地文件匹配：{File.LocalName} → {If(Target, "无匹配")}，当前状态 {File.State}")
                    If File.State >= NetState.WaitingToDownload Then Continue For 'Aborted
                    'Found an identical file
                    If Target IsNot Nothing Then
                        File.IsCopy = True
                        Try
                            FileUtils.Copy(Target, File.LocalPath)
                            File.Finish(False)
                            Continue For
                        Catch
                        End Try
                    End If
                    'Fall back to downloading
                    SyncLock LockState
                        File.State = NetState.WaitingToDownload
                        File.IsCopy = False
                    End SyncLock
                Next
            Catch ex As Exception
                OnFail(New List(Of Exception) From {New Exception("下载已存在文件查找失败", ex)})
            End Try
        End Sub
        Private Function CheckExistingFile(FolderList As List(Of String), VersionFolders As List(Of String), File As NetFile) As String
            'The target file already exists
            If File.Check.Check(File.LocalPath) Is Nothing Then Return File.LocalPath
            'Copying is disabled in the settings
            'No usable check rule, can only start downloading
            If File.Check.Hash Is Nothing AndAlso File.Check.ActualSize < 0 Then Return Nothing
            'Roughly determine the file category
            Dim TypeIndexes =
                {"\assets\", "\libraries\", "\versions\", "\mods\", "\coremods\", "\lib\", "\resourcepacks\", "\texturepacks\", "\shaderpacks\"}.
                Select(Function(FolderName) (FolderName, File.LocalPath.IndexOfF(FolderName, True))).
                Where(Function(kv) kv.Item2 >= 0).ToList
            If Not TypeIndexes.Any Then
                If PathUtils.GetExtension(File.LocalName) = "jar" Then
                    TypeIndexes.Add(("\versions\", 1)) 'Always check jars as version files, to include jars saved via Save As
                Else
                    Return Nothing
                End If
            End If
            Dim Type = TypeIndexes.MaxBy(Function(kv) kv.Item2).FolderName.TrimStart("\"c)
            'Look up by category
            Static Sizes As New ConcurrentDictionary(Of String, Long)
            Static CheckCandidate As Func(Of String, FileChecker, Boolean) =
            Function(Candidate, Checker)
                'Quick size check
                Dim Length = Sizes.GetOrAdd(Candidate, Function(c) If(FileUtils.Exists(c), FileUtils.GetInfo(c).Length, -1))
                If Checker.ActualSize <> Length Then Return False
                'Then a hash check
                Return Checker.Check(Candidate) Is Nothing
            End Function
            Select Case Type
                Case "assets\", "libraries\"
                    'assets/libraries: look up the same path in the MC folders
                    For Each Folder In FolderList
                        Dim Candidate = Folder & Type & File.LocalPath.AfterFirst(Type)
                        If CheckCandidate(Candidate, File.Check) Then Return Candidate
                    Next
                    Return Nothing
                Case "versions\"
                    'Version jar or json: look up each version folder in the MC folders
                    For Each VersionFolder In VersionFolders
                        For Each Candidate In DirectoryUtils.EnumerateFiles(VersionFolder, searchPattern:="*." & PathUtils.GetExtension(File.LocalPath))
                            If CheckCandidate(Candidate, File.Check) Then Return Candidate
                        Next
                    Next
                Case Else
                    'Community resources
                    If File.Check.ActualSize < 0 OrElse File.Check.Hash Is Nothing Then Return Nothing 'Both the file size and the hash must be specified
                    For Each Folder In FolderList.Concat(VersionFolders)
                        For Each Candidate In DirectoryUtils.EnumerateFiles(Folder & Type)
                            If CheckCandidate(Candidate, File.Check) Then Return Candidate
                        Next
                    Next
            End Select
            Return Nothing
        End Function

        Public Sub OnFileFinish(File As NetFile)
            'Require all files to finish
            SyncLock FileRemainLock
                FileRemain -= 1
                If FileRemain > 0 Then Return
            End SyncLock
            OnFinish()
        End Sub
        Public Sub OnFinish()
            RaisePreviewFinish()
            SyncLock LockState
                If State > LoadState.Loading Then Return
                State = LoadState.Finished
            End SyncLock
        End Sub
        Public Sub OnFileFail(File As NetFile)
            'Add the source errors to the main error list
            For Each Source In File.Sources
                If Not IsNothing(Source.Ex) Then File.Ex.Add(Source.Ex)
            Next
            OnFail(File.Ex)
        End Sub
        Public Overrides Sub Failed(Ex As Exception)
            OnFail(New List(Of Exception) From {Ex})
        End Sub
        Public Sub OnFail(ExList As List(Of Exception))
            SyncLock LockState
                If State >= LoadState.Finished Then Return
                If ExList Is Nothing OrElse Not ExList.Any() Then ExList = New List(Of Exception) From {New Exception("未知错误！")}
                'Find a meaningful error message
                Dim UsefulExs = ExList.Where(Function(e) Not e.IsCanceled AndAlso TypeOf e IsNot TimeoutException).ToList
                [Error] = If(UsefulExs.FirstOrDefault, ExList.FirstOrDefault)
                'Get the files that actually failed
                For Each File In Files
                    If File.State <> NetState.Canceled Then Continue For
                    If File.Sources.All(Function(s) s.Ex.IsCanceled OrElse TypeOf s.Ex Is TimeoutException) Then Continue For
                    Dim Detail As String = File.Sources.Select(Function(s) $"{If(s.Ex Is Nothing, "无错误信息。", s.Ex.GetDisplay(False))}（{s.Url}）").Join(vbCrLf)
                    [Error] = New Exception("文件下载失败：" & File.LocalPath & vbCrLf &
                                            "各下载源的错误如下：" & vbCrLf & Detail, [Error])
                    Exit For
                Next
                'Change to failed only after setting the Error object, so WaitForExit can catch the error
                State = LoadState.Failed
            End SyncLock
            'Abort all files
            For Each TaskFile In Files
                TaskFile.Cancel(Me)
            Next
            'Write logs only after leaving the sync lock
            Logger.Error(New AggregateException(ExList), "下载加载器失败", LogBehavior.None)
        End Sub
        Public Overrides Sub Cancel()
            SyncLock LockState
                If State >= LoadState.Finished Then Return
                State = LoadState.Canceled
            End SyncLock
            Logger.Info($"{Name} 已取消！")
            'Abort all files
            For Each TaskFile In Files
                TaskFile.Cancel(Me)
            Next
        End Sub

    End Class

    Public NetManager As New NetManagerClass
    ''' <summary>
    ''' Download file management.
    ''' </summary>
    Public Class NetManagerClass

#Region "Properties"

        ''' <summary>
        ''' Files to download, as "local path - file object" key-value pairs.
        ''' </summary>
        Public AllFiles As New ConcurrentDictionary(Of String, NetFile)

        ''' <summary>
        ''' All current download tasks.
        ''' </summary>
        Public Tasks As New ConcurrentBag(Of LoaderDownload)

        ''' <summary>
        ''' Size downloaded so far.
        ''' </summary>
        Public DownloadDone As Long = 0

        ''' <summary>
        ''' Number of files not yet downloaded.
        ''' </summary>
        Public FileRemain As Integer = 0

        'These properties are refreshed by RefreshStat
        ''' <summary>
        ''' Current global download speed in bytes per second.
        ''' </summary>
        Public Speed As Long = 0

        Public ReadOnly Uuid As Integer = GetUuid()

#End Region

        ''' <summary>
        ''' Progress and speed are refreshed by the download manager thread about every 0.1 seconds.
        ''' </summary>
        Private Sub RefreshStat()
            Try
                Dim DeltaTime As Long = GetTimeMs() - RefreshStatLast
                If DeltaTime = 0 Then Return
                RefreshStatLast += DeltaTime
#Region "Refresh Overall Speed"
                'Compute the instantaneous speed
                Static SpeedLast As New List(Of Long) 'Keeps up to the last 30 speed records, newest first
                Static SpeedLastDone As Long = 0 'Downloaded size at the last speed measurement
                Dim ActualSpeed As Double = Math.Max(0, (DownloadDone - SpeedLastDone) / (DeltaTime / 1000))
                SpeedLast.Insert(0, ActualSpeed)
                If SpeedLast.Count >= 31 Then SpeedLast.RemoveAt(30)
                SpeedLastDone = DownloadDone
                'Compute the speed for display
                Dim SpeedSum As Long = 0, SpeedDiv As Long = 0, Weight = SpeedLast.Count
                For Each SpeedRecord In SpeedLast
                    SpeedSum += SpeedRecord * Weight
                    SpeedDiv += Weight
                    Weight -= 1
                Next
                Speed = If(SpeedDiv > 0, SpeedSum / SpeedDiv, 0)
                'Compute the new speed lower bound
                Dim Limit As Long = 0
                If SpeedLast.Count >= 10 Then Limit = SpeedLast.Take(10).Average * 0.85 '85% of the average speed over the last second
                If Limit > NetTaskSpeedLimitLow Then
                    NetTaskSpeedLimitLow = Limit
                    Logger.Info($"速度下限已提升到 {StringUtils.FormatByteSize(Limit)}")
                End If
#End Region
#Region "Refresh Download Task Properties"
                For Each Task In Tasks
                    Task.RefreshStat()
                Next
#End Region
            Catch ex As Exception
                Logger.Warn(ex, "刷新下载公开属性失败")
            End Try
        End Sub
        Private RefreshStatLast As Long

        ''' <summary>
        ''' Starts the monitoring thread used to add download threads.
        ''' </summary>
        Private Sub StartManager()
            Static IsStarted As Boolean = False
            If IsStarted Then Return
            IsStarted = True
            Dim ThreadStarter =
            Sub(Id As Integer) '0 or 1
                Try
                    While True
                        Thread.Sleep(20)
                        'Get the file list
                        Dim WaitingFiles As New List(Of NetFile)
                        Dim OngoingFiles As New List(Of NetFile)
                        If Id = 0 AndAlso FileRemain = 0 Then AllFiles.Clear() 'Clear if finished
                        For Each File As NetFile In AllFiles.Values
                            If File.Uuid Mod 2 = Id Then Continue For
                            If File.State = NetState.WaitingToDownload Then
                                WaitingFiles.Add(File)
                            ElseIf File.State < NetState.Merging AndAlso File.State >= NetState.WaitingToDownload Then
                                OngoingFiles.Add(File)
                            End If
                        Next
                        'Start threads for waiting files
                        For Each File As NetFile In WaitingFiles
                            If NetTaskThreadCount >= NetTaskThreadLimit Then Continue While 'Maximum thread count check
                            Dim NewThread = File.TryBeginThread()
                            If NewThread IsNot Nothing AndAlso NewThread.Source.Url.Contains("bmclapi") Then Thread.Sleep(100) 'Reduce the BMCLAPI request rate
                        Next
                        'Add threads for in-progress files
                        If Speed >= NetTaskSpeedLimitLow Then Continue While 'Speed is sufficient, no need to add more
                        For Each File As NetFile In OngoingFiles
                            If NetTaskThreadCount >= NetTaskThreadLimit Then Continue While 'Maximum thread count check
                            'Count threads by kind
                            Dim PreparingCount = 0, DownloadingCount = 0
                            If File.Threads IsNot Nothing Then
                                For Each Thread As NetThread In File.Threads.ToList
                                    If Thread.State < NetState.Downloading Then
                                        PreparingCount += 1
                                    ElseIf Thread.State = NetState.Downloading Then
                                        DownloadingCount += 1
                                    End If
                                Next
                            End If
                            'Add threads
                            If PreparingCount > DownloadingCount Then Continue For 'More threads are preparing than downloading, stop adding
                            Dim NewThread = File.TryBeginThread()
                            If NewThread IsNot Nothing AndAlso NewThread.Source.Url.Contains("bmclapi") Then Thread.Sleep(100) 'Reduce the BMCLAPI request rate
                        Next
                    End While
                Catch ex As Exception
                    Logger.Error(ex, $"下载管理启动线程 {Id} 出错", LogBehavior.AlertThenCrash)
                End Try
            End Sub
            RunInNewThread(Sub() ThreadStarter(0), "NetManager ThreadStarter 0")
            RunInNewThread(Sub() ThreadStarter(1), "NetManager ThreadStarter 1")
            RunInNewThread(
            Sub()
                Try
                    Dim NextTick As Long = GetTimeMs()
                    While True
                        'Increase the speed limit allowance
                        If NetTaskSpeedLimitHigh > 0 Then Interlocked.Add(NetTaskSpeedLimitLeft, CLng(NetTaskSpeedLimitHigh / 10))
                        'Refresh the public properties
                        RefreshStat()
                        'Wait 100 ms
                        NextTick += 100
                        Dim SleepTime = NextTick - GetTimeMs()
                        If SleepTime > 0 Then
                            Thread.Sleep(SleepTime)
                        Else
                            NextTick = GetTimeMs() 'Timed out, catch up frames directly without waiting
                        End If
                    End While
                Catch ex As Exception
                    Logger.Error(ex, "下载管理刷新线程出错", LogBehavior.AlertThenCrash)
                End Try
            End Sub, "NetManager StatRefresher")
        End Sub

        Private DownloadCacheLock As New Object
        Private IsDownloadCacheCleared As Boolean = False
        ''' <summary>
        ''' Starts a download task.
        ''' </summary>
        Public Function Start(Task As LoaderDownload) As List(Of NetFile)
            StartManager()
            'Clean the cache
            SyncLock DownloadCacheLock 'Prevent cleaning repeatedly when multiple download tasks start at the same time
                If Not IsDownloadCacheCleared Then
                    Try
                        Logger.Info("开始清理下载缓存")
                        DirectoryUtils.Delete(PathTemp & "Download")
                        Logger.Info("下载缓存已清理")
                    Catch ex As Exception
                        Logger.Warn(ex, "清理下载缓存失败")
                    End Try
                    IsDownloadCacheCleared = True
                End If
            End SyncLock
            'Add each file
            Dim NewFiles As New List(Of NetFile)
            Dim NewFilesList As New List(Of NetFile)
            For Each File In Task.Files
                Dim OngoingFile As NetFile = Nothing
                If AllFiles.TryGetValue(File.LocalPath, OngoingFile) AndAlso OngoingFile.State < NetState.Finished Then
                    'This file is being downloaded by another task
                    Logger.Trace(Function() $"{File.LocalName}：无需开始，该文件已在下载中，目标 {File.LocalPath}")
                    File = OngoingFile 'Replace the file in the list with the one being downloaded, so both tasks point to the same file; discard the current File object
                Else
                    'Normal case (the file may already have finished in another task, which does not matter)
                    Logger.Trace(Function() $"{File.LocalName}：开始，目标 {File.LocalPath}")
                    'The counter must be incremented before adding to the list, or the manager thread may clear it between the two steps
                    Interlocked.Increment(FileRemain)
                    AllFiles(File.LocalPath) = File
                    NewFiles.Add(File)
                End If
                NewFilesList.Add(File)
                File.Tasks.Add(Task)
            Next
            Task.Files = New ConcurrentQueue(Of NetFile)(NewFilesList)
            Tasks.Add(Task)
            Return NewFiles
        End Function

    End Class

    ''' <summary>
    ''' Whether there are download tasks in progress that need to be shown on the task manager page.
    ''' </summary>
    Public Function HasDownloadingTask(Optional IgnoreCustomDownload As Boolean = False) As Boolean
        Return LoaderTaskbar.Any(
        Function(Task) Task.Show AndAlso Task.State = LoadState.Loading AndAlso
                       (Not IgnoreCustomDownload OrElse Not Task.Name.ToString.Contains("自定义下载")))
    End Function

#End Region

#Region "Ports"

    ''' <summary>
    ''' Gets a random available port.
    ''' </summary>
    Public Function FindFreePort() As Integer
        Dim Listener As New TcpListener(IPAddress.Loopback, 0)
        Listener.Start()
        Dim port As Integer = CType(Listener.LocalEndpoint, IPEndPoint).Port
        Listener.Stop()
        Return port
    End Function

    ''' <summary>
    ''' Gets the list of ports currently in use.
    ''' </summary>
    Public Function GetUsedPorts() As List(Of Integer)
        Dim IPProperties = NetworkInformation.IPGlobalProperties.GetIPGlobalProperties()
        Dim UsedPorts As New List(Of Integer)
        UsedPorts.AddRange(IPProperties.GetActiveTcpListeners().Select(Function(ep) ep.Port))
        UsedPorts.AddRange(IPProperties.GetActiveUdpListeners().Select(Function(ep) ep.Port))
        UsedPorts.AddRange(IPProperties.GetActiveTcpConnections().Select(Function(conn) conn.LocalEndPoint.Port))
        Return UsedPorts.Distinct().ToList()
    End Function

    ''' <summary>
    ''' Finds several consecutive available ports.
    ''' </summary>
    Public Function FindFreePorts(ConsecutiveCount As Integer, ParamArray ExtraBlackLists As Integer()) As List(Of Integer)
        Dim UsedPorts = GetUsedPorts().Concat(ExtraBlackLists)
        For port = 12000 + RandomInteger(0, 1000) To 65000 - ConsecutiveCount
            Dim Range = Enumerable.Range(port, ConsecutiveCount)
            If Not Range.Any(Function(p) UsedPorts.Contains(p)) Then Return Range.ToList
        Next
        Throw New Exception("未能找到可用的端口！")
    End Function

#End Region

    ''' <summary>
    ''' Tests ping. Returns -1 on failure.
    ''' </summary>
    Public Function Ping(Ip As String, Optional Timeout As Integer = 10000, Optional MakeLog As Boolean = True) As Integer
        Dim PingResult As NetworkInformation.PingReply
        Try
            PingResult = (New NetworkInformation.Ping).Send(Ip)
        Catch ex As Exception
            If MakeLog Then Logger.Info($"Ping {Ip} 失败：{ex.Message}")
            Return -1
        End Try
        If PingResult.Status = NetworkInformation.IPStatus.Success Then
            If MakeLog Then Logger.Info($"Ping {Ip} 结束：{PingResult.RoundtripTime}ms")
            Return PingResult.RoundtripTime
        Else
            If MakeLog Then Logger.Info($"Ping {Ip} 失败")
            Return -1
        End If
    End Function


End Module
