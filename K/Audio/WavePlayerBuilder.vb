Imports System.Reflection
Imports CefSharp.DevTools.CSS
Imports NAudio.CoreAudioApi
Imports NAudio.Wave

Public Class WavePlayerBuilder

    ''' <summary>
    ''' 播放器类型
    ''' </summary>
    Public Enum PlayerType
        WaveOut = 0
        DirectSound
        WASAPI
    End Enum

    Private ReadOnly Settings As SettingContainer

    ''' <summary>
    ''' 初始化构建器
    ''' </summary>
    ''' <param name="Settings"></param>
    Public Sub New(Settings As SettingContainer)
        Me.Settings = Settings
    End Sub

    Private Function BuildDirectSoundPlayer(Device As MMDevice) As IWavePlayer
        If Device IsNot Nothing Then
            Dim Id As String = Device.Properties(PropertyKeys.PKEY_AudioEndpoint_GUID).Value
            If Not String.IsNullOrEmpty(Id) Then
                Try
                    Return New DirectSoundOut(Guid.Parse(Id))
                Catch ex As Exception
                    Logger.Debug("创建指定的 DirectSound 设备时出错 - {0}", ex.Message)
                End Try
            End If
        End If

        Return New DirectSoundOut()
    End Function

    Private Function BuildWASAPIPlayer(Device As MMDevice) As IWavePlayer
        Try
            With Settings.Settings.Audio
                If Device IsNot Nothing Then
                    Return New WasapiOut(Device, AudioClientShareMode.Shared, .WasApiSyncEvent, .WasApiLatency)
                Else
                    Return New WasapiOut(AudioClientShareMode.Shared, .WasApiSyncEvent, .WasApiLatency)
                End If
            End With
        Catch ex As Exception
            Logger.Debug("创建指定的 WASAPI 设备时出错 - {0}", ex.Message)

            Return New WasapiOut()
        End Try
    End Function

    Private Function BuildWaveOutPlayer(Device As MMDevice) As IWavePlayer
        If Device IsNot Nothing Then
            For i = 0 To WaveOut.DeviceCount - 1
                Dim Caps = WaveOut.GetCapabilities(i)
                If Device.FriendlyName = Caps.ProductName Then
                    Return New WaveOutEvent() With {
                        .DeviceNumber = i
                    }
                End If
            Next
        End If

        Return New WaveOutEvent()
    End Function

    Private Function BuildDefault(Device As MMDevice) As IWavePlayer
        Return BuildWaveOutPlayer(Device)
    End Function

    ''' <summary>
    ''' 构建播放器对象
    ''' </summary>
    ''' <param name="Device">播放设备</param>
    ''' <returns></returns>
    Public Function Build(Device As MMDevice) As IWavePlayer
        Dim Mode = Settings.Settings.Audio.DummyApiMode
        If Not [Enum].IsDefined(GetType(PlayerType), Mode) Then Return BuildDefault(Device)

        Try
            Dim m = [GetType]().GetMethod($"Build{Mode}Player", BindingFlags.Instance Or BindingFlags.NonPublic)
            If m IsNot Nothing Then Return m.Invoke(Me, {Device})
        Catch
        End Try

        Return BuildDefault(Device)
    End Function

End Class
