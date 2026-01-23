Imports System.Collections.Specialized
Imports System.Drawing
Imports System.Net
Imports System.Text
Imports Newtonsoft.Json

Namespace DLNA.MusicProvider

    Public MustInherit Class DLNALyricProvider
        Implements IDisposable

        Protected Const UserAgent As String = "Mozilla/5.0 (Linux; Android 6.0; Nexus 5 Build/MRA58N) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/144.0.0.0 Mobile Safari/537.36"

        ''' <summary>
        ''' 请求缓存
        ''' </summary>
        Protected ReadOnly RequestCache As New Dictionary(Of String, String)

        ''' <summary>
        ''' 配置信息
        ''' </summary>
        Protected Settings As String

        ''' <summary>
        ''' Cookie
        ''' </summary>
        Protected Cookie As String = $"NMTID={Guid.NewGuid().ToString()}" '此处为默认Cookie

        ''' <summary>
        ''' Refer
        ''' </summary>
        ''' <returns></returns>
        Protected MustOverride ReadOnly Property HttpRefer As String

        ''' <summary>
        ''' 歌词服务标识
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property Id As String

        ''' <summary>
        ''' 尝试更新配置项
        ''' </summary>
        ''' <param name="Settings">配置项</param>
        Public MustOverride Sub TryUpdateSetting(Settings As Dictionary(Of String, Object))

        ''' <summary>
        ''' 检查是否与服务匹配
        ''' </summary>
        ''' <param name="Meta">元数据</param>
        ''' <returns></returns>
        Public MustOverride Function IsMatch(Meta As String) As Boolean

        ''' <summary>
        ''' 获取歌词数据
        ''' </summary>
        ''' <param name="Meta">元数据</param>
        ''' <returns>Json格式数据</returns>
        Protected MustOverride Function GetLyric(Meta As String) As String

        ''' <summary>
        ''' 加载配置信息
        ''' </summary>
        ''' <param name="Settings">配置信息</param>
        Public Overridable Sub LoadSettings(Settings As String)
            Me.Settings = Settings
        End Sub

        ''' <summary>
        ''' 运行指令
        ''' </summary>
        ''' <param name="Args">参数</param>
        ''' <returns></returns>
        Public Overridable Function RunCommand(Args As String()) As String
            Return "不支持的指令"
        End Function

        ''' <summary>
        ''' 发送POST请求
        ''' </summary>
        ''' <param name="url"></param>
        ''' <param name="paramDict">参数</param>
        ''' <exception cref="WebException"></exception>
        ''' <returns></returns>
        Protected Function SendPost(url As String, paramDict As Dictionary(Of String, String)) As String
            Dim result As String
            Using wc = New WebClient()
                wc.Headers.Add(HttpRequestHeader.ContentType, "application/x-www-form-urlencoded")
                wc.Headers.Add(HttpRequestHeader.Referer, HttpRefer)
                wc.Headers.Add(HttpRequestHeader.UserAgent, UserAgent)
                wc.Headers.Add(HttpRequestHeader.Cookie, Cookie)

                Dim request = New NameValueCollection()
                For Each keyPair In paramDict
                    request.Add(keyPair.Key, keyPair.Value)
                Next

                Dim bytes = wc.UploadValues(url, "POST", request)
                result = Encoding.UTF8.GetString(bytes)
            End Using

            Return result
        End Function


        ''' <summary>
        ''' 发送POST请求
        ''' </summary>
        ''' <param name="url"></param>
        ''' <param name="paramDict">参数</param>
        ''' <exception cref="WebException"></exception>
        ''' <returns></returns>
        Protected Function SendJsonPost(url As String, paramDict As Dictionary(Of String, Object)) As String
            Using wc = New WebClient()
                wc.Encoding = Encoding.UTF8
                wc.Headers.Add(HttpRequestHeader.ContentType, "application/json")
                wc.Headers.Add(HttpRequestHeader.Referer, HttpRefer)
                wc.Headers.Add(HttpRequestHeader.UserAgent, UserAgent)
                wc.Headers.Add(HttpRequestHeader.Cookie, Cookie)

                Return wc.UploadString(url, JsonConvert.SerializeObject(paramDict))
            End Using
        End Function

        ''' <summary>
        ''' 请求歌词数据
        ''' </summary>
        ''' <param name="Meta">元数据</param>
        ''' <returns>Json格式数据</returns>
        Public Overridable Function QueryLyrics(Meta As String) As String
            If String.IsNullOrEmpty(Meta) Then Return vbNullString

            If RequestCache.ContainsKey(Meta) Then
                Return RequestCache(Meta)
            Else
                Dim Result = GetLyric(Meta)
                If String.IsNullOrEmpty(Result) Then Return vbNullString

                RequestCache.Add(Meta, Result)

                Return Result
            End If
        End Function

        ''' <summary>
        ''' 获取滚动歌词颜色
        ''' </summary>
        ''' <param name="Attribute">属性</param>
        ''' <param name="Highlight">颜色高亮校正</param>
        ''' <returns></returns>
        Public Overridable Function GetLyricColor(Attribute As DLNAMusicAttribute, Highlight As Boolean) As String
            If String.IsNullOrEmpty(Attribute.Album) Then
                Console.WriteLine("无法获取专辑图片")
                Return vbNullString
            End If

            Dim Buffer As Byte()
            Using wc = New WebClient()
                With wc
                    With .Headers
                        .Add(HttpRequestHeader.Referer, HttpRefer)
                        .Add(HttpRequestHeader.UserAgent, UserAgent)
                        .Add(HttpRequestHeader.Cookie, Cookie)
                    End With

                    Try
                        Buffer = .DownloadData(Attribute.Album)
                    Catch ex As Exception
                        Console.WriteLine("获取专辑图片失败 - {0}", ex.Message)
                        Return vbNullString
                    End Try
                End With
            End Using

            Try
                Using ms As New IO.MemoryStream(Buffer)
                    Using img As Image = Image.FromStream(ms, True, True)
                        Dim c As Color = ColorUtils.GetHighSaturationDominantColor(img)
                        If c = Color.Transparent Then Throw New ArgumentException("无法解析图像颜色")

                        If Highlight Then c = ColorUtils.HighlightColor(c)
                        With c
                            Return $"{ .R}, { .G}, { .B}"
                        End With
                    End Using
                End Using
            Catch ex As Exception
                Console.WriteLine("解析专辑图片失败 - {0}", ex.Message)
            End Try

            Return vbNullString
        End Function

        ''' <summary>
        ''' 解析音乐流元数据
        ''' </summary>
        ''' <param name="Meta">元数据</param>
        ''' <returns></returns>
        Public Overridable Function ParseMusicAttribute(Meta As String) As DLNAMusicAttribute
            Return DLNAMusicProviders.ParseMusicAttributeDefault(Meta)
        End Function

        ''' <summary>
        ''' 保存配置信息
        ''' </summary>
        ''' <returns></returns>
        Public Overridable Function Save() As String
            Return Settings
        End Function

        ''' <summary>
        ''' 释放资源
        ''' </summary>
        Public Sub Dispose() Implements IDisposable.Dispose
            RequestCache.Clear()
        End Sub

    End Class

End Namespace
