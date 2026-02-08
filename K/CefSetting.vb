Imports System.Windows.Forms
Imports CefSharp

Public Class CefSetting
    Inherits CefSettingsBase

    ''' <summary>
    ''' 初始化
    ''' </summary>
    ''' <param name="Settings">配置容器</param>
    Public Sub New(Settings As SettingContainer)
        Dim CachePath As String = IO.Path.Combine(Application.StartupPath, "cache")
        If Not IO.Directory.Exists(CachePath) Then IO.Directory.CreateDirectory(CachePath)

        Me.CachePath = CachePath
        PersistSessionCookies = Settings.Settings.KeepLogin
        PersistUserPreferences = Settings.Settings.KeepLogin

        If Settings.Settings.Audio.IsDummyAudio Then CefCommandLineArgs.Add("disable-audio-output", "1")

        If Settings.Settings.DebugMode Then
            Console.WriteLine("CEFSharp 初始化完成")
        Else
            CefCommandLineArgs.Add("log-severity", "fatal")
        End If
    End Sub

End Class
