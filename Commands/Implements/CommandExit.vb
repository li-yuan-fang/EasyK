Public Class CommandExit
    Inherits Command

    Private ReadOnly ExitAction As Action

    Public Sub New(ExitAction)
        MyBase.New("exit", "exit - 退出", CommandType.System)
        Me.ExitAction = ExitAction
    End Sub

    Protected Overrides Sub Process(Args() As String)
        Console.WriteLine("正在关闭点歌系统...")
        ExitAction.Invoke()
    End Sub

End Class
