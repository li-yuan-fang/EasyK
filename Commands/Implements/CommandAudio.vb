Imports System.Text.RegularExpressions
Imports NAudio.CoreAudioApi

Namespace Commands

    Public Class CommandAudio
        Inherits Command

        Private ReadOnly K As EasyK

        Private ReadOnly Settings As SettingContainer

        Private ReadOnly Emu As New MMDeviceEnumerator

        Private ReadOnly NumericRegex As New Regex("^\d+$")

        Public Sub New(K As EasyK, Settings As SettingContainer)
            MyBase.New("audio", "audio [device/latency/volume/exclusive] [device:id/latency:ms/volume:0.0-1.0/exclusive:true/false] - 配置音频设备", CommandType.System)
            Me.K = K
            Me.Settings = Settings
        End Sub

        '打印所有音频设备
        Private Sub PrintMMDevices()
            Dim Result As New List(Of String) From {
                "有效的音频设备:"
            }

            Dim Devs = Emu.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)

            For i = 0 To Devs.Count - 1
                With Result
                    .Add(String.Empty)
                    .Add($"#{i + 1}")
                    .Add($"ID: {Devs(i).ID}")
                    .Add($"名称: {Devs(i).FriendlyName}")
                End With
            Next

            Logger.PrintOriginalLines(Result.ToArray())
        End Sub

        Protected Overrides Sub Process(Args() As String)
            If Args.Length < 2 Then
                InvalidUsage()
                Return
            End If

            Select Case Args(1)
                Case "device"
                    If Args.Length < 3 Then
                        PrintMMDevices()
                        Return
                    End If

                    Dim DeviceId As String = Args(2)
                    Dim Device As MMDevice
                    If NumericRegex.IsMatch(DeviceId) Then
                        Dim Devs = Emu.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                        Dim Index As Integer = Integer.Parse(DeviceId) - 1

                        If Index < 0 OrElse Index >= Devs.Count Then
                            Console.WriteLine("无效的设备索引")
                            PrintMMDevices()
                            Return
                        End If

                        Device = Devs(Index)
                    Else
                        Try
                            Device = Emu.GetDevice(DeviceId)
                        Catch ex As Exception
                            Console.WriteLine("无效的设备ID")
                            PrintMMDevices()
                            Return
                        End Try
                    End If

                    K.SetDummyDevice(Device)
                    Console.WriteLine("音频设备已设置为 {0}", Device.FriendlyName)
                Case "latency"
                    If Args.Length < 3 Then
                        Console.WriteLine("当前延迟为 {0}ms", Settings.Settings.Audio.DeviceLatency)
                        Return
                    End If

                    If Not NumericRegex.IsMatch(Args(2)) Then
                        Console.WriteLine("无效的延迟值")
                        Return
                    End If

                    Settings.Settings.Audio.DeviceLatency = Integer.Parse(Args(2))
                    K.SetDummyDevice(Nothing)

                    Console.WriteLine("延迟已设置为 {0}ms", Args(2))
                Case "volume"
                    If Args.Length < 3 Then
                        Console.WriteLine("当前音量为 {0}%", (K.Volume * 100).ToString("0.0"))
                        Return
                    End If

                    Try
                        Dim Volume As Single = Single.Parse(Args(2))
                        If Volume < 0 OrElse Volume > 1 Then Throw New Exception()

                        K.Volume = Volume
                        Console.WriteLine("音量已更改为 {0}%", (Volume * 100).ToString("0.0"))
                    Catch
                        Console.WriteLine("无效的音量")
                    End Try
                Case "exclusive"
                    If Args.Length < 3 Then
                        Console.WriteLine("当前独占状态为 {0}", Settings.Settings.Audio.DeviceExclusive.ToString().ToLower())
                        Return
                    End If

                    Try
                        Dim Exclusive As Boolean = Boolean.Parse(Args(2))
                        Settings.Settings.Audio.DeviceExclusive = Exclusive
                        K.SetDummyDevice(Nothing)

                        Console.WriteLine("独占状态已设置为 {0}", Exclusive.ToString().ToLower())
                    Catch
                        InvalidUsage()
                    End Try
                Case Else
                    InvalidUsage()
            End Select
        End Sub

    End Class

End Namespace
