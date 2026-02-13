Imports EasyK.Accompaniment
Imports NAudio.Wave

Public Class AccompanimentProvider
    Implements IWaveProvider, IResetable

    Private ReadOnly source As IWaveProvider

    Private ReadOnly Dummy As DummyPlayer

    Private ReadOnly UseFourierTransform As Boolean

    Private ReadOnly CenterAcc As CenterAccompaniment

    Private ReadOnly STFTAcc As STFTAccompanimentProvider

    Private FFT As Boolean = False

    Public Sub New(Dummy As DummyPlayer, Settings As SettingContainer, Provider As IWaveProvider, WaveFormat As WaveFormat)
        source = Provider
        Me.WaveFormat = WaveFormat
        With WaveFormat
            CenterAcc = New CenterAccompaniment(.Channels)
        End With

        STFTAcc = New STFTAccompanimentProvider(Provider)

        Me.Dummy = Dummy
        With Settings.Settings.Audio
            CenterAcc.ReductionFactor = .AccompanimentReductionFactor
            STFTAcc.ReductionFactor = .AccompanimentReductionFactor
            UseFourierTransform = .UseFourierTransform
        End With
    End Sub

    Public ReadOnly Property WaveFormat As WaveFormat Implements IWaveProvider.WaveFormat

    Public Function Read(buffer() As Byte, offset As Integer, count As Integer) As Integer Implements IWaveProvider.Read
        If Not Dummy.Accompaniment Then
            If FFT Then
                FFT = False
                Reset()
            End If

            Return source.Read(buffer, offset, count)
        End If

        If UseFourierTransform Then
            FFT = True
            Return STFTAcc.Read(buffer, offset, count)
        Else
            Dim bytesRead As Integer = source.Read(buffer, 0, count)
            CenterAcc.Progress(buffer, offset, bytesRead)
            Return bytesRead
        End If
    End Function

    Public Sub Reset() Implements IResetable.Reset
        STFTAcc.Reset()
    End Sub

End Class
