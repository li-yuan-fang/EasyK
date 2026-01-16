Imports CefSharp
Imports Newtonsoft.Json

<Serializable>
Public Class CookieContainer

    <JsonProperty("bili")>
    Public Property Bili As List(Of Cookie) = New List(Of Cookie)

End Class
