Imports System.Windows.Forms
Imports CefSharp

Public Class CefSetting
    Inherits CefSettingsBase

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

        '音频托管
        If Settings.Settings.Audio.IsDummyAudio Then CefCommandLineArgs.Add("disable-audio-output", "1")

        '允许自动播放
        CefCommandLineArgs.Add("autoplay-policy", "no-user-gesture-required")

        '日志
        If Settings.Settings.DebugMode Then
            Console.WriteLine("CEFSharp 初始化完成")
        Else
            LogSeverity = LogSeverity.Disable
        End If
    End Sub

End Class
