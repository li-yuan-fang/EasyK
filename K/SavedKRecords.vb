Imports Newtonsoft.Json

<Serializable>
Public Class SavedKRecords

    <Serializable>
    Public Class Record

        <JsonProperty("id")>
        Public ReadOnly Id As String

        <JsonProperty("title")>
        Public Property Title As String

        <JsonProperty("order")>
        Public Property Order As String

        <JsonProperty("type")>
        Public Property Type As Integer

        <JsonProperty("content")>
        Public Property Content As String

        Public Sub New()
        End Sub

        Public Sub New(Original As EasyKBookRecord)
            With Original
                Id = .Id
                Title = .Title
                Order = .Order
                Type = .Type
                Content = .Content
            End With
        End Sub

        Public Function Recover() As EasyKBookRecord
            Try
                Return New EasyKBookRecord(Me)
            Catch
                Return Nothing
            End Try
        End Function

        Public Overrides Function ToString() As String
            Return JsonConvert.SerializeObject(Me)
        End Function

    End Class

    <JsonProperty("queue")>
    Public Property Queue As List(Of Record) = New List(Of Record)

    <JsonProperty("outdated")>
    Public Property Outdated As List(Of Record) = New List(Of Record)

End Class
