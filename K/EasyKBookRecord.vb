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

    ''' <summary>
    ''' 初始化点歌记录
    ''' </summary>
    ''' <param name="Title">标题</param>
    ''' <param name="Order">点歌人</param>
    ''' <param name="Type">类型</param>
    ''' <param name="Content">内容</param>
    ''' <remarks>ID自动生成</remarks>
    Public Sub New(Title As String, Order As String, Type As EasyKType, Content As String)
        Dim Id As String = Now.Ticks.ToString("x2")
        Me.Id = Id
        Me.Title = Title
        Me.Order = Order
        Me.Type = Type
        Me.Content = Content
    End Sub

    ''' <summary>
    ''' 重新生成点歌记录
    ''' </summary>
    ''' <param name="Original">原始记录</param>
    ''' <param name="Content">内容</param>
    ''' <remarks>用于更新内容</remarks>
    Public Sub New(Original As EasyKBookRecord, Content As String)
        With Original
            Id = .Id
            Title = .Title
            Order = .Order
            Type = .Type

            Me.Content = Content
        End With
    End Sub

    ''' <summary>
    ''' 生成点歌记录
    ''' </summary>
    ''' <param name="Saved">储存的点歌记录</param>
    ''' <remarks>阻止复制相同ID</remarks>
    Public Sub New(Saved As SavedKRecords.Record)
        With Saved
            Id = .Id
            Title = .Title
            Order = .Order
            Type = [Enum].Parse(GetType(EasyKType), .Type)
            Content = .Content
        End With
    End Sub

End Class
