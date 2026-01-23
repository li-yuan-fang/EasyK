Imports System.Drawing

Public Class ColorUtils

    ' 高饱和度阈值（0-1，可根据需求调整，0.5 代表只保留饱和度≥50%的像素）
    Private Const MinSaturation As Double = 0.6
    ' 明度过滤范围（避免过亮/过暗的像素干扰，0-1）
    Private Const MinLightness As Double = 0.2
    Private Const MaxLightness As Double = 0.7

    ''' <summary>
    ''' 计算图片的高饱和度代表色
    ''' </summary>
    ''' <param name="image">图片</param>
    ''' <returns>高饱和度代表色（Color类型），无符合条件像素时返回透明色</returns>
    Public Shared Function GetHighSaturationDominantColor(image As Image) As Color
        Try
            Using bmp As New Bitmap(image)
                Dim rect As New Rectangle(0, 0, bmp.Width, bmp.Height)
                Dim bmpData = bmp.LockBits(rect, Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat)
                Dim stride As Integer = bmpData.Stride
                Dim pixelBuffer As Byte() = New Byte(stride * bmp.Height - 1) {}
                System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, pixelBuffer, 0, pixelBuffer.Length)
                bmp.UnlockBits(bmpData)

                ' 统计高饱和度像素的色相出现次数
                Dim hueCount As New Dictionary(Of Integer, Integer)()
                Dim bytesPerPixel As Integer = If(bmp.PixelFormat = Imaging.PixelFormat.Format32bppArgb, 4, 3)

                For y As Integer = 0 To bmp.Height - 1
                    For x As Integer = 0 To bmp.Width - 1
                        ' 计算像素在缓冲区中的位置
                        Dim pos As Integer = y * stride + x * bytesPerPixel
                        ' 提取 BGR 通道（注意：Bitmap 存储顺序是 BGR 而非 RGB）
                        Dim b As Byte = pixelBuffer(pos)
                        Dim g As Byte = pixelBuffer(pos + 1)
                        Dim r As Byte = pixelBuffer(pos + 2)

                        ' 转换为 HSL
                        Dim h, s, l As Double
                        RgbToHsl(r, g, b, h, s, l)

                        ' 过滤高饱和度、适中明度的像素
                        If s >= MinSaturation AndAlso l >= MinLightness AndAlso l <= MaxLightness Then
                            ' 将色相取整（0-359），减少聚类维度
                            Dim hueInt As Integer = CInt(Math.Round(h)) Mod 360
                            If hueCount.ContainsKey(hueInt) Then
                                hueCount(hueInt) += 1
                            Else
                                hueCount(hueInt) = 1
                            End If
                        End If
                    Next
                Next

                ' 找出出现次数最多的色相
                If hueCount.Count = 0 Then
                    Return Color.Transparent ' 无符合条件的高饱和度像素
                End If

                Dim maxCount As Integer = 0
                Dim dominantHue As Integer = 0
                For Each kvp In hueCount
                    If kvp.Value > maxCount Then
                        maxCount = kvp.Value
                        dominantHue = kvp.Key
                    End If
                Next

                ' 将最频色相转回 RGB（饱和度取0.8，明度取0.5，保证鲜艳度）
                Return HslToRgb(dominantHue, 0.8, 0.5)
            End Using
        Catch ex As Exception
            Console.WriteLine("计算失败：" & ex.Message)
            Return Color.Transparent
        End Try
    End Function

    ''' <summary>
    ''' RGB 转 HSL 色彩空间
    ''' </summary>
    Private Shared Sub RgbToHsl(r As Byte, g As Byte, b As Byte, ByRef h As Double, ByRef s As Double, ByRef l As Double)
        Dim rNorm As Double = r / 255.0
        Dim gNorm As Double = g / 255.0
        Dim bNorm As Double = b / 255.0

        Dim max As Double = Math.Max(Math.Max(rNorm, gNorm), bNorm)
        Dim min As Double = Math.Min(Math.Min(rNorm, gNorm), bNorm)
        Dim delta As Double = max - min

        ' 计算明度 L
        l = (max + min) / 2.0

        ' 计算饱和度 S
        If delta = 0 Then
            s = 0 ' 灰度色，饱和度为0
            h = 0 ' 色相无意义
        Else
            s = If(l < 0.5, delta / (max + min), delta / (2 - max - min))

            ' 计算色相 H
            If rNorm = max Then
                h = (gNorm - bNorm) / delta
            ElseIf gNorm = max Then
                h = 2 + (bNorm - rNorm) / delta
            Else
                h = 4 + (rNorm - gNorm) / delta
            End If

            h *= 60 ' 转换为角度（0-360）
            If h < 0 Then h += 360
        End If
    End Sub

    ''' <summary>
    ''' HSL 转 RGB 色彩空间
    ''' </summary>
    Private Shared Function HslToRgb(h As Double, s As Double, l As Double) As Color
        If s = 0 Then
            ' 灰度色
            Dim gray As Byte = CByte(Math.Round(l * 255))
            Return Color.FromArgb(gray, gray, gray)
        End If

        Dim q As Double = If(l < 0.5, l * (1 + s), l + s - l * s)
        Dim p As Double = 2 * l - q
        Dim hNorm As Double = h / 360.0

        Dim r As Double = HueToRgb(p, q, hNorm + 1 / 3)
        Dim g As Double = HueToRgb(p, q, hNorm)
        Dim b As Double = HueToRgb(p, q, hNorm - 1 / 3)

        Return Color.FromArgb(
            CByte(Math.Round(r * 255)),
            CByte(Math.Round(g * 255)),
            CByte(Math.Round(b * 255))
        )
    End Function

    ''' <summary>
    ''' 辅助函数：将色相分量转换为 RGB 分量
    ''' </summary>
    Private Shared Function HueToRgb(p As Double, q As Double, t As Double) As Double
        If t < 0 Then t += 1
        If t > 1 Then t -= 1
        If t < 1 / 6 Then Return p + (q - p) * 6 * t
        If t < 1 / 2 Then Return q
        If t < 2 / 3 Then Return p + (q - p) * (2 / 3 - t) * 6
        Return p
    End Function

    ''' <summary>
    ''' 提亮颜色
    ''' </summary>
    ''' <param name="Original">原始颜色</param>
    ''' <returns></returns>
    Public Shared Function HighlightColor(Original As Color) As Color
        With Original
            Dim Max = Math.Max(Math.Max(.R, .G), .B)
            If Max = &HFF Then Return Original

            Dim Rate = 255 / Max
            Dim r = .R * Rate
            Dim g = .G * Rate
            Dim b = .B * Rate
            Return Color.FromArgb(r, g, b)
        End With
    End Function

End Class
