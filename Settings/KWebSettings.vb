Imports Newtonsoft.Json

<Serializable>
Public Class KWebSettings

    ''' <summary>
    ''' Web服务器自动除错
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("auto_debug")>
    Public Property AutoDebug As Boolean = True

    ''' <summary>
    ''' Web服务器端口
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("port")>
    Public Property Port As Integer = 8086

    ''' <summary>
    ''' 自动查找端口阈值下界
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("auto_port_min")>
    Public Property AutoPortMin As Integer = 7000

    ''' <summary>
    ''' 自动查找端口阈值上界
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("auto_port_max")>
    Public Property AutoPortMax As Integer = 9999

    ''' <summary>
    ''' 授权码
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("key")>
    Public Property PassKey As String = vbNullString

    ''' <summary>
    ''' 上传设置
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("upload")>
    Public Property Upload As UploadSettings = New UploadSettings()

End Class
