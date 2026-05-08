Namespace Accompaniment

    Public Class ChannelUtils

        ''' <summary>
        ''' 声道配对
        ''' </summary>
        ''' <param name="Channels">声道数</param>
        ''' <returns></returns>
        Public Shared Function MapChannels(Channels As Integer) As List(Of ChannelRole)
            Dim roles As New List(Of ChannelRole)

            Select Case Channels
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
                    For i As Integer = 0 To Channels - 1
                        If i >= 8 Then
                            roles.Add(ChannelRole.FrontLeft) ' 默认映射
                        Else
                            roles.Add(DirectCast(i, ChannelRole))
                        End If
                    Next
            End Select

            Return roles
        End Function

        ''' <summary>
        ''' 获取中置声道
        ''' </summary>
        ''' <param name="ChannelRoles">声道定位</param>
        ''' <returns></returns>
        Public Shared Function GetCenterChannelIndices(ChannelRoles As List(Of ChannelRole)) As List(Of Integer)
            Dim indices As New List(Of Integer)

            For i As Integer = 0 To ChannelRoles.Count - 1
                If ChannelRoles(i) = ChannelRole.FrontCenter Then
                    indices.Add(i)
                End If
            Next

            Return indices
        End Function

        ''' <summary>
        ''' 获取对称声道
        ''' </summary>
        ''' <param name="ChannelRoles">声道定位</param>
        ''' <returns></returns>
        Public Shared Function GetSideChannelPairs(ChannelRoles As List(Of ChannelRole)) As List(Of Tuple(Of Integer, Integer))
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

    End Class

End Namespace
