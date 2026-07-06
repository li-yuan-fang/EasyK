Imports System.Drawing
Imports System.Net.NetworkInformation
Imports System.Threading
Imports CefSharp
Imports EasyK.DLNA.Player
Imports Microsoft.AspNetCore.Http
'1
Public Class EasyK
    Implements IDisposable

    Private PlayerForm As FrmPlayer

    Private WithEvents QRForm As FrmQRCode

    Private Current As EasyKBookRecord = Nothing

    Private ReadOnly Queue As New LinkedList(Of EasyKBookRecord)

    Private ReadOnly OutdatedQueue As New LinkedList(Of EasyKBookRecord)

    Private LastValidAdapter As NetworkInterface = Nothing

    Private NetworkChangeCommit As Date = Date.MinValue

    Friend ReadOnly DLNAServer As DLNA.DLNA

    Friend ReadOnly Settings As SettingContainer

    Friend ReadOnly Dummy As DummyPlayer

    Friend _LyricOffset As Double = 0.0D

    Private _Running As Boolean = False

    Private _SavedQRBounds As Rectangle

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

            If value > 0 Then
                Alert(Math.Round(value * 100.0F), AlertIcon.Volume)
            Else
                Alert("静音", AlertIcon.Mute)
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

            If value Then
                Alert("实时伴奏", AlertIcon.Accompaniment)
            Else
                Alert("原声", AlertIcon.Original)
            End If
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
    ''' 获取或设置DLNA歌词交错
    ''' </summary>
    ''' <returns></returns>
    Public Property DLNALyricIntersect As Boolean
        Get
            Return Settings.Settings.DLNA.LyricIntersect
        End Get
        Set(value As Boolean)
            With Settings.Settings.DLNA
                If .LyricIntersect <> value Then
                    .LyricIntersect = value

                    If DLNAServer.Player IsNot Nothing Then DLNAServer.Player.UpdateMusicLyricOptions()
                End If
            End With
        End Set
    End Property

    ''' <summary>
    ''' 获取或设置DLNA歌词对比度阈值
    ''' </summary>
    ''' <returns></returns>
    Public Property DLNALyricContrast As Double
        Get
            Return Settings.Settings.DLNA.LyricContrastThreshold
        End Get
        Set(value As Double)
            With Settings.Settings.DLNA
                If .LyricContrastThreshold <> value Then
                    .LyricContrastThreshold = value

                    If DLNAServer.Player IsNot Nothing Then DLNAServer.Player.UpdateMusicLyricOptions()
                End If
            End With
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

        '绑定网络变化事件
        AddHandler NetworkChange.NetworkAddressChanged, AddressOf OnNetworkAddressChange
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

            Logger.Warn("自动部署失败 - 找不到对应的屏幕")
        End With
    End Sub

    ''' <summary>
    ''' 部署
    ''' </summary>
    ''' <param name="Bounds">部署区域</param>
    Public Sub Setup(Bounds As Rectangle)
        _Running = True
        _SavedQRBounds = New Rectangle(-1, -1, 0, 0)
        PlayerForm.Setup(Bounds)

        '检测是否需要显示二维码
        If Settings.Settings.AutoShowQR Then ShowQRCode(False)

        Push()
    End Sub

    ''' <summary>
    ''' 推进播放进度/切歌
    ''' </summary>
    Public Sub Push()
        Push(False)
    End Sub

    ''' <summary>
    ''' 强制切歌
    ''' </summary>
    ''' <remarks>仅供控制台使用</remarks>
    Public Sub ForcePush()
        Interlocked.Exchange(PushLock, 0)
        Push(True)
    End Sub

    ''' <summary>
    ''' 推进播放进度/切歌
    ''' </summary>
    ''' <param name="Manual">手动切歌</param>
    Public Sub Push(Manual As Boolean)
        '原子操作 阻止短时多次切歌
        Dim value As Integer = Interlocked.Exchange(PushLock, 1)
        If value <> 0 Then Return

        Dim Temp As EasyKBookRecord

        SyncLock Queue
            If Not IsSetup Then
                Task.Run(Sub()
                             RaiseEvent OnPlayerTerminated()
                             Current = Nothing
                             RestartPlayerForm()

                             Interlocked.Exchange(PushLock, 0)
                         End Sub)
                Return
            End If

            If Manual AndAlso Current IsNot Nothing Then Alert("切歌", AlertIcon.Push)

            If Queue.Count = 0 Then
                Task.Run(Sub()
                             RaiseEvent OnPlayerTerminated()
                             Current = Nothing

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
            Logger.Info("开始播放 {0} - {1} (来自 {2})",
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
            If Settings.Settings.FairnessMode Then
                '公平插入点歌
                BookFair(Record, Queue.First)
            Else
                '常规点歌
                Queue.AddLast(Record)
            End If
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

                Dim Offset As Single = CSng(Math.Abs([Step]) / PlayerForm.Duration)
                If Prev Then
                    .Position = Math.Max(Math.Min(.Position - Offset, 1), 0)
                Else
                    .Position = Math.Max(Math.Min(.Position + Offset, 1), 0)
                End If
            End With
        End With
    End Sub

    ''' <summary>
    ''' 重唱
    ''' </summary>
    Public Sub Replay()
        If Current Is Nothing OrElse DLNAServer.Player Is Nothing Then Return

        If Current.Type = EasyKType.Bilibili Then
            '调用B站复位脚本
            PlayerForm.BiliReplay()
        Else
            'VLC播放器模式
            If Not PlayerForm.Playing Then Return
            DLNAServer.Player.Position = 0
        End If
    End Sub

    Private Shared Function GetOccupied(Record As EasyKBookRecord) As String
        With Record
            Select Case .Type
                Case EasyKType.Video
                    Return .Content
                Case EasyKType.DLNA
                    If Not .Content.StartsWith("{") Then Return vbNullString

                    Dim MusicBuffer = JsonUtils.SafeDeserializeObject(Of StoredMusic)(.Content)
                    Return If(MusicBuffer IsNot Nothing, MusicBuffer.Resource, vbNullString)
                Case Else
                    Return vbNullString
            End Select
        End With
    End Function

    ''' <summary>
    ''' 获取已占用的缓存文件
    ''' </summary>
    ''' <returns></returns>
    Public Function GetOccupiedFiles() As List(Of String)
        Dim Occupied As New List(Of String)

        SyncLock Queue
            For Each Record As EasyKBookRecord In Queue
                Dim o = GetOccupied(Record)
                If Not String.IsNullOrEmpty(o) Then Occupied.Add(o)
            Next
        End SyncLock

        SyncLock OutdatedQueue
            For Each Record As EasyKBookRecord In OutdatedQueue
                Dim o = GetOccupied(Record)
                If Not String.IsNullOrEmpty(o) Then Occupied.Add(o)
            Next
        End SyncLock

        Return Occupied
    End Function

    '发送提示消息
    Private Sub Alert(Title As String, Icon As AlertIcon)
        If PlayerForm Is Nothing Then Return

        PlayerForm.Alert(Title, Icon)
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
    ''' 已点列表随机排序
    ''' </summary>
    Public Sub Random()
        Random(Settings.Settings.BalancedBookRandom)
    End Sub

    ''' <summary>
    ''' 已点列表随机排序
    ''' </summary>
    ''' <param name="Banlanced">是否采用平衡排序算法</param>
    Public Sub Random(Banlanced As Boolean)
        If Banlanced Then
            RandomBalanced()
        Else
            If Settings.Settings.FairnessMode Then
                Settings.Settings.FairnessMode = False
                Logger.Warn("公平模式已自动关闭 - 非平衡随机排序被触发")
            End If

            RandomCommon()
        End If
    End Sub

    '随机排序
    Private Sub RandomCommon()
        Dim Rnd As New Random()

        SyncLock Queue
            Dim Saved As New List(Of EasyKBookRecord)(Queue)
            '使用 Fisher-Yates 洗牌算法进行随机排序
            For i As Integer = Saved.Count - 1 To 1 Step -1
                Dim j As Integer = Rnd.Next(i + 1)
                ' 交换元素
                Dim temp As EasyKBookRecord = Saved(i)
                Saved(i) = Saved(j)
                Saved(j) = temp
            Next

            Queue.Clear()
            For Each s In Saved
                Queue.AddLast(s)
            Next
        End SyncLock
    End Sub

    '平衡随机排序
    Private Sub RandomBalanced()
        Dim First As String = If(Current IsNot Nothing, Current.Order, vbNullString)
        Dim Pool As New Dictionary(Of String, List(Of EasyKBookRecord))

        SyncLock Queue
            '创建剩余池
            For Each Record As EasyKBookRecord In Queue
                With Record
                    If Pool.ContainsKey(.Order) Then
                        Pool(.Order).Add(Record)
                    Else
                        Pool.Add(.Order, New List(Of EasyKBookRecord)({Record}))
                    End If
                End With
            Next

            Queue.Clear()
            For Each Record As EasyKBookRecord In RandomBalanced(Pool, First)
                Queue.AddLast(Record)
            Next
        End SyncLock
    End Sub

    '平衡随机排序
    Private Function RandomBalanced(Pool As Dictionary(Of String, List(Of EasyKBookRecord)),
                                    AvoidFirst As String,
                                    Optional OnlyMemberRandom As Boolean = False) As List(Of EasyKBookRecord)
        Dim Rnd As New Random()
        Dim Members As New List(Of String)(Pool.Keys)
        Dim Result As New List(Of EasyKBookRecord)

        '查找第一个元素
        If Not String.IsNullOrEmpty(AvoidFirst) AndAlso Members.Contains(AvoidFirst) Then
            Members.Remove(AvoidFirst)

            Dim Record As EasyKBookRecord = RandomBanlancedPick(Rnd, Pool, Members, OnlyMemberRandom)
            If Record IsNot Nothing Then Result.Add(Record)

            Members.Add(AvoidFirst)
        End If

        '常规遍历
        While Members.Count > 0
            Dim Record As EasyKBookRecord = RandomBanlancedPick(Rnd, Pool, Members, OnlyMemberRandom)
            If Record IsNot Nothing Then Result.Add(Record)
        End While

        '深度搜索
        With Result
            If .Count > 0 Then
                .AddRange(RandomBalanced(Pool, .Last().Order))
            End If
        End With

        Return Result
    End Function

    '随机提取一条记录
    Private Function RandomBanlancedPick(Rnd As Random,
                                         Pool As Dictionary(Of String, List(Of EasyKBookRecord)),
                                         Members As List(Of String),
                                         OnlyMemberRandom As Boolean) As EasyKBookRecord
        '随机选出点歌人
        If Members.Count <= 0 Then Return Nothing

        Dim MemberIndex As Integer = Rnd.Next(Members.Count)
        Dim Member As String = Members(MemberIndex)
        Members.RemoveAt(MemberIndex)

        '随机选歌
        If Not Pool.ContainsKey(Member) Then Return Nothing

        Dim Count As Integer = Pool(Member).Count
        If Count <= 0 Then Return Nothing

        '检查是否需要随机选取指定成员的歌曲
        Dim RecordIndex As Integer = If(OnlyMemberRandom, 0, Rnd.Next(Count))

        Dim Record As EasyKBookRecord = Pool(Member)(RecordIndex)
        Pool(Member).RemoveAt(RecordIndex)

        Return Record
    End Function

    '检测顺序是否公平
    Private Shared Function IsRankFair([Next] As LinkedListNode(Of EasyKBookRecord)) As Boolean
        If [Next] Is Nothing Then Return True

        Dim Counter As Integer = 0
        Dim Members As New HashSet(Of String)
        While [Next] IsNot Nothing
            Dim Order = [Next].Value.Order

            If Members.Contains(Order) Then
                '出现重复 可能完成了一轮
                If Members.Count = Counter Then
                    '当前轮公平 继续下一轮
                    Return IsRankFair([Next])
                Else
                    '当前轮不公平
                    Return False
                End If
            Else
                '未出现重复
                Members.Add(Order)
            End If

            Counter += 1
            [Next] = [Next].Next
        End While

        Return Members.Count = Counter
    End Function

    ''' <summary>
    ''' 公平重排序
    ''' </summary>
    Public Sub ReRankFair()
        SyncLock Queue
            '检测是否需要重排序
            If IsRankFair(Queue.First) Then Return

            '锁定首位点歌人
            Dim First As String = If(Current IsNot Nothing, Current.Order, vbNullString)

            '创建剩余池
            Dim Pool As New Dictionary(Of String, List(Of EasyKBookRecord))
            For Each Record As EasyKBookRecord In Queue
                With Record
                    If Pool.ContainsKey(.Order) Then
                        Pool(.Order).Add(Record)
                    Else
                        Pool.Add(.Order, New List(Of EasyKBookRecord)({Record}))
                    End If
                End With
            Next

            '运行完全重排序
            Queue.Clear()
            For Each Record As EasyKBookRecord In RandomBalanced(Pool, First, True)
                Queue.AddLast(Record)
            Next
        End SyncLock
    End Sub

    '公平点歌(增量算法)
    Private Sub BookFair(Record As EasyKBookRecord, Node As LinkedListNode(Of EasyKBookRecord))
        Dim Members As New HashSet(Of String)
        While Node IsNot Nothing
            Dim Order = Node.Value.Order

            If Members.Contains(Order) Then
                '出现重复 可能完成了一轮
                If Members.Contains(Record.Order) Then
                    '本轮已插入 继续查找
                    BookFair(Record, Node)
                Else
                    '本轮未插入 则插入本轮最后一位
                    Queue.AddBefore(Node, Record)
                End If

                Return
            Else
                '未出现重复
                Members.Add(Order)
            End If

            Node = Node.Next
        End While

        '查找结束但是未找到插入点 直接放到最后
        Queue.AddLast(Record)
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
        RemoveHandler NetworkChange.NetworkAddressChanged, AddressOf OnNetworkAddressChange

        Dummy.Dispose()
        DLNAServer.Dispose()

        Dim Storage As New CefStorage()
        Cef.GetGlobalCookieManager().VisitAllCookies(Storage)

        With PlayerForm
            Try
                .Invoke(Sub()
                            Cef.Shutdown()
                            .Close()
                        End Sub)
            Catch ex As Exception
                Logger.Debug("释放主窗体出错 - {0}", ex.Message)
            End Try

            .Dispose()
        End With
        PlayerForm = Nothing

        Storage.Clean()
    End Sub

End Class
