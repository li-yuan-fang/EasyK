Namespace DLNA.Protocol

    Public Class RenderingControl
        Inherits DLNAService

        ''' <summary>
        ''' 获取或设置是否为第一次调整音量
        ''' </summary>
        ''' <returns></returns>
        Friend Property FirstVolume As Boolean = True

        '缓存的音量
        Private StoredVolume As Single = 0

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
            Handled = True

            Return New Dictionary(Of String, String) From {
                    {"CurrentVolume", Math.Round(Protocol.DLNA.K.Volume * 100)}
                }
        End Function

        Protected Function SetVolume(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            Handled = True

            If Args.ContainsKey("DesiredVolume") Then
                Protocol.DLNA.K.Volume = Math.Max(Math.Min(Val(Args("DesiredVolume")) / 100.0F, 1), 0)
            End If

            Return Nothing
        End Function

        Protected Function GetMute(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            Handled = True

            Return New Dictionary(Of String, String)() From {
                    {"CurrentMute", Protocol.DLNA.K.Volume = 0}
                }
        End Function

        Protected Function SetMute(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            Handled = True

            If Args.ContainsKey(Args("DesiredMute")) Then
                With Protocol
                    Try
                        With .DLNA.K
                            If Boolean.Parse(Args("DesiredMute")) Then
                                If .Volume > 0 Then
                                    StoredVolume = .Volume
                                    .Volume = 0
                                End If
                            Else
                                If StoredVolume > 0 Then
                                    .Volume = StoredVolume
                                Else
                                    .Volume = 1
                                End If
                            End If
                        End With
                    Catch ex As Exception
                        If .Settings.Settings.DebugMode Then
                            Console.WriteLine("无法解析的静音请求 - {0}", ex.Message)
                        End If
                    End Try
                End With
            End If

            Return Nothing
        End Function

    End Class

End Namespace
