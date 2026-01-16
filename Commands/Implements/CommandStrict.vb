Public Class CommandStrict
    Inherits Command

    Private ReadOnly Settings As SettingContainer

    Public Sub New(Settings As SettingContainer)
        MyBase.New("strict", "strict [true/false] - 设置投屏权限是否严格", CommandType.System)
        Me.Settings = Settings
    End Sub

    Protected Overrides Sub Process(Args() As String)
        With Settings.Settings.DLNA
            If Args.Length < 2 Then
                Console.WriteLine("投屏权限严格模式: {0}", .StrictPermission.ToString().ToLower())
            Else
                Try
                    .StrictPermission = Boolean.Parse(Args(1))
                    Console.WriteLine("投屏权限严格模式已设置为: {0}", .StrictPermission.ToString().ToLower())
                Catch
                    InvalidUsage()
                End Try
            End If
        End With
    End Sub

End Class
