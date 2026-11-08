Imports Newtonsoft.Json

Namespace KWeb.Serializable

    <Serializable>
    Public Class RequestDebug

        <Serializable>
        Private Class LogItem

            <JsonProperty("level")>
            Public Property Level As String

            <JsonProperty("message")>
            Public Property Message As String

        End Class

        <JsonProperty("logs")>
        Private Property Logs As List(Of LogItem)

        Public Function GetLogs() As List(Of Tuple(Of String, String))
            Dim Result As New List(Of Tuple(Of String, String))

            For Each l In Logs
                Result.Add(New Tuple(Of String, String)(l.Level, l.Message))
            Next

            Return Result
        End Function

    End Class

End Namespace