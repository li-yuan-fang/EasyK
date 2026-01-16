Imports System.ComponentModel
Imports System.Windows.Forms
Imports CefSharp
Imports CefSharp.WinForms

Public Class FrmLogin

    Private Class CookieVisitor
        Implements ICookieVisitor

        Private ReadOnly Cookies As New List(Of Cookie)

        Public Event OnVisitComplete(Cookies As List(Of Cookie))

        Public Function Visit(cookie As Cookie, count As Integer, total As Integer, ByRef deleteCookie As Boolean) As Boolean Implements ICookieVisitor.Visit
            Cookies.Add(cookie)

            If count = total - 1 Then RaiseEvent OnVisitComplete(Cookies)

            Return True
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            Cookies.Clear()
        End Sub

    End Class

    Friend WithEvents Browser As ChromiumWebBrowser

    Private Prevent As Boolean = True

    Private WithEvents Visitor As New CookieVisitor()

    ''' <summary>
    ''' 获取Cookie成功事件
    ''' </summary>
    ''' <param name="Cookies"></param>
    Public Event SaveCookies(Cookies As List(Of Cookie))

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

    Public Overloads Sub Dispose()
        With Browser
            .CloseDevTools()
            .GetBrowser().CloseBrowser(True)
            .Dispose()
        End With

        MyBase.Dispose()
    End Sub

    Private Sub FrmLogin_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If Prevent Then
            e.Cancel = True
            Prevent = False

            Browser.GetCookieManager().VisitAllCookies(Visitor)
        End If
    End Sub

    Private Sub Visitor_OnVisitComplete(Cookies As List(Of Cookie)) Handles Visitor.OnVisitComplete
        RaiseEvent SaveCookies(Cookies)
        Prevent = False
        Invoke(Sub() Close())
    End Sub

    'TODO:加载本地储存的Cookies

End Class