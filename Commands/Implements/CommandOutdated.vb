Namespace Commands

    Public Class CommandOutdated
        Inherits Command

        Private ReadOnly K As EasyK

        Public Sub New(K As EasyK)
            MyBase.New("outdated", "outdated - 列出已唱歌曲", CommandType.User)
            Me.K = K
        End Sub

        Protected Overrides Sub Process(Args() As String)
            Dim List As List(Of EasyKBookRecord) = K.GetOutdatedList()
            If List.Count = 0 Then
                Console.WriteLine("已唱列表为空")
                Return
            End If


            Dim Result As New List(Of String) From {
                "=====已唱歌曲====="
            }

            Dim i As Integer = 1
            For Each Record As EasyKBookRecord In List
                With Record
                    Result.Add($"#{i}  { .Title} (ID:{ .Id} Content:{ .Content})")
                    Result.Add($"来源: { .Order} 播放方式: {If(.Type = EasyKType.Bilibili, "bilibili", "VLC")}")
                End With

                i += 1
            Next
            Result.Add($"共 {List.Count} 首已播放")

            Logger.PrintOriginalLines(Result.ToArray())
        End Sub

    End Class

End Namespace
