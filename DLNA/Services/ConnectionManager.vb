Namespace DLNA.Protocol

    Public Class ConnectionManager
        Inherits DLNAService

        ''' <summary>
        ''' 构建服务
        ''' </summary>
        ''' <param name="Protocol">协议管理器</param>
        Public Sub New(Protocol As DLNAProtocol)
            MyBase.New(Protocol, NameOf(ConnectionManager), My.Resources.ConnectionManager)

            '配置默认状态值
            SetState("A_ARG_TYPE_Direction", "Output")
            SetState("A_ARG_TYPE_ConnectionStatus", "OK")
            SetState("A_ARG_TYPE_ConnectionID", "0")
            SetState("SinkProtocolInfo", My.Resources.SinkProtocolInfo)

            '配置推送
            SetStateEvent("SourceProtocolInfo", False)
            SetStateEvent("A_ARG_TYPE_Direction", True)
        End Sub

        Protected Overrides Function GenerateNotify(Updated As Dictionary(Of String, String)) As String
            Dim root = New XElement(ServiceNamespace + "propertyset")
            root.Add(New XAttribute(XNamespace.Xmlns + "e", ServiceNamespace))

            For Each kvp In Updated
                Dim prop = New XElement(ServiceNamespace + "property")
                Dim item = New XElement(ServiceNamespace + kvp.Key)
                item.Value = kvp.Value.ToString()
                prop.Add(item)
                root.Add(prop)
            Next

            Return $"<?xml version=""1.0"" encoding=""UTF-8""?>{vbCrLf}{root.ToString(SaveOptions.DisableFormatting)}"
        End Function

    End Class

End Namespace
