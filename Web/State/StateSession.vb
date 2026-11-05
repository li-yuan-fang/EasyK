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

                KeepAliveSlim.Set()
            End SyncLock
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
                    Dim Signaled As Boolean = KeepAliveSlim.WaitOne(Settings.Settings.Web.StateTimeout)

                    If Cancelled OrElse Token.IsCancellationRequested Then Return

                    Dim PendingEvents As List(Of PlayerEvent)

                    SyncLock Queue
                        PendingEvents = New List(Of PlayerEvent)(Queue)
                        Queue.Clear()
                        KeepAliveSlim.Reset()
                    End SyncLock

                    If PendingEvents.Count > 0 Then
                        Dim Builder As New StringBuilder()

                        For Each PlayerEvent As PlayerEvent In PendingEvents
                            Builder.Append(PlayerEvent.GetResponse())
                        Next

                        Await WriteSSEFrameAsync(ctx, Builder.ToString())
                    ElseIf Not Signaled Then
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