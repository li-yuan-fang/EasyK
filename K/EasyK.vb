Imports System.Net.NetworkInformation
Imports System.Web
Imports CefSharp
Imports Newtonsoft.Json

Public Enum EasyKType
    Video = 0
    Bilibili
    DLNA
End Enum

<Serializable>
Public Class EasyKBookRecord

    <JsonProperty("id")>
    Public ReadOnly Id As String

    <JsonProperty("title")>
    Public ReadOnly Title As String

    <JsonProperty("order")>
    Public ReadOnly Order As String

    <JsonIgnore>
    Public ReadOnly Type As EasyKType

    <JsonIgnore>
    Public ReadOnly Content As String

    Public Sub New(Title As String, Order As String, Type As EasyKType, Content As String)
        Dim Id As String = Now.Ticks.ToString("x2")
        Me.Id = Id
        Me.Title = Title
        Me.Order = Order
        Me.Type = Type
        Me.Content = Content
    End Sub

End Class

Public Class EasyK
    Implements IDisposable

    Private PlayerForm As FrmMain

    Private QRForm As FrmQRCode

    Private Current As EasyKBookRecord = Nothing

    Private ReadOnly Queue As New LinkedList(Of EasyKBookRecord)

    Private ReadOnly OutdatedQueue As New LinkedList(Of EasyKBookRecord)

    Private LastValidAdapter As NetworkInterface = Nothing

    Friend ReadOnly Settings As SettingContainer

    Friend ReadOnly Dummy As DummyPlayer

    Friend _LyricOffset As Double = 0.0D

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
    ''' 投屏功能重置事件
    ''' </summary>
    Public Event OnMirrorReset()

    ''' <summary>
    ''' 投屏播放事件
    ''' </summary>
    Public Event OnMirrorPlay()

    ''' <summary>
    ''' 获取或设置播放进度(仅VLC)
    ''' </summary>
    ''' <returns></returns>
    Friend Property PlayingPosition As Single
        Get
            Try
                Return If(PlayerForm Is Nothing OrElse PlayerForm.IsDisposed(), 0, PlayerForm.Position)
            Catch
                Return 0
            End Try
        End Get
        Set(value As Single)
            If PlayerForm Is Nothing OrElse PlayerForm.IsDisposed() Then Return

            Try
                With PlayerForm
                    .Invoke(Sub() .Position = value)
                End With
            Catch
            End Try
        End Set
    End Property

    ''' <summary>
    ''' 获取或设置播放速度(仅VLC)
    ''' </summary>
    ''' <returns></returns>
    Friend Property PlayingRate As Single
        Get
            Try
                Return If(PlayerForm Is Nothing OrElse PlayerForm.IsDisposed(), 0, PlayerForm.Rate)
            Catch
                Return 1
            End Try
        End Get
        Set(value As Single)
            If PlayerForm Is Nothing OrElse PlayerForm.IsDisposed() Then Return

            Try
                With PlayerForm
                    .Invoke(Sub() .Rate = value)
                End With
            Catch
            End Try
        End Set
    End Property

    ''' <summary>
    ''' 获取视频长度(仅VLC)
    ''' </summary>
    ''' <returns></returns>
    Friend ReadOnly Property PlayingDuration As Double
        Get
            Try
                Return If(PlayerForm Is Nothing OrElse PlayerForm.IsDisposed(), 0, PlayerForm.Duration)
            Catch
                Return 0
            End Try
        End Get
    End Property

    ''' <summary>
    ''' 获取DLNA加载状态
    ''' </summary>
    ''' <returns></returns>
    Friend ReadOnly Property DLNALoading As Boolean
        Get
            Return If(PlayerForm Is Nothing OrElse PlayerForm.IsDisposed(), True, PlayerForm.DLNALoading)
        End Get
    End Property

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
    ''' 获取或设置歌词偏移
    ''' </summary>
    ''' <returns></returns>
    Public Property LyricOffset As Double
        Get
            Return _LyricOffset
        End Get
        Set(value As Double)
            If _LyricOffset <> value Then
                _LyricOffset = value
                TriggerMirrorPlay("RefreshOffset")
            End If
        End Set
    End Property

    ''' <summary>
    ''' 获取运行状态
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Running As Boolean
        Get
            Return _Running
        End Get
    End Property

    Private _Running As Boolean = False

    ''' <summary>
    ''' 初始化
    ''' </summary>
    Public Sub New(Settings As SettingContainer)
        Me.Settings = Settings
        Dummy = New DummyPlayer(Me, Settings)
        If Not Cef.IsInitialized Then Cef.Initialize(New CefSetting(Settings))

        PlayerForm = New FrmMain(Me, Settings)
        AddHandler PlayerForm.OnDLNAReset, AddressOf TriggerMirrorReset
    End Sub

    ''' <summary>
    ''' 显示主窗体
    ''' </summary>
    Public Sub Show()
        PlayerForm.Show()
    End Sub

    ''' <summary>
    ''' 部署并播放
    ''' </summary>
    Public Sub Play()
        _Running = True
        PlayerForm.Setup()

        '检测是否需要显示二维码
        If Settings.Settings.AutoShowQR Then ShowQRCode(False)

        Push()
    End Sub

    ''' <summary>
    ''' 推进播放进度/切歌
    ''' </summary>
    Public Sub Push()
        SyncLock Queue
            If Not Running Then
                Current = Nothing
                Task.Run(Sub()
                             RaiseEvent OnPlayerTerminated()
                             RestartPlayerForm()
                         End Sub)
                Return
            End If

            If Queue.Count = 0 Then
                Current = Nothing
                Task.Run(Sub() RaiseEvent OnPlayerTerminated())
                Return
            End If

            Current = Queue.First.Value
            Queue.RemoveFirst()
        End SyncLock

        SyncLock OutdatedQueue
            OutdatedQueue.AddFirst(Current)
        End SyncLock

        Task.Run(Sub()
                     RaiseEvent OnPlayerTerminated()
                     RaiseEvent OnPlayerPlay(Current.Type, Current.Content)
                 End Sub)

        With Current
            Console.WriteLine("开始播放 {0} - {1} (来自 {2})", .Title, .Content, .Order)
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
    ''' 获取播放器播放状态
    ''' </summary>
    ''' <returns></returns>
    Public Function IsPlaying() As Boolean
        If PlayerForm Is Nothing Then Return False
        With PlayerForm
            Return DirectCast(.Invoke(Function() .Playing), Boolean)
        End With
    End Function

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
        SyncLock Queue
            Dim node As LinkedListNode(Of EasyKBookRecord) = Queue.First()
            While node IsNot Nothing
                If node.Value.Id.Equals(Id) Then
                    Queue.Remove(node)
                    Queue.AddFirst(node)

                    Return node.Value
                End If

                node = node.Next
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
    Public Sub Seek(Prev As Boolean)
        If Not IsPlaying() Then Return

        PlayerForm.Invoke(Sub()
                              Dim Offset As Single = CSng(5D / PlayingDuration)
                              If Prev Then
                                  PlayingPosition = Math.Max(Math.Min(PlayingPosition - Offset, 1), 0)
                              Else
                                  PlayingPosition = Math.Max(Math.Min(PlayingPosition + Offset, 1), 0)
                              End If
                          End Sub)

        'DLNA响应
        TriggerMirrorReset()
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
    ''' 锁定主窗体到最顶层
    ''' </summary>
    ''' <returns></returns>
    Public Function Lock() As Boolean
        If PlayerForm Is Nothing OrElse PlayerForm.IsDisposed() Then Return False

        With PlayerForm
            .Invoke(Sub() .TopMost = Not .TopMost)

            Return .TopMost
        End With
    End Function

    ''' <summary>
    ''' 是否允许投屏
    ''' </summary>
    ''' <returns></returns>
    Public Function CanMirror() As Boolean
        If Current Is Nothing OrElse Current.Type <> EasyKType.DLNA Then Return False
        Return PlayerForm IsNot Nothing AndAlso PlayerForm.Setuped
    End Function

    ''' <summary>
    ''' 触发投屏播放事件
    ''' </summary>
    ''' <param name="Content">内容</param>
    Public Sub TriggerMirrorPlay(Content As String)
        RaiseEvent OnPlayerPlay(EasyKType.DLNA, Content)
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

            Height = CInt(.Height * 0.25)
            Width = CInt(Height * 0.9)

            X = CInt(.Width - Width - 1)
            Y = CInt((.Height - Height) / 2 - 1)

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
            ShowQRCode($"http://{LocalIP}:{Port}/?pass={HttpUtility.UrlEncode(Key)}", Outside)
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
                                .FormBorderStyle = Windows.Forms.FormBorderStyle.None
                                .ShowInTaskbar = False
                                .Round = True
                                .Show()

                                FormUtils.SetParent(.Handle, PlayerForm.Handle)
                            End With
                        End Sub)

                UpdateQRBounds()
            End If
        End With
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
        QRForm = Nothing
    End Sub

    ''' <summary>
    ''' 刷新二维码
    ''' </summary>
    Public Sub RefreshQRCode()
        If QRForm Is Nothing OrElse QRForm.IsDisposed Then Return

        ShowQRCode(False)
    End Sub

    '重启主窗体
    Private Sub RestartPlayerForm()
        Dim NewForm As FrmMain = Nothing

        If QRForm IsNot Nothing Then CloseQRCode()

        With PlayerForm
            RemoveHandler .OnDLNAReset, AddressOf TriggerMirrorReset

            .Invoke(Sub()
                        NewForm = New FrmMain(Me, Settings)
                        NewForm.Show()

                        .Close()
                    End Sub)

            .Dispose()
        End With

        PlayerForm = NewForm
        AddHandler PlayerForm.OnDLNAReset, AddressOf TriggerMirrorReset
    End Sub

    '触发投屏复位
    Private Sub TriggerMirrorReset()
        RaiseEvent OnMirrorReset()
    End Sub

    '触发投屏播放
    Friend Sub TriggerMirrorPlay()
        RaiseEvent OnMirrorPlay()
    End Sub

    ''' <summary>
    ''' 销毁资源
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
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
    End Sub

End Class
