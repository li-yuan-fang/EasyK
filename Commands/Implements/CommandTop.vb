Namespace Commands

    Public Class CommandTop
        Inherits Command

        Private ReadOnly K As EasyK

        Public Sub New(K As EasyK)
            MyBase.New("top", "top <ID> - 顶歌", CommandType.User)
            Me.K = K
        End Sub

        Protected Overrides Sub Process(Args() As String)
            If Args.Length < 2 Then
                InvalidUsage()
                Return
            End If

            Dim Result = K.SendToTop(Args(1))
            If Result IsNot Nothing Then
                Console.WriteLine("顶歌成功 - {0}", $"{Result.Title}(来自:{Result.Order})")
            Else
                Console.WriteLine("顶歌失败 ID不存在 - {0}", Args(1))
            End If
        End Sub

    End Class

End Namespace
