Imports NAudio.CoreAudioApi
Imports NAudio.CoreAudioApi.Interfaces

Public Class DeviceChangeHandler
    Implements IMMNotificationClient, IDisposable

    '冷却时间(单位:ms)
    Private Const TriggerCool As Double = 500D

    '设备ID
    Private Device As String = String.Empty

    '上次更新时间
    Private LastUpdate As Date

    Private ReadOnly Emu As MMDeviceEnumerator

    ''' <summary>
    ''' 播放设备更新事件
    ''' </summary>
    ''' <param name="DeviceId">设备ID</param>
    Public Event OnDeviceUpdate(DeviceId As String)

    Public Sub OnDeviceStateChanged(deviceId As String, newState As DeviceState) Implements IMMNotificationClient.OnDeviceStateChanged
    End Sub

    Public Sub OnDeviceAdded(pwstrDeviceId As String) Implements IMMNotificationClient.OnDeviceAdded
    End Sub

    Public Sub OnDeviceRemoved(deviceId As String) Implements IMMNotificationClient.OnDeviceRemoved
    End Sub

    Public Sub OnDefaultDeviceChanged(flow As DataFlow, role As Role, defaultDeviceId As String) Implements IMMNotificationClient.OnDefaultDeviceChanged
        If flow = DataFlow.Capture Then Return

        SyncLock Device
            If String.IsNullOrEmpty(defaultDeviceId) Then
                Device = defaultDeviceId
            ElseIf defaultDeviceId <> Device OrElse
                (Now - LastUpdate).TotalMilliseconds >= TriggerCool Then
                Device = defaultDeviceId
                LastUpdate = Now

                Dim Dev = Emu.GetDevice(Device)
                Dim Guid = Dev.Properties(PropertyKeys.PKEY_AudioEndpoint_GUID)

                Task.Run(Sub() RaiseEvent OnDeviceUpdate(Guid.Value))
            End If
        End SyncLock
    End Sub

    Public Sub OnPropertyValueChanged(pwstrDeviceId As String, key As PropertyKey) Implements IMMNotificationClient.OnPropertyValueChanged
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
