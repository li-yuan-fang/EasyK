Imports System.Windows.Documents
Imports Newtonsoft.Json

Namespace DLNA.MusicProvider

    ''' <summary>
    ''' DLNA逐字歌词单元
    ''' </summary>
    <Serializable>
    Public Class DLNALyricVerbatimBase

        ''' <summary>
        ''' 获取开始时间
        ''' </summary>
        ''' <returns></returns>
        <JsonProperty("start", NullValueHandling:=NullValueHandling.Ignore)>
        Public ReadOnly Property Start As Double

        ''' <summary>
        ''' 获取结束时间
        ''' </summary>
        ''' <returns></returns>
        <JsonProperty("end", NullValueHandling:=NullValueHandling.Ignore)>
        Public ReadOnly Property [End] As Double

        ''' <summary>
        ''' 获取文字内容
        ''' </summary>
        ''' <returns></returns>
        <JsonProperty("content", NullValueHandling:=NullValueHandling.Ignore)>
        Public ReadOnly Property Content As String

        ''' <summary>
        ''' 构建逐字歌词单元
        ''' </summary>
        ''' <param name="Start">开始时间</param>
        ''' <param name="[End]">结束时间</param>
        ''' <param name="Content">文字内容</param>
        Public Sub New(Start As Double, [End] As Double, Content As String)
            Me.Start = Start
            Me.[End] = [End]
            Me.Content = Content
        End Sub

    End Class

    ''' <summary>
    ''' DLNA逐字歌词注音体
    ''' </summary>
    <Serializable>
    Public Class DLNALyricVerbatimBody

        ''' <summary>
        ''' 获取开始时间
        ''' </summary>
        ''' <returns></returns>
        <JsonIgnore>
        Public ReadOnly Property Start As Double
            Get
                Return If(Text.Count > 0, Text.First.Start, 0D)
            End Get
        End Property

        ''' <summary>
        ''' 获取结束时间
        ''' </summary>
        ''' <returns></returns>
        <JsonIgnore>
        Public ReadOnly Property [End] As Double
            Get
                Return If(Text.Count > 0, Text.Last.End, 0D)
            End Get
        End Property

        ''' <summary>
        ''' 获取文字内容
        ''' </summary>
        ''' <returns></returns>
        <JsonProperty("text")>
        Public ReadOnly Property Text As List(Of DLNALyricVerbatimBase)

        ''' <summary>
        ''' 获取假名
        ''' </summary>
        ''' <returns></returns>
        <JsonProperty("kana", NullValueHandling:=NullValueHandling.Ignore)>
        Public Property Kana As List(Of DLNALyricVerbatimBase)

        ''' <summary>
        ''' 初始化逐字歌词注音体
        ''' </summary>
        ''' <param name="Text">文字内容</param>
        Public Sub New(Text As List(Of DLNALyricVerbatimBase))
            Me.Text = Text
            Me.Kana = Nothing
        End Sub

        ''' <summary>
        ''' 从逐字歌词单元构建
        ''' </summary>
        ''' <param name="Base">逐字歌词单元</param>
        ''' <returns></returns>
        Public Shared Function FromBase(Base As List(Of DLNALyricVerbatimBase)) As List(Of DLNALyricVerbatimBody)
            Dim Result As New List(Of DLNALyricVerbatimBody)
            For Each b In Base
                Result.Add(FromBase(b))
            Next
            Return Result
        End Function

        ''' <summary>
        ''' 从逐字歌词单元构建
        ''' </summary>
        ''' <param name="Base">逐字歌词单元</param>
        ''' <returns></returns>
        Public Shared Function FromBase(ParamArray Base() As DLNALyricVerbatimBase) As List(Of DLNALyricVerbatimBody)
            Dim Result As New List(Of DLNALyricVerbatimBody)
            For Each b In Base
                Result.Add(FromBase(b))
            Next
            Return Result
        End Function

        ''' <summary>
        ''' 从逐字歌词单元构建
        ''' </summary>
        ''' <param name="Base">逐字歌词单元</param>
        ''' <returns></returns>
        Public Shared Function FromBase(Base As DLNALyricVerbatimBase) As DLNALyricVerbatimBody
            Return New DLNALyricVerbatimBody(New List(Of DLNALyricVerbatimBase) From {Base})
        End Function

        ''' <summary>
        ''' 合并逐字歌词单元为注音体
        ''' </summary>
        ''' <param name="Base">逐字歌词单元</param>
        ''' <returns></returns>
        Public Shared Function Combine(ParamArray Base() As DLNALyricVerbatimBase) As DLNALyricVerbatimBody
            Return New DLNALyricVerbatimBody(Base.ToList)
        End Function

        ''' <summary>
        ''' 去除假名
        ''' </summary>
        ''' <returns></returns>
        Public Function RemoveKana() As DLNALyricVerbatimBody
            Return New DLNALyricVerbatimBody(Text)
        End Function

    End Class

    ''' <summary>
    ''' DLNA歌词行
    ''' </summary>
    <Serializable>
    Public Class DLNALyric

        ''' <summary>
        ''' 获取歌词时间
        ''' </summary>
        ''' <returns></returns>
        <JsonProperty("time", NullValueHandling:=NullValueHandling.Ignore)>
        Public ReadOnly Property Time As Double

        ''' <summary>
        ''' 获取简单歌词行
        ''' </summary>
        ''' <returns></returns>
        <JsonProperty("plain", NullValueHandling:=NullValueHandling.Ignore)>
        Public ReadOnly Property Plain As List(Of String)

        ''' <summary>
        ''' 获取带假名逐字歌词行
        ''' </summary>
        ''' <returns></returns>
        <JsonProperty("verbatimK", NullValueHandling:=NullValueHandling.Ignore)>
        Public ReadOnly Property VerbatimK As List(Of DLNALyricVerbatimBody)

        ''' <summary>
        ''' 获取逐字歌词行
        ''' </summary>
        ''' <returns></returns>
        <JsonProperty("verbatim", NullValueHandling:=NullValueHandling.Ignore)>
        Public ReadOnly Property Verbatim As List(Of DLNALyricVerbatimBase)

        Protected Sub New(Time As Double, Plain As List(Of String))
            Me.Time = Time
            Me.Plain = Plain
        End Sub

        ''' <summary>
        ''' 初始化复杂歌词对象
        ''' </summary>
        ''' <param name="Time">行开始时间</param>
        ''' <param name="Plain">简易歌词集合</param>
        ''' <param name="VerbatimK">带假名逐字歌词歌词单元集合</param>
        ''' <param name="Verbatim">逐字歌词集合</param>
        ''' <remarks>歌词从上到下显示顺序为 带假名逐字(正文) -> 逐字(罗马音/拼音) -> 简易(翻译/无逐字时正文)</remarks>
        Public Sub New(Time As Double, Plain As List(Of String), VerbatimK As List(Of DLNALyricVerbatimBody), Verbatim As List(Of DLNALyricVerbatimBase))
            Me.New(Time, Plain)
            Me.VerbatimK = VerbatimK
            Me.Verbatim = Verbatim
        End Sub

        ''' <summary>
        ''' 生成简易歌词对象
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
