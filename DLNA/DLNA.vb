Imports EasyK.DLNA.Player
Imports Microsoft.AspNetCore.Http
Imports Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http

Namespace DLNA

    ''' <summary>
    ''' DLNA访问权限检查
    ''' </summary>
    ''' <param name="Context">HTTP上下文</param>
    ''' <returns></returns>
    Public Delegate Function DLNAAccessCheck(Context As HttpContext) As Boolean

    Public Class DLNA
        Implements IDisposable

        Private Const APIPrefix As String = "/dlna"

        Friend Const Agent As String = "Windows/10 UPnP/1.1 EasyK/1.0"

        Private Const SSDP_Description As String = "/SSDP.xml"

        Private Const ActionPrefix As String = "/action"

        Private Const EventPrefix As String = "/event"

        Private Const AVTransport As String = "/AVTransport"

        Private Const RenderingControl As String = "/RenderingControl"

        Private Const ConnectionManager As String = "/ConnectionManager"

        Private ReadOnly SSDPServer As SSDP

        Private ReadOnly Settings As SettingContainer

        Private ReadOnly SSDPContent As String

        Private ReadOnly Protocol As Protocol.DLNAProtocol

        Friend ReadOnly K As EasyK

        ''' <summary>
        ''' DLNA访问权限检查
        ''' </summary>
        ''' <returns></returns>
        Public Property CheckAccess As DLNAAccessCheck = (Function() True)

        ''' <summary>
        ''' DLNA播放器
        ''' </summary>
        ''' <remarks>部署窗口后不为Nothing</remarks>
        ''' <returns></returns>
        Public Property Player As DLNAPlayer
            Get
                Return Protocol.AVTransportService.Player
            End Get
            Set(value As DLNAPlayer)
                With Protocol.AVTransportService
                    If value Is Nothing Then
                        .UnregisterPlayer()
                    Else
                        .RegisterPlayer(value)
                    End If
                End With
            End Set
        End Property

        ''' <summary>
        ''' 初始化DLNA协议栈
        ''' </summary>
        ''' <param name="K"></param>
        ''' <param name="Settings">配置容器</param>
        Public Sub New(K As EasyK, Settings As SettingContainer)
            Me.K = K
            Me.Settings = Settings

            '绑定API
            WebStartup.Register(Me, APIPrefix)

            '启动DLNA协议管理器
            Protocol = New Protocol.DLNAProtocol(Me, Settings)

            '更新SSDP描述文件
            SSDPContent = GetSSDPDescription()

            '启动SSDP服务器
            SSDPServer = New SSDP(Settings)
            RegisterSSDPServices()
        End Sub

        Private Sub RegisterSSDPService(Id As String, Type As String)
            SSDPServer.Register(New SSDPService($"{If(String.IsNullOrEmpty(Id), vbNullString, $"{Id}::")}{Type}",
                                            $"http://${{IP}}{APIPrefix}{SSDP_Description}",
                                            Type,
                                            Agent,
                                            $"max-age={Settings.Settings.DLNA.SSDPMaxAge}"))
        End Sub

        '注册SSDP服务
        Private Sub RegisterSSDPServices()
            Dim UUID As String = $"uuid:{Settings.Settings.DLNA.UUID}"

            RegisterSSDPService(UUID, "upnp:rootdevice")
            RegisterSSDPService(vbNullString, UUID)
            RegisterSSDPService(UUID, "urn:schemas-upnp-org:device:MediaRenderer:1")
            RegisterSSDPService(UUID, "urn:schemas-upnp-org:service:RenderingControl:1")
            RegisterSSDPService(UUID, "urn:schemas-upnp-org:service:ConnectionManager:1")
            RegisterSSDPService(UUID, "urn:schemas-upnp-org:service:AVTransport:1")
        End Sub

        '生成SSDP描述文件
        Private Function GetSSDPDescription() As String
            Return $"<?xml version=""1.0"" encoding=""UTF-8""?><root xmlns:dlna=""urn:schemas-dlna-org:device-1-0"" xmlns=""urn:schemas-upnp-org:device-1-0""><specVersion><major>1</major><minor>0</minor></specVersion><device><deviceType>urn:schemas-upnp-org:device:MediaRenderer:1</deviceType><UDN>uuid:{Settings.Settings.DLNA.UUID}</UDN><friendlyName>EasyK</friendlyName><serialNumber>1024</serialNumber><dlna:X_DLNADOC xmlns:dlna=""urn:schemas-dlna-org:device-1-0"">DMR-1.50</dlna:X_DLNADOC><serviceList><service><serviceType>urn:schemas-upnp-org:service:AVTransport:1</serviceType><serviceId>urn:upnp-org:serviceId:AVTransport</serviceId><controlURL>{APIPrefix}{AVTransport}{ActionPrefix}</controlURL><eventSubURL>{APIPrefix}{AVTransport}{EventPrefix}</eventSubURL><SCPDURL>{APIPrefix}{AVTransport}.xml</SCPDURL></service><service><serviceType>urn:schemas-upnp-org:service:RenderingControl:1</serviceType><serviceId>urn:upnp-org:serviceId:RenderingControl</serviceId><controlURL>{APIPrefix}{RenderingControl}{ActionPrefix}</controlURL><eventSubURL>{APIPrefix}{RenderingControl}{EventPrefix}</eventSubURL><SCPDURL>{APIPrefix}{RenderingControl}.xml</SCPDURL></service><service><serviceType>urn:schemas-upnp-org:service:ConnectionManager:1</serviceType><serviceId>urn:upnp-org:serviceId:ConnectionManager</serviceId><controlURL>{APIPrefix}{ConnectionManager}{ActionPrefix}</controlURL><eventSubURL>{APIPrefix}{ConnectionManager}{EventPrefix}</eventSubURL><SCPDURL>{APIPrefix}{ConnectionManager}.xml</SCPDURL></service></serviceList></device></root>"
        End Function

        ''' <summary>
        ''' 销毁资源
        ''' </summary>
        Public Sub Dispose() Implements IDisposable.Dispose
            SSDPServer.Dispose()
            Protocol.Dispose()
        End Sub

        Private Shared Sub AddHeaders(ctx As HttpContext)
            With ctx.Response
                WebStartup.AddHeaderSafe(.Headers, "Server", Agent)
                WebStartup.AddHeaderSafe(.Headers, "Allow", "GET, HEAD, POST, SUBSCRIBE, UNSUBSCRIBE")
                WebStartup.AddHeaderSafe(.Headers, "Ext", "")
            End With
        End Sub

        '添加允许头
        Private Shared Sub AddAllowHeader(ctx As HttpContext)
            WebStartup.AddHeaderSafe(ctx.Response.Headers, "Allow", "GET, HEAD, POST, SUBSCRIBE, UNSUBSCRIBE")
        End Sub

        '虚拟文件API

        <WebApi(SSDP_Description, HttpMethod.Get)>
        Private Function Description(ctx As HttpContext) As Task
            AddAllowHeader(ctx)
            Return WebStartup.RespondText(ctx, SSDPContent, "application/xml")
        End Function

        <WebApi(AVTransport & ".xml", HttpMethod.Get)>
        Private Function AVTransportXml(ctx As HttpContext) As Task
            AddAllowHeader(ctx)
            Return WebStartup.RespondText(ctx, My.Resources.AVTransport, "application/xml")
        End Function

        <WebApi(RenderingControl & ".xml", HttpMethod.Get)>
        Private Function RenderingControlXml(ctx As HttpContext) As Task
            AddAllowHeader(ctx)
            Return WebStartup.RespondText(ctx, My.Resources.RenderingControl, "application/xml")
        End Function

        <WebApi(ConnectionManager & ".xml", HttpMethod.Get)>
        Private Function ConnectionManagerXml(ctx As HttpContext) As Task
            AddAllowHeader(ctx)
            Return WebStartup.RespondText(ctx, My.Resources.ConnectionManager, "application/xml")
        End Function

        '事件API

        <WebApi(AVTransport & ActionPrefix, HttpMethod.Post)>
        Private Function AVTransportAction(ctx As HttpContext) As Task
            AddHeaders(ctx)
            Return Protocol.AVTransportService.Act(ctx, CheckAccess(ctx))
        End Function

        <WebApi(AVTransport & EventPrefix)>
        Private Function AVTransportEvent(ctx As HttpContext) As Task
            AddHeaders(ctx)
            Return Protocol.AVTransportService.EventHandler(ctx)
        End Function

        <WebApi(RenderingControl & ActionPrefix, HttpMethod.Post)>
        Private Function RenderingControlAction(ctx As HttpContext) As Task
            AddHeaders(ctx)
            Return Protocol.RenderingControlService.Act(ctx, CheckAccess(ctx))
        End Function

        <WebApi(RenderingControl & EventPrefix)>
        Private Function RenderingControlEvent(ctx As HttpContext) As Task
            AddHeaders(ctx)
            Return Protocol.RenderingControlService.EventHandler(ctx)
        End Function

        <WebApi(ConnectionManager & ActionPrefix, HttpMethod.Post)>
        Private Function ConnectionManagerAction(ctx As HttpContext) As Task
            AddHeaders(ctx)
            Return Protocol.ConnectionManagerService.Act(ctx, CheckAccess(ctx))
        End Function

        <WebApi(ConnectionManager & EventPrefix)>
        Private Function ConnectionManagerEvent(ctx As HttpContext) As Task
            AddHeaders(ctx)
            Return Protocol.ConnectionManagerService.EventHandler(ctx)
        End Function

    End Class

End Namespace
