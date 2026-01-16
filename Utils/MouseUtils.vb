Imports System.Runtime.InteropServices

Public Class MouseUtils

    <DllImport("user32.dll")>
    Private Shared Sub SetCursorPos(ByVal x As Integer, ByVal y As Integer)
    End Sub


    <DllImport("user32.dll")>
    Private Shared Function GetCursorPos(ByRef Point As LPPOINT) As UInteger
    End Function


    <DllImport("user32.dll", EntryPoint:="mouse_event")>
    Private Shared Sub mouse_event(ByVal dwFlags As Integer, ByVal dx As Integer, ByVal dy As Integer, ByVal cButtons As Integer, ByVal dwExtraInfo As IntPtr)
    End Sub

    Private Const MOUSEEVENTF_LEFTDOWN As UInteger = &H2
    Private Const MOUSEEVENTF_LEFTUP As UInteger = &H4

    Private Structure LPPOINT
        Public x As Integer
        Public y As Integer
    End Structure

    ''' <summary>
    ''' 模拟鼠标点击
    ''' </summary>
    ''' <param name="x"></param>
    ''' <param name="y"></param>
    Public Shared Sub MouseClick(x As Integer, y As Integer)
        Dim Original As LPPOINT
        GetCursorPos(Original)

        SetCursorPos(x, y)

        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero)
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero)

        SetCursorPos(Original.x, Original.y)
    End Sub

End Class
