Imports System.Drawing
Imports CefSharp

Public Class EasyKPlayer
    Implements IDisposable, KPlayer, KPlayerManager

    Private ReadOnly K As EasyK

    Private ReadOnly Settings As SettingContainer

    Private ReadOnly QR As QRManager

    Private _PlayerForm As FrmPlayer

    ''' <summary>
    ''' 获取播放器窗体
    ''' </summary>
    ''' <returns></returns>
    Friend ReadOnly Property PlayerForm As FrmPlayer Implements KPlayerManager.PlayerForm
        Get
            Return _PlayerForm
        End Get
    End Property

    ''' <summary>
    ''' 获取托管播放器
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Dummy As DummyPlayer Implements KPlayerManager.Dummy

    ''' <summary>
    ''' 获取或设置音量
    ''' </summary>
    ''' <returns></returns>
    Public Property Volume As Single Implements KPlayer.Volume
        Get
            Return If(Settings.Settings.Audio.IsDummyAudio, Dummy.Volume, AudioUtils.GetSystemVolume())
        End Get
        Set(value As Single)
            If Settings.Settings.Audio.IsDummyAudio Then
                Dummy.Volume = Math.Max(0, Math.Min(value, 1))
            ElseIf Settings.Settings.Audio.AllowUpdateSystemVolume Then
                AudioUtils.SetSystemVolume(value)
            End If

            If value > 0 Then
                Alert(Math.Round(value * 100.0F), AlertIcon.Volume)
            Else
                Alert("静音", AlertIcon.Mute)
            End If
        End Set
    End Property

    ''' <summary>
    ''' 获取或设置伴唱状态
    ''' </summary>
    ''' <returns></returns>
    Public Property Accompaniment As Boolean Implements KPlayer.Accompaniment
        Get
            If Not Settings.Settings.Audio.AllowAccompaniment Then Return False
            Return Dummy.Accompaniment
        End Get
        Set(value As Boolean)
            If Not Settings.Settings.Audio.AllowAccompaniment Then Return
            Dummy.Accompaniment = value

            If value Then
                Alert("实时伴奏", AlertIcon.Accompaniment)
            Else
                Alert("原声", AlertIcon.Original)
            End If
        End Set
    End Property

    ''' <summary>
    ''' 获取或设置播放速率
    ''' </summary>
    ''' <returns></returns>
    Public Property Rate As Single Implements KPlayer.Rate
        Get
            If K.GetCurrent() Is Nothing OrElse _PlayerForm Is Nothing Then Return 1.0F

            With _PlayerForm
                If .IsDisposed() OrElse Not .Setuped Then Return 1.0F
                Return .Rate
            End With
        End Get
        Set(value As Single)
            If K.GetCurrent() Is Nothing OrElse _PlayerForm Is Nothing Then Return

            With _PlayerForm
                If .IsDisposed() OrElse Not .Setuped Then Return
                .Rate = value
            End With
        End Set
    End Property

    ''' <summary>
    ''' 获取或设置播放进度
    ''' </summary>
    ''' <returns></returns>
    Public Property Position As Single Implements KPlayer.Position
        Get
            If K.GetCurrent() Is Nothing OrElse _PlayerForm Is Nothing Then Return 0F

            With _PlayerForm
                If .IsDisposed() OrElse Not .Setuped Then Return 0F
                Return .Position
            End With
        End Get
        Set(value As Single)
            If K.GetCurrent() Is Nothing OrElse _PlayerForm Is Nothing Then Return

            With _PlayerForm
                If .IsDisposed() OrElse Not .Setuped Then Return
                .Position = value
            End With
        End Set
    End Property

    ''' <summary>
    ''' 获取播放时长
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Duration As Double Implements KPlayer.Duration
        Get
            If K.GetCurrent() Is Nothing OrElse _PlayerForm Is Nothing Then Return 0D

            With _PlayerForm
                If .IsDisposed() OrElse Not .Setuped Then Return 0D
                Return .Duration
            End With
        End Get
    End Property

    ''' <summary>
    ''' 获取播放状态
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property IsPlaying As Boolean Implements KPlayer.IsPlaying
        Get
            If PlayerForm Is Nothing Then Return False

            With PlayerForm
                If .IsDisposed Then Return False
                Return .Playing
            End With
        End Get
    End Property

    ''' <summary>
    ''' 获取部署状态
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property IsSetup As Boolean Implements KPlayer.IsSetup
        Get
            If PlayerForm Is Nothing Then Return False

            With PlayerForm
                If .IsDisposed() Then Return False
                Return .Setuped
            End With
        End Get
    End Property

    ''' <summary>
    ''' 部署
    ''' </summary>
    Friend Sub Setup() Implements KPlayerManager.Setup
        Setup(Rectangle.Empty)
    End Sub

    ''' <summary>
    ''' 启动播放器
    ''' </summary>
    Friend Sub Start() Implements KPlayerManager.Start
        _PlayerForm.Show()
    End Sub

    ''' <summary>
    ''' 尝试自动部署
    ''' </summary>
    Friend Sub TryAutoSetup() Implements KPlayerManager.TryAutoSetup
        With Settings.Settings
            If .Restore Is Nothing Then Return

            '查找屏幕
            Dim m = ScreenUtils.GetMonitors()
            With .Restore
                For i = 0 To m.Count - 1
                    If m(i).Name = .Name AndAlso m(i).ManufacturerName = .ManufacturerName AndAlso
                        m(i).ProductCodeID = .ProductCodeID AndAlso m(i).SerialNumber = .SerialNumber AndAlso
                        m(i).ManufactureDate = .ManufactureDate Then
                        Dim Screens As Windows.Forms.Screen() = Windows.Forms.Screen.AllScreens()
                        If i >= Screens.Length Then Continue For

                        '部署
                        Setup(Screens(i).Bounds)

                        Return
                    End If
                Next
            End With

            Logger.Warn("自动部署失败 - 找不到对应的屏幕")
        End With
    End Sub

    Private Sub Setup(Bounds As Rectangle)
        QR.ResetBounds()
        _PlayerForm.Setup(Bounds)

        '检测是否需要显示二维码
        If Settings.Settings.AutoShowQR Then QR.ShowQRCode(False)

        K.Push()
    End Sub

    ''' <summary>
    ''' 初始化播放器管理器
    ''' </summary>
    ''' <param name="K"></param>
    ''' <param name="Settings">配置容器</param>
    ''' <param name="QR">二维码管理器</param>
    Public Sub New(K As EasyK, Settings As SettingContainer, QR As QRManager)
        Me.K = K
        Me.Settings = Settings
        Me.QR = QR

        Dummy = New DummyPlayer(K, Settings)
        _PlayerForm = New FrmPlayer(K, Settings, Me)
    End Sub

    ''' <summary>
    ''' 暂停
    ''' </summary>
    Public Sub Pause() Implements KPlayer.Pause
        K.Pause()
    End Sub

    ''' <summary>
    ''' 重启播放器窗体
    ''' </summary>
    Friend Sub RestartPlayerForm() Implements KPlayerManager.RestartPlayerForm
        Dim NewForm As FrmPlayer = Nothing

        QR.CloseQRCode()

        With _PlayerForm
            .Invoke(Sub()
                        NewForm = New FrmPlayer(K, Settings, Me)
                        NewForm.Show()

                        .Close()
                    End Sub)

            .Dispose()
        End With

        _PlayerForm = NewForm
    End Sub

    ''' <summary>
    ''' 发送提示消息
    ''' </summary>
    ''' <param name="Title">消息标题</param>
    ''' <param name="Icon">消息图标</param>
    Friend Sub Alert(Title As String, Icon As AlertIcon) Implements KPlayerManager.Alert
        If _PlayerForm Is Nothing Then Return

        With _PlayerForm
            If .IsDisposed() OrElse Not .Setuped Then Return
            .Alert(Title, Icon)
        End With
    End Sub

    ''' <summary>
    ''' 获取主屏幕
    ''' </summary>
    ''' <returns></returns>
    Public Function GetMainScreen() As ScreenUtils.OverlapScreen Implements KPlayerManager.GetMainScreen
        If _PlayerForm Is Nothing OrElse _PlayerForm.IsDisposed() Then
            Return New ScreenUtils.OverlapScreen With {
                .Id = -1,
                .Screen = Nothing
            }
        Else
            With _PlayerForm
                Return .Invoke(Function() ScreenUtils.GetOverlapScreen(.DesktopBounds))
            End With
        End If
    End Function

    ''' <summary>
    ''' 销毁资源
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        Dummy.Dispose()

        With _PlayerForm
            Try
                .Invoke(Sub()
                            Cef.Shutdown()
                            .Close()
                        End Sub)
            Catch ex As Exception
                Logger.Debug("释放主窗体出错 - {0}", ex.Message)
            End Try

            .Dispose()
        End With
        _PlayerForm = Nothing
    End Sub

End Class
