Imports System.Collections.Concurrent

Namespace DLNA.Protocol

    Public Class DLNAAction

        ''' <summary>
        ''' 获取操作名称
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Name As String

        ''' <summary>
        ''' 获取返回参数数量
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property ReturnLength As Integer
            Get
                Return Returns.Count
            End Get
        End Property

        Private ReadOnly ServiceNamespace As String

        Private ReadOnly Arguments As New ConcurrentDictionary(Of String, String)

        Private ReadOnly Returns As New ConcurrentDictionary(Of String, String)

        ''' <summary>
        ''' 初始化操作对象
        ''' </summary>
        ''' <param name="ServiceNamespace">服务命名空间</param>
        ''' <param name="Element">Xml元素</param>
        Public Sub New(ServiceNamespace As String, Element As XElement)
            Me.ServiceNamespace = ServiceNamespace
            Name = Element.Element(DLNAService.ServiceNamespace + "name").Value

            Dim Original = From el In Element.Descendants(DLNAService.ServiceNamespace + "argument")
                           Where el.Parent.Name = DLNAService.ServiceNamespace + "argumentList"
                           Select el
            For Each Arg In Original
                Dim Name As String = Arg.Element(DLNAService.ServiceNamespace + "name").Value
                Dim Related As XElement = Arg.Element(DLNAService.ServiceNamespace + "relatedStateVariable")
                If Related Is Nothing Then Continue For

                Select Case Arg.Element(DLNAService.ServiceNamespace + "direction").Value.ToLower()
                    Case = "in"
                        Arguments.TryAdd(Name, Related.Value)
                    Case = "out"
                        Returns.TryAdd(Name, Related.Value)
                    Case Else
                        Throw New ArgumentException($"无效的参数方向: {Name}")
                End Select
            Next
        End Sub

        ''' <summary>
        ''' 获取合法参数
        ''' </summary>
        ''' <param name="Content">远程调用请求</param>
        ''' <returns></returns>
        Public Function GetValidArgs(Content As XElement) As Dictionary(Of String, String)
            Dim Valid As New Dictionary(Of String, String)
            For Each Element In Content.Elements
                Dim Name As String = Element.Name.LocalName
                If Arguments.ContainsKey(Name) Then Valid.Add(Name, Element.Value)
            Next

            Return Valid
        End Function

        ''' <summary>
        ''' 更新状态
        ''' </summary>
        ''' <param name="Service">协议管理器对象</param>
        ''' <param name="Args">合法参数</param>
        Public Sub Update(Service As DLNAService, Args As Dictionary(Of String, String))
            For Each Arg As String In Args.Keys
                If Not Arguments.ContainsKey(Arg) OrElse Arguments(Arg).StartsWith(DLNAService.StateBypassPrefix) Then Continue For

                Service.SetState(Arguments(Arg), Args(Arg))
            Next
        End Sub

        ''' <summary>
        ''' 获取Xml形式返回值
        ''' </summary>
        ''' <param name="Returns">返回值对象</param>
        ''' <returns></returns>
        Public Function GetXmlReturns(Returns As Dictionary(Of String, String)) As String
            Dim envelope = New XElement(XName.Get("Envelope", DLNAService.SOAPNamespace))
            envelope.SetAttributeValue(XNamespace.Xmlns + "s", DLNAService.SOAPNamespace)
            envelope.SetAttributeValue(XName.Get("encodingStyle", DLNAService.SOAPNamespace), DLNAService.SOAPEncodingNamespace)
            Dim body = New XElement(XName.Get("Body", DLNAService.SOAPNamespace))
            Dim response = New XElement(XName.Get($"{Name}Response", ServiceNamespace))
            response.SetAttributeValue(XNamespace.Xmlns + "u", ServiceNamespace)

            For Each kvp In Returns
                Dim prop = New XElement(kvp.Key)
                If kvp.Value IsNot Nothing Then prop.Value = kvp.Value
                response.Add(prop)
            Next

            body.Add(response)
            envelope.Add(body)

            Return envelope.ToString(SaveOptions.DisableFormatting)
        End Function

        ''' <summary>
        ''' 获取返回值对象
        ''' </summary>
        ''' <param name="Service">协议管理器对象</param>
        ''' <returns></returns>
        Public Function GetReturns(Service As DLNAService) As Dictionary(Of String, String)
            Dim Returns As New Dictionary(Of String, String)
            For Each r In Me.Returns
                Returns.Add(r.Key, Service.GetState(r.Value))
            Next

            Return Returns
        End Function

    End Class

End Namespace
