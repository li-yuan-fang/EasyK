Imports Newtonsoft.Json

Namespace KWeb.Serializable

    <Serializable>
    Public Class RequestPanel

        <JsonProperty("id")>
        Public Property Id As String

        <JsonProperty("value")>
        Public Property Value As Object

    End Class

End Namespace
