Imports System.Threading
Imports NAudio.Dsp
Imports NAudio.Wave

Namespace Accompaniment

    Public Class STFTAccompanimentProvider
        Inherits STFTAccompaniment
        Implements IWaveProvider

        ' 源提供器（PCM-16）
        Private ReadOnly _source As IWaveProvider

        ' 字节缓冲区（用于PCM-16读写）
        Private _sourceByteBuffer As Byte()
        Private _outputByteBuffer As Byte()

        Private _isSourceExhausted As Boolean = False

        ''' <summary>
        ''' 构造函数
        ''' </summary>
        Public Sub New(source As IWaveProvider, Optional windowType As STFTWindowType = STFTWindowType.Hann)
            MyBase.New(source.WaveFormat, windowType)
            _source = source

            ' 字节缓冲区（PCM-16：每个采样2字节）
            Dim bytesPerFrame As Integer = FFT_Size * Channels * 2  ' 2 bytes per sample
            _sourceByteBuffer = New Byte(bytesPerFrame - 1) {}
            _outputByteBuffer = New Byte(bytesPerFrame - 1) {}
        End Sub

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
            PerformSTFT()

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

    End Class

End Namespace