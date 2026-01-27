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
        Browser = New ChromiumWebBrowser(Url)

        With Browser
            .Dock = DockStyle.Fill
        End With
        Controls.Add(Browser)
    End Sub

End Class