Namespace Commands

    Public Class CommandPort
        Inherits Command

        Private ReadOnly Web As KWebCore

        Private ReadOnly Settings As SettingContainer

        Public Sub New(Web As KWebCore, Settings As SettingContainer)
            MyBase.New("port", "port [端口] - 显示/设置HTTP服务器端口", CommandType.System)
            Me.Web = Web
            Me.Settings = Settings
        End Sub

        Protected Overrides Sub Process(Args() As String)
            If Args.Length < 2 Then
                Console.WriteLine("当前HTTP端口为 {0}", Settings.Settings.Web.Port)
            Else
                Dim Port As Integer = Val(Args(1))
                If Port < 1 OrElse Port > 65535 Then
                    Console.WriteLine("无效的端口号")
                    Return
                End If

                Settings.Settings.Web.Port = Port
                Web.RestartServer()
                Console.WriteLine("HTTP端口已修改为 {0}", Port)
            End If
        End Sub

    End Class

End Namespace
