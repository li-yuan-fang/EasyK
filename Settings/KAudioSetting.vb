Imports Newtonsoft.Json

<Serializable>
Public Class KAudioSetting

    <JsonProperty("dummy_audio")>
    Public Property DummyAudio As Boolean = True

    <JsonProperty("allow_update_sys_vol")>
    Public Property AllowUpdateSystemVolume As Boolean = True

    <JsonProperty("allow_accompaniment")>
    Public Property AllowAccompaniment As Boolean = False

    <JsonProperty("use_fourier_transform")>
    Public Property UseFourierTransform As Boolean = True

    <JsonProperty("accompaniment_reduction_factor")>
    Public Property AccompanimentReductionFactor As Single = 0.9

    <JsonProperty("auto_reset_accompaniment")>
    Public Property AutoResetAccompaniment As Boolean = True

    <JsonIgnore>
    Friend ReadOnly Property IsDummyAudio As Boolean
        Get
            Return DummyAudio OrElse AllowAccompaniment
        End Get
    End Property

End Class
