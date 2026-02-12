Imports Newtonsoft.Json

<Serializable>
Public Class UploadSettings

    ''' <summary>
    ''' 上传分块大小(单位:byte)
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("chunk_size")>
    Public Property ChunkSize As Integer = 1 * 1024 * 1024

    ''' <summary>
    ''' 上传会话有效期(单位:tick)
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("expire")>
    Public Property ExpireDuration As Long = 2 * 60 * 10 ^ 7

    ''' <summary>
    ''' 缓存清理间隔(单位:ms)
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("clean_interval")>
    Public Property CleanDuration As Integer = 10 * 1000

    ''' <summary>
    ''' 提交计数阈值
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("flush_count")>
    Public Property RefreshThreshold As Integer = 10

End Class
