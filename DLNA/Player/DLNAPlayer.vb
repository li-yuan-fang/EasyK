Imports System.Threading
Imports System.Windows.Forms
Imports EasyK.DLNA.MusicProvider
Imports LibVLCSharp.Shared
Imports Newtonsoft.Json

Namespace DLNA.Player

    ''' <summary>
    ''' 投屏信息更新操作
    ''' </summary>
    ''' <param name="Id">ID</param>
    ''' <param name="Content">内容</param>
    Public Delegate Sub UpdateRecord(Id As String, Content As String)

    Public Class DLNAPlayer
        Implements IDisposable

        '所有的操作必须引入检查
        'DLNAPlayer绑定于FrmPlayer上(初始化于Setup之后)
        '需要考虑外部操作的影响
        '面板/指令(pause、push、seek)
        'DLNA(play、stop、pause、seek)

        ''' <summary>
        ''' 播放事件
        ''' </summary>
        Public Event OnPlay()

        ''' <summary>
        ''' 终止事件
        ''' </summary>
        Public Event OnTerminated()

        ''' <summary>
        ''' 暂停事件
        ''' </summary>
        Public Event OnPause()

        Private WithEvents K As EasyK

        Private ReadOnly Player As FrmPlayer

        Private ReadOnly TempFolder As String

        Private ReadOnly VLCLib As LibVLC

        Private ReadOnly BrowserLoaded As ManualResetEventSlim

        '当前点歌信息缓存
        Private CurrentRecord As EasyKBookRecord = Nothing

        '音乐模式缓存
        Private MusicBuffer As StoredMusic = Nothing

        '取消标志(出错 自动切歌)
        Private Cancelled As Boolean = False

        '下载完成标志
        Private Downloaded As Boolean = False

        '解析标志
        Private ReadOnly ResourceParsed As New ManualResetEventSlim(False)

        '上一次解析的标题(用于防止连播)
        Private LastTitle As String = vbNullString

        '音乐模式
        Private _MusicMode As Boolean = False

        ''' <summary>
        ''' 获取音乐模式状态
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property MusicMode As Boolean
            Get
                Return _MusicMode
            End Get
        End Property

        '等待连接
        '只能用作显示 正在等待投屏 的标志
        '不能用于检测 SetURI 权限
        '(比如B站可以中途修改画质 更新URI)
        Private _Waiting As Boolean = False

        ''' <summary>
        ''' 获取等待连接状态
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Waiting As Boolean
            Get
                Return _Waiting
            End Get
        End Property

        Private LoadingCountdown As CountdownEvent = Nothing

        ''' <summary>
        ''' 获取加载状态
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Loading As Boolean
            Get
                Return LoadingCountdown IsNot Nothing AndAlso Not LoadingCountdown.IsSet()
            End Get
        End Property

        ''' <summary>
        ''' 获取播放状态
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Playing As Boolean
            Get
                Dim Current As EasyKBookRecord = K.GetCurrent()
                If Current Is Nothing OrElse Current.Type <> EasyKType.DLNA Then Return False

                With Player
                    Return DirectCast(.Invoke(Function() .VLCPlayer.MediaPlayer.IsPlaying), Boolean)
                End With
            End Get
        End Property

        Private _Duration As Double = 0

        ''' <summary>
        ''' 获取播放时长
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Duration As Double
            Get
                Return Math.Max(_Duration, 0)
            End Get
        End Property

        ''' <summary>
        ''' 获取或设置播放进度
        ''' </summary>
        ''' <returns></returns>
        Public Property Position As Single
            Get
                With Player
                    Return DirectCast(.Invoke(Function() .VLCPlayer.MediaPlayer.Position), Single)
                End With
            End Get
            Set(value As Single)
                With Player
                    .Invoke(Sub() .VLCPlayer.MediaPlayer.Position = value)
                End With

                UpdateMusicState()
            End Set
        End Property

        ''' <summary>
        ''' 获取或设置播放速度
        ''' </summary>
        ''' <returns></returns>
        Public Property Rate As Single
            Get
                With Player
                    Return DirectCast(.Invoke(Function() .VLCPlayer.MediaPlayer.Rate), Single)
                End With
            End Get
            Set(value As Single)
                With Player
                    .Invoke(Sub() .VLCPlayer.MediaPlayer.SetRate(value))
                End With

                UpdateMusicState()
            End Set
        End Property

        ''' <summary>
        ''' 更新任务操作
        ''' </summary>
        ''' <returns></returns>
        Public Property Update As UpdateRecord = Sub() Return

        ''' <summary>
        ''' 初始化
        ''' </summary>
        ''' <param name="K"></param>
        ''' <param name="Player">播放器窗口</param>
        ''' <param name="VLCLib"></param>
        ''' <param name="BrowserLoaded">浏览器加载信号量</param>
        Public Sub New(K As EasyK, Player As FrmPlayer, VLCLib As LibVLC, BrowserLoaded As ManualResetEventSlim)
            Me.K = K
            Me.Player = Player
            Me.VLCLib = VLCLib
            Me.BrowserLoaded = BrowserLoaded

            TempFolder = IO.Path.Combine(Application.StartupPath, K.Settings.Settings.TempFolder)

            With K
                AddHandler .OnPlayerPlay, AddressOf OnPlayerPlay
                AddHandler .OnPlayerPause, AddressOf OnPlayerPause
                AddHandler .OnPlayerTerminated, AddressOf OnPlayerTerminated
            End With
        End Sub

        '访问检查
        Private Function Accessible() As Boolean
            Dim Current As EasyKBookRecord = K.GetCurrent()
            If Current Is Nothing OrElse Current.Type <> EasyKType.DLNA Then Return False

            Return True
        End Function

        '取消加载
        Private Sub Cancel()
            Cancelled = True

            ResourceParsed.Set()

            If LoadingCountdown Is Nothing OrElse LoadingCountdown.IsSet Then Return
            Try
                LoadingCountdown.Signal(LoadingCountdown.CurrentCount)
            Catch ex As Exception
                If K.Settings.Settings.DebugMode Then
                    Console.WriteLine("中断DLNA播放时出错 - {0}", ex.Message)
                End If
            End Try
        End Sub

        '下载资源
        Private Async Function Download(Url As String) As Task(Of String)
            '生成可用文件名
            Dim FileName As String = IO.Path.Combine(TempFolder, MusicBuffer.Resource)

            '下载
            Using wc As New Net.WebClient()
                Await wc.DownloadFileTaskAsync(Url, FileName)
            End Using

            If Not IO.File.Exists(FileName) Then Throw New DataException("获取远程资源失败")

            If Cancelled Then Throw New InvalidOperationException("操作被取消")

            '更新标志
            Downloaded = True

            Return FileName
        End Function

        '尝试先下载VLC无法直接解析的远程资源
        Private Sub TryDownloadFirst(Url As String)
            If Not NetUtils.IsURL(Url) Then
                Console.WriteLine("远程资源地址无效")
                With CurrentRecord
                    Console.WriteLine("已自动切歌 - {0}(点歌人: {1} ID:{2})", .Title, .Order, .Id)
                End With

                '切歌
                If Accessible() AndAlso Not Cancelled Then
                    Cancel()
                    K.Push()
                End If

                Return
            End If

            '下载资源
            Download(Url).ContinueWith(Sub(t)
                                           Dim ex As AggregateException = t.Exception
                                           If ex IsNot Nothing Then
                                               Console.WriteLine("远程资源下载失败 - {0}", ex.InnerException.Message)
                                               With CurrentRecord
                                                   Console.WriteLine("已自动切歌 - {0}(点歌人: {1} ID:{2})", .Title, .Order, .Id)
                                               End With

                                               '切歌
                                               If Accessible() AndAlso Not Cancelled Then
                                                   Cancel()
                                                   K.Push()
                                               End If
                                               Return
                                           End If

                                           '先禁用Downloaded标志 避免缓存未检查的资源
                                           Downloaded = False

                                           Dim FileName = t.Result
                                           With Player
                                               Dim e As Exception = .Invoke(Function()
                                                                                With .VLCPlayer.MediaPlayer
                                                                                    Try
                                                                                        If .Media IsNot Nothing Then .Media.Dispose()

                                                                                        .Media = New Media(VLCLib, FileName)
                                                                                        .Media.Parse().Wait()

                                                                                        _Duration = .Media.Duration / 1000
                                                                                        If _Duration <= 0 Then
                                                                                            _Duration = 0
                                                                                            Throw New DataException("无法解析的资源")
                                                                                        End If
                                                                                    Catch excp As Exception
                                                                                        Return excp
                                                                                    End Try

                                                                                    Return Nothing
                                                                                End With
                                                                            End Function)

                                               If ex IsNot Nothing Then
                                                   '已下载资源出错
                                                   Console.WriteLine("远程资源无效 - {0}", ex.Message)
                                                   With CurrentRecord
                                                       Console.WriteLine("已自动切歌 - {0}(点歌人: {1} ID:{2})", .Title, .Order, .Id)
                                                   End With

                                                   '切歌
                                                   If Accessible() AndAlso Not Cancelled Then
                                                       Cancel()
                                                       K.Push()
                                                   End If
                                                   Return
                                               End If
                                           End With

                                           '检查是否已取消
                                           If Not Accessible() OrElse Cancelled Then Return

                                           '更新Download标志
                                           Downloaded = True

                                           '触发加载
                                           Try
                                               If Not LoadingCountdown.IsSet Then LoadingCountdown.Signal()
                                           Catch excp As Exception
                                               If K.Settings.Settings.DebugMode Then
                                                   Console.WriteLine("触发DLNA加载时出错 - {0}", excp.Message)
                                               End If
                                           End Try
                                           ResourceParsed.Set()

                                           '发送播放消息
                                           RaiseEvent OnPlay()
                                       End Sub)
        End Sub

        ''' <summary>
        ''' 资源处理(必须在委托里调用)
        ''' </summary>
        ''' <param name="URI">资源路径</param>
        ''' <param name="Buffered">缓存状态</param>
        Private Sub HandleResource(URI As String, Buffered As Boolean)
            With Player.VLCPlayer.MediaPlayer
                .Media = New Media(VLCLib, URI, FromType.FromLocation)
                Dim ParseTask = .Media.Parse(MediaParseOptions.ParseLocal Or MediaParseOptions.ParseNetwork)
                ParseTask.ContinueWith(Sub()
                                           _Duration = .Media.Duration / 1000D

                                           '检测是否取消
                                           If Not Accessible() OrElse Cancelled Then
                                               _Duration = 0
                                               Return
                                           End If

                                           '检测加载来源是否是缓存资源
                                           If Buffered Then
                                               If _Duration <= 0 Then
                                                   '缓存无效
                                                   '尝试转入正常投屏模式

                                                   Console.WriteLine("缓存资源无效 切换到常规投屏模式...")

                                                   '取消加载
                                                   Cancel()

                                                   '更新信息
                                                   Update.Invoke(CurrentRecord.Id, MusicBuffer.Original)
                                                   CurrentRecord = K.GetCurrent()
                                                   _Waiting = True

                                                   '更新UI
                                                   With Player
                                                       .Invoke(Sub()
                                                                   .VLCPlayer.Visible = False
                                                                   .Browser.Visible = False
                                                                   .Refresh()
                                                               End Sub)
                                                   End With

                                                   '复位取消标志
                                                   Cancelled = False
                                               Else
                                                   '触发标志
                                                   Try
                                                       If Not LoadingCountdown.IsSet Then LoadingCountdown.Signal()
                                                   Catch ex As Exception
                                                       If K.Settings.Settings.DebugMode Then
                                                           Console.WriteLine("触发DLNA加载时出错 - {0}", ex.Message)
                                                       End If
                                                   End Try
                                                   ResourceParsed.Set()

                                                   '播放
                                                   Play()
                                               End If

                                               Return
                                           End If

                                           '不处于缓存模式 正常处理
                                           If _Duration <= 0 Then
                                               '主要是网易云已缓存的flac资源
                                               '网易云会通过app局域网传输资源
                                               '但是Content-Type: audio/mpeg
                                               '而不是audio/flac
                                               '导致VLC无法正常识别

                                               '无法识别资源 尝试先下载
                                               TryDownloadFirst(URI)
                                           Else
                                               Try
                                                   If Not LoadingCountdown.IsSet Then LoadingCountdown.Signal()
                                               Catch e As Exception
                                                   If K.Settings.Settings.DebugMode Then
                                                       Console.WriteLine("触发DLNA加载时出错 - {0}", e.Message)
                                                   End If
                                               End Try
                                               ResourceParsed.Set()

                                               '缓存模式处理
                                               If Not MusicMode OrElse Not Settings.Settings.DLNA.MusicBufferMode Then Return

                                               Dim TaskDownload = Download(URI)
                                               Dim ex As AggregateException = TaskDownload.Exception
                                               If ex IsNot Nothing Then
                                                   Console.WriteLine("资源缓存失败 - {0}", ex.InnerException.Message)
                                                   With CurrentRecord
                                                       Console.WriteLine("将不会缓存 {0}(点歌人: {1} ID:{2})", .Title, .Order, .Id)
                                                   End With
                                               End If
                                           End If
                                       End Sub)
            End With
        End Sub

        ''' <summary>
        ''' 更新音乐模式播放器状态
        ''' </summary>
        Public Sub UpdateMusicState()
            If Not Accessible() OrElse Cancelled OrElse Not MusicMode Then Return

            Task.Run(Sub()
                         BrowserLoaded.Wait()
                         If Not Accessible() OrElse Cancelled OrElse Not MusicMode Then Return

                         Try
                             Player.RunScript(DLNAMusicProviders.GenerateUpdateStateScript(Playing, Rate, Position))
                         Catch ex As Exception
                             If K.Settings.Settings.DebugMode Then
                                 Console.WriteLine("DLNA音乐模式更新播放状态出错 - {0}", ex.Message)
                             End If
                         End Try
                     End Sub)
        End Sub

        ''' <summary>
        ''' 更新音乐模式歌词偏移
        ''' </summary>
        Public Sub UpdateMusicLyricOffset()
            If Not Accessible() OrElse Cancelled OrElse Not MusicMode Then Return

            Task.Run(Sub()
                         BrowserLoaded.Wait()
                         If Not Accessible() OrElse Cancelled Then Return

                         Try
                             Player.RunScript(DLNAMusicProviders.GenerateUpdateOffsetScript(K.DLNALyricOffset))
                         Catch ex As Exception
                             If K.Settings.Settings.DebugMode Then
                                 Console.WriteLine("DLNA音乐模式更新歌词偏移出错 - {0}", ex.Message)
                             End If
                         End Try
                     End Sub)
        End Sub

        ''' <summary>
        ''' 重新拉取音乐模式歌词
        ''' </summary>
        Public Sub PullMusicLyrics()
            If Not Accessible() OrElse Cancelled OrElse Not MusicMode Then Return

            Task.Run(Sub()
                         Dim Lyric As String = DLNAMusicProviders.GenerateUpdateLyricScript(MusicBuffer.Meta)
                         If String.IsNullOrEmpty(Lyric) Then
                             If K.Settings.Settings.DebugMode Then Console.WriteLine("DLNA音乐模式无法获取歌词")

                             '触发
                             If LoadingCountdown IsNot Nothing AndAlso Not LoadingCountdown.IsSet Then LoadingCountdown.Signal()

                             Return
                         End If

                         '等待浏览器加载
                         BrowserLoaded.Wait()
                         If Not Accessible() OrElse Cancelled Then Return

                         '执行脚本
                         Try
                             Player.RunScript(Lyric)
                         Catch ex As Exception
                             If K.Settings.Settings.DebugMode Then
                                 Console.WriteLine("DLNA音乐模式获取歌词出错 - {0}", ex.Message)
                             End If
                         End Try

                         '触发
                         If LoadingCountdown IsNot Nothing AndAlso Not LoadingCountdown.IsSet Then LoadingCountdown.Signal()
                     End Sub)
        End Sub

        '处理音乐模式元数据
        Private Sub LoadMusicMode()
            '加载UI
            BrowserLoaded.Reset()
            With Player
                .Invoke(Sub()
                            .VLCPlayer.Visible = False

                            With .Browser
                                .LoadUrl($"file:///{IO.Path.Combine(Application.StartupPath, "wwwroot", "dlna", "music_box.html").Replace("\", "/")}")
                                .Visible = True
                            End With
                        End Sub)
            End With

            '异步拉取歌词
            PullMusicLyrics()

            With MusicBuffer

                '同步加载属性信息
                If .Attribute Is Nothing Then .Attribute = DLNAMusicProviders.ParseMusicAttribute(.Meta)
                If .Attribute Is Nothing Then
                    '放弃加载音乐信息
                    Try
                        If Not LoadingCountdown.IsSet Then LoadingCountdown.Signal()
                    Catch ex As Exception
                        If K.Settings.Settings.DebugMode Then
                            Console.WriteLine("触发DLNA加载时出错 - {0}", ex.Message)
                        End If
                    End Try

                    Return
                End If

                '获取歌词颜色
                If String.IsNullOrEmpty(.LyricColor) Then
                    .LyricColor = DLNAMusicProviders.GenerateUpdateLyricColorScript(
                        .Meta,
                        .Attribute,
                        K.Settings.Settings.DLNA.LyricHighlight
                    )
                End If

                '获取默认时长
                Dim DefaultDuration As Long = 0
                If .Attribute.Duration <= 0 Then
                    ResourceParsed.Wait()
                    If Not Accessible() OrElse Cancelled Then Return

                    DefaultDuration = Duration
                End If

                '生成信息脚本
                Dim Base = DLNAMusicProviders.GenerateUpdateMusicScript(.Attribute, CurrentRecord.Title, DefaultDuration)

                '等待浏览器加载
                BrowserLoaded.Wait()
                If Not Accessible() OrElse Cancelled Then Return

                '执行脚本
                Try
                    If String.IsNullOrEmpty(.LyricColor) Then
                        Player.RunScript(Base)
                    Else
                        Player.RunScript(Base, .LyricColor)
                    End If
                Catch ex As Exception
                    If K.Settings.Settings.DebugMode Then
                        Console.WriteLine("DLNA音乐模式更新信息出错 - {0}", ex.Message)
                    End If
                End Try

                '触发加载
                Try
                    If Not LoadingCountdown.IsSet Then LoadingCountdown.Signal()
                Catch ex As Exception
                    If K.Settings.Settings.DebugMode Then
                        Console.WriteLine("触发DLNA加载时出错 - {0}", ex.Message)
                    End If
                End Try
            End With
        End Sub

        '获取标题
        Private Shared Function GetMetaTitle(Doc As XDocument) As String
            Dim Elements = From el In Doc.Descendants(DLNAMusicProviders.MetaNamespace + "item") Select el

            For Each Item In Elements
                Dim Title = Item.Element(DLNAMusicProviders.DCNamespace + "title")
                If String.IsNullOrEmpty(Title.Value) OrElse Title.Value.ToLower() = "null" Then Continue For

                Return Title.Value
            Next

            Return vbNullString
        End Function

        '检测连续投屏
        Private Function CheckContinueByTime() As Boolean
            If Duration > 0 Then
                Dim Remain As Single = (1 - Position) * Duration
                If Remain > 0 AndAlso Remain < Settings.Settings.DLNA.PreventContinueRange Then Return True
            End If

            Return False
        End Function

        ''' <summary>
        ''' 提交DLNA资源
        ''' </summary>
        ''' <param name="URI">资源URI</param>
        ''' <param name="Meta">元数据</param>
        ''' <exception cref="InvalidOperationException">禁止自动连续投屏操作</exception>
        Public Sub CommitResource(URI As String, Meta As String)
            '只在常规投屏下触发
            '可以是初次加载 也可以是换源

            Dim MDoc As XDocument = XmlUtils.SafeParseXml(Meta)

            If Waiting Then
                '初次加载

                '获取标题
                LastTitle = GetMetaTitle(MDoc)

                '检测音乐模式
                _MusicMode = DLNAMusicProviders.IsMusicMeta(MDoc)
                If _MusicMode Then
                    '生成可用文件名
                    Dim FileName As String = vbNullString
                    While String.IsNullOrEmpty(FileName) OrElse IO.File.Exists(IO.Path.Combine(TempFolder, FileName))
                        FileName = Guid.NewGuid().ToString()
                    End While

                    '初始化缓冲区
                    MusicBuffer = New StoredMusic() With {
                        .Original = CurrentRecord.Content,
                        .Meta = Meta,
                        .Resource = FileName
                    }

                    '音频+基础信息(Attr+Color)+歌词
                    LoadingCountdown = New CountdownEvent(3)

                    '异步拉取数据
                    Task.Run(AddressOf LoadMusicMode)
                Else
                    '只需要加载视频即可
                    LoadingCountdown = New CountdownEvent(1)
                End If
            Else
                '换源

                If MDoc Is Nothing Then
                    'Meta无效 通过时间检测连播
                    If CheckContinueByTime() Then Throw New InvalidOperationException("禁止自动连续投屏")
                Else
                    Dim Title As String = GetMetaTitle(MDoc)
                    If String.IsNullOrEmpty(Title) AndAlso String.IsNullOrEmpty(LastTitle) Then
                        '无法通过Title判断 通过时间检测连播
                        If CheckContinueByTime() Then Throw New InvalidOperationException("禁止自动连续投屏")
                    ElseIf Title <> LastTitle Then
                        '自动连续投屏
                        Throw New InvalidOperationException("禁止自动连续投屏")
                    End If
                End If

                '只换源 不重载音乐信息
                LoadingCountdown = New CountdownEvent(1)
            End If

            '更新等待标志
            _Waiting = False

            '更新下载标志
            Downloaded = False

            With Player
                .Invoke(Sub()
                            '更新标志
                            .VLCPlayer.Visible = Not MusicMode
                            .Refresh()

                            '加载资源
                            HandleResource(URI, False)
                        End Sub)
            End With
        End Sub

        Private Sub InsidePlay()
            With Player
                .Invoke(Sub() .VLCPlayer.MediaPlayer.Play())
            End With
        End Sub

        ''' <summary>
        ''' 播放
        ''' </summary>
        Public Sub Play()
            If Not Accessible() OrElse Waiting Then Return

            If LoadingCountdown IsNot Nothing AndAlso Not LoadingCountdown.IsSet Then
                '加载中
                Task.Run(Sub()
                             LoadingCountdown.Wait()
                             If Not Accessible() OrElse Cancelled Then Return

                             InsidePlay()
                         End Sub)
            Else
                InsidePlay()
            End If
        End Sub

        ''' <summary>
        ''' 停止
        ''' </summary>
        Public Sub [Stop]()
            If Not Accessible() OrElse Cancelled OrElse Waiting Then Return

            Cancel()
            K.Push()
        End Sub

        ''' <summary>
        ''' 释放资源
        ''' </summary>
        Public Sub Dispose() Implements IDisposable.Dispose
            _Waiting = False
            Downloaded = False
            Cancel()

            With K
                RemoveHandler .OnPlayerPlay, AddressOf OnPlayerPlay
                RemoveHandler .OnPlayerPause, AddressOf OnPlayerPause
                RemoveHandler .OnPlayerTerminated, AddressOf OnPlayerTerminated
            End With
        End Sub

        Private Sub OnPlayerPause(Type As EasyKType)
            If Type <> EasyKType.DLNA Then Return

            RaiseEvent OnPause()
        End Sub

        Private Sub OnPlayerPlay(Type As EasyKType, Content As String)
            If Type <> EasyKType.DLNA Then Return

            '更新取消标志
            Cancelled = False

            '解析复位
            ResourceParsed.Reset()

            '记录当前点歌信息
            CurrentRecord = K.GetCurrent()

            If Content.StartsWith("{") Then
                '投屏缓存
                MusicBuffer = JsonUtils.SafeDeserializeObject(Of StoredMusic)(Content)
                _MusicMode = True

                Dim FileName As String = IO.Path.Combine(TempFolder, MusicBuffer.Resource)

                Dim Original As String = "127.0.0.1"

                '缓存无效则转常规投屏
                If MusicBuffer IsNot Nothing Then
                    If IO.File.Exists(FileName) Then
                        '播放缓存资源

                        '更新等待标志
                        _Waiting = False

                        '禁用Download标志 避免重复缓存
                        Downloaded = False

                        '创建加载标志
                        LoadingCountdown = New CountdownEvent(3)

                        '异步加载元数据
                        Task.Run(AddressOf LoadMusicMode)

                        '加载资源
                        Player.Invoke(Sub() HandleResource($"file:///{FileName.Replace("\", "/")}", True))

                        Return
                    End If

                    '获取原始点歌人IP
                    If Not String.IsNullOrEmpty(MusicBuffer.Original) Then Original = MusicBuffer.Original
                End If

                '还原投屏信息
                Update.Invoke(CurrentRecord.Id, Original)
                CurrentRecord = K.GetCurrent()
            End If

            '常规投屏

            '更新等待标志
            _Waiting = True

            With Player
                .Invoke(Sub() .Refresh())
            End With
        End Sub

        Private Sub OnPlayerTerminated()
            '更新偏移量
            With K
                If .Settings.Settings.DLNA.AutoResetOffset Then ._LyricOffset = 0
            End With

            '播放速度复位
            Rate = 1.0

            '检查是否需要缓存
            If MusicMode AndAlso MusicBuffer IsNot Nothing AndAlso K.Settings.Settings.DLNA.MusicBufferMode AndAlso
                Downloaded AndAlso MusicBuffer.Attribute IsNot Nothing Then

                '缓存
                Dim Content = JsonConvert.SerializeObject(MusicBuffer)
                Update.Invoke(CurrentRecord.Id, Content)
            End If

            '处理取消
            _Waiting = False
            _Duration = 0
            Downloaded = False
            CurrentRecord = Nothing
            MusicBuffer = Nothing
            Cancel()

            With Player
                .Invoke(Sub() .Refresh())
            End With

            RaiseEvent OnTerminated()
        End Sub

    End Class

End Namespace
