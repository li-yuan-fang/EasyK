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
    Public Property AllowAccompaniment As Boolean = False

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
