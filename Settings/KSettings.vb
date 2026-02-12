Imports System.Windows.Forms
Imports Newtonsoft.Json

<Serializable>
Public Class KSettings

    ''' <summary>
    ''' Debug模式
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("debug")>
    Public Property DebugMode As Boolean = False

    ''' <summary>
    ''' 视频缓存目录
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("temp_folder")>
    Public Property TempFolder As String = "temp"

    ''' <summary>
    ''' 保持登录(主要是bilibili)
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("keep_login")>
    Public Property KeepLogin As Boolean = True

    ''' <summary>
    ''' 部署后自动显示二维码
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("auto_show_qr")>
    Public Property AutoShowQR As Boolean = True

    ''' <summary>
    ''' 音频设置
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("audio")>
    Public Property Audio As KAudioSetting = New KAudioSetting()

    ''' <summary>
    ''' Web服务器设置
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("web")>
    Public Property Web As KWebSettings = New KWebSettings()

    ''' <summary>
    ''' DLNA设置
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("dlna")>
    Public Property DLNA As DLNASettings = New DLNASettings()

    ''' <summary>
    ''' 插件设置
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("plugins")>
    Public Property Plugins As Dictionary(Of String, String) = New Dictionary(Of String, String)

    ''' <summary>
    ''' 插件通用设置
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("plugin_common")>
    Public Property PluginCommon As Dictionary(Of String, Object) = New Dictionary(Of String, Object) From {
        {"kana", True},             '显示假名
        {"translated", True},       '显示翻译
        {"roma", False}             '显示罗马音
    }

    ''' <summary>
    ''' 退出时自动清理缓存
    ''' </summary>
    ''' <returns></returns>
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
