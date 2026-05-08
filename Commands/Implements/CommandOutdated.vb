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
                Console.WriteLine("已点列表为空")
                Return
            End If

            Console.WriteLine("=====已唱歌曲=====")
            Dim i As Integer = 1
            For Each Record As EasyKBookRecord In List
                With Record
                    Console.WriteLine("#{0}  {1} (ID:{2} Content:{3})", i, .Title, .Id, .Content)
                    Console.WriteLine("来源: {0} 播放方式: {1}", .Order, If(.Type = EasyKType.Bilibili, "bilibili", "本地"))
                End With

                i += 1
            Next
            Console.WriteLine("共 {0} 首已播放", List.Count)
        End Sub

    End Class

End Namespace
