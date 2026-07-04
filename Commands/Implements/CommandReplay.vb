Namespace Commands

    Public Class CommandReplay
        Inherits Command

        Private ReadOnly K As EasyK

        Public Sub New(K As EasyK)
            MyBase.New("replay", "replay - 重唱当前歌曲", CommandType.User)
            Me.K = K
        End Sub

        Protected Overrides Sub Process(Args() As String)
            K.Replay()
            Console.WriteLine("操作成功完成")
        End Sub

    End Class

End Namespace
