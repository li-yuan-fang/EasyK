Imports System.Management
Imports System.Windows.Controls.Primitives
Imports System.Windows.Forms
Imports Newtonsoft.Json

Public Class ScreenUtils

    ''' <summary>
    ''' 获取详细的显示器信息（通过 WMI）
    ''' </summary>
    Public Shared Function GetMonitors(Optional ActiveOnly As Boolean = True) As List(Of MonitorInfo)
        Dim monitors As New List(Of MonitorInfo)

        Try
            ' 使用 Win32_DesktopMonitor 获取基本显示器信息
            Using searcher As New ManagementObjectSearcher("SELECT * FROM Win32_DesktopMonitor")
                For Each monitor As ManagementObject In searcher.Get()
                    If monitor("Availability") <> 8 Then Continue For

                    Dim info As New MonitorInfo With {
                        .DeviceID = monitor("DeviceID")?.ToString(),
                        .Name = monitor("Name")?.ToString(),
                        .Status = monitor("Status")?.ToString()
                    }

                    If Not ActiveOnly OrElse info.Status = "OK" Then monitors.Add(info)
                Next
            End Using

            ' 获取更详细的 EDID 信息（包括制造商、序列号等）
            AttachEdidInfo(monitors)

        Catch ex As Exception
            If Settings.Settings.DebugMode Then Console.WriteLine($"获取显示器信息失败: {ex.Message}")
        End Try

        Return monitors
    End Function

    ''' <summary>
    ''' 通过 WMI 的 WmiMonitorBasicDisplayParams 获取 EDID 信息
    ''' </summary>
    Private Shared Sub AttachEdidInfo(monitors As List(Of MonitorInfo))
        Try
            Using searcher As New ManagementObjectSearcher(
                "root\WMI",
                "SELECT * FROM WmiMonitorBasicDisplayParams")

                For Each monitor As ManagementObject In searcher.Get()
                    Dim instanceName As String = monitor("InstanceName")?.ToString()

                    ' 查找匹配的显示器
                    Dim matchedMonitor = monitors.FirstOrDefault(
                        Function(m) instanceName.Contains(m.DeviceID.Replace("\", "#")))
                Next
            End Using

            ' 获取制造商和序列号（WmiMonitorID）
            Using searcher As New ManagementObjectSearcher(
                "root\WMI",
                "SELECT * FROM WmiMonitorID")

                Dim index As Integer = 0
                For Each monitor As ManagementObject In searcher.Get()
                    If index < monitors.Count Then
                        Dim info As MonitorInfo = monitors(index)

                        ' 制造商名称（需要解码）
                        info.ManufacturerName = DecodeMonitorString(monitor("ManufacturerName"))

                        ' 产品代码 ID
                        info.ProductCodeID = DecodeMonitorHex(monitor("ProductCodeID"))

                        ' 序列号
                        info.SerialNumber = DecodeMonitorString(monitor("SerialNumberID"))

                        ' 生产日期（从 WeekOfManufacture 和 YearOfManufacture 计算）
                        Dim year As Integer = Convert.ToInt32(monitor("YearOfManufacture"))
                        Dim week As Integer = Convert.ToInt32(monitor("WeekOfManufacture"))
                        info.ManufactureDate = $"{year}-{week}"

                        index += 1
                    End If
                Next
            End Using

        Catch ex As Exception
            If Settings.Settings.DebugMode Then Console.WriteLine($"获取 EDID 信息失败: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' 解码显示器字符串（WMI 返回的是 UInt16 数组）
    ''' </summary>
    Private Shared Function DecodeMonitorString(data As Object) As String
        If data Is Nothing Then Return String.Empty

        Try
            Dim charArray As UInt16() = DirectCast(data, UInt16())
            Dim chars As New List(Of Char)

            For Each c As UInt16 In charArray
                If c <> 0 Then
                    chars.Add(Convert.ToChar(c))
                End If
            Next

            Return New String(chars.ToArray()).Trim()
        Catch
            Return String.Empty
        End Try
    End Function

    ''' <summary>
    ''' 解码十六进制产品代码
    ''' </summary>
    Private Shared Function DecodeMonitorHex(data As Object) As String
        If data Is Nothing Then Return String.Empty

        Try
            Dim hexArray As UInt16() = DirectCast(data, UInt16())
            Return BitConverter.ToString(
                hexArray.Select(Function(x) Convert.ToByte(x)).ToArray()).Replace("-", "")
        Catch
            Return String.Empty
        End Try
    End Function

    <Serializable>
    Public Class MonitorInfo

        <JsonIgnore>
        Public Property DeviceID As String

        <JsonProperty("name")>
        Public Property Name As String

        <JsonIgnore>
        Public Property Status As String

        <JsonProperty("manufacturer_name")>
        Public Property ManufacturerName As String

        <JsonProperty("product_id")>
        Public Property ProductCodeID As String

        <JsonProperty("serial")>
        Public Property SerialNumber As String

        <JsonProperty("manufacture_date")>
        Public Property ManufactureDate As String

    End Class

    Public Structure OverlapScreen
        Public Id As Integer
        Public Screen As Screen
    End Structure

    ''' <summary>
    ''' 获取最大重叠
    ''' </summary>
    ''' <param name="DesktopBounds">桌面区域</param>
    ''' <returns></returns>
    Public Shared Function GetOverlapScreen(DesktopBounds As Drawing.Rectangle) As OverlapScreen
        Dim max As Integer = 0
        Dim overlap As Screen = Nothing
        Dim overlapId As Integer = -1

        Dim i As Integer = 0
        For Each s In Screen.AllScreens()
            Dim r As Drawing.Rectangle = Drawing.Rectangle.Intersect(s.Bounds, DesktopBounds)
            Dim size As Integer = r.Width * r.Height
            If size > max Then
                max = size
                overlap = s
                overlapId = i
            End If

            i += 1
        Next

        Return New OverlapScreen With {
            .Id = overlapId,
            .Screen = overlap
        }
    End Function

End Class
