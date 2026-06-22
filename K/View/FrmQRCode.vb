Imports System.Drawing
Imports System.Windows.Forms

Public Class FrmQRCode

    Private Const ZoomRate As Double = 1.1D

    Private Shared Transparent As Color = Color.FromArgb(255, 255, 254)

    Friend WithEvents QR As QRCodeBox

    Private _Round As Boolean = False

    Private Dragging As Boolean = False

    Private DragStart As Point

    Friend Shadows Parent As Form = Nothing

    ''' <summary>
    ''' 位置更新事件
    ''' </summary>
    ''' <param name="Pos">位置</param>
    Public Event OnPositionUpdate(Pos As Point)

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

    Public Sub New(Url As String)

        ' 此调用是设计器所必需的。
        InitializeComponent()

        ' 在 InitializeComponent() 调用之后添加任何初始化。
        QR = New QRCodeBox(Url)
        Controls.Add(QR)
        TransparencyKey = Transparent
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

        RaiseEvent OnPositionUpdate(New Point(Location.X - Parent.Bounds.X, Location.Y - Parent.Bounds.Y))
    End Sub

    Private Sub FrmQRCode_MouseMove(sender As Object, e As MouseEventArgs) Handles Me.MouseMove
        If e.Button = MouseButtons.Left AndAlso Dragging Then
            Dim Original As Point = Location
            Dim X As Integer = e.X - DragStart.X
            Dim Y As Integer = e.Y - DragStart.Y
            If Math.Abs(X) > 1 OrElse Math.Abs(Y) > 1 Then
                X += Original.X
                Y += Original.Y

                If Parent Is Nothing Then
                    Location = New Point(X, Y)
                Else
                    With Parent.Bounds
                        X -= .X
                        Y -= .Y

                        Dim LocX As Integer = Math.Max(Math.Min(X, .Width - Width), 0)
                        Dim LocY As Integer = Math.Max(Math.Min(Y, .Height - Height), 0)

                        Location = New Point(LocX, LocY)
                    End With
                End If
            End If
        End If
    End Sub

    Private Function ManualResize(ZoomLarge As Boolean) As Size
        Dim h As Integer
        If ZoomLarge Then
            h = Math.Round(Height * ZoomRate)
            Return New Size(If(Parent Is Nothing, Math.Round(Width * ZoomRate), 0.9 * h), h)
        Else
            h = Math.Round(Height / ZoomRate)
            Return New Size(If(Parent Is Nothing, Math.Round(Width / ZoomRate), 0.9 * h), h)
        End If
    End Function

    Private Sub FrmQRCode_MouseWheel(sender As Object, e As MouseEventArgs) Handles Me.MouseWheel
        If (Control.ModifierKeys And Keys.Control) = &H0 OrElse e.Delta = 0 Then Return

        Dim Zoomed As Size = ManualResize(e.Delta > 0)
        Dim X As Integer = Location.X
        Dim Y As Integer = Location.Y

        If Parent IsNot Nothing Then
            With Parent.Bounds
                X -= .X
                Y -= .Y

                X = Math.Max(Math.Min(X, .Width - Zoomed.Width), 0)
                Y = Math.Max(Math.Min(Y, .Height - Zoomed.Height), 0)
            End With
        End If

        With Zoomed
            SetBounds(X, Y, .Width, .Height)
        End With
        UpdateQR()
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