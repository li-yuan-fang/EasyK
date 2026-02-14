Imports Newtonsoft.Json

Public Class JsonUtils

    ''' <summary>
    ''' 安全解析Json
    ''' </summary>
    ''' <typeparam name="T">反序列化类型</typeparam>
    ''' <param name="Text"></param>
    ''' <returns></returns>
    Public Shared Function SafeDeserializeObject(Of T)(Text As String) As T
        Try
            Return JsonConvert.DeserializeObject(Of T)(Text)
        Catch
            Return Nothing
        End Try
    End Function

End Class
