Namespace Commands

    Public Class CommandPause
        Inherits Command

        Private ReadOnly K As EasyK

        Public Sub New(K As EasyK)
            MyBase.New("pause", "pause - 暂停", CommandType.User)
            Me.K = K
        End Sub

        Protected Overrides Sub Process(Args() As String)
            K.Pause()
            Console.WriteLine("暂停执行成功")
        End Sub

    End Class

End Namespace
