Imports System.Reflection
Imports System.Text
Imports System.Windows.Forms

Namespace DLNA.MusicProvider

    Public Class DLNAMusicProviders

        'Upnp命名空间
        Friend Shared ReadOnly UpnpNamespace As XNamespace = XNamespace.Get("urn:schemas-upnp-org:metadata-1-0/upnp/")

        'Meta命名空间
        Friend Shared ReadOnly MetaNamespace As XNamespace = XNamespace.Get("urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/")

        'DC命名空间
        Friend Shared ReadOnly DCNamespace As XNamespace = XNamespace.Get("http://purl.org/dc/elements/1.1/")

        '音频流识别前缀
        Private Const ResAudioPrefix As String = "http-get:*:audio/"

        Private Const ClassAudioPrefix As String = "object.item.audioItem"

        Private Shared ReadOnly Providers As New List(Of DLNALyricProvider)

        ''' <summary>
        ''' 加载插件
        ''' </summary>
        Public Shared Sub LoadProviders(Settings As SettingContainer)
            '目录检查
            Dim Folder As String = IO.Path.Combine(Application.StartupPath, "plugins")
            If Not IO.Directory.Exists(Folder) Then
                Try
                    IO.Directory.CreateDirectory(Folder)
                Catch ex As Exception
                    Console.WriteLine("加载DLNA插件时出错 - {0}", ex.Message)
                End Try

                Return
            End If

            '扫描插件
            For Each File In IO.Directory.GetFiles(Folder, "*.dll")
                Try
                    Dim asm = Assembly.LoadFrom(File)

                    For Each T As Type In asm.GetTypes()
                        With T
                            If .IsClass AndAlso Not .IsAbstract AndAlso .IsSubclassOf(GetType(DLNALyricProvider)) Then
                                '注册插件
                                Providers.Add(Activator.CreateInstance(T))
                            End If
                        End With
                    Next
                Catch ex As Exception
                    Console.WriteLine("加载DLNA插件时出错 - {0}", ex.Message)
                    Console.WriteLine(File)
                End Try
            Next

            '加载配置信息
            With Settings.Settings.Plugins
                For Each Provider In Providers
                    Dim Id As String = Provider.Id

                    Try
                        Dim Setting As String = If(.ContainsKey(Id), .Item(Id), vbNullString)
                        Provider.LoadSettings(Setting)
                        Provider.TryUpdateSetting(Settings.Settings.PluginCommon)
                    Catch ex As Exception
                        Console.WriteLine("加载DLNA插件配置信息时出错 - {0}: {1}", Id, ex.Message)
                    End Try
                Next
            End With
        End Sub

        ''' <summary>
        ''' 尝试更新配置项
        ''' </summary>
        Public Shared Sub TryUpdateSettings()
            For Each Provider In Providers
                Provider.TryUpdateSetting(Settings.Settings.PluginCommon)
            Next
        End Sub

        ''' <summary>
        ''' 运行插件指令
        ''' </summary>
        ''' <param name="Id">插件ID</param>
        ''' <param name="Args">参数</param>
        ''' <returns></returns>
        Public Shared Function RunCommand(Id As String, Args As String()) As String
            For Each Provider In Providers
                With Provider
                    If .Id.ToLower() = Id.ToLower() Then Return .RunCommand(Args)
                End With
            Next

            Return $"找不到指定的插件 - {Id}"
        End Function

        ''' <summary>
        ''' 卸载插件
        ''' </summary>
        Public Shared Sub UnloadProviders(Settings As SettingContainer)
            For Each Provider In Providers
                With Provider
                    .Dispose()

                    Dim Id As String = .Id
                    Dim Saved As String = .Save()
                    With Settings.Settings.Plugins
                        If .ContainsKey(Id) Then
                            .Item(Id) = Saved
                        Else
                            .Add(Id, Saved)
                        End If
                    End With
                End With
            Next

            Providers.Clear()
        End Sub

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
                If pi IsNot Nothing AndAlso pi.Value.StartsWith(ResAudioPrefix) Then Return True
            Next

            Elements = From el In Doc.Descendants(UpnpNamespace + "class")
                       Where el.Parent.Name = MetaNamespace + "item"
                       Select el

            For Each c In Elements
                If Not String.IsNullOrEmpty(c.Value) AndAlso c.Value.StartsWith(ClassAudioPrefix) Then Return True
            Next

            Return False
        End Function

        Friend Shared Function ParseMusicAttributeDefault(Meta As String) As DLNAMusicAttribute
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
        ''' 解析音乐流元数据
        ''' </summary>
        ''' <param name="Meta">元数据</param>
        ''' <returns></returns>
        Public Shared Function ParseMusicAttribute(Meta As String) As DLNAMusicAttribute
            Dim Attribute As DLNAMusicAttribute = Nothing
            For Each Provider In Providers
                With Provider
                    If .IsMatch(Meta) Then
                        Attribute = .ParseMusicAttribute(Meta)
                        Exit For
                    End If
                End With
            Next

            If Attribute Is Nothing Then Attribute = ParseMusicAttributeDefault(Meta)

            Return Attribute
        End Function

        ''' <summary>
        ''' 生成更新音乐信息脚本
        ''' </summary>
        ''' <param name="Attribute">属性</param>
        ''' <param name="DefaultTitle">默认标题</param>
        ''' <param name="DefaultDuration">默认时长</param>
        ''' <returns></returns>
        Public Shared Function GenerateUpdateMusicScript(Attribute As DLNAMusicAttribute,
                                                         DefaultTitle As String,
                                                         DefaultDuration As Long) As String
            Dim Builder As New StringBuilder()
            With Builder
                Dim Title As String = If(String.IsNullOrEmpty(Attribute.Title), DefaultTitle, Attribute.Title)
                .Append($"window.setTitle(""{Title.Replace("""", "\""")}"");")
                .Append($"window.setArtist(""{Attribute.Artist.Replace("""", "\""")}"");")

                If Not String.IsNullOrEmpty(Attribute.Album) Then
                    .Append($"window.setAlbum(""{Attribute.Album.Replace("""", "\""")}"");")
                End If

                .Append($"window.setTotal({If(Attribute.Duration > 0, Attribute.Duration, DefaultDuration)});")
            End With
            Return Builder.ToString()
        End Function

        ''' <summary>
        ''' 生成更新状态脚本
        ''' </summary>
        ''' <param name="Playing">播放状态</param>
        ''' <param name="Rate">播放速度</param>
        ''' <param name="Position">播放进度</param>
        ''' <returns></returns>
        Public Shared Function GenerateUpdateStateScript(Playing As Boolean, Rate As Single, Position As Single) As String
            Dim Builder As New StringBuilder()
            With Builder
                .Append($"window.setPlaying({Playing.ToString().ToLower()});")
                .Append($"window.setCurrent({Position.ToString()});")
                .Append($"window.setRate({Rate.ToString()});")
            End With
            Return Builder.ToString()
        End Function

        ''' <summary>
        ''' 生成更新歌词脚本
        ''' </summary>
        ''' <param name="Meta">元数据</param>
        ''' <returns></returns>
        Public Shared Function GenerateUpdateLyricScript(Meta As String) As String
            For Each Provider In Providers
                With Provider
                    If Not .IsMatch(Meta) Then Continue For

                    Dim Lyrics = .QueryLyrics(Meta)
                    If String.IsNullOrEmpty(Lyrics) Then Continue For

                    Return $"window.setLyric({Lyrics});"
                End With
            Next

            Return vbNullString
        End Function

        ''' <summary>
        ''' 获取歌词颜色脚本
        ''' </summary>
        ''' <param name="Meta">元数据</param>
        ''' <param name="Attritube">属性</param>
        ''' <param name="Highlight">颜色高亮校正</param>
        ''' <returns></returns>
        Public Shared Function GenerateUpdateLyricColorScript(Meta As String, Attritube As DLNAMusicAttribute, Highlight As Boolean) As String
            For Each Provider In Providers
                With Provider
                    If Not .IsMatch(Meta) Then Continue For

                    Dim LyricColor = .GetLyricColor(Attritube, Highlight)
                    Console.WriteLine("颜色 {0}", LyricColor)
                    If String.IsNullOrEmpty(LyricColor) Then Continue For

                    Return $"window.setLyricColor({LyricColor});"
                End With
            Next

            Return vbNullString
        End Function

    End Class

End Namespace
