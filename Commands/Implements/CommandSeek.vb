Namespace Commands

    Public Class CommandSeek
        Inherits Command

        Private ReadOnly K As EasyK

        Public Sub New(K As EasyK)
            MyBase.New("seek", "seek <-/+/具体秒数>  - 快进/快退", CommandType.User)
            Me.K = K
        End Sub

        Private Sub Seek(Prev As Boolean, Optional [Step] As Double = 5D)
            With K.Player
                Dim Offset As Single = CSng(Math.Abs([Step]) / .Duration)
                If Prev Then
                    .Position = Math.Max(Math.Min(.Position - Offset, 1), 0)
                Else
                    .Position = Math.Max(Math.Min(.Position + Offset, 1), 0)
                End If
            End With
        End Sub

        Protected Overrides Sub Process(Args() As String)
            If Args.Length < 2 Then
                InvalidUsage()
                Return
            End If

            Select Case Args(1).ToLower()
                Case "-"
                    Seek(True)
                Case "+"
                    Seek(False)
                Case Else
                    Dim [Step] As Double

                    Try
                        [Step] = Double.Parse(Args(1))
                    Catch
                        InvalidUsage()
                        Return
                    End Try

                    If [Step] <> 0 Then Seek([Step] < 0, [Step])
            End Select

            Console.WriteLine("操作成功")
        End Sub

    End Class

End Namespace
