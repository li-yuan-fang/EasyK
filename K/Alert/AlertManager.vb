Imports System.Reflection

Public Class AlertManager
    Implements IDisposable

    '消息提示窗口
    Private WithEvents Alert As FrmAlert = Nothing

    '播放器窗口
    Private ReadOnly Player As FrmPlayer

    '配置容器
    Private ReadOnly Settings As SettingContainer

    ''' <summary>
    ''' 初始化消息提示管理器
    ''' </summary>
    ''' <param name="Player">播放器窗口</param>
    ''' <param name="Settings">配置容器</param>
    Public Sub New(Player As FrmPlayer, Settings As SettingContainer)
        Me.Player = Player
        Me.Settings = Settings
    End Sub

    '消息提示窗口关闭回调
    Private Sub Alert_Closed()
        RemoveHandler Alert.OnClose, AddressOf Alert_Closed
        Alert = Nothing
    End Sub

    ''' <summary>
    ''' 显示消息
    ''' </summary>
    ''' <param name="Title">标题</param>
    ''' <param name="Icon">图标</param>
    Public Sub Show(Title As String, Icon As AlertIcon)
        Dim Duration As Double = Settings.Settings.AlertDuration
        If Duration <= 0 Then Return

        If Alert Is Nothing Then
            If Player Is Nothing Then Return

            Task.Run(Sub()
                         With Player
                             .Invoke(Sub()
                                         If .IsDisposed Then Return

                                         Alert = New FrmAlert(Player, Title, GetIcon(Icon), Duration)
                                         AddHandler Alert.OnClose, AddressOf Alert_Closed
                                     End Sub)
                         End With
                     End Sub)
        Else
            Alert.Refresh(Title, GetIcon(Icon), Duration)
        End If
    End Sub

    ''' <summary>
    ''' 销毁资源
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        If Alert IsNot Nothing Then Alert.Close()
    End Sub

    '获取SVG图标
    Private Shared Function GetIcon(Icon As AlertIcon) As String
        Dim asm = Assembly.GetExecutingAssembly()
        Dim resourceType = asm.GetType("EasyK.My.Resources.Resources")

        Dim prop = resourceType.GetProperty($"SVG_{Icon}", BindingFlags.Static Or BindingFlags.NonPublic)
        If prop Is Nothing Then Return vbNullString

        Return DirectCast(prop.GetValue(Nothing), String)
    End Function

End Class
