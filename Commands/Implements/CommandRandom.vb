Namespace Commands

    Public Class CommandRandom
        Inherits Command

        Private ReadOnly K As EasyK

        Public Sub New(K As EasyK)
            MyBase.New("random", "random [banlanced/common] - 随机排序已点歌曲(如不指定算法则按默认配置处理)", CommandType.User)
            Me.K = K
        End Sub

        Protected Overrides Sub Process(Args() As String)
            Dim Start As Long = Now.Ticks

            If Args.Length < 2 Then
                K.Random()
            Else
                Select Case Args(1).ToLower()
                    Case "banlanced"
                        K.Random(True)
                    Case "common"
                        K.Random(False)
                    Case Else
                        InvalidUsage()
                        Return
                End Select
            End If

            Console.WriteLine("随机排序操作完成 - 用时 {0} ms", ((Now.Ticks - Start) / 10 ^ 4).ToString("0.0"))
        End Sub

    End Class

End Namespace
