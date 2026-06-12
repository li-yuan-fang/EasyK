Imports System.Text.RegularExpressions
Imports System.Xml

Public Class XmlUtils

    Private Shared ReadOnly RootRegex As New Regex("<([a-zA-Z_][\w:.-]*)")

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

                '检测不完整格式
                '如Universal Media Server

                '获取根元素
                Dim Root As String = GetRootNode(Xml)
                If Root = "item" Then
                    Dim j = Xml.IndexOf("<item")
                    Dim NewXml As String = Xml.Substring(0, j) &
                        "<DIDL-Lite xmlns:dc=""http://purl.org/dc/elements/1.1/"" xmlns:upnp=""urnschemas-upnp - org: metadata-1-0/upnp/"" xmlns=""urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/"">"

                    Dim k = Xml.IndexOf("</item>", j) + "</item>".Length
                    NewXml &= Xml.Substring(j, (k - j)) & "</DIDL-Lite>" & Xml.Substring(k)

                    Xml = NewXml
                End If

                '检测未定义命名空间错误
                '如QQ音乐

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

    Private Shared Function GetRootNode(Xml As String) As String
        If String.IsNullOrEmpty(Xml) Then Return vbNullString

        Dim m = RootRegex.Match(Xml)
        Return If(m.Success, m.Groups(1).Value, vbNullString)
    End Function

End Class
