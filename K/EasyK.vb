Imports System.Drawing
Imports System.Net.NetworkInformation
Imports System.Threading
Imports CefSharp
Imports Microsoft.AspNetCore.Http

Public Class EasyK
    Implements IDisposable

    Private PlayerForm As FrmPlayer

    Private WithEvents QRForm As FrmQRCode

    Private Current As EasyKBookRecord = Nothing

    Private ReadOnly Queue As New LinkedList(Of EasyKBookRecord)

    Private ReadOnly OutdatedQueue As New LinkedList(Of EasyKBookRecord)

    Private LastValidAdapter As NetworkInterface = Nothing

    Friend ReadOnly DLNAServer As DLNA.DLNA

    Friend ReadOnly Settings As SettingContainer

    Friend ReadOnly Dummy As DummyPlayer

    Friend _LyricOffset As Double = 0.0D

    Private _Running As Boolean = False

    Private _SavedQRPosition As Point

    Private PushLock As Integer = 0

    ''' <summary>
    ''' 播放器暂停事件
    ''' <param name="Type">类型</param>
    ''' </summary>
    Public Event OnPlayerPause(Type As EasyKType)

    ''' <summary>
    ''' 播放器播放事件
    ''' </summary>
    ''' <param name="Type">类型</param>
    ''' <param name="Content">资源</param>
    Public Event OnPlayerPlay(Type As EasyKType, Content As String)

    ''' <summary>
    ''' 播放器终止事件
    ''' </summary>
    Public Event OnPlayerTerminated()

    ''' <summary>
    ''' 获取或设置音量
    ''' </summary>
    ''' <returns></returns>
    Public Property Volume As Single
        Get
            Return If(Settings.Settings.Audio.IsDummyAudio, Dummy.Volume, AudioUtils.GetSystemVolume())
        End Get
        Set(value As Single)
            If Settings.Settings.Audio.IsDummyAudio Then
                Dummy.Volume = Math.Max(0, Math.Min(value, 1))
            ElseIf Settings.Settings.Audio.AllowUpdateSystemVolume Then
                AudioUtils.SetSystemVolume(value)
            End If
        End Set
    End Property

    ''' <summary>
    ''' 获取或设置伴唱状态
    ''' </summary>
    ''' <returns></returns>
    Public Property Accompaniment As Boolean
        Get
            If Not Settings.Settings.Audio.AllowAccompaniment Then Return False
            Return Dummy.Accompaniment
        End Get
        Set(value As Boolean)
            If Not Settings.Settings.Audio.AllowAccompaniment Then Return
            Dummy.Accompaniment = value
        End Set
    End Property

    ''' <summary>
    ''' 获取或设置DLNA歌词偏移
    ''' </summary>
    ''' <returns></returns>
    Public Property DLNALyricOffset As Double
        Get
            Return _LyricOffset
        End Get
        Set(value As Double)
            If _LyricOffset <> value Then
                _LyricOffset = value

                If DLNAServer.Player IsNot Nothing Then DLNAServer.Player.UpdateMusicLyricOffset()
            End If
        End Set
    End Property

    ''' <summary>
    ''' 获取部署状态
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property IsSetup As Boolean
        Get
            Return _Running
        End Get
    End Property

    ''' <summary>
    ''' 初始化
    ''' </summary>
    Public Sub New(Settings As SettingContainer)
        Me.Settings = Settings

        '初始化托管音频
        Dummy = New DummyPlayer(Me, Settings)

        '初始化CefSharp
        If Not Cef.IsInitialized Then Cef.Initialize(New CefSetting(Settings))

        '加载DLNA插件
        DLNA.MusicProvider.DLNAMusicProviders.LoadProviders(Settings)

        '加载DLNA服务
        DLNAServer = New DLNA.DLNA(Me, Settings) With {
            .CheckAccess = New DLNA.DLNAAccessCheck(AddressOf DLNAAccessCheck)
        }

        '启动播放器窗口
        PlayerForm = New FrmPlayer(Me, Settings)
    End Sub

    ''' <summary>
    ''' 显示主窗体
    ''' </summary>
    Public Sub Show()
        PlayerForm.Show()
    End Sub

    'DLNA访问权限检查
    Private Function DLNAAccessCheck(ctx As HttpContext) As Boolean
        If Not IsSetup OrElse Current Is Nothing Then Return False

        If Current.Type <> EasyKType.DLNA Then Return False

        '检测访问者
        If Settings.Settings.DLNA.StrictPermission AndAlso Not String.IsNullOrEmpty(Current.Content) AndAlso
            Current.Content <> ctx.Connection.RemoteIpAddress.ToString() AndAlso
            Not NetUtils.LocalAddresses.Contains(Current.Content) Then Return False

        Return True
    End Function

    ''' <summary>
    ''' 部署
    ''' </summary>
    Public Sub Setup()
        Setup(Rectangle.Empty)
    End Sub

    ''' <summary>
    ''' 尝试自动部署
    ''' </summary>
    Public Sub TryAutoSetup()
        With Settings.Settings
            If .Restore Is Nothing Then Return

            '查找屏幕
            Dim m = ScreenUtils.GetMonitors()
            With .Restore
                For i = 0 To m.Count - 1
                    If m(i).Name = .Name AndAlso m(i).ManufacturerName = .ManufacturerName AndAlso
                        m(i).ProductCodeID = .ProductCodeID AndAlso m(i).SerialNumber = .SerialNumber AndAlso
                        m(i).ManufactureDate = .ManufactureDate Then
                        Dim Screens As Windows.Forms.Screen() = Windows.Forms.Screen.AllScreens()
                        If i >= Screens.Length Then Continue For

                        '部署
                        Setup(Screens(i).Bounds)

                        Return
                    End If
                Next
            End With

            If .DebugMode Then
                Console.WriteLine("自动部署失败 - 找不到对应的屏幕")
            End If
        End With
    End Sub

    ''' <summary>
    ''' 部署
    ''' </summary>
    ''' <param name="Bounds">部署区域</param>
    Public Sub Setup(Bounds As Rectangle)
        _Running = True
        _SavedQRPosition = New Point(-1, -1)
        PlayerForm.Setup(Bounds)

        '检测是否需要显示二维码
        If Settings.Settings.AutoShowQR Then ShowQRCode(False)

        Push()
    End Sub

    ''' <summary>
    ''' 推进播放进度/切歌
    ''' </summary>
    Public Sub Push()
        '原子操作 阻止短时多次切歌
        Dim value As Integer = Interlocked.Exchange(PushLock, 1)
        If value <> 0 Then Return

        Dim Temp As EasyKBookRecord

        SyncLock Queue
            If Not IsSetup Then
                Current = Nothing
                Task.Run(Sub()
                             RaiseEvent OnPlayerTerminated()
                             RestartPlayerForm()
                             Interlocked.Exchange(PushLock, 0)
                         End Sub)
                Return
            End If

            If Queue.Count = 0 Then
                Current = Nothing
                Task.Run(Sub()
                             RaiseEvent OnPlayerTerminated()
                             Interlocked.Exchange(PushLock, 0)
                         End Sub)
                Return
            End If

            Temp = Queue.First.Value
            Queue.RemoveFirst()
        End SyncLock

        SyncLock OutdatedQueue
            OutdatedQueue.AddFirst(Temp)
        End SyncLock

        Task.Run(Sub()
                     RaiseEvent OnPlayerTerminated()

                     Current = Temp
                     RaiseEvent OnPlayerPlay(Current.Type, Current.Content)

                     Interlocked.Exchange(PushLock, 0)
                 End Sub)

        With Temp
            Console.WriteLine("开始播放 {0} - {1} (来自 {2})",
                              .Title,
                              If(.Content.Length > 20, $"{ .Content.Substring(0, 20)}..", .Content),
                              .Order)
        End With
    End Sub

    ''' <summary>
    ''' 暂停
    ''' </summary>
    Public Sub Pause()
        If Current Is Nothing Then Return
        Task.Run(Sub() RaiseEvent OnPlayerPause(Current.Type))
    End Sub

    ''' <summary>
    ''' 复位
    ''' </summary>
    ''' <param name="Now">立刻复位</param>
    Public Sub Reset(Now As Boolean)
        _Running = False

        If Now Then
            RestartPlayerForm()
            Current = Nothing
        ElseIf Current Is Nothing Then
            RestartPlayerForm()
        End If
    End Sub

    ''' <summary>
    ''' 顶歌
    ''' </summary>
    ''' <param name="Id">ID</param>
    Public Function SendToTop(Id As String) As EasyKBookRecord
        Return RankBook(Id, 0)
    End Function

    ''' <summary>
    ''' 已点歌曲重排序
    ''' </summary>
    ''' <param name="Id">ID</param>
    ''' <param name="Rank">序号</param>
    ''' <returns></returns>
    Public Function RankBook(Id As String, Rank As Integer) As EasyKBookRecord
        SyncLock Queue
            Dim Node As LinkedListNode(Of EasyKBookRecord) = Queue.First()
            While Node IsNot Nothing
                If Node.Value.Id.Equals(Id) Then
                    '查找成功
                    Queue.Remove(Node)

                    If Rank <= 0 Then
                        '直接置顶
                        Queue.AddFirst(Node)
                    Else
                        '查找插入点
                        Dim Head As LinkedListNode(Of EasyKBookRecord) = Queue.First()
                        Dim i As Integer = 1
                        While Head IsNot Nothing AndAlso i < Rank
                            Head = Head.Next
                            i += 1
                        End While

                        If Head Is Nothing Then
                            Queue.AddLast(Node)
                        Else
                            Queue.AddAfter(Head, Node)
                        End If

                        Return Node.Value
                    End If

                    Return Node.Value
                End If

                Node = Node.Next
            End While
        End SyncLock

        Return Nothing
    End Function

    ''' <summary>
    ''' 获取点歌列表
    ''' </summary>
    ''' <returns></returns>
    Public Function GetBookList() As List(Of EasyKBookRecord)
        SyncLock Queue
            Return Queue.ToList()
        End SyncLock
    End Function

    ''' <summary>
    ''' 获取正在播放
    ''' </summary>
    ''' <returns></returns>
    Public Function GetCurrent() As EasyKBookRecord
        Return Current
    End Function

    ''' <summary>
    ''' 点歌
    ''' </summary>
    ''' <param name="Title">标题</param>
    ''' <param name="Order">点歌人</param>
    ''' <param name="Type">来源</param>
    ''' <param name="Content">内容</param>
    ''' <returns></returns>
    Public Function Book(Title As String, Order As String, Type As EasyKType, Content As String) As String
        Dim Record As New EasyKBookRecord(Title, Order, Type, Content)

        SyncLock Queue
            Queue.AddLast(Record)
        End SyncLock

        If Current Is Nothing Then Push()

        Return Record.Id
    End Function

    ''' <summary>
    ''' 移除歌曲
    ''' </summary>
    ''' <param name="Id">ID</param>
    ''' <returns></returns>
    Public Function Remove(Id As String) As Boolean
        SyncLock Queue
            Dim node As LinkedListNode(Of EasyKBookRecord) = Queue.First()
            While node IsNot Nothing
                If node.Value.Id.Equals(Id) Then
                    Queue.Remove(node)

                    Return True
                End If

                node = node.Next
            End While
        End SyncLock

        Return False
    End Function

    ''' <summary>
    ''' 获取已唱列表
    ''' </summary>
    ''' <returns></returns>
    Public Function GetOutdatedList() As List(Of EasyKBookRecord)
        SyncLock OutdatedQueue
            Return OutdatedQueue.ToList()
        End SyncLock
    End Function

    ''' <summary>
    ''' 重新点歌
    ''' </summary>
    ''' <param name="Id">ID</param>
    ''' <param name="Order">点歌人</param>
    ''' <returns></returns>
    Public Function Reorder(Id As String, Order As String) As String
        SyncLock OutdatedQueue
            For Each Recorder As EasyKBookRecord In OutdatedQueue
                With Recorder
                    If .Id = Id Then
                        Return Book(.Title, Order, .Type, .Content)
                    End If
                End With
            Next
        End SyncLock

        SyncLock Queue
            For Each Recorder As EasyKBookRecord In Queue
                With Recorder
                    If .Id = Id Then
                        Return Book(.Title, Order, .Type, .Content)
                    End If
                End With
            Next
        End SyncLock

        Return vbNullString
    End Function

    ''' <summary>
    ''' 调整进度
    ''' </summary>
    ''' <param name="Prev">向前调整</param>
    ''' <param name="Step">步长</param>
    Public Sub Seek(Prev As Boolean, Optional [Step] As Double = 5D)
        With DLNAServer
            If .Player Is Nothing Then Return

            With .Player
                If Not PlayerForm.Playing Then Return

                Dim Offset As Single = CSng(Math.Abs([Step]) / .Duration)
                If Prev Then
                    .Position = Math.Max(Math.Min(.Position - Offset, 1), 0)
                Else
                    .Position = Math.Max(Math.Min(.Position + Offset, 1), 0)
                End If
            End With
        End With
    End Sub

    ''' <summary>
    ''' 获取已占用的缓存文件
    ''' </summary>
    ''' <returns></returns>
    Public Function GetOccupiedFiles() As List(Of String)
        Dim Occupied As New List(Of String)

        SyncLock Queue
            For Each Record As EasyKBookRecord In Queue
                With Record
                    If .Type <> EasyKType.Video Then Continue For

                    Occupied.Add(.Content)
                End With
            Next
        End SyncLock

        SyncLock OutdatedQueue
            For Each Record As EasyKBookRecord In OutdatedQueue
                With Record
                    If .Type <> EasyKType.Video Then Continue For

                    Occupied.Add(.Content)
                End With
            Next
        End SyncLock

        Return Occupied
    End Function

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

            Height = CInt(.Height * 0.25)
            Width = CInt(Height * 0.9)

            If _SavedQRPosition.X >= 0 AndAlso _SavedQRPosition.Y >= 0 Then
                X = _SavedQRPosition.X
                Y = _SavedQRPosition.Y
            Else
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
            Console.WriteLine("显示二维码失败 - 获取本机IP失败")
            Return
        End If

        '保存有效网卡
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
                AddHandler QRForm.OnPositionUpdate, AddressOf QRForm_OnPositionUpdate
            End If
        End With
    End Sub

    Private Sub QRForm_OnPositionUpdate(Pos As Point)
        _SavedQRPosition = Pos
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
            Console.WriteLine("自动显示二维码失败 - 无法获取默认网卡")
            Return False
        End If
    End Function

    ''' <summary>
    ''' 关闭二维码显示
    ''' </summary>
    Public Sub CloseQRCode()
        If QRForm Is Nothing Then Return

        If PlayerForm IsNot Nothing AndAlso Not PlayerForm.IsDisposed Then PlayerForm.Invoke(Sub() QRForm.Close())
        RemoveHandler QRForm.OnPositionUpdate, AddressOf QRForm_OnPositionUpdate
        QRForm = Nothing
    End Sub

    ''' <summary>
    ''' 刷新二维码
    ''' </summary>
    Public Sub RefreshQRCode()
        If QRForm Is Nothing OrElse QRForm.IsDisposed Then Return

        ShowQRCode(False)
    End Sub

    ''' <summary>
    ''' 获取主屏幕
    ''' </summary>
    ''' <returns></returns>
    Public Function GetMainScreen() As ScreenUtils.OverlapScreen
        If PlayerForm Is Nothing OrElse PlayerForm.IsDisposed() Then
            Return New ScreenUtils.OverlapScreen With {
                .Id = -1,
                .Screen = Nothing
            }
        Else
            With PlayerForm
                Return .Invoke(Function() ScreenUtils.GetOverlapScreen(.DesktopBounds))
            End With
        End If
    End Function

    ''' <summary>
    ''' 刷新DLNA歌词
    ''' </summary>
    Public Sub RefreshDLNALyrics()
        If DLNAServer.Player Is Nothing Then Return

        DLNAServer.Player.PullMusicLyrics()
    End Sub

    ''' <summary>
    ''' 刷新记录
    ''' </summary>
    ''' <param name="Id"></param>
    ''' <param name="Content"></param>
    Friend Sub UpdateRecord(Id As String, Content As String)
        SyncLock OutdatedQueue
            Dim Node As LinkedListNode(Of EasyKBookRecord) = OutdatedQueue.First()
            While Node IsNot Nothing
                If Node.Value.Id.Equals(Id) Then
                    '查找成功
                    Node.Value = New EasyKBookRecord(Node.Value, Content)
                    If Current IsNot Nothing AndAlso Current.Id = Id Then Current = Node.Value

                    Return
                End If

                Node = Node.Next
            End While
        End SyncLock

        SyncLock Queue
            Dim Node As LinkedListNode(Of EasyKBookRecord) = Queue.First()
            While Node IsNot Nothing
                If Node.Value.Id.Equals(Id) Then
                    '查找成功
                    Node.Value = New EasyKBookRecord(Node.Value, Content)
                    If Current IsNot Nothing AndAlso Current.Id = Id Then Current = Node.Value

                    Return
                End If

                Node = Node.Next
            End While
        End SyncLock
    End Sub

    '重启主窗体
    Private Sub RestartPlayerForm()
        Dim NewForm As FrmPlayer = Nothing

        If QRForm IsNot Nothing Then CloseQRCode()

        With PlayerForm
            .Invoke(Sub()
                        NewForm = New FrmPlayer(Me, Settings)
                        NewForm.Show()

                        .Close()
                    End Sub)

            .Dispose()
        End With

        PlayerForm = NewForm
    End Sub

    ''' <summary>
    ''' 销毁资源
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        Dummy.Dispose()
        DLNAServer.Dispose()

        Dim Storage As New CefStorage()
        Cef.GetGlobalCookieManager().VisitAllCookies(Storage)

        With PlayerForm
            Try
                .Invoke(Sub()
                            Cef.ShutdownWithoutChecks()
                            .Close()
                        End Sub)
            Catch ex As Exception
                If Settings.Settings.DebugMode Then
                    Console.WriteLine("释放主窗体出错 - {0}", ex.Message)
                End If
            End Try

            .Dispose()
        End With
        PlayerForm = Nothing

        Storage.Clean()
    End Sub

End Class
