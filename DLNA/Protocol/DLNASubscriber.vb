Imports System.Net
Imports System.Text

Namespace DLNA.Protocol

    Public Class DLNASubscriber

        Private Const SeqThreshold As UInteger = 4294967295UI

        ''' <summary>
        ''' 获取SID
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property SID As String

        Private ReadOnly Protocol As DLNAProtocol

        Private ReadOnly Deliver As List(Of String)

        Private Seq As UInteger

        Private Failed As Integer

        Private ExpireTime As Long

        ''' <summary>
        ''' 初始化订阅者
        ''' </summary>
        ''' <param name="Protocol">DLNA协议管理器对象</param>
        ''' <param name="Deliver">传递Url</param>
        ''' <param name="Timeout">超时时间</param>
        Public Sub New(Protocol As DLNAProtocol, Deliver As List(Of String), Timeout As Integer)
            Me.Protocol = Protocol
            Me.Deliver = Deliver

            SID = Guid.NewGuid().ToString().ToLower()
            Seq = 1
            Failed = 0
            ExpireTime = Now.AddSeconds(Timeout).Ticks
        End Sub

        ''' <summary>
        ''' 检测是否过期
        ''' </summary>
        ''' <returns></returns>
        Public Function IsExpired() As Boolean
            Return Now.Ticks > ExpireTime OrElse Failed > Protocol.Settings.Settings.DLNA.EventNotifyFails
        End Function

        ''' <summary>
        ''' 订阅续期
        ''' </summary>
        ''' <param name="Timeout">超时时间</param>
        Public Sub Renew(Timeout As Integer)
            ExpireTime = Now.AddSeconds(Timeout).Ticks
        End Sub

        ''' <summary>
        ''' 推送参数值
        ''' </summary>
        Public Sub Push(States As Dictionary(Of String, String))
            Dim root = New XElement(DLNAService.ServiceNamespace + "propertyset")
            root.Add(New XAttribute(XNamespace.Xmlns + "e", DLNAService.ServiceNamespace))

            For Each kvp In States
                Dim prop = New XElement(DLNAService.ServiceNamespace + "property")
                Dim item = New XElement(kvp.Key)
                If kvp.Value IsNot Nothing Then item.Value = kvp.Value
                prop.Add(item)
                root.Add(prop)
            Next

            Dim Xml As String = $"<?xml version=""1.0"" encoding=""UTF-8""?>{vbCrLf}{root.ToString()}"
            Commit(Encoding.UTF8.GetBytes(Xml), 0)
        End Sub

        Private Sub Commit(Updated As Byte(), Seq As UInteger)
            Task.Run(Sub()
                         For Each Url As String In Deliver
                             Dim Request As HttpWebRequest = DirectCast(WebRequest.Create(Url), HttpWebRequest)
                             With Request
                                 .Method = "NOTIFY"

                                 With .Headers
                                     .Add("NT", "upnp:event")
                                     .Add("NTS", "upnp:propchange")
                                     .Add("SID", $"uuid:{SID}")
                                     .Add("SEQ", Seq.ToString())
                                 End With

                                 .ContentType = "text/xml; charset=""utf-8"""
                                 .ContentLength = Updated.Length

                                 Try
                                     Using Stream As IO.Stream = Request.GetRequestStream()
                                         Stream.Write(Updated, 0, Updated.Length)
                                     End Using

                                     Dim Response As HttpWebResponse = .GetResponse()
                                     If Response.StatusCode = HttpStatusCode.OK Then
                                         '推送成功
                                         Failed = 0
                                         Return
                                     End If
                                 Catch ex As Exception
                                     If Protocol.Settings.Settings.DebugMode Then
                                         Console.WriteLine("订阅投递失败: {0} - {1}", Url, ex.Message)
                                     End If
                                 End Try
                             End With
                         Next

                         '推送失败
                         Failed = Failed + 1
                     End Sub)
        End Sub

        ''' <summary>
        ''' 提交事件更改
        ''' </summary>
        ''' <param name="Updated">已更新列表数据</param>
        Public Sub Commit(Updated As Byte())
            Dim Seq As UInteger = Me.Seq
            Me.Seq += 1
            If Me.Seq > SeqThreshold Then Me.Seq = 1

            Commit(Updated, Seq)
        End Sub

    End Class

End Namespace
