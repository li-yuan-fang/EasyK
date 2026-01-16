Imports System.Management
Imports System.Net.NetworkInformation
Imports CefSharp.DevTools.CSS

Public Class NetUtils

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
    ''' <param name="GUID"></param>
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

End Class
