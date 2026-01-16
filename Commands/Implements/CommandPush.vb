Public Class CommandPush
    Inherits Command

    Private ReadOnly K As EasyK

    Public Sub New(K As EasyK)
        MyBase.New("push", "push - 切歌", CommandType.User)
        Me.K = K
    End Sub

    Protected Overrides Sub Process(Args() As String)
        K.Push()
        Console.WriteLine("切歌执行成功")
    End Sub

End Class
