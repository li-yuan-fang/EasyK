Imports System.Text
Imports System.Threading
Imports Microsoft.AspNetCore.Http

Namespace KWeb.State

    Friend Class StateSession
        Implements IDisposable

        Private Const Packet_Keepalive As String = ": keep alive" & vbLf & vbLf

        Private ReadOnly Manager As StateManager

        Private ReadOnly Settings As SettingContainer

        Private ReadOnly Queue As New LinkedList(Of PlayerEvent)

        Private ReadOnly KeepAliveSlim As New ManualResetEvent(False)

        Private Cancelled As Boolean = False

        ''' <summary>
        ''' 初始化会话
        ''' </summary>
        ''' <param name="Manager">状态管理器</param>
        ''' <param name="Settings">配置容器</param>
        Public Sub New(Manager As StateManager, Settings As SettingContainer)
            Me.Manager = Manager
            Me.Settings = Settings

            Manager.Register(Me)
        End Sub

        ''' <summary>
        ''' 提交事件
        ''' </summary>
        ''' <param name="e">事件类型</param>
        Public Sub Commit(e As PlayerEvent)
            SyncLock Queue
                With Queue
                    If .Count = 0 Then
                        .AddLast(e)
                    Else
                        If .Last.Value.Type <> e.Type Then .AddLast(e)
                    End If
                End With
            End SyncLock

            KeepAliveSlim.Set()
        End Sub

        Private Shared Async Function WriteSSEFrameAsync(ctx As HttpContext, frame As String) As Task
            Dim bytes As Byte() = Encoding.UTF8.GetBytes(frame)

            With ctx
                Dim Body = .Response.Body
                Await Body.WriteAsync(bytes, 0, bytes.Length, .RequestAborted)
                Await Body.FlushAsync(.RequestAborted)
            End With
        End Function

        ''' <summary>
        ''' 响应SSE请求
        ''' </summary>
        ''' <param name="ctx">HTTP上下文</param>
        ''' <returns></returns>
        Public Async Function SSEHandler(ctx As HttpContext) As Task
            With ctx.Response
                .StatusCode = StatusCodes.Status200OK
                .ContentType = "text/event-stream; charset=utf-8"
                .Headers("Cache-Control") = "no-store"
                .Headers("Vary") = "Accept"
            End With

            Try
                Await WriteSSEFrameAsync(ctx, ": connected" & vbLf & vbLf)

                Dim Token = ctx.RequestAborted

                While Not Token.IsCancellationRequested
                    Dim Result As Boolean
                    With KeepAliveSlim
                        Result = .WaitOne(Settings.Settings.Web.StateTimeout)

                        If Cancelled Then Return

                        .Reset()
                    End With

                    If Result Then
                        '外部触发
                        Dim Events As List(Of PlayerEvent)
                        SyncLock Queue
                            With Queue
                                If .Count = 0 Then Continue While

                                Events = New List(Of PlayerEvent)(Queue)
                                .Clear()
                            End With
                        End SyncLock

                        Dim Builder As New StringBuilder
                        For Each e As PlayerEvent In Events
                            Builder.Append(e.GetResponse())
                        Next

                        Await WriteSSEFrameAsync(ctx, Builder.ToString())
                    Else
                        '超时
                        Await WriteSSEFrameAsync(ctx, Packet_Keepalive)
                    End If
                End While
            Catch ex As Exception
                Dim Remote As String = ctx.Connection.RemoteIpAddress.ToString()
                If ctx.RequestAborted.IsCancellationRequested Then
                    Logger.Debug("SSE连接断开: {0} - {1}", Remote, ex.Message)
                Else
                    Logger.Debug("SSE连接意外断开: {0} - {1}", Remote, ex.Message)
                End If
            Finally
                If Not Cancelled Then Dispose()
            End Try
        End Function

        ''' <summary>
        ''' 销毁资源
        ''' </summary>
        Public Sub Dispose() Implements IDisposable.Dispose
            Manager.Unregister(Me)

            Cancelled = True
            KeepAliveSlim.Set()
        End Sub

    End Class

End Namespace