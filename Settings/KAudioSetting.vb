Imports Newtonsoft.Json

<Serializable>
Public Class KAudioSetting

    <JsonProperty("dummy_audio")>
    Public Property DummyAudio As Boolean = True

    <JsonProperty("allow_update_sys_vol")>
    Public Property AllowUpdateSystemVolume As Boolean = True

    <JsonProperty("allow_accompaniment")>
    Public Property AllowAccompaniment As Boolean = False

    <JsonIgnore>
    Friend ReadOnly Property IsDummyAudio As Boolean
        Get
            Return DummyAudio OrElse AllowAccompaniment
        End Get
    End Property

End Class
