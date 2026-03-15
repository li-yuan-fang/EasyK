Imports System.Management
Imports System.Net
Imports System.Net.NetworkInformation
Imports System.Net.Sockets
Imports System.Text.RegularExpressions

Public Class NetUtils

    Private Shared ReadOnly UrlRegex As New Regex("^https?://(?:www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b(?:[-a-zA-Z0-9()@:%_\+.~#?&//=]*)$")

    Private Shared ReadOnly ValidDevices As String() = {"USB", "PCI", "BTH"}

    ''' <summary>
    ''' 获取所有有效网卡
    ''' </summary>
    ''' <returns></returns>
    Public Shared Function GetValidAdapters() As List(Of NetworkInterface)
        Dim Valid As New List(Of NetworkInterface)


        Try
            For Each nic As NetworkInterface In NetworkInterface.GetAllNetworkInterfaces()
                With nic
                    If .NetworkInterfaceType = NetworkInterfaceType.Unknown OrElse
                    .NetworkInterfaceType = NetworkInterfaceType.Loopback Then Continue For

                    If .OperationalStatus <> OperationalStatus.Up Then Continue For

                    With .GetIPProperties()
                        Dim IPv6 As Boolean = False
                        For Each u In .UnicastAddresses()
                            If u.Address.AddressFamily = Net.Sockets.AddressFamily.InterNetworkV6 Then
                                IPv6 = True
                                Exit For
                            End If
                        Next

                        If IPv6 OrElse .GatewayAddresses().Count > 0 Then Valid.Add(nic)
                    End With
                End With
            Next
        Catch
        End Try

        Return Valid
    End Function

    ''' <summary>
    ''' 尝试获取主要网卡
    ''' </summary>
    ''' <returns></returns>
    Public Shared Function TryGetMajorAdapter() As NetworkInterface
        Dim Valid As List(Of NetworkInterface) = GetValidAdapters()
        If Valid.Count = 0 Then Return Nothing

        '获取WMI
        Dim PnP As New Dictionary(Of String, String)
        Dim Searcher As New ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionID IS NOT NULL")
        For Each Query As ManagementObject In Searcher.Get()
            PnP.Add(Query("GUID"), Query("PNPDeviceID"))
        Next

        If PnP.Count = 0 Then Return Nothing

        '检测网卡
        Dim Stable As New List(Of NetworkInterface)
        For Each v In Valid
            With v
                If Not PnP.ContainsKey(.Id) Then Continue For

                For Each d In ValidDevices
                    If PnP(.Id).ToUpper().StartsWith($"{d}\") Then
                        Stable.Add(v)
                        Continue For
                    End If
                Next
            End With
        Next

        Return If(Stable.Count = 1, Stable(0), Nothing)
    End Function

    ''' <summary>
    ''' 获取MAC地址
    ''' </summary>
    ''' <param name="Adapter">网卡</param>
    ''' <returns></returns>
    Public Shared Function GetMAC(Adapter As NetworkInterface) As String
        Return BitConverter.ToString(Adapter.GetPhysicalAddress().GetAddressBytes()).ToUpper().Replace("-", ":")
    End Function

    ''' <summary>
    ''' 获取网卡
    ''' </summary>
    ''' <param name="GUID">网卡GUID</param>
    ''' <returns></returns>
    Public Shared Function GetAdapter(GUID As String) As NetworkInterface
        Try
            For Each nic As NetworkInterface In NetworkInterface.GetAllNetworkInterfaces()
                With nic
                    If .Id.ToUpper() = GUID.ToUpper() Then Return nic
                End With
            Next
        Catch
        End Try

        Return Nothing
    End Function

    ''' <summary>
    ''' 获取网卡
    ''' </summary>
    ''' <param name="MAC">MAC地址</param>
    ''' <returns></returns>
    Public Shared Function GetAdapterByMAC(MAC As String) As NetworkInterface
        Try
            For Each nic As NetworkInterface In NetworkInterface.GetAllNetworkInterfaces()
                If GetMAC(nic) = MAC.ToUpper() Then Return nic
            Next
        Catch
        End Try

        Return Nothing
    End Function

    ''' <summary>
    ''' 获取本机有效IP
    ''' </summary>
    ''' <param name="Adapater">网卡</param>
    ''' <returns></returns>
    Public Shared Function GetLocalIP(Adapater As NetworkInterface) As String
        Dim IPv4 As New List(Of String)
        Dim IPv6 As New List(Of String)

        With Adapater.GetIPProperties()
            For Each u In .UnicastAddresses
                Select Case u.Address.AddressFamily
                    Case AddressFamily.InterNetwork
                        IPv4.Add(u.Address.ToString())
                    Case AddressFamily.InterNetworkV6
                        IPv6.Add(u.Address.ToString())
                End Select
            Next
        End With

        If IPv4.Count > 0 Then
            Return IPv4(0)
        ElseIf IPv6.Count > 0 Then
            Return $"[{IPv6(0)}]"
        Else
            Return vbNullString
        End If
    End Function

    ''' <summary>
    ''' 获取已占用TCP端口
    ''' </summary>
    ''' <returns></returns>
    Public Shared Function GetUsedTcpPorts() As HashSet(Of Integer)
        Dim usedPorts As New HashSet(Of Integer)()
        Dim ipProps As IPGlobalProperties = IPGlobalProperties.GetIPGlobalProperties()

        ' 获取所有监听中的TCP连接（已占用端口）
        Dim tcpListeners = ipProps.GetActiveTcpListeners()
        For Each endpoint As IPEndPoint In tcpListeners
            usedPorts.Add(endpoint.Port)
        Next

        ' 获取所有已建立的TCP连接（补充占用端口）
        Dim tcpConnections = ipProps.GetActiveTcpConnections()
        For Each conn As TcpConnectionInformation In tcpConnections
            If conn.LocalEndPoint IsNot Nothing Then
                usedPorts.Add(conn.LocalEndPoint.Port)
            End If
        Next

        Return usedPorts
    End Function

    ''' <summary>
    ''' 检测URL是否合法
    ''' </summary>
    ''' <param name="Url"></param>
    ''' <returns></returns>
    Public Shared Function IsURL(Url As String) As Boolean
        Return UrlRegex.IsMatch(Url)
    End Function

End Class
