Imports Newtonsoft.Json

Namespace KWeb.State

    <Serializable>
    Friend Class ServerState

        <JsonProperty("media", NullValueHandling:=NullValueHandling.Include)>
        Public ReadOnly Property Media As EasyKBookRecord

        <JsonProperty("player")>
        Public ReadOnly Property Player As PlayerState

        ''' <summary>
        ''' 初始化数据对象
        ''' </summary>
        ''' <param name="K"></param>
        ''' <param name="State">播放器状态</param>
        Public Sub New(K As EasyK, State As PlayState)
            With K
                Media = .GetCurrent()
                Player = New PlayerState(State, .Player.Position, .Player.Duration, .Player.Rate)
            End With

            Logger.Debug(JsonConvert.SerializeObject(Me, Formatting.Indented))
        End Sub

        Public Overrides Function ToString() As String
            Return JsonConvert.SerializeObject(Me)
        End Function

        ''' <summary>
        ''' 生成Json字符串
        ''' </summary>
        ''' <param name="K"></param>
        ''' <param name="State">播放器状态</param>
        ''' <returns></returns>
        Public Shared Function GenerateJson(K As EasyK, State As PlayState) As String
            Return New ServerState(K, State).ToString()
        End Function

    End Class

End Namespace
