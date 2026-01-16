Imports Newtonsoft.Json

<Serializable>
Public Class UploadSettings

    <JsonProperty("chunk_size")>
    Public Property ChunkSize As Integer = 1 * 1024 * 1024

    <JsonProperty("expire")>
    Public Property ExpireDuration As Long = 2 * 60 * 10 ^ 7

    <JsonProperty("clean_interval")>
    Public Property CleanDuration As Integer = 10 * 10

    <JsonProperty("flush_count")>
    Public Property RefreshThreshold As Integer = 10

End Class
