Imports System.Drawing
Imports System.Windows.Forms

Public Class FrmQRCode

    Private Shared Transparent As Color = Color.FromArgb(255, 255, 254)

    Friend WithEvents QR As QRCodeBox

    Private ReadOnly ParentSize As Size

    Private _Round As Boolean = False

    Private Dragging As Boolean = False

    Private DragStart As Point

    ''' <summary>
    ''' 获取或设置圆角状态
    ''' </summary>
    ''' <returns></returns>
    Public Property Round As Boolean
        Get
            Return _Round
        End Get
        Set(value As Boolean)
            _Round = value
            If value Then
                BackColor = Transparent
            Else
                BackColor = Color.White
            End If
        End Set
    End Property

    Private ReadOnly Property ValidWidth As Integer
        Get
            Return If(FormBorderStyle <> FormBorderStyle.None, Width - 18, Width)
        End Get
    End Property

    Private ReadOnly Property ValidHeight As Integer
        Get
            Return If(FormBorderStyle <> FormBorderStyle.None, Height - 47, Height)
        End Get
    End Property

    Private Sub UpdateQR()
        If QR Is Nothing Then Return
        QR.SetBounds(0, 0, ValidWidth, ValidHeight)
    End Sub

    Public Sub New(Url As String, ParentSize As Size)

        ' 此调用是设计器所必需的。
        InitializeComponent()

        ' 在 InitializeComponent() 调用之后添加任何初始化。
        QR = New QRCodeBox(Url)
        Controls.Add(QR)
        TransparencyKey = Transparent

        Me.ParentSize = ParentSize
    End Sub

    Private Sub FrmQRCode_Resize(sender As Object, e As EventArgs) Handles Me.Resize
        UpdateQR()
    End Sub

    Private Sub FrmQRCode_Load(sender As Object, e As EventArgs) Handles Me.Load
        UpdateQR()
    End Sub

    Private Sub FrmQRCode_Closed(sender As Object, e As EventArgs) Handles Me.Closed
        Dispose()
    End Sub

    Private Sub FrmQRCode_MouseDown(sender As Object, e As MouseEventArgs) Handles Me.MouseDown
        If e.Button = MouseButtons.Left Then
            Dragging = True
            Cursor = Cursors.SizeAll
            DragStart.X = e.X
            DragStart.Y = e.Y
        End If
    End Sub

    Private Sub FrmQRCode_MouseUp(sender As Object, e As MouseEventArgs) Handles Me.MouseUp
        Dragging = False
        Cursor = Cursors.Default
    End Sub

    Private Sub FrmQRCode_MouseMove(sender As Object, e As MouseEventArgs) Handles Me.MouseMove
        If e.Button = MouseButtons.Left AndAlso Dragging Then
            Dim Original As Point = Location
            Dim X As Integer = e.X - DragStart.X
            Dim Y As Integer = e.Y - DragStart.Y
            If Math.Abs(X) > 1 OrElse Math.Abs(Y) > 1 Then
                With ParentSize
                    If .Width > 0 AndAlso .Height > 0 Then
                        Dim LocX As Integer = Math.Max(Math.Min(Original.X + X, .Width - Width), 0)
                        Dim LocY As Integer = Math.Max(Math.Min(Original.Y + Y, .Height - Height), 0)

                        Location = New Point(LocX, LocY)
                    Else
                        Location = New Point(Original.X + X, Original.Y + Y)
                    End If
                End With
            End If
        End If
    End Sub

    Private Sub QR_MouseDown(sender As Object, e As MouseEventArgs) Handles QR.MouseDown
        FrmQRCode_MouseDown(Me, e)
    End Sub

    Private Sub QR_MouseUp(sender As Object, e As MouseEventArgs) Handles QR.MouseUp
        FrmQRCode_MouseUp(Me, e)
    End Sub

    Private Sub QR_MouseMove(sender As Object, e As MouseEventArgs) Handles QR.MouseMove
        FrmQRCode_MouseMove(Me, e)
    End Sub

End Class