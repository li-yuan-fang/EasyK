Imports NAudio.CoreAudioApi
Imports NAudio.CoreAudioApi.Interfaces

Public Class DeviceChangeHandler
    Implements IMMNotificationClient, IDisposable

    '冷却时间(单位:ms)
    Private Const TriggerCool As Double = 500D

    '设备ID
    Private Device As String = vbNullString

    '上次更新时间
    Private LastUpdate As Date

    Private ReadOnly Emu As MMDeviceEnumerator

    Private ReadOnly DeviceLock As New Object()

    ''' <summary>
    ''' 播放设备更新事件
    ''' </summary>
    ''' <param name="Device">设备对象</param>
    Public Event OnDeviceUpdate(Device As MMDevice)

    Private Sub OnDeviceStateChanged(deviceId As String, newState As DeviceState) Implements IMMNotificationClient.OnDeviceStateChanged
    End Sub

    Private Sub OnDeviceAdded(pwstrDeviceId As String) Implements IMMNotificationClient.OnDeviceAdded
    End Sub

    Private Sub OnDeviceRemoved(deviceId As String) Implements IMMNotificationClient.OnDeviceRemoved
    End Sub

    Private Sub OnDefaultDeviceChanged(flow As DataFlow, role As Role, defaultDeviceId As String) Implements IMMNotificationClient.OnDefaultDeviceChanged
        If flow = DataFlow.Capture Then Return

        SyncLock DeviceLock
            If String.IsNullOrEmpty(defaultDeviceId) Then
                Device = vbNullString
            ElseIf defaultDeviceId <> Device OrElse
                (Now - LastUpdate).TotalMilliseconds >= TriggerCool Then
                Device = defaultDeviceId
                LastUpdate = Now

                Dim Dev = Emu.GetDevice(Device)
                If Dev Is Nothing Then Return

                Task.Run(Sub() RaiseEvent OnDeviceUpdate(Dev))
            End If
        End SyncLock
    End Sub

    Private Sub OnPropertyValueChanged(pwstrDeviceId As String, key As PropertyKey) Implements IMMNotificationClient.OnPropertyValueChanged
    End Sub

    ''' <summary>
    ''' 初始化
    ''' </summary>
    Public Sub New()
        LastUpdate = Now

        Emu = New MMDeviceEnumerator()
        Emu.RegisterEndpointNotificationCallback(Me)
    End Sub

    ''' <summary>
    ''' 销毁资源
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        With Emu
            .UnregisterEndpointNotificationCallback(Me)
            .Dispose()
        End With
    End Sub

End Class
