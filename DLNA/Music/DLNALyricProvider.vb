Imports System.Collections.Specialized
Imports System.Net
Imports System.Text
Imports Newtonsoft.Json

Namespace DLNA.MusicProvider

    Public MustInherit Class DLNALyricProvider

        Protected Const UserAgent As String = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/63.0.3239.132 Safari/537.36"

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
        Public MustOverride Function GetLyric(Meta As String) As String

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

    End Class

End Namespace
