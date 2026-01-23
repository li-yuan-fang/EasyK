Imports Newtonsoft.Json

<Serializable>
Public Class DLNASettings

    <JsonProperty("uuid")>
    Public Property UUID As String = Guid.NewGuid().ToString()

    <JsonProperty("ssdp_notify_interval")>
    Public Property SSDPNotifyInterval As Long = 3000

    <JsonProperty("ssdp_max_age")>
    Public Property SSDPMaxAge As Integer = 66

    <JsonProperty("event_interval")>
    Public Property EventInterval As Integer = 1000

    <JsonProperty("event_default_expire")>
    Public Property EventDefaultExpire As Integer = 900

    <JsonProperty("event_max_expire")>
    Public Property EventMaxExpire As Integer = 3600

    <JsonProperty("event_notify_fails")>
    Public Property EventNotifyFails As Integer = 3

    <JsonProperty("strict_permission")>
    Public Property StrictPermission As Boolean = False

    <JsonProperty("prevent_continue_range")>
    Public Property PreventContinueRange As Single = 4.0F

    <JsonProperty("lyric_colorful")>
    Public Property LyricColorful As Boolean = True

    <JsonProperty("lyric_highlight")>
    Public Property LyricHighlight As Boolean = True

End Class
