Namespace Commands

    Public Class CommandSeek
        Inherits Command

        Private ReadOnly K As EasyK

        Public Sub New(K As EasyK)
            MyBase.New("seek", "seek <-/+/具体秒数>  - 快进/快退(暂时不支持bilibili)", CommandType.User)
            Me.K = K
        End Sub

        Protected Overrides Sub Process(Args() As String)
            If Args.Length < 2 Then
                InvalidUsage()
                Return
            End If

            Select Case Args(1).ToLower()
                Case "-"
                    K.Seek(True)
                Case "+"
                    K.Seek(False)
                Case Else
                    Dim [Step] As Double

                    Try
                        [Step] = Double.Parse(Args(1))
                    Catch
                        InvalidUsage()
                        Return
                    End Try

                    If [Step] <> 0 Then K.Seek([Step] < 0, [Step])
            End Select

            Console.WriteLine("操作成功")
        End Sub

    End Class

End Namespace
