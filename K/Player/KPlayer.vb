Public Interface KPlayer

    ''' <summary>
    ''' 获取或设置音量
    ''' </summary>
    ''' <returns></returns>
    Property Volume As Single

    ''' <summary>
    ''' 获取或设置伴唱状态
    ''' </summary>
    ''' <returns></returns>
    Property Accompaniment As Boolean

    ''' <summary>
    ''' 获取或设置播放速率
    ''' </summary>
    ''' <returns></returns>
    Property Rate As Single

    ''' <summary>
    ''' 获取或设置播放进度
    ''' </summary>
    ''' <returns></returns>
    Property Position As Single

    ''' <summary>
    ''' 获取播放时长
    ''' </summary>
    ''' <returns></returns>
    ReadOnly Property Duration As Double

    ''' <summary>
    ''' 获取部署状态
    ''' </summary>
    ''' <returns></returns>
    ReadOnly Property IsSetup As Boolean

    ''' <summary>
    ''' 获取播放状态
    ''' </summary>
    ''' <returns></returns>
    ReadOnly Property IsPlaying As Boolean

    ''' <summary>
    ''' 提示消息
    ''' </summary>
    ''' <param name="Title">消息标题</param>
    ''' <param name="Icon">消息图标</param>
    Sub Alert(Title As String, Icon As AlertIcon)

    ''' <summary>
    ''' 暂停
    ''' </summary>
    Sub Pause()

End Interface
