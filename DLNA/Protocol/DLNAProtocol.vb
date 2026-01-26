Imports System.Collections.Concurrent
Imports CefSharp.DevTools.CSS

Namespace DLNA.Protocol

    Public Class DLNAProtocol
        Implements IDisposable

        ''' <summary>
        ''' AVTransport服务
        ''' </summary>
        Friend WithEvents AVTransportService As AVTransport

        ''' <summary>
        ''' RenderingControl服务
        ''' </summary>
        Friend WithEvents RenderingControlService As RenderingControl

        ''' <summary>
        ''' ConnectionManager服务
        ''' </summary>
        Friend WithEvents ConnectionManagerService As ConnectionManager

        Private Running As Boolean

        Private BroadcastTask As Task

        Friend Settings As SettingContainer

        Friend DLNA As DLNA

        ''' <summary>
        ''' 初始化DLNA协议管理器
        ''' </summary>
        ''' <param name="DLNA">DLNA协议栈</param>
        ''' <param name="Settings">配置容器</param>
        Public Sub New(DLNA As DLNA, Settings As SettingContainer)
            Me.DLNA = DLNA
            Me.Settings = Settings

            AVTransportService = New AVTransport(Me)
            RenderingControlService = New RenderingControl(Me)
            ConnectionManagerService = New ConnectionManager(Me)

            Running = True
            BroadcastTask = Task.Run(AddressOf BroadcastLoop)
        End Sub

        '广播循环
        Private Sub BroadcastLoop()
            Dim i As Long
            While Running
                i = 0
                While i < Settings.Settings.DLNA.EventInterval
                    Threading.Thread.Sleep(10)
                    i += 10

                    If Not Running Then Return
                End While

                '发送事件广播
                AVTransportService.Broadcast()
                RenderingControlService.Broadcast()
                ConnectionManagerService.Broadcast()
            End While
        End Sub

        ''' <summary>
        ''' 销毁资源
        ''' </summary>
        Public Sub Dispose() Implements IDisposable.Dispose
            Running = False

            With BroadcastTask
                If Not .IsCompleted Then .Wait()
                .Dispose()
            End With
        End Sub

    End Class

End Namespace
