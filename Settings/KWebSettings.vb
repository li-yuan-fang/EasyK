Imports Newtonsoft.Json

<Serializable>
Public Class KWebSettings

    <JsonProperty("auto_debug")>
    Public Property AutoDebug As Boolean = True

    <JsonProperty("port")>
    Public Property Port As Integer = 8086

    <JsonProperty("auto_port_min")>
    Public Property AutoPortMin As Integer = 7000

    <JsonProperty("auto_port_max")>
    Public Property AutoPortMax As Integer = 9999

    <JsonProperty("key")>
    Public Property PassKey As String = vbNullString

    <JsonProperty("upload")>
    Public Property Upload As UploadSettings = New UploadSettings()

End Class
