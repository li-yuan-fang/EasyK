Imports System.Windows.Forms

Module ModMain

    Public WithEvents Commands As CommandParser

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

        '运行DLNA服务器
        DLNAServer = New DLNA.DLNA(KCore, Settings)

        '运行指令系统
        Commands = New CommandParser(KCore, WebServer, Settings)
        With Commands
            AddHandler .OnExit, AddressOf ExitApplication
            Task.Run(Sub() .Run())
        End With

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
        DLNAServer.Dispose()
        WebServer.Dispose()
        KCore.Dispose()

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

        '保存配置
        Settings.Dispose()

        End
    End Sub

End Module
