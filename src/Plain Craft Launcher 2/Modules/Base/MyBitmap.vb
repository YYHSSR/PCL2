'An all-purpose automatic image type conversion helper class

Imports System.Drawing.Imaging

Public Class MyBitmap

    ''' <summary>
    ''' The stored image
    ''' </summary>
    Public Pic As System.Drawing.Bitmap

    'Automatic type conversion
    'Supported classes: Image, ImageSource, Bitmap, ImageBrush, BitmapSource
    Public Shared Widening Operator CType(Image As System.Drawing.Image) As MyBitmap
        If Image Is Nothing Then Return Nothing
        Return New MyBitmap(Image)
    End Operator
    Public Shared Widening Operator CType(Image As MyBitmap) As System.Drawing.Image
        If Image Is Nothing Then Return Nothing
        Return Image.Pic
    End Operator
    Public Shared Widening Operator CType(Image As ImageSource) As MyBitmap
        If Image Is Nothing Then Return Nothing
        Return New MyBitmap(Image)
    End Operator
    Public Shared Widening Operator CType(Image As MyBitmap) As ImageSource
        If Image Is Nothing Then Return Nothing
        Dim BitmapPic As System.Drawing.Bitmap = Image.Pic
        Dim rect = New System.Drawing.Rectangle(0, 0, BitmapPic.Width, BitmapPic.Height)
        Dim bitmapData = BitmapPic.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
        Try
            Dim Result = BitmapSource.Create(BitmapPic.Width, BitmapPic.Height, BitmapPic.HorizontalResolution, BitmapPic.VerticalResolution,
                                       PixelFormats.Bgra32, Nothing, bitmapData.Scan0,
                                       rect.Width * rect.Height * 4, bitmapData.Stride)
            Result.Freeze()
            Return Result
        Finally
            BitmapPic.UnlockBits(bitmapData)
        End Try
    End Operator
    Public Shared Widening Operator CType(Image As System.Drawing.Bitmap) As MyBitmap
        If Image Is Nothing Then Return Nothing
        Return New MyBitmap(Image)
    End Operator
    Public Shared Widening Operator CType(Image As MyBitmap) As System.Drawing.Bitmap
        If Image Is Nothing Then Return Nothing
        Return Image.Pic
    End Operator
    Public Shared Widening Operator CType(Image As ImageBrush) As MyBitmap
        If Image Is Nothing Then Return Nothing
        Return New MyBitmap(Image)
    End Operator
    Public Shared Widening Operator CType(Image As MyBitmap) As ImageBrush
        If Image Is Nothing Then Return Nothing
        Return New ImageBrush(New MyBitmap(Image.Pic))
    End Operator

    Public Sub New()
    End Sub
    Public Sub New(FilePathOrResourceName As String)
        Try
            FilePathOrResourceName = FilePathOrResourceName.Replace("pack://application:,,,/images/", PathImage)
            If FilePathOrResourceName.StartsWithF(PathImage) Then
                'Use the cache
                Static Cache As New Concurrent.ConcurrentDictionary(Of String, MyBitmap)
                If Cache.ContainsKey(FilePathOrResourceName) Then
                    Pic = Cache(FilePathOrResourceName).Pic
                Else
                    Pic = New MyBitmap(CType((New ImageSourceConverter).ConvertFromString(FilePathOrResourceName), ImageSource))
                    Cache.TryAdd(FilePathOrResourceName, Pic)
                End If
            Else
                'The file lock is only released when loading this way, managing the FileStream ourselves
                Using InputStream = FileUtils.ReadAsStream(FilePathOrResourceName)
                    'Check whether it has a WebP file header
                    Dim Header(1) As Byte
                    InputStream.Read(Header, 0, 2)
                    InputStream.Seek(0, SeekOrigin.Begin)
                    If Header(0) = 82 AndAlso Header(1) = 73 Then
                        'Read WebP
                        Dim FileBytes(InputStream.Length - 1) As Byte
                        InputStream.Read(FileBytes, 0, FileBytes.Length)
                        Pic = WebPDecoder.DecodeFromBytes(FileBytes) 'Code is isolated in another class so that Imazen.WebP.dll is only loaded when this branch is taken
                    Else
                        Pic = New System.Drawing.Bitmap(InputStream)
                    End If
                End Using
            End If
        Catch ex As Exception
            Pic = My.Application.TryFindResource(FilePathOrResourceName)
            If Pic Is Nothing Then
                Pic = New System.Drawing.Bitmap(1, 1)
                If TypeOf ex Is ArgumentException Then
                    Throw New Exception($"图片格式不支持，或图片文件损坏（{FilePathOrResourceName}）", ex)
                Else
                    Throw New Exception($"加载 MyBitmap 意外失败（{FilePathOrResourceName}）", ex)
                End If
            Else
                Logger.Warn(ex, $"指定类型有误的 MyBitmap 加载（{FilePathOrResourceName}）")
                Exit Try
            End If
        End Try
    End Sub
    Public Sub New(Image As ImageSource)
        Using MS = New MemoryStream()
            Dim Encoder = New PngBitmapEncoder()
            Encoder.Frames.Add(BitmapFrame.Create(Image))
            Encoder.Save(MS)
            Pic = New System.Drawing.Bitmap(MS)
        End Using
    End Sub
    Public Sub New(Image As System.Drawing.Image)
        Pic = Image
    End Sub
    Public Sub New(Image As System.Drawing.Bitmap)
        Pic = Image
    End Sub
    Public Sub New(Image As ImageBrush)
        Using MS = New MemoryStream()
            Dim Encoder = New BmpBitmapEncoder()
            Encoder.Frames.Add(BitmapFrame.Create(Image.ImageSource))
            Encoder.Save(MS)
            Pic = New System.Drawing.Bitmap(MS)
        End Using
    End Sub
    Private Class WebPDecoder 'Code is isolated in another class so that Imazen.WebP.dll is only loaded when this method is called
        Public Shared Function DecodeFromBytes(Bytes As Byte()) As System.Drawing.Bitmap
            Return New Imazen.WebP.SimpleDecoder().DecodeFromBytes(Bytes, Bytes.Length)
        End Function
    End Class

    ''' <summary>
    ''' Gets a cropped image. This does not modify the original object and returns a new object.
    ''' </summary>
    Public Function Clip(X As Integer, Y As Integer, Width As Integer, Height As Integer) As MyBitmap
        Dim bmp As New System.Drawing.Bitmap(Width, Height, Pic.PixelFormat)
        bmp.SetResolution(Pic.HorizontalResolution, Pic.VerticalResolution)
        Using g As System.Drawing.Graphics = System.Drawing.Graphics.FromImage(bmp)
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor
            g.TranslateTransform(-X, -Y)
            g.DrawImage(Pic, New System.Drawing.Rectangle(0, 0, Pic.Width, Pic.Height))
        End Using
        Return bmp
    End Function

    ''' <summary>
    ''' Gets a rotated or flipped image. This does not modify the original object and returns a new object.
    ''' </summary>
    Public Function RotateFlip(Type As System.Drawing.RotateFlipType) As MyBitmap
        Dim bmp As New System.Drawing.Bitmap(Pic)
        bmp.SetResolution(Pic.HorizontalResolution, Pic.VerticalResolution)
        bmp.RotateFlip(Type)
        Return bmp
    End Function

    ''' <summary>
    ''' Saves the image to a file.
    ''' </summary>
    Public Sub Save(FilePath As String)
        Dim encoder As BitmapEncoder = New PngBitmapEncoder()
        encoder.Frames.Add(BitmapFrame.Create(Me))
        Using fileStream = FileUtils.CreateAsStream(FilePath)
            encoder.Save(fileStream)
        End Using
    End Sub

End Class
