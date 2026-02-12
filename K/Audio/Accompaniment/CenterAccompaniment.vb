Namespace Accompaniment

    Public Class CenterAccompaniment

        Private Const BytesPerPCMSample As Integer = 2

        '声道数
        Private ReadOnly Channels As Integer

        '声道角色
        Private ReadOnly ChannelRoles As List(Of ChannelRole)

        '中置声道索引
        Private ReadOnly CenterChannelIndices As List(Of Integer)

        '可配对的侧面声道
        Private ReadOnly SideChannelPairs As List(Of Tuple(Of Integer, Integer))

        Private ReadOnly PCMFrameSize As Integer

        Private _ReductionFactor As Single = 0.9F

        ''' <summary>
        ''' 初始化中置伴奏器
        ''' </summary>
        ''' <param name="Channels">声道数</param>
        Public Sub New(Channels As Integer)
            Me.Channels = Channels
            PCMFrameSize = Me.Channels * BytesPerPCMSample

            ChannelRoles = ChannelUtils.MapChannels(Channels)
            CenterChannelIndices = ChannelUtils.GetCenterChannelIndices(ChannelRoles)
            SideChannelPairs = ChannelUtils.GetSideChannelPairs(ChannelRoles)
        End Sub

        ''' <summary>
        ''' 处理PCM-16
        ''' </summary>
        ''' <param name="Buffer">缓冲区</param>
        ''' <param name="Offset">偏移量</param>
        ''' <param name="BytesRead">字节数</param>
        Public Sub Progress(ByRef Buffer As Byte(), Offset As Integer, BytesRead As Integer)
            ' 确保处理完整的帧
            Dim framesToProcess = BytesRead \ PCMFrameSize
            Dim actualBytes = framesToProcess * PCMFrameSize

            For frame As Integer = 0 To framesToProcess - 1
                Dim frameOffset = frame * PCMFrameSize

                ' 步骤1: 解析所有声道样本到浮点数组
                Dim samples(Channels - 1) As Single
                For ch As Integer = 0 To Channels - 1
                    Dim sampleOffset = frameOffset + (ch * BytesPerPCMSample)
                    Dim rawValue As Short = BitConverter.ToInt16(Buffer, sampleOffset)
                    samples(ch) = rawValue / 32768.0F
                Next

                ' 步骤2: 计算各对侧面声道的差分信号（去除中置内容）
                Dim processedSamples = ProcessFrame(samples)

                ' 步骤3: 写回缓冲区
                For ch As Integer = 0 To Channels - 1
                    Dim sampleOffset = frameOffset + (ch * BytesPerPCMSample)
                    Dim clamped As Single = Math.Max(-1.0F, Math.Min(1.0F, processedSamples(ch)))
                    Dim newValue As Short = CShort(clamped * 32767.0F)

                    Buffer(Offset + sampleOffset) = CByte(newValue And &HFF)
                    Buffer(Offset + sampleOffset + 1) = CByte((newValue >> 8) And &HFF)
                Next
            Next
        End Sub

        ''' <summary>
        ''' 处理Float-32
        ''' </summary>
        ''' <param name="Buffer">缓冲区</param>
        ''' <param name="Offset">偏移量</param>
        ''' <param name="SamplesRead">长度</param>
        Public Sub Progress(ByRef Buffer As Single(), Offset As Integer, SamplesRead As Integer)
            For i = Offset To Offset + SamplesRead - 1 Step Channels
                Dim Frame = ProcessFrame(Buffer.Skip(i).Take(Channels).ToArray())

                For j = 0 To Channels - 1
                    Buffer(i + j) = Frame(j)
                Next
            Next
        End Sub

        ''' <summary>
        ''' 获取或设置衰减系数
        ''' </summary>
        ''' <returns></returns>
        Public Property ReductionFactor As Single
            Get
                Return _ReductionFactor
            End Get
            Set(value As Single)
                _ReductionFactor = Math.Max(0.0F, Math.Min(1.0F, value))
            End Set
        End Property

        ''' <summary>
        ''' 处理单帧多声道数据
        ''' </summary>
        Private Function ProcessFrame(samples() As Single) As Single()
            If Channels = 2 Then
                ' 立体声特殊处理：传统中置消除
                Return ProcessStereo(samples)
            End If

            Dim result(Channels - 1) As Single
            Array.Copy(samples, result, Channels)

            ' 多声道处理策略
            ' 1. 计算所有侧面声道的平均中置估计
            Dim centerEstimate As Single = 0
            Dim centerCount As Integer = 0

            For Each pair In SideChannelPairs
                Dim left = samples(pair.Item1)
                Dim right = samples(pair.Item2)
                ' 中置估计 = 左右声道的共同部分
                centerEstimate += (left + right) * 0.5F
                centerCount += 1
            Next

            If centerCount > 0 Then
                centerEstimate /= centerCount
            End If

            ' 2. 处理各侧面声道对：增强差分信号，衰减共同信号
            For Each pair In SideChannelPairs
                Dim leftIdx = pair.Item1
                Dim rightIdx = pair.Item2

                Dim left = samples(leftIdx)
                Dim right = samples(rightIdx)

                ' 提取侧面（差分）和中置（共同）成分
                Dim side As Single = (left - right) * 0.5F
                Dim center As Single = (left + right) * 0.5F

                ' 根据与中置估计的相似度调整消除强度
                Dim similarity As Single = Math.Abs(center - centerEstimate)
                Dim dynamicReduction As Single = _ReductionFactor * (1.0F - similarity * 0.5F)

                ' 重建声道：保留侧面，衰减中置
                Dim newCenter As Single = center * (1.0F - dynamicReduction)
                result(leftIdx) = side + newCenter
                result(rightIdx) = -side + newCenter
            Next

            ' 3. 处理明确的中置声道（5.1/7.1的Center声道）
            For Each centerIdx In CenterChannelIndices
                ' 中置声道直接大幅衰减
                result(centerIdx) = samples(centerIdx) * (1.0F - _ReductionFactor * 0.95F)
            Next

            ' 4. 低音炮声道通常不处理（人声很少在LFE）
            Dim lfeIdx = ChannelRoles.IndexOf(ChannelRole.LowFrequency)
            If lfeIdx >= 0 Then
                ' LFE轻微衰减避免人声低频残留
                result(lfeIdx) = samples(lfeIdx) * 0.9F
            End If

            Return result
        End Function

        ''' <summary>
        ''' 立体声专用处理（向后兼容）
        ''' </summary>
        Private Function ProcessStereo(samples() As Single) As Single()
            Dim result(1) As Single

            Dim left = samples(0)
            Dim right = samples(1)

            ' 计算中置和侧面
            Dim center As Single = (left + right) * 0.5F
            Dim side As Single = (left - right) * 0.5F

            ' 可选：添加轻微的时间差补偿（模拟立体声混响）
            Dim newCenter As Single = center * (1.0F - _ReductionFactor)

            result(0) = side + newCenter
            result(1) = -side + newCenter

            Return result
        End Function

    End Class

End Namespace
