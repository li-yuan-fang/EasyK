Namespace Commands

    Public Class CommandHelp
        Inherits Command

        Private ReadOnly Commands As List(Of Command)

        Public Sub New(Commands As List(Of Command))
            MyBase.New("help", "help - 帮助", CommandType.None)
            Me.Commands = Commands
        End Sub

        Private Sub Separator()
            Console.WriteLine("=====帮助菜单=====")
        End Sub

        Private Sub PrintType(Type As CommandType)
            Select Case Type
                Case CommandType.System
                    Console.WriteLine("#系统指令")
                Case CommandType.User
                    Console.WriteLine("#点歌指令")
            End Select

            For Each Command In Commands
                With Command
                    If .Type <> Type Then Continue For

                    Console.WriteLine(.Usage)
                End With
            Next

            Console.WriteLine()
        End Sub

        Protected Overrides Sub Process(Args() As String)
            Separator()
            For Each t As CommandType In [Enum].GetValues(GetType(CommandType))
                PrintType(t)
            Next
            Separator()
        End Sub

    End Class

End Namespace
