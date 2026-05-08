Imports System.Threading
Imports NAudio.Dsp
Imports NAudio.Wave

Namespace Accompaniment

    Public MustInherit Class STFTAccompaniment

        '常量
        Protected Const FFT_Size As Integer = 2048

        Protected Const Hop_Size As Integer = FFT_Size \ 4

        Protected Const Overlap_Size As Integer = FFT_Size - Hop_Size

        Protected Shared ReadOnly FFT_Pow As Integer = CInt(Math.Log(FFT_Size, 2))

        '窗
        Protected ReadOnly Window As Single()

        '采样率
        Protected ReadOnly SampleRate As Integer

        ' 缓冲区
        Protected ReadOnly _inputBuffer As Single()     ' 输入缓冲区
        Protected ReadOnly _outputBuffer As Single()    ' 输出缓冲区（重叠相加）
        Protected ReadOnly _overlapBuffer As Single()()   ' 重叠缓冲区

        ' 状态
        Protected _inputBufferPos As Integer = 0
        Protected _outputBufferPos As Integer = 0
        Protected _outputBufferFilled As Integer = 0
        Protected _isFirstFrame As Boolean = True

        '声道数
        Protected ReadOnly Channels As Integer

        '声道角色
        Protected ReadOnly ChannelRoles As List(Of ChannelRole)

        '中置声道索引
        Protected ReadOnly CenterChannelIndices As List(Of Integer)

        '可配对的侧面声道
        Protected ReadOnly SideChannelPairs As List(Of Tuple(Of Integer, Integer))

        '衰减系数
        Protected _ReductionFactor As Single

        ''' <summary>
        ''' 获取或设置衰减系数
        ''' </summary>
        ''' <returns></returns>
        Public Property ReductionFactor As Single
            Get
                Return _ReductionFactor
            End Get
            Set(value As Single)
                _ReductionFactor = Math.Max(0.0F, value)
            End Set
        End Property

        ''' <summary>
        ''' 构造函数
        ''' </summary>
        ''' <param name="WaveFormat">音频格式</param>
        ''' <param name="WindowType">窗函数类型</param>
        Protected Sub New(WaveFormat As WaveFormat, WindowType As STFTWindowType)
            Channels = WaveFormat.Channels
            SampleRate = WaveFormat.SampleRate

            ' 生成窗函数
            Window = GenerateWindow(FFT_Size, WindowType)

            '声道配置
            ChannelRoles = ChannelUtils.MapChannels(Channels)
            CenterChannelIndices = ChannelUtils.GetCenterChannelIndices(ChannelRoles)
            SideChannelPairs = ChannelUtils.GetSideChannelPairs(ChannelRoles)

            ' 初始化缓冲区
            Dim FFT_Step As Integer = FFT_Size * Channels
            _inputBuffer = New Single(FFT_Step - 1) {}
            _outputBuffer = New Single(FFT_Step - 1) {}

            _overlapBuffer = New Single(Channels - 1)() {}
            For ch = 0 To Channels - 1
                _overlapBuffer(ch) = New Single(FFT_Size - 1) {}
                Array.Clear(_overlapBuffer(ch), 0, FFT_Size)
            Next
        End Sub

        ''' <summary>
        ''' 生成窗函数
        ''' </summary>
        Public Shared Function GenerateWindow(size As Integer, type As STFTWindowType) As Single()
            Dim window(size - 1) As Single

            For i As Integer = 0 To size - 1
                Select Case type
                    Case STFTWindowType.Hann
                        ' Hann窗：0.5 * (1 - cos(2πn/(N-1)))
                        window(i) = 0.5F * (1.0F - CSng(Math.Cos(2.0 * Math.PI * i / (size - 1))))

                    Case STFTWindowType.Hamming
                        ' Hamming窗：0.54 - 0.46 * cos(2πn/(N-1))
                        window(i) = 0.54F - 0.46F * CSng(Math.Cos(2.0 * Math.PI * i / (size - 1)))

                    Case STFTWindowType.Blackman
                        ' Blackman窗
                        window(i) = 0.42F - 0.5F * CSng(Math.Cos(2.0 * Math.PI * i / (size - 1))) +
                                   0.08F * CSng(Math.Cos(4.0 * Math.PI * i / (size - 1)))

                    Case STFTWindowType.Rectangular
                        ' 矩形窗
                        window(i) = 1.0F

                    Case Else
                        window(i) = 1.0F
                End Select
            Next

            Return window
        End Function

        ''' <summary>
        ''' 执行STFT和逆变换
        ''' </summary>
        Protected Sub PerformSTFT()
            Dim FFTStep As Integer = FFT_Size * Channels
            Dim fft As New List(Of Complex())

            '前处理
            Dim Countdown As New CountdownEvent(Channels)
            For ch = 0 To Channels - 1
                Dim f = New Complex(FFT_Size - 1) {}
                Dim id = ch
                fft.Add(f)

                Task.Run(Sub()
                             Dim j = 0
                             For i As Integer = id To FFTStep - 1 Step Channels
                                 With f(j)
                                     .X = _inputBuffer(i) * Window(j)
                                     .Y = 0
                                 End With
                                 j += 1
                             Next

                             FastFourierTransform.FFT(True, FFT_Pow, f)

                             Countdown.Signal()
                         End Sub)
            Next

            Countdown.Wait()

            '清除人声
            Progress(fft)

            '后处理
            Countdown.Dispose()
            Countdown = New CountdownEvent(Channels)

            For ch = 0 To Channels - 1
                Dim id = ch

                Task.Run(Sub()
                             Dim f = fft(id)

                             FastFourierTransform.FFT(False, FFT_Pow, f)

                             ' 重叠相加合成
                             For i As Integer = 0 To FFT_Size - 1
                                 Dim windowedSample As Single = f(i).X * Window(i)

                                 If _isFirstFrame Then
                                     ' 第一帧直接写入
                                     _overlapBuffer(id)(i) = windowedSample
                                 Else
                                     ' 后续帧进行重叠相加
                                     _overlapBuffer(id)(i) += windowedSample
                                 End If
                             Next

                             '将前hopSize个样本复制到输出缓冲区
                             For i = 0 To Hop_Size - 1
                                 _outputBuffer(i * Channels + id) = _overlapBuffer(id)(i)
                             Next

                             ' 移动重叠缓冲区
                             ' 将剩余数据移到前面，为下一帧做准备
                             Array.Copy(_overlapBuffer(id), Hop_Size, _overlapBuffer(id), 0, Overlap_Size)

                             ' 清空新移动区域的后部（避免残留数据影响）
                             For i As Integer = Overlap_Size To FFT_Size - 1
                                 _overlapBuffer(id)(i) = 0.0F
                             Next

                             Countdown.Signal()
                         End Sub)
            Next

            Countdown.Wait()

            _outputBufferFilled = Hop_Size * Channels
            _outputBufferPos = 0

            _isFirstFrame = False
        End Sub

        ''' <summary>
        ''' 重置处理器状态
        ''' </summary>
        Public Overridable Sub Reset()
            _inputBufferPos = 0
            _outputBufferPos = 0
            _outputBufferFilled = 0
            _isFirstFrame = True

            Array.Clear(_inputBuffer, 0, _inputBuffer.Length)
            Array.Clear(_outputBuffer, 0, _outputBuffer.Length)

            For ch = 0 To Channels - 1
                _overlapBuffer(ch) = New Single(FFT_Size - 1) {}
                Array.Clear(_overlapBuffer(ch), 0, FFT_Size)
            Next
        End Sub


        ''' <summary>
        ''' 并行人声消音处理
        ''' </summary>
        ''' <param name="fft">波形</param>
        Protected Sub Progress(fft As List(Of Complex()))
            Dim Countdown As New CountdownEvent(SideChannelPairs.Count + CenterChannelIndices.Count)

            For Each Side In SideChannelPairs
                Task.Run(Sub()
                             ProcessPairVocalRemoval(fft(Side.Item1), fft(Side.Item2))
                             Countdown.Signal()
                         End Sub)
            Next

            For Each Central In CenterChannelIndices
                Task.Run(Sub()
                             AttenuateCenterChannel(fft(Central))
                             Countdown.Signal()
                         End Sub)
            Next

            Countdown.Wait()
        End Sub

        ''' <summary>
        ''' 对称声道消音处理
        ''' </summary>
        ''' <param name="fft1">声道1</param>
        ''' <param name="fft2">声道2</param>
        Protected Sub ProcessPairVocalRemoval(ByRef fft1 As Complex(), ByRef fft2 As Complex())
            For k As Integer = 0 To FFT_Size \ 2
                '计算幅度和相位
                Dim mag1 As Double = Magnitude(fft1(k))
                Dim mag2 As Double = Magnitude(fft2(k))

                If mag1 < 0.0001 OrElse mag2 < 0.0001 Then Continue For

                Dim phase1 As Double = Phase(fft1(k))
                Dim phase2 As Double = Phase(fft2(k))

                '相干性分析
                Dim magRatio As Double = Math.Min(mag1, mag2) / Math.Max(mag1, mag2)
                Dim phaseDiff As Double = Math.Abs(phase1 - phase2)
                If phaseDiff > Math.PI Then phaseDiff = 2 * Math.PI - phaseDiff

                Dim coherence As Double = magRatio * (1 - phaseDiff / Math.PI)
                coherence = Math.Max(0, Math.Min(1, coherence))

                '频率
                Dim freq As Double = k * SampleRate / FFT_Size

                '计算局部对比度
                Dim contrast = ComputeLocalContrast(k, fft1) ' 使用左声道或平均

                '根据局部对比度调整（关键：合唱场景通常有多个峰值）
                '对比度高 = 频谱稀疏 = 可能是独立声源，降低阈值（更容易保留）
                '对比度低 = 频谱密集 = 可能是混叠，提高阈值（更严格消除）
                Dim contrastFactor = 1.0 - (contrast * 0.3) ' 对比度0-1，调整范围±0.3

                Dim dynamicThreshold = contrastFactor * GetFrequencyAdaptiveThreshold(freq)

                If coherence > dynamicThreshold Then
                    Dim attenuation As Double = GetVocalFrequencyWeight(freq)
                    attenuation *= coherence

                    '中置/侧向分解
                    Dim centerX As Double = (fft1(k).X + fft2(k).X) * 0.5
                    Dim centerY As Double = (fft1(k).Y + fft2(k).Y) * 0.5
                    Dim sideX As Double = (fft1(k).X - fft2(k).X) * 0.5
                    Dim sideY As Double = (fft1(k).Y - fft2(k).Y) * 0.5

                    '衰减中置（人声），保留侧向（伴奏）
                    Dim att As Double = Math.Max(1 - attenuation * _ReductionFactor, 0)
                    centerX *= att
                    centerY *= att

                    fft1(k).X = CSng(centerX + sideX)
                    fft1(k).Y = CSng(centerY + sideY)
                    fft2(k).X = CSng(centerX - sideX)
                    fft2(k).Y = CSng(centerY - sideY)

                    '二次振幅衰减
                    mag1 = Magnitude(fft1(k)) * att
                    mag2 = Magnitude(fft2(k)) * att
                    phase1 = Phase(fft1(k))
                    phase2 = Phase(fft2(k))

                    With fft1(k)
                        .X = mag1 * Math.Cos(phase1)
                        .Y = mag1 * Math.Sin(phase1)
                    End With
                    With fft2(k)
                        .X = mag2 * Math.Cos(phase2)
                        .Y = mag2 * Math.Sin(phase2)
                    End With

                    '共轭对称
                    If k > 0 AndAlso k < FFT_Size \ 2 Then
                        Dim mirror As Integer = FFT_Size - k
                        fft1(mirror).X = fft1(k).X
                        fft1(mirror).Y = -fft1(k).Y
                        fft2(mirror).X = fft2(k).X
                        fft2(mirror).Y = -fft2(k).Y
                    End If
                End If
            Next
        End Sub

        ''' <summary>
        ''' 中置声道消音处理
        ''' </summary>
        ''' <param name="fft">声道</param>
        Protected Sub AttenuateCenterChannel(ByRef fft As Complex())
            For k As Integer = 0 To FFT_Size \ 2
                Dim freq As Double = k * SampleRate / FFT_Size
                ' 中置声道通常包含清晰人声，进行轻度宽频衰减
                If freq >= 1000 AndAlso freq <= 6000 Then
                    Dim attenuation As Single = Math.Max(1 - GetVocalFrequencyWeight(freq) * _ReductionFactor, 0)
                    Dim mag = Magnitude(fft(k)) * attenuation
                    Dim p = Phase(fft(k))
                    With fft(k)
                        .X = mag * Math.Cos(p)
                        .Y = mag * Math.Sin(p)
                    End With

                    If k > 0 AndAlso k < FFT_Size \ 2 Then
                        Dim i = FFT_Size - k

                        mag = Magnitude(fft(i)) * attenuation
                        p = Phase(fft(i))

                        With fft(i)
                            .X = mag * Math.Cos(p)
                            .Y = mag * Math.Sin(p)
                        End With
                    End If
                End If
            Next
        End Sub

        ''' <summary>
        ''' 计算人声权重
        ''' </summary>
        ''' <param name="freq">频率</param>
        ''' <returns></returns>
        Protected Shared Function GetVocalFrequencyWeight(freq As Single) As Single
            ' 人声基频范围：男声80-250Hz，女声200-400Hz
            ' 人声泛音：最高到4000-5000Hz

            Select Case freq
                Case < 80
                    Return 0.2F   ' 极低频，不太可能人声
                Case 80 To 250
                    Return 0.9F   ' 男声基频
                Case 250 To 500
                    Return 1.0F   ' 女声基频+男声泛音
                Case 500 To 2000
                    Return 0.95F  ' 人声主体（最重要频段）
                Case 2000 To 4000
                    Return 0.85F  ' 人声清晰度频段
                Case 4000 To 8000
                    Return 0.5F   ' 嘶嘶声，可能是人声也可能是镲片
                Case Else
                    Return 0.3F   ' 极高频，基本不是人声
            End Select
        End Function

        ''' <summary>
        ''' 计算频率自适应的基础阈值
        ''' </summary>
        Protected Shared Function GetFrequencyAdaptiveThreshold(freq As Double) As Double
            ' 人耳对不同频率的相位敏感度不同
            ' 中频(1-4kHz)最敏感，低频和高频容忍度更高

            Select Case freq
                Case < 80
                    Return 0.8
                Case 80 To 250
                    ' 低频：波长较长，房间反射导致相位混乱，提高阈值（更严格）
                    Return 0.75
                Case 250 To 500
                    Return 0.7
                Case 500 To 1000
                    ' 中低频：男声基频区，适度严格
                    Return 0.65
                Case 1000 To 4000
                    ' 中频：人声清晰度区，人耳最敏感，降低阈值（更容易识别为相干）
                    Return 0.5
                Case 4000 To 8000
                    ' 高频：泛音区，相位不稳定，提高阈值
                    Return 0.7
                Case Else
                    ' 极高频
                    Return 0.8
            End Select
        End Function

        ''' <summary>
        ''' 计算局部频谱对比度（Scharr或简单差分）
        ''' </summary>
        Protected Shared Function ComputeLocalContrast(bin As Integer, fft As Complex()) As Double
            If bin <= 1 OrElse bin >= FFT_Size \ 2 - 1 Then Return 0.5

            Dim magCenter = Magnitude(fft(bin))
            Dim magLeft = Magnitude(fft(bin - 1))
            Dim magRight = Magnitude(fft(bin + 1))
            Dim magFarLeft = Magnitude(fft(bin - 2))
            Dim magFarRight = Magnitude(fft(bin + 2))

            ' 局部方差归一化
            Dim localMean = (magFarLeft + magLeft + magCenter + magRight + magFarRight) / 5
            If localMean < 0.0001 Then Return 0

            Dim variance = ((magFarLeft - localMean) ^ 2 + (magLeft - localMean) ^ 2 +
                   (magCenter - localMean) ^ 2 + (magRight - localMean) ^ 2 +
                   (magFarRight - localMean) ^ 2) / 5

            ' 对比度 = 标准差/均值（变异系数）
            Dim contrast = Math.Sqrt(variance) / localMean

            ' 归一化到0-1
            Return Math.Min(1.0, contrast / 2.0) ' 假设2.0为最大合理CV
        End Function

        ''' <summary>
        ''' 计算复数幅度
        ''' </summary>
        Protected Shared Function Magnitude(c As Complex) As Single
            Return Math.Sqrt(c.X * c.X + c.Y * c.Y)
        End Function

        ''' <summary>
        ''' 计算复数相位
        ''' </summary>
        Protected Shared Function Phase(c As Complex) As Single
            Return Math.Atan2(c.Y, c.X)
        End Function

    End Class

End Namespace
