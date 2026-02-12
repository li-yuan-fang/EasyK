Imports System.Collections.Concurrent
Imports System.Reflection
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.AspNetCore.Http
Imports Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http
Imports Microsoft.Extensions.DependencyInjection



<AttributeUsage(AttributeTargets.Method)>
Public Class WebApi
    Inherits Attribute

    ''' <summary>
    ''' 匹配前缀
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Prefix As String

    ''' <summary>
    ''' 匹配方法
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Method As HttpMethod

    ''' <summary>
    ''' 定义WebAPI
    ''' </summary>
    ''' <param name="Prefix">匹配前缀</param>
    Public Sub New(Prefix As String)
        Me.New(Prefix, HttpMethod.None)
    End Sub

    ''' <summary>
    ''' 定义WebAPI
    ''' </summary>
    ''' <param name="Prefix">匹配前缀</param>
    ''' <param name="Method">匹配方法</param>
    Public Sub New(Prefix As String, Method As HttpMethod)
        Me.Prefix = Prefix
        Me.Method = Method
    End Sub

End Class

Public Class WebStartup
    Implements IStartup

    ''' <summary>
    ''' 获取或设置静态网页根目录
    ''' </summary>
    Public Shared WebRoot As String

    ''' <summary>
    ''' 获取或设置默认首页
    ''' </summary>
    Public Shared WebDefault As String = "index.html"

    ''' <summary>
    ''' 响应方式匹配表
    ''' </summary>
    Public Shared ReadOnly WebMap As New ConcurrentDictionary(Of String, RequestDelegate)()

    Public Shared RangeRegex As New Text.RegularExpressions.Regex("^(bytes\=)?\d*\-\d*$")

    Private Shared UTF8 As New Text.UTF8Encoding(False)

    Public Sub Configure(app As IApplicationBuilder) Implements IStartup.Configure
        app.Run(AddressOf HttpResponse)
    End Sub

    Public Function ConfigureServices(services As IServiceCollection) As IServiceProvider Implements IStartup.ConfigureServices
        Return services.BuildServiceProvider()
    End Function

    Private Shared Function CombinePath(Root As String, Uri As String) As String
        Dim Base As String = Root.Replace("/", "\")
        If Base.Length > 0 Then
            For i = Base.Length - 1 To 0 Step -1
                If Base(i) <> "\" Then
                    Base = Base.Substring(0, i + 1)
                    Exit For
                End If
            Next
        End If

        Return Base & Uri.Replace("/", "\")
    End Function

    Private Function HttpResponse(ctx As HttpContext) As Task
        Dim Request As HttpRequest = ctx.Request
        Dim RemotePath As String = Request.Path

        '匹配Map
        For Each api In WebMap
            If RemotePath.ToLower().StartsWith(api.Key.ToLower()) Then Return api.Value(ctx)
        Next

        '处理文件请求
        Dim Response As HttpResponse = ctx.Response

        '文件检查
        Dim i As Integer = RemotePath.IndexOf("?")
        Dim j As Integer = RemotePath.IndexOf("#")
        If i >= 0 AndAlso j >= 0 Then
            RemotePath = RemotePath.Substring(0, Math.Min(i, j))
        ElseIf i >= 0 Then
            RemotePath = RemotePath.Substring(0, i)
        ElseIf j >= 0 Then
            RemotePath = RemotePath.Substring(0, j)
        End If
        If RemotePath.EndsWith("/") Then RemotePath &= WebDefault

        Dim PhysicsPath As String = CombinePath(WebRoot, RemotePath)

        If Not IO.File.Exists(PhysicsPath) Then
            If Settings.Settings.DebugMode Then Console.WriteLine("{0} - {1} {2} 404", RemotePath, Request.Method, PhysicsPath)
            Return RespondStatusOnly(ctx, 404)
        End If

        If ctx.Request.Method.ToUpper() = "HEAD" Then
            Return RespondStatusOnly(ctx, 200)
        ElseIf ctx.Request.Method.ToUpper() <> "GET" Then
            Return RespondStatusOnly(ctx, 403)
        End If

        Dim Range As String = Request.Headers().Item("Range")
        Dim Status As Integer = 200
        Dim Offset As Integer = 0

        Dim ContentRange As String = Nothing
        Dim Info As New IO.FileInfo(PhysicsPath)
        Dim Length As Integer = Info.Length

        If Not String.IsNullOrEmpty(Range) Then
            If RangeRegex.IsMatch(Range) Then
                Status = 206

                Range = Range.Replace("bytes=", "")

                Dim Ranges As String() = Strings.Split(Range, "-")
                Dim Invalid1 = String.IsNullOrEmpty(Ranges(0))
                Dim Invalid2 = String.IsNullOrEmpty(Ranges(1))
                If Ranges.Length < 2 OrElse (Invalid1 AndAlso Invalid2) Then Return RespondStatusOnly(ctx, 416)

                If Not Invalid1 Then
                    Offset = Val(Range(0))

                    '超出范围
                    If Offset >= Length Then Return RespondStatusOnly(ctx, 416)

                    If Not Invalid2 Then
                        Length = Val(Range(1)) - Offset + 1
                    Else
                        Length -= Offset
                    End If

                    '超出范围
                    If Length <= 0 Then Return RespondStatusOnly(ctx, 416)
                Else
                    Offset = Length - Val(Range(1))

                    '超出范围
                    If Offset < 0 Then Return RespondStatusOnly(ctx, 416)

                    Length = Val(Range(1))
                End If

                ContentRange = $"bytes {Range(0)}-{Range(1)}/{Info.Length}"
            Else
                Return RespondStatusOnly(ctx, 416)
            End If
        End If

        Info = Nothing

        With Response
            .StatusCode = Status
            .ContentType = Web.MimeMapping.GetMimeMapping(PhysicsPath)
            .ContentLength = Length

            .Headers().Item("Accept-Ranges") = "bytes"
            If Status = 206 Then .Headers().Item("Content-Range") = ContentRange

            If .ContentType.ToLower().Contains("javascript") Then
                .Headers().Item("Content-Range") = ContentRange
            End If

            Dim Buffer(Length - 1) As Byte
            Try
                Using stream As New IO.FileStream(PhysicsPath, IO.FileMode.Open, IO.FileAccess.Read)
                    stream.Read(Buffer, Offset, Length)
                End Using
            Catch
                Return RespondStatusOnly(ctx, 500)
            End Try

            Return Response.Body.WriteAsync(Buffer, 0, Length)
        End With
    End Function

    ''' <summary>
    ''' 安全添加响应头
    ''' </summary>
    ''' <param name="Headers">响应头</param>
    ''' <param name="Key">关键字</param>
    ''' <param name="Value">值</param>
    Public Shared Sub AddHeaderSafe(Headers As IHeaderDictionary, Key As String, Value As String)
        If Headers.ContainsKey(Key) Then
            Headers(Key) = Value
        Else
            Headers.Add(Key, Value)
        End If
    End Sub

    ''' <summary>
    ''' 注册Web API调用
    ''' </summary>
    ''' <param name="Instance">响应器实例</param>
    Public Shared Sub Register(Instance As Object)
        Register(Instance, vbNullString)
    End Sub

    ''' <summary>
    ''' 注册Web API调用
    ''' </summary>
    ''' <param name="Instance">响应器实例</param>
    ''' <param name="Prefix">匹配前缀</param>
    Public Shared Sub Register(Instance As Object, Prefix As String)
        If Instance Is Nothing Then Return

        With WebMap
            For Each m As MethodInfo In Instance.GetType.GetMethods(BindingFlags.NonPublic Or BindingFlags.Public Or BindingFlags.Instance)
                Dim api As WebApi = m.GetCustomAttribute(Of WebApi)
                If api IsNot Nothing Then
                    .TryAdd($"{Prefix}{api.Prefix}", Function(ctx)
                                                         If api.Method <> HttpMethod.None AndAlso
                                                                    ctx.Request.Method.ToUpper <>
                                                                    [Enum].GetName(GetType(HttpMethod), api.Method).ToUpper() Then
                                                             Return RespondStatusOnly(ctx)
                                                         End If

                                                         Return m.Invoke(Instance, {ctx})
                                                     End Function)
                End If
            Next
        End With
    End Sub

    ''' <summary>
    ''' 注册Web API调用
    ''' </summary>
    ''' <param name="Instance">响应器实例</param>
    ''' <param name="Settings">配置容器</param>
    Public Shared Sub Register(Instance As Object, Settings As SettingContainer)
        Register(Instance, Settings, vbNullString)
    End Sub

    ''' <summary>
    ''' 注册Web API调用
    ''' </summary>
    ''' <param name="Instance">响应器实例</param>
    ''' <param name="Settings">配置容器</param>
    ''' <param name="Prefix">匹配前缀</param>
    Public Shared Sub Register(Instance As Object, Settings As SettingContainer, Prefix As String)
        If Instance Is Nothing Then Return

        With WebMap
            For Each m As MethodInfo In Instance.GetType.GetMethods(BindingFlags.NonPublic Or BindingFlags.Public Or BindingFlags.Instance)
                Dim api As WebApi = m.GetCustomAttribute(Of WebApi)
                If api IsNot Nothing Then
                    .TryAdd($"{Prefix}{api.Prefix}", Function(ctx)
                                                         If api.Method <> HttpMethod.None AndAlso
                                                                    ctx.Request.Method.ToUpper <>
                                                                    [Enum].GetName(GetType(HttpMethod), api.Method).ToUpper() Then
                                                             Return RespondStatusOnly(ctx)
                                                         End If

                                                         If Not String.IsNullOrEmpty(Settings.Settings.Web.PassKey) AndAlso
                                                                Not Settings.Settings.Web.PassKey.Equals(ctx.Request.Cookies.Item("key")) Then
                                                             Return RespondStatusOnly(ctx)
                                                         End If

                                                         Return m.Invoke(Instance, {ctx})
                                                     End Function)
                End If
            Next
        End With
    End Sub

    ''' <summary>
    ''' 获取请求正文
    ''' </summary>
    ''' <param name="ctx">上下文</param>
    ''' <returns></returns>
    Public Shared Function GetRequestBody(ctx As HttpContext) As String
        Using reader As New IO.StreamReader(ctx.Request.Body)
            Return reader.ReadToEnd()
        End Using
    End Function

    ''' <summary>
    ''' 发送响应码
    ''' </summary>
    ''' <param name="ctx">上下文</param>
    ''' <param name="Code">响应码</param>
    ''' <returns></returns>
    Public Shared Function RespondStatusOnly(ctx As HttpContext, Optional Code As Integer = 403) As Task
        With ctx.Response
            .StatusCode = Code
            .ContentLength = 0
            Return .WriteAsync("")
        End With
    End Function

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="ctx">上下文</param>
    ''' <param name="str">文本内容</param>
    ''' <param name="type">Content-Type头</param>
    ''' <returns></returns>
    Public Shared Function RespondText(ctx As HttpContext, str As String, Optional type As String = "text/plain") As Task
        Dim Buffer() As Byte = UTF8.GetBytes(str)

        With ctx.Response
            .StatusCode = 200
            .ContentType = type
            .ContentLength = Buffer.Length
            Return .Body.WriteAsync(Buffer, 0, .ContentLength)
        End With
    End Function

    ''' <summary>
    ''' 发送Json响应
    ''' </summary>
    ''' <param name="ctx">上下文</param>
    ''' <param name="json">Json内容</param>
    ''' <returns></returns>
    Public Shared Function RespondJson(ctx As HttpContext, json As String) As Task
        Return RespondText(ctx, json, "application/json")
    End Function

End Class
