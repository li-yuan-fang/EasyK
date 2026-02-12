Imports System.Reflection
Imports NAudio.Wave
Imports NAudio.Wave.SampleProviders

Public Class DummyPlayer

    Private WaveProvider As BufferedWaveProvider = Nothing

    Private MusicProvider As ISampleProvider = Nothing

    Private VolumeProvider As VolumeSampleProvider = Nothing

    Private Direct As DirectSoundOut = Nothing

    '储存的音量
    Private StoredVolume As Single = 0.5

    Private ReadOnly Settings As SettingContainer

    ''' <summary>
    ''' 初始化托管音频播放器
    ''' </summary>
    ''' <param name="K"></param>
    ''' <param name="Settings">配置容器</param>
    Public Sub New(K As EasyK, Settings As SettingContainer)
        Me.Settings = Settings
        AddHandler K.OnPlayerTerminated, AddressOf OnPlayerTerminated
    End Sub

    '自动关闭伴唱
    Private Sub OnPlayerTerminated()
        If Settings.Settings.Audio.AutoResetAccompaniment Then Accompaniment = False
    End Sub

    ''' <summary>
    ''' 获取或设置伴唱模式
    ''' </summary>
    ''' <returns></returns>
    Public Property Accompaniment As Boolean = False

    ''' <summary>
    ''' 获取或设置音量
    ''' </summary>
    ''' <returns></returns>
    Public Property Volume As Single
        Get
            Return If(VolumeProvider IsNot Nothing, VolumeProvider.Volume, StoredVolume)
        End Get
        Set(value As Single)
            If VolumeProvider IsNot Nothing Then
                VolumeProvider.Volume = value
            Else
                StoredVolume = value
            End If
        End Set
    End Property

    Public Sub Setup(WaveFormat As WaveFormat, Float As Boolean)
        [Stop]()

        '缓冲区
        WaveProvider = New BufferedWaveProvider(WaveFormat) With {
            .BufferDuration = TimeSpan.FromSeconds(2),
            .DiscardOnBufferOverflow = True
        }

        '音频处理
        If Settings.Settings.Audio.AllowAccompaniment Then
            If Float Then
                MusicProvider = New AccompanimentProviderFloat(Me, Settings, WaveProvider.ToSampleProvider(), WaveFormat)
            Else
                Dim ap = New AccompanimentProvider(Me, Settings, WaveProvider, WaveFormat)
                MusicProvider = ap.ToSampleProvider()
            End If
        Else
            MusicProvider = WaveProvider.ToSampleProvider()
        End If

        '音量调整
        VolumeProvider = New VolumeSampleProvider(MusicProvider) With {
            .Volume = StoredVolume
        }

        '播放
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
        If MusicProvider IsNot Nothing Then MusicProvider = Nothing

        _Mute = False
    End Sub

End Class
