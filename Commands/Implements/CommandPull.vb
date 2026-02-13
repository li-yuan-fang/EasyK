Namespace Commands

    Public Class CommandPull
        Inherits Command

        Private ReadOnly K As EasyK

        Public Sub New(K As EasyK)
            MyBase.New("pull", "pull - 重新拉取音乐信息(仅限DLNA音乐模式)", CommandType.System)
            Me.K = K
        End Sub

        Protected Overrides Sub Process(Args() As String)
            K.TriggerMirrorPlay("Refresh")
            Console.WriteLine("刷新指令已发送")
        End Sub

    End Class

End Namespace
