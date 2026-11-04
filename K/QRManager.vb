Imports System.Drawing
Imports System.Net.NetworkInformation
Imports System.Threading

Public Class QRManager
    Implements IDisposable

    Private ReadOnly Settings As SettingContainer

    Private WithEvents QRForm As FrmQRCode

    Private LastValidAdapter As NetworkInterface = Nothing

    Private NetworkChangeCommit As Date = Date.MinValue

    Private _SavedQRBounds As Rectangle

    ''' <summary>
    ''' 获取或设置获取播放器窗体的委托
    ''' </summary>
    ''' <returns></returns>
    Public Property GetPlayerForm As Func(Of FrmPlayer) = Function() Nothing

    Private ReadOnly Property PlayerForm As FrmPlayer
        Get
            Return GetPlayerForm()()
        End Get
    End Property

    ''' <summary>
    ''' 初始化二维码管理器
    ''' </summary>
    ''' <param name="Settings">配置容器</param>
    Public Sub New(Settings As SettingContainer)
        Me.Settings = Settings

        '绑定网络变化事件
        AddHandler NetworkChange.NetworkAddressChanged, AddressOf OnNetworkAddressChange
    End Sub

    Friend Sub ResetBounds()
        _SavedQRBounds = New Rectangle(-1, -1, 0, 0)
    End Sub

    ''' <summary>
    ''' 获取二维码显示状态
    ''' </summary>
    ''' <returns></returns>
    Public Function IsQRCodeShown() As Boolean
        Return QRForm IsNot Nothing AndAlso QRForm.Visible AndAlso Not QRForm.IsDisposed()
    End Function

    Private Sub UpdateQRBounds()
        If Not IsQRCodeShown() Then Return

        With PlayerForm
            Dim Width, Height As Integer
            Dim X, Y As Integer

            If _SavedQRBounds.X >= 0 AndAlso _SavedQRBounds.Y >= 0 Then
                With _SavedQRBounds
                    X = .X
                    Y = .Y
                    Width = .Width
                    Height = .Height
                End With
            Else
                Height = CInt(.Height * 0.25)
                Width = CInt(Height * 0.9)

                X = CInt(.Width - Width - 1)
                Y = CInt((.Height - Height) / 2 - 1)
            End If

            .Invoke(Sub() QRForm.SetBounds(X, Y, Width, Height))
        End With
    End Sub

    ''' <summary>
    ''' 显示二维码
    ''' </summary>
    ''' <param name="Adapter">网卡</param>
    ''' <param name="Outside">以独立窗口显示</param>
    Public Sub ShowQRCode(Adapter As NetworkInterface, Outside As Boolean)
        Dim LocalIP As String = NetUtils.GetLocalIP(Adapter)
        If String.IsNullOrEmpty(LocalIP) Then
            Logger.Error("显示二维码失败 - 获取本机IP失败")
            Return
        End If

        '缓存有效网卡
        LastValidAdapter = Adapter

        Dim Key As String = Settings.Settings.Web.PassKey
        Dim Port As Integer = Settings.Settings.Web.Port
        If String.IsNullOrEmpty(Key) Then
            ShowQRCode($"http://{LocalIP}:{Port}/", Outside)
        Else
            ShowQRCode($"http://{LocalIP}:{Port}/?pass={System.Web.HttpUtility.UrlEncode(Key)}", Outside)
        End If
    End Sub

    ''' <summary>
    ''' 显示二维码
    ''' </summary>
    ''' <param name="Url">点歌Url</param>
    ''' <param name="Outside">以独立窗口显示</param>
    Public Sub ShowQRCode(Url As String, Outside As Boolean)
        CloseQRCode()

        If PlayerForm Is Nothing Then Return

        With PlayerForm
            If Outside OrElse Not .Setuped Then
                .Invoke(Sub()
                            QRForm = New FrmQRCode(Url)
                            QRForm.Show()
                        End Sub)
            Else
                .Invoke(Sub()
                            QRForm = New FrmQRCode(Url)

                            With QRForm
                                .Parent = PlayerForm
                                .FormBorderStyle = Windows.Forms.FormBorderStyle.None
                                .ShowInTaskbar = False
                                .Round = True
                                .Show()

                                FormUtils.SetParent(.Handle, PlayerForm.Handle)
                            End With
                        End Sub)

                UpdateQRBounds()
                AddHandler QRForm.OnBoundsUpdate, AddressOf QRForm_OnBoundsUpdate
            End If
        End With
    End Sub

    Private Sub QRForm_OnBoundsUpdate(Bounds As Rectangle)
        _SavedQRBounds = Bounds
    End Sub

    ''' <summary>
    ''' 显示二维码
    ''' </summary>
    ''' <param name="Outside">以独立窗口显示</param>
    Public Function ShowQRCode(Outside As Boolean) As Boolean
        Dim Adapter As NetworkInterface = NetUtils.TryGetMajorAdapter()
        If Adapter Is Nothing AndAlso LastValidAdapter IsNot Nothing Then Adapter = LastValidAdapter

        If Adapter IsNot Nothing Then
            '获取网卡成功
            ShowQRCode(Adapter, Outside)
            Return True
        Else
            Logger.Error("自动显示二维码失败 - 无法获取默认网卡")
            Return False
        End If
    End Function

    ''' <summary>
    ''' 关闭二维码显示
    ''' </summary>
    Public Sub CloseQRCode()
        If QRForm Is Nothing Then Return

        If PlayerForm IsNot Nothing AndAlso Not PlayerForm.IsDisposed Then PlayerForm.Invoke(Sub() QRForm.Close())
        RemoveHandler QRForm.OnBoundsUpdate, AddressOf QRForm_OnBoundsUpdate
        QRForm = Nothing
    End Sub

    '网络变化
    Private Sub OnNetworkAddressChange(sender As Object, e As EventArgs)
        If NetworkChangeCommit = Date.MinValue Then
            '新触发
            NetworkChangeCommit = Now.AddSeconds(3)

            Task.Run(Sub()
                         While Now < NetworkChangeCommit
                             Thread.Sleep(100)
                         End While

                         '复位
                         NetworkChangeCommit = Date.MinValue

                         '提交二维码更新
                         RefreshQRCode()
                     End Sub)
        Else
            '更新时间
            NetworkChangeCommit = Now.AddSeconds(3)
        End If
    End Sub

    ''' <summary>
    ''' 刷新二维码
    ''' </summary>
    Public Sub RefreshQRCode()
        If QRForm Is Nothing OrElse QRForm.IsDisposed Then Return

        ShowQRCode(False)
    End Sub

    ''' <summary>
    ''' 销毁资源
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        RemoveHandler NetworkChange.NetworkAddressChanged, AddressOf OnNetworkAddressChange
        CloseQRCode()
    End Sub

End Class
