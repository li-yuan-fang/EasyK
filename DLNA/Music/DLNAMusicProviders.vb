Imports System.Text
Imports LibVLCSharp.Shared

Namespace DLNA.MusicProvider

    Public Class DLNAMusicProviders

        'Upnp命名空间
        Friend Shared ReadOnly UpnpNamespace As XNamespace = XNamespace.Get("urn:schemas-upnp-org:metadata-1-0/upnp/")

        'Meta命名空间
        Friend Shared ReadOnly MetaNamespace As XNamespace = XNamespace.Get("urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/")

        'DC命名空间
        Friend Shared ReadOnly DCNamespace As XNamespace = XNamespace.Get("http://purl.org/dc/elements/1.1/")

        Private Const AudioPrefix As String = "http-get:*:audio/"

        ''' <summary>
        ''' 检查是否是音乐流
        ''' </summary>
        ''' <param name="Meta">元数据</param>
        ''' <returns></returns>
        Public Shared Function IsMusicMeta(Meta As String) As Boolean
            Dim Doc As XDocument = XmlUtils.SafeParseXml(Meta)
            If Doc Is Nothing Then Return False

            Dim Elements = From el In Doc.Descendants(MetaNamespace + "res")
                           Where el.Parent.Name = MetaNamespace + "item"
                           Select el

            For Each Resource In Elements
                Dim pi = Resource.Attribute("protocolInfo")
                If pi IsNot Nothing AndAlso pi.Value.StartsWith(AudioPrefix) Then Return True
            Next

            Return False
        End Function

        ''' <summary>
        ''' 解析音乐流元数据
        ''' </summary>
        ''' <param name="Meta">元数据</param>
        ''' <returns></returns>
        Public Shared Function ParseMusicAttribute(Meta As String) As DLNAMusicAttribute
            Dim Doc As XDocument = XmlUtils.SafeParseXml(Meta)
            If Doc Is Nothing Then Return Nothing

            Dim Elements = From el In Doc.Descendants(MetaNamespace + "item") Select el

            For Each Item In Elements
                Dim Title = Item.Element(DCNamespace + "title")
                Dim Artist = Item.Element(UpnpNamespace + "artist")
                Dim Album = Item.Element(UpnpNamespace + "albumArtURI")
                Dim Resource = Item.Element(MetaNamespace + "res")

                Dim Duration As Long = 0
                If Resource IsNot Nothing Then
                    Dim d = Resource.Attribute("duration")
                    If d IsNot Nothing AndAlso Not String.IsNullOrEmpty(d.Value) Then
                        Duration = TimeUtils.ParseString(d.Value)
                    End If
                End If

                Return New DLNAMusicAttribute(If(Album IsNot Nothing, Album.Value, vbNullString),
                                                If(Title IsNot Nothing, Title.Value, vbNullString),
                                                If(Artist IsNot Nothing, Artist.Value, vbNullString),
                                                Duration)
            Next

            Return Nothing
        End Function

        ''' <summary>
        ''' 生成更新音乐信息脚本
        ''' </summary>
        ''' <param name="Attribute">音乐信息</param>
        ''' <returns></returns>
        Public Shared Function GenerateUpdateMusicScript(Attribute As DLNAMusicAttribute) As String
            Dim Builder As New StringBuilder()
            With Builder
                .Append($"window.setTitle(""{Attribute.Title}"");")
                .Append($"window.setArtist(""{Attribute.Artist}"");")

                If Not String.IsNullOrEmpty(Attribute.Album) Then
                    .Append($"window.setAlbum(""{Attribute.Album}"");")
                End If

                .Append($"window.setTotal({Attribute.Duration});")
            End With
            Return Builder.ToString()
        End Function

        ''' <summary>
        ''' 生成更新状态脚本
        ''' </summary>
        ''' <param name="Playing">播放状态</param>
        ''' <param name="Position">播放进度</param>
        ''' <returns></returns>
        Public Shared Function GenerateUpdateStateScript(Playing As Boolean, Position As Single) As String
            Dim Builder As New StringBuilder()
            With Builder
                .Append($"window.setPlaying({Playing.ToString().ToLower()});")
                .Append($"window.setCurrent({Position.ToString("0.000")});")
            End With
            Return Builder.ToString()
        End Function

    End Class

End Namespace
