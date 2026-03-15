Namespace Commands

    Public Class CommandBili
        Inherits Command

        Private ReadOnly K As EasyK

        Public Sub New(K As EasyK)
            MyBase.New("bili", "bili - 登录bilibili", CommandType.System)
            Me.K = K
        End Sub

        Protected Overrides Sub Process(Args() As String)
            Using Login As New FrmLogin("https://www.bilibili.com/")
                Login.ShowDialog()
            End Using

            K.Reset(False)
        End Sub

    End Class

End Namespace
