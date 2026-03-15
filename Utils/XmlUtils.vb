Imports System.Xml

Public Class XmlUtils

    ''' <summary>
    ''' 安全解析Xml
    ''' </summary>
    ''' <param name="Xml"></param>
    ''' <returns></returns>
    Public Shared Function SafeParseXml(Xml As String) As XDocument
        Dim Doc As XDocument = Nothing

        For i = 1 To 3
            Try
                Doc = XDocument.Parse(Xml)
                Exit For
            Catch ex As XmlException
                '处理Xml违例
                '主要是QQ音乐

                '计算出错位置
                Dim Lines As String() = Split(Xml, vbCrLf)
                Dim Pos As Integer = 0
                With ex
                    '检测是否溢出
                    If .LineNumber() - 1 >= Lines.Length Then Return Nothing

                    For j = 0 To .LineNumber() - 2
                        Pos += Lines(j).Length + 2
                    Next
                    Pos += .LinePosition() - 1
                End With

                '获取命名空间
                Dim [Namespace] As String = vbNullString
                For j = Pos To Xml.Length - 1
                    If Xml(j) = ":"c Then Exit For
                    [Namespace] &= Xml(j)
                Next

                '补充命名空间标志
                Xml = Xml.Replace($" {[Namespace]}=", $" xmlns:{[Namespace]}=")
            Catch ex As Exception
                If Settings.Settings.DebugMode Then
                    Console.WriteLine("解析XML时出错 - {0}", ex.Message)
                End If

                Return Nothing
            End Try
        Next

        Return Doc
    End Function

End Class
