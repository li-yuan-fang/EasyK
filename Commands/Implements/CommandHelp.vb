Namespace Commands

    Public Class CommandHelp
        Inherits Command

        Private Const Separator As String = "=====帮助菜单====="

        Private ReadOnly Commands As List(Of Command)

        Public Sub New(Commands As List(Of Command))
            MyBase.New("help", "help - 帮助", CommandType.None)
            Me.Commands = Commands
        End Sub

        Private Sub PrintType(Result As List(Of String), Type As CommandType)
            Select Case Type
                Case CommandType.System
                    Result.Add("#系统指令")
                Case CommandType.User
                    Result.Add("#点歌指令")
            End Select

            For Each Command In Commands
                With Command
                    If .Type <> Type Then Continue For

                    Result.Add(.Usage)
                End With
            Next

            Result.Add("")
        End Sub

        Protected Overrides Sub Process(Args() As String)
            Dim Result As New List(Of String) From {
                Separator
            }

            For Each t As CommandType In [Enum].GetValues(GetType(CommandType))
                PrintType(Result, t)
            Next
            Result.Add(Separator)

            Logger.PrintOriginalLines(Result.ToArray())
        End Sub

    End Class

End Namespace
