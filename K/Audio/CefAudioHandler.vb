Imports System.Runtime.InteropServices
Imports CefSharp
Imports CefSharp.Structs
Imports NAudio.Wave

Public Class CefAudioHandler
    Implements IAudioHandler

    Private ReadOnly K As EasyK

    Private ReadOnly Dummy As DummyPlayer

    Private ReadOnly Settings As SettingContainer

    Private Played As Boolean = False

    '声道数
    Private Channels As Integer = 2

    Public Sub New(K As EasyK, Settings As SettingContainer, Dummy As DummyPlayer)
        Me.K = K
        Me.Settings = Settings
        Me.Dummy = Dummy
    End Sub

    Public Sub OnAudioStreamStarted(chromiumWebBrowser As IWebBrowser, browser As IBrowser, parameters As AudioParameters, channels As Integer) Implements IAudioHandler.OnAudioStreamStarted
        Me.Channels = channels
        Played = False
        Dummy.Setup(WaveFormat.CreateIeeeFloatWaveFormat(parameters.SampleRate, channels), True)

        If Settings.Settings.DebugMode Then
            Console.WriteLine("CefSharp 托管音频播放")
            Console.WriteLine("采样率 {0}", parameters.SampleRate)
            Console.WriteLine("声道数 {0}", channels)
            Console.WriteLine("声道 {0} - {1}", [Enum].GetName(GetType(Enums.ChannelLayout), parameters.ChannelLayout), parameters.ChannelLayout)
        End If
    End Sub

    Public Sub OnAudioStreamPacket(chromiumWebBrowser As IWebBrowser, browser As IBrowser, data As IntPtr, noOfFrames As Integer, pts As Long) Implements IAudioHandler.OnAudioStreamPacket
        Try
            Dim BufferSize As Integer = Channels * noOfFrames * 4
            Dim AudioBuffer As Byte() = New Byte(BufferSize - 1) {}

            For c = 0 To Channels - 1
                '获取每个通道的数据指针
                Dim channelPtr As IntPtr = Marshal.ReadIntPtr(data, c * IntPtr.Size)

                For i = 0 To noOfFrames - 1
                    '读取float值 (4字节)
                    Dim floatBytes(3) As Byte
                    Marshal.Copy(channelPtr + (i * 4), floatBytes, 0, 4)

                    '计算目标位置并复制字节
                    Dim destIndex As Integer = (i * Channels + c) * 4
                    Buffer.BlockCopy(floatBytes, 0, AudioBuffer, destIndex, 4)
                Next
            Next

            With Dummy
                .Append(AudioBuffer, pts)
                If Not Played Then
                    .Play()
                    Played = True
                End If
            End With
        Catch ex As AccessViolationException
            Console.WriteLine("CefSharp 托管音频访存出错 - {0}", ex.Message)
        Catch ex As Exception
            Console.WriteLine("CefSharp 托管音频出错 - {0}", ex.Message)
        End Try
    End Sub

    Public Sub OnAudioStreamStopped(chromiumWebBrowser As IWebBrowser, browser As IBrowser) Implements IAudioHandler.OnAudioStreamStopped
        Dummy.Stop()
        Played = False
    End Sub

    Public Sub OnAudioStreamError(chromiumWebBrowser As IWebBrowser, browser As IBrowser, errorMessage As String) Implements IAudioHandler.OnAudioStreamError
        Dummy.Stop()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Dummy.Stop()
    End Sub

    Public Function GetAudioParameters(chromiumWebBrowser As IWebBrowser, browser As IBrowser, ByRef parameters As AudioParameters) As Boolean Implements IAudioHandler.GetAudioParameters
        Dim Current = K.GetCurrent()
        If Current Is Nothing OrElse Current.Type <> EasyKType.Bilibili Then Return False
        Return Settings.Settings.Audio.IsDummyAudio
    End Function

End Class
