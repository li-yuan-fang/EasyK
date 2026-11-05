Imports Newtonsoft.Json

Namespace KWeb.Serializable

    <Serializable>
    Public Class RequestBook

        <JsonProperty("title")>
        Public Property Title As String

        <JsonProperty("type")>
        Public Property Type As Integer

        <JsonProperty("content")>
        Public Property Content As String

    End Class

End Namespace
