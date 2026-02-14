Namespace Commands

    Public Class CommandPass
        Inherits Command

        Private ReadOnly K As EasyK

        Private ReadOnly Settings As SettingContainer

        Public Sub New(K As EasyK, Settings As SettingContainer)
            MyBase.New("pass", "pass [授权码] - 设置/清除授权码", CommandType.System)
            Me.K = K
            Me.Settings = Settings
        End Sub

        Protected Overrides Sub Process(Args() As String)
            If Args.Length < 2 OrElse String.IsNullOrEmpty(Args(1)) Then
                Settings.Settings.Web.PassKey = vbNullString
                Console.WriteLine("授权码已清除")
            Else
                Settings.Settings.Web.PassKey = Args(1)
                Console.WriteLine("授权码已更新")
            End If

            '刷新二维码
            K.RefreshQRCode()
        End Sub

    End Class

End Namespace
