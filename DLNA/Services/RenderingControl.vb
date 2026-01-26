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

        Protected Function SetVolume(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            With Protocol
                If .Settings.Settings.AllowVolumeUpdate Then
                    If FirstVolume Then
                        Dim Volume As Integer = Val(Args("DesiredVolume")) / 2
                        With .DLNA.K
                            .UpdateVolume(FormUtils.VolumeAction.Down, 50)
                            .UpdateVolume(FormUtils.VolumeAction.Up, Volume)
                        End With

                        FirstVolume = False
                    Else
                        Dim Volume As Integer = Val(GetState("Volume"))
                        Dim NewVolume As Integer = Val(Args("DesiredVolume"))
                        Dim Offset As Integer = (NewVolume - Volume) / 2

                        If Offset <> 0 Then
                            .DLNA.K.UpdateVolume(If(Offset < 0, FormUtils.VolumeAction.Down, FormUtils.VolumeAction.Up), Math.Abs(Offset))
                        End If
                    End If
                End If
            End With

            Return Nothing
        End Function

        Protected Function SetMute(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            With Protocol
                If .Settings.Settings.AllowVolumeUpdate Then
                    .DLNA.K.UpdateVolume(FormUtils.VolumeAction.Mute, 0)
                End If
            End With

            Return Nothing
        End Function

    End Class

End Namespace
