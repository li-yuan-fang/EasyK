Imports Newtonsoft.Json

<Serializable>
Public Class KWebSettings

    <JsonProperty("port")>
    Public Property Port As Integer = 8086

    <JsonProperty("key")>
    Public Property PassKey As String = vbNullString

    <JsonProperty("upload")>
    Public Property Upload As UploadSettings = New UploadSettings()

End Class
