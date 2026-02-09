Imports NAudio
Imports NAudio.Wave
Imports NAudio.Wave.SampleProviders

Public Class DummyPlayer

    Private WaveProvider As BufferedWaveProvider = Nothing

    Private VolumeProvider As VolumeSampleProvider = Nothing

    Private Direct As DirectSoundOut = Nothing

    '储存的音量
    Private StoredVolume As Single = 0.5

    Private _Mute As Boolean = False

    ''' <summary>
    ''' 获取或设置托管静音模式
    ''' </summary>
    ''' <returns></returns>
    Public Property Mute As Boolean
        Get
            Return _Mute
        End Get
        Set(value As Boolean)
            If VolumeProvider IsNot Nothing Then
                If value Then
                    StoredVolume = VolumeProvider.Volume
                    VolumeProvider.Volume = 0
                Else
                    VolumeProvider.Volume = StoredVolume
                End If
            End If

            _Mute = value
        End Set
    End Property

    ''' <summary>
    ''' 获取或设置音量
    ''' </summary>
    ''' <returns></returns>
    Public Property Volume As Single
        Get
            Return If(VolumeProvider IsNot Nothing, VolumeProvider.Volume, StoredVolume)
        End Get
        Set(value As Single)
            If VolumeProvider IsNot Nothing AndAlso Not Mute Then
                VolumeProvider.Volume = value
            Else
                StoredVolume = value
            End If
        End Set
    End Property

    Public Sub Setup(WaveFormat As WaveFormat)
        [Stop]()

        WaveProvider = New BufferedWaveProvider(WaveFormat) With {
            .BufferDuration = TimeSpan.FromSeconds(2),
            .DiscardOnBufferOverflow = True
        }

        VolumeProvider = New VolumeSampleProvider(WaveProvider.ToSampleProvider()) With {
            .Volume = StoredVolume
        }

        Direct = New DirectSoundOut()
        SyncLock Direct
            Direct.Init(VolumeProvider)
        End SyncLock
    End Sub

    Public Sub Append(Wave As Byte(), pts As Long)
        If Direct Is Nothing OrElse WaveProvider Is Nothing Then Return

        SyncLock Direct
            WaveProvider.AddSamples(Wave, 0, Wave.Length)
        End SyncLock
    End Sub

    Public Sub Play()
        If Direct Is Nothing Then Return

        SyncLock Direct
            If Direct.PlaybackState <> PlaybackState.Playing Then Direct.Play()
        End SyncLock
    End Sub

    Public Sub Pause()
        If Direct Is Nothing Then Return

        SyncLock Direct
            Direct.Pause()
        End SyncLock
    End Sub

    Public Sub CleanBuffer()
        If Direct Is Nothing OrElse WaveProvider Is Nothing Then Return

        SyncLock Direct
            WaveProvider.ClearBuffer()
        End SyncLock
    End Sub

    Public Sub [Stop]()
        If Direct IsNot Nothing Then
            SyncLock Direct
                Direct.Stop()
                Direct.Dispose()
                Direct = Nothing
            End SyncLock
        End If

        If VolumeProvider IsNot Nothing Then
            StoredVolume = VolumeProvider.Volume
            VolumeProvider = Nothing
        End If

        If WaveProvider IsNot Nothing Then WaveProvider = Nothing

        _Mute = False
    End Sub

End Class
