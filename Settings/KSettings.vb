Imports System.Windows.Forms
Imports Newtonsoft.Json

<Serializable>
Public Class KSettings

    <JsonProperty("debug")>
    Public Property DebugMode As Boolean = False

    <JsonProperty("temp_folder")>
    Public Property TempFolder As String = "temp"

    <JsonProperty("allow_volume")>
    Public Property AllowVolumeUpdate As Boolean = True

    <JsonProperty("web")>
    Public Property Web As KWebSettings = New KWebSettings()

    <JsonProperty("dlna")>
    Public Property DLNA As DLNASettings = New DLNASettings()

    <JsonProperty("clean_exit")>
    Public Property CleanOnExit As Boolean = True

    ''' <summary>
    ''' 创建或打开配置文件
    ''' </summary>
    ''' <returns></returns>
    Public Shared Function CreateSettings() As KSettings
        Dim Path As String = IO.Path.Combine(Application.StartupPath, "configs.json")
        If IO.File.Exists(Path) Then
            Try
                Return JsonConvert.DeserializeObject(Of KSettings)(IO.File.ReadAllText(Path, Text.Encoding.UTF8))
            Catch ex As Exception
                Console.WriteLine("读取配置文件失败 - {0}", ex.Message)
            End Try
        End If

        Return New KSettings()
    End Function

    ''' <summary>
    ''' 保存配置文件
    ''' </summary>
    Public Sub Save()
        Dim Json As String = JsonConvert.SerializeObject(Me, Formatting.Indented)
        Dim Path As String = IO.Path.Combine(Application.StartupPath, "configs.json")
        Try
            IO.File.WriteAllText(Path, Json, Text.Encoding.UTF8)
        Catch ex As Exception
            Console.WriteLine("保存配置文件失败 - {0}", ex.Message)
        End Try
    End Sub

End Class
