Imports System.Windows.Forms
Imports CefSharp

Public Class CefSetting
    Inherits CefSettingsBase

    Private Const UA As String = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36"

    ''' <summary>
    ''' 初始化
    ''' </summary>
    ''' <param name="Settings">配置容器</param>
    Public Sub New(Settings As SettingContainer)
        '缓存
        Dim CachePath As String = IO.Path.Combine(Application.StartupPath, "cache")
        If Not IO.Directory.Exists(CachePath) Then IO.Directory.CreateDirectory(CachePath)

        Me.CachePath = CachePath
        PersistSessionCookies = Settings.Settings.KeepLogin
        PersistUserPreferences = Settings.Settings.KeepLogin

        '默认语言
        Locale = "zh-CN"

        'UserAgent
        UserAgent = UA

        '命令行参数
        With CefCommandLineArgs

            '音频托管
            If Settings.Settings.Audio.IsDummyAudio Then .Add("disable-audio-output", "1")

            '允许自动播放
            .Add("autoplay-policy", "no-user-gesture-required")

            'GPU加速
            .Add("enable-gpu", "1")
            .Add("enable-gpu-compositing", "1")
            .Add("ignore-gpu-blocklist", "1")
            .Add("use-angle", "d3d11")
            .Add("enable-accelerated-video-decode", "1")
        End With

        '日志
        If Settings.Settings.DebugMode Then
            Console.WriteLine("CEFSharp 初始化完成")
        Else
            LogSeverity = LogSeverity.Disable
        End If
    End Sub

End Class
