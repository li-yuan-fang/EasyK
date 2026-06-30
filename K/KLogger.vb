''' <summary>
''' 日志类型
''' </summary>
Public Enum Logging
    ''' <summary>
    ''' 信息
    ''' </summary>
    Info = 10

    ''' <summary>
    ''' 警告
    ''' </summary>
    Warn = 6

    ''' <summary>
    ''' 错误
    ''' </summary>
    [Error] = 12

    ''' <summary>
    ''' 调试信息
    ''' </summary>
    Debug = 9
End Enum

''' <summary>
''' 日志系统
''' </summary>
Public Class KLogger
    Inherits IO.TextWriter

    Private ReadOnly Inner As IO.TextWriter

    Private ReadOnly Settings As SettingContainer

    Public Overrides ReadOnly Property Encoding As Text.Encoding
        Get
            Return Inner.Encoding
        End Get
    End Property

#Region "接管输出"

    Public Overrides Sub Write(format As String, arg0 As Object)
        SyncLock Inner
            Inner.Write(format, arg0)
        End SyncLock
    End Sub

    Public Overrides Sub Write(format As String, arg0 As Object, arg1 As Object)
        SyncLock Inner
            Inner.Write(format, arg0, arg1)
        End SyncLock
    End Sub

    Public Overrides Sub Write(format As String, arg0 As Object, arg1 As Object, arg2 As Object)
        SyncLock Inner
            Inner.Write(format, arg0, arg1, arg2)
        End SyncLock
    End Sub

    Public Overrides Sub Write(format As String, ParamArray arg() As Object)
        SyncLock Inner
            Inner.Write(format, arg)
        End SyncLock
    End Sub

    Public Overrides Sub Write(value As String)
        SyncLock Inner
            Inner.Write(value)
        End SyncLock
    End Sub

    Public Overrides Sub WriteLine()
        SyncLock Inner
            Inner.WriteLine()
        End SyncLock
    End Sub

    Public Overrides Sub WriteLine(format As String, arg0 As Object)
        SyncLock Inner
            Inner.WriteLine(format, arg0)
        End SyncLock
    End Sub

    Public Overrides Sub WriteLine(format As String, arg0 As Object, arg1 As Object)
        SyncLock Inner
            Inner.WriteLine(format, arg0, arg1)
        End SyncLock
    End Sub

    Public Overrides Sub WriteLine(format As String, arg0 As Object, arg1 As Object, arg2 As Object)
        SyncLock Inner
            Inner.WriteLine(format, arg0, arg1, arg2)
        End SyncLock
    End Sub

    Public Overrides Sub WriteLine(format As String, ParamArray arg() As Object)
        SyncLock Inner
            Inner.WriteLine(format, arg)
        End SyncLock
    End Sub

    Public Overrides Sub WriteLine(value As String)
        SyncLock Inner
            Inner.WriteLine(value)
        End SyncLock
    End Sub

#End Region

    '打印信息
    Private Sub Print(Type As Logging, Content As String)
        SyncLock Inner
            With Inner
                Console.ForegroundColor = ConsoleColor.White
                .Write($"[{Now:HH:mm:ss}/")

                Console.ForegroundColor = Type
                .Write([Enum].GetName(GetType(Logging), Type).ToUpper())

                Console.ForegroundColor = ConsoleColor.White
                .WriteLine($"] {Content}")
            End With

        End SyncLock
    End Sub

    ''' <summary>
    ''' 打印信息
    ''' </summary>
    ''' <param name="Content">内容</param>
    Public Sub Info(Content As String)
        Print(Logging.Info, Content)
    End Sub

    ''' <summary>
    ''' 打印信息
    ''' </summary>
    ''' <param name="Content">内容</param>
    ''' <param name="Params">参数</param>
    Public Sub Info(Content As String, ParamArray Params() As String)
        Print(Logging.Info, String.Format(Content, Params))
    End Sub

    ''' <summary>
    ''' 打印警告
    ''' </summary>
    ''' <param name="Content">内容</param>
    Public Sub Warn(Content As String)
        Print(Logging.Warn, Content)
    End Sub

    ''' <summary>
    ''' 打印警告
    ''' </summary>
    ''' <param name="Content">内容</param>
    ''' <param name="Params">参数</param>
    Public Sub Warn(Content As String, ParamArray Params() As String)
        Print(Logging.Warn, String.Format(Content, Params))
    End Sub

    ''' <summary>
    ''' 打印错误
    ''' </summary>
    ''' <param name="Content">内容</param>
    Public Sub [Error](Content As String)
        Print(Logging.Error, Content)
    End Sub

    ''' <summary>
    ''' 打印错误
    ''' </summary>
    ''' <param name="Content">内容</param>
    ''' <param name="Params">参数</param>
    Public Sub [Error](Content As String, ParamArray Params() As String)
        Print(Logging.Error, String.Format(Content, Params))
    End Sub

    ''' <summary>
    ''' 打印调试信息
    ''' </summary>
    ''' <param name="Content">内容</param>
    Public Sub Debug(Content As String)
        If Not Settings.Settings.DebugMode Then Return

        Print(Logging.Debug, Content)
    End Sub

    ''' <summary>
    ''' 打印调试信息
    ''' </summary>
    ''' <param name="Content">内容</param>
    ''' <param name="Params">参数</param>
    Public Sub Debug(Content As String, ParamArray Params() As String)
        Print(Logging.Debug, String.Format(Content, Params))
    End Sub

    ''' <summary>
    ''' 打印多行信息
    ''' </summary>
    ''' <param name="Lines">行数</param>
    Public Sub PrintOriginalLines(ParamArray Lines() As String)
        SyncLock Inner
            For Each Line In Lines
                Inner.WriteLine(Line)
            Next
        End SyncLock
    End Sub

    ''' <summary>
    ''' 初始化日志系统
    ''' </summary>
    ''' <param name="Settings">配置容器</param>

    Public Sub New(Settings As SettingContainer)
        Me.Settings = Settings

        Inner = Console.Out
        Console.SetOut(Me)
    End Sub

End Class