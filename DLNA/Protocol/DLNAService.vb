Imports System.Collections.Concurrent
Imports System.Reflection
Imports System.Text.RegularExpressions
Imports Microsoft.AspNetCore.Http
Imports Microsoft.Extensions.Primitives

Namespace DLNA.Protocol

    ''' <summary>
    ''' DLNA服务抽象基类
    ''' </summary>
    Public MustInherit Class DLNAService

        Friend Const SOAPNamespace As String = "http://schemas.xmlsoap.org/soap/envelope/"

        Friend Const SOAPEncodingNamespace As String = "http://schemas.xmlsoap.org/soap/encoding/"

        Private Const [Namespace] As String = "urn:schemas-upnp-org:service"

        Private Const ActionsName As String = "actionList"

        Private Const ActionName As String = "action"

        Private Const StateTableName As String = "serviceStateTable"

        Private Const StateName As String = "stateVariable"

        Friend Const StateBypassPrefix As String = "A_ARG_TYPE_"

        ''' <summary>
        ''' 服务描述文档命名空间
        ''' </summary>
        Public Shared ReadOnly ServiceNamespace As XNamespace = XNamespace.Get($"{[Namespace]}-1-0")

        ''' <summary>
        ''' SOAP命名空间
        ''' </summary>
        Protected Shared ReadOnly SOAPNamespaceX As XNamespace = XNamespace.Get(SOAPNamespace)

        Protected Shared ReadOnly MetaNamespace As XNamespace = XNamespace.Get("urn:schemas-upnp-org:metadata-1-0/AVT/")

        Private Shared ReadOnly UUIDRegex As New Regex("(?<=^uuid\:)([A-Za-z0-9\-]+)$")

        Private Shared ReadOnly TimeoutRegex As New Regex("(?<=^Second-)(\d+)$")

        Private Shared ReadOnly DeliverRegex As New Regex("(?<=\<)((https?):\/\/([^\/\s]+)\/?([^#\s]*)#?([^#\s]*))(?=\>)")

        Private Shared ReadOnly MethodRegex As New Regex("(?<=#)([A-Za-z\d_]+)(?=\""$)")

        Protected ReadOnly ServiceName As String

        Friend ReadOnly Protocol As DLNAProtocol

        '本服务的状态
        Protected ReadOnly States As New ConcurrentDictionary(Of String, Object)

        '本服务的更新列表
        Friend ReadOnly Updated As New HashSet(Of String)

        '本服务的操作
        Protected ReadOnly Actions As New ConcurrentDictionary(Of String, DLNAAction)

        '本服务相关状态值名称
        Protected ReadOnly Related As New HashSet(Of String)

        '订阅者
        Protected ReadOnly Subscribers As New ConcurrentDictionary(Of String, DLNASubscriber)

        Protected Sub New(Protocol As DLNAProtocol, Name As String, Xml As String)
            Me.Protocol = Protocol
            ServiceName = $"{[Namespace]}:{Name}:1"

            Dim Doc As XDocument = XDocument.Parse(Xml)

            '注册状态值
            RegisterState(Doc)

            '注册远程调用
            RegisterActions(Doc)
        End Sub

        '注册状态
        Protected Sub RegisterState(Doc As XDocument)
            Dim Elements = From el In Doc.Descendants(ServiceNamespace + StateName)
                           Where el.Parent.Name = ServiceNamespace + StateTableName
                           Select el

            For Each StateVariable In Elements
                Dim State As Object = DLNAState(Of Object).CreateState(Me, StateVariable)
                If State Is Nothing Then Continue For

                Dim Name As String = State.GetType().GetProperty("Name").GetValue(State)

                Related.Add(Name)

                With States
                    If .ContainsKey(Name) Then
                        States(Name) = State
                    Else
                        .TryAdd(Name, State)
                    End If
                End With
            Next
        End Sub

        '注册远程调用
        Protected Sub RegisterActions(Doc As XDocument)
            Dim Elements = From el In Doc.Descendants(ServiceNamespace + ActionName)
                           Where el.Parent.Name = ServiceNamespace + ActionsName
                           Select el

            For Each e In Elements
                Dim Action As New DLNAAction(ServiceName, e)
                Actions.TryAdd(Action.Name, Action)
            Next
        End Sub

        ''' <summary>
        ''' 设置参数值是否推送
        ''' </summary>
        ''' <param name="Name">参数名称</param>
        ''' <param name="SendKeys">是否推送</param>
        Protected Sub SetStateEvent(Name As String, SendKeys As Boolean)
            If Not States.ContainsKey(Name) Then Return

            Dim State = States(Name)
            If State Is Nothing Then Return

            Dim p = State.GetType().GetProperty(NameOf(DLNAState(Of Object).SendEvents))
            If p Is Nothing Then Return
            p.SetValue(State, SendKeys)
        End Sub

        ''' <summary>
        ''' 更新广播
        ''' </summary>
        Public Sub Broadcast()
            '获取更新列表
            Dim Updated As List(Of String)
            SyncLock Me.Updated
                Updated = New List(Of String)(Me.Updated)
                Me.Updated.Clear()
            End SyncLock

            '发送事件广播
            If Updated.Count > 0 Then Broadcast(Updated)
        End Sub


        ''' <summary>
        ''' 生成更新消息
        ''' </summary>
        ''' <param name="Updated">已更新状态</param>
        ''' <returns></returns>
        Protected MustOverride Function GenerateNotify(Updated As Dictionary(Of String, String)) As String

        '状态更新广播
        Protected Sub Broadcast(Updated As List(Of String))
            '清理无效订阅
            Dim Invalid As New List(Of String)
            For Each s In Subscribers
                If s.Value.IsExpired() Then Invalid.Add(s.Key)
            Next
            For Each i As String In Invalid
                Subscribers.TryRemove(i, Nothing)
            Next

            '获取有效更新列表
            Dim Valid As New Dictionary(Of String, String)

            For Each u As String In Updated
                If Not Related.Contains(u) OrElse Not States.ContainsKey(u) Then Continue For

                Dim State = States(u)
                If State Is Nothing Then Continue For

                Dim Value = State.GetType().GetProperty(NameOf(DLNAState(Of Object).Value))
                If Value Is Nothing Then Continue For

                Dim v = Value.GetValue(State)

                Valid.Add(u, If(v Is Nothing, v, v.ToString))
            Next

            If Valid.Count = 0 Then Return

            '生成更新Xml
            Dim Xml As String = GenerateNotify(Valid)
            If String.IsNullOrEmpty(Xml) Then Return

            '发送
            Dim Buffer As Byte() = Text.Encoding.UTF8.GetBytes(Xml)
            For Each s As DLNASubscriber In Subscribers.Values
                s.Commit(Buffer)
            Next
        End Sub

        ''' <summary>
        ''' 响应事件管理请求
        ''' </summary>
        ''' <param name="ctx">Http上下文</param>
        ''' <returns></returns>
        Public Function EventHandler(ctx As HttpContext) As Task
            With ctx.Request
                Select Case .Method
                    Case "SUBSCRIBE"
                        If .Headers().ContainsKey("SID") Then
                            '续订
                            Return SubscribeRenew(ctx)
                        Else
                            '订阅
                            Return SubscribeNew(ctx)
                        End If
                    Case "UNSUBSCRIBE"
                        '取消
                        Return Unsubscribe(ctx)
                    Case Else
                        Return WebStartup.RespondStatusOnly(ctx)
                End Select
            End With
        End Function

        Private Function SubscribeNew(ctx As HttpContext) As Task
            With ctx.Request
                Dim NTValue As StringValues = .Headers("NT")
                Dim CallbackValue As StringValues = .Headers("CALLBACK")
                If NTValue.Count = 0 OrElse NTValue(0) <> "upnp:event" OrElse CallbackValue.Count = 0 Then _
                                Return WebStartup.RespondStatusOnly(ctx, 412)

                Dim Urls As New List(Of String)
                For Each m As Match In DeliverRegex.Matches(CallbackValue(0))
                    If m.Length > 0 Then Urls.Add(m.Value)
                Next
                If Urls.Count = 0 Then Return WebStartup.RespondStatusOnly(ctx, 412)

                '获取超时时间
                Dim TimeoutValue As StringValues = .Headers("TIMEOUT")
                Dim Timeout As Integer = Protocol.Settings.Settings.DLNA.EventDefaultExpire
                If TimeoutValue.Count > 0 Then
                    Dim m = TimeoutRegex.Match(TimeoutValue(0))
                    If Not String.IsNullOrEmpty(m.Value) Then _
                        Timeout = Math.Min(Val(m.Value), Protocol.Settings.Settings.DLNA.EventMaxExpire)
                End If

                '订阅
                Dim Subscriber As DLNASubscriber
                Do
                    Subscriber = New DLNASubscriber(Protocol, Urls, Timeout)
                Loop While Subscribers.ContainsKey(Subscriber.SID)

                Subscribers.TryAdd(Subscriber.SID, Subscriber)

                '推送状态值
                Dim States As New Dictionary(Of String, String)
                For Each Name As String In Related
                    If Not States.ContainsKey(Name) Then
                        States.Add(Name, vbNullString)
                        Continue For
                    End If

                    Dim s = States(Name)
                    If s Is Nothing Then Continue For

                    '跳过不需要发送的参数
                    Dim e = s.GetType().GetProperty(NameOf(DLNAState(Of Object).SendEvents))
                    If Not If(e.GetValue(s), False) Then Continue For

                    Dim p = s.GetType().GetProperty(NameOf(DLNAState(Of Object).Value))
                    Dim v = p.GetValue(s)

                    States.Add(Name, If(v Is Nothing, v, v.ToString()))
                Next

                Subscriber.Push(States)

                Return RespondSubscription(ctx, Subscriber.SID, Timeout)
            End With
        End Function

        Private Function SubscribeRenew(ctx As HttpContext) As Task
            With ctx.Request
                If .Headers().ContainsKey("NT") OrElse .Headers().ContainsKey("CALLBACK") Then _
                                Return WebStartup.RespondStatusOnly(ctx, 400)

                '检查SID
                Dim SIDValue As StringValues = .Headers("SID")
                If SIDValue.Count = 0 Then Return WebStartup.RespondStatusOnly(ctx, 412)

                Dim m = UUIDRegex.Match(SIDValue(0))
                Dim SID As String = m.Value
                If String.IsNullOrEmpty(SID) OrElse Not Subscribers.ContainsKey(SID) Then _
                    Return WebStartup.RespondStatusOnly(ctx, 412)

                Dim Subscriber = Subscribers(SID)
                If Subscriber.IsExpired() Then
                    Subscribers.TryRemove(SID, Nothing)
                    Return WebStartup.RespondStatusOnly(ctx, 412)
                End If

                '获取超时时间
                Dim TimeoutValue As StringValues = .Headers("TIMEOUT")
                Dim Timeout As Integer = Protocol.Settings.Settings.DLNA.EventDefaultExpire
                If TimeoutValue.Count > 0 Then
                    m = TimeoutRegex.Match(TimeoutValue(0))
                    If Not String.IsNullOrEmpty(m.Value) Then _
                        Timeout = Math.Min(Val(m.Value), Protocol.Settings.Settings.DLNA.EventMaxExpire)
                End If

                '订阅续期
                Subscriber.Renew(Timeout)

                Return RespondSubscription(ctx, SID, Timeout)
            End With
        End Function

        Private Function Unsubscribe(ctx As HttpContext) As Task
            With ctx.Request
                If .Headers().ContainsKey("NT") OrElse .Headers().ContainsKey("CALLBACK") Then _
                                Return WebStartup.RespondStatusOnly(ctx, 400)

                '检查SID
                Dim SIDValue As StringValues = .Headers("SID")
                If SIDValue.Count = 0 Then Return WebStartup.RespondStatusOnly(ctx, 412)

                Dim m = UUIDRegex.Match(SIDValue(0))
                Dim SID As String = m.Value
                If String.IsNullOrEmpty(SID) OrElse Not Subscribers.ContainsKey(SID) Then _
                    Return WebStartup.RespondStatusOnly(ctx, 412)

                Dim Subscriber = Subscribers(SID)
                If Subscriber.IsExpired() Then
                    Subscribers.TryRemove(SID, Nothing)
                    Return WebStartup.RespondStatusOnly(ctx, 412)
                End If

                '移除
                Subscribers.TryRemove(SID, Nothing)

                Return WebStartup.RespondStatusOnly(ctx, 200)
            End With
        End Function

        Private Shared Function RespondSubscription(ctx As HttpContext, SID As String, Timeout As Integer) As Task
            With ctx.Response
                WebStartup.AddHeaderSafe(.Headers, "SERVER", DLNA.Agent)
                WebStartup.AddHeaderSafe(.Headers, "SID", $"uuid:{SID}")
                WebStartup.AddHeaderSafe(.Headers, "TIMEOUT", $"Second-{Timeout}")
            End With

            Return WebStartup.RespondStatusOnly(ctx, 200)
        End Function

        ''' <summary>
        ''' 解析远程操作
        ''' </summary>
        ''' <param name="ctx">Http上下文</param>
        ''' <returns></returns>
        Public Function Act(ctx As HttpContext) As Task
            With ctx.Request
                '参数头检查
                If Not .Headers.ContainsKey("SOAPAction") Then Return WebStartup.RespondStatusOnly(ctx, 402)

                Dim ActionValue = .Headers("SOAPAction")
                If ActionValue.Count = 0 Then Return WebStartup.RespondStatusOnly(ctx, 402)

                '检查服务名称和方法名称
                If Not ActionValue(0).StartsWith($"""{ServiceName}") Then Return WebStartup.RespondStatusOnly(ctx, 401)

                Dim m = MethodRegex.Match(ActionValue(0))
                If m.Length = 0 OrElse Not Actions.ContainsKey(m.Value) Then Return WebStartup.RespondStatusOnly(ctx, 401)
            End With

            Dim Request As String = WebStartup.GetRequestBody(ctx)
            Dim Response As String = RemoteCall(Request)

            If String.IsNullOrEmpty(Response) Then Return WebStartup.RespondStatusOnly(ctx, 500)

            Return WebStartup.RespondText(ctx, Response, "text/xml;charset=utf-8")
        End Function

        ''' <summary>
        ''' 设置状态值
        ''' </summary>
        ''' <param name="Name">状态名称</param>
        ''' <param name="Value">状态值</param>
        Public Sub SetState(Name As String, Value As String)
            If Not States.ContainsKey(Name) Then Return

            Dim State = States(Name)
            If State Is Nothing Then Return

            Dim p = State.GetType().GetProperty(NameOf(DLNAState(Of Object).Value))
            If p Is Nothing Then Return

            p.SetValue(State, Value)
        End Sub

        ''' <summary>
        ''' 获取状态值
        ''' </summary>
        ''' <param name="Name">状态名称</param>
        ''' <returns></returns>
        Public Function GetState(Name As String) As String
            If Not States.ContainsKey(Name) Then Return vbNullString

            Dim State = States(Name)
            If State Is Nothing Then Return vbNullString

            Dim p = State.GetType().GetProperty(NameOf(DLNAState(Of Object).Value))
            If p Is Nothing Then Return vbNullString

            Dim v = p.GetValue(State)

            Return If(v Is Nothing, vbNullString, v.ToString())
        End Function

        ''' <summary>
        ''' 执行远程调用
        ''' </summary>
        ''' <param name="Request">远程调用请求Xml</param>
        ''' <returns>返回值</returns>
        Public Function RemoteCall(Request As String) As String
            Dim Doc As XDocument = XmlUtils.SafeParseXml(Request)
            If Doc Is Nothing Then Return vbNullString

            Dim Elements = From el In Doc.Descendants(SOAPNamespaceX + "Body")
                           Select el
            For Each Body In Elements
                With Body.Elements()
                    If .Count() = 0 Then Continue For

                    Return RemoteCall(.First())
                End With
            Next

            Return vbNullString
        End Function

        ''' <summary>
        ''' 执行远程调用
        ''' </summary>
        ''' <param name="Content">远程调用请求</param>
        ''' <returns>返回值</returns>
        Public Function RemoteCall(Content As XElement) As String
            Dim xn As XName = Content.Name
            If xn.NamespaceName <> ServiceName Then Return vbNullString

            If Not Actions.ContainsKey(xn.LocalName) Then Return vbNullString
            Dim Action = Actions(xn.LocalName)

            Dim Args As Dictionary(Of String, String) = Action.GetValidArgs(Content)

            Dim Caller As MethodInfo = Me.GetType().GetMethod(xn.LocalName, BindingFlags.Instance Or BindingFlags.NonPublic Or BindingFlags.Public)
            Dim Returns As Dictionary(Of String, String) = Nothing
            Dim Handled As Boolean = False
            If Caller IsNot Nothing Then
                '关于远程调用函数的说明
                'Func(ByRef Handled As Boolean, ByVal Args As Dictionary(Of String, String)) As Dictionary(Of String, String)

                Try
                    Returns = Caller.Invoke(Me, {Handled, Args})
                Catch ex As TargetInvocationException
                    If Protocol.Settings.Settings.DebugMode Then
                        Console.WriteLine("DLNA远程调用中断 - {0}", ex.InnerException.Message)
                    End If

                    Return vbNullString
                Catch ex As Exception
                    If Protocol.Settings.Settings.DebugMode Then
                        Console.WriteLine("DLNA远程调用中断 - {0}", ex.InnerException.Message)
                    End If

                    Return vbNullString
                End Try
            End If

            If Not Handled Then Action.Update(Me, Args)

            If Returns Is Nothing Then
                Returns = Action.GetReturns(Me)
            ElseIf Returns.Count < Action.ReturnLength Then
                Dim Standard = Action.GetReturns(Me)
                For Each r In Standard
                    If Not Returns.ContainsKey(r.Key) Then Returns.Add(r.Key, r.Value)
                Next
            End If

            If Protocol.Settings.Settings.DebugMode Then
                Console.WriteLine("远程调用 - {0}:{1}", ServiceName, Action.Name)
                Console.WriteLine(String.Join(vbCrLf, Args.Select(Function(kvp) $"{kvp.Key}: {kvp.Value}")))
                Console.WriteLine("返回值:")
                Console.WriteLine(String.Join(vbCrLf, Returns.Select(Function(kvp) $"{kvp.Key}: {If(Not String.IsNullOrEmpty(kvp.Value) AndAlso kvp.Value.Length > 100, $"{kvp.Value.Substring(0, 100)}...", kvp.Value)}")))
            End If


            Return Action.GetXmlReturns(Returns)
        End Function

    End Class

End Namespace
