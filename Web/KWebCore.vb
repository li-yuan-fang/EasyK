Imports System.Windows.Forms
Imports Microsoft.AspNetCore.Http
Imports Newtonsoft.Json
Imports HttpMethod = Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpMethod

Public Class KWebCore
    Implements IDisposable

    Private Const APIPrefix As String = "/api"

    Private ReadOnly K As EasyK

    Private ReadOnly Server As WebServer

    Private ReadOnly Uploader As UploadManager

    Private Shared ContentRegex As New Text.RegularExpressions.Regex("^[A-Za-z\d]+(?:\?p=\d+)?$")

    Private ReadOnly Settings As SettingContainer

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

        Server = New WebServer(Settings.Settings.Web.Port, Settings.Settings.DebugMode)
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
        Uploader.Dispose()
        Server.Dispose()
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

        Dim Id As RequestId = JsonConvert.DeserializeObject(Of RequestId)(Request)
        If Id Is Nothing OrElse String.IsNullOrEmpty(Id.Id) Then Return WebStartup.RespondJson(ctx, "{""success"":false}")

        Dim User As String = ctx.Request.Cookies.Item("name")
        If String.IsNullOrEmpty(User) Then User = "未知用户"

        Console.WriteLine("{0}> 对 {1} 执行顶歌", User, Id.Id)

        Return WebStartup.RespondJson(ctx, $"{{""success"":{K.SendToTop(Id.Id).ToString().ToLower()}}}")
    End Function

    <WebApi("/push", HttpMethod.Get)>
    Private Function Push(ctx As HttpContext) As Task
        K.Push()
        Return WebStartup.RespondJson(ctx, "{""success"":true}")
    End Function

    <WebApi("/pause", HttpMethod.Get)>
    Private Function Puause(ctx As HttpContext) As Task
        K.Pause()
        Return WebStartup.RespondJson(ctx, "{""success"":true}")
    End Function

    <WebApi("/remove", HttpMethod.Post)>
    Private Function Remove(ctx As HttpContext) As Task
        Dim Request As String = WebStartup.GetRequestBody(ctx)

        Dim Id As RequestId = JsonConvert.DeserializeObject(Of RequestId)(Request)
        If Id Is Nothing OrElse String.IsNullOrEmpty(Id.Id) Then Return WebStartup.RespondJson(ctx, "{""success"":false}")

        Return WebStartup.RespondJson(ctx, $"{{""success"":{K.Remove(Id.Id).ToString().ToLower()}}}")
    End Function

    <WebApi("/outdated", HttpMethod.Get)>
    Private Function Outdated(ctx As HttpContext) As Task
        Return WebStartup.RespondJson(ctx, $"{{""list"":{JsonConvert.SerializeObject(K.GetOutdatedList())}}}")
    End Function

    <WebApi("/reorder", HttpMethod.Post)>
    Private Function Reorder(ctx As HttpContext) As Task
        Dim Request As String = WebStartup.GetRequestBody(ctx)

        Dim Id As RequestId = JsonConvert.DeserializeObject(Of RequestId)(Request)
        If Id Is Nothing OrElse String.IsNullOrEmpty(Id.Id) Then Return WebStartup.RespondJson(ctx, "{""success"":false}")

        Dim Order As String = ctx.Request.Cookies.Item("name")
        If String.IsNullOrEmpty(Order) Then Order = "未知用户"

        Dim NewId As String = K.Reorder(Id.Id, Order)
        If Not String.IsNullOrEmpty(NewId) Then
            Return WebStartup.RespondJson(ctx, $"{{""success"":true,""id"":""{NewId}""}}")
        Else
            Return WebStartup.RespondJson(ctx, "{""success"":false}")
        End If
    End Function

    <WebApi("/book", HttpMethod.Post)>
    Private Function Book(ctx As HttpContext) As Task
        Dim Request As String = WebStartup.GetRequestBody(ctx)

        Dim Booking As RequestBook = JsonConvert.DeserializeObject(Of RequestBook)(Request)
        If Booking Is Nothing OrElse Not [Enum].IsDefined(GetType(EasyKType), Booking.Type) OrElse
            (Not ContentRegex.IsMatch(Booking.Content) AndAlso Booking.Type <> EasyKType.DLNA) Then
            Return WebStartup.RespondJson(ctx, "{""success"":false}")
        End If

        Dim Order As String = ctx.Request.Cookies.Item("name")
        Uploader.FreeSession(Order)

        If String.IsNullOrEmpty(Order) Then Order = "未知用户"

        Dim NewId As String
        With Booking
            NewId = K.Book(.Title, Order, .Type, If(.Type = EasyKType.DLNA, ctx.Connection.RemoteIpAddress.ToString(), .Content))
        End With
        If Not String.IsNullOrEmpty(NewId) Then
            Return WebStartup.RespondJson(ctx, $"{{""success"":true,""id"":""{NewId}""}}")
        Else
            Return WebStartup.RespondJson(ctx, "{""success"":false}")
        End If
    End Function

    <WebApi("/upload")>
    Private Function Upload(ctx As HttpContext) As Task
        Return Uploader.Progress(ctx)
    End Function

    <WebApi("/volume", HttpMethod.Post)>
    Private Function Volume(ctx As HttpContext) As Task
        If Not Settings.Settings.AllowVolumeUpdate Then Return WebStartup.RespondJson(ctx, "{""success"":false}")

        Dim Request As String = WebStartup.GetRequestBody(ctx)

        Dim v As RequestVolume = JsonConvert.DeserializeObject(Of RequestVolume)(Request)
        If v Is Nothing OrElse Not [Enum].IsDefined(GetType(FormUtils.VolumeAction), v.VolumeAction) Then
            Return WebStartup.RespondJson(ctx, "{""success"":false}")
        End If

        K.UpdateVolume(DirectCast(v.VolumeAction, FormUtils.VolumeAction), v.VolumeValue)

        Return WebStartup.RespondJson(ctx, "{""success"":true}")
    End Function

End Class
