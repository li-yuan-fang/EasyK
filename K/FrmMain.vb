Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms
Imports CefSharp
Imports CefSharp.WinForms
Imports EasyK.DLNA.MusicProvider
Imports LibVLCSharp.Shared
Imports Newtonsoft.Json

Public Class FrmMain

    Friend WithEvents Browser As ChromiumWebBrowser

    Friend WithEvents VLCPlayer As New LibVLCSharp.WinForms.VideoView()

    Private Const Title_None As String = "暂无播放源"

    Private Const Title_Waiting_DLNA As String = "等待投屏中"

    Private WithEvents K As EasyK

    Private ReadOnly BigFont As Font

    Private ReadOnly Settings As SettingContainer

    Private Shared VLCLib As LibVLC

    Private Prevent As Boolean = True

    ''' <summary>
    ''' 获取播放状态
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Playing As Boolean
        Get
            Return Setuped AndAlso VLCPlayer.MediaPlayer.IsPlaying
        End Get
    End Property

    ''' <summary>
    ''' 获取或设置播放进度
    ''' </summary>
    ''' <returns></returns>
    Public Property Position As Single
        Get
            Return VLCPlayer.MediaPlayer.Position
        End Get
        Set(value As Single)
            VLCPlayer.MediaPlayer.Position = value
            UpdateDLNAMusicState()
        End Set
    End Property

    ''' <summary>
    ''' DLNA加载模式
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property DLNALoading As Boolean
        Get
            Return DLNA_Loading
        End Get
    End Property

    ''' <summary>
    ''' 获取或设置播放速度
    ''' </summary>
    ''' <returns></returns>
    Public Property Rate As Single
        Get
            Return VLCPlayer.MediaPlayer.Rate
        End Get
        Set(value As Single)
            VLCPlayer.MediaPlayer.SetRate(value)
            UpdateDLNAMusicState()
        End Set
    End Property

    Private _Duration As Double = 0

    ''' <summary>
    ''' 获取时长
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Duration As Double
        Get
            Return Math.Max(_Duration, 0)
        End Get
    End Property

    ''' <summary>
    ''' 获取部署状态
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Setuped As Boolean
        Get
            Return _Setuped
        End Get
    End Property

    ''' <summary>
    ''' 投屏重置事件
    ''' </summary>
    Public Event OnDLNAReset()

    '加载标志
    Private _Setuped As Boolean = False

    '浏览器播放标志
    Private Browser_Playing As Boolean = False

    Private Browser_Loaded As Boolean = False

    'DLNA初次等待标志
    Private DLNA_Waiting As Boolean = True

    'DLNA加载标志
    Private DLNA_Loading As Boolean = True

    'DLNA音频流模式标志
    Private DLNA_Music As Boolean = False

    'DLNA音频流信息
    Private DLNA_Music_Meta As String = vbNullString

    '空白右键菜单
    Private Class BrowserMenuHandler
        Implements IContextMenuHandler

        Public Sub OnBeforeContextMenu(chromiumWebBrowser As IWebBrowser, browser As IBrowser, frame As IFrame, parameters As IContextMenuParams, model As IMenuModel) Implements IContextMenuHandler.OnBeforeContextMenu
            model.Clear()
        End Sub

        Public Sub OnContextMenuDismissed(chromiumWebBrowser As IWebBrowser, browser As IBrowser, frame As IFrame) Implements IContextMenuHandler.OnContextMenuDismissed
        End Sub

        Public Function OnContextMenuCommand(chromiumWebBrowser As IWebBrowser, browser As IBrowser, frame As IFrame, parameters As IContextMenuParams, commandId As CefMenuCommand, eventFlags As CefEventFlags) As Boolean Implements IContextMenuHandler.OnContextMenuCommand
            Return False
        End Function

        Public Function RunContextMenu(chromiumWebBrowser As IWebBrowser, browser As IBrowser, frame As IFrame, parameters As IContextMenuParams, model As IMenuModel, callback As IRunContextMenuCallback) As Boolean Implements IContextMenuHandler.RunContextMenu
            Return False
        End Function

    End Class

    Private Class BrowserCallback

        Private ReadOnly _Base As FrmMain

        Public Sub New(Base As FrmMain)
            _Base = Base
        End Sub

        'B站相关调用

        Public Sub onComplete()
            If _Base Is Nothing OrElse _Base.IsDisposed() Then Return

            With _Base
                .OnPlayerTerminated()
                .K.Push()
            End With
        End Sub

        Public Sub tryClick()
            If _Base Is Nothing OrElse _Base.IsDisposed() Then Return

            Dim x As Integer = _Base.Left + _Base.Width / 2 - 1
            Dim y As Integer = _Base.Top + _Base.Height / 2 - 1

            With _Base
                .Invoke(Sub()
                            .BringToFront()
                            .TopMost = True
                            MouseUtils.MouseClick(x, y)
                            .TopMost = False
                        End Sub)
            End With
        End Sub

        'DLNA音频流相关调用

        Public Sub queryState()
            If _Base Is Nothing OrElse _Base.IsDisposed() Then Return

            _Base.UpdateDLNAMusicState()
        End Sub

    End Class

    Public Sub New(K As EasyK, Settings As SettingContainer)

        ' 此调用是设计器所必需的。
        InitializeComponent()

        ' 在 InitializeComponent() 调用之后添加任何初始化。

        '加载配置
        Me.Settings = Settings

        '初始化字体
        BigFont = New Font(Font.FontFamily, 50, FontStyle.Bold)

        '初始化浏览器
        Dim CachePath As String = IO.Path.Combine(Windows.Forms.Application.StartupPath, "cache")
        If Not IO.Directory.Exists(CachePath) Then IO.Directory.CreateDirectory(CachePath)

        Dim BrowserSettings As New RequestContextSettings()
        With BrowserSettings
            .CachePath = CachePath
            .PersistSessionCookies = True
            .CookieableSchemesList = "https"
        End With

        Browser = New ChromiumWebBrowser("", New RequestContext(BrowserSettings))

        With Browser
            .Visible = False
            .Dock = DockStyle.Fill

            .MenuHandler = New BrowserMenuHandler()
            .JavascriptObjectRepository.Settings.LegacyBindingEnabled = True
            .JavascriptObjectRepository.Register("easy_k", New BrowserCallback(Me), False, BindingOptions.DefaultBinder)
        End With
        Controls.Add(Browser)

        '初始化VLC
        Dim Args As New List(Of String)
        With Args
            .Add("--no-spu")
            If Not Settings.Settings.DebugMode Then .Add("--quiet")

            VLCLib = New LibVLC(.ToArray())
        End With

        With VLCPlayer
            .Visible = False
            .Dock = DockStyle.Fill
            .MediaPlayer = New MediaPlayer(VLCLib)
            .MediaPlayer.Media = Nothing

            AddHandler .MediaPlayer.Stopped, AddressOf VLC_Stopped
        End With
        Controls.Add(VLCPlayer)

        '绑定EasyK
        Me.K = K
        With K
            AddHandler .OnPlayerPause, AddressOf OnPlayerPause
            AddHandler .OnPlayerPlay, AddressOf OnPlayerPlay
            AddHandler .OnPlayerTerminated, AddressOf OnPlayerTerminated
        End With
    End Sub

    ''' <summary>
    ''' 部署
    ''' </summary>
    Public Sub Setup()
        Me.BackColor = Drawing.Color.Black
        Btn_Setup.Visible = False

        Me.FormBorderStyle = Windows.Forms.FormBorderStyle.None

        Dim selected As Rectangle = Nothing
        Dim max As Integer = 0
        For Each s In Screen.AllScreens()
            Dim r As Rectangle = Rectangle.Intersect(s.Bounds, Me.DesktopBounds)
            Dim size As Integer = r.Width * r.Height
            If size > max Then
                max = size
                selected = s.Bounds
            End If
        Next

        If selected.Width <= 0 OrElse selected.Height <= 0 Then
            Me.WindowState = FormWindowState.Maximized
        Else
            Me.Bounds = selected
        End If

        _Setuped = True
    End Sub

    Protected Overrides Sub OnPaint(e As Windows.Forms.PaintEventArgs)
        MyBase.OnPaint(e)

        If _Setuped Then
            With e.Graphics
                .TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAlias

                Dim Title As String = If(K.CanMirror() AndAlso DLNA_Waiting, Title_Waiting_DLNA, Title_None)

                Dim MySizeF = .MeasureString(Title, BigFont)
                Dim x As Single = (Me.Width - MySizeF.Width) / 2 - 1
                Dim y As Single = (Me.Height - MySizeF.Height) / 2 - 1
                .DrawString(Title, BigFont, Brushes.White, x, y)
            End With
        End If
    End Sub
    Private Sub FrmMain_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If Prevent Then
            e.Cancel = True
            Prevent = False
            Task.Run(Sub() K.Reset(True))
        End If
    End Sub

    Public Overloads Sub Close()
        With VLCPlayer
            With .MediaPlayer
                RemoveHandler .Stopped, AddressOf VLC_Stopped
                .Dispose()
            End With
            .Dispose()
        End With

        With K
            RemoveHandler .OnPlayerPause, AddressOf OnPlayerPause
            RemoveHandler .OnPlayerPlay, AddressOf OnPlayerPlay
            RemoveHandler .OnPlayerTerminated, AddressOf OnPlayerTerminated
        End With

        Prevent = False
        MyBase.Close()
    End Sub

    Private Sub Btn_Setup_Click(sender As Object, e As EventArgs) Handles Btn_Setup.Click
        K.Play()
    End Sub

    '更新内置音乐播放器信息
    Private Sub UpdateDLNAMusic()
        Task.Run(Sub()
                     '获取属性
                     Dim Attribute = DLNAMusicProviders.ParseMusicAttribute(DLNA_Music_Meta)
                     If Attribute Is Nothing Then Return

                     '调用歌词更新
                     UpdateLyrics()
                     UpdateLyricColor(Attribute)

                     '生成音乐信息脚本
                     Dim Current = K.GetCurrent()
                     Dim DefaultTitle As String = If(Current Is Nothing, vbNullString, Current.Title)

                     Dim MusicScript As String = DLNAMusicProviders.GenerateUpdateMusicScript(Attribute, DefaultTitle)

                     '等待
                     While Not Browser_Loaded
                         Threading.Thread.Sleep(10)
                     End While

                     '执行脚本
                     Try
                         Invoke(Sub() Browser.EvaluateScriptAsync(MusicScript))
                     Catch ex As Exception
                         If K.Settings.Settings.DebugMode Then
                             Console.WriteLine("DLNA音乐模式获取音乐信息出错 - {0}", ex.Message)
                         End If
                     End Try
                 End Sub)
    End Sub

    Private Sub UpdateLyrics()
        Task.Run(Sub()
                     Dim Lyric As String = DLNAMusicProviders.GenerateUpdateLyricScript(DLNA_Music_Meta)
                     If String.IsNullOrEmpty(Lyric) Then
                         If K.Settings.Settings.DebugMode Then Console.WriteLine("DLNA音乐模式无法获取歌词")
                         Return
                     End If

                     '等待
                     While Not Browser_Loaded
                         Threading.Thread.Sleep(10)
                     End While

                     '执行脚本
                     Try
                         Invoke(Sub() Browser.EvaluateScriptAsync(Lyric))
                     Catch ex As Exception
                         If K.Settings.Settings.DebugMode Then
                             Console.WriteLine("DLNA音乐模式获取歌词出错 - {0}", ex.Message)
                         End If
                     End Try
                 End Sub)
    End Sub

    Private Sub UpdateLyricColor(Attribute As DLNAMusicAttribute)
        If Not K.Settings.Settings.DLNA.LyricColorful Then Return

        Task.Run(Sub()
                     Dim LyricColor As String = DLNAMusicProviders.GenerateUpdateLyricColorScript(
                                DLNA_Music_Meta,
                                Attribute,
                                K.Settings.Settings.DLNA.LyricHighlight
                            )
                     If String.IsNullOrEmpty(LyricColor) Then
                         If K.Settings.Settings.DebugMode Then Console.WriteLine("DLNA音乐模式无法获取歌词颜色")
                         Return
                     End If

                     '等待
                     While Not Browser_Loaded
                         Threading.Thread.Sleep(10)
                     End While

                     '执行脚本
                     Try
                         Invoke(Sub() Browser.EvaluateScriptAsync(LyricColor))
                     Catch ex As Exception
                         If K.Settings.Settings.DebugMode Then
                             Console.WriteLine("DLNA音乐模式获取歌词颜色出错 - {0}", ex.Message)
                         End If
                     End Try

                 End Sub)
    End Sub

    '更新内置音乐播放器状态
    Private Sub UpdateDLNAMusicState()
        If Not DLNA_Music Then Return

        Task.Run(Sub()
                     While Not Browser_Loaded
                         Threading.Thread.Sleep(10)
                     End While

                     If IsDisposed Then Return

                     Try
                         Invoke(Sub() Browser.EvaluateScriptAsync(DLNAMusicProviders.GenerateUpdateStateScript(Playing, Rate, Position)))
                     Catch ex As Exception
                         If K.Settings.Settings.DebugMode Then
                             Console.WriteLine("DLNA音乐模式获取播放状态出错 - {0}", ex.Message)
                         End If
                     End Try
                 End Sub)
    End Sub

    Private Sub Browser_FrameLoadEnd(sender As Object, e As FrameLoadEndEventArgs) Handles Browser.FrameLoadEnd
        If Browser_Playing AndAlso Not Browser_Loaded AndAlso e.Frame.IsMain Then
            Browser_Loaded = True

            If DLNA_Music Then
                'DLNA音乐模式
                UpdateDLNAMusicState()
            Else
                'B站模式
                With e.Browser
                    '全屏&进度检查
                    .EvaluateScriptAsync("let interval1 = setInterval(() => { let s = document.getElementsByClassName('bpx-player-ctrl-web'); if (s.length > 0) { s[0].click(); clearInterval(interval1); interval1 = setInterval(() => { if (document.getElementsByClassName('bpx-player-ctrl-time-current')[0].firstChild.data === document.getElementsByClassName('bpx-player-ctrl-time-duration')[0].firstChild.data) { clearInterval(interval1); easy_k.onComplete(); } }, 1000); } }, 1000);")
                    '播完暂停
                    .EvaluateScriptAsync("let interval2 = setInterval(() => document.getElementsByName('bui-radio1')?.forEach((item) => { if (item.value === '2') { item.click(); clearInterval(interval2); } }), 500);")

                    '关闭弹幕
                    .EvaluateScriptAsync("let interval3 = setInterval(() => { let group = document.getElementsByClassName('bui-danmaku-switch-on'); if (group.length > 0) { if (window.getComputedStyle(group[0]).display !== 'none') { if (document.getElementsByClassName('bui-danmaku-switch-input').length > 0) { document.getElementsByClassName('bui-danmaku-switch-input')[0].click(); clearInterval(interval3); } } else { clearInterval(interval3); } } }, 500);")
                    '开启声音
                    .EvaluateScriptAsync("let interval4 = setInterval(() => { let group = document.getElementsByClassName('bpx-player-ctrl-volume-icon'); if (group.length > 0) { if (group[0].style['display'] == 'none') { group[0].click(); setTimeout(() => easy_k.tryClick(), 500)} clearInterval(interval4); }  }, 500);")
                End With
            End If
        End If
    End Sub

    '尝试先下载VLC无法直接解析的远程资源
    Private Sub TryDownloadFirst(Url As String)
        If Not NetUtils.IsURL(Url) Then Return

        Console.WriteLine("远程资源不可用 尝试先行下载...")
        With VLCPlayer.MediaPlayer
            RemoveHandler .Stopped, AddressOf VLC_Stopped
            BeginInvoke(Sub() .Stop())

            '生成可用文件名
            Dim TempFolder As String = IO.Path.Combine(Application.StartupPath, K.Settings.Settings.TempFolder)
            Dim FileName As String = vbNullString
            While String.IsNullOrEmpty(FileName) OrElse IO.File.Exists(IO.Path.Combine(TempFolder, FileName))
                FileName = Guid.NewGuid().ToString()
            End While

            FileName = IO.Path.Combine(TempFolder, FileName)

            Try
                Using wc As New Net.WebClient()
                    wc.DownloadFile(Url, FileName)
                End Using

                If Not IO.File.Exists(FileName) Then
                    Throw New DataException("获取远程资源失败")
                End If

                If .Media IsNot Nothing Then .Media.Dispose()

                .Media = New Media(VLCLib, FileName)
                .Media.Parse().Wait()

                _Duration = .Media.Duration / 1000
                If _Duration <= 0 Then
                    Throw New DataException("无法解析的资源")
                End If

                .Play()

                DLNA_Loading = False
                If DLNA_Music Then UpdateDLNAMusicState()
            Catch ex As Exception
                Console.WriteLine("先行下载失败 - {0}", ex.Message)

                Invoke(Sub()
                           .Stop()
                           VLCPlayer.Visible = False
                           If .Media IsNot Nothing Then .Media.Dispose()
                       End Sub)
                K.Push()
            End Try

            AddHandler .Stopped, AddressOf VLC_Stopped
        End With
    End Sub

    Private Sub VLC_Stopped(sender As Object, e As EventArgs)
        Invoke(Sub()
                   With VLCPlayer
                       .Visible = False
                       With .MediaPlayer
                           If .Media IsNot Nothing Then .Media.Dispose()
                       End With
                   End With
               End Sub)

        K.Push()
    End Sub

    Private Sub OnPlayerPause(Type As EasyKType)
        Select Case Type
            Case EasyKType.Bilibili
                Browser_Playing = Not Browser_Playing
                Invoke(Sub() Browser.EvaluateScriptAsync("document.getElementsByClassName('bpx-player-ctrl-play')[0].click();"))
            Case EasyKType.Video, EasyKType.DLNA
                Invoke(Sub() VLCPlayer.MediaPlayer.Pause())
                UpdateDLNAMusicState()
        End Select
    End Sub

    Private Sub OnPlayerPlay(Type As EasyKType, Content As String)
        Select Case Type
            Case EasyKType.Video
                Dim VideoPath As String = If(Content.StartsWith("@"),
                                                Content.Substring(1),
                                                IO.Path.Combine(Application.StartupPath, Settings.Settings.TempFolder, Content))
                If Not IO.File.Exists(VideoPath) Then
                    Console.WriteLine("加载本地视频失败 - {0}", Content)
                    K.Push()

                    Return
                End If

                Console.WriteLine("加载本地视频 - {0}"， VideoPath)

                Invoke(Sub()
                           With VLCPlayer
                               With .MediaPlayer
                                   .Media = New Media(VLCLib, VideoPath, FromType.FromPath)
                                   .Media.Parse().ContinueWith(Sub() _Duration = .Media.Duration / 1000)

                                   .Play()
                               End With

                               .Visible = True
                           End With
                       End Sub)
            Case EasyKType.Bilibili
                Browser_Playing = True
                Browser_Loaded = False

                Console.WriteLine("加载 bilibili - {0}", Content)

                Invoke(Sub()
                           With Browser
                               .Visible = True
                               .LoadUrl($"https://www.bilibili.com/video/{Content}")
                           End With
                       End Sub)
            Case EasyKType.DLNA
                If DLNA_Waiting AndAlso
                    (String.IsNullOrEmpty(Content) OrElse Not Content.StartsWith("@")) Then
                    _Duration = 0
                    Invoke(Sub()
                               VLCPlayer.MediaPlayer.SetRate(1.0)

                               Refresh()
                           End Sub)

                    Return
                End If

                If String.IsNullOrEmpty(Content) Then
                    '仅播放
                    Invoke(Sub()
                               With VLCPlayer
                                   .MediaPlayer().Play()

                                   If Not DLNA_Music Then .Visible = True
                               End With

                               Refresh()
                           End Sub)

                    UpdateDLNAMusicState()
                ElseIf Content = "Refresh" Then
                    '刷新DLNA音乐播放器信息
                    If Not DLNA_Music Then Return

                    UpdateDLNAMusic()
                ElseIf Content.StartsWith("@") Then
                    '设置资源
                    DLNA_Waiting = False

                    Invoke(Sub()
                               With VLCPlayer
                                   With .MediaPlayer
                                       .Media = New Media(VLCLib, Content.Substring(1), FromType.FromLocation)
                                       Dim ParseTask = .Media.Parse(MediaParseOptions.ParseLocal Or MediaParseOptions.ParseNetwork)
                                       ParseTask.ContinueWith(Sub()
                                                                  _Duration = .Media.Duration / 1000D

                                                                  If _Duration <= 0 Then
                                                                      '主要是网易云已缓存的flac资源
                                                                      '网易云会通过app局域网传输资源
                                                                      '但是Content-Type: audio/mpeg
                                                                      '而不是audio/flac
                                                                      '导致VLC无法正常识别

                                                                      '无法识别资源 尝试先下载
                                                                      TryDownloadFirst(Content.Substring(1))
                                                                  Else
                                                                      DLNA_Loading = False
                                                                  End If
                                                              End Sub)
                                   End With

                                   If Not DLNA_Music Then .Visible = True
                               End With

                               Refresh()
                           End Sub)
                ElseIf Content.StartsWith("<") Then
                    '音乐模式
                    DLNA_Music = True
                    DLNA_Music_Meta = Content
                    Browser_Playing = True
                    Browser_Loaded = False

                    '拉取音乐信息
                    UpdateDLNAMusic()

                    Invoke(Sub()
                               VLCPlayer.Visible = False

                               With Browser
                                   .LoadUrl($"file:///{IO.Path.Combine(Application.StartupPath, "wwwroot", "dlna", "music_box.html").Replace("\", "/")}")
                                   .Visible = True
                               End With
                           End Sub)
                End If
        End Select
    End Sub

    Private Sub OnPlayerTerminated()
        DLNA_Waiting = True
        DLNA_Loading = True
        DLNA_Music = False
        DLNA_Music_Meta = vbNullString

        If Browser_Playing Then
            Browser_Playing = False
            Invoke(Sub()
                       With Browser
                           .Visible = False
                           .LoadUrl("")
                       End With
                   End Sub)
        End If

        Invoke(Sub()
                   With VLCPlayer
                       .Visible = False

                       With .MediaPlayer
                           If .IsPlaying Then
                               RemoveHandler .Stopped, AddressOf VLC_Stopped
                               .Stop()
                               AddHandler .Stopped, AddressOf VLC_Stopped
                           End If
                           If .Media IsNot Nothing Then .Media.Dispose()
                       End With
                   End With

                   Refresh()
               End Sub)
    End Sub

End Class