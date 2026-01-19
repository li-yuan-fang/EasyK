Imports Newtonsoft.Json

Namespace DLNA.MusicProvider

    <Serializable>
    Public Class DLNALyric

        ''' <summary>
        ''' 获取歌词时间
        ''' </summary>
        ''' <returns></returns>
        <JsonProperty("time")>
        Public ReadOnly Property Time As Double

        ''' <summary>
        ''' 获取简单歌词行
        ''' </summary>
        ''' <returns></returns>
        <JsonProperty("plain")>
        Public ReadOnly Property Plain As List(Of String)

        ''' <summary>
        ''' 获取逐字歌词行
        ''' </summary>
        ''' <returns></returns>
        <JsonProperty("verbatim")>
        Public ReadOnly Property Verbatim As List(Of String)

        Protected Sub New(Time As Double, Plain As List(Of String))
            Me.Time = Time
            Me.Plain = Plain
        End Sub

        ''' <summary>
        ''' 生成简单歌词对象
        ''' </summary>
        ''' <param name="Lyrics">歌词列表</param>
        ''' <returns></returns>
        Public Shared Function GeneratePlain(Lyrics As Dictionary(Of Double, List(Of String))) As List(Of DLNALyric)
            Dim Result As New List(Of DLNALyric)

            For Each l In Lyrics
                Result.Add(New DLNALyric(l.Key, l.Value))
            Next

            Return Result
        End Function

    End Class

End Namespace
