Imports Newtonsoft.Json

Namespace KWeb.State

    <Serializable>
    Friend Class PlayerState

        <JsonProperty("state")>
        Public ReadOnly Property State As PlayState

        <JsonProperty("current")>
        Public ReadOnly Property Position As Single

        <JsonProperty("duration")>
        Public ReadOnly Property Duration As Double

        <JsonProperty("rate")>
        Public ReadOnly Property Rate As Single

        ''' <summary>
        ''' 初始化数据对象
        ''' </summary>
        ''' <param name="State">播放状态</param>
        ''' <param name="Position">当前位置</param>
        ''' <param name="Duration">总时长</param>
        ''' <param name="Rate">播放速率</param>
        Public Sub New(State As PlayState, Position As Single, Duration As Double, Rate As Single)
            Me.State = State
            Me.Position = Position
            Me.Duration = Duration
            Me.Rate = Rate
        End Sub

        ''' <summary>
        ''' 初始化数据对象
        ''' </summary>
        ''' <param name="K"></param>
        ''' <param name="State">播放器状态</param>
        Public Sub New(K As EasyK, State As PlayState)
            Me.New(State, K.Player.Position, K.Player.Duration, K.Player.Rate)
        End Sub

        Public Overrides Function ToString() As String
            Return JsonConvert.SerializeObject(Me)
        End Function

    End Class

End Namespace
