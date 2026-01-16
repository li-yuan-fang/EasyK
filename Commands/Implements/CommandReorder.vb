Public Class CommandReorder
    Inherits Command

    Private ReadOnly K As EasyK

    Public Sub New(K As EasyK)
        MyBase.New("reorder", "reorder <ID> - 重新点歌", CommandType.User)
        Me.K = K
    End Sub

    Protected Overrides Sub Process(Args() As String)
        If Args.Length < 2 Then
            InvalidUsage()
            Return
        End If

        Dim Id As String = K.Reorder(Args(1), "控制台")
        If Not String.IsNullOrEmpty(Id) Then
            Console.WriteLine("重新点歌成功 - {0} => {1}", Args(1), Id)
        Else
            Console.WriteLine("重新点歌失败 ID不存在 - {0}", Args(1))
        End If
    End Sub

End Class
