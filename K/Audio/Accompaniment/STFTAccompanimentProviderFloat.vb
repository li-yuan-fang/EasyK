Imports NAudio.Dsp
Imports NAudio.Wave

Namespace Accompaniment

    Public Class STFTAccompanimentProviderFloat
        Implements ISampleProvider

        ''' <summary>
        ''' 窗函数类型枚举
        ''' </summary>
        Public Enum WindowType
            Rectangular
            Hann
            Hamming
            Blackman
        End Enum

        Private Enum ChannelRole
            FrontLeft      ' 0
            FrontRight     ' 1
            FrontCenter    ' 2 - 人声主要位置！
            LowFrequency   ' 3 - 低音炮
            BackLeft       ' 4
            BackRight      ' 5
            SideLeft       ' 6 (7.1)
            SideRight      ' 7 (7.1)
        End Enum

        Private Const FFT_Size As Integer = 2048

        Private Const Hop_Size As Integer = FFT_Size \ 4

        Private Const Overlap_Size As Integer = FFT_Size - Hop_Size

        Private Shared ReadOnly FFT_Pow As Integer = CInt(Math.Log(FFT_Size, 2))

        ' 音频源
        Private ReadOnly _source As ISampleProvider

        '窗
        Private ReadOnly Window As Single()

        '采样率
        Private ReadOnly SampleRate As Integer

        ' 缓冲区
        Private ReadOnly _inputBuffer As Single()     ' 输入缓冲区
        Private ReadOnly _outputBuffer As Single()    ' 输出缓冲区（重叠相加）
        Private ReadOnly _overlapBuffer As Single()()   ' 重叠缓冲区

        ' 状态
        Private _inputBufferPos As Integer = 0
        Private _outputBufferPos As Integer = 0
        Private _outputBufferFilled As Integer = 0
        Private _isFirstFrame As Boolean = True

        '声道数
        Private ReadOnly Channels As Integer

        '声道角色
        Private ReadOnly ChannelRoles As List(Of ChannelRole)

        '中置声道索引
        Private ReadOnly CenterChannelIndices As List(Of Integer)

        '可配对的侧面声道
        Private ReadOnly SideChannelPairs As List(Of Tuple(Of Integer, Integer))

        ''' <summary>
        ''' 构造函数
        ''' </summary>
        ''' <param name="source">输入音频源</param>
        ''' <param name="windowType">窗函数类型</param>
        Public Sub New(source As ISampleProvider, Optional windowType As WindowType = WindowType.Hann)
            _source = source
            Channels = _source.WaveFormat.Channels
            SampleRate = source.WaveFormat.SampleRate

            ' 生成窗函数
            Window = GenerateWindow(FFT_Size, windowType)

            '声道配置
            ChannelRoles = MapChannels(Channels)
            CenterChannelIndices = GetCenterChannelIndices()
            SideChannelPairs = GetSideChannelPairs()

            ' 初始化缓冲区
            Dim FFT_Step As Integer = FFT_Size * Channels
            _inputBuffer = New Single(FFT_Step - 1) {}
            _outputBuffer = New Single(FFT_Step - 1) {}

            _overlapBuffer = New Single(Channels - 1)() {}
            For ch = 0 To Channels - 1
                _overlapBuffer(ch) = New Single(FFT_Size - 1) {}

                For i = 0 To FFT_Size - 1
                    _overlapBuffer(ch)(i) = 0
                Next
            Next
        End Sub

        ''' <summary>
        ''' 生成窗函数
        ''' </summary>
        Private Function GenerateWindow(size As Integer, type As WindowType) As Single()
            Dim window(size - 1) As Single

            For i As Integer = 0 To size - 1
                Select Case type
                    Case WindowType.Hann
                        ' Hann窗：0.5 * (1 - cos(2πn/(N-1)))
                        window(i) = 0.5F * (1.0F - CSng(Math.Cos(2.0 * Math.PI * i / (size - 1))))

                    Case WindowType.Hamming
                        ' Hamming窗：0.54 - 0.46 * cos(2πn/(N-1))
                        window(i) = 0.54F - 0.46F * CSng(Math.Cos(2.0 * Math.PI * i / (size - 1)))

                    Case WindowType.Blackman
                        ' Blackman窗
                        window(i) = 0.42F - 0.5F * CSng(Math.Cos(2.0 * Math.PI * i / (size - 1))) +
                                   0.08F * CSng(Math.Cos(4.0 * Math.PI * i / (size - 1)))

                    Case WindowType.Rectangular
                        ' 矩形窗
                        window(i) = 1.0F

                    Case Else
                        window(i) = 1.0F
                End Select
            Next

            Return window
        End Function

        '声道配对
        Private Function MapChannels(channels As Integer) As List(Of ChannelRole)
            Dim roles As New List(Of ChannelRole)

            Select Case channels
                Case 1 ' 单声道 - 无法消除，直接返回
                    roles.Add(ChannelRole.FrontCenter)

                Case 2 ' 立体声
                    roles.Add(ChannelRole.FrontLeft)
                    roles.Add(ChannelRole.FrontRight)

                Case 4 ' 四声道 (FL, FR, BL, BR)
                    roles.Add(ChannelRole.FrontLeft)
                    roles.Add(ChannelRole.FrontRight)
                    roles.Add(ChannelRole.BackLeft)
                    roles.Add(ChannelRole.BackRight)

                Case 6 ' 5.1声道
                    roles.Add(ChannelRole.FrontLeft)
                    roles.Add(ChannelRole.FrontRight)
                    roles.Add(ChannelRole.FrontCenter)
                    roles.Add(ChannelRole.LowFrequency)
                    roles.Add(ChannelRole.BackLeft)
                    roles.Add(ChannelRole.BackRight)

                Case 8 ' 7.1声道
                    roles.Add(ChannelRole.FrontLeft)
                    roles.Add(ChannelRole.FrontRight)
                    roles.Add(ChannelRole.FrontCenter)
                    roles.Add(ChannelRole.LowFrequency)
                    roles.Add(ChannelRole.BackLeft)
                    roles.Add(ChannelRole.BackRight)
                    roles.Add(ChannelRole.SideLeft)
                    roles.Add(ChannelRole.SideRight)

                Case Else ' 自定义多声道，循环映射
                    For i As Integer = 0 To channels - 1
                        If i >= 8 Then
                            roles.Add(ChannelRole.FrontLeft) ' 默认映射
                        Else
                            roles.Add(DirectCast(i, ChannelRole))
                        End If
                    Next
            End Select

            Return roles
        End Function

        '获取中置声道索引（人声主要所在）
        Private Function GetCenterChannelIndices() As List(Of Integer)
            Dim indices As New List(Of Integer)

            For i As Integer = 0 To ChannelRoles.Count - 1
                If ChannelRoles(i) = ChannelRole.FrontCenter Then
                    indices.Add(i)
                End If
            Next

            ' 如果没有明确的中置声道（如立体声），所有声道都视为可能包含人声
            If indices.Count = 0 AndAlso Channels = 2 Then
                ' 立体声特殊处理：虚拟中置由左右混合产生
            End If

            Return indices
        End Function

        '获取可配对的侧面声道（用于提取差分信号）
        Private Function GetSideChannelPairs() As List(Of Tuple(Of Integer, Integer))
            Dim pairs As New List(Of Tuple(Of Integer, Integer))

            ' 前侧左右配对
            Dim flIndex = ChannelRoles.IndexOf(ChannelRole.FrontLeft)
            Dim frIndex = ChannelRoles.IndexOf(ChannelRole.FrontRight)
            If flIndex >= 0 AndAlso frIndex >= 0 Then
                pairs.Add(Tuple.Create(flIndex, frIndex))
            End If

            ' 后侧左右配对
            Dim blIndex = ChannelRoles.IndexOf(ChannelRole.BackLeft)
            Dim brIndex = ChannelRoles.IndexOf(ChannelRole.BackRight)
            If blIndex >= 0 AndAlso brIndex >= 0 Then
                pairs.Add(Tuple.Create(blIndex, brIndex))
            End If

            ' 侧环绕配对 (7.1)
            Dim slIndex = ChannelRoles.IndexOf(ChannelRole.SideLeft)
            Dim srIndex = ChannelRoles.IndexOf(ChannelRole.SideRight)
            If slIndex >= 0 AndAlso srIndex >= 0 Then
                pairs.Add(Tuple.Create(slIndex, srIndex))
            End If

            Return pairs
        End Function

        ''' <summary>
        ''' 读取样本数据
        ''' </summary>
        Public Function Read(buffer As Single(), offset As Integer, count As Integer) As Integer Implements ISampleProvider.Read
            Dim samplesRead As Integer = 0
            Dim targetIndex As Integer = offset

            While samplesRead < count
                ' 如果输出缓冲区有数据，先读取
                If _outputBufferPos < _outputBufferFilled Then
                    Dim available As Integer = _outputBufferFilled - _outputBufferPos
                    Dim toCopy As Integer = Math.Min(available, count - samplesRead)

                    For i = 0 To toCopy - 1
                        buffer(targetIndex + i) = _outputBuffer(_outputBufferPos + i)
                    Next

                    _outputBufferPos += toCopy
                    targetIndex += toCopy
                    samplesRead += toCopy

                    If samplesRead >= count Then Exit While
                End If

                ' 需要处理新的帧
                If Not ProcessNextFrame() Then Exit While
            End While

            Return samplesRead
        End Function

        ''' <summary>
        ''' 处理下一帧音频数据
        ''' </summary>
        ''' <returns>是否成功处理</returns>
        Private Function ProcessNextFrame() As Boolean
            ' 从源读取足够的数据填充输入缓冲区
            Dim FFTStep As Integer = FFT_Size * Channels
            Dim samplesNeeded As Integer = FFTStep - _inputBufferPos

            If samplesNeeded > 0 Then
                Dim tempBuffer(samplesNeeded - 1) As Single
                Dim read As Integer = _source.Read(tempBuffer, 0, samplesNeeded)

                ' 将读取的数据复制到输入缓冲区
                Array.Copy(tempBuffer, 0, _inputBuffer, _inputBufferPos, read)
                _inputBufferPos += read

                ' 如果读取的数据不足，说明源已结束
                If read < samplesNeeded Then
                    ' 填充剩余部分为0（零填充）
                    For i As Integer = _inputBufferPos To FFTStep - 1
                        _inputBuffer(i) = 0.0F
                    Next

                    If _inputBufferPos = 0 Then
                        ' 完全没有数据了
                        Return False
                    End If
                End If
            End If

            ' 执行STFT处理
            PerformStftAndInverse()

            ' 更新输入缓冲区位置（帧移）
            ' 将未使用的数据移到缓冲区开头
            Dim remaining As Integer = Overlap_Size * Channels
            Array.Copy(_inputBuffer, Hop_Size * Channels, _inputBuffer, 0, remaining)
            _inputBufferPos = remaining

            Return True
        End Function

        ''' <summary>
        ''' 执行STFT和逆变换
        ''' </summary>
        Private Sub PerformStftAndInverse()
            Dim FFTStep As Integer = FFT_Size * Channels
            Dim fft As New List(Of Complex())

            '前处理
            For ch = 0 To Channels - 1
                Dim f = New Complex(FFT_Size - 1) {}
                Dim j = 0
                For i As Integer = ch To FFTStep - 1 Step Channels
                    With f(j)
                        .X = _inputBuffer(i) * Window(j)
                        .Y = 0
                    End With
                    j += 1
                Next

                FastFourierTransform.FFT(True, FFT_Pow, f)

                fft.Add(f)
            Next

            '清除人声
            Progress(fft)

            '后处理
            For ch = 0 To Channels - 1
                Dim f = fft(ch)

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

                ' 8. 移动重叠缓冲区
                ' 将剩余数据移到前面，为下一帧做准备
                Array.Copy(_overlapBuffer(ch), Hop_Size, _overlapBuffer(ch), 0, Overlap_Size)

                ' 清空新移动区域的后部（避免残留数据影响）
                For i As Integer = Overlap_Size To FFT_Size - 1
                    _overlapBuffer(ch)(i) = 0.0F
                Next
            Next

            _outputBufferFilled = Hop_Size * Channels
            _outputBufferPos = 0

            _isFirstFrame = False
        End Sub

        ''' <summary>
        ''' 获取输出格式
        ''' </summary>
        Public ReadOnly Property WaveFormat As WaveFormat Implements ISampleProvider.WaveFormat
            Get
                Return _source.WaveFormat
            End Get
        End Property

        ''' <summary>
        ''' 重置处理器状态
        ''' </summary>
        Public Sub Reset()
            _inputBufferPos = 0
            _outputBufferPos = 0
            _outputBufferFilled = 0
            _isFirstFrame = True

            Array.Clear(_inputBuffer, 0, _inputBuffer.Length)
            Array.Clear(_outputBuffer, 0, _outputBuffer.Length)
            Array.Clear(_overlapBuffer, 0, _overlapBuffer.Length)
        End Sub

        Private Sub Progress(fft As List(Of Complex()))
            For Each Side In SideChannelPairs
                ProcessPairVocalRemoval(fft(Side.Item1), fft(Side.Item2))
            Next

            For Each Central In CenterChannelIndices
                AttenuateCenterChannel(fft(Central))
            Next
        End Sub

        Private Sub ProcessPairVocalRemoval(ByRef fft1 As Complex(), ByRef fft2 As Complex())
            For k As Integer = 0 To FFT_Size \ 2
                ' 计算幅度和相位
                Dim mag1 As Double = Magnitude(fft1(k))
                Dim mag2 As Double = Magnitude(fft2(k))

                If mag1 < 0.0001 OrElse mag2 < 0.0001 Then Continue For

                Dim phase1 As Double = Phase(fft1(k))
                Dim phase2 As Double = Phase(fft2(k))

                ' 相干性分析
                Dim magRatio As Double = Math.Min(mag1, mag2) / Math.Max(mag1, mag2)
                Dim phaseDiff As Double = Math.Abs(phase1 - phase2)
                If phaseDiff > Math.PI Then phaseDiff = 2 * Math.PI - phaseDiff

                Dim coherence As Double = magRatio * (1 - phaseDiff / Math.PI)
                coherence = Math.Max(0, Math.Min(1, coherence))

                ' 频率判断
                Dim freq As Double = k * SampleRate / FFT_Size
                Dim attenuation As Double = GetVocalFrequencyWeight(freq)

                If coherence > 0.6 Then
                    ' 中置/侧向分解
                    Dim centerX As Double = (fft1(k).X + fft2(k).X) * 0.5
                    Dim centerY As Double = (fft1(k).Y + fft2(k).Y) * 0.5
                    Dim sideX As Double = (fft1(k).X - fft2(k).X) * 0.5
                    Dim sideY As Double = (fft1(k).Y - fft2(k).Y) * 0.5

                    ' 衰减中置（人声），保留侧向（伴奏）
                    Dim att As Double = (1 - attenuation * coherence)
                    centerX *= att
                    centerY *= att

                    fft1(k).X = CSng(centerX + sideX)
                    fft1(k).Y = CSng(centerY + sideY)
                    fft2(k).X = CSng(centerX - sideX)
                    fft2(k).Y = CSng(centerY - sideY)

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

                    ' 共轭对称
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

        Private Sub AttenuateCenterChannel(ByRef fft As Complex())
            For k As Integer = 0 To FFT_Size \ 2
                Dim freq As Double = k * SampleRate / FFT_Size
                ' 中置声道通常包含清晰人声，进行轻度宽频衰减
                If freq >= 1000 AndAlso freq <= 6000 Then
                    Dim attenuation As Single = GetVocalFrequencyWeight(freq)
                    fft(k).X *= attenuation
                    fft(k).Y *= attenuation

                    If k > 0 AndAlso k < FFT_Size \ 2 Then
                        fft(FFT_Size - k).X *= attenuation
                        fft(FFT_Size - k).Y *= attenuation
                    End If
                End If
            Next
        End Sub

        Private Shared Function GetVocalFrequencyWeight(freq As Single) As Single
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
        ''' 计算复数幅度
        ''' </summary>
        Private Shared Function Magnitude(c As Complex) As Single
            Return Math.Sqrt(c.X * c.X + c.Y * c.Y)
        End Function

        ''' <summary>
        ''' 计算复数相位
        ''' </summary>
        Private Shared Function Phase(c As Complex) As Single
            Return Math.Atan2(c.Y, c.X)
        End Function

    End Class

End Namespace
