Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Public Class ConsoleUtils

    Private Enum CtrlType
        CTRL_C_EVENT = 0          ' Ctrl+C
        CTRL_BREAK_EVENT = 1      ' Ctrl+Break
        CTRL_CLOSE_EVENT = 2      ' 关闭控制台窗口 (X按钮)
        CTRL_LOGOFF_EVENT = 5     ' 用户注销
        CTRL_SHUTDOWN_EVENT = 6   ' 系统关机
    End Enum

    ' 委托类型定义
    Private Delegate Function HandlerRoutine(ctrlType As CtrlType) As Boolean

    ' 导入Win32 API
    <DllImport("kernel32.dll")>
    Private Shared Function SetConsoleCtrlHandler(handler As HandlerRoutine, add As Boolean) As Boolean
    End Function

    ''' <summary>
    ''' 注册退出事件
    ''' </summary>
    ''' <param name="ExitCallback">退出回调</param>
    Public Shared Sub RegisterExit(ExitCallback As MethodInvoker)
        SetConsoleCtrlHandler(Function(ctrlType As CtrlType) As Boolean
                                  Select Case ctrlType
                                      Case CtrlType.CTRL_CLOSE_EVENT, CtrlType.CTRL_LOGOFF_EVENT, CtrlType.CTRL_SHUTDOWN_EVENT
                                          '无法阻止的操作
                                          ExitCallback.Invoke()
                                      Case CtrlType.CTRL_C_EVENT, CtrlType.CTRL_BREAK_EVENT
                                          '可以阻止的操作
                                          ExitCallback.Invoke()
                                          Return True
                                  End Select

                                  Return False
                              End Function, True)
    End Sub

End Class
