Imports System.Text
Imports System.Windows.Forms
Imports EasyK.ConsoleUtils

Module ModMain

    Public WithEvents Commands As Commands.CommandParser

    Public KCore As EasyK

    Public Settings As SettingContainer

    Public WebServer As KWebCore

    Public Logger As KLogger

    <STAThread>
    Sub Main()
        Console.Title = "EasyK"
        Console.ForegroundColor = ConsoleColor.White
        Console.InputEncoding = Encoding.Unicode
        Console.OutputEncoding = Encoding.Unicode

        '加载配置
        Settings = New SettingContainer()

        '初始化日志系统
        Logger = New KLogger(Settings)

        '运行点歌主服务
        KCore = New EasyK(Settings)

        '运行网络服务器
        WebServer = New KWebCore(KCore, Settings)
        AddHandler WebServer.OnUncaughtError, AddressOf ExitApplication

        '注册意外退出事件
        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnUnhandledException

        '运行指令系统
        Commands = New Commands.CommandParser(KCore, WebServer, Settings)
        With Commands
            AddHandler .OnExit, AddressOf ExitApplication
            Task.Run(Sub() .Run())
        End With

        '注册控制台回调
        SetConsoleCtrlHandler(AddressOf ConsoleExit, True)

        '显示播放器窗口
        KCore.Show()

        Logger.PrintOriginalLines(
            "===== EasyK =====",
            $"Ver: {Application.ProductVersion}{If(Settings.Settings.DebugMode, " (Debug模式)", vbNullString)}",
            "启动完成",
            "=================",
            "可输入 help 以查看帮助"
        )

        '尝试自动部署
        KCore.TryAutoSetup()

        Application.Run()
    End Sub

    Private Sub ExitApplication() Handles Commands.OnExit
        Logger.Info("正在关闭点歌系统...")

        '解除事件关联
        Try
            RemoveHandler Commands.OnExit, AddressOf ExitApplication
            RemoveHandler WebServer.OnUncaughtError, AddressOf ExitApplication

            RemoveHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnUnhandledException
        Catch
        End Try

        '注销控制台回调
        SetConsoleCtrlHandler(AddressOf ConsoleExit, False)

        '关闭指令系统
        Commands.Close()

        '关闭服务
        WebServer.Dispose()
        KCore.Dispose()
        DLNA.MusicProvider.DLNAMusicProviders.UnloadProviders(Settings)

        '保存配置
        Settings.Dispose()

        '清理
        If Settings.Settings.CleanOnExit AndAlso String.IsNullOrEmpty(Settings.Settings.SavedList) Then
            Dim Folder As String = IO.Path.Combine(Application.StartupPath, Settings.Settings.TempFolder)
            For Each File As String In IO.Directory.GetFiles(Folder)
                Try
                    IO.File.Delete(File)
                Catch ex As Exception
                    Logger.Debug($"清理文件 {File} 时失败 - {ex.Message}")
                End Try
            Next
        End If

        '保存日志
        Logger.Dispose()

        End
    End Sub

    '可能被非托管代码调用
    '必须尽量简单快速
    '重要:不能引入面向对象等高级特性
    Private Function ConsoleExit(ctrlType As CtrlType) As Boolean
        Select Case ctrlType
            Case CtrlType.CTRL_CLOSE_EVENT, CtrlType.CTRL_LOGOFF_EVENT, CtrlType.CTRL_SHUTDOWN_EVENT
                ExitApplication()
            Case CtrlType.CTRL_BREAK_EVENT, CtrlType.CTRL_C_EVENT
                ExitApplication()
                Return True
        End Select

        Return False
    End Function

    '遭遇无法处理的异常
    Private Sub OnUnhandledException(sender As Object, e As UnhandledExceptionEventArgs)
        Dim ex = CType(e.ExceptionObject, Exception)
        Logger.Error("遭遇无法处理的错误")
        Logger.PrintOriginalLines(
            $"{ex.Message}",
            $"Caller: {ex.TargetSite}",
            $"Stack:",
            ex.StackTrace,
            ""
        )

        Logger.Info("正在保存点歌信息...")
        Try
            KCore.Save()
            Logger.Info("点歌信息保存成功")
        Catch excp As Exception
            Logger.Error("保存点歌信息失败 - {0}", excp.Message)
        End Try

        '调用正常退出渠道
        ExitApplication()
    End Sub

End Module
