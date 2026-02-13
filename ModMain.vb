Imports System.Net
Imports System.Windows.Forms
Imports EasyK.ConsoleUtils

Module ModMain

    Public WithEvents Commands As Commands.CommandParser

    Public KCore As EasyK

    Public Settings As SettingContainer

    Public WebServer As KWebCore

    Public DLNAServer As DLNA.DLNA

    <STAThread>
    Sub Main()
        Console.Title = "EasyK"
        Console.ForegroundColor = ConsoleColor.White

        '加载配置
        Settings = New SettingContainer()

        '运行点歌主服务
        KCore = New EasyK(Settings)

        '运行网络服务器
        WebServer = New KWebCore(KCore, Settings)
        AddHandler WebServer.OnUncaughtError, AddressOf ExitApplication

        '加载DLNA插件
        DLNA.MusicProvider.DLNAMusicProviders.LoadProviders(Settings)

        '运行DLNA服务器
        DLNAServer = New DLNA.DLNA(KCore, Settings)

        '运行指令系统
        Commands = New Commands.CommandParser(KCore, WebServer, Settings)
        With Commands
            AddHandler .OnExit, AddressOf ExitApplication
            Task.Run(Sub() .Run())
        End With

        '注册控制台回调
        SetConsoleCtrlHandler(AddressOf UnexpectedExit, True)

        '显示播放器窗口
        KCore.Show()

        Console.WriteLine("===== EasyK =====")
        Console.WriteLine($"Ver: {Application.ProductVersion}")
        Console.WriteLine("启动完成")
        If Settings.Settings.DebugMode Then Console.WriteLine("#Debug模式#")
        Console.WriteLine("=================")
        Console.WriteLine("可输入 help 以查看帮助")

        Application.Run()
    End Sub

    Private Sub ExitApplication() Handles Commands.OnExit
        '保存配置
        Settings.Dispose()

        '解除事件关联
        Try
            RemoveHandler Commands.OnExit, AddressOf ExitApplication
            RemoveHandler WebServer.OnUncaughtError, AddressOf ExitApplication
        Catch
        End Try

        '关闭指令系统
        Commands.Close()

        '清理
        If Settings.Settings.CleanOnExit Then
            Dim Folder As String = IO.Path.Combine(Application.StartupPath, Settings.Settings.TempFolder)
            For Each File As String In IO.Directory.GetFiles(Folder)
                Try
                    IO.File.Delete(File)
                Catch ex As Exception
                    Console.WriteLine("清理文件 {0} 时失败 - {1}", File, ex.Message)
                End Try
            Next
        End If

        '关闭服务
        DLNAServer.Dispose()
        DLNA.MusicProvider.DLNAMusicProviders.UnloadProviders(Settings)
        WebServer.Dispose()
        KCore.Dispose()

        End
    End Sub

    '可能被非托管代码调用
    '必须尽量简单快速
    '重要:不能引入面向对象等高级特性
    Private Function UnexpectedExit(ctrlType As CtrlType) As Boolean
        Select Case ctrlType
            Case CtrlType.CTRL_CLOSE_EVENT, CtrlType.CTRL_LOGOFF_EVENT, CtrlType.CTRL_SHUTDOWN_EVENT
                ExitApplication()
            Case CtrlType.CTRL_BREAK_EVENT, CtrlType.CTRL_C_EVENT
                ExitApplication()
                Return True
        End Select

        Return False
    End Function

End Module
