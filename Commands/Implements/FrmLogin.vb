Imports System.ComponentModel
Imports System.Windows.Forms
Imports CefSharp
Imports CefSharp.WinForms

Public Class FrmLogin

    Friend WithEvents Browser As ChromiumWebBrowser

    Public Sub New(Url As String)

        ' 此调用是设计器所必需的。
        InitializeComponent()

        ' 在 InitializeComponent() 调用之后添加任何初始化。
        Dim CachePath As String = IO.Path.Combine(Windows.Forms.Application.StartupPath, "cache")
        If Not IO.Directory.Exists(CachePath) Then IO.Directory.CreateDirectory(CachePath)

        Dim BrowserSettings As New RequestContextSettings()
        With BrowserSettings
            .CachePath = CachePath
            .PersistSessionCookies = True
            .CookieableSchemesList = "https"
        End With

        Browser = New ChromiumWebBrowser(Url, New RequestContext(BrowserSettings))

        With Browser
            .Dock = DockStyle.Fill
        End With
        Controls.Add(Browser)
    End Sub

End Class