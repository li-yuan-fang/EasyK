Public Class CommandRemove
    Inherits Command

    Private ReadOnly K As EasyK

    Public Sub New(K As EasyK)
        MyBase.New("remove", "remove <ID> - 删除", CommandType.User)
        Me.K = K
    End Sub

    Protected Overrides Sub Process(Args() As String)
        If Args.Length < 2 Then
            InvalidUsage()
            Return
        End If

        If (K.Remove(Args(1))) Then
            Console.WriteLine("移除成功 - {0}", Args(1))
        Else
            Console.WriteLine("移除失败 ID不存在 - {0}", Args(1))
        End If
    End Sub

End Class
