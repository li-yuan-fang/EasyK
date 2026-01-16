Imports System.Reflection

Namespace DLNA.Protocol

    Public Class DLNAState(Of T)

        ''' <summary>
        ''' 合法数值类型
        ''' </summary>
        Public Enum AllowValueType
            ''' <summary>
            ''' 任意值
            ''' </summary>
            Any = 0
            ''' <summary>
            ''' 连续值域
            ''' </summary>
            Range
            ''' <summary>
            ''' 离散值域
            ''' </summary>
            List
        End Enum

        ''' <summary>
        ''' 获取状态名称
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Name As String

        ''' <summary>
        ''' 获取状态值
        ''' </summary>
        ''' <returns></returns>
        Public Property Value As Object
            Get
                Return _Value
            End Get
            Set(value As Object)
                Try
                    _Value = ParseValue(value)
                    If AllowedValueType = AllowValueType.List Then _Value = FitList(_Value)

                    '当需要推送事件时加入
                    If SendEvents Then
                        SyncLock _Service.Updated
                            _Service.Updated.Add(Name)
                        End SyncLock
                    End If
                Catch ex As Exception
                    If _Service.Protocol.Settings.Settings.DebugMode Then
                        Console.WriteLine("DLNA状态值赋值失败({0} <= {1}) - {2}", Name, value, ex.Message)
                    End If
                End Try
            End Set
        End Property

        ''' <summary>
        ''' 获取状态类型
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Type As Type

        ''' <summary>
        ''' 获取事件关联状态
        ''' </summary>
        ''' <returns></returns>
        Public Property SendEvents As Boolean

        ''' <summary>
        ''' 获取合法值域类型
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property AllowedValueType As AllowValueType
            Get
                Return _AllowedValueType
            End Get
        End Property

        ''' <summary>
        ''' 获取合法值域
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property AllowedValueList As List(Of T)

        ''' <summary>
        ''' 获取合法下限
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property AllowedMinimum As Long

        ''' <summary>
        ''' 获取合法上限
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property AllowedMaximum As Long

        Protected _Value As T

        Protected _AllowedValueType As AllowValueType

        Protected ReadOnly _Service As DLNAService

        Private Function FitList(value As Object) As Object
            If Type = GetType(String) Then
                If String.IsNullOrEmpty(value) Then Return AllowedValueList(0)

                For Each a In AllowedValueList
                    If DirectCast(DirectCast(a, Object), String).ToLower() = DirectCast(value, String).ToLower() Then _
                        Return a
                Next
                Return AllowedValueList(0)
            Else
                If Not AllowedValueList.Contains(value) Then Return AllowedValueList(0)
            End If

            Return value
        End Function

        Private Function FitRange(value As Object) As Object
            If value < AllowedMinimum Then
                value = AllowedMinimum
            ElseIf value > AllowedMaximum Then
                value = AllowedMaximum
            End If

            Return value
        End Function

        Private Function ParseValue(value As Object) As T
            If value Is Nothing Then Return value

            Select Case GetType(T)
                Case = GetType(Boolean), GetType(Short), GetType(UShort), GetType(Integer), GetType(UInteger)
                    Dim Parse As MethodInfo = Type.GetMethod("Parse",
                                                             BindingFlags.Public Or BindingFlags.Static,
                                                             Nothing,
                                                             New Type() {value.GetType()},
                                                             Nothing)
                    If GetType(T) <> GetType(Boolean) AndAlso AllowedValueType = AllowValueType.Range Then
                        Return FitRange(Parse.Invoke(Nothing, {value}))
                    Else
                        Return Parse.Invoke(Nothing, {value})
                    End If
                Case Else
                    Return DirectCast(value.ToString(), Object)
            End Select
        End Function

        Private Shared Function ParseValue(Type As Type, Value As String) As Object
            Select Case Type
                Case = GetType(Boolean), GetType(Short), GetType(UShort), GetType(Integer), GetType(UInteger)
                    Dim Parse As MethodInfo = Type.GetMethod("Parse", BindingFlags.Public Or BindingFlags.Static)
                    Return Parse.Invoke(Nothing, {Value})
                Case Else
                    Return Value
            End Select
        End Function

        ''' <summary>
        ''' 创建DLNAState对象
        ''' </summary>
        ''' <param name="Service">协议管理器对象</param>
        ''' <param name="Element">Xml元素</param>
        ''' <returns></returns>
        Public Shared Function CreateState(Service As DLNAService, Element As XElement) As Object
            Try
                With Element
                    Dim Name As String = .Element(DLNAService.ServiceNamespace + "name").Value
                    Dim SendEvents As Boolean = If(.Attribute("sendEvents"), "no") = "yes"

                    Dim SType As Type
                    Select Case .Element(DLNAService.ServiceNamespace + "dataType").Value.ToLower()
                        Case = "boolean"
                            SType = GetType(Boolean)
                        Case = "i2"
                            SType = GetType(Short)
                        Case = "ui2"
                            SType = GetType(UShort)
                        Case = "i4"
                            SType = GetType(Integer)
                        Case = "ui4"
                            SType = GetType(UInteger)
                        Case = "string"
                            SType = GetType(String)
                        Case Else
                            Return Nothing
                    End Select

                    Dim Value As Object = Nothing

                    Dim DefaultValue As XElement = .Element(DLNAService.ServiceNamespace + "defaultValue")
                    If DefaultValue IsNot Nothing Then Value = ParseValue(SType, DefaultValue.Value)

                    Dim AllowedValueList As Object = Nothing
                    Dim AllowedMinimum As Long = 0L
                    Dim AllowedMaximum As Long = 0L

                    Dim AllowedType As AllowValueType
                    Dim AllowedList As XElement = .Element(DLNAService.ServiceNamespace + "allowedValueList")
                    Dim AllowedRange As XElement = .Element(DLNAService.ServiceNamespace + "allowedValueRange")
                    If AllowedList IsNot Nothing Then
                        AllowedType = AllowValueType.List

                        Dim ListType As Type = GetType(List(Of )).MakeGenericType(SType)
                        AllowedValueList = Activator.CreateInstance(ListType)
                        For Each Allowed As XElement In AllowedList.Elements(DLNAService.ServiceNamespace + "allowedValue")
                            AllowedValueList.Add(ParseValue(SType, Allowed.Value))
                        Next
                    ElseIf AllowedRange IsNot Nothing Then
                        AllowedType = AllowValueType.Range

                        AllowedMinimum = Val(AllowedRange.Element(DLNAService.ServiceNamespace + "minimum").Value)
                        AllowedMaximum = Val(AllowedRange.Element(DLNAService.ServiceNamespace + "maximum").Value)
                    Else
                        AllowedType = AllowValueType.Any
                    End If

                    Dim StateType As Type = GetType(DLNAState(Of )).MakeGenericType(SType)
                    Dim Instance As Object = Activator.CreateInstance(StateType,
                                                                  BindingFlags.Instance Or BindingFlags.NonPublic Or BindingFlags.CreateInstance,
                                                                  Nothing,
                                                                  New Object() {Service, Name, Value, SendEvents, AllowedValueList, AllowedMinimum, AllowedMaximum},
                                                                  Nothing,
                                                                  Nothing)
                    Dim AType As FieldInfo = StateType.GetField(NameOf(_AllowedValueType), BindingFlags.Instance Or BindingFlags.NonPublic)
                    AType.SetValue(Instance, AllowedType)

                    Return Instance
                End With
            Catch
                Return Nothing
            End Try
        End Function

        Protected Sub New(Service As DLNAService, Name As String, Value As T, SendEvents As Boolean,
                          AllowedValueList As List(Of T), AllowedMinimum As Long, AllowedMaximum As Long)
            _Service = Service
            Me.Name = Name
            _Value = Value
            Type = GetType(T)
            Me.SendEvents = SendEvents
            Me.AllowedValueList = AllowedValueList
            Me.AllowedMinimum = AllowedMinimum
            Me.AllowedMaximum = AllowedMaximum
        End Sub

    End Class

End Namespace
