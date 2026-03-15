Imports NAudio.Dsp
Imports NAudio.Wave

Namespace Accompaniment

    Public Class STFTAccompanimentProviderFloat
        Inherits STFTAccompaniment
        Implements ISampleProvider

        ' 音频源
        Private ReadOnly _source As ISampleProvider

        ''' <summary>
        ''' 构造函数
        ''' </summary>
        ''' <param name="source">输入音频源</param>
        ''' <param name="windowType">窗函数类型</param>
        Public Sub New(source As ISampleProvider, Optional windowType As STFTWindowType = STFTWindowType.Hann)
            MyBase.New(source.WaveFormat, windowType)
            _source = source
        End Sub

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
            PerformSTFT()

            ' 更新输入缓冲区位置（帧移）
            ' 将未使用的数据移到缓冲区开头
            Dim remaining As Integer = Overlap_Size * Channels
            Array.Copy(_inputBuffer, Hop_Size * Channels, _inputBuffer, 0, remaining)
            _inputBufferPos = remaining

            Return True
        End Function

        ''' <summary>
        ''' 获取输出格式
        ''' </summary>
        Public ReadOnly Property WaveFormat As WaveFormat Implements ISampleProvider.WaveFormat
            Get
                Return _source.WaveFormat
            End Get
        End Property

    End Class

End Namespace
