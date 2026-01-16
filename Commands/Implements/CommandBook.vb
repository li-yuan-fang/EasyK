Public Class CommandBook
    Inherits Command

    Private ReadOnly K As EasyK

    Public Sub New(K As EasyK)
        MyBase.New("book", "book <歌名> <bili/local/dlna> [内容] - 点歌", CommandType.User)
        Me.K = K
    End Sub

    Protected Overrides Sub Process(Args() As String)
        If Args.Length < 3 Then
            InvalidUsage()
            Return
        End If

        Dim Content As String = vbNullString
        Dim Type As EasyKType
        Select Case Args(2).ToLower()
            Case = "bili"
                Type = EasyKType.Bilibili

                If Args.Length < 4 Then
                    InvalidUsage()
                    Return
                End If
                Content = Args(3)
            Case = "local"
                Type = EasyKType.Video

                If Args.Length < 4 Then
                    InvalidUsage()
                    Return
                End If
                Content = Args(3)
            Case = "dlna"
                Type = EasyKType.DLNA
                If Args.Length >= 4 Then Content = Args(3)
            Case Else
                InvalidUsage()
                Return
        End Select

        Console.WriteLine("点歌成功 - {0}", K.Book(Args(1), "控制台", Type, Content))
    End Sub

End Class
