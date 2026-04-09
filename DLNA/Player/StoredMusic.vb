Imports EasyK.DLNA.MusicProvider
Imports Newtonsoft.Json

Namespace DLNA.Player

    <Serializable>
    Public Class StoredMusic

        <JsonProperty("attribute")>
        Public Property Attribute As DLNAMusicAttribute = Nothing

        <JsonProperty("meta")>
        Public Property Meta As String

        <JsonProperty("resource")>
        Public Property Resource As String

        <JsonProperty("original")>
        Public Property Original As String

        <JsonProperty("lyric_color")>
        Public Property LyricColor As String = vbNullString

    End Class

End Namespace