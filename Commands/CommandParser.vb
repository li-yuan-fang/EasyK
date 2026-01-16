Public Class CommandParser

    Private ReadOnly K As EasyK

    Private ReadOnly Web As KWebCore

    Private ReadOnly Settings As SettingContainer

    Private ReadOnly Commands As New List(Of Command)

    Private ReadOnly ExitAction As New Action(Sub() RaiseEvent OnExit())

    ''' <summary>
    ''' 退出事件
    ''' </summary>
    Public Event OnExit()

    ''' <summary>
    ''' 初始化
    ''' </summary>
    ''' <param name="K"></param>
    ''' <param name="Web"></param>
    Public Sub New(K As EasyK, Web As KWebCore, Settings As SettingContainer)
        Me.K = K
        Me.Web = Web
        Me.Settings = Settings

        LoadCommands()
    End Sub

    Private Sub LoadCommands()
        With Commands
            .Add(New CommandHelp(Commands))

            .Add(New CommandPass(Settings))
            .Add(New CommandBili(K, Settings))
            .Add(New CommandClean(K, Web, Settings))
            .Add(New CommandLock(K))
            .Add(New CommandQR(K, Settings))
            .Add(New CommandStrict(Settings))
            .Add(New CommandReset(K))
            .Add(New CommandExit(ExitAction))

            .Add(New CommandList(K))
            .Add(New CommandBook(K))
            .Add(New CommandTop(K))
            .Add(New CommandPush(K))
            .Add(New CommandPause(K))
            .Add(New CommandSeek(K))
            .Add(New CommandRemove(K))
            .Add(New CommandOutdated(K))
            .Add(New CommandReorder(K))
        End With
    End Sub

    ''' <summary>
    ''' 运行指令系统
    ''' </summary>
    Public Sub Run()
        Do
            Dim cmd As String = Console.ReadLine()
            Dim Success As Boolean = False
            For Each Parser As Command In Commands
                If Parser.Match(cmd) Then
                    Success = True
                    Exit For
                End If
            Next

            If Not Success Then Console.WriteLine("未知指令 帮助指令为: help")
        Loop
    End Sub

End Class
