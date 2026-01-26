Namespace Commands

    Public Class CommandLock
        Inherits Command

        Private ReadOnly K As EasyK

        Public Sub New(K As EasyK)
            MyBase.New("lock", "lock - 更改播放器置顶锁定状态", CommandType.System)
            Me.K = K
        End Sub

        Protected Overrides Sub Process(Args() As String)
            Console.WriteLine("播放器窗体置顶锁定状态已更改: {0}", K.Lock())
        End Sub

    End Class

End Namespace
