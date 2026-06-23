Imports Newtonsoft.Json

<Serializable>
Public Class AsyncSettings

    ''' <summary>
    ''' 完全同步模式
    ''' </summary>
    ''' <remarks>完全禁用异步计算(核心数较少的设备推荐)</remarks>
    ''' <returns></returns>
    <JsonProperty("completely_sync")>
    Public Property CompletelySync As Boolean = False

    ''' <summary>
    ''' 自动同步核心数阈值
    ''' </summary>
    ''' <remarks>核心数低于该数值则自动采用同步计算(如设置为负数则完全采用异步计算)</remarks>
    ''' <returns></returns>
    <JsonProperty("auto_sync_threshold")>
    Public Property AutoSyncThreshold As Integer = 4

    ''' <summary>
    ''' 异步计算并发模式
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("async_mode")>
    Public Property AsyncMode As AsyncUtils.ConcurrencyMode = AsyncUtils.ConcurrencyMode.NoLimit

End Class
