Namespace Commands

    Public Class CommandRestore
        Inherits Command

        Private ReadOnly K As EasyK

        Private ReadOnly Settings As SettingContainer

        Private Sub PrintMonitor(monitor As ScreenUtils.MonitorInfo)
            With monitor
                Console.WriteLine("设备ID: {0}", If(.DeviceID, "未知"))
                Console.WriteLine("设备名称: {0}", If(.Name, "未知"))
                Console.WriteLine("制造商: {0}", If(.ManufacturerName, "未知"))
                Console.WriteLine("产品ID: {0}", If(.ProductCodeID, "未知"))
                Console.WriteLine("序列号: {0}", If(.SerialNumber, "未知"))
                Console.WriteLine("生产时间: {0}", If(.ManufactureDate, "未知"))
            End With
        End Sub

        Public Sub New(K As EasyK, Settings As SettingContainer)
            MyBase.New("restore", "restore [info/save/clear] - 自动部署", CommandType.System)
            Me.K = K
            Me.Settings = Settings
        End Sub

        Protected Overrides Sub Process(Args() As String)
            Dim Content As String = If(Args.Length < 2, "info", Args(1).ToLower())

            Select Case Content
                Case "info"

                    If K.IsSetup Then
                        Dim major = K.GetMainScreen()
                        With major
                            If .Id < 0 Then
                                Console.WriteLine("无法获取当前播放器信息")
                            Else
                                Console.WriteLine("当前播放器信息>")
                                With .Screen.Bounds
                                    Console.WriteLine("全局坐标: {0},{1}", .Left, .Top)
                                    Console.WriteLine("窗口尺寸: {0},{1}", .Width, .Height)
                                End With

                                Dim m = ScreenUtils.GetMonitors()
                                If .Id < m.Count Then
                                    PrintMonitor(m(.Id))
                                Else
                                    Console.WriteLine("无法获取当前屏幕信息")
                                End If
                            End If
                        End With
                    Else
                        Console.WriteLine("当前播放器状态: 未部署")
                    End If
                    Console.WriteLine()

                    If Settings.Settings.Restore IsNot Nothing Then
                        Console.WriteLine("自动部署信息>")
                        PrintMonitor(Settings.Settings.Restore)
                    Else
                        Console.WriteLine("自动部署状态: 未配置")
                    End If
                Case "save"
                    If Not K.IsSetup Then
                        Console.WriteLine("当前播放器未部署")
                        Return
                    End If

                    Dim major = K.GetMainScreen()
                    With major
                        If .Id < 0 Then
                            Console.WriteLine("无法获取当前播放器信息")
                            Return
                        End If

                        Dim m = ScreenUtils.GetMonitors()
                        If .Id >= m.Count Then
                            Console.WriteLine("无法获取当前屏幕信息")
                            Return
                        End If

                        Settings.Settings.Restore = m(.Id)
                        Console.WriteLine("自动部署功能配置成功")
                    End With
                Case "clear"
                    Settings.Settings.Restore = Nothing
                    Console.WriteLine("自动部署已关闭")
                Case Else
                    InvalidUsage()
            End Select
        End Sub

    End Class

End Namespace
