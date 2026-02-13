Imports System.Windows.Forms
Imports Microsoft.AspNetCore.Http
Imports Newtonsoft.Json
Imports HttpMethod = Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpMethod

Public Class KWebCore
    Implements IDisposable

    Private Enum ServerErrorHandler
        None
        BannedHyperV
        SearchPort
    End Enum

    Private Const APIPrefix As String = "/api"

    Private ReadOnly K As EasyK

    Private WithEvents Server As WebServer

    Private ReadOnly Uploader As UploadManager

    Private Shared ContentRegex As New Text.RegularExpressions.Regex("^[A-Za-z\d]+(?:\?p=\d+)?$")

    Private ReadOnly Settings As SettingContainer

    Private Handler As ServerErrorHandler

    ''' <summary>
    ''' 发生无法处理的错误
    ''' </summary>
    Public Event OnUncaughtError()

    ''' <summary>
    ''' 初始化
    ''' </summary>
    ''' <param name="K"></param>
    ''' <param name="Settings">配置容器</param>
    Public Sub New(K As EasyK, Settings As SettingContainer)
        Me.K = K
        Me.Settings = Settings

        WebStartup.WebRoot = IO.Path.Combine(Application.StartupPath, "wwwroot")
        WebStartup.Register(Me, Settings, APIPrefix)

        Uploader = New UploadManager(Settings)

        Handler = ServerErrorHandler.None
        Server = New WebServer(Settings.Settings.Web.Port, Settings.Settings.DebugMode)
        AddHandler Server.OnErrorTrigger, AddressOf OnServerError
    End Sub

    '自动处理服务器错误
    Private Sub OnServerError(Exceptions As Exception())
        For Each e In Exceptions
            If e.GetType() = GetType(Net.Sockets.SocketException) AndAlso e.HResult = -2147467259 Then
                '端口绑定错误
                If Settings.Settings.Web.AutoDebug Then
                    HandleServerSocketError()
                    Return
                Else
                    Console.WriteLine("服务器错误自动除错已关闭 无法自动除错")
                End If
            End If

            Console.WriteLine("服务器错误 #{0}: {1}(0x{2})", e.GetType().FullName, e.Message, e.HResult.ToString("x2"))
        Next

        Console.WriteLine("检测到无法处理的服务器错误 需要人工介入处理")
        RaiseEvent OnUncaughtError()
    End Sub

    ''' <summary>
    ''' 重启HTTP服务器
    ''' </summary>
    Public Sub RestartServer()
        RemoveHandler Server.OnErrorTrigger, AddressOf OnServerError
        Server.Dispose()
        Server = New WebServer(Settings.Settings.Web.Port, Settings.Settings.DebugMode)
        AddHandler Server.OnErrorTrigger, AddressOf OnServerError
    End Sub

    '处理端口占用错误
    Private Sub HandleServerSocketError()
        RemoveHandler Server.OnErrorTrigger, AddressOf OnServerError

        Select Case Handler
            Case ServerErrorHandler.None
                Console.WriteLine("HTTP服务器端口被占用")
                Console.WriteLine("正在尝试调整Hyper-V端口占用...")

                '调整Hyper-V端口占用
                Shell("netsh int ipv4 set dynamic tcp start=49152 num=16384", AppWinStyle.Hide, True)
                Shell("netsh int ipv4 set dynamic tcp start=49152 num=16384", AppWinStyle.Hide, True)

                Console.WriteLine("Hyper-V端口占用调整完成")
                Console.WriteLine("正在尝试重启WinNAT服务...")

                '重启WinNAT服务
                Shell("net stop winnat", AppWinStyle.Hide, True)
                Shell("net start winnat", AppWinStyle.Hide, True)

                Console.WriteLine("重启WinNAT服务完成")
                Console.WriteLine("正在尝试重启HTTP服务端...")

                '更新进度
                Handler = ServerErrorHandler.BannedHyperV

                '重启服务器
                RestartServer()
            Case ServerErrorHandler.BannedHyperV
                Console.WriteLine("HTTP服务器端口仍被占用")
                Console.WriteLine("正在尝试自动查找可用端口...")

                Dim Used = NetUtils.GetUsedTcpPorts()
                With Settings.Settings.Web
                    For i = .AutoPortMin To .AutoPortMax
                        If Not Used.Contains(i) Then
                            '更新配置
                            .Port = i

                            '更新进度
                            Handler = ServerErrorHandler.SearchPort

                            Console.WriteLine("HTTP服务器端口更改为 {0}", i)
                            Console.WriteLine("正在尝试重启HTTP服务端...")

                            '重启服务器
                            RestartServer()

                            Return
                        End If
                    Next
                End With

                Console.WriteLine("未在配置的范围内找到可用端口")
                Console.WriteLine("无法处理服务器错误 需要人工介入处理")
                RaiseEvent OnUncaughtError()
            Case Else
                Console.WriteLine("无法处理服务器错误 需要人工介入处理")
                RaiseEvent OnUncaughtError()
        End Select
    End Sub

    ''' <summary>
    ''' 获取已占用的缓存文件
    ''' </summary>
    ''' <returns></returns>
    Public Function GetOccupiedFiles() As List(Of String)
        Return Uploader.GetOccupiedFiles()
    End Function

    ''' <summary>
    ''' 销毁资源
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        With Server
            RemoveHandler .OnErrorTrigger, AddressOf OnServerError
            .Dispose()
        End With

        Uploader.Dispose()
    End Sub

    <WebApi("/current", HttpMethod.Get)>
    Private Function Current(ctx As HttpContext) As Task
        Return WebStartup.RespondJson(ctx, $"{{""current"":{JsonConvert.SerializeObject(K.GetCurrent())}}}")
    End Function

    <WebApi("/list", HttpMethod.Get)>
    Private Function List(ctx As HttpContext) As Task
        Return WebStartup.RespondJson(ctx, $"{{""list"":{JsonConvert.SerializeObject(K.GetBookList())}}}")
    End Function

    <WebApi("/top", HttpMethod.Post)>
    Private Function Top(ctx As HttpContext) As Task
        Dim Request As String = WebStartup.GetRequestBody(ctx)

        Dim Id As RequestId = JsonUtils.SafeDeserializeObject(Of RequestId)(Request)
        If Id Is Nothing OrElse String.IsNullOrEmpty(Id.Id) Then _
            Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status400BadRequest)

        Dim User As String = ctx.Request.Cookies.Item("name")
        If String.IsNullOrEmpty(User) Then User = "未知用户"

        Dim Result As EasyKBookRecord = K.SendToTop(Id.Id)
        If Result IsNot Nothing Then
            Console.WriteLine("{0}> 对 {1} 执行顶歌", User, $"{Result.Title}(来自:{Result.Order})")
            Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status204NoContent)
        Else
            Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status422UnprocessableEntity)
        End If
    End Function

    <WebApi("/push", HttpMethod.Get)>
    Private Function Push(ctx As HttpContext) As Task
        K.Push()
        Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status204NoContent)
    End Function

    <WebApi("/pause", HttpMethod.Get)>
    Private Function Puause(ctx As HttpContext) As Task
        K.Pause()
        Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status204NoContent)
    End Function

    <WebApi("/remove", HttpMethod.Post)>
    Private Function Remove(ctx As HttpContext) As Task
        Dim Request As String = WebStartup.GetRequestBody(ctx)

        Dim Id As RequestId = JsonUtils.SafeDeserializeObject(Of RequestId)(Request)
        If Id Is Nothing OrElse String.IsNullOrEmpty(Id.Id) Then _
            Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status400BadRequest)

        Return WebStartup.RespondStatusOnly(ctx, If(K.Remove(Id.Id),
                                                    StatusCodes.Status204NoContent,
                                                    StatusCodes.Status422UnprocessableEntity))
    End Function

    <WebApi("/outdated", HttpMethod.Get)>
    Private Function Outdated(ctx As HttpContext) As Task
        Return WebStartup.RespondJson(ctx, $"{{""list"":{JsonConvert.SerializeObject(K.GetOutdatedList())}}}")
    End Function

    <WebApi("/reorder", HttpMethod.Post)>
    Private Function Reorder(ctx As HttpContext) As Task
        Dim Request As String = WebStartup.GetRequestBody(ctx)

        Dim Id As RequestId = JsonUtils.SafeDeserializeObject(Of RequestId)(Request)
        If Id Is Nothing OrElse String.IsNullOrEmpty(Id.Id) Then _
            Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status400BadRequest)

        Dim Order As String = ctx.Request.Cookies.Item("name")
        If String.IsNullOrEmpty(Order) Then Order = "未知用户"

        Dim NewId As String = K.Reorder(Id.Id, Order)
        If Not String.IsNullOrEmpty(NewId) Then
            Return WebStartup.RespondJson(ctx, $"{{""id"":""{NewId}""}}")
        Else
            Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status422UnprocessableEntity)
        End If
    End Function

    <WebApi("/book", HttpMethod.Post)>
    Private Function Book(ctx As HttpContext) As Task
        Dim Request As String = WebStartup.GetRequestBody(ctx)

        Dim Booking As RequestBook = JsonUtils.SafeDeserializeObject(Of RequestBook)(Request)
        If Booking Is Nothing OrElse Not [Enum].IsDefined(GetType(EasyKType), Booking.Type) OrElse
            String.IsNullOrWhiteSpace(Booking.Title) OrElse
            (Not ContentRegex.IsMatch(Booking.Content) AndAlso Booking.Type <> EasyKType.DLNA) Then
            Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status400BadRequest)
        End If

        Dim Order As String = ctx.Request.Cookies.Item("name")
        Uploader.FreeSession(Order)

        If String.IsNullOrEmpty(Order) Then Order = "未知用户"

        With Booking
            Dim NewId As String = K.Book(.Title, Order, DirectCast(.Type, EasyKType), If(.Type = EasyKType.DLNA, ctx.Connection.RemoteIpAddress.ToString(), .Content))
            Return WebStartup.RespondJson(ctx, $"{{""id"":""{NewId}""}}")
        End With
    End Function

    <WebApi("/upload")>
    Private Function Upload(ctx As HttpContext) As Task
        Return Uploader.Progress(ctx)
    End Function

    '生成面板信息
    Private Function GeneratePanelResult() As String
        Dim PanelResult As New Dictionary(Of String, Object)(Settings.Settings.PluginCommon)

        With PanelResult
            .Add("volume", K.Volume)
            If Settings.Settings.Audio.AllowAccompaniment Then .Add("accompaniment", K.Accompaniment)

            .Add("offset", K.LyricOffset)
        End With

        Return JsonConvert.SerializeObject(PanelResult)
    End Function

    <WebApi("/panel")>
    Private Function Panel(ctx As HttpContext) As Task
        Select Case ctx.Request.Method.ToUpper()
            Case "GET"
                Return WebStartup.RespondJson(ctx, GeneratePanelResult())
            Case "POST"
                Dim Request As String = WebStartup.GetRequestBody(ctx)

                Dim p As RequestPanel = JsonUtils.SafeDeserializeObject(Of RequestPanel)(Request)
                If p Is Nothing Then Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status400BadRequest)

                Select Case p.Id.ToLower()
                    Case "volume"
                        '更改音量
                        Try
                            K.Volume = Double.Parse(p.Value)
                        Catch
                            Console.WriteLine("错误的音量 - {0}", p.Value)
                        End Try
                    Case "accompaniment"
                        '更改伴唱状态
                        Try
                            K.Accompaniment = Boolean.Parse(p.Value)
                        Catch
                            Console.WriteLine("错误的伴唱状态 - {0}", p.Value)
                        End Try
                    Case "offset"
                        '更改歌词偏移
                        Try
                            K.LyricOffset = Double.Parse(p.Value)
                        Catch
                            Console.WriteLine("错误的歌词偏移 - {0}", p.Value)
                        End Try
                    Case Else
                        If Settings.Settings.PluginCommon.ContainsKey(p.Id) Then
                            '更新插件配置
                            Settings.Settings.PluginCommon(p.Id) = p.Value
                            DLNA.MusicProvider.DLNAMusicProviders.TryUpdateSettings()

                            K.TriggerMirrorPlay("Refresh")
                        Else
                            Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status400BadRequest)
                        End If
                End Select

                Return WebStartup.RespondJson(ctx, GeneratePanelResult())
        End Select

        Return WebStartup.RespondStatusOnly(ctx)
    End Function

    <WebApi("/volume", HttpMethod.Post)>
    Private Function Volume(ctx As HttpContext) As Task
        Dim Request As String = WebStartup.GetRequestBody(ctx)

        Dim p As RequestVolume = JsonUtils.SafeDeserializeObject(Of RequestVolume)(Request)
        If p Is Nothing Then Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status400BadRequest)

        K.Volume = p.Volume

        Return WebStartup.RespondJson(ctx, $"{{""value"":{K.Volume}}}")
    End Function

End Class
