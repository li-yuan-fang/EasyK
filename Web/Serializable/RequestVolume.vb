Imports Newtonsoft.Json

<Serializable>
Public Class RequestVolume

    <JsonProperty("action")>
    Public Property VolumeAction As Integer

    <JsonProperty("value")>
    Public Property VolumeValue As Integer = 1

End Class
