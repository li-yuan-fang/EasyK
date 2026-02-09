Imports Newtonsoft.Json

Namespace DLNA.MusicProvider

    <Serializable>
    Public Class DLNAMusicAttribute

        ''' <summary>
        ''' 获取专辑封面
        ''' </summary>
        ''' <returns></returns>
        <JsonProperty("album")>
        Public ReadOnly Property Album As String

        ''' <summary>
        ''' 获取标题
        ''' </summary>
        ''' <returns></returns>
        <JsonProperty("title")>
        Public ReadOnly Property Title As String

        ''' <summary>
        ''' 获取创作者
        ''' </summary>
        ''' <returns></returns>
        <JsonProperty("artist")>
        Public ReadOnly Property Artist As String

        ''' <summary>
        ''' 获取歌曲长度
        ''' </summary>
        ''' <returns></returns>
        <JsonProperty("duration")>
        Public ReadOnly Property Duration As Long

        ''' <summary>
        ''' 初始化
        ''' </summary>
        ''' <param name="Album">专辑封面</param>
        ''' <param name="Title">标题</param>
        ''' <param name="Artist">创作者</param>
        ''' <param name="Duration">歌曲长度</param>
        Public Sub New(Album As String, Title As String, Artist As String, Duration As Long)
            Me.Album = Album
            Me.Title = Title
            Me.Artist = Artist
            Me.Duration = Duration
        End Sub

        ''' <summary>
        ''' 生成字符串
        ''' </summary>
        ''' <returns></returns>
        Public Overrides Function ToString() As String
            Return JsonConvert.SerializeObject(Me)
        End Function

    End Class

End Namespace
