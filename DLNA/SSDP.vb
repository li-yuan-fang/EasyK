Imports System.Globalization
Imports System.Net
Imports System.Net.Sockets
Imports System.Reflection

Namespace DLNA

    Public Class SSDPService

        ''' <summary>
        ''' 获取服务标识符
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property USN As String

        ''' <summary>
        ''' 获取描述文件路径
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Location As String

        ''' <summary>
        ''' 获取设备类型
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property ST As String

        ''' <summary>
        ''' 获取额外信息
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Ext As String

        ''' <summary>
        ''' 获取服务器信息
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Server As String

        ''' <summary>
        ''' 获取缓存控制信息
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property CacheControl As String

        ''' <summary>
        ''' 实例化SSDP服务
        ''' </summary>
        ''' <param name="USN">服务标识符</param>
        ''' <param name="Location">描述文件路径</param>
        ''' <param name="ST">设备类型</param>
        ''' <param name="Server">服务器信息</param>
        Public Sub New(USN As String, Location As String, ST As String, Server As String)
            Me.New(USN, Location, ST, vbNullString, Server, "max-age=1800")
        End Sub

        ''' <summary>
        ''' 实例化SSDP服务
        ''' </summary>
        ''' <param name="USN">服务标识符</param>
        ''' <param name="Location">描述文件路径</param>
        ''' <param name="ST">设备类型</param>
        ''' <param name="Server">服务器信息</param>
        ''' <param name="CacheControl">缓存控制信息</param>
        Public Sub New(USN As String, Location As String, ST As String, Server As String, CacheControl As String)
            Me.New(USN, Location, ST, vbNullString, Server, CacheControl)
        End Sub

        ''' <summary>
        ''' 实例化SSDP服务
        ''' </summary>
        ''' <param name="USN">服务标识符</param>
        ''' <param name="Location">描述文件路径</param>
        ''' <param name="ST">设备类型</param>
        ''' <param name="Ext">额外信息</param>
        ''' <param name="Server">服务器信息</param>
        ''' <param name="CacheControl">缓存控制信息</param>
        Public Sub New(USN As String, Location As String, ST As String, Ext As String, Server As String, CacheControl As String)
            Me.USN = USN
            Me.Location = Location
            Me.ST = ST
            Me.Ext = Ext
            Me.Server = Server
            Me.CacheControl = CacheControl
        End Sub

        Private Shared Function GetHeaderName(Original As String) As String
            Dim Builder As New Text.StringBuilder()
            With Builder
                For i = 0 To Original.Length - 1
                    If Char.IsUpper(Original(i)) AndAlso i > 0 AndAlso Char.IsLower(Original(i - 1)) Then .Append("-")
                    .Append(Original(i))
                Next
            End With
            Return Builder.ToString().ToUpper()
        End Function

        ''' <summary>
        ''' 生成响应
        ''' </summary>
        ''' <param name="LocalIP">本地IP</param>
        ''' <returns></returns>
        Public Function GetResponse(LocalIP As String) As String
            Dim Builder As New Text.StringBuilder()
            With Builder
                For Each p As PropertyInfo In GetType(SSDPService).GetProperties(BindingFlags.Public Or BindingFlags.Instance)
                    Dim Value As String = p.GetValue(Me)
                    If p.Name = NameOf(Location) Then Value = Value.Replace("${IP}", LocalIP)
                    .Append($"{GetHeaderName(p.Name)}: {Value}").AppendLine()
                Next
            End With
            Return Builder.ToString()
        End Function

        ''' <summary>
        ''' 生成通知
        ''' </summary>
        ''' <param name="LocalIP">本地IP</param>
        ''' <returns></returns>
        Public Function GetNotify(LocalIP As String) As String
            Dim Builder As New Text.StringBuilder()
            With Builder
                For Each p As PropertyInfo In GetType(SSDPService).GetProperties(BindingFlags.Public Or BindingFlags.Instance)
                    Dim Name As String = p.Name
                    Dim Value As String = p.GetValue(Me)

                    Select Case Name
                        Case = NameOf(Location)
                            Value = Value.Replace("${IP}", LocalIP)
                        Case = NameOf(ST)
                            Name = "NT"
                    End Select

                    .Append($"{GetHeaderName(Name)}: {Value}").AppendLine()
                Next
            End With
            Return Builder.ToString()
        End Function

    End Class

    ''' <summary>
    ''' SSDP服务器
    ''' </summary>
    Public Class SSDP
        Implements IDisposable

        Private Const SSDP_Group As String = "239.255.255.250"

        Private Const SSDP_Port As Integer = 1900

        Private Shared ReadOnly UTF8 As New Text.UTF8Encoding(False)

        Private ReadOnly Group As IPAddress = IPAddress.Parse(SSDP_Group)

        Private ReadOnly Services As New List(Of SSDPService)

        Private ReadOnly UDPClients As New Dictionary(Of UdpClient, Task)

        Private ReadOnly Settings As SettingContainer

        Private ReadOnly NotifyLoop As Task

        Private Running As Boolean

        ''' <summary>
        ''' 初始化SSDP服务器
        ''' </summary>
        ''' <param name="Settings">配置容器</param>
        Public Sub New(Settings As SettingContainer)
            Me.Settings = Settings

            '复位
            Running = True

            '创建UDP客户端
            CreateClients()

            '启动广播
            NotifyLoop = Task.Run(Sub()
                                      Dim i As Integer
                                      While Running
                                          i = 0
                                          While i < Settings.Settings.DLNA.SSDPNotifyInterval
                                              Threading.Thread.Sleep(10)
                                              i += 10

                                              If Not Running Then Return
                                          End While

                                          Notify()
                                      End While
                                  End Sub)
        End Sub

        Private Sub CreateClients()
            For Each ni In NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                If ni.OperationalStatus = System.Net.NetworkInformation.OperationalStatus.Up Then
                    For Each addr In ni.GetIPProperties().UnicastAddresses
                        If addr.Address.AddressFamily = AddressFamily.InterNetwork Then
                            CreateClient(addr.Address)
                        End If
                    Next
                End If
            Next
        End Sub

        '创建客户端
        Private Sub CreateClient(LocalIP As IPAddress)
            Dim UDP As New UdpClient()
            Dim [Loop] As Task
            With UDP
                With .Client
                    .SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, 0)
                    .SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1)
                    .SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, LocalIP.GetAddressBytes())
                    .Bind(New IPEndPoint(LocalIP, SSDP_Port))
                End With

                .JoinMulticastGroup(Group, LocalIP)

                [Loop] = Task.Run(Sub() ClientLoop(UDP))
            End With

            UDPClients.Add(UDP, [Loop])
        End Sub

        '客户端循环
        Private Sub ClientLoop(UDP As UdpClient)
            Dim Source As New IPEndPoint(IPAddress.Any, 0)

            While Running
                Try
                    Dim Buffer As Byte() = UDP.Receive(Source)
                    If Not Running Then Return

                    RespondSearch(UDP, Source, UTF8.GetString(Buffer))
                Catch
                End Try
            End While
        End Sub

        '响应搜索
        Private Sub RespondSearch(UDP As UdpClient, Source As IPEndPoint, Content As String)
            If String.IsNullOrWhiteSpace(Content) Then Return

            '检查请求类型
            Dim Lines As String() = Split(Content, vbCrLf)
            If Lines.Length = 0 OrElse Not Lines(0).StartsWith("M-SEARCH ") Then Return

            '获取头
            Dim Headers As New Dictionary(Of String, String)
            For Each Line As String In Lines
                If String.IsNullOrEmpty(Line) OrElse Not Line.Contains(":") Then Continue For

                Dim i As Integer = Line.IndexOf(":")
                Dim Value As String = Line.Substring(i + 1)
                If Value.StartsWith(" ") Then Value = Value.Substring(1)

                Headers.Add(Line.Substring(0, i).ToUpper(), Value)
            Next

            If Not Headers.ContainsKey("ST") Then
                Headers.Clear()
                Return
            End If

            '获取本机IP
            Dim Local As IPEndPoint = DirectCast(UDP.Client.LocalEndPoint, IPEndPoint)
            Dim LocalIP As String = vbNullString
            With Local
                If .Address IsNot Nothing Then
                    With .Address
                        If .IsIPv6LinkLocal OrElse .IsIPv6SiteLocal Then
                            LocalIP = $"[{ .ToString()}]:{Settings.Settings.Web.Port.ToString()}"
                        ElseIf .IsIPv4MappedToIPv6 Then
                            LocalIP = $"{ .MapToIPv4().ToString()}:{Settings.Settings.Web.Port.ToString()}"
                        Else
                            LocalIP = $"{ .ToString()}:{Settings.Settings.Web.Port.ToString()}"
                        End If
                    End With
                End If
            End With

            '遍历服务
            Dim ServicesCopy As List(Of SSDPService)
            SyncLock Services
                ServicesCopy = New List(Of SSDPService)(Services)
            End SyncLock

            For Each Service As SSDPService In ServicesCopy
                Dim ST As String = Headers("ST").ToLower()
                If ST <> Service.ST.ToLower() AndAlso ST <> "ssdp:all" Then Continue For

                Dim Builder As New Text.StringBuilder
                With Builder
                    .Append("HTTP/1.1 200 OK").AppendLine()
                    .Append(Service.GetResponse(LocalIP))
                    .Append($"DATE: {DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture)}")
                    .AppendLine().AppendLine()
                End With

                Dim Buffer As Byte() = UTF8.GetBytes(Builder.ToString())
                Dim MX As Integer = Math.Min(If(Headers.ContainsKey("MX"), Val(Headers("MX")), 0), 10)
                Dim Remote As New IPEndPoint(Source.Address, Source.Port)
                Task.Run(Sub()
                             If MX > 0 Then
                                 Dim Rand As New Random()
                                 Dim Delay As Integer = Rand.Next(0, MX + 1)
                                 Threading.Thread.Sleep(Delay * 1000)
                             End If

                             Try
                                 UDP.Send(Buffer, Buffer.Length, Remote)
                             Catch
                             End Try
                         End Sub)
            Next
        End Sub

        ''' <summary>
        ''' 注册服务
        ''' </summary>
        ''' <param name="Service">SSDP服务</param>
        Public Sub Register(Service As SSDPService)
            SyncLock Services
                Services.Add(Service)
            End SyncLock
        End Sub

        ''' <summary>
        ''' 注销服务
        ''' </summary>
        ''' <param name="Service">SSDP服务</param>
        Public Sub Unregister(Service As SSDPService)
            SyncLock Services
                Services.Remove(Service)
            End SyncLock
        End Sub

        ''' <summary>
        ''' 发送通知
        ''' </summary>
        Public Sub Notify()
            Notify("ssdp:alive")
        End Sub

        Private Sub Notify(NTS As String)
            Dim Multicast As New IPEndPoint(Group, SSDP_Port)

            Dim ServiceCopy As List(Of SSDPService)
            SyncLock Services
                ServiceCopy = New List(Of SSDPService)(Services)
            End SyncLock

            For Each ni In NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                If ni.OperationalStatus <> NetworkInformation.OperationalStatus.Up Then Continue For

                For Each addr In ni.GetIPProperties().UnicastAddresses
                    If addr.Address.AddressFamily <> AddressFamily.InterNetwork Then Continue For

                    Dim LocalIP As String = $"{ addr.Address.ToString()}:{Settings.Settings.Web.Port.ToString()}"

                    For Each Service As SSDPService In ServiceCopy
                        Dim Builder As New Text.StringBuilder
                        With Builder
                            .Append("NOTIFY * HTTP/1.1").AppendLine()
                            .Append($"HOST: {SSDP_Group}:{SSDP_Port}").AppendLine()
                            .Append($"NTS: {NTS}").AppendLine()
                            .Append(Service.GetNotify(LocalIP))
                            .AppendLine()
                        End With

                        Dim Buffer As Byte() = UTF8.GetBytes(Builder.ToString())
                        Using Client As New UdpClient()
                            With Client
                                With .Client
                                    .SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, 0)
                                    .SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1)
                                    .SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, addr.Address.GetAddressBytes())
                                End With
                                .JoinMulticastGroup(Group, addr.Address)

                                .Send(Buffer, Buffer.Length, Multicast)
                            End With
                        End Using
                    Next
                Next
            Next
        End Sub

        ''' <summary>
        ''' 销毁资源
        ''' </summary>
        Public Sub Dispose() Implements IDisposable.Dispose
            Running = False

            '停止广播
            If Not NotifyLoop.IsCompleted Then NotifyLoop.Wait()

            '广播关闭消息
            Notify("ssdp:byebye")

            '终止UDP响应
            For Each UDP As UdpClient In UDPClients.Keys
                With UDP
                    .DropMulticastGroup(Group)
                    .Close()
                    .Dispose()
                End With

                Dim [Loop] As Task = UDPClients(UDP)
                With [Loop]
                    If Not .IsCompleted Then .Wait()
                    .Dispose()
                End With
            Next

            UDPClients.Clear()
        End Sub

    End Class

End Namespace
