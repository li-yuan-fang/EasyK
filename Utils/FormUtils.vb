Imports System.Drawing
Imports System.Runtime.InteropServices
Imports System.Threading
Imports Microsoft.AspNetCore.Server.Kestrel

Public Class FormUtils

    <DllImport("user32.dll")>
    Public Shared Function GetParent(hWnd As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Public Shared Function GetWindow(hWnd As IntPtr, uCmd As UInteger) As IntPtr
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Public Shared Function FindWindow(lpClassName As String, lpWindowName As String) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Public Shared Function GetWindowThreadProcessId(hWnd As IntPtr, ByRef lpdwProcessId As UInteger) As UInteger
    End Function

    <DllImport("user32.dll")>
    Public Shared Function SetParent(hWndChild As IntPtr, hWndNewParent As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Public Shared Function SetWindowPos(hwnd As IntPtr, hWndInsertAfter As IntPtr, x As Integer, y As Integer, cx As Integer,
                                         cy As Integer, wFlags As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Public Shared Function SendMessage(hWnd As IntPtr, Msg As UInteger, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    Private Const Volume_Mute As Integer = &H80000
    Private Const Volume_Up As Integer = &HA0000
    Private Const Volume_Down As Integer = &H90000

    Private Const WM_APPCOMMAND As UInteger = &H319

    Private Const GW_HWNDNEXT As UInteger = 2

    Private Shared Function ProcIDFromWnd(ByVal hwnd As IntPtr) As UInteger
        Dim idProc As Integer
        GetWindowThreadProcessId(hwnd, idProc)
        Return idProc
    End Function

    Public Enum VolumeAction
        Mute = 0
        Up
        Down
    End Enum

    ''' <summary>
    ''' 获取窗体句柄(仅支持单窗体进程)
    ''' </summary>
    ''' <param name="PId">进程Id</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Shared Function GetFormHandle(ByVal PId As UInteger) As IntPtr
        Dim tempHwnd As IntPtr
        ' Grab the first window handle that Windows finds:
        tempHwnd = FindWindow(vbNullString, vbNullString)

        ' Loop until you find a match or there are no more window handles:
        Do Until tempHwnd = IntPtr.Zero
            ' Check if no parent for this window
            If IntPtr.Zero.Equals(GetParent(tempHwnd)) Then
                ' Check for PID match
                If PId = ProcIDFromWnd(tempHwnd) Then
                    ' Return found handle
                    Return tempHwnd
                End If
            End If

            ' Get the next window handle
            tempHwnd = GetWindow(tempHwnd, GW_HWNDNEXT)
        Loop
        Return IntPtr.Zero
    End Function

    ''' <summary>
    ''' 使子窗体匹配父窗体
    ''' </summary>
    ''' <param name="Parent">父窗体句柄</param>
    ''' <param name="Child">子窗体句柄</param>
    ''' <param name="UpdateParent">是否设置父子关系</param>
    Public Shared Sub SetChildFitParent(Parent As IntPtr, ParentSize As Size, Child As IntPtr, Optional UpdateParent As Boolean = True)
        If UpdateParent Then SetParent(Child, Parent)

        With ParentSize
            SetWindowPos(Child, -1, 0, 0, .Width, .Height, &H400)
        End With
    End Sub

    ''' <summary>
    ''' 调整系统音量
    ''' </summary>
    ''' <param name="Handle">窗体句柄</param>
    ''' <param name="Action">操作</param>
    Public Shared Sub UpdateVolume(Handle As IntPtr, Action As VolumeAction)
        Select Case Action
            Case VolumeAction.Mute
                SendMessage(Handle, WM_APPCOMMAND, Handle, New IntPtr(Volume_Mute))
            Case VolumeAction.Up
                SendMessage(Handle, WM_APPCOMMAND, Handle, New IntPtr(Volume_Up))
            Case VolumeAction.Down
                SendMessage(Handle, WM_APPCOMMAND, Handle, New IntPtr(Volume_Down))
        End Select
    End Sub

End Class
