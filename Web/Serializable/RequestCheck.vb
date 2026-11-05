Imports Newtonsoft.Json

Namespace KWeb.Serializable

    <Serializable>
    Public Class RequestCheck

        <JsonProperty("size")>
        Public Property Size As Integer

        <JsonProperty("hash")>
        Public Property Hash As String

    End Class

End Namespace
