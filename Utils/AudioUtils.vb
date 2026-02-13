Imports CefSharp.DevTools.CSS
Imports NAudio.CoreAudioApi

Public Class AudioUtils

    ''' <summary>
    ''' 设置系统音量
    ''' </summary>
    ''' <param name="volume">音量</param>
    Public Shared Sub SetSystemVolume(volume As Single)
        volume = Math.Max(0.0F, Math.Min(1.0F, volume))

        Using enumerator As New MMDeviceEnumerator()
            Using device As MMDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                device.AudioEndpointVolume.MasterVolumeLevelScalar = volume
            End Using
        End Using
    End Sub

    ''' <summary>
    ''' 获取系统音量
    ''' </summary>
    ''' <returns></returns>
    Public Shared Function GetSystemVolume() As Single
        Using enumerator As New MMDeviceEnumerator()
            Using device As MMDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                Return device.AudioEndpointVolume.MasterVolumeLevelScalar
            End Using
        End Using
    End Function

    ''' <summary>
    ''' 设置当前进程音量
    ''' </summary>
    ''' <param name="volume">音量</param>
    Public Shared Sub SetCurrentVolume(volume As Single)
        Using enumerator As New MMDeviceEnumerator()
            Using device As MMDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                Dim sessionManager = device.AudioSessionManager
                Dim sessions = sessionManager.Sessions

                For i As Integer = 0 To sessions.Count - 1
                    Dim session = sessions(i)
                    If session.GetProcessID = Process.GetCurrentProcess().Id Then
                        session.SimpleAudioVolume.Volume = volume
                        Exit For
                    End If
                Next
            End Using
        End Using
    End Sub

    ''' <summary>
    ''' 获取当前进程音量
    ''' </summary>
    ''' <returns></returns>
    Public Shared Function GetCurrentVolume() As Single
        Using enumerator As New MMDeviceEnumerator()
            Using device As MMDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                Dim sessionManager = device.AudioSessionManager
                Dim sessions = sessionManager.Sessions

                For i As Integer = 0 To sessions.Count - 1
                    Dim session = sessions(i)
                    If session.GetProcessID = Process.GetCurrentProcess().Id Then
                        Return session.SimpleAudioVolume.Volume
                    End If
                Next
            End Using
        End Using

        Return 0
    End Function

End Class
