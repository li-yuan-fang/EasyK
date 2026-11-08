Imports System.Text.RegularExpressions
Imports System.Threading
Imports NAudio.CoreAudioApi
Imports NAudio.Wave
Imports NAudio.Wave.SampleProviders

Public Class DummyPlayer
    Implements IDisposable

    Private Shared ReadOnly DeviceIdRegex As New Regex("(\{[A-Fa-f\d\-].{35,35}\})(?=$)")

    Private ReadOnly PlayerMutex As New Object()

    Private WaveProvider As BufferedWaveProvider = Nothing

    Private MusicProvider As Accompaniment.IResetable = Nothing

    Private VolumeProvider As VolumeSampleProvider = Nothing

    Private WithEvents Player As IWavePlayer = Nothing

    Private StoredDevice As MMDevice = Nothing

    '储存的音量(主要用于VolumeProvider未初始化时储存音量)
    Private StoredVolume As Single = 0.5

    '内部名义音量(实际音量要考虑伴奏增益)
    Private InternalVolume As Single = 0.5

    '加载播放设备信号量
    Private ReadOnly Loading As New ManualResetEventSlim(True)

    '加载完成标志
    Private Loaded As Boolean = False

    '播放设备传感器
    Private WithEvents DeviceSensor As New DeviceChangeHandler()

    Private WithEvents K As EasyK

    Private ReadOnly Settings As SettingContainer

    Private ReadOnly Builder As WavePlayerBuilder

    ''' <summary>
    ''' 初始化托管音频播放器
    ''' </summary>
    ''' <param name="K"></param>
    ''' <param name="Settings">配置容器</param>
    Public Sub New(K As EasyK, Settings As SettingContainer)
        Me.K = K
        Me.Settings = Settings

        Builder = New WavePlayerBuilder(Settings)
    End Sub

    ''' <summary>
    ''' 释放资源
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        DeviceSensor.Dispose()
    End Sub

    '自动关闭伴唱
    Private Sub OnPlayerTerminated() Handles K.OnPlayerTerminated
        If Settings.Settings.Audio.AutoResetAccompaniment Then Accompaniment = False
    End Sub

    '伴奏模式
    Private _Accompaniment As Boolean = False

    ''' <summary>
    ''' 获取或设置伴唱模式
    ''' </summary>
    ''' <returns></returns>
    Public Property Accompaniment As Boolean
        Get
            Return _Accompaniment
        End Get
        Set(value As Boolean)
            _Accompaniment = value
            UpdateRealVolume()
        End Set
    End Property

    ''' <summary>
    ''' 获取或设置音量
    ''' </summary>
    ''' <returns></returns>
    Public Property Volume As Single
        Get
            Return If(VolumeProvider IsNot Nothing, InternalVolume, StoredVolume)
        End Get
        Set(value As Single)
            If VolumeProvider IsNot Nothing Then
                InternalVolume = value
                UpdateRealVolume()
            Else
                StoredVolume = value
            End If
        End Set
    End Property

    '更新实际音量
    Private Sub UpdateRealVolume()
        If VolumeProvider Is Nothing Then Return

        '名义音量静音则直接静音
        If InternalVolume = 0 Then
            VolumeProvider.Volume = 0
            Return
        End If

        If Accompaniment Then
            VolumeProvider.Volume = InternalVolume + Settings.Settings.Audio.AccompanimentVolumeGain
        Else
            VolumeProvider.Volume = InternalVolume
        End If
    End Sub

    ''' <summary>
    ''' 重载播放设备
    ''' </summary>
    ''' <param name="Device">指定设备ID</param>
    ''' <remarks>指定设备ID说明重装请求来自事件</remarks>
    Private Sub ReloadDevice(Device As MMDevice) Handles DeviceSensor.OnDeviceUpdate
        '检测访问来源
        Dim Trigger As Boolean = Device IsNot Nothing

        If Trigger Then
            Logger.Debug("更新播放设备事件> {0}", Device.Properties(PropertyKeys.PKEY_AudioEndpoint_GUID).Value)

            If VolumeProvider Is Nothing OrElse WaveProvider Is Nothing Then Return

            '来自事件的请求需要先锁定访问
            With Loading
                .Wait()
                .Reset()
            End With
        End If

        If Player IsNot Nothing Then
            SyncLock PlayerMutex
                Try
                    Player.Stop()
                    Player.Dispose()
                Catch ex As Exception
                    Logger.Debug("卸载音频设备时出错 - {0}", ex.Message)
                End Try

                Player = Nothing
            End SyncLock
        End If

        Try
            SyncLock PlayerMutex
                If Trigger Then
                    StoredDevice = Device
                    Player = Builder.Build(Device)

                    '如果是来自事件的更新 需要执行播放
                    Task.Run(Sub() Play(True))
                Else
                    Player = Builder.Build(StoredDevice)
                End If

                Player.Init(VolumeProvider)
            End SyncLock

            Loaded = True
            Loading.Set()
        Catch ex As Exception
            Logger.Error("重载托管音频设备出错 - {0}", ex.Message)

            '彻底卸载音频设备
            [Stop](True, False)
        End Try
    End Sub

    Private Sub Player_PlaybackStopped(sender As Object, e As StoppedEventArgs) Handles Player.PlaybackStopped
        If e.Exception IsNot Nothing Then
            Logger.Error("托管音频 NAudio 内部错误 - {0}", e.Exception.ToString)
            Logger.Info("请优先尝试通过 Windows音量合成器 调整程序播放设备 使其与主播放设备一致 一般都能解决")
            Logger.Warn("如无法解决 也可尝试通过配置文件开启WASAPI 但是这样做会造成额外的CPU负担")
        End If
    End Sub

    ''' <summary>
    ''' 初始化设备
    ''' </summary>
    ''' <param name="WaveFormat">波形格式</param>
    ''' <param name="Float">浮点格式</param>
    Public Sub Setup(WaveFormat As WaveFormat, Float As Boolean)
        '释放所有托管播放器
        [Stop](False, True)

        Try
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
            InternalVolume = StoredVolume
            VolumeProvider = New VolumeSampleProvider(Commit)
            UpdateRealVolume()
        Catch ex As Exception
            Logger.Error("配置托管音频设备出错 - {0}", ex.Message)

            '彻底卸载音频设备
            [Stop](True, False)
            Return
        End Try

        '配置输出
        ReloadDevice(Nothing)
    End Sub

    Public Sub Append(Wave As Byte(), pts As Long)
        Loading.Wait()
        If Not Loaded OrElse Player Is Nothing OrElse WaveProvider Is Nothing Then Return

        SyncLock PlayerMutex
            WaveProvider.AddSamples(Wave, 0, Wave.Length)
        End SyncLock
    End Sub

    Public Sub Play()
        Play(False)
    End Sub

    Private Sub Play(Force As Boolean)
        Loading.Wait()
        If Not Loaded OrElse Player Is Nothing Then Return

        SyncLock PlayerMutex
            If Force OrElse Player.PlaybackState <> PlaybackState.Playing Then Player.Play()
        End SyncLock
    End Sub

    Public Sub Pause()
        Loading.Wait()
        If Not Loaded OrElse Player Is Nothing Then Return

        SyncLock PlayerMutex
            Player.Pause()
        End SyncLock
    End Sub

    Public Sub CleanBuffer()
        Loading.Wait()
        If Not Loaded OrElse Player Is Nothing OrElse WaveProvider Is Nothing Then Return

        SyncLock PlayerMutex
            WaveProvider.ClearBuffer()
            If MusicProvider IsNot Nothing Then MusicProvider.Reset()
        End SyncLock
    End Sub

    Public Sub [Stop]()
        [Stop](True, True)
    End Sub

    Private Sub [Stop](Free As Boolean, Wait As Boolean)
        '访问Direct之前先确保上锁
        If Wait Then
            With Loading
                .Wait()
                .Reset()
            End With
        End If

        Loaded = False

        If Player IsNot Nothing Then
            SyncLock PlayerMutex
                Try
                    With Player
                        .Stop()
                        .Dispose()
                    End With
                Catch ex As Exception
                    Logger.Debug("卸载音频设备时出错 - {0}", ex.Message)
                End Try

                Player = Nothing
            End SyncLock
        End If

        If VolumeProvider IsNot Nothing Then
            StoredVolume = InternalVolume
            VolumeProvider = Nothing
        End If

        If WaveProvider IsNot Nothing Then WaveProvider = Nothing
        If MusicProvider IsNot Nothing Then MusicProvider = Nothing

        If Free Then Loading.Set()
    End Sub

End Class
