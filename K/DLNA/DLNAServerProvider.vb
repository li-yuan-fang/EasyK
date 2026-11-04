Imports EasyK.DLNA.Player

Public Class DLNAServerProvider
    Inherits DLNA.DLNA
    Implements DLNAServer, DLNAServerManager

    Private _LyricOffset As Double = 0.0D

    ''' <summary>
    ''' 获取或设置DLNA设备名称
    ''' </summary>
    ''' <returns></returns>
    Public Property DeviceName As String Implements DLNAServer.DeviceName
        Get
            Return Me.Name
        End Get
        Set(value As String)
            Me.Name = value
        End Set
    End Property

    ''' <summary>
    ''' 获取或设置DLNA歌词偏移
    ''' </summary>
    ''' <returns></returns>
    Public Property LyricOffset As Double Implements DLNAServer.LyricOffset
        Get
            Return _LyricOffset
        End Get
        Set(value As Double)
            If _LyricOffset <> value Then
                _LyricOffset = value

                If Player IsNot Nothing Then Player.UpdateMusicLyricOffset()
            End If
        End Set
    End Property

    ''' <summary>
    ''' 获取或设置DLNA歌词交错
    ''' </summary>
    ''' <returns></returns>
    Public Property LyricIntersect As Boolean Implements DLNAServer.LyricIntersect
        Get
            Return Settings.Settings.DLNA.LyricIntersect
        End Get
        Set(value As Boolean)
            With Settings.Settings.DLNA
                If .LyricIntersect <> value Then
                    .LyricIntersect = value

                    If Player IsNot Nothing Then Player.UpdateMusicLyricOptions()
                End If
            End With
        End Set
    End Property

    ''' <summary>
    ''' 获取或设置DLNA歌词对比度阈值
    ''' </summary>
    ''' <returns></returns>
    Public Property LyricContrast As Double Implements DLNAServer.LyricContrast
        Get
            Return Settings.Settings.DLNA.LyricContrastThreshold
        End Get
        Set(value As Double)
            With Settings.Settings.DLNA
                If .LyricContrastThreshold <> value Then
                    .LyricContrastThreshold = value

                    If Player IsNot Nothing Then Player.UpdateMusicLyricOptions()
                End If
            End With
        End Set
    End Property

    ''' <summary>
    ''' 重置歌词偏移
    ''' </summary>
    Public Sub ResetLyricsOffset() Implements DLNAServerManager.ResetLyricsOffset
        _LyricOffset = 0.0D
    End Sub

    ''' <summary>
    ''' 设置DLNA播放器对象
    ''' </summary>
    ''' <param name="Player">DLNA播放器对象</param>
    Public Sub SetDLNAPlayer(Player As DLNAPlayer) Implements DLNAServerManager.SetDLNAPlayer
        Me.Player = Player
    End Sub

    ''' <summary>
    ''' 初始化DLNA服务
    ''' </summary>
    ''' <param name="K"></param>
    ''' <param name="Settings">配置容器</param>
    Public Sub New(K As EasyK, Settings As SettingContainer)
        MyBase.New(K, Settings)
    End Sub

End Class
