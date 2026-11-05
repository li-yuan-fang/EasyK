Imports System.ComponentModel
Imports System.Drawing
Imports System.Threading
Imports System.Windows.Forms
Imports CefSharp
Imports CefSharp.WinForms
Imports EasyK.DLNA.Player
Imports LibVLCSharp.Shared

Public Class FrmPlayer

    Friend WithEvents Browser As ChromiumWebBrowser

    Friend WithEvents VLCPlayer As New LibVLCSharp.WinForms.VideoView()

    Private Const Title_None As String = "暂无播放源"

    Private Const Title_Waiting_DLNA As String = "等待投屏中"

    Private WithEvents K As EasyK

    Private TitleFont As Font

    Private SubtitleFont As Font

    Private ReadOnly Settings As SettingContainer

    Private ReadOnly PlayerStarter As KPlayerManager

    Private ReadOnly VlcAudioHandler As VlcAudioHandler

    Private ReadOnly VLCLib As LibVLC

    Private Prevent As Boolean = True

    ''' <summary>
    ''' 播放器解析完成事件
    ''' </summary>
    Friend Event OnPlayerParsed()

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
    ''' 部署标志
    ''' </summary>
    Private _Setuped As Boolean = False

    ''' <summary>
    ''' 浏览器打开Bilibili标志
    ''' </summary>
    Private Browser_Bili As Boolean = False

    ''' <summary>
    ''' 浏览器加载信号量
    ''' </summary>
    Private ReadOnly Browser_Loaded As New ManualResetEventSlim(False)

    'DLNA播放器逻辑
    Private WithEvents DPlayer As DLNAPlayer = Nothing

    '消息提示窗口
    Private _Alert As AlertManager = Nothing

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

        Private ReadOnly _Base As FrmPlayer

        Public Sub New(Base As FrmPlayer)
            _Base = Base
        End Sub

        'B站相关调用
        Public Sub onComplete()
            If _Base Is Nothing OrElse _Base.IsDisposed() OrElse _Base.K.GetCurrent().Type <> EasyKType.Bilibili Then Return

            _Base.K.Push()
        End Sub

        Public Sub onLoaded(duration As String)
            If _Base Is Nothing Then Return

            With _Base
                If .IsDisposed() OrElse .K.GetCurrent().Type <> EasyKType.Bilibili Then Return

                ._Duration = TimeUtils.ParseString(duration) * 1000L
                .TriggerParsed()
            End With
        End Sub

        Public Sub onUpdate(current As String)
            If _Base Is Nothing Then Return

            With _Base
                If .IsDisposed() OrElse .K.GetCurrent().Type <> EasyKType.Bilibili Then Return
                ._BiliPosition = TimeUtils.ParseString(current)
            End With
        End Sub

        Public Sub onPaused(Paused As Boolean)
            If _Base Is Nothing Then Return

            With _Base
                If .IsDisposed() OrElse .K.GetCurrent().Type <> EasyKType.Bilibili Then Return

                If Paused Then
                    .Alert("暂停", AlertIcon.Pause)
                Else
                    .Alert("播放", AlertIcon.Play)
                End If
                .K.TriggerPaused(EasyKType.Bilibili, Paused)

                ._BiliPlaying = Not Paused
            End With
        End Sub

        'DLNA音频流相关调用
        Public Sub queryState()
            If _Base Is Nothing Then Return

            With _Base
                If Not .IsDisposed() AndAlso .DPlayer IsNot Nothing Then .DPlayer.UpdateMusicState()
            End With
        End Sub

    End Class

    ''' <summary>
    ''' 获取播放状态
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Playing As Boolean
        Get
            If Not Setuped Then Return False

            Dim Current = K.GetCurrent()
            If Current Is Nothing Then Return False

            If Current.Type = EasyKType.Bilibili Then Return _BiliPlaying

            Return VLCPlayer.MediaPlayer.IsPlaying
        End Get
    End Property

    Private _BiliPlaying As Boolean = False

    '单位:s
    Private _BiliPosition As Long = 0

    ''' <summary>
    ''' 获取或设置播放进度
    ''' </summary>
    ''' <returns></returns>
    Public Property Position As Single
        Get
            If Not Setuped Then Return 0

            Dim Current = K.GetCurrent()
            If Current Is Nothing Then Return 0

            If Current.Type = EasyKType.Bilibili Then
                If _Duration = 0 Then Return 0

                Return _BiliPosition * 1000L / _Duration
            End If

            If DPlayer IsNot Nothing Then Return DPlayer.Position

            Return 0
        End Get
        Set(value As Single)
            If Not Setuped Then Return

            Dim Current = K.GetCurrent()
            If Current Is Nothing Then Return

            If Current.Type = EasyKType.Bilibili Then
                If _Duration = 0 Then
                    Logger.Warn("无法更新进度 - B站视频未完成加载")
                    Return
                End If

                Dim Pos As Double = _Duration / 1000 * value
                BiliUpdate(Pos)
                _BiliPosition = Math.Round(Pos)

                Return
            End If

            If DPlayer IsNot Nothing Then
                With DPlayer
                    .Position = value

                    '到开头自动播放
                    If value = 0F Then .Play()
                End With
            End If
        End Set
    End Property

    ''' <summary>
    ''' 获取或设置播放速率
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks>B站渠道暂时不支持速度调节 可能后续会调整</remarks>
    Public Property Rate As Single
        Get
            Dim Current = K.GetCurrent()
            If Current Is Nothing OrElse Current.Type = EasyKType.Bilibili Then
                Return 1.0F
            End If

            If DPlayer Is Nothing Then Return 1.0F

            Return DPlayer.Rate
        End Get
        Set(value As Single)
            If Not Setuped Then Return
            Dim Current = K.GetCurrent()
            If Current Is Nothing OrElse Current.Type = EasyKType.Bilibili Then Return

            If DPlayer Is Nothing Then Return
            DPlayer.Rate = value
        End Set
    End Property

    '单位:ms
    Private _Duration As Long = 0

    ''' <summary>
    ''' 获取播放时长
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Duration As Double
        Get
            If DPlayer IsNot Nothing Then
                Dim Current = K.GetCurrent()
                If Current IsNot Nothing AndAlso Current.Type = EasyKType.DLNA Then
                    Return DPlayer.Duration
                End If
            End If

            Return Math.Max(_Duration / 1000D, 0)
        End Get
    End Property

    '触发解析事件
    Private Sub TriggerParsed()
        Task.Run(Sub() RaiseEvent OnPlayerParsed())
    End Sub

    Public Sub New(K As EasyK, Settings As SettingContainer, PlayerStarter As KPlayerManager)

        ' 此调用是设计器所必需的。
        InitializeComponent()

        ' 在 InitializeComponent() 调用之后添加任何初始化。

        '加载配置
        Me.Settings = Settings
        Me.PlayerStarter = PlayerStarter

        '初始化浏览器
        Browser = New ChromiumWebBrowser()

        With Browser
            .Visible = False
            .Dock = DockStyle.Fill

            .AudioHandler = New CefAudioHandler(K, Settings, PlayerStarter.Dummy)

            .MenuHandler = New BrowserMenuHandler()
            .JavascriptObjectRepository.Settings.LegacyBindingEnabled = True
            .JavascriptObjectRepository.Register("easy_k", New BrowserCallback(Me), False, BindingOptions.DefaultBinder)
        End With
        Controls.Add(Browser)

        '初始化VLC
        Dim Args As New List(Of String)
        With Args
            .Add("--no-spu")
            .Add("--vout=direct3d11")
            .Add("--avcodec-hw=d3d11va")
            If Not Settings.Settings.DebugMode Then .Add("--quiet")

            VLCLib = New LibVLC(.ToArray())
        End With

        With VLCPlayer
            .Visible = False
            .Dock = DockStyle.Fill
            .MediaPlayer = New MediaPlayer(VLCLib)

            If Settings.Settings.Audio.IsDummyAudio Then VlcAudioHandler = New VlcAudioHandler(K, .MediaPlayer, PlayerStarter.Dummy)

            With .MediaPlayer
                .Media = Nothing
                AddHandler .Stopped, AddressOf VLC_Stopped
            End With
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
    ''' 发送提示消息
    ''' </summary>
    ''' <param name="Title">消息标题</param>
    ''' <param name="Icon">消息图标</param>
    Public Sub Alert(Title As String, Icon As AlertIcon)
        If Not Setuped OrElse _Alert Is Nothing Then Return

        _Alert.Show(Title, Icon)
    End Sub

    ''' <summary>
    ''' 部署
    ''' </summary>
    Public Sub Setup(Selected As Rectangle)
        '配置基本信息
        Me.BackColor = Drawing.Color.Black
        Btn_Setup.Visible = False

        If Selected.Size.IsEmpty Then
            '查找屏幕
            Dim Overlap = ScreenUtils.GetOverlapScreen(DesktopBounds)
            With Overlap
                If .Id >= 0 Then Selected = .Screen.Bounds
            End With
        End If

        '部署窗口
        Me.FormBorderStyle = Windows.Forms.FormBorderStyle.None
        If Selected.Width <= 0 OrElse Selected.Height <= 0 Then
            Me.WindowState = FormWindowState.Maximized
        Else
            Me.Bounds = Selected
        End If

        '全屏
        Dim FullResult As Boolean = FormUtils.SetPropW(Me.Handle, "MarkFullscreenWindow", 1)
        Logger.Debug("全屏窗口配置: {0}", FullResult)

        '初始化字体
        Dim TitleSize As Single = 50.0F * Height / 1080.0F
        Dim SubtitleSize As Single = 30.0F * Height / 1080.0F
        TitleFont = New Font(Font.FontFamily, TitleSize, FontStyle.Bold)
        SubtitleFont = New Font(Font.FontFamily, SubtitleSize, FontStyle.Bold)

        '初始化DLNA播放器
        DPlayer = New DLNAPlayer(K, Me, VLCLib, Browser_Loaded) With {
            .Update = K.DLNAManager.UpdateRecordCallback
        }
        K.DLNAManager.SetDLNAPlayer(DPlayer)
        AddHandler DPlayer.OnParsed, AddressOf TriggerParsed

        '初始化消息提示窗口
        _Alert = New AlertManager(Me, Settings)

        '保持屏幕常亮
        BeginInvoke(Sub() FormUtils.KeepScreenOn())

        _Setuped = True
    End Sub

    Protected Overrides Sub OnPaint(e As Windows.Forms.PaintEventArgs)
        MyBase.OnPaint(e)

        If _Setuped Then
            With e.Graphics
                .TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAlias

                '生成标题
                Dim Title As String
                Dim SubTitle As String = vbNullString
                If DPlayer IsNot Nothing AndAlso DPlayer.Waiting Then
                    Title = Title_Waiting_DLNA

                    Dim Current = K.GetCurrent()
                    If Current IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(Current.Title) Then _
                        SubTitle = Current.Title
                Else
                    Title = Title_None
                End If

                If String.IsNullOrEmpty(SubTitle) Then
                    '只有主标题
                    Dim MySizeF = .MeasureString(Title, TitleFont)
                    Dim x As Single = (Me.Width - MySizeF.Width) / 2 - 1
                    Dim y As Single = (Me.Height - MySizeF.Height) / 2 - 1
                    .DrawString(Title, TitleFont, Brushes.White, x, y)
                Else
                    Dim CenterY As Single = Me.Height / 2 - 1

                    Dim TitleSizeF = .MeasureString(Title, TitleFont)
                    Dim TitleX As Single = (Me.Width - TitleSizeF.Width) / 2 - 1
                    Dim TitleY As Single = CenterY - TitleSizeF.Height

                    Dim SubtitleSizeF = .MeasureString(SubTitle, SubtitleFont)
                    Dim SubtitleX As Single = (Me.Width - SubtitleSizeF.Width) / 2 - 1

                    .DrawString(Title, TitleFont, Brushes.White, TitleX, TitleY)
                    .DrawString(SubTitle, SubtitleFont, Brushes.White, SubtitleX, CenterY)
                End If
            End With
        End If
    End Sub

    Private Sub FrmMain_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If Prevent Then
            e.Cancel = True
            Prevent = False
            Task.Run(Sub() K.Reset(True))
        End If
        FormUtils.CancelKeepScreenOn()
    End Sub

    Public Overloads Sub Close()
        PlayerStarter.Dummy.Stop()
        K.DLNAManager.SetDLNAPlayer(Nothing)

        If DPlayer IsNot Nothing Then
            With DPlayer
                RemoveHandler .OnParsed, AddressOf TriggerParsed
                .Dispose()
            End With
        End If
        If _Alert IsNot Nothing Then _Alert.Dispose()

        With VLCPlayer
            With .MediaPlayer
                RemoveHandler .Stopped, AddressOf VLC_Stopped
                .Dispose()
            End With
            .Dispose()
        End With

        VLCLib.Dispose()

        With K
            RemoveHandler .OnPlayerPause, AddressOf OnPlayerPause
            RemoveHandler .OnPlayerPlay, AddressOf OnPlayerPlay
            RemoveHandler .OnPlayerTerminated, AddressOf OnPlayerTerminated
        End With

        Prevent = False
        MyBase.Close()
    End Sub

    Private Sub Btn_Setup_Click(sender As Object, e As EventArgs) Handles Btn_Setup.Click
        Btn_Setup.Enabled = False
        PlayerStarter.Setup()
    End Sub

    ''' <summary>
    ''' 调用Javascript脚本
    ''' </summary>
    ''' <param name="Scripts">Javascript脚本</param>
    Friend Sub RunScript(ParamArray Scripts() As String)
        If Not Setuped OrElse IsDisposed Then Return

        Invoke(Sub()
                   For Each s In Scripts
                       Browser.EvaluateScriptAsync(s)
                   Next
               End Sub)
    End Sub

    'B站更新进度
    Private Sub BiliUpdate(Pos As Double)
        BeginInvoke(Sub() Browser.EvaluateScriptAsync($"(() => {{ let video = document.querySelector('#bilibili-player .bpx-player-video-wrap video'); if (!video) return; video.currentTime = {Pos:0.00}; }})();"))
    End Sub

    '运行B站自动脚本
    Private Sub RunBiliScript(Browser As IBrowser)
        _BiliPlaying = True

        With Browser
            '全屏
            .EvaluateScriptAsync("var interval1 = setInterval(() => { let s = document.getElementsByClassName('bpx-player-ctrl-web'); if (s.length > 0) { s[0].click(); clearInterval(interval1); } }, 100);")
            '关闭自动连播
            .EvaluateScriptAsync("var interval2 = setInterval(() => document.getElementsByName('bui-radio1')?.forEach((item) => { if (item.value === '2') { item.click(); clearInterval(interval2); } }), 50);")

            '关闭弹幕
            .EvaluateScriptAsync("var interval3 = setInterval(() => { let group = document.getElementsByClassName('bui-danmaku-switch-on'); if (group.length > 0) { if (window.getComputedStyle(group[0]).display !== 'none') { if (document.getElementsByClassName('bui-danmaku-switch-input').length > 0) { document.getElementsByClassName('bui-danmaku-switch-input')[0].click(); clearInterval(interval3); } } else { clearInterval(interval3); } } }, 50);")

            '尝试从头播放
            .EvaluateScriptAsync("var head_cnt = 0; var interval4 = setInterval(() => { let buttons = document.getElementsByClassName('bpx-player-toast-confirm'); for (let i = 0; i < buttons.length; i++) { if (buttons[i].textContent == '从头播放') { buttons[i].click(); clearInterval(interval4); break; } } head_cnt++; if (head_cnt >= 100) clearInterval(interval4); }, 50);")

            '进度管理
            .EvaluateScriptAsync("var element_current = undefined; var element_duration = undefined; var interval5 = setInterval(() => { if (!element_current) { let sel = document.getElementsByClassName('bpx-player-ctrl-time-current'); if (!sel || sel.length == 0) return; element_current = sel[0]; } if (!element_duration) { let sel = document.getElementsByClassName('bpx-player-ctrl-time-duration'); if (!sel || sel.length == 0) return; element_duration = sel[0]; easy_k.onLoaded(element_duration.innerText); } let current = element_current.innerText; let duration = element_duration.innerText; if (current === duration) { clearInterval(interval5); easy_k.onComplete(); return; } easy_k.onUpdate(element_current.innerText); }, 1000);")
        End With
    End Sub

    Private Sub Browser_FrameLoadEnd(sender As Object, e As FrameLoadEndEventArgs) Handles Browser.FrameLoadEnd
        If Not Browser_Loaded.IsSet() AndAlso e.Frame.IsMain Then
            If Browser_Bili Then
                'B站模式

                '此处检查主要处理某些需要二次转跳的视频
                '例如 我不曾忘记(BV1P24y1a7Lt) 需要转跳到 2023原神新春会
                '所以会出现多次加载完成的情况
                Dim Checkpoint = e.Browser.EvaluateScriptAsync("document.getElementById('bilibili-player') != undefined")
                Checkpoint.ContinueWith(Sub()
                                            With Checkpoint.Result
                                                If Browser_Loaded.IsSet() Then Return

                                                Try
                                                    If .Success AndAlso Boolean.Parse(.Result) Then
                                                        RunBiliScript(e.Browser)
                                                        Browser_Loaded.Set()
                                                    End If
                                                Catch
                                                End Try
                                            End With
                                        End Sub)
            Else
                '其他情况
                Browser_Loaded.Set()
            End If
        End If
    End Sub

    Private Sub VLC_Stopped(sender As Object, e As EventArgs)
        '播放器复位
        Invoke(Sub() VLCPlayer.Visible = False)

        With VLCPlayer.MediaPlayer
            If .Media IsNot Nothing Then .Media.Dispose()
        End With

        '推进进度
        K.Push()
    End Sub

    Private Sub OnPlayerPause(Type As EasyKType)
        Select Case Type
            Case EasyKType.Bilibili
                Invoke(Sub() Browser.EvaluateScriptAsync("document.getElementsByClassName('bpx-player-ctrl-play')[0].click(); setTimeout(() => { let video = document.querySelector('#bilibili-player .bpx-player-video-wrap video'); if (!video) return; easy_k.onPaused(video.paused); }, 20);"))
            Case EasyKType.Video, EasyKType.DLNA
                Dim Paused As Boolean
                With VLCPlayer.MediaPlayer
                    Paused = .IsPlaying
                    If Paused Then
                        Alert("暂停", AlertIcon.Pause)
                    Else
                        Alert("播放", AlertIcon.Play)
                    End If

                    .Pause()
                End With

                K.TriggerPaused(Type, Paused)

                Task.Run(Sub()
                             '延迟使状态稳定 避免暂停后播放状态无法及时恢复问题
                             Thread.Sleep(100)

                             If DPlayer IsNot Nothing Then DPlayer.UpdateMusicState()
                         End Sub)
        End Select
    End Sub

    Private Sub OnPlayerPlay(Type As EasyKType, Content As String)
        Select Case Type
            Case EasyKType.Video
                Dim VideoPath As String = If(Content.StartsWith("@"),
                                                Content.Substring(1),
                                                IO.Path.Combine(Application.StartupPath, Settings.Settings.TempFolder, Content))
                If Not IO.File.Exists(VideoPath) Then
                    Logger.Error("加载本地视频失败 - {0}", Content)
                    K.Push()

                    Return
                End If

                Logger.Info("加载本地视频 - {0}"， VideoPath)

                Invoke(Sub()
                           With VLCPlayer
                               With .MediaPlayer
                                   .Media = New Media(VLCLib, VideoPath, FromType.FromPath)
                                   .Media.Parse().ContinueWith(Sub()
                                                                   _Duration = .Media.Duration
                                                                   TriggerParsed()
                                                               End Sub)

                                   .Play()
                               End With

                               .Visible = True
                           End With
                       End Sub)
            Case EasyKType.Bilibili
                Browser_Bili = True
                Browser_Loaded.Reset()

                Logger.Info("加载 bilibili - {0}", Content)

                Invoke(Sub()
                           With Browser
                               .Visible = True
                               .LoadUrl($"https://www.bilibili.com/video/{Content}")
                           End With
                       End Sub)
        End Select
    End Sub

    Private Sub OnPlayerTerminated()
        _Duration = 0
        _BiliPosition = 0
        _BiliPlaying = False

        Invoke(Sub()
                   With Browser
                       If Browser_Bili Then
                           .EvaluateScriptAsync("document.getElementById('bilibili-player').innerHTML = ''; clearInterval(interval1); clearInterval(interval2); clearInterval(interval3); clearInterval(interval4); clearInterval(interval5); clearInterval(interval6); window.location.href = 'about:blank';")
                           Browser_Bili = False
                       End If

                       .Visible = False
                   End With

                   VLCPlayer.Visible = False
               End Sub)

        With VLCPlayer.MediaPlayer
            If .IsPlaying Then
                RemoveHandler .Stopped, AddressOf VLC_Stopped
                .Stop()
                AddHandler .Stopped, AddressOf VLC_Stopped
            End If
            If .Media IsNot Nothing Then .Media.Dispose()
            .SetRate(1.0F)
        End With
    End Sub

End Class