Namespace KWeb.State

    Friend Class PlayerLoadedEvent
        Inherits PlayerEvent

        Private ReadOnly Manager As StateManager

        Private ReadOnly K As EasyK

        ''' <summary>
        ''' 初始化事件
        ''' </summary>
        ''' <param name="Manager">状态管理器</param>
        ''' <param name="K"></param>
        Public Sub New(Manager As StateManager, K As EasyK)
            MyBase.New(PlayerEventType.Loaded)
            Me.Manager = Manager
            Me.K = K
        End Sub

        Protected Overrides Function GetData() As String
            Dim Player As New PlayerState(K, Manager.State)
            Return Player.ToString()
        End Function

    End Class

End Namespace
