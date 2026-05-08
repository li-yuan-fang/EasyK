Imports System.Drawing
Imports System.Windows.Forms
Imports QRCoder

Public Class QRCodeBox

    Private Const Title As String = "扫码点歌"

    Private ReadOnly QRGenerator As QRCodeGenerator

    Private ReadOnly QRCodeData As QRCodeData

    Private ReadOnly QRCode As QRCode

    Private ReadOnly ModuleWidth As Integer

    ''' <summary>
    ''' 初始化二维码显示器
    ''' </summary>
    ''' <param name="Url"></param>
    Public Sub New(Url As String)
        ' 此调用是设计器所必需的。
        InitializeComponent()

        ' 在 InitializeComponent() 调用之后添加任何初始化。
        SetStyle(ControlStyles.SupportsTransparentBackColor, True)
        BackColor = Color.Transparent

        QRGenerator = New QRCodeGenerator()
        QRCodeData = QRGenerator.CreateQrCode(Url, QRCodeGenerator.ECCLevel.Q)
        QRCode = New QRCode(QRCodeData)

        Using QRBitmap As Bitmap = QRCode.GetGraphic(1, Color.Black, Color.White, False)
            ModuleWidth = QRBitmap.Width
        End Using
    End Sub

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

                    .FillPath(Brushes.White, Path)
                End Using

                '绘制内容
                Dim TitleSizeF As SizeF
                Using Font As New Font(Me.Font.FontFamily, Width * 0.06F)
                    TitleSizeF = .MeasureString(Title, Font)

                    Dim GapHeight As Single = Height * 0.01F

                    Dim QRWidth As Integer = Math.Min(Width, Height)
                    QRWidth = Math.Max(Math.Min(QRWidth * 0.9, Height - GapHeight - TitleSizeF.Height - 20) \ ModuleWidth, 1)

                    Using QRBitmap As Bitmap = QRCode.GetGraphic(QRWidth, Color.Black, Color.White, False)
                        QRBitmap.SetResolution(.DpiX, .DpiY)

                        Dim X, Y As Single
                        With QRBitmap
                            X = (Width - .Width) / 2 - 1
                        End With

                        Dim StrX, StrY As Single
                        With TitleSizeF
                            StrX = (Width - .Width) / 2 - 1
                            Y = (Height - (QRBitmap.Height + GapHeight + .Height)) / 2 - 1
                            StrY = Y + QRBitmap.Height + GapHeight
                        End With

                        .DrawImageUnscaled(QRBitmap, X, Y)
                        .DrawString(Title, Font, Brushes.Black, StrX, StrY)
                    End Using
                End Using
            End With
        End Using

        Return Background
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)

        With e.Graphics
            .SmoothingMode = Drawing2D.SmoothingMode.AntiAlias

            .DrawImageUnscaled(GeneratePanel(), 0, 0)
        End With
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        Refresh()
    End Sub

    Private Sub QRCodeBox_Disposed(sender As Object, e As EventArgs) Handles Me.Disposed
        QRCode.Dispose()
        QRCodeData.Dispose()
        QRGenerator.Dispose()
    End Sub

End Class
