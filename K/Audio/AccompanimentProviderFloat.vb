Imports EasyK.Accompaniment
Imports NAudio.Wave

Public Class AccompanimentProviderFloat
    Implements ISampleProvider, IResetable

    Private ReadOnly source As ISampleProvider

    Private ReadOnly Dummy As DummyPlayer

    Private ReadOnly UseFourierTransform As Boolean

    Private ReadOnly CenterAcc As CenterAccompaniment

    Private ReadOnly STFTAcc As STFTAccompanimentProviderFloat

    Private FFT As Boolean = False

    Public Sub New(Dummy As DummyPlayer, Settings As SettingContainer, Provider As ISampleProvider, WaveFormat As WaveFormat)
        source = Provider
        Me.WaveFormat = WaveFormat
        With WaveFormat
            CenterAcc = New CenterAccompaniment(.Channels)
        End With

        STFTAcc = New STFTAccompanimentProviderFloat(Provider)

        Me.Dummy = Dummy
        With Settings.Settings.Audio
            CenterAcc.ReductionFactor = .AccompanimentReductionFactor
            STFTAcc.ReductionFactor = .AccompanimentReductionFactor
            UseFourierTransform = .UseFourierTransform
        End With
    End Sub

    Public ReadOnly Property WaveFormat As WaveFormat Implements ISampleProvider.WaveFormat

    Public Function Read(buffer() As Single, offset As Integer, count As Integer) As Integer Implements ISampleProvider.Read
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
            Dim samplesRead As Integer = source.Read(buffer, 0, count)
            CenterAcc.Progress(buffer, offset, samplesRead)
            Return samplesRead
        End If
    End Function

    Public Sub Reset() Implements IResetable.Reset
        STFTAcc.Reset()
    End Sub

End Class
