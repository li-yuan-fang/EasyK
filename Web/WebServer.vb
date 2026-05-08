Imports System.Net
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.Extensions.Logging

Public Class WebServer
    Implements IDisposable

    Private Host As IWebHost

    ''' <summary>
    ''' 获取服务器端口
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Port As Integer

    ''' <summary>
    ''' 服务器出错事件
    ''' </summary>
    ''' <param name="Exceptions">错误</param>
    Public Event OnErrorTrigger(Exceptions As Exception())

    ''' <summary>
    ''' 初始化Web服务端
    ''' </summary>
    ''' <param name="Port">端口</param>
    Public Sub New(Port As Integer, DebugMode As Boolean)
        Me.Port = Math.Min(Math.Max(Port, 1), 65535)

        Dim builder As New WebHostBuilder()

        With builder
            .UseKestrel(Sub(options)
                            options.Listen(IPAddress.Any, Port)
                        End Sub)
            .UseStartup(Of WebStartup)()
            .ConfigureLogging(Sub(Logging)
                                  If Not DebugMode Then Logging.ClearProviders()
                              End Sub)
        End With

        Host = builder.Build()
        Dim ServerTask = Host.RunAsync()
        ServerTask.ContinueWith(Sub()
                                    '错误检测
                                    If ServerTask.Exception IsNot Nothing Then
                                        RaiseEvent OnErrorTrigger(ServerTask.Exception.InnerExceptions.ToArray())
                                    End If
                                End Sub)
    End Sub

    ''' <summary>
    ''' 销毁资源
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        Host.Dispose()
    End Sub

End Class
