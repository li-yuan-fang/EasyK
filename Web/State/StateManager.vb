Imports System.Collections.Concurrent
Imports Microsoft.AspNetCore.Http

Namespace KWeb.State

    ''' <summary>
    ''' 状态管理器
    ''' </summary>
    Public Class StateManager
        Implements IDisposable

        Private ReadOnly Sessions As New List(Of StateSession)()

        Private WithEvents K As EasyK

        Private WithEvents PlayerManager As KPlayerManager

        Private ReadOnly Settings As SettingContainer

        Private _State As PlayState

        ''' <summary>
        ''' 获取播放器状态
        ''' </summary>
        ''' <returns></returns>
        Friend ReadOnly Property State As PlayState
            Get
                Return _State
            End Get
        End Property

        ''' <summary>
        ''' 初始化状态管理器
        ''' </summary>
        ''' <param name="K"></param>
        ''' <param name="Settings">配置容器</param>
        Public Sub New(K As EasyK, Settings As SettingContainer)
            Me.K = K
            Me.Settings = Settings
            PlayerManager = K.PlayerManager

            _State = PlayState.Stopped
        End Sub

        Friend Sub Register(Session As StateSession)
            SyncLock Sessions
                Sessions.Add(Session)
            End SyncLock
        End Sub

        Friend Sub Unregister(Session As StateSession)
            SyncLock Sessions
                Sessions.Remove(Session)
            End SyncLock
        End Sub

        ''' <summary>
        ''' 处理Web请求
        ''' </summary>
        ''' <param name="ctx">HTTP上下文</param>
        ''' <returns></returns>
        Public Function WebHandler(ctx As HttpContext) As Task
            With ctx
                Dim Accept As String = .Request.Headers().Item("Accept")
                If String.IsNullOrEmpty(Accept) Then
                    With .Response
                        .Headers("Cache-Control") = "no-store"
                        .Headers("Vary") = "Accept"
                    End With

                    Return WebStartup.RespondJson(ctx, ServerState.GenerateJson(K, _State))
                End If

                ' TODO: Accept解析要优化
                Dim Accepts As String() = Strings.Split(Accept.ToLower(), ",")
                If Accepts.Any(Function(item) item.Trim().StartsWith("text/event-stream")) Then
                    Dim Session As New StateSession(Me, Settings)
                    Return Session.SSEHandler(ctx)
                End If

                If Accepts.Any(Function(item)
                                   Dim Trimmed As String = item.Trim()
                                   Return Trimmed.StartsWith("application/json") OrElse Trimmed.StartsWith("*/*")
                               End Function) Then
                    With .Response
                        .Headers("Cache-Control") = "no-store"
                        .Headers("Vary") = "Accept"
                    End With

                    Return WebStartup.RespondJson(ctx, ServerState.GenerateJson(K, _State))
                End If

                Return WebStartup.RespondStatusOnly(ctx, StatusCodes.Status406NotAcceptable)
            End With
        End Function

        ' TODO: 考虑立即快照并提交事件结果
        Private Sub Commit(e As PlayerEvent)
            Dim Saved As List(Of StateSession)
            SyncLock Sessions
                Saved = New List(Of StateSession)(Sessions)
            End SyncLock

            For Each Session As StateSession In Saved
                Session.Commit(e)
            Next
        End Sub

        Private Sub OnPlayerPlay(Type As EasyKType, Content As String) Handles K.OnPlayerPlay
            _State = PlayState.Stopped
            Commit(New PlayerPushEvent(K))
        End Sub

        Private Sub OnPlayerPaused(Type As EasyKType, Paused As Boolean) Handles K.OnPlayerPaused
            _State = If(Paused, PlayState.Paused, PlayState.Playing)
            Commit(New PlayerPlayingEvent(Me, K))
        End Sub

        Private Sub OnPlayerTerminated() Handles K.OnPlayerTerminated
            _State = PlayState.Stopped
            Commit(New PlayerPushEvent(K, True))
        End Sub

        Private Sub OnPlayerUpdate() Handles PlayerManager.OnPlayingUpdate
            Commit(New PlayerPlayingEvent(Me, K))
        End Sub

        Private Sub OnPlayerParsed() Handles PlayerManager.OnPlayerParsed
            If _State = PlayState.Stopped Then _State = PlayState.Playing
            Commit(New PlayerLoadedEvent(Me, K))
        End Sub

        ''' <summary>
        ''' 销毁资源
        ''' </summary>
        Public Sub Dispose() Implements IDisposable.Dispose
            Dim Saved As List(Of StateSession)
            SyncLock Sessions
                Saved = New List(Of StateSession)(Sessions)
                Sessions.Clear()
            End SyncLock

            For Each Session As StateSession In Saved
                Session.Dispose()
            Next
        End Sub

    End Class

End Namespace
