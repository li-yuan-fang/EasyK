Public Class CommandReset
    Inherits Command

    Private ReadOnly K As EasyK

    Public Sub New(K As EasyK)
        MyBase.New("reset", "reset [now] - 点歌系统复位", CommandType.System)
        Me.K = K
    End Sub

    Protected Overrides Sub Process(Args() As String)
        Dim Immediately As Boolean = Args.Length >= 2 AndAlso Args(1).ToLower().Equals("now")

        K.Reset(Immediately)
        If Immediately Then
            Console.WriteLine("点歌系统已复位")
        Else
            Console.WriteLine("点歌系统将在当前源播放结束后复位")
        End If
    End Sub

End Class
