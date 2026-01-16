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
    ''' 初始化Web服务端
    ''' </summary>
    ''' <param name="Port">端口</param>
    Public Sub New(Port As Integer, DebugMode As Boolean)
        Me.Port = Port

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
        Host.RunAsync()
    End Sub

    ''' <summary>
    ''' 销毁资源
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        Host.Dispose()
    End Sub

End Class
