Namespace Commands

    Public Class CommandExit
        Inherits Command

        Private ReadOnly K As EasyK

        Private ReadOnly ExitAction As Action

        Public Sub New(K As EasyK, ExitAction As Action)
            MyBase.New("exit", "exit [save] - 退出(填写save则保存点歌记录)", CommandType.System)
            Me.K = K
            Me.ExitAction = ExitAction
        End Sub

        Protected Overrides Sub Process(Args() As String)
            If Args.Length < 2 Then
                ExitAction.Invoke()
                Return
            End If

            If Args(1).ToLower() <> "save" Then
                InvalidUsage()
                Return
            End If

            K.Save()
            ExitAction.Invoke()
        End Sub

    End Class

End Namespace
