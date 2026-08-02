Imports System.Text
Imports System.Threading
Imports System.Windows.Forms
Imports EasyK.ConsoleUtils
Imports NAudio.CoreAudioApi

Module ModMain

    Public WithEvents Commands As Commands.CommandParser

    Public KCore As EasyK

    Public Settings As SettingContainer

    Public WebServer As KWebCore

    Public Logger As KLogger

    Private ReadOnly ConsoleExitHandler As HandlerRoutine = AddressOf ConsoleExit

    Private ReadOnly SafeExitLock As New ManualResetEvent(False)

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

        '务必保证Loger及之前的操作不能抛出错误

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
        SetConsoleCtrlHandler(ConsoleExitHandler, True)

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

        '创建安全锁
        SafeExitLock.Reset()
        Task.Run(AddressOf SafeExit)

        '解除事件关联
        Try
            If Commands IsNot Nothing Then RemoveHandler Commands.OnExit, AddressOf ExitApplication
            If WebServer IsNot Nothing Then RemoveHandler WebServer.OnUncaughtError, AddressOf ExitApplication

            RemoveHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnUnhandledException
        Catch
        End Try

        '注销控制台回调
        SetConsoleCtrlHandler(ConsoleExitHandler, False)

        '关闭指令系统
        If Commands IsNot Nothing Then Commands.Close()

        '关闭服务
        If WebServer IsNot Nothing Then WebServer.Dispose()
        If KCore IsNot Nothing Then KCore.Dispose()

        '解除安全锁
        SafeExitLock.Set()

        '卸载插件并保持配置
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

    '安全退出机制
    '只保存最关键的数据
    '防死锁
    Private Sub SafeExit()
        Dim IsSafe As Boolean = SafeExitLock.WaitOne(Settings.Settings.SafeExitTime * 1000)
        If IsSafe Then Return

        DLNA.MusicProvider.DLNAMusicProviders.UnloadProviders(Settings)
        Settings.Dispose()
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
