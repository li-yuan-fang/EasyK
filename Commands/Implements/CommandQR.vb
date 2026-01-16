Imports System.Net.NetworkInformation
Imports System.Net.Sockets
Imports System.Web

Public Class CommandQR
    Inherits Command

    Private ReadOnly K As EasyK

    Private ReadOnly Settings As SettingContainer

    Public Sub New(K As EasyK, Settings As SettingContainer)
        MyBase.New("qr", "qr [auto/list/hide/网卡GUID/网卡MAC] [inside/outside] - 显示/隐藏点歌二维码", CommandType.System)
        Me.K = K
        Me.Settings = Settings
    End Sub

    '获取本机有效IP
    Private Function GetLocalIP(Adapater As NetworkInterface) As String
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

    '打印有效网卡
    Private Sub PrintValidAdapters()
        Dim Valid = NetUtils.GetValidAdapters()
        If Valid.Count = 0 Then
            Console.WriteLine("未发现有效的网卡")
            Return
        End If

        Console.WriteLine("发现了以下网卡:")
        For Each v In Valid
            With v
                Console.WriteLine("# {0}", .Name)
                Console.WriteLine("GUID: {0}", .Id)
                Console.WriteLine("MAC: {0}", NetUtils.GetMAC(v))
                With .GetIPProperties()
                    Dim IPv4 As New List(Of String)
                    Dim IPv6 As New List(Of String)
                    For Each u In .UnicastAddresses
                        Select Case u.Address.AddressFamily
                            Case AddressFamily.InterNetwork
                                IPv4.Add(u.Address.ToString())
                            Case AddressFamily.InterNetworkV6
                                IPv6.Add(u.Address.ToString())
                        End Select
                    Next

                    If IPv4.Count > 0 Then
                        Console.WriteLine("IPv4: {0}", String.Join(" ", IPv4))
                    End If
                    If IPv6.Count > 0 Then
                        Console.WriteLine("IPv6: {0}", String.Join(" ", IPv6))
                    End If
                End With
            End With

            Console.WriteLine()
        Next
    End Sub

    '打开二维码窗口
    Private Sub OpenQRCode(Adapter As NetworkInterface, Outside As Boolean)
        Dim LocalIP As String = GetLocalIP(Adapter)
        If String.IsNullOrEmpty(LocalIP) Then
            Console.WriteLine("获取本机IP失败")
            Return
        End If

        Dim Key As String = Settings.Settings.Web.PassKey
        Dim Port As Integer = Settings.Settings.Web.Port
        If String.IsNullOrEmpty(Key) Then
            K.ShowQRCode($"http://{LocalIP}:{Port}/", Outside)
        Else
            K.ShowQRCode($"http://{LocalIP}:{Port}/?pass={HttpUtility.UrlEncode(Key)}", Outside)
        End If
    End Sub

    Protected Overrides Sub Process(Args() As String)
        Dim Content As String = If(Args.Length < 2, "auto", Args(1).ToLower())
        Dim Outside As Boolean = Args.Length >= 3 AndAlso Args(2).ToLower() = "outside"

        Select Case Content
            Case "auto"
                If K.IsQRCodeShown() Then
                    '关闭二维码窗口
                    K.CloseQRCode()
                    Return
                End If

                Dim Adapter As NetworkInterface = NetUtils.TryGetMajorAdapter()
                If Adapter Is Nothing Then
                    Console.WriteLine("自动查找网卡失败")
                    PrintValidAdapters()
                    Return
                End If

                OpenQRCode(Adapter, Outside)
            Case "list"
                '列出可用网卡
                PrintValidAdapters()
            Case "hide"
                '关闭二维码窗口
                K.CloseQRCode()
            Case Else
                '指定网卡
                Dim Adapter As NetworkInterface = NetUtils.GetAdapter(Content)
                If Adapter Is Nothing Then Adapter = NetUtils.GetAdapterByMAC(Content)

                If Adapter Is Nothing Then
                    Console.WriteLine("找不到指定的网卡")
                    PrintValidAdapters()
                    Return
                End If

                OpenQRCode(Adapter, Outside)
        End Select
    End Sub

End Class
