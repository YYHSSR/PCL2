Public Class MyImage
    Inherits Image

#Region "Public properties"

    ''' <summary>
    ''' Cache lifetime for network images.
    ''' The image will only be downloaded again after this period.
    ''' </summary>
    Public FileCacheExpiredTime As New TimeSpan(7, 0, 0, 0) '7 days

    ''' <summary>
    ''' Whether network images may be stored locally as a cache.
    ''' </summary>
    Public Property EnableCache As Boolean
        Get
            Return GetValue(EnableCacheProperty)
        End Get
        Set(value As Boolean)
            SetValue(EnableCacheProperty, value)
        End Set
    End Property
    Public Shared Shadows ReadOnly EnableCacheProperty As DependencyProperty = DependencyProperty.Register(
        "EnableCache", GetType(Boolean), GetType(MyImage), New PropertyMetadata(True))

    ''' <summary>
    ''' Similar to the Source of Image.
    ''' If the string starts with http, the image is downloaded and then displayed; if EnableCache is True, the image is also saved as a local cache.
    ''' Supports WebP images.
    ''' </summary>
    Public Shadows Property Source As String 'Overrides the Source property of Image
        Get
            Return _Source
        End Get
        Set(value As String)
            If value = "" Then value = Nothing
            If _Source = value Then Return
            _Source = value
            If Not IsInitialized Then Return 'Fix property read order: load the image only after all XAML properties have been read (#4868)
            Load()
        End Set
    End Property
    Private _Source As String = ""
    Public Shared Shadows ReadOnly SourceProperty As DependencyProperty = DependencyProperty.Register(
        "Source", GetType(String), GetType(MyImage), New PropertyMetadata(New PropertyChangedCallback(
    Sub(sender, e) If sender IsNot Nothing Then CType(sender, MyImage).Source = e.NewValue.ToString())))

    ''' <summary>
    ''' If Source is a network image, this address is used as the fallback image source.
    ''' </summary>
    Public Property FallbackSource As String
        Get
            Return _FallbackSource
        End Get
        Set(value As String)
            _FallbackSource = value
        End Set
    End Property
    Private _FallbackSource As String = Nothing

    ''' <summary>
    ''' Local image shown while the network image is downloading.
    ''' </summary>
    Public Property LoadingSource As String
        Get
            Return _LoadingSource
        End Get
        Set(value As String)
            _LoadingSource = value
        End Set
    End Property
    Private _LoadingSource As String = "pack://application:,,,/images/Icons/NoIcon.png"

#End Region

    ''' <summary>
    ''' The image path that is actually rendered.
    ''' </summary>
    Public Property ActualSource As String
        Get
            Return _ActualSource
        End Get
        Set(value As String)
            If value = "" Then value = Nothing
            If _ActualSource = value Then Return
            _ActualSource = value
            Try
                Dim Bitmap As ImageSource = If(value Is Nothing, Nothing, New MyBitmap(value)) 'Trigger any file read here to avoid reading files on the UI thread as much as possible
                RunInUi(Sub() MyBase.Source = Bitmap)
            Catch ex As Exception
                Logger.Warn(ex, $"加载图片失败（{value}）")
                Try
                    If value.StartsWithF(PathTemp, True) Then FileUtils.Delete(value)
                Catch
                End Try
            End Try
        End Set
    End Property
    Private _ActualSource As String = Nothing

    Private Sub Load() _
        Handles Me.Initialized 'Fix property read order: load the image only after all XAML properties have been read (#4868)
        'Empty
        If Source Is Nothing Then
            ActualSource = Nothing
            Return
        End If
        'Local image
        If Not Source.StartsWithF("http") Then
            ActualSource = Source
            Return
        End If
        'Load the network image from cache
        Dim EnableCache As Boolean = Me.EnableCache
        Dim TempPath As String = $"{PathTemp}MyImage\{Source.GetStableHashCode()}.png{If(EnableCache, "", GetUuid())}" 'Append a random string when caching is disabled to avoid conflicts
        Dim TempFile = FileUtils.GetInfo(TempPath)
        If EnableCache AndAlso TempFile.Exists Then
            ActualSource = TempPath 'Show the cached image
            If (Date.Now - TempFile.LastWriteTime) < FileCacheExpiredTime Then Return 'No need to refresh the cache
        Else
            ActualSource = LoadingSource 'Show the loading image
        End If
        RunInNewThread(
        Sub()
            Dim IsLocalFallback As Boolean = Not String.IsNullOrEmpty(FallbackSource) AndAlso Not FallbackSource.StartsWithF("http")
            Try
                'Download
                NetDownloadByLoader(
                    If(FallbackSource IsNot Nothing AndAlso FallbackSource.StartsWithF("http") AndAlso Source <> FallbackSource,
                        {Source, FallbackSource}, {Source}),
                    TempPath, SimulateBrowserHeaders:=True)
                ActualSource = TempPath
                If Not EnableCache Then FileUtils.Delete(TempPath)
            Catch ex As Exception
                Try
                    If TempPath IsNot Nothing Then FileUtils.Delete(TempPath)
                Catch
                End Try
                'Load the local fallback image
                If IsLocalFallback Then
                    Try
                        ActualSource = FallbackSource
                        Logger.Warn(ex, $"下载图片失败，使用本地备用图片（{Source}）")
                        Return
                    Catch exx As Exception
                        Logger.Error(ex, $"下载图片失败（{Source}）", LogBehavior.Toast)
                        Logger.Error(exx, $"加载备用图片失败（{FallbackSource}）", LogBehavior.Toast)
                    End Try
                Else
                    Logger.Error(ex, $"下载图片失败（{Source}，备用：{FallbackSource}）", LogBehavior.Toast)
                End If
            End Try
        End Sub, "MyImage PicLoader " & GetUuid() & "#", ThreadPriority.BelowNormal)
    End Sub

End Class