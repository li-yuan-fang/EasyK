Imports Newtonsoft.Json

Namespace KWeb.Serializable

    <Serializable>
    Public Class RequestRank

        <JsonProperty("id")>
        Public Property Id As String

        <JsonProperty("rank")>
        Public Property Rank As Integer

    End Class

End Namespace