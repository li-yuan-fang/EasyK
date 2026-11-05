Imports Newtonsoft.Json

Namespace KWeb.State

    Friend Class PlayerPushEvent
        Inherits PlayerEvent

        Private ReadOnly K As EasyK

        Private ReadOnly Clear As Boolean

        ''' <summary>
        ''' 初始化事件
        ''' </summary>
        ''' <param name="K"></param>
        Public Sub New(K As EasyK)
            Me.New(K, False)
        End Sub

        ''' <summary>
        ''' 初始化事件
        ''' </summary>
        ''' <param name="K"></param>
        ''' <param name="Clear">清空模式</param>
        Public Sub New(K As EasyK, Clear As Boolean)
            MyBase.New(PlayerEventType.Push)
            Me.K = K
            Me.Clear = Clear
        End Sub

        Protected Overrides Function GetData() As String
            Return JsonConvert.SerializeObject(If(Clear, Nothing, K.GetCurrent()))
        End Function

    End Class

End Namespace
