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
        ''' 获取歌词行
        ''' </summary>
        ''' <returns></returns>
        <JsonProperty("text")>
        Public ReadOnly Property Text As List(Of String)

        Protected Sub New(Time As Double, Text As List(Of String))
            Me.Time = Time
            Me.Text = Text
        End Sub

        ''' <summary>
        ''' 生成歌词对象
        ''' </summary>
        ''' <param name="Lyrics">歌词列表</param>
        ''' <returns></returns>
        Public Shared Function Generate(Lyrics As Dictionary(Of Double, List(Of String))) As List(Of DLNALyric)
            Dim Result As New List(Of DLNALyric)

            For Each l In Lyrics
                Result.Add(New DLNALyric(l.Key, l.Value))
            Next

            Return Result
        End Function

    End Class

End Namespace
