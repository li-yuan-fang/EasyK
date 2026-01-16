Imports CefSharp
Imports Newtonsoft.Json

Public Class CommandBili
    Inherits Command

    Private ReadOnly K As EasyK

    Private ReadOnly Settings As SettingContainer

    Public Sub New(K As EasyK, Settings As SettingContainer)
        MyBase.New("bili", "bili - 登录bilibili", CommandType.System)
        Me.K = K
        Me.Settings = Settings
    End Sub

    Private Sub SaveCookies(Cookies As List(Of Cookie))
        SyncLock Settings.Settings.Cookies.Bili
            Settings.Settings.Cookies.Bili = New List(Of Cookie)(Cookies)
        End SyncLock
    End Sub

    Protected Overrides Sub Process(Args() As String)
        Using Login As New FrmLogin("https://www.bilibili.com/")
            With Login
                AddHandler .SaveCookies, AddressOf SaveCookies
                .ShowDialog()
                RemoveHandler .SaveCookies, AddressOf SaveCookies
            End With
        End Using

        K.Reset(False)
    End Sub

End Class
