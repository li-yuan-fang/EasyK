Imports NAudio.Dsp
Imports NAudio.Wave

Namespace Accompaniment

    Public Class STFTAccompanimentProvider
        Implements IWaveProvider

        Public Enum WindowType
            Rectangular
            Hann
            Hamming
            Blackman
        End Enum

        Private Enum ChannelRole
            FrontLeft
            FrontRight
            FrontCenter
            LowFrequency
            BackLeft
            BackRight
            SideLeft
            SideRight
        End Enum

        Private Const FFT_Size As Integer = 2048
        Private Const Hop_Size As Integer = FFT_Size \ 4
        Private Const Overlap_Size As Integer = FFT_Size - Hop_Size
        Private Shared ReadOnly FFT_Pow As Integer = CInt(Math.Log(FFT_Size, 2))

        ' 源提供器（PCM-16）
        Private ReadOnly _source As IWaveProvider

        ' 窗函数
        Private ReadOnly Window As Single()

        ' 音频参数
        Private ReadOnly SampleRate As Integer
        Private ReadOnly Channels As Integer
        Private ReadOnly ChannelRoles As List(Of ChannelRole)
        Private ReadOnly CenterChannelIndices As List(Of Integer)
        Private ReadOnly SideChannelPairs As List(Of Tuple(Of Integer, Integer))

        ' 缓冲区（浮点处理）
        Private ReadOnly _inputBuffer As Single()
        Private ReadOnly _outputBuffer As Single()
        Private ReadOnly _overlapBuffer As Single()()

        ' 字节缓冲区（用于PCM-16读写）
        Private _sourceByteBuffer As Byte()
        Private _outputByteBuffer As Byte()

        ' 状态
        Private _inputBufferPos As Integer = 0
        Private _outputBufferPos As Integer = 0
        Private _outputBufferFilled As Integer = 0
        Private _isFirstFrame As Boolean = True
        Private _isSourceExhausted As Boolean = False

        ''' <summary>
        ''' 构造函数
        ''' </summary>
        Public Sub New(source As IWaveProvider, Optional windowType As WindowType = WindowType.Hann)
            _source = source
            With source.WaveFormat
                Channels = .Channels
                SampleRate = .SampleRate
            End With

            ' 初始化
            Window = GenerateWindow(FFT_Size, windowType)
            ChannelRoles = MapChannels(Channels)
            CenterChannelIndices = GetCenterChannelIndices()
            SideChannelPairs = GetSideChannelPairs()

            ' 浮点缓冲区（交错采样）
            Dim fftStep As Integer = FFT_Size * Channels
            _inputBuffer = New Single(fftStep - 1) {}
            _outputBuffer = New Single(fftStep - 1) {}

            _overlapBuffer = New Single(Channels - 1)() {}
            For ch = 0 To Channels - 1
                _overlapBuffer(ch) = New Single(FFT_Size - 1) {}
                Array.Clear(_overlapBuffer(ch), 0, FFT_Size)
            Next

            ' 字节缓冲区（PCM-16：每个采样2字节）
            Dim bytesPerFrame As Integer = FFT_Size * Channels * 2  ' 2 bytes per sample
            _sourceByteBuffer = New Byte(bytesPerFrame - 1) {}
            _outputByteBuffer = New Byte(bytesPerFrame - 1) {}
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
        ''' 输出格式（与源相同，PCM-16）
        ''' </summary>
        Public ReadOnly Property WaveFormat As WaveFormat Implements IWaveProvider.WaveFormat
            Get
                Return _source.WaveFormat
            End Get
        End Property

        ''' <summary>
        ''' 读取PCM-16数据
        ''' </summary>
        Public Function Read(buffer As Byte(), offset As Integer, count As Integer) As Integer Implements IWaveProvider.Read
            Dim totalBytesRead As Integer = 0
            Dim targetOffset As Integer = offset

            While totalBytesRead < count
                ' 如果输出缓冲区有数据，先复制
                If _outputBufferPos < _outputBufferFilled Then
                    Dim available As Integer = _outputBufferFilled - _outputBufferPos
                    Dim toCopy As Integer = Math.Min(available, count - totalBytesRead)

                    System.Buffer.BlockCopy(_outputByteBuffer, _outputBufferPos, buffer, targetOffset, toCopy)

                    _outputBufferPos += toCopy
                    targetOffset += toCopy
                    totalBytesRead += toCopy

                    If totalBytesRead >= count Then Exit While
                End If

                ' 源已耗尽且没有更多处理数据
                If _isSourceExhausted Then Exit While

                ' 处理新帧
                If Not ProcessNextFrame() Then
                    _isSourceExhausted = True
                End If
            End While

            Return totalBytesRead
        End Function

        ''' <summary>
        ''' 处理下一帧
        ''' </summary>
        Private Function ProcessNextFrame() As Boolean
            Dim fftStep As Integer = FFT_Size * Channels
            Dim samplesNeeded As Integer = fftStep - _inputBufferPos

            ' 从源读取字节数据
            If samplesNeeded > 0 AndAlso Not _isSourceExhausted Then
                ' 计算需要的字节数（PCM-16：每个采样2字节）
                Dim bytesNeeded As Integer = samplesNeeded * 2
                Dim bytesRead As Integer = 0

                ' 确保缓冲区足够
                If _sourceByteBuffer.Length < bytesNeeded Then
                    Array.Resize(_sourceByteBuffer, bytesNeeded)
                End If

                ' 读取源数据
                While bytesRead < bytesNeeded
                    Dim read As Integer = _source.Read(_sourceByteBuffer, bytesRead, bytesNeeded - bytesRead)
                    If read = 0 Then
                        _isSourceExhausted = True
                        Exit While
                    End If
                    bytesRead += read
                End While

                ' 字节转浮点（PCM-16 to Float）
                Dim samplesRead As Integer = bytesRead \ 2
                For i As Integer = 0 To samplesRead - 1
                    ' PCM-16是小端序：低字节在前，高字节在后
                    Dim byteIndex As Integer = i * 2
                    Dim sample As Short = BitConverter.ToInt16(_sourceByteBuffer, byteIndex)
                    ' 转换为-1.0到1.0的浮点
                    _inputBuffer(_inputBufferPos + i) = sample / 32768.0F
                Next

                _inputBufferPos += samplesRead

                ' 如果数据不足，零填充
                If samplesRead < samplesNeeded Then
                    For i As Integer = _inputBufferPos To fftStep - 1
                        _inputBuffer(i) = 0.0F
                    Next
                End If
            End If

            ' 执行STFT处理
            PerformStftAndInverse()

            ' 浮点转字节（Float to PCM-16）
            Dim outputSamples As Integer = Hop_Size * Channels
            Dim outputBytes As Integer = outputSamples * 2

            ' 确保输出缓冲区足够
            If _outputByteBuffer.Length < outputBytes Then
                Array.Resize(_outputByteBuffer, outputBytes)
            End If

            ' 转换并裁剪
            For i As Integer = 0 To outputSamples - 1
                ' 裁剪到[-1, 1]
                Dim a = Math.Min(1.0F, _outputBuffer(i))
                Dim sample As Double = Math.Max(-1.0F, a)
                Dim b = Math.Round(sample * 32767D)
                ' 转换为16位整数
                Dim intSample As Short = CShort(b)
                ' 小端序写入
                Dim byteIndex As Integer = i * 2
                _outputByteBuffer(byteIndex) = CByte(intSample And &HFF)
                _outputByteBuffer(byteIndex + 1) = CByte((intSample >> 8) And &HFF)
            Next

            ' 更新状态
            _outputBufferFilled = outputBytes
            _outputBufferPos = 0

            ' 移动输入缓冲区（重叠保留）
            Dim remainingSamples As Integer = Overlap_Size * Channels
            Array.Copy(_inputBuffer, Hop_Size * Channels, _inputBuffer, 0, remainingSamples)
            _inputBufferPos = remainingSamples

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