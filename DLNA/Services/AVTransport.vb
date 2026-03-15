Namespace DLNA.Protocol

    Public Class AVTransport
        Inherits DLNAService

        '暂停播放
        Private Sub OnPause(Type As EasyKType)
            If Type <> EasyKType.DLNA Then Return

            Task.Run(Sub()
                         Threading.Thread.Sleep(20)

                         SetState("TransportState", If(Protocol.DLNA.K.IsPlaying(), "PLAYING", "PAUSED_PLAYBACK"))
                     End Sub)
        End Sub

        '停止播放
        Private Sub OnTerminated()
            SetState("TransportState", "NO_MEDIA_PRESENT")
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
        ''' 构建服务
        ''' </summary>
        ''' <param name="Protocol">协议管理器</param>
        Public Sub New(Protocol As DLNAProtocol)
            MyBase.New(Protocol, NameOf(AVTransport), My.Resources.AVTransport)

            '配置默认状态值
            SetState("TransportPlaySpeed", "1")
            SetState("TransportStatus", "OK")

            SetState("CurrentTrack", "1")
            SetState("NumberOfTracks", "1")
            SetState("PlaybackStorageMedium", "NONE")
            SetState("RelativeCounterPosition", Integer.MaxValue.ToString())
            SetState("AbsoluteCounterPosition", Integer.MaxValue.ToString())

            '配置推送
            SetStateEvent("LastChange", False)
            SetStateEvent("TransportState", True)
            SetStateEvent("TransportStatus", True)
            SetStateEvent("CurrentMediaDuration", True)
            SetStateEvent("CurrentTrackDuration", True)
            SetStateEvent("CurrentTrack", True)
            SetStateEvent("NumberOfTracks", True)

            '绑定事件
            AddHandler Protocol.DLNA.K.OnPlayerPause, AddressOf OnPause
            AddHandler Protocol.DLNA.K.OnPlayerTerminated, AddressOf OnTerminated
            AddHandler Protocol.DLNA.K.OnMirrorPlay, AddressOf OnPlay
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
            With Protocol
                With .DLNA.K
                    '投屏连播(比如B站多集连播)
                    Dim Remain As Single = (1 - .PlayingPosition) * .PlayingDuration
                    If Remain > 0 AndAlso Remain < Settings.Settings.DLNA.PreventContinueRange Then _
                        Throw New InvalidOperationException("禁止连续投屏")

                    If .CanMirror() Then
                        '设置资源路径
                        .TriggerMirrorPlay($"@{Args("CurrentURI")}")

                        '检查是否为音乐模式
                        If MusicProvider.DLNAMusicProviders.IsMusicMeta(Args("CurrentURIMetaData")) Then
                            '设置音乐模式资源
                            .TriggerMirrorPlay(Args("CurrentURIMetaData"))
                        End If
                    End If
                End With
            End With

            SetState("TransportState", "STOPPED")
            SetState("CurrentTrackURI", Args("CurrentURI"))
            SetState("CurrentTrackMetaData", Args("CurrentURIMetaData"))

            Return Nothing
        End Function

        Protected Function Play(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            With Protocol
                With .DLNA.K
                    .TriggerMirrorPlay(Nothing)
                    .PlayingRate = Val(Args("Speed"))
                End With
            End With

            SetState("TransportState", "PLAYING")

            Return Nothing
        End Function

        Protected Function Seek(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            With Protocol.DLNA.K
                If Not Args.ContainsKey("Unit") OrElse Args("Unit") <> "REL_TIME" Then _
                    Throw New ArgumentException("不支持的快进方式")

                If Not Args.ContainsKey("Target") Then Throw New ArgumentException("无效的转跳目标")
                Dim Target As Long = TimeUtils.ParseString(Args("Target"))

                Dim Position As Single = Target / .PlayingDuration
                Position = Math.Max(Math.Min(Position, 1), 0)
                .PlayingPosition = Position
            End With

            SetState("TransportState", GetState("TransportState"))

            Return Nothing
        End Function

        Protected Function Pause(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            With Protocol
                With .DLNA.K
                    If .IsPlaying() Then
                        RemoveHandler .OnPlayerPause, AddressOf OnPause
                        .Pause()
                        AddHandler .OnPlayerPause, AddressOf OnPause
                    End If
                End With
            End With

            SetState("TransportState", "PAUSED_PLAYBACK")

            Return Nothing
        End Function

        Protected Function [Stop](ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            With Protocol
                With .DLNA.K
                    If .IsPlaying Then .Push()
                End With
            End With

            SetState("TransportState", "STOPPED")

            Return Nothing
        End Function

        Protected Function GetPositionInfo(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            Dim Duration As String
            Dim Progress As String
            With Protocol.DLNA.K
                Duration = TimeUtils.SecondToString(Math.Round(.PlayingDuration))

                Dim p As Double = Math.Round(.PlayingDuration * .PlayingPosition)
                Progress = TimeUtils.SecondToString(p)
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
            SetState("CurrentMediaDuration", TimeUtils.SecondToString(Math.Round(Protocol.DLNA.K.PlayingDuration)))

            Return Nothing
        End Function

        Protected Function GetTransportInfo(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)
            With Protocol.DLNA.K
                Return If(.DLNALoading,
                    New Dictionary(Of String, String) From {
                        {"CurrentTransportState", "TRANSITIONING"}
                    },
                    Nothing)
            End With
        End Function

    End Class

End Namespace
