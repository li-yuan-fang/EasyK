Imports System.Runtime.InteropServices
Imports CefSharp
Imports LibVLCSharp.Shared
Imports NAudio.Wave

Public Class VlcAudioHandler

    Private ReadOnly K As EasyK

    Private ReadOnly Dummy As DummyPlayer

    Private Channels As Integer = 2

    Private Playing As Boolean = False

    Public Sub New(K As EasyK, VlcMediaPlayer As MediaPlayer, Dummy As DummyPlayer)
        Me.K = K
        Me.Dummy = Dummy

        With VlcMediaPlayer
            .SetAudioFormatCallback(AddressOf OnSetup, AddressOf OnCleanup)
            .SetAudioCallbacks(AddressOf OnPlay, AddressOf OnPause, AddressOf OnResume, AddressOf OnFlush, Nothing)
        End With
    End Sub

    Private Function OnSetup(ByRef opaque As IntPtr, ByRef format As IntPtr, ByRef rate As UInteger, ByRef channels As UInteger) As Integer
        If Not K.Settings.Settings.Audio.IsDummyAudio Then Return -1
        Dim Current = K.GetCurrent()
        If Current Is Nothing OrElse Current.Type = EasyKType.Bilibili Then Return -2

        Dummy.Setup(New WaveFormat(rate, channels))
        Me.Channels = channels
        Playing = False

        If Settings.Settings.DebugMode Then
            Console.WriteLine("LibVlcSharp 托管音频播放")
            Console.WriteLine("采样率 {0}", rate)
            Console.WriteLine("声道数 {0}", channels)
        End If

        Return 0
    End Function

    Private Sub OnCleanup(opaque As IntPtr)
        Dummy.Stop()
    End Sub

    Private Sub OnPlay(data As IntPtr, samples As IntPtr, count As UInteger, pts As Long)
        Dim BufferSize As Integer = 2 * count * Channels
        Dim AudioBuffer As Byte() = New Byte(BufferSize - 1) {}

        Marshal.Copy(samples, AudioBuffer, 0, AudioBuffer.Length)

        With Dummy
            .Append(AudioBuffer, pts)

            If Not Playing Then
                .Play()
                Playing = True
            End If
        End With
    End Sub

    Private Sub OnPause(data As IntPtr, pts As Long)
        Dummy.Pause()
    End Sub

    Private Sub OnResume(data As IntPtr, pts As Long)
        Dummy.Play()
    End Sub

    Private Sub OnFlush(data As IntPtr, pts As Long)
        Dummy.CleanBuffer()
    End Sub

End Class
