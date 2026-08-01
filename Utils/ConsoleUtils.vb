Imports System.IO
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
    ''' 标准流
    ''' </summary>
    Public Enum StandardStream As Integer
        ''' <summary>
        ''' 标准输入流
        ''' </summary>
        STD_INPUT_HANDLE = -10

        ''' <summary>
        ''' 标准输出流
        ''' </summary>
        STD_OUTPUT_HANDLE = -11

        ''' <summary>
        ''' 标准错误流
        ''' </summary>
        STD_ERROR_HANDLE = -12
    End Enum

    ''' <summary>
    ''' 控制台输入流模式
    ''' </summary>
    Public Enum ConsoleInputMode As UInteger
        ENABLE_PROCESSED_INPUT = &H1
        ENABLE_LINE_INPUT = &H2
        ENABLE_ECHO_INPUT = &H4
        ENABLE_WINDOW_INPUT = &H8
        ENABLE_MOUSE_INPUT = &H10
        ENABLE_INSERT_MODE = &H20
        ENABLE_QUICK_EDIT_MODE = &H40
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

    ''' <summary>
    ''' 设置控制台模式
    ''' </summary>
    ''' <param name="hConsoleHandle">控制台句柄</param>
    ''' <param name="dwMode">控制台模式</param>
    ''' <returns></returns>
    <DllImport("kernel32.dll", SetLastError:=True)>
    Public Shared Function SetConsoleMode(hConsoleHandle As IntPtr, dwMode As UInteger) As Boolean
    End Function

    ''' <summary>
    ''' 获取控制台模式
    ''' </summary>
    ''' <param name="hConsoleHandle">控制台句柄</param>
    ''' <param name="lpMode">控制台模式</param>
    ''' <returns></returns>
    <DllImport("kernel32.dll", SetLastError:=True)>
    Public Shared Function GetConsoleMode(hConsoleHandle As IntPtr, ByRef lpMode As UInteger) As Boolean
    End Function

    ''' <summary>
    ''' 获取标准句柄
    ''' </summary>
    ''' <param name="nStdHandle"></param>
    ''' <returns></returns>
    <DllImport("kernel32.dll")>
    Public Shared Function GetStdHandle(nStdHandle As StandardStream) As IntPtr
    End Function

End Class
