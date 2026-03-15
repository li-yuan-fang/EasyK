Namespace Commands

    Public Class CommandExit
        Inherits Command

        Private ReadOnly ExitAction As Action

        Public Sub New(ExitAction As Action)
            MyBase.New("exit", "exit - 退出", CommandType.System)
            Me.ExitAction = ExitAction
        End Sub

        Protected Overrides Sub Process(Args() As String)
            ExitAction.Invoke()
        End Sub

    End Class

End Namespace
