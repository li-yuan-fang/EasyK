Imports System.Text.RegularExpressions
Imports System.Threading
Imports NAudio.Wave
Imports NAudio.Wave.SampleProviders

Public Class DummyPlayer
    Implements IDisposable

    Private Shared ReadOnly DeviceIdRegex As New Regex("(\{[A-Fa-f\d\-].{35,35}\})(?=$)")

    Private WaveProvider As BufferedWaveProvider = Nothing

    Private MusicProvider As Accompaniment.IResetable = Nothing

    Private VolumeProvider As VolumeSampleProvider = Nothing

    Private Direct As DirectSoundOut = Nothing

    Private StoredDevice As String = vbNullString

    '储存的音量
    Private StoredVolume As Single = 0.5

    '加载播放设备信号量
    Private ReadOnly Loading As New ManualResetEventSlim(True)

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

    ''' <summary>
    ''' 重载播放设备
    ''' </summary>
    ''' <param name="DeviceId">指定设备ID</param>
    ''' <remarks>指定设备ID说明重装请求来自事件</remarks>
    Private Sub ReloadDevice(DeviceId As String) Handles DeviceSensor.OnDeviceUpdate
        '检测访问来源
        Dim Trigger As Boolean = Not String.IsNullOrEmpty(DeviceId)

        If Trigger Then
            If Settings.Settings.DebugMode Then
                Console.WriteLine("更新播放设备事件> {0}", DeviceId)
            End If

            If VolumeProvider Is Nothing OrElse WaveProvider Is Nothing Then Return

            '来自事件的请求需要先锁定访问
            With Loading
                .Wait()
                .Reset()
            End With
        End If

        If Direct IsNot Nothing Then
            SyncLock Direct
                Try
                    Direct.Stop()
                    Direct.Dispose()
                Catch ex As Exception
                    If Settings.Settings.DebugMode Then
                        Console.WriteLine("卸载音频设备时出错 - {0}", ex.Message)
                    End If
                End Try

                Direct = Nothing
            End SyncLock
        End If

        If Trigger Then
            StoredDevice = DeviceId
            Direct = New DirectSoundOut(Guid.Parse(DeviceId))

            '如果是来自事件的更新 需要执行播放
            Task.Run(Sub() Play(True))
        Else
            If String.IsNullOrEmpty(StoredDevice) Then
                Direct = New DirectSoundOut()
            Else
                Direct = New DirectSoundOut(Guid.Parse(StoredDevice))
            End If
        End If
        SyncLock Direct
            Direct.Init(VolumeProvider)
        End SyncLock

        '允许播放
        Loading.Set()
    End Sub

    Public Sub Setup(WaveFormat As WaveFormat, Float As Boolean)
        '释放所有托管播放器
        [Stop](False)

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
        Play(False)
    End Sub

    Private Sub Play(Force As Boolean)
        Loading.Wait()
        If Direct Is Nothing Then Return

        SyncLock Direct
            If Force OrElse Direct.PlaybackState <> PlaybackState.Playing Then Direct.Play()
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
        Loading.Wait()
        If Direct Is Nothing OrElse WaveProvider Is Nothing Then Return

        SyncLock Direct
            WaveProvider.ClearBuffer()
            If MusicProvider IsNot Nothing Then MusicProvider.Reset()
        End SyncLock
    End Sub

    Public Sub [Stop]()
        [Stop](True)
    End Sub

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="Free"></param>
    Private Sub [Stop](Free As Boolean)
        '访问Direct之前先上锁
        With Loading
            .Wait()
            .Reset()
        End With

        If Direct IsNot Nothing Then
            SyncLock Direct
                Try
                    Direct.Stop()
                    Direct.Dispose()
                Catch ex As Exception
                    If Settings.Settings.DebugMode Then
                        Console.WriteLine("卸载音频设备时出错 - {0}", ex.Message)
                    End If
                End Try

                Direct = Nothing
            End SyncLock
        End If

        If VolumeProvider IsNot Nothing Then
            StoredVolume = VolumeProvider.Volume
            VolumeProvider = Nothing
        End If

        If WaveProvider IsNot Nothing Then WaveProvider = Nothing
        If MusicProvider IsNot Nothing Then MusicProvider = Nothing

        If Free Then Loading.Set()
    End Sub

End Class
