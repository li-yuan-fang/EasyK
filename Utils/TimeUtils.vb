Imports System.Text.RegularExpressions

Public Class TimeUtils

    Private Shared ReadOnly TimeStringRegex As New Regex("^[\d\:\.]+$")

    ''' <summary>
    ''' 秒转换为时分秒
    ''' </summary>
    ''' <param name="Seconds">秒数</param>
    ''' <returns></returns>
    Public Shared Function SecondToString(Seconds As Long) As String
        Dim h As Long = Seconds \ 3600
        Dim m As Long = (Seconds Mod 3600) \ 60
        Dim s As Long = Seconds Mod 60
        Return String.Format("{0:D}:{1:D2}:{2:D2}", h, m, s)
    End Function

    ''' <summary>
    ''' 解析时分秒
    ''' </summary>
    ''' <param name="Str">时分秒字符串</param>
    ''' <returns></returns>
    Public Shared Function ParseString(Str As String) As Long
        If String.IsNullOrEmpty(Str) OrElse Not TimeStringRegex.IsMatch(Str) Then Return 0

        Dim Sum As Long = 0
        Dim Power As Long = 0
        Dim Buffer As String = vbNullString
        For i = Str.Length - 1 To 0 Step -1
            Select Case Str(i)
                Case ":"c
                    If Not String.IsNullOrEmpty(Buffer) Then
                        Sum += Long.Parse(Buffer) * (60 ^ Power)
                        Buffer = vbNullString
                    End If

                    Power += 1
                Case "."c
                    Buffer = vbNullString
                    Power = 0
                Case Else
                    Buffer = Str(i) & Buffer
            End Select
        Next

        If Not String.IsNullOrEmpty(Buffer) Then
            Sum += Long.Parse(Buffer) * (60 ^ Power)
        End If

        Return Sum
    End Function

    ''' <summary>
    ''' 解析时分秒
    ''' </summary>
    ''' <param name="Str">时分秒字符串</param>
    ''' <returns></returns>
    Public Shared Function ParseStringFloat(Str As String) As Double
        If String.IsNullOrEmpty(Str) OrElse Not TimeStringRegex.IsMatch(Str) Then Return 0

        Dim Sum As Double = 0
        Dim Power As Double = 0
        Dim Buffer As String = vbNullString
        For i = Str.Length - 1 To 0 Step -1
            Select Case Str(i)
                Case ":"c
                    If Not String.IsNullOrEmpty(Buffer) Then
                        Sum += Double.Parse(Buffer) * (60 ^ Power)
                        Buffer = vbNullString
                    End If

                    Power += 1
                Case "."c
                    If Not String.IsNullOrEmpty(Buffer) Then
                        Sum += Double.Parse($"0.{Buffer}")
                        Buffer = vbNullString
                    End If

                    Power = 0
                Case Else
                    Buffer = Str(i) & Buffer
            End Select
        Next

        If Not String.IsNullOrEmpty(Buffer) Then
            Sum += Double.Parse(Buffer) * (60 ^ Power)
        End If

        Return Sum
    End Function

End Class
