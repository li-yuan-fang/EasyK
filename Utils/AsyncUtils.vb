Imports System.Management
Imports System.Threading

Public Class AsyncUtils

    ''' <summary>
    ''' 循环委托
    ''' </summary>
    ''' <param name="Index">索引</param>
    Public Delegate Sub LoopInvoker(Index As Integer)

    ''' <summary>
    ''' 循环前置委托
    ''' </summary>
    ''' <param name="Handled">跳出本循环</param>
    ''' <param name="Index">索引</param>
    Public Delegate Sub PreLoopInvoker(ByRef Handled As Boolean, Index As Integer)

    ''' <summary>
    ''' 枚举循环委托
    ''' </summary>
    ''' <param name="e">枚举元素</param>
    Public Delegate Sub LoopEnumerableInvoker(Of T)(e As T)

    ''' <summary>
    ''' 枚举循环前置委托
    ''' </summary>
    ''' <param name="Handled">跳出本循环</param>
    ''' <param name="e">枚举元素</param>
    Public Delegate Sub PreLoopEnumerableInvoker(Of T)(ByRef Handled As Boolean, e As T)

    ''' <summary>
    ''' 并发模式
    ''' </summary>
    Public Enum ConcurrencyMode As Integer
        ''' <summary>
        ''' 无限制(默认)
        ''' </summary>
        ''' <remarks>高性能设备推荐</remarks>
        NoLimit = 0

        ''' <summary>
        ''' 根据物理核心数限制
        ''' </summary>
        ByPhysics

        ''' <summary>
        ''' 根据逻辑核心数限制
        ''' </summary>
        ByLogic
    End Enum

    ''' <summary>
    ''' 并发限制器
    ''' </summary>
    Public Class ConcurrencyLimiter
        Implements IDisposable

        Private ReadOnly Semaphore As SemaphoreSlim

        ''' <summary>
        ''' 初始化并发限制器
        ''' </summary>
        ''' <param name="MaxConcurrency">最大并发数</param>
        Public Sub New(MaxConcurrency As Integer)
            Semaphore = New SemaphoreSlim(MaxConcurrency, MaxConcurrency)
        End Sub

        ''' <summary>
        ''' 异步执行
        ''' </summary>
        ''' <param name="t">任务</param>
        ''' <returns></returns>
        Public Async Function ExecuteAsync(t As Task) As Task
            Await Semaphore.WaitAsync()

            Try
                Await t
            Catch ex As Exception
                Logger.Debug("异步执行框架出错 - {0}", ex.Message)
            Finally
                Semaphore.Release()
            End Try
        End Function

        ''' <summary>
        ''' 销毁资源
        ''' </summary>
        Public Sub Dispose() Implements IDisposable.Dispose
            Semaphore.Dispose()
        End Sub

    End Class

    '物理核心数缓存
    Private Shared _PhysicsCores As Integer = -1

    '逻辑核心数缓存
    Private Shared _LogicCores As Integer = -1

    ''' <summary>
    ''' 获取逻辑核心数
    ''' </summary>
    ''' <returns></returns>
    Public Shared Function GetLogicCores() As Integer
        If _LogicCores < 0 Then _LogicCores = Environment.ProcessorCount

        Return _LogicCores
    End Function

    ''' <summary>
    ''' 获取物理核心数
    ''' </summary>
    ''' <returns></returns>
    Public Shared Function GetPhysicalCores() As Integer
        If _PhysicsCores < 0 Then _PhysicsCores = GetPhysicalCoresForce()

        Return _PhysicsCores
    End Function

    ''' <summary>
    ''' 获取物理核心数
    ''' </summary>
    ''' <returns></returns>
    Public Shared Function GetPhysicalCoresForce() As Integer
        Dim Sum As Integer = 0

        Try
            Using searcher As New ManagementObjectSearcher("SELECT NumberOfCores FROM Win32_Processor")
                For Each mo As ManagementObject In searcher.Get()
                    Sum += Convert.ToInt32(mo("NumberOfCores"))
                Next
            End Using
        Catch
            Return 0
        End Try

        Return Sum
    End Function

    Private Shared Function GetCount(Start As Integer, [End] As Integer, [Step] As Integer) As Integer
        Dim count As Integer

        If [Step] > 0 Then
            ' 正步长
            If Start > [End] Then
                count = 0
            Else
                count = CInt(Math.Floor(([End] - Start) / [Step])) + 1
            End If
        ElseIf [Step] < 0 Then
            ' 负步长
            If Start < [End] Then
                count = 0
            Else
                count = CInt(Math.Floor((Start - [End]) / Math.Abs([Step]))) + 1
            End If
        Else
            Return 0
        End If

        Return count
    End Function

    ''' <summary>
    ''' 执行计算
    ''' </summary>
    ''' <param name="DisableAsync">禁用异步执行</param>
    ''' <param name="ConcurrencyMode">并发模式</param>
    ''' <param name="Count">循环数</param>
    ''' <param name="PreSync">前处理过程(同步)</param>
    ''' <param name="Async">处理过程(异步)</param>
    Public Shared Sub Process(DisableAsync As Boolean,
                              ConcurrencyMode As ConcurrencyMode,
                              Count As Integer,
                              PreSync As PreLoopInvoker,
                              Async As LoopInvoker)
        Process(DisableAsync, ConcurrencyMode, Count, PreSync, Async, 0, 1)
    End Sub

    ''' <summary>
    ''' 执行计算
    ''' </summary>
    ''' <param name="DisableAsync">禁用异步执行</param>
    ''' <param name="ConcurrencyMode">并发模式</param>
    ''' <param name="Count">循环数</param>
    ''' <param name="PreSync">前处理过程(同步)</param>
    ''' <param name="Async">处理过程(异步)</param>
    ''' <param name="Start">起始位置</param>
    Public Shared Sub Process(DisableAsync As Boolean,
                              ConcurrencyMode As ConcurrencyMode,
                              Count As Integer,
                              PreSync As PreLoopInvoker,
                              Async As LoopInvoker,
                              Start As Integer)
        Process(DisableAsync, ConcurrencyMode, Count, PreSync, Async, Start, 1)
    End Sub

    ''' <summary>
    ''' 执行计算
    ''' </summary>
    ''' <param name="DisableAsync">禁用异步执行</param>
    ''' <param name="ConcurrencyMode">并发模式</param>
    ''' <param name="Count">循环数</param>
    ''' <param name="PreSync">前处理过程(同步)</param>
    ''' <param name="Async">处理过程(异步)</param>
    ''' <param name="Start">起始位置</param>
    ''' <param name="Step">步长</param>
    Public Shared Sub Process(DisableAsync As Boolean,
                              ConcurrencyMode As ConcurrencyMode,
                              Count As Integer,
                              PreSync As PreLoopInvoker,
                              Async As LoopInvoker,
                              Start As Integer,
                              [Step] As Integer)
        '如果禁用异步执行 则直接转跳
        If DisableAsync Then
            ProcessSync(Count, PreSync, Async, Start, [Step])
            Return
        End If

        '按物理核心数判断
        Dim Cores = GetPhysicalCores()

        If Settings.Settings.Async.AutoSyncThreshold >= 0 AndAlso Cores < Settings.Settings.Async.AutoSyncThreshold Then
            ProcessSync(Count, PreSync, Async, Start, [Step])
        Else
            ProcessAsync(
                If(ConcurrencyMode = ConcurrencyMode.ByLogic, GetLogicCores(), Cores),
                ConcurrencyMode,
                Count,
                PreSync,
                Async,
                Start,
                [Step]
            ).Wait()
        End If
    End Sub

    Private Shared Sub ProcessSync(Count As Integer,
                                   PreSync As PreLoopInvoker,
                                   Async As LoopInvoker,
                                   Start As Integer,
                                   [Step] As Integer)
        For i = Start To Count - 1 Step [Step]
            If PreSync IsNot Nothing Then
                Dim Handled As Boolean = False
                PreSync.Invoke(Handled, i)

                If Handled Then Continue For
            End If

            Async.Invoke(i)
        Next
    End Sub

    Private Shared Async Function ProcessAsync(Cores As Integer,
                                               ConcurrencyMode As ConcurrencyMode,
                                               Count As Integer,
                                               PreSync As PreLoopInvoker,
                                               Async As LoopInvoker,
                                               Start As Integer,
                                               [Step] As Integer) As Task
        If ConcurrencyMode = ConcurrencyMode.NoLimit Then
            Dim Total As Integer = GetCount(Start, Count - 1, [Step])

            If Total > 0 Then
                '数据有效
                Dim Tasks As New List(Of Task)

                For i = Start To Count - 1 Step [Step]
                    If PreSync IsNot Nothing Then
                        Dim Handled As Boolean = False
                        PreSync.Invoke(Handled, i)

                        If Handled Then Continue For
                    End If

                    Dim Index As Integer = i
                    Tasks.Add(Task.Run(Sub() Async.Invoke(Index)))
                Next

                Await Task.WhenAll(Tasks)

                Return
            End If
        End If

        '限制核心数
        Using Scheduler As New ConcurrencyLimiter(Cores)
            Dim Tasks As New List(Of Task)

            For i = Start To Count - 1 Step [Step]
                If PreSync IsNot Nothing Then
                    Dim Handled As Boolean = False
                    PreSync.Invoke(Handled, i)

                    If Handled Then Continue For
                End If

                Dim Index As Integer = i
                Tasks.Add(Scheduler.ExecuteAsync(Task.Run(Sub() Async.Invoke(Index))))
            Next

            Await Task.WhenAll(Tasks)
        End Using
    End Function

    ''' <summary>
    ''' 执行计算
    ''' </summary>
    ''' <param name="DisableAsync">禁用异步执行</param>
    ''' <param name="ConcurrencyMode">并发模式</param>
    ''' <param name="Source">枚举数据源</param>
    ''' <param name="PreSync">前处理过程(同步)</param>
    ''' <param name="Async">处理过程(异步)</param>
    Public Shared Sub Process(Of T)(DisableAsync As Boolean,
                                    ConcurrencyMode As ConcurrencyMode,
                                    Source As IEnumerable(Of T),
                                    PreSync As PreLoopEnumerableInvoker(Of T),
                                    Async As LoopEnumerableInvoker(Of T))

        '如果禁用异步执行 则直接转跳
        If DisableAsync Then
            ProcessEnumerableSync(Source, PreSync, Async)
            Return
        End If

        '按物理核心数判断
        Dim Cores = GetPhysicalCores()

        If Settings.Settings.Async.AutoSyncThreshold >= 0 AndAlso Cores < Settings.Settings.Async.AutoSyncThreshold Then
            '物理核心过少 同步执行 减小异步带来的开销
            ProcessEnumerableSync(Source, PreSync, Async)
        Else
            ProcessEnumerableAsync(
                If(ConcurrencyMode = ConcurrencyMode.ByLogic, GetLogicCores(), Cores),
                ConcurrencyMode,
                Source,
                PreSync,
                Async
            ).Wait()
        End If
    End Sub

    Private Shared Sub ProcessEnumerableSync(Of T)(Source As IEnumerable(Of T),
                                                   PreSync As PreLoopEnumerableInvoker(Of T),
                                                   Async As LoopEnumerableInvoker(Of T))

        For Each e In Source
            If PreSync IsNot Nothing Then
                Dim Handled As Boolean = False
                PreSync.Invoke(Handled, e)

                If Handled Then Continue For
            End If

            Async.Invoke(e)
        Next
    End Sub

    Private Shared Async Function ProcessEnumerableAsync(Of T)(Cores As Integer,
                                                               ConcurrencyMode As ConcurrencyMode,
                                                               Source As IEnumerable(Of T),
                                                               PreSync As PreLoopEnumerableInvoker(Of T),
                                                               Async As LoopEnumerableInvoker(Of T)) As Task
        If ConcurrencyMode = ConcurrencyMode.NoLimit Then
            Dim CurrentContext = SynchronizationContext.Current

            Try
                SynchronizationContext.SetSynchronizationContext(Nothing)

                Dim Tasks As New List(Of Task)

                For Each e In Source
                    If PreSync IsNot Nothing Then
                        Dim Handled As Boolean = False
                        PreSync.Invoke(Handled, e)

                        If Handled Then Continue For
                    End If

                    Tasks.Add(
                        Task.Factory.StartNew(
                            Sub() Async.Invoke(e),
                            CancellationToken.None,
                            TaskCreationOptions.None,
                            TaskScheduler.Default
                        )
                    )
                Next

                Await Task.WhenAll(Tasks)
            Finally
                SynchronizationContext.SetSynchronizationContext(CurrentContext)
            End Try
        Else
            Dim CurrentContext = SynchronizationContext.Current

            Try
                SynchronizationContext.SetSynchronizationContext(Nothing)

                Using Scheduler As New ConcurrencyLimiter(Cores)
                    Dim Tasks As New List(Of Task)

                    For Each e In Source
                        If PreSync IsNot Nothing Then
                            Dim Handled As Boolean = False
                            PreSync.Invoke(Handled, e)

                            If Handled Then Continue For
                        End If

                        Tasks.Add(
                            Scheduler.ExecuteAsync(
                                Task.Factory.StartNew(
                                    Sub() Async.Invoke(e),
                                    CancellationToken.None,
                                    TaskCreationOptions.None,
                                    TaskScheduler.Default
                                )
                            )
                        )
                    Next

                    Await Task.WhenAll(Tasks)
                End Using
            Finally
                SynchronizationContext.SetSynchronizationContext(CurrentContext)
            End Try
        End If
    End Function

End Class
