Imports Newtonsoft.Json

<Serializable>
Public Class KAudioSetting

    ''' <summary>
    ''' 使用托管音频
    ''' </summary>
    ''' <remarks>如需禁用托管音频 需要同时禁用DummyAudio和AllowAccompaniment</remarks>
    ''' <returns></returns>
    <JsonProperty("dummy_audio")>
    Public Property DummyAudio As Boolean = True

    ''' <summary>
    ''' 允许更改系统音量
    ''' </summary>
    ''' <remarks>只有不使用托管音频时会用到</remarks>
    ''' <returns></returns>
    <JsonProperty("allow_update_sys_vol")>
    Public Property AllowUpdateSystemVolume As Boolean = True

    ''' <summary>
    ''' 允许使用实时伴唱功能
    ''' </summary>
    ''' <returns></returns>
    <JsonProperty("allow_accompaniment")>
    Public Property AllowAccompaniment As Boolean = True

    ''' <summary>
    ''' 使用傅里叶变换运算实时伴唱
    ''' </summary>
    ''' <remarks>当体感延迟较大时可以禁用</remarks>
    ''' <returns></returns>
    <JsonProperty("use_fourier_transform")>
    Public Property UseFourierTransform As Boolean = True

    ''' <summary>
    ''' 实时伴唱人声衰减系数
    ''' </summary>
    ''' <remarks>数值越大衰减越多</remarks>
    ''' <returns></returns>
    <JsonProperty("accompaniment_reduction_factor")>
    Public Property AccompanimentReductionFactor As Single = 0.9F

    ''' <summary>
    ''' 自动关闭伴唱
    ''' </summary>
    ''' <remarks>即切歌时自动关闭伴唱</remarks>
    ''' <returns></returns>
    <JsonProperty("auto_reset_accompaniment")>
    Public Property AutoResetAccompaniment As Boolean = True

    ''' <summary>
    ''' 傅里叶变换响度增益
    ''' </summary>
    ''' <remarks>实时伴奏模式下使用傅里叶变换法时的响度增益</remarks>
    ''' <returns></returns>
    <JsonProperty("fourier_magnitude_gain")>
    Public Property FourierMagnitudeGain As Double = 0.01D

    ''' <summary>
    ''' 伴奏音量增益
    ''' </summary>
    ''' <remarks>实时伴奏模式下的音量增益</remarks>
    ''' <returns></returns>
    <JsonProperty("accompaniment_volume_gain")>
    Public Property AccompanimentVolumeGain As Single = 0.05F

    ''' <summary>
    ''' 是否使用WASAPI
    ''' </summary>
    ''' <remarks>默认采用DirectsoundOut以获得更好的性能表现 但是切换音频设备时小概率可能会触发Bug 所以保留WASAPI备用</remarks>
    ''' <returns></returns>
    <JsonProperty("dummy_use_wasapi")>
    Public Property DummyUseWasApi As Boolean = False

    ''' <summary>
    ''' WASAPI模式下是否开启同步处理事件
    ''' </summary>
    ''' <remarks>低性能设备不建议开启</remarks>
    ''' <returns></returns>
    <JsonProperty("wasapi_sync_event")>
    Public Property WasApiSyncEvent As Boolean = False

    ''' <summary>
    ''' WASAPI延迟(单位:ms)
    ''' </summary>
    ''' <remarks>一般不需要动</remarks>
    ''' <returns></returns>
    <JsonProperty("wasapi_latency")>
    Public Property WasApiLatency As Integer = 200

    ''' <summary>
    ''' 获取托管音频启用状态
    ''' </summary>
    ''' <returns></returns>
    <JsonIgnore>
    Friend ReadOnly Property IsDummyAudio As Boolean
        Get
            Return DummyAudio OrElse AllowAccompaniment
        End Get
    End Property

End Class
