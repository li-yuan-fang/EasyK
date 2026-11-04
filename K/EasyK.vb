Imports System.Drawing
Imports System.Threading
Imports Newtonsoft.Json
Imports CefSharp
Imports EasyK.DLNA.Player
Imports Microsoft.AspNetCore.Http

Public Class EasyK
    Implements IDisposable

    Private Current As EasyKBookRecord = Nothing

    Private ReadOnly Queue As New LinkedList(Of EasyKBookRecord)

    Private ReadOnly OutdatedQueue As New LinkedList(Of EasyKBookRecord)

    Private ReadOnly DLNAServer As DLNAServerProvider

    Private ReadOnly _Player As EasyKPlayer

    Friend ReadOnly Settings As SettingContainer

    Private Blocked As String = vbNullString

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
    ''' 获取二维码管理器
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property QRManager As QRManager

    ''' <summary>
    ''' 获取播放器对象
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Player As KPlayer
        Get
            Return _Player
        End Get
    End Property

    ''' <summary>
    ''' 获取播放器管理器
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property PlayerManager As KPlayerManager
        Get
            Return _Player
        End Get
    End Property

    ''' <summary>
    ''' 获取DLNA服务管理器
    ''' </summary>
    ''' <returns></returns>
    Friend ReadOnly Property DLNAManager As DLNAServerManager
        Get
            Return DLNAServer
        End Get
    End Property

    ''' <summary>
    ''' 获取DLNA服务对象
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property DLNAProvider As DLNAServer
        Get
            Return DLNAServer
        End Get
    End Property

    ''' <summary>
    ''' 初始化
    ''' </summary>
    Public Sub New(Settings As SettingContainer)
        Me.Settings = Settings

        '初始化CefSharp
        If Not Cef.IsInitialized Then Cef.Initialize(New CefSetting(Settings))

        '加载DLNA插件
        DLNA.MusicProvider.DLNAMusicProviders.LoadProviders(Settings)

        '加载DLNA服务
        DLNAServer = New DLNAServerProvider(Me, Settings, AddressOf UpdateRecord, AddressOf DLNAAccessCheck)

        '尝试恢复点歌记录
        Load()

        QRManager = New QRManager(Settings)

        _Player = New EasyKPlayer(Me, Settings, QRManager)
        QRManager.GetPlayerForm = Function() PlayerManager.PlayerForm
    End Sub

    'DLNA访问权限检查
    Private Function DLNAAccessCheck(ctx As HttpContext) As Boolean
        If Not Player.IsSetup OrElse Current Is Nothing Then Return False

        If Current.Type <> EasyKType.DLNA Then Return False

        '检测访问者
        If Settings.Settings.DLNA.StrictPermission AndAlso Not String.IsNullOrEmpty(Current.Content) AndAlso
            Current.Content <> ctx.Connection.RemoteIpAddress.ToString() AndAlso
            Not NetUtils.LocalAddresses.Contains(Current.Content) Then

            Logger.Warn("{0} 试图越权访问DLNA服务被拒", ctx.Connection.RemoteIpAddress.ToString())

            Return False
        End If

        Return True
    End Function

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
        If Current IsNot Nothing AndAlso Current.Id = Blocked Then Blocked = vbNullString

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

        '检查并处理顶歌锁定状态
        If Current IsNot Nothing AndAlso Current.Id = Blocked Then
            If Manual Then
                '阻止切歌
                Logger.Warn("已拦截非法切歌请求")

                Interlocked.Exchange(PushLock, 0)
                Return
            End If

            '解锁
            Blocked = vbNullString
        End If

        Dim Temp As EasyKBookRecord

        SyncLock Queue
            If Not Player.IsSetup Then
                Task.Run(Sub()
                             RaiseEvent OnPlayerTerminated()
                             Current = Nothing
                             PlayerManager.RestartPlayerForm()

                             Interlocked.Exchange(PushLock, 0)
                         End Sub)
                Return
            End If

            If Manual AndAlso Current IsNot Nothing Then PlayerManager.Alert("切歌", AlertIcon.Push)

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
                              .Content.Debug(),
                              .Order)
        End With
    End Sub

    ''' <summary>
    ''' 暂停
    ''' </summary>
    Friend Sub Pause()
        If Current Is Nothing Then Return
        Task.Run(Sub() RaiseEvent OnPlayerPause(Current.Type))
    End Sub

    ''' <summary>
    ''' 复位
    ''' </summary>
    ''' <param name="Now">立刻复位</param>
    Public Sub Reset(Now As Boolean)
        If Now Then
            PlayerManager.RestartPlayerForm()
            Current = Nothing
        ElseIf Current Is Nothing Then
            PlayerManager.RestartPlayerForm()
        End If
    End Sub

    ''' <summary>
    ''' 顶歌
    ''' </summary>
    ''' <param name="Id">ID</param>
    Public Function SendToTop(Id As String) As EasyKBookRecord
        Return RankBook(Id, 0)
    End Function

    '检查顶歌权限
    Private Function CheckRankPermission(Id As String, Rank As Integer) As Boolean
        If String.IsNullOrEmpty(Blocked) Then Return True

        If Blocked = Id Then Return Rank = 0

        '要考虑正在播放的情况
        If Current IsNot Nothing AndAlso Blocked = Current.Id Then Return True

        Return Rank <> 0
    End Function

    ''' <summary>
    ''' 已点歌曲重排序
    ''' </summary>
    ''' <param name="Id">ID</param>
    ''' <param name="Rank">序号</param>
    ''' <returns></returns>
    Public Function RankBook(Id As String, Rank As Integer) As EasyKBookRecord
        If Not CheckRankPermission(Id, Rank) Then
            Logger.Warn("已拦截非法顶歌请求")
            Return Nothing
        End If

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
    ''' 锁定顶歌
    ''' </summary>
    ''' <returns></returns>
    Public Function Block() As Boolean
        If Not String.IsNullOrEmpty(Blocked) Then
            Logger.Info("锁定顶歌已解除(ID: {0})", Blocked)

            Blocked = vbNullString
            Return True
        End If

        Dim Id As String = vbNullString
        SyncLock Queue
            If Queue.Count > 0 Then Id = Queue.First.Value.Id
        End SyncLock

        If String.IsNullOrEmpty(Id) Then
            If Current Is Nothing Then Return False

            Return Block(Current.Id)
        End If

        Return Block(Id)
    End Function

    ''' <summary>
    ''' 锁定顶歌
    ''' </summary>
    ''' <param name="Id">ID</param>
    ''' <returns></returns>
    Public Function Block(Id As String) As Boolean
        Blocked = Id
        If RankBook(Id, 0) IsNot Nothing Then Return True
        If Current IsNot Nothing AndAlso Current.Id = Id Then Return True

        '撤销锁定
        Blocked = vbNullString
        Return False
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

    '公平移除
    Private Sub RemoveFair(ToRemove As LinkedListNode(Of EasyKBookRecord))
        Dim Order As String = ToRemove.Value.Order

        Dim ValidCandidates As New List(Of LinkedListNode(Of EasyKBookRecord)) From {ToRemove}
        Dim Node = ToRemove.Next()
        While Node IsNot Nothing
            With Node.Value
                If .Order = Order Then
                    '成功找到一个候选节点
                    ValidCandidates.Add(Node)
                End If
            End With

            Node = Node.Next()
        End While

        If ValidCandidates.Count > 1 Then
            '移动节点内容
            For i = 0 To ValidCandidates.Count - 2
                ValidCandidates(i).Value = ValidCandidates(i + 1).Value
            Next
        End If

        '删除末尾节点
        Queue.Remove(ValidCandidates.Last())
    End Sub

    ''' <summary>
    ''' 移除歌曲
    ''' </summary>
    ''' <param name="Id">ID</param>
    ''' <returns></returns>
    Public Function Remove(Id As String) As EasyKBookRecord
        If Id = Blocked Then Return Nothing

        SyncLock Queue
            Dim node As LinkedListNode(Of EasyKBookRecord) = Queue.First()
            While node IsNot Nothing
                If node.Value.Id.Equals(Id) Then
                    If Settings.Settings.FairnessMode Then
                        '公平移除
                        RemoveFair(node)
                    Else
                        '常规移除
                        Queue.Remove(node)
                    End If

                    Return node.Value
                End If

                node = node.Next
            End While
        End SyncLock

        Return Nothing
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
                                    AvoidFirst As String) As List(Of EasyKBookRecord)
        Dim Rnd As New Random()
        Dim Members As New List(Of String)(Pool.Keys)
        Dim Result As New List(Of EasyKBookRecord)

        '查找第一个元素
        If Not String.IsNullOrEmpty(AvoidFirst) AndAlso Members.Contains(AvoidFirst) Then
            Members.Remove(AvoidFirst)

            Dim Record As EasyKBookRecord = RandomBanlancedPick(Rnd, Pool, Members)
            If Record IsNot Nothing Then Result.Add(Record)

            Members.Add(AvoidFirst)
        End If

        '常规遍历
        While Members.Count > 0
            Dim Record As EasyKBookRecord = RandomBanlancedPick(Rnd, Pool, Members)
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
                                         Members As List(Of String)) As EasyKBookRecord
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
        Dim RecordIndex As Integer = Rnd.Next(Count)

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
            '创建剩余池和点歌顺序
            Dim Pool As New Dictionary(Of String, List(Of EasyKBookRecord))
            Dim Members As New SortedSet(Of String)
            For Each Record As EasyKBookRecord In Queue
                With Record
                    Members.Add(.Order)
                    If Pool.ContainsKey(.Order) Then
                        Pool(.Order).Add(Record)
                    Else
                        Pool.Add(.Order, New List(Of EasyKBookRecord)({Record}))
                    End If
                End With
            Next

            If Current IsNot Nothing Then
                With Current
                    Members.Remove(.Order)
                    Members.Add(.Order)
                End With
            End If

            '运行完全重排序
            Dim Total As Integer = Queue.Count
            Queue.Clear()
            While Total > 0
                For Each m In Members
                    Dim r As List(Of EasyKBookRecord) = Pool(m)
                    If r.Count = 0 Then Continue For

                    Queue.AddLast(r(0))
                    Total -= 1
                Next
            End While
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
    Private Sub UpdateRecord(Id As String, Content As String)
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

    ''' <summary>
    ''' 保存点歌记录
    ''' </summary>
    Public Sub Save()
        Dim Saved As New SavedKRecords()

        With Saved
            With .Queue
                SyncLock Queue
                    For Each Record In Queue
                        .Add(New SavedKRecords.Record(Record))
                    Next
                End SyncLock
            End With

            With .Outdated
                SyncLock OutdatedQueue
                    For Each Record In OutdatedQueue
                        .Add(New SavedKRecords.Record(Record))
                    Next
                End SyncLock
            End With
        End With

        Settings.Settings.SavedList = JsonConvert.SerializeObject(Saved)
    End Sub

    '加载点歌记录
    Private Sub Load()
        If String.IsNullOrEmpty(Settings.Settings.SavedList) Then Return

        Dim Saved As SavedKRecords = JsonUtils.SafeDeserializeObject(Of SavedKRecords)(Settings.Settings.SavedList)
        SyncLock Queue
            For Each Record In Saved.Queue
                Dim r = Record.Recover()
                If r Is Nothing Then
                    Logger.Error("恢复点歌记录时出错 - {0}", Record.ToString())
                    Continue For
                End If

                Queue.AddLast(r)
            Next
        End SyncLock
        SyncLock OutdatedQueue
            For Each Record In Saved.Outdated
                Dim r = Record.Recover()
                If r Is Nothing Then
                    Logger.Error("恢复点歌记录时出错 - {0}", Record.ToString())
                    Continue For
                End If

                OutdatedQueue.AddLast(r)
            Next
        End SyncLock

        '清除缓存
        Settings.Settings.SavedList = vbNullString

        Logger.Info("恢复点歌记录完成")
    End Sub

    ''' <summary>
    ''' 销毁资源
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        QRManager.Dispose()

        DLNAServer.Dispose()

        Dim Storage As New CefStorage()
        Cef.GetGlobalCookieManager().VisitAllCookies(Storage)

        _Player.Dispose()

        Storage.Clean()
    End Sub

End Class
