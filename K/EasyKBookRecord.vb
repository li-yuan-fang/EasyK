Imports Newtonsoft.Json

<Serializable>
Public Class EasyKBookRecord

    <JsonProperty("id")>
    Public ReadOnly Id As String

    <JsonProperty("title")>
    Public ReadOnly Title As String

    <JsonProperty("order")>
    Public ReadOnly Order As String

    <JsonIgnore>
    Public ReadOnly Type As EasyKType

    <JsonIgnore>
    Public ReadOnly Content As String

    Public Sub New(Title As String, Order As String, Type As EasyKType, Content As String)
        Dim Id As String = Now.Ticks.ToString("x2")
        Me.Id = Id
        Me.Title = Title
        Me.Order = Order
        Me.Type = Type
        Me.Content = Content
    End Sub

    Public Sub New(Original As EasyKBookRecord, Content As String)
        With Original
            Id = .Id
            Title = .Title
            Order = .Order
            Type = .Type

            Me.Content = Content
        End With
    End Sub

End Class
