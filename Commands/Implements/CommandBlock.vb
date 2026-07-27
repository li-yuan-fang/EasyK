Namespace Commands

    Public Class CommandBlock
        Inherits Command

        Private ReadOnly K As EasyK

        Public Sub New(K As EasyK)
            MyBase.New("block", "block [ID] - 锁定顶歌", CommandType.System)
            Me.K = K
        End Sub

        Protected Overrides Sub Process(Args() As String)
            If Args.Length < 2 Then
                If K.Block() Then
                    Console.WriteLine("操作成功")
                Else
                    Console.WriteLine("操作失败")
                End If
            Else
                If K.Block(Args(1)) Then
                    Console.WriteLine("操作成功")
                Else
                    Console.WriteLine("操作失败 - 找不到指定的歌曲")
                End If
            End If
        End Sub

    End Class

End Namespace
