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

End Class
