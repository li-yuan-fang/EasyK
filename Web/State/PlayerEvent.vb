Imports CefSharp.DevTools.Database

Namespace KWeb.State

    Friend MustInherit Class PlayerEvent

        Private BufferedData As String = vbNullString

        ''' <summary>
        ''' 获取事件类型
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Type As PlayerEventType

        ''' <summary>
        ''' 获取数据响应
        ''' </summary>
        ''' <returns></returns>
        Public Function GetResponse() As String
            '牺牲部分实时性 降低IO访问量
            If String.IsNullOrEmpty(BufferedData) Then BufferedData = GetData()

            Return $"event: {Type.ToString().ToLower()}{vbLf}data: {BufferedData}{vbLf}{vbLf}"
        End Function

        ''' <summary>
        ''' 获取响应数据部分
        ''' </summary>
        ''' <returns></returns>
        Protected MustOverride Function GetData() As String

        ''' <summary>
        ''' 初始化事件
        ''' </summary>
        ''' <param name="Type">事件类型</param>
        Protected Sub New(Type As PlayerEventType)
            Me.Type = Type
        End Sub

    End Class

End Namespace
