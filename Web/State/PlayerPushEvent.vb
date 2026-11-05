Imports Newtonsoft.Json

Namespace KWeb.State

    Friend Class PlayerPushEvent
        Inherits PlayerEvent

        Private ReadOnly K As EasyK

        ''' <summary>
        ''' 初始化事件
        ''' </summary>
        ''' <param name="K"></param>
        Public Sub New(K As EasyK)
            MyBase.New(PlayerEventType.Push)
            Me.K = K
        End Sub

        Protected Overrides Function GetData() As String
            Return JsonConvert.SerializeObject(K.GetCurrent())
        End Function

    End Class

End Namespace
