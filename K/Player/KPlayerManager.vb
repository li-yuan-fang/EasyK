Public Interface KPlayerManager

    ''' <summary>
    ''' 启动播放器
    ''' </summary>
    Sub Start()

    ''' <summary>
    ''' 尝试自动部署播放器
    ''' </summary>
    Sub TryAutoSetup()

    ''' <summary>
    ''' 部署播放器
    ''' </summary>
    Sub Setup()

    ''' <summary>
    ''' 获取主屏幕
    ''' </summary>
    ''' <returns></returns>
    Function GetMainScreen() As ScreenUtils.OverlapScreen

    ''' <summary>
    ''' 获取托管播放器
    ''' </summary>
    ''' <returns></returns>
    ReadOnly Property Dummy As DummyPlayer

    ''' <summary>
    ''' 重启播放器窗体
    ''' </summary>
    Sub RestartPlayerForm()

    ''' <summary>
    ''' 提示消息
    ''' </summary>
    ''' <param name="Title">消息标题</param>
    ''' <param name="Icon">消息图标</param>
    Sub Alert(Title As String, Icon As AlertIcon)

    ''' <summary>
    ''' 获取播放器窗体
    ''' </summary>
    ''' <returns></returns>
    ReadOnly Property PlayerForm As FrmPlayer

End Interface
