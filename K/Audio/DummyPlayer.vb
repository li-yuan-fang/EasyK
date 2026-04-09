Imports System.Reflection
Imports System.Threading
Imports NAudio.Wave
Imports NAudio.Wave.SampleProviders

Public Class DummyPlayer
    Implements IDisposable

    Private WaveProvider As BufferedWaveProvider = Nothing

    Private MusicProvider As Accompaniment.IResetable = Nothing

    Private VolumeProvider As VolumeSampleProvider = Nothing

    Private Direct As DirectSoundOut = Nothing

    '储存的音量
    Private StoredVolume As Single = 0.5

    '加载播放设备信号量
    Private ReadOnly Loading As New ManualResetEventSlim(False)

    '播放设备传感器
    Private WithEvents DeviceSensor As New DeviceChangeHandler()

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

    ''' <summary>
    ''' 释放资源
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        DeviceSensor.Dispose()
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

    '重载播放设备
    Private Sub ReloadDevice(DeviceId As String) Handles DeviceSensor.OnDeviceUpdate
        If Loading.IsSet() Then Loading.Reset()
        If VolumeProvider Is Nothing OrElse WaveProvider Is Nothing Then Return

        If Direct IsNot Nothing Then
            SyncLock Direct
                Direct.Stop()
                Direct.Dispose()
                Direct = Nothing
            End SyncLock
        End If

        Direct = New DirectSoundOut()
        SyncLock Direct
            Direct.Init(VolumeProvider)
        End SyncLock

        '允许播放
        Loading.Set()

        '如果是被迫重载 则自动播放
        If Not String.IsNullOrEmpty(DeviceId) Then Play()
    End Sub

    Public Sub Setup(WaveFormat As WaveFormat, Float As Boolean, Optional Id As String = vbNullString)
        '释放所有托管播放器
        [Stop]()

        '缓冲区
        WaveProvider = New BufferedWaveProvider(WaveFormat) With {
            .BufferDuration = TimeSpan.FromSeconds(2),
            .DiscardOnBufferOverflow = True
        }

        '音频处理
        Dim Commit As ISampleProvider
        If Settings.Settings.Audio.AllowAccompaniment Then
            If Float Then
                Dim p = New AccompanimentProviderFloat(Me, Settings, WaveProvider.ToSampleProvider(), WaveFormat)
                MusicProvider = p
                Commit = p
            Else
                Dim p = New AccompanimentProvider(Me, Settings, WaveProvider, WaveFormat)
                MusicProvider = p
                Commit = p.ToSampleProvider()
            End If
        Else
            Commit = WaveProvider.ToSampleProvider()
        End If

        '音量调整
        VolumeProvider = New VolumeSampleProvider(Commit) With {
            .Volume = StoredVolume
        }

        '配置输出
        ReloadDevice(vbNullString)
    End Sub

    Public Sub Append(Wave As Byte(), pts As Long)
        Loading.Wait()
        If Direct Is Nothing OrElse WaveProvider Is Nothing Then Return

        SyncLock Direct
            WaveProvider.AddSamples(Wave, 0, Wave.Length)
        End SyncLock
    End Sub

    Public Sub Play()
        Loading.Wait()
        If Direct Is Nothing Then Return

        SyncLock Direct
            If Direct.PlaybackState <> PlaybackState.Playing Then Direct.Play()
        End SyncLock
    End Sub

    Public Sub Pause()
        Loading.Wait()
        If Direct Is Nothing Then Return

        SyncLock Direct
            Direct.Pause()
        End SyncLock
    End Sub

    Public Sub CleanBuffer()
        If Direct Is Nothing OrElse WaveProvider Is Nothing Then Return

        SyncLock Direct
            WaveProvider.ClearBuffer()
            If MusicProvider IsNot Nothing Then MusicProvider.Reset()
        End SyncLock
    End Sub

    Public Sub [Stop]()
        '阻塞播放
        Loading.Reset()

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
    End Sub

End Class
