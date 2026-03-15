Imports System.Runtime.InteropServices

Public Class ConsoleUtils

    ''' <summary>
    ''' 操作类型
    ''' </summary>
    Public Enum CtrlType
        ''' <summary>
        ''' Ctrl+C
        ''' </summary>
        CTRL_C_EVENT = 0

        ''' <summary>
        ''' Ctrl+Break
        ''' </summary>
        CTRL_BREAK_EVENT = 1

        ''' <summary>
        ''' 关闭控制台窗口 (X按钮)
        ''' </summary>
        CTRL_CLOSE_EVENT = 2

        ''' <summary>
        ''' 用户注销
        ''' </summary>
        CTRL_LOGOFF_EVENT = 5

        ''' <summary>
        ''' 系统关机
        ''' </summary>
        CTRL_SHUTDOWN_EVENT = 6
    End Enum

    ''' <summary>
    ''' 控制台关闭回调
    ''' </summary>
    ''' <param name="ctrlType">操作类型</param>
    ''' <returns></returns>
    Public Delegate Function HandlerRoutine(ctrlType As CtrlType) As Boolean

    ''' <summary>
    ''' 设置控制台关闭回调
    ''' </summary>
    ''' <param name="handler">控制台关闭回调</param>
    ''' <param name="add">注册/注销</param>
    ''' <returns></returns>
    <DllImport("kernel32.dll")>
    Public Shared Function SetConsoleCtrlHandler(handler As HandlerRoutine, add As Boolean) As Boolean
    End Function

End Class
