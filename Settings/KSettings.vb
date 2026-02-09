Imports System.Windows.Forms
Imports Newtonsoft.Json

<Serializable>
Public Class KSettings

    <JsonProperty("debug")>
    Public Property DebugMode As Boolean = False

    <JsonProperty("temp_folder")>
    Public Property TempFolder As String = "temp"

    <JsonProperty("keep_login")>
    Public Property KeepLogin As Boolean = True

    <JsonProperty("audio")>
    Public Property Audio As KAudioSetting = New KAudioSetting()

    <JsonProperty("web")>
    Public Property Web As KWebSettings = New KWebSettings()

    <JsonProperty("dlna")>
    Public Property DLNA As DLNASettings = New DLNASettings()

    <JsonProperty("plugins")>
    Public Property Plugins As Dictionary(Of String, String) = New Dictionary(Of String, String)

    <JsonProperty("plugin_common")>
    Public Property PluginCommon As Dictionary(Of String, Object) = New Dictionary(Of String, Object) From {
        {"kana", True},
        {"translated", True},
        {"roma", False}
    }

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
