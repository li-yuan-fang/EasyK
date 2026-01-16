Public MustInherit Class Command

    Public Enum CommandType
        None
        System
        User
    End Enum

    ''' <summary>
    ''' 获取指令前缀
    ''' </summary>
    Public ReadOnly Prefix As String

    ''' <summary>
    ''' 获取指令类型
    ''' </summary>
    Public ReadOnly Type As CommandType

    ''' <summary>
    ''' 获取指令用法
    ''' </summary>
    Public ReadOnly Usage As String

    ''' <summary>
    ''' 实例化指令对象
    ''' </summary>
    ''' <param name="Prefix">指令前缀</param>
    ''' <param name="Usage">指令用法</param>
    ''' <param name="Type">指令类型</param>
    Protected Sub New(Prefix As String, Usage As String, Type As CommandType)
        Me.Prefix = Prefix
        Me.Usage = Usage
        Me.Type = Type
    End Sub

    ''' <summary>
    ''' 运行指令
    ''' </summary>
    ''' <param name="Args">指令参数</param>
    Protected MustOverride Sub Process(Args() As String)

    ''' <summary>
    ''' 打印用法
    ''' </summary>
    Protected Sub InvalidUsage()
        Console.WriteLine("指令格式错误 - {0}", Usage)
    End Sub

    ''' <summary>
    ''' 匹配指令
    ''' </summary>
    ''' <param name="Input">指令输入</param>
    ''' <returns></returns>
    Public Function Match(Input As String) As Boolean
        If String.IsNullOrEmpty(Input) Then Return False

        Dim Args As New List(Of String)

        Dim Buffer As String = vbNullString
        Dim Quoted As Boolean = False
        For i = 0 To Input.Length - 1
            Select Case Input(i)
                Case = """"c
                    If Quoted Then
                        Args.Add(Buffer)
                        Buffer = vbNullString
                        Quoted = False
                    Else
                        If String.IsNullOrEmpty(Buffer) Then
                            Quoted = True
                        Else
                            Buffer = vbNullString
                        End If
                    End If
                Case = " "c
                    If Quoted Then
                        Buffer &= Input(i)
                    Else
                        If Not String.IsNullOrEmpty(Buffer) Then
                            Args.Add(Buffer)
                            Buffer = vbNullString
                        End If
                    End If
                Case Else
                    Buffer &= Input(i)
            End Select
        Next

        If Not String.IsNullOrEmpty(Buffer) Then Args.Add(Buffer)

        If Not Prefix.ToLower().Equals(Args(0).ToLower()) Then Return False

        Process(Args.ToArray())

        Return True
    End Function

End Class
