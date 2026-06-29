Namespace Commands

    Public Class CommandFair
        Inherits Command

        Private ReadOnly K As EasyK

        Public Sub New(K As EasyK)
            MyBase.New("fair", "fair [true/false] - 查看/开关公平模式", CommandType.System)
            Me.K = K
        End Sub

        Protected Overrides Sub Process(Args() As String)
            With Settings.Settings
                If Args.Length < 2 Then
                    Console.WriteLine("公平模式: {0}", .FairnessMode.ToString().ToLower())
                Else
                    Try
                        .FairnessMode = Boolean.Parse(Args(1))
                        If .FairnessMode Then K.ReRankFair()

                        Console.WriteLine("公平模式已设置为: {0}", .FairnessMode.ToString().ToLower())
                    Catch
                        InvalidUsage()
                    End Try
                End If
            End With
        End Sub

    End Class

End Namespace
