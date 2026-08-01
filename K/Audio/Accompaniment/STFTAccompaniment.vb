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

        '频率表
        Protected ReadOnly FreqTable(FFT_Size - 1) As Double

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

        'Channels*FFTSize
        Protected ReadOnly FFTStep As Integer

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

            '计算频率表
            For i = 0 To FFT_Size - 1
                FreqTable(i) = SampleRate * i / FFT_Size
            Next

            ' 生成窗函数
            Window = GenerateWindow(FFT_Size, WindowType)

            '声道配置
            ChannelRoles = ChannelUtils.MapChannels(Channels)
            CenterChannelIndices = ChannelUtils.GetCenterChannelIndices(ChannelRoles)
            SideChannelPairs = ChannelUtils.GetSideChannelPairs(ChannelRoles)

            '掩膜
            _smoothedMasks = CreateHalfBinBuffers(SideChannelPairs.Count)

            ' 初始化缓冲区
            FFTStep = FFT_Size * Channels
            _inputBuffer = New Single(FFTStep - 1) {}
            _outputBuffer = New Single(FFTStep - 1) {}

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
                        ' Hann窗
                        'Symmetric Hann
                        'window(i) = 0.5F * (1.0F - CSng(Math.Cos(2.0 * Math.PI * i / (size - 1))))

                        'Periodic Hann
                        window(i) = 0.5F * (1.0F - CSng(Math.Cos(2.0 * Math.PI * i / size)))

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
            Dim fft As New List(Of Complex())

            '前处理
            AsyncUtils.Process(
                Settings.Settings.Async.CompletelySync,
                Settings.Settings.Async.AsyncMode,
                Channels,
                Sub(ByRef h, i) fft.Add(New Complex(FFT_Size - 1) {}),
                Sub(ch)
                    Dim f As Complex() = fft(ch)
                    Dim j As Integer = 0

                    For i As Integer = ch To FFTStep - 1 Step Channels
                        With f(j)
                            .X = _inputBuffer(i) * Window(j)
                            .Y = 0
                        End With
                        j += 1
                    Next

                    FastFourierTransform.FFT(True, FFT_Pow, f)
                End Sub
            )

            '清除人声
            Progress(fft)

            '后处理
            AsyncUtils.Process(
                Settings.Settings.Async.CompletelySync,
                Settings.Settings.Async.AsyncMode,
                Channels,
                Nothing,
                Sub(ch)
                    Dim f As Complex() = fft(ch)

                    FastFourierTransform.FFT(False, FFT_Pow, f)

                    ' 重叠相加合成
                    For i As Integer = 0 To FFT_Size - 1
                        Dim windowedSample As Single = f(i).X * Window(i)

                        If _isFirstFrame Then
                            ' 第一帧直接写入
                            _overlapBuffer(ch)(i) = windowedSample
                        Else
                            ' 后续帧进行重叠相加
                            _overlapBuffer(ch)(i) += windowedSample
                        End If
                    Next

                    '将前hopSize个样本复制到输出缓冲区
                    For i = 0 To Hop_Size - 1
                        _outputBuffer(i * Channels + ch) = _overlapBuffer(ch)(i)
                    Next

                    ' 移动重叠缓冲区
                    ' 将剩余数据移到前面，为下一帧做准备
                    Array.Copy(_overlapBuffer(ch), Hop_Size, _overlapBuffer(ch), 0, Overlap_Size)

                    ' 清空新移动区域的后部（避免残留数据影响）
                    For i As Integer = Overlap_Size To FFT_Size - 1
                        _overlapBuffer(ch)(i) = 0.0F
                    Next
                End Sub
            )

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

            ClearJaggedBuffer(_smoothedMasks)

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
            '暂且不封装异步和同步方法
            Dim Cores As Integer = AsyncUtils.GetPhysicalCores()

            If Settings.Settings.Async.CompletelySync OrElse Cores < 4 Then
                '同步执行
                For i = 0 To SideChannelPairs.Count - 1
                    With SideChannelPairs(i)
                        ProcessPairVocalRemoval(_smoothedMasks(i), fft(.Item1), fft(.Item2))
                    End With
                Next

                For Each Central In CenterChannelIndices
                    AttenuateCenterChannel(fft(Central))
                Next
            Else
                '异步执行
                Dim Countdown As New CountdownEvent(SideChannelPairs.Count + CenterChannelIndices.Count)

                For i = 0 To SideChannelPairs.Count - 1
                    Dim Index As Integer = i
                    Task.Run(Sub()
                                 With SideChannelPairs(Index)
                                     ProcessPairVocalRemoval(_smoothedMasks(Index), fft(.Item1), fft(.Item2))
                                 End With

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
            End If
        End Sub

        ' ==================== 状态缓冲区 ====================
        ' 每个声道对（或声道）的平滑掩膜状态，维度：[channelPairCount][halfBins + 1]
        Private ReadOnly _smoothedMasks As Single()()

        ' 初始化调用
        Private Shared Function CreateHalfBinBuffers(count As Integer) As Single()()
            If count <= 0 Then Return New Single()() {}
            Dim buffers(count - 1)() As Single
            For i = 0 To count - 1
                buffers(i) = New Single(FFT_Size \ 2) {}
            Next
            Return buffers
        End Function

        ' 重置调用
        Private Shared Sub ClearJaggedBuffer(ByRef buffer As Single()())
            For i = 0 To buffer.Length - 1
                Array.Clear(buffer(i), 0, buffer(i).Length)
            Next
        End Sub

        ' ==================== 软掩膜（Soft Knee） ====================
        ''' <summary>
        ''' 将检测分数平滑映射到 [0, 1] 掩膜值
        ''' </summary>
        ''' <param name="value">原始检测分数（如 coherence * centerDominance）</param>
        ''' <param name="threshold">阈值中心，例如 0.62</param>
        ''' <param name="knee">膝部宽度，例如 0.22；0 表示硬阈值</param>
        Private Shared Function SmoothKnee(value As Double, threshold As Double, knee As Double) As Double
            If knee <= 0.0 Then Return If(value >= threshold, 1.0, 0.0)

            Dim startValue As Double = threshold - knee * 0.5
            Dim endValue As Double = threshold + knee * 0.5
            Dim x As Double = Clamp((value - startValue) / (endValue - startValue), 0.0, 1.0)

            ' 三次平滑曲线（Smoothstep）
            Return x * x * (3.0 - 2.0 * x)
        End Function

        ' ==================== 时域平滑（Temporal Smoothing） ====================
        ''' <summary>
        ''' 对掩膜值进行时域平滑，独立控制 Attack / Release
        ''' </summary>
        ''' <param name="Masks">指定对称声道的掩膜</param>
        ''' <param name="bin">频点索引</param>
        ''' <param name="target">目标掩膜值（经软膝处理后）</param>
        ''' <param name="attack">上升系数 0~1，越大跟踪越快，例如 0.70</param>
        ''' <param name="release">下降系数 0~1，越小释放越慢，例如 0.25</param>
        Private Function SmoothMask(ByRef Masks As Single(), bin As Integer, target As Double, attack As Double, release As Double) As Double
            Dim previous As Double = Masks(bin)
            Dim alpha As Double = If(target > previous, attack, release)
            Dim smoothed As Double = previous + (target - previous) * alpha

            Masks(bin) = CSng(smoothed)
            Return smoothed
        End Function

        Private Shared Function Clamp(value As Double, min As Double, max As Double) As Double
            If value < min Then Return min
            If value > max Then Return max
            Return value
        End Function

        ''' <summary>
        ''' 对称声道消音处理
        ''' </summary>
        ''' <param name="fft1">声道1</param>
        ''' <param name="fft2">声道2</param>
        Protected Sub ProcessPairVocalRemoval(ByRef Masks As Single(), ByRef fft1 As Complex(), ByRef fft2 As Complex())
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
                Dim freq As Double = FreqTable(k)

                '计算局部对比度
                Dim contrast = ComputeLocalContrast(k, fft1) ' 使用左声道或平均

                '根据局部对比度调整（关键：合唱场景通常有多个峰值）
                '对比度高 = 频谱稀疏 = 可能是独立声源，降低阈值（更容易保留）
                '对比度低 = 频谱密集 = 可能是混叠，提高阈值（更严格消除）
                Dim contrastFactor = 1.0 - (contrast * 0.3) ' 对比度0-1，调整范围±0.3

                Dim dynamicThreshold = contrastFactor * GetFrequencyAdaptiveThreshold(freq)

                If coherence > dynamicThreshold Then
                    Dim midX As Double = (fft1(k).X + fft2(k).X) * 0.5
                    Dim midY As Double = (fft1(k).Y + fft2(k).Y) * 0.5
                    Dim sideX As Double = (fft1(k).X - fft2(k).X) * 0.5
                    Dim sideY As Double = (fft1(k).Y - fft2(k).Y) * 0.5

                    Dim midMagnitude = Magnitude(midX, midY)
                    Dim sideMagnitude = Magnitude(sideX, sideY)
                    Dim centerDominance = midMagnitude / (midMagnitude + sideMagnitude + 0.0000001)

                    Dim rate As Double = coherence * centerDominance

                    '软膝 将检测分数转为 0-1 的掩膜值
                    Dim rawMask As Double = SmoothKnee(rate, 0.62, 0.25)

                    '时域平滑 抑制抽吸感和闪烁
                    Dim mask As Double = SmoothMask(Masks, k, rawMask, 0.7, 0.15)

                    Dim attenuation As Double = GetVocalFrequencyWeight(freq)
                    attenuation *= mask

                    '衰减中置（人声），保留侧向（伴奏）
                    Dim att As Double = Math.Max(1 - attenuation * _ReductionFactor, 0)

                    '中置/侧向分解
                    Dim centerX As Double = midX * att
                    Dim centerY As Double = midY * att

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
                Dim freq As Double = FreqTable(k)
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

        Protected Shared Function Magnitude(x As Single, y As Single) As Single
            Return Math.Sqrt(x * x + y * y)
        End Function

        ''' <summary>
        ''' 计算复数相位
        ''' </summary>
        Protected Shared Function Phase(c As Complex) As Single
            Return Math.Atan2(c.Y, c.X)
        End Function

    End Class

End Namespace
