
Imports System.Collections.Concurrent
Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.AspNetCore.Http
Imports Microsoft.Extensions.Primitives
Imports Newtonsoft.Json
Imports EasyK.KWeb.Serializable

Namespace KWeb.Upload

    Public Class UploadManager
        Implements IDisposable

        Private Shared ReadOnly NumericRegex As New Text.RegularExpressions.Regex("^\d+$")

        'Tip.此处长度为64 所以单个字符重复次数是63
        Private Shared ReadOnly HashRegex As New Text.RegularExpressions.Regex("^[A-Za-z\d].{63}$")

        '上传会话
        Private ReadOnly Sessions As New ConcurrentDictionary(Of String, UploadSession)()

        '清理任务
        Private ReadOnly Cleaner As Task

        '已占用的文件列表(上传中的文件)
        Private ReadOnly Occupied As New List(Of String)

        '已上传的文件(Id, Hash)
        Private ReadOnly Uploaded As New Dictionary(Of String, String)

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
                Logger.Debug("{0}> 完成状态: {1}", Name, Session.IsCompleted().ToString().ToLower())

                If Session.IsCompleted() Then
                    '上传完成
                    Dim Hash As String = Session.ComputeSHA256()

                    If String.IsNullOrEmpty(Hash) Then
                        Logger.Warn("会话 #{0} 上传文件时遭遇严重错误 已取消上传")

                        Sessions.TryRemove(Name, Session)
                        If Session IsNot Nothing Then Session.Dispose()

                        Return WebStartup.RespondJson(ctx, "{""busy"":false}")
                    End If

                    Task.Run(Sub()
                                 SyncLock Uploaded
                                     Uploaded.Add(Session.Id, Hash)
                                 End SyncLock
                             End Sub)

                    Return WebStartup.RespondJson(ctx, $"{{""busy"":true,""id"":""{Session.Id}"",""complete"":true,""hash"":""{Hash}""}}")
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
            Dim Request As RequestCheck = JsonUtils.SafeDeserializeObject(Of RequestCheck)(WebStartup.GetRequestBody(ctx))
            If Request Is Nothing OrElse Request.Size <= 0 Then Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status400BadRequest)

            '检查策略
            If Settings.Settings.Web.Upload.LessUploadPolicy Then
                Dim Suspicious As New List(Of String)

                SyncLock Uploaded
                    For Each p In Uploaded
                        If p.Value.ToLower() = Request.Hash.ToLower() Then Suspicious.Add(p.Key)
                    Next
                End SyncLock

                '检查是否发现可疑文件
                If Suspicious.Count > 0 Then
                    Dim Invalid As New List(Of String)
                    Dim Bypass As String = vbNullString

                    '检查可疑文件
                    For Each s In Suspicious
                        Dim Path As String = IO.Path.Combine(Application.StartupPath, Settings.Settings.TempFolder, s)
                        If Not IO.File.Exists(Path) Then
                            Invalid.Add(s)
                            Continue For
                        End If

                        Try
                            Dim fi As New IO.FileInfo(Path)
                            If fi.Length = Request.Size Then
                                '尺寸符合
                                Bypass = s
                                Exit For
                            End If
                        Catch ex As Exception
                            Invalid.Add(s)
                            Logger.Debug("获取缓存文件大小失败 - {0}", ex.Message)
                        End Try
                    Next

                    '清理
                    If Invalid.Count > 0 Then
                        SyncLock Uploaded
                            For Each i In Invalid
                                Uploaded.Remove(i)
                            Next
                        End SyncLock
                    End If

                    If Not String.IsNullOrEmpty(Bypass) Then
                        '跳过
                        Return WebStartup.RespondJson(ctx, $"{{""id"":""{Bypass}"",""chunk"":{Settings.Settings.Web.Upload.ChunkSize},""bypass"":true}}")
                    End If
                End If
            End If

            '移除旧会话
            Dim Session As UploadSession = Nothing
            Sessions.TryRemove(Name, Session)
            If Session IsNot Nothing Then Session.Dispose()

            '检查文件尺寸
            Dim Max As Integer = Settings.Settings.Web.Upload.MaxUploadSize
            If Max >= 0 AndAlso Request.Size > Max Then Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status413PayloadTooLarge)

            '创建新会话
            Session = New UploadSession(Request.Size, Settings)
            Logger.Debug("{0}> 新会话 {1}", Name, Session.Id)

            If Sessions.TryAdd(Name, Session) Then
                Return WebStartup.RespondJson(ctx, $"{{""id"":""{Session.Id}"",""chunk"":{Settings.Settings.Web.Upload.ChunkSize},""bypass"":false}}")
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
                Logger.Debug("{0}> #{1} {2} {3} - {4}", Name, Index, Buffer.Length, Hash, LocalHash)

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

End Namespace
