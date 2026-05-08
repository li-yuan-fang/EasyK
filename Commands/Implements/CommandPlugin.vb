Namespace Commands

    Public Class CommandPlugin
        Inherits Command

        Public Sub New()
            MyBase.New("plugin", "plugin <插件ID> [参数..] - 运行插件指令", CommandType.System)
        End Sub

        Protected Overrides Sub Process(Args() As String)
            If Args.Length < 2 Then
                InvalidUsage()
                Return
            End If

            Console.WriteLine(DLNA.MusicProvider.DLNAMusicProviders.RunCommand(Args(1), Args.Skip(2).ToArray()))
        End Sub

    End Class

End Namespace
