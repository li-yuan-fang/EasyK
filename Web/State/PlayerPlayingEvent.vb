Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports Newtonsoft.Json.Serialization

Namespace KWeb.State

    Friend Class PlayerPlayingEvent
        Inherits PlayerEvent

        Private Shared ReadOnly Serializer = JsonSerializer.Create(New JsonSerializerSettings With {
            .ContractResolver = New CamelCasePropertyNamesContractResolver()
        })

        Private ReadOnly Manager As StateManager

        Private ReadOnly K As EasyK

        ''' <summary>
        ''' 初始化事件
        ''' </summary>
        ''' <param name="Manager">状态管理器</param>
        ''' <param name="K"></param>
        Public Sub New(Manager As StateManager, K As EasyK)
            MyBase.New(PlayerEventType.Playing)
            Me.Manager = Manager
            Me.K = K
        End Sub

        Protected Overrides Function GetData() As String
            Dim Data = JObject.FromObject(New PlayerState(K, Manager.State), Serializer)
            With Data
                .Remove("duration")
                Return .ToString(Formatting.None)
            End With
        End Function

    End Class

End Namespace
