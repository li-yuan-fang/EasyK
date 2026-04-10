Imports System.ComponentModel
Imports System.Drawing
Imports System.Threading
Imports System.Windows.Forms
Imports CefSharp
Imports CefSharp.WinForms
Imports EasyK.DLNA
Imports EasyK.DLNA.MusicProvider
Imports EasyK.DLNA.Player
Imports LibVLCSharp.Shared

Public Class FrmPlayer

    Friend WithEvents Browser As ChromiumWebBrowser

    Friend WithEvents VLCPlayer As New LibVLCSharp.WinForms.VideoView()

    Private Const Title_None As String = "暂无播放源"

    Private Const Title_Waiting_DLNA As String = "等待投屏中"

    Private WithEvents K As EasyK

    Private ReadOnly BigFont As Font

    Private ReadOnly Settings As SettingContainer

    Private ReadOnly VlcAudioHandler As VlcAudioHandler

    Private ReadOnly VLCLib As LibVLC

    Private Prevent As Boolean = True

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
    Private DPlayer As DLNAPlayer = Nothing

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
            If _Base Is Nothing OrElse _Base.IsDisposed() Then Return

            _Base.K.Push()
        End Sub

        'DLNA音频流相关调用
        Public Sub queryState()
            If _Base Is Nothing OrElse _Base.IsDisposed() Then Return

            With _Base
                If .DPlayer IsNot Nothing Then .DPlayer.UpdateMusicState()
            End With
        End Sub

    End Class

    ''' <summary>
    ''' 获取播放状态
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Playing As Boolean
        Get
            Return DirectCast(Invoke(Function() VLCPlayer.MediaPlayer.IsPlaying), Boolean)
        End Get
    End Property

    Public Sub New(K As EasyK, Settings As SettingContainer)

        ' 此调用是设计器所必需的。
        InitializeComponent()

        ' 在 InitializeComponent() 调用之后添加任何初始化。

        '加载配置
        Me.Settings = Settings

        '初始化字体
        BigFont = New Font(Font.FontFamily, 50, FontStyle.Bold)

        '初始化浏览器
        Browser = New ChromiumWebBrowser()

        With Browser
            .Visible = False
            .Dock = DockStyle.Fill

            .AudioHandler = New CefAudioHandler(K, Settings, K.Dummy)

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

            If Settings.Settings.Audio.IsDummyAudio Then VlcAudioHandler = New VlcAudioHandler(K, .MediaPlayer, K.Dummy)

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
    ''' 部署
    ''' </summary>
    Public Sub Setup(Selected As Rectangle)
        Me.BackColor = Drawing.Color.Black
        Btn_Setup.Visible = False

        Me.FormBorderStyle = Windows.Forms.FormBorderStyle.None

        If Selected.Size.IsEmpty Then
            Dim Overlap = ScreenUtils.GetOverlapScreen(DesktopBounds)
            With Overlap
                If .Id >= 0 Then Selected = .Screen.Bounds
            End With
        End If

        If Selected.Width <= 0 OrElse Selected.Height <= 0 Then
            Me.WindowState = FormWindowState.Maximized
        Else
            Me.Bounds = Selected
        End If

        Dim FullResult As Boolean = FormUtils.SetPropW(Me.Handle, "MarkFullscreenWindow", 1)
        If Settings.Settings.DebugMode Then Console.WriteLine("全屏窗口配置: {0}", FullResult)

        DPlayer = New DLNAPlayer(K, Me, VLCLib, Browser_Loaded) With {
            .Update = New UpdateRecord(AddressOf K.UpdateRecord)
        }
        K.DLNAServer.Player = DPlayer

        _Setuped = True
    End Sub

    Protected Overrides Sub OnPaint(e As Windows.Forms.PaintEventArgs)
        MyBase.OnPaint(e)

        If _Setuped Then
            With e.Graphics
                .TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAlias

                Dim Title As String = If(DPlayer IsNot Nothing AndAlso DPlayer.Waiting, Title_Waiting_DLNA, Title_None)

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
        With K
            .Dummy.Stop()
            .DLNAServer.Player = Nothing
        End With

        If DPlayer IsNot Nothing Then DPlayer.Dispose()

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
        K.Setup()
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

    '运行B站自动脚本
    Private Sub RunBiliScript(Browser As IBrowser)
        With Browser
            '全屏&进度检查
            .EvaluateScriptAsync("var interval1 = setInterval(() => { let s = document.getElementsByClassName('bpx-player-ctrl-web'); if (s.length > 0) { s[0].click(); clearInterval(interval1); interval1 = setInterval(() => { if (document.getElementsByClassName('bpx-player-ctrl-time-current')[0].innerText === document.getElementsByClassName('bpx-player-ctrl-time-duration')[0].innerText) { clearInterval(interval1); easy_k.onComplete(); } }, 1000); } }, 100);")
            '关闭自动连播
            .EvaluateScriptAsync("var interval2 = setInterval(() => document.getElementsByName('bui-radio1')?.forEach((item) => { if (item.value === '2') { item.click(); clearInterval(interval2); } }), 50);")

            '关闭弹幕
            .EvaluateScriptAsync("var interval3 = setInterval(() => { let group = document.getElementsByClassName('bui-danmaku-switch-on'); if (group.length > 0) { if (window.getComputedStyle(group[0]).display !== 'none') { if (document.getElementsByClassName('bui-danmaku-switch-input').length > 0) { document.getElementsByClassName('bui-danmaku-switch-input')[0].click(); clearInterval(interval3); } } else { clearInterval(interval3); } } }, 50);")
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
        Invoke(Sub()
                   With VLCPlayer
                       .Visible = False
                       With .MediaPlayer
                           If .Media IsNot Nothing Then .Media.Dispose()
                       End With
                   End With
               End Sub)

        '推进进度
        K.Push()
    End Sub

    Private Sub OnPlayerPause(Type As EasyKType)
        Select Case Type
            Case EasyKType.Bilibili
                Invoke(Sub() Browser.EvaluateScriptAsync("document.getElementsByClassName('bpx-player-ctrl-play')[0].click();"))
            Case EasyKType.Video, EasyKType.DLNA
                Invoke(Sub() VLCPlayer.MediaPlayer.Pause())
                If DPlayer IsNot Nothing Then DPlayer.UpdateMusicState()
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
                                   .Media.Parse().Wait()

                                   .Play()
                               End With

                               .Visible = True
                           End With
                       End Sub)
            Case EasyKType.Bilibili
                Browser_Bili = True
                Browser_Loaded.Reset()

                Console.WriteLine("加载 bilibili - {0}", Content)

                Invoke(Sub()
                           With Browser
                               .Visible = True
                               .LoadUrl($"https://www.bilibili.com/video/{Content}")
                           End With
                       End Sub)
        End Select
    End Sub

    Private Sub OnPlayerTerminated()
        'Browser_Loaded.Reset()

        Invoke(Sub()
                   With Browser
                       If Browser_Bili Then
                           .EvaluateScriptAsync("document.getElementById('bilibili-player').innerHTML = ''; clearInterval(interval1); clearInterval(interval2); clearInterval(interval3); clearInterval(interval4); window.location.href = 'http://easyk/';")
                           Browser_Bili = False
                       End If

                       .Visible = False
                   End With
               End Sub)

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
               End Sub)
    End Sub

End Class