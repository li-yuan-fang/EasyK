
Imports System.Collections.Concurrent
Imports System.Security.Cryptography
Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.AspNetCore.Http
Imports Microsoft.Extensions.Primitives
Imports Newtonsoft.Json

Public Class UploadManager
    Implements IDisposable

    Private Shared NumericRegex As New Text.RegularExpressions.Regex("^\d+$")

    'Tip.此处长度为64 所以单个字符重复次数是63
    Private Shared HashRegex As New Text.RegularExpressions.Regex("^[A-Za-z\d].{63}$")

    '上传会话
    Private Class UploadSession
        Implements IDisposable

        '会话ID
        Public ReadOnly Property Id As String

        Private ExpireTime As Long

        Private ReadOnly Total As Integer

        Private ReadOnly Uploaded As New HashSet(Of Integer)

        Private RefreshCount As Integer

        Private ReadOnly Stream As IO.FileStream

        Private ReadOnly StreamMuteX As New Mutex()

        Private ReadOnly Settings As SettingContainer

        Private Hash As String = vbNullString

        ''' <summary>
        ''' 初始化上传会话
        ''' </summary>
        ''' <param name="Size">文件大小(单位:byte)</param>
        ''' <param name="Settings">配置容器</param>
        Public Sub New(Size As Integer, Settings As SettingContainer)
            Me.Settings = Settings

            Id = Now.Ticks.ToString("x2")

            Total = Size \ Settings.Settings.Web.Upload.ChunkSize
            If Size Mod Settings.Settings.Web.Upload.ChunkSize <> 0 Then Total += 1

            ExpireTime = Now.Ticks + Settings.Settings.Web.Upload.ExpireDuration

            Stream = New IO.FileStream(IO.Path.Combine(Application.StartupPath, Settings.Settings.TempFolder, Id), IO.FileMode.Create)
            RefreshCount = 0
        End Sub

        ''' <summary>
        ''' 计算Hash
        ''' </summary>
        ''' <returns></returns>
        Public Function ComputeSHA256() As String
            If Not String.IsNullOrEmpty(Hash) Then Return Hash

            Dim Result As String = vbNullString
            Using sha256 As IncrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
                SyncLock StreamMuteX
                    With Stream
                        Dim buffer(1023) As Byte

                        .Seek(0, IO.SeekOrigin.Begin)
                        Dim cnt As Integer = .Read(buffer, 0, buffer.Length)
                        While cnt > 0
                            sha256.AppendData(buffer, 0, cnt)
                            cnt = .Read(buffer, 0, buffer.Length)
                        End While
                    End With
                End SyncLock

                Dim hashBytes As Byte() = sha256.GetHashAndReset()
                If hashBytes IsNot Nothing Then
                    For Each b As Byte In hashBytes
                        Result &= b.ToString("x2")
                    Next
                End If
            End Using

            If IsCompleted() Then Hash = Result

            Return Result
        End Function

        ''' <summary>
        ''' 上传分块
        ''' </summary>
        ''' <param name="Buffer">分块数据</param>
        ''' <param name="Index">分块序号</param>
        ''' <returns></returns>
        Public Function Upload(Buffer() As Byte, Index As Integer) As Boolean
            Dim Complete As Boolean

            SyncLock Uploaded
                Uploaded.Add(Index)
                ExpireTime = Now.Ticks + Settings.Settings.Web.Upload.ExpireDuration

                Complete = Uploaded.Count >= Total
            End SyncLock

            SyncLock StreamMuteX
                With Stream
                    .Seek(Index * Settings.Settings.Web.Upload.ChunkSize, IO.SeekOrigin.Begin)
                    .Write(Buffer, 0, Math.Min(Settings.Settings.Web.Upload.ChunkSize, Buffer.Length))
                End With
                RefreshCount += 1

                If RefreshCount >= Settings.Settings.Web.Upload.RefreshThreshold OrElse Complete Then
                    RefreshCount = 0
                    Stream.Flush()
                End If
            End SyncLock

            Return Complete
        End Function

        ''' <summary>
        ''' 检测上传是否超时
        ''' </summary>
        ''' <returns></returns>
        Public Function IsExpired() As Boolean
            Return Now.Ticks > ExpireTime
        End Function

        ''' <summary>
        ''' 检测上传是否完成
        ''' </summary>
        ''' <returns></returns>
        Public Function IsCompleted() As Boolean
            SyncLock Uploaded
                Return Uploaded.Count >= Total
            End SyncLock
        End Function

        ''' <summary>
        ''' 获取未完成的分块
        ''' </summary>
        ''' <returns></returns>
        Public Function GetRequirements() As List(Of Integer)
            Dim Requirements As New List(Of Integer)
            SyncLock Uploaded
                For i = 0 To Total - 1
                    If Not Uploaded.Contains(i) Then Requirements.Add(i)
                Next
            End SyncLock

            Return Requirements
        End Function

        ''' <summary>
        ''' 销毁资源
        ''' </summary>
        Public Sub Dispose() Implements IDisposable.Dispose
            With Stream
                .Flush()
                .Close()
                .Dispose()
            End With
        End Sub

    End Class

    '上传会话
    Private ReadOnly Sessions As New ConcurrentDictionary(Of String, UploadSession)()

    '清理任务
    Private ReadOnly Cleaner As Task

    '已占用的文件列表
    Private ReadOnly Occupied As New List(Of String)

    Private ReadOnly Settings As SettingContainer

    Private ExitFlag As Boolean = False

    ''' <summary>
    ''' 初始化上传管理器
    ''' </summary>
    ''' <param name="Settings">配置容器</param>
    Public Sub New(Settings As SettingContainer)
        Me.Settings = Settings

        Dim Folder As String = IO.Path.Combine(Application.StartupPath, Settings.Settings.TempFolder)
        If Not IO.Directory.Exists(Folder) Then IO.Directory.CreateDirectory(Folder)

        Cleaner = Task.Run(AddressOf Clean)
    End Sub

    '清理
    Private Sub Clean()
        Dim cnt As Long
        While Not ExitFlag
            cnt = 0
            While Not ExitFlag AndAlso cnt < Settings.Settings.Web.Upload.CleanDuration
                Thread.Sleep(100)
                cnt += 100
            End While

            If ExitFlag Then Return

            Dim ToRemove As New List(Of String)
            SyncLock Occupied
                Occupied.Clear()
                For Each key As String In Sessions.Keys
                    If Sessions(key).IsExpired() Then
                        ToRemove.Add(key)
                        Continue For
                    End If

                    Occupied.Add(Sessions(key).Id)
                Next
            End SyncLock

            For Each key As String In ToRemove
                Dim Session As UploadSession = Sessions(key)
                If Session Is Nothing Then
                    Sessions.TryRemove(key, Session)
                Else
                    If Session.IsExpired Then Sessions.TryRemove(key, Session)
                End If
            Next
        End While
    End Sub

    ''' <summary>
    ''' 处理上传
    ''' </summary>
    ''' <param name="ctx">HTTP上下文</param>
    ''' <returns></returns>
    Public Function Progress(ctx As HttpContext) As Task
        '上传流程: 请求上传会话(POST) -> 上传分块(PUT) -> 获取会话状态(GET)

        Dim Name As String = ctx.Request.Cookies.Item("name")
        If String.IsNullOrEmpty(Name) Then Return WebStartup.RespondStatusOnly(ctx)

        Select Case ctx.Request.Method
            Case = "GET"
                Return HandleGet(ctx, Name)
            Case = "POST"
                Return HandlePost(ctx, Name)
            Case = "PUT"
                Return HandlePut(ctx, Name)
            Case Else
                Return WebStartup.RespondStatusOnly(ctx)
        End Select
    End Function

    ''' <summary>
    ''' 释放上下文
    ''' </summary>
    ''' <param name="Name">用户名称</param>
    Public Sub FreeSession(Name As String)
        If String.IsNullOrEmpty(Name) Then Return

        Dim Session As UploadSession = Nothing
        If Not Sessions.TryRemove(Name, Session) OrElse Session Is Nothing Then Return
        Session.Dispose()
        Session = Nothing
    End Sub

    '获取当前会话状态
    Private Function HandleGet(ctx As HttpContext, Name As String) As Task
        Dim Session As UploadSession = Nothing
        If Sessions.TryGetValue(Name, Session) Then
            '返回当前会话状态
            If Settings.Settings.DebugMode Then Console.WriteLine("{0}> 完成状态: {1}", Name, Session.IsCompleted().ToString().ToLower())

            If Session.IsCompleted() Then
                Return WebStartup.RespondJson(ctx, $"{{""busy"":true,""id"":""{Session.Id}"",""complete"":true,""hash"":""{Session.ComputeSHA256()}""}}")
            Else
                Return WebStartup.RespondJson(ctx, $"{{""busy"":true,""id"":""{Session.Id}"",""complete"":false,""require"":{JsonConvert.SerializeObject(Session.GetRequirements)}}}")
            End If
        Else
            '空会话
            Return WebStartup.RespondJson(ctx, "{""busy"":false}")
        End If
    End Function

    '请求新会话
    Private Function HandlePost(ctx As HttpContext, Name As String) As Task
        '解析请求
        Dim Request As RequestSize = JsonUtils.SafeDeserializeObject(Of RequestSize)(WebStartup.GetRequestBody(ctx))
        If Request Is Nothing OrElse Request.Size <= 0 Then Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status400BadRequest)

        '移除旧会话
        Dim Session As UploadSession = Nothing
        Sessions.TryRemove(Name, Session)
        If Session IsNot Nothing Then Session.Dispose()

        '检查文件尺寸
        Dim Max As Integer = Settings.Settings.Web.Upload.MaxUploadSize
        If Max >= 0 AndAlso Request.Size > Max Then Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status413PayloadTooLarge)

        '创建新会话
        Session = New UploadSession(Request.Size, Settings)
        If Settings.Settings.DebugMode Then Console.WriteLine("{0}> 新会话 {1}", Name, Session.Id)

        If Sessions.TryAdd(Name, Session) Then
            Return WebStartup.RespondJson(ctx, $"{{""id"":""{Session.Id}"",""chunk"":{Settings.Settings.Web.Upload.ChunkSize}}}")
        Else
            Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status500InternalServerError)
        End If
    End Function

    '上传分块
    Private Function HandlePut(ctx As HttpContext, Name As String) As Task
        Dim Session As UploadSession = Nothing
        If Not Sessions.TryGetValue(Name, Session) OrElse Session Is Nothing OrElse Session.IsExpired() Then _
            Return WebStartup.RespondStatusOnly(ctx)

        Dim Buffer As Byte()
        Dim IndexValue As StringValues
        Dim HashValue As StringValues

        Dim Index As Integer
        Dim Hash As String

        With ctx.Request
            If Not .Headers.TryGetValue("Upload-Index", IndexValue) OrElse Not .Headers.TryGetValue("Upload-Hash", HashValue) Then _
                Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status400BadRequest)

            If IndexValue.Count = 0 OrElse HashValue.Count = 0 OrElse Not NumericRegex.IsMatch(IndexValue(0).ToString()) OrElse
                Not HashRegex.IsMatch(HashValue(0).ToString()) Then _
                Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status400BadRequest)

            Index = Val(IndexValue(0))
            Hash = HashValue(0)
        End With

        Dim Content As String = WebStartup.GetRequestBody(ctx)
        Buffer = Convert.FromBase64String(Content)

        Dim LocalHash As String = HashUtils.ComputeSHA256(Buffer)
        If LocalHash <> Hash.ToLower() Then
            If Settings.Settings.DebugMode Then _
                Console.WriteLine("{0}> #{1} {2} {3} - {4}", Name, Index, Buffer.Length, Hash, LocalHash)

            Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status400BadRequest)
        End If

        Return WebStartup.RespondJson(ctx, $"{{""complete"":{Session.Upload(Buffer, Index).ToString().ToLower()}}}")
    End Function

    ''' <summary>
    ''' 获取已占用的文件列表
    ''' </summary>
    ''' <returns></returns>
    Public Function GetOccupiedFiles() As List(Of String)
        SyncLock Occupied
            Return New List(Of String)(Occupied)
        End SyncLock
    End Function

    ''' <summary>
    ''' 销毁资源
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        ExitFlag = True
        With Cleaner
            .Wait()
            .Dispose()
        End With

        For Each key As String In Sessions.Keys
            Sessions(key).Dispose()
        Next
        Sessions.Clear()
    End Sub

End Class
