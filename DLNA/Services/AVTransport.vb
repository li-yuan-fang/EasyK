Imports CefSharp.DevTools
Imports EasyK.DLNA.Player

Namespace DLNA.Protocol

    Public Class AVTransport
        Inherits DLNAService

        Private WithEvents DPlayer As DLNAPlayer

        ''' <summary>
        ''' 获取播放器对象
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Player As DLNAPlayer
            Get
                Return DPlayer
            End Get
        End Property

        ''' <summary>
        ''' 构建服务
        ''' </summary>
        ''' <param name="Protocol">协议管理器</param>
        Public Sub New(Protocol As DLNAProtocol)
            MyBase.New(Protocol, NameOf(AVTransport), My.Resources.AVTransport)

            '配置默认状态值
            SetState("CurrentTrack", "1")
            SetState("NumberOfTracks", "1")
            SetState("PlaybackStorageMedium", "NONE")
            SetState("RelativeCounterPosition", Integer.MaxValue.ToString())
            SetState("AbsoluteCounterPosition", Integer.MaxValue.ToString())

            ResetState()

            '配置推送
            SetStateEvent("LastChange", False)
            SetStateEvent("TransportState", True)
            SetStateEvent("TransportStatus", True)
            SetStateEvent("CurrentMediaDuration", True)
            SetStateEvent("CurrentTrackDuration", True)
            SetStateEvent("CurrentTrack", True)
            SetStateEvent("NumberOfTracks", True)
        End Sub

        ''' <summary>
        ''' 注册DLNA播放器
        ''' </summary>
        Public Sub RegisterPlayer(Player As DLNAPlayer)
            DPlayer = Player

            AddHandler Player.OnPause, AddressOf OnPause
            AddHandler Player.OnTerminated, AddressOf ResetState
            AddHandler Player.OnPlay, AddressOf OnPlay
        End Sub

        ''' <summary>
        ''' 解除DLNA播放器注册
        ''' </summary>
        Public Sub UnregisterPlayer()
            RemoveHandler Player.OnPause, AddressOf OnPause
            RemoveHandler Player.OnTerminated, AddressOf ResetState
            RemoveHandler Player.OnPlay, AddressOf OnPlay

            DPlayer = Nothing
        End Sub

        '复位状态
        Private Sub ResetState()
            SetState("CurrentPlayMode", "NORMAL")
            SetState("CurrentTrackURI", vbNullString)
            SetState("CurrentTrackMetaData", vbNullString)
            SetState("CurrentMediaDuration", "0:00:00")
            SetState("CurrentTrackDuration", "0:00:00")
            SetState("TransportPlaySpeed", "1")
            SetState("TransportState", "NO_MEDIA_PRESENT")
            SetState("TransportStatus", "OK")
        End Sub

        '暂停播放
        Private Sub OnPause()
            Task.Run(Sub()
                         Threading.Thread.Sleep(50)

                         If Player Is Nothing Then Return
                         SetState("TransportState", If(Player.Playing(), "PLAYING", "PAUSED_PLAYBACK"))
                     End Sub)
        End Sub

        '开始播放
        Private Sub OnPlay()
            '主要解决缓存文件加载时间过长导致轴对不上的问题
            SetState("TransportState", "PAUSED_PLAYBACK")
            Broadcast()
            Threading.Thread.Sleep(100)
            SetState("TransportState", "PLAYING")
            Broadcast()
        End Sub

        ''' <summary>
        ''' 生成更新消息
        ''' </summary>
        ''' <param name="Updated">已更新状态</param>
        ''' <returns></returns>
        Protected Overrides Function GenerateNotify(Updated As Dictionary(Of String, String)) As String
            Dim root = New XElement(ServiceNamespace + "propertyset")
            root.Add(New XAttribute(XNamespace.Xmlns + "e", ServiceNamespace))

            Dim prop = New XElement(ServiceNamespace + "property")
            Dim lastChange = New XElement("LastChange")
            Dim eventEl = New XElement(MetaNamespace + "Event")
            Dim instanceId = New XElement(MetaNamespace + "InstanceID")
            instanceId.SetAttributeValue("val", "0")

            For Each kvp In Updated
                Dim p = New XElement(MetaNamespace + kvp.Key)
                p.SetAttributeValue("val", If(kvp.Value, ""))
                instanceId.Add(p)
            Next

            eventEl.Add(instanceId)
            lastChange.Value = eventEl.ToString(SaveOptions.DisableFormatting)
            prop.Add(lastChange)
            root.Add(prop)

            Return $"<?xml version=""1.0"" encoding=""UTF-8""?>{vbCrLf}{root.ToString(SaveOptions.DisableFormatting)}"
        End Function

        Protected Function SetAVTransportURI(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            With Protocol.DLNA
                If .Player Is Nothing Then
                    Handled = True
                    Return Nothing
                End If

                .Player.CommitResource(Args("CurrentURI"), Args("CurrentURIMetaData"))
            End With

            SetState("TransportState", "STOPPED")
            SetState("CurrentTrackURI", Args("CurrentURI"))
            SetState("CurrentTrackMetaData", Args("CurrentURIMetaData"))

            Return Nothing
        End Function

        Protected Function Play(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            With Protocol.DLNA
                If .Player Is Nothing Then Return Nothing

                With .Player
                    .Play()
                    .Rate = Val(Args("Speed"))
                End With
            End With

            SetState("TransportState", "PLAYING")

            Return Nothing
        End Function

        Protected Function Seek(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            With Protocol.DLNA
                If .Player Is Nothing Then Return Nothing

                If Not Args.ContainsKey("Unit") OrElse Args("Unit") <> "REL_TIME" Then _
                    Throw New ArgumentException("不支持的快进方式")

                If Not Args.ContainsKey("Target") Then Throw New ArgumentException("无效的转跳目标")
                Dim Target As Long = TimeUtils.ParseString(Args("Target"))

                With .Player
                    .Position = Math.Max(Math.Min(Target / .Duration, 1), 0)
                End With
            End With

            SetState("TransportState", GetState("TransportState"))

            Return Nothing
        End Function

        Protected Function Pause(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            With Protocol
                With .DLNA
                    If .Player IsNot Nothing AndAlso .Player.Playing Then
                        RemoveHandler Player.OnPause, AddressOf OnPause
                        .K.Pause()
                        AddHandler Player.OnPause, AddressOf OnPause
                    End If
                End With
            End With

            SetState("TransportState", "PAUSED_PLAYBACK")

            Return Nothing
        End Function

        Protected Function [Stop](ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            With Protocol.DLNA
                If .Player IsNot Nothing Then .Player.Stop()
            End With

            SetState("TransportState", "STOPPED")

            Return Nothing
        End Function

        Protected Function GetPositionInfo(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            Dim Duration As String
            Dim Progress As String
            With Protocol.DLNA
                If .Player Is Nothing Then Return Nothing

                With .Player
                    Duration = TimeUtils.SecondToString(Math.Round(.Duration))

                    Dim p As Double = Math.Round(.Duration * .Position)
                    Progress = TimeUtils.SecondToString(p)
                End With
            End With

            Dim Returns As New Dictionary(Of String, String)
            With Returns
                .Add("TrackDuration", Duration)
                .Add("RelTime", Progress)
                .Add("AbsTime", Progress)
            End With

            SetState("CurrentTrackDuration", Duration)

            Return Returns
        End Function

        Protected Function GetMediaInfo(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            With Protocol.DLNA
                If .Player Is Nothing Then Return Nothing

                SetState("CurrentMediaDuration", TimeUtils.SecondToString(Math.Round(.Player.Duration)))
            End With

            Return Nothing
        End Function

        Protected Function GetTransportInfo(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)

            With Protocol.DLNA
                If .Player Is Nothing OrElse Not .Player.Loading Then Return Nothing

                Return New Dictionary(Of String, String) From {
                                        {"CurrentTransportState", "TRANSITIONING"}
                                    }
            End With
        End Function

    End Class

End Namespace
