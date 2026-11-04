Public Interface DLNAServer

    ''' <summary>
    ''' 获取或设置DLNA设备名称
    ''' </summary>
    ''' <returns></returns>
    Property DeviceName As String

    ''' <summary>
    ''' 获取或设置DLNA歌词对比度阈值
    ''' </summary>
    ''' <returns></returns>
    Property LyricContrast As Double

    ''' <summary>
    ''' 获取或设置DLNA歌词交错
    ''' </summary>
    ''' <returns></returns>
    Property LyricIntersect As Boolean

    ''' <summary>
    ''' 获取或设置DLNA歌词偏移
    ''' </summary>
    ''' <returns></returns>
    Property LyricOffset As Double

    ''' <summary>
    ''' 刷新DLNA歌词
    ''' </summary>
    Sub RefreshLyrics()

End Interface
