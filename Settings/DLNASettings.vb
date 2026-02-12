Imports Newtonsoft.Json

<Serializable>
Public Class DLNASettings

    ''' <summary>
    ''' DLNA设备UUID
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("uuid")>
    Public Property UUID As String = Guid.NewGuid().ToString()

    ''' <summary>
    ''' SSDP设备广播间隔(单位:ms)
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("ssdp_notify_interval")>
    Public Property SSDPNotifyInterval As Long = 3000

    ''' <summary>
    ''' SSDP设备广播包寿命(单位:s)
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("ssdp_max_age")>
    Public Property SSDPMaxAge As Integer = 66

    ''' <summary>
    ''' DLNA事件订阅推送间隔(单位:ms)
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("event_interval")>
    Public Property EventInterval As Integer = 1000

    ''' <summary>
    ''' DLNA事件订阅默认有效时间(单位:s)
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("event_default_expire")>
    Public Property EventDefaultExpire As Integer = 900

    ''' <summary>
    ''' DLNA事件订阅最大有效时间(单位:s)
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("event_max_expire")>
    Public Property EventMaxExpire As Integer = 3600

    ''' <summary>
    ''' DLNA事件推送最大失败次数(超过则取消订阅)
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("event_notify_fails")>
    Public Property EventNotifyFails As Integer = 3

    ''' <summary>
    ''' 严格检查用户投屏权限
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("strict_permission")>
    Public Property StrictPermission As Boolean = False

    ''' <summary>
    ''' 阻止连播的时间间隔(单位:s)
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("prevent_continue_range")>
    Public Property PreventContinueRange As Single = 4.0F

    ''' <summary>
    ''' DLNA音乐模式时使用彩色滚动歌词
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("lyric_colorful")>
    Public Property LyricColorful As Boolean = True

    ''' <summary>
    ''' DLNA音乐模式时对彩色滚动歌词进行高亮
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("lyric_highlight")>
    Public Property LyricHighlight As Boolean = True

    ''' <summary>
    ''' 每首歌结束时自动复位偏移
    ''' </summary>
    ''' <returns></returns>
    Public Property AutoResetOffset As Boolean = True

End Class
