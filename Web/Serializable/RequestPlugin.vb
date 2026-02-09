Imports Newtonsoft.Json

<Serializable>
Public Class RequestPlugin

    <JsonProperty("id")>
    Public Property Id As String

    <JsonProperty("value")>
    Public Property Value As Object

End Class
