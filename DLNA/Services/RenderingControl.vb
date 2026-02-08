Namespace DLNA.Protocol

    Public Class RenderingControl
        Inherits DLNAService

        ''' <summary>
        ''' 获取或设置是否为第一次调整音量
        ''' </summary>
        ''' <returns></returns>
        Friend Property FirstVolume As Boolean = True

        ''' <summary>
        ''' 构建服务
        ''' </summary>
        ''' <param name="Protocol">协议管理器</param>
        Public Sub New(Protocol As DLNAProtocol)
            MyBase.New(Protocol, NameOf(RenderingControl), My.Resources.RenderingControl)

            '配置默认状态值
            SetState("CurrentConnectionIDs", "0")

            '配置推送
            SetStateEvent("LastChange", False)
            SetStateEvent("Volume", True)
            SetStateEvent("Mute", True)
        End Sub

        ''' <summary>
        ''' 生成更新消息
        ''' </summary>
        ''' <param name="Updated">已更新状态</param>
        ''' <returns></returns>
        Protected Overrides Function GenerateNotify(Updated As Dictionary(Of String, String)) As String
            Dim root = New XElement(ServiceNamespace + "propertyset")
            root.Add(New XAttribute(XNamespace.Xmlns + "e", ServiceNamespace))

            Dim prop = New XElement(ServiceNamespace + "property")
            Dim lastChange = New XElement("LastChange")
            Dim eventEl = New XElement(MetaNamespace + "Event")
            Dim instanceId = New XElement(MetaNamespace + "InstanceID")
            instanceId.SetAttributeValue("val", "0")

            For Each kvp In Updated
                Dim p = New XElement(MetaNamespace + kvp.Key)
                p.SetAttributeValue("val", If(kvp.Value, ""))
                instanceId.Add(p)
            Next

            eventEl.Add(instanceId)
            lastChange.Value = eventEl.ToString(SaveOptions.DisableFormatting)
            prop.Add(lastChange)
            root.Add(prop)

            Return $"<?xml version=""1.0"" encoding=""UTF-8""?>{vbCrLf}{root.ToString(SaveOptions.DisableFormatting)}"
        End Function

        Protected Function GetVolume(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            With Protocol
                '仅在托管模式下接管GetVolume

                If Not .Settings.Settings.Audio.IsDummyAudio Then Return Nothing

                Handled = True
                Return New Dictionary(Of String, String) From {
                    {"CurrentVolume", Math.Round(.DLNA.K.DummyVolume * 100)}
                }
            End With
        End Function

        Protected Function SetVolume(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            With Protocol
                If .Settings.Settings.Audio.IsDummyAudio Then
                    '托管音量

                    Handled = True

                    If Not Args.ContainsKey("DesiredVolume") Then Return Nothing

                    Dim Volume As Single = Math.Max(Math.Min(Val(Args("DesiredVolume")) / 100.0F, 1), 0)
                    .DLNA.K.DummyVolume = Volume
                ElseIf .Settings.Settings.Audio.AllowUpdateSystemVolume Then
                    '系统音量

                    If FirstVolume Then
                        Dim Volume As Integer = Val(Args("DesiredVolume")) / 2
                        With .DLNA.K
                            .UpdateSystemVolume(FormUtils.VolumeAction.Down, 50)
                            .UpdateSystemVolume(FormUtils.VolumeAction.Up, Volume)
                        End With

                        FirstVolume = False
                    Else
                        Dim Volume As Integer = Val(GetState("Volume"))
                        Dim NewVolume As Integer = Val(Args("DesiredVolume"))
                        Dim Offset As Integer = (NewVolume - Volume) / 2

                        If Offset <> 0 Then
                            .DLNA.K.UpdateSystemVolume(If(Offset < 0, FormUtils.VolumeAction.Down, FormUtils.VolumeAction.Up), Math.Abs(Offset))
                        End If
                    End If
                End If
            End With

            Return Nothing
        End Function

        Protected Function SetMute(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            With Protocol
                If .Settings.Settings.Audio.IsDummyAudio Then
                    '托管模式

                    If Args.ContainsKey(Args("DesiredMute")) Then
                        Try
                            .DLNA.K.DummyMute = Boolean.Parse(Args("DesiredMute"))
                        Catch ex As Exception
                            If .Settings.Settings.DebugMode Then
                                Console.WriteLine("无法解析的静音请求 - {0}", ex.Message)
                            End If
                        End Try
                    End If
                ElseIf .Settings.Settings.Audio.AllowUpdateSystemVolume Then
                    '系统静音

                    .DLNA.K.UpdateSystemVolume(FormUtils.VolumeAction.Mute, 0)
                End If
            End With

            Return Nothing
        End Function

    End Class

End Namespace
