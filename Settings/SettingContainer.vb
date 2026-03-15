Public Class SettingContainer
    Implements IDisposable

    ''' <summary>
    ''' 获取配置
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Settings As KSettings
        Get
            Return _Settings
        End Get
    End Property

    Private _Settings As KSettings

    ''' <summary>
    ''' 初始化
    ''' </summary>
    Public Sub New()
        Reload()
    End Sub

    ''' <summary>
    ''' 重新载入配置
    ''' </summary>
    Public Sub Reload()
        _Settings = KSettings.CreateSettings()
    End Sub

    ''' <summary>
    ''' 销毁资源
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        _Settings.Save()
    End Sub

End Class
