Imports System.Reflection

Namespace Commands

    Public Class CommandParser

        Private ReadOnly Settings As SettingContainer

        Private ReadOnly Commands As New List(Of Command)

        Private ReadOnly ExitAction As New Action(Sub() RaiseEvent OnExit())

        Private ReadOnly ProvidedParameters() As Object

        Private ExitFlag As Boolean

        ''' <summary>
        ''' 退出事件
        ''' </summary>
        Public Event OnExit()

        ''' <summary>
        ''' 初始化
        ''' </summary>
        ''' <param name="K"></param>
        ''' <param name="Web"></param>
        Public Sub New(K As EasyK, Web As KWebCore, Settings As SettingContainer)
            Me.Settings = Settings
            ProvidedParameters = {K, Web, Settings, Commands, ExitAction}
            ExitFlag = False

            LoadCommands()
        End Sub

        Private Sub LoadCommands()
            Dim Commands As New Dictionary(Of CommandType, List(Of Command))
            For Each t As CommandType In [Enum].GetValues(GetType(CommandType))
                Commands.Add(t, New List(Of Command))
            Next

            '反射加载
            Dim CommandNamespace As String = "EasyK.Commands"
            For Each asm As Assembly In AppDomain.CurrentDomain.GetAssemblies()
                Try
                    AsyncUtils.Process(
                        Settings.Settings.Async.CompletelySync,
                        Settings.Settings.Async.AsyncMode,
                        asm.GetTypes(),
                        Sub(ByRef h, type)
                            With type
                                '检查类型
                                If .Namespace <> CommandNamespace OrElse
                                    .IsAbstract OrElse
                                    Not GetType(Command).IsAssignableFrom(type) Then h = True
                            End With
                        End Sub,
                        Sub(type)
                            '遍历构造器
                            For Each c In type.GetConstructors()
                                Dim Valid As Boolean = True
                                Dim Params As New List(Of Object)

                                For Each p In c.GetParameters()
                                    '已选择的参数
                                    Dim Selected As Object = Nothing

                                    '遍历所有能提供的参数
                                    For Each pp In ProvidedParameters
                                        '检测所要求的参数是否为能提供的参数的父类
                                        If Not p.ParameterType.IsAssignableFrom(pp.GetType()) Then Continue For

                                        Selected = pp
                                        Exit For
                                    Next

                                    '参数无效 提前退出
                                    If Selected Is Nothing Then
                                        Valid = False
                                        Exit For
                                    End If

                                    Params.Add(Selected)
                                Next

                                '如果构造器无效 则尝试下一个
                                If Not Valid Then Continue For

                                '匹配构造函数成功 退出循环
                                Try
                                    Dim cmd As Command = c.Invoke(Params.ToArray())
                                    SyncLock Commands
                                        Commands(cmd.Type).Add(cmd)
                                    End SyncLock
                                Catch ex As Exception
                                    Console.WriteLine("加载指令 {0} 时出错 - {1}",
                                                      type.Name.Substring(Math.Min(type.Name.Length, 7)),
                                                      ex.Message
                                    )
                                End Try

                                Exit For
                            Next
                        End Sub
                    )
                Catch ex As ReflectionTypeLoadException
                    For Each e As Exception In ex.LoaderExceptions
                        Console.WriteLine("从程序集 {0} 加载指令时出错 - {1}", asm.FullName, e.Message)
                    Next
                Catch ex As Exception
                    Console.WriteLine("从程序集 {0} 加载指令时出错 - {1}", asm.FullName, ex.Message)
                End Try
            Next

            For Each t As CommandType In Commands.Keys()
                '指令排序
                Commands(t).Sort(Function(a, b) a.Prefix.CompareTo(b.Prefix))

                For Each c In Commands(t)
                    Me.Commands.Add(c)
                Next
            Next
        End Sub

        ''' <summary>
        ''' 运行指令系统
        ''' </summary>
        Public Sub Run()
            While Not ExitFlag
                Dim cmd As String = Console.ReadLine()
                If String.IsNullOrWhiteSpace(cmd) Then Continue While

                Dim Success As Boolean = False
                For Each Parser As Command In Commands
                    If Parser.Match(cmd) Then
                        Success = True
                        Exit For
                    End If
                Next

                If Not Success Then Console.WriteLine("未知指令 帮助指令为: help")
            End While
        End Sub

        ''' <summary>
        ''' 关闭指令系统
        ''' </summary>
        Public Sub Close()
            ExitFlag = True
            Try
                Console.SetIn(New IO.StringReader(""))
            Catch
            End Try
        End Sub

    End Class

End Namespace
