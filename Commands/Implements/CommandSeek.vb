Namespace Commands

    Public Class CommandSeek
        Inherits Command

        Private ReadOnly K As EasyK

        Public Sub New(K As EasyK)
            MyBase.New("seek", "seek <prev/next>  - 快进/快退(暂时不支持bilibili)", CommandType.User)
            Me.K = K
        End Sub

        Protected Overrides Sub Process(Args() As String)
            If Args.Length < 2 Then
                InvalidUsage()
                Return
            End If

            Select Case Args(1).ToLower()
                Case "prev", "p", "-"
                    K.Seek(True)
                Case "next", "n", "+"
                    K.Seek(False)
                Case Else
                    InvalidUsage()
                    Return
            End Select

            Console.WriteLine("操作成功")
        End Sub

    End Class

End Namespace
