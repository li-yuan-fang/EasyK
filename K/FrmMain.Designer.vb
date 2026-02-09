<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class FrmMain
    Inherits System.Windows.Forms.Form

    'Form 重写 Dispose，以清理组件列表。
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Windows 窗体设计器所必需的
    Private components As System.ComponentModel.IContainer

    '注意: 以下过程是 Windows 窗体设计器所必需的
    '可以使用 Windows 窗体设计器修改它。  
    '不要使用代码编辑器修改它。
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(FrmMain))
        Me.Btn_Setup = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        'Btn_Setup
        '
        Me.Btn_Setup.Location = New System.Drawing.Point(137, 88)
        Me.Btn_Setup.Name = "Btn_Setup"
        Me.Btn_Setup.Size = New System.Drawing.Size(109, 50)
        Me.Btn_Setup.TabIndex = 0
        Me.Btn_Setup.Text = "部署"
        Me.Btn_Setup.UseVisualStyleBackColor = True
        '
        'FrmMain
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(12.0!, 27.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(390, 234)
        Me.Controls.Add(Me.Btn_Setup)
        Me.Font = New System.Drawing.Font("微软雅黑", 12.0!)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle
        Me.Icon = CType(resources.GetObject("$this.Icon"), System.Drawing.Icon)
        Me.ImeMode = System.Windows.Forms.ImeMode.Off
        Me.Margin = New System.Windows.Forms.Padding(4, 5, 4, 5)
        Me.MaximizeBox = False
        Me.Name = "FrmMain"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "播放器窗口"
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents Btn_Setup As Windows.Forms.Button
End Class
