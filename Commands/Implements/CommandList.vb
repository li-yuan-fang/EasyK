Namespace Commands

    Public Class CommandList
        Inherits Command

        Private ReadOnly K As EasyK

        Public Sub New(K As EasyK)
            MyBase.New("list", "list - 列出已点歌曲", CommandType.User)
            Me.K = K
        End Sub

        Protected Overrides Sub Process(Args() As String)
            Dim List As List(Of EasyKBookRecord) = K.GetBookList()
            If List.Count = 0 Then
                Console.WriteLine("已点列表为空")
                Return
            End If

            Dim Result As New List(Of String)

            Dim Current As EasyKBookRecord = K.GetCurrent()
            If Current IsNot Nothing Then
                With Result
                    .Add("=====正在播放=====")
                    .Add($"{Current.Title} (ID:{Current.Id} Content:{Current.Content})")
                    .Add($"来源: {Current.Order} 播放方式: {If(Current.Type = EasyKType.Bilibili, "bilibili", "VLC")}")
                End With
            End If

            Result.Add("=====已点歌曲=====")
            Dim i As Integer = 1
            For Each Record As EasyKBookRecord In List
                With Record
                    Result.Add($"#{i}  { .Title} (ID:{ .Id} Content:{ .Content})")
                    Result.Add($"来源: { .Order} 播放方式: {If(.Type = EasyKType.Bilibili, "bilibili", "VLC")}")
                End With

                i += 1
            Next
            Result.Add($"共 {List.Count} 首待播放")

            Logger.PrintOriginalLines(Result.ToArray())
        End Sub

    End Class

End Namespace
