Imports System.Security.Cryptography

Public Class HashUtils

    ''' <summary>
    ''' 计算SHA256
    ''' </summary>
    ''' <param name="Buffer">数据</param>
    ''' <returns></returns>
    Public Shared Function ComputeSHA256(Buffer As Byte()) As String
        If Buffer Is Nothing Then Return vbNullString
        Dim Ret As String = vbNullString
        Try
            Using HashComputer As SHA256 = SHA256.Create
                Dim HashBytes() As Byte = HashComputer.ComputeHash(Buffer)
                If Not HashBytes Is Nothing Then
                    For Each b As Byte In HashBytes
                        Ret &= b.ToString("x2")
                    Next
                End If
            End Using
        Catch
        End Try
        Return Ret
    End Function

End Class
