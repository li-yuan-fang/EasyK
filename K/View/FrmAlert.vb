Imports System.Drawing
Imports System.Threading
Imports System.Windows.Forms
Imports Svg

Public Class FrmAlert

    '标题
    Private Title As String

    'SVG图形
    Private Svg As String

    '结束时间(单位:Tick)
    Private EndTime As Long = 0

    '等待信号量
    Private ReadOnly Wait As New ManualResetEvent(False)

    Private ReadOnly BackgroundBrush As New SolidBrush(Color.FromArgb(50, 50, 50))

    ''' <summary>
    ''' 消息关闭事件
    ''' </summary>
    Public Event OnClose()

    ''' <summary>
    ''' 初始化消息提示窗口
    ''' </summary>
    ''' <param name="Player"></param>
    ''' <param name="Title"></param>
    ''' <param name="Svg"></param>
    ''' <param name="Time"></param>
    Public Sub New(Player As FrmPlayer, Title As String, Svg As String, Time As Double)

        ' 此调用是设计器所必需的。
        InitializeComponent()

        ' 在 InitializeComponent() 调用之后添加任何初始化。

        Me.Title = Title
        Me.Svg = Svg

        With Player
            Dim w, h, x, y As Integer
            h = 0.16 * .Height
            w = 0.9 * h

            x = (.Width - w) / 2 - 1
            y = 0.15 * .Height - 1

            Show(Player)
            SetBounds(x, y, w, h)
        End With

        EndTime = Now.AddSeconds(Time).Ticks
        Task.Run(AddressOf Alert)
    End Sub

    ''' <summary>
    ''' 刷新消息内容
    ''' </summary>
    ''' <param name="Title">标题</param>
    ''' <param name="Svg">图标</param>
    ''' <param name="Time">时长(单位:s)</param>
    Public Overloads Sub Refresh(Title As String, Svg As String, Time As Double)
        If Title = Me.Title AndAlso Svg = Me.Svg Then
            EndTime += Now.AddSeconds(Time).Ticks
            Return
        End If

        Me.Title = Title
        Me.Svg = Svg
        EndTime = Now.AddSeconds(Time).Ticks

        Invoke(Sub() Refresh())
    End Sub

    '执行等待
    Private Sub Alert()
        While EndTime > Now.Ticks
            Dim SleepTime As Integer
            Try
                SleepTime = (EndTime - Now.Ticks) / 10 ^ 4
            Catch
                If IsDisposed Then
                    Return
                Else
                    Exit While
                End If
            End Try

            If SleepTime <= 0 Then Exit While

            '利用超时机制实现可控等待
            If Wait.WaitOne(SleepTime) Then Return
        End While

        Close()
    End Sub

    ''' <summary>
    ''' 关闭消息提示窗口
    ''' </summary>
    Public Overloads Sub Close()
        Wait.Set()
        Invoke(Sub()
                   MyBase.Close()
                   Dispose()
               End Sub)

        RaiseEvent OnClose()
    End Sub

    '生成显示内容
    Private Function GeneratePanel() As Bitmap
        Dim Background As New Bitmap(Width, Height)

        Using g As Graphics = Graphics.FromImage(Background)
            With g
                .Clear(Color.Transparent)
                .SmoothingMode = Drawing2D.SmoothingMode.AntiAlias

                '绘制边框
                Dim Radius As Integer = Math.Max(Math.Min(Width, Height) * 0.05, 3)
                Using Path As New Drawing2D.GraphicsPath()
                    Dim Rect As New Rectangle(0, 0, Width, Height)
                    Dim Diameter As Integer = Radius * 2

                    With Path
                        If Radius > 0 AndAlso Radius <= Math.Min(Width / 2, Height / 2) Then
                            .StartFigure()
                            .AddArc(Rect.Left, Rect.Top, Diameter, Diameter, 180, 90)
                            .AddArc(Rect.Right - Diameter, Rect.Top, Diameter, Diameter, 270, 90)
                            .AddArc(Rect.Right - Diameter, Rect.Bottom - Diameter, Diameter, Diameter, 0, 90)
                            .AddArc(Rect.Left, Rect.Bottom - Diameter, Diameter, Diameter, 90, 90)
                            .CloseFigure()
                        Else
                            .AddRectangle(Rect)
                        End If
                    End With

                    .FillPath(BackgroundBrush, Path)
                End Using

                '绘制内容
                Dim ContentWidth As Single = Width * 0.7F

                Dim Font As New Font(Me.Font.FontFamily, Width * 0.1F)

                Dim TitleSizeF As SizeF = .MeasureString(Title, Font)
                If TitleSizeF.Width > ContentWidth Then
                    Dim NewSize = ContentWidth / TitleSizeF.Width * Font.Size

                    Font.Dispose()
                    Font = New Font(Me.Font.FontFamily, NewSize)
                    TitleSizeF = .MeasureString(Title, Font)
                End If

                Dim SvgWidth As Integer = ContentWidth - 0.15 * Width

                Dim GapHeight As Single = 0.08 * Height

                Dim X, Y As Single
                X = (Width - SvgWidth) / 2 - 1
                Y = (Height - (SvgWidth + GapHeight + TitleSizeF.Height)) / 2 - 1

                If Not String.IsNullOrEmpty(Svg) Then
                    Dim Doc As SvgDocument = SvgDocument.FromSvg(Of SvgDocument)(Svg)
                    Using SvgBitmap As Bitmap = Doc.Draw(SvgWidth, SvgWidth)
                        .DrawImage(SvgBitmap, X, Y, SvgWidth, SvgWidth)
                    End Using
                End If

                Y += SvgWidth + GapHeight
                X = (Width - TitleSizeF.Width) / 2 - 1

                .DrawString(Title, Font, Brushes.White, X, Y)

                Font.Dispose()
            End With
        End Using

        Return Background
    End Function

    '输出图形
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)

        With e.Graphics
            .SmoothingMode = Drawing2D.SmoothingMode.AntiAlias

            .DrawImageUnscaled(GeneratePanel(), 0, 0)
        End With
    End Sub

End Class