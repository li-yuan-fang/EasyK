Imports System.Runtime.InteropServices

Public Class KeyboardUtils

    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Private Shared Function SendMessage(hWnd As IntPtr, Msg As Integer, wParam As Integer, lParam As Integer) As Integer
    End Function

    Private Const WM_KEYDOWN As Integer = &H100
    Private Const WM_KEYUP As Integer = &H101

    ''' <summary>
    ''' 模拟按键
    ''' </summary>
    ''' <param name="Handle">窗体句柄</param>
    ''' <param name="KeyCode">按键码</param>
    Public Shared Sub SendKey(Handle As IntPtr, KeyCode As Integer)
        SendMessage(Handle, WM_KEYDOWN, KeyCode, 0)
        SendMessage(Handle, WM_KEYUP, KeyCode, 0)
    End Sub

End Class
