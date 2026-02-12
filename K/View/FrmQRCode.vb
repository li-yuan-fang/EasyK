Imports System.Drawing
Imports System.Windows.Forms

Public Class FrmQRCode

    Private Shared Transparent As Color = Color.FromArgb(255, 255, 254)

    Friend ReadOnly QR As QRCodeBox

    Private _Round As Boolean = False

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

End Class