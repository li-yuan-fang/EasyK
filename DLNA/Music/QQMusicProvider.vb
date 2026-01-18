Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Runtime.Remoting.Metadata.W3cXsd2001
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Xml
Imports ICSharpCode.SharpZipLib.Zip.Compression.Streams

Namespace DLNA.MusicProvider

    Friend Class Decrypter
        Private Shared ReadOnly QQKey As Byte() = Encoding.ASCII.GetBytes("!@#)(*$%123ZXC!@!@#)(NHL")

        ''' <summary>
        ''' 解密 QRC 歌词
        ''' </summary>
        ''' <param name="encryptedLyrics">加密的歌词</param>
        ''' <returns>解密后的 QRC 歌词</returns>
        Public Shared Function DecryptLyrics(encryptedLyrics As String) As String
            If String.IsNullOrEmpty(encryptedLyrics) Then
                Return Nothing
            End If

            Dim encryptedTextByte As Byte() = HexStringToByteArray(encryptedLyrics) ' parse text to bites array
            Dim data As Byte() = New Byte(encryptedTextByte.Length - 1) {}
            'Dim schedule As Byte(,,) = New Byte(2, 15, 5) {} ' 3×16×6 三维数组
            Dim schedule As Byte()()() = New Byte(2)()() {}
            For i = 0 To 2
                schedule(i) = New Byte(15)() {}
                For j = 0 To 15
                    schedule(i)(j) = New Byte(5) {}
                Next
            Next

            DESHelper.TripleDESKeySetup(QQKey, schedule, DESHelper.DECRYPT)

            For i = 0 To encryptedTextByte.Length - 1 Step 8
                Dim temp As Byte() = New Byte(7) {}
                DESHelper.TripleDESCrypt(encryptedTextByte.Skip(i).ToArray(), temp, schedule)
                Stop
                For j = 0 To 7
                    data(i + j) = temp(j)
                Next
            Next

            IO.File.WriteAllBytes("F:\Downloads\1\1.zip", data)

            Dim unzip As Byte() = SharpZipLibDecompress(data)
            Dim result As String = Encoding.UTF8.GetString(unzip)
            Return result
        End Function

        Private Shared Function SharpZipLibDecompress(data As Byte()) As Byte()
            Using compressed As New MemoryStream(data)
                Using decompressed As New MemoryStream()
                    Using inputStream As New InflaterInputStream(compressed)
                        inputStream.CopyTo(decompressed)
                    End Using
                    Return decompressed.ToArray()
                End Using
            End Using
        End Function

        Private Shared Function HexStringToByteArray(hexString As String) As Byte()
            Dim length As Integer = hexString.Length
            Dim bytes As Byte() = New Byte(length \ 2 - 1) {}
            For i As Integer = 0 To length - 1 Step 2
                bytes(i \ 2) = Convert.ToByte(hexString.Substring(i, 2), 16)
            Next
            Return bytes
        End Function

    End Class
    Public NotInheritable Class DESHelper
        Private Sub New()
        End Sub
        Private Shared ReadOnly ENCRYPT As UInteger = 1
        Public Shared ReadOnly DECRYPT As UInteger = 0

        Private Shared Function BITNUM(a As Byte(), b As Integer, c As Integer) As UInteger
            Return CUInt((a((b) \ 32 * 4 + 3 - ((b) Mod 32) \ 8) >> (7 - (b Mod 8))) And &H1) << (c)
        End Function

        Private Shared Function BITNUMINTR(a As UInteger, b As Integer, c As Integer) As Byte
            Return CByte((((a) >> (31 - (b))) And &H1) << (c))
        End Function

        Private Shared Function BITNUMINTL(a As UInteger, b As Integer, c As Integer) As UInteger
            Return ((((a) << (b)) And &H80000000UI) >> (c))
        End Function

        Private Shared Function SBOXBIT(a As Byte) As UInteger
            Return CUInt(((a) And &H20) Or (((a) And &H1F) >> 1) Or (((a) And &H1) << 4))
        End Function

        Private Shared ReadOnly sbox1 As Byte() = {14, 4, 13, 1, 2, 15,
        11, 8, 3, 10, 6, 12,
        5, 9, 0, 7, 0, 15,
        7, 4, 14, 2, 13, 1,
        10, 6, 12, 11, 9, 5,
        3, 8, 4, 1, 14, 8,
        13, 6, 2, 11, 15, 12,
        9, 7, 3, 10, 5, 0,
        15, 12, 8, 2, 4, 9,
        1, 7, 5, 11, 3, 14,
        10, 0, 6, 13}

        Private Shared ReadOnly sbox2 As Byte() = {15, 1, 8, 14, 6, 11,
        3, 4, 9, 7, 2, 13,
        12, 0, 5, 10, 3, 13,
        4, 7, 15, 2, 8, 15,
        12, 0, 1, 10, 6, 9,
        11, 5, 0, 14, 7, 11,
        10, 4, 13, 1, 5, 8,
        12, 6, 9, 3, 2, 15,
        13, 8, 10, 1, 3, 15,
        4, 2, 11, 6, 7, 12,
        0, 5, 14, 9}

        Private Shared ReadOnly sbox3 As Byte() = {10, 0, 9, 14, 6, 3,
        15, 5, 1, 13, 12, 7,
        11, 4, 2, 8, 13, 7,
        0, 9, 3, 4, 6, 10,
        2, 8, 5, 14, 12, 11,
        15, 1, 13, 6, 4, 9,
        8, 15, 3, 0, 11, 1,
        2, 12, 5, 10, 14, 7,
        1, 10, 13, 0, 6, 9,
        8, 7, 4, 15, 14, 3,
        11, 5, 2, 12}

        Private Shared ReadOnly sbox4 As Byte() = {7, 13, 14, 3, 0, 6,
        9, 10, 1, 2, 8, 5,
        11, 12, 4, 15, 13, 8,
        11, 5, 6, 15, 0, 3,
        4, 7, 2, 12, 1, 10,
        14, 9, 10, 6, 9, 0,
        12, 11, 7, 13, 15, 1,
        3, 14, 5, 2, 8, 4,
        3, 15, 0, 6, 10, 10,
        13, 8, 9, 4, 5, 11,
        12, 7, 2, 14}

        Private Shared ReadOnly sbox5 As Byte() = {2, 12, 4, 1, 7, 10,
        11, 6, 8, 5, 3, 15,
        13, 0, 14, 9, 14, 11,
        2, 12, 4, 7, 13, 1,
        5, 0, 15, 10, 3, 9,
        8, 6, 4, 2, 1, 11,
        10, 13, 7, 8, 15, 9,
        12, 5, 6, 3, 0, 14,
        11, 8, 12, 7, 1, 14,
        2, 13, 6, 15, 0, 9,
        10, 4, 5, 3}

        Private Shared ReadOnly sbox6 As Byte() = {12, 1, 10, 15, 9, 2,
        6, 8, 0, 13, 3, 4,
        14, 7, 5, 11, 10, 15,
        4, 2, 7, 12, 9, 5,
        6, 1, 13, 14, 0, 11,
        3, 8, 9, 14, 15, 5,
        2, 8, 12, 3, 7, 0,
        4, 10, 1, 13, 11, 6,
        4, 3, 2, 12, 9, 5,
        15, 10, 11, 14, 1, 7,
        6, 0, 8, 13}

        Private Shared ReadOnly sbox7 As Byte() = {4, 11, 2, 14, 15, 0,
        8, 13, 3, 12, 9, 7,
        5, 10, 6, 1, 13, 0,
        11, 7, 4, 9, 1, 10,
        14, 3, 5, 12, 2, 15,
        8, 6, 1, 4, 11, 13,
        12, 3, 7, 14, 10, 15,
        6, 8, 0, 5, 9, 2,
        6, 11, 13, 8, 1, 4,
        10, 7, 9, 5, 0, 15,
        14, 2, 3, 12}

        Private Shared ReadOnly sbox8 As Byte() = {13, 2, 8, 4, 6, 15,
        11, 1, 10, 9, 3, 14,
        5, 0, 12, 7, 1, 15,
        13, 8, 10, 3, 7, 4,
        12, 5, 6, 11, 0, 14,
        9, 2, 7, 11, 4, 1,
        9, 12, 14, 2, 0, 6,
        10, 13, 15, 3, 5, 8,
        2, 1, 14, 7, 4, 10,
        8, 13, 15, 12, 9, 0,
        3, 5, 6, 11}

        Public Shared Sub KeySchedule(key As Byte(), schedule As Byte()(), mode As UInteger)
            Dim i As UInteger, j As UInteger, toGen As UInteger, C As UInteger, D As UInteger
            Dim key_rnd_shift As UInteger() = {1, 1, 2, 2, 2, 2,
            2, 2, 1, 2, 2, 2,
            2, 2, 2, 1}
            Dim key_perm_c As UInteger() = {56, 48, 40, 32, 24, 16,
            8, 0, 57, 49, 41, 33,
            25, 17, 9, 1, 58, 50,
            42, 34, 26, 18, 10, 2,
            59, 51, 43, 35}
            Dim key_perm_d As UInteger() = {62, 54, 46, 38, 30, 22,
            14, 6, 61, 53, 45, 37,
            29, 21, 13, 5, 60, 52,
            44, 36, 28, 20, 12, 4,
            27, 19, 11, 3}
            Dim key_compression As UInteger() = {13, 16, 10, 23, 0, 4,
            2, 27, 14, 5, 20, 9,
            22, 18, 11, 3, 25, 7,
            15, 6, 26, 19, 12, 1,
            40, 51, 30, 36, 46, 54,
            29, 39, 50, 44, 32, 47,
            43, 48, 38, 55, 33, 52,
            45, 41, 49, 35, 28, 31}

            i = 0
            j = 31
            C = 0
            While i < 28
                C = C Or BITNUM(key, CInt(key_perm_c(i)), CInt(j))
                i += 1
                j -= 1
            End While

            i = 0
            j = 31
            D = 0
            While i < 28
                D = D Or BITNUM(key, CInt(key_perm_d(i)), CInt(j))
                i += 1
                j -= 1
            End While

            For i = 0 To 15
                C = ((C << CInt(key_rnd_shift(i))) Or (C >> (28 - CInt(key_rnd_shift(i))))) And &HFFFFFFF0UI
                D = ((D << CInt(key_rnd_shift(i))) Or (D >> (28 - CInt(key_rnd_shift(i))))) And &HFFFFFFF0UI

                If mode = DECRYPT Then
                    toGen = 15 - i
                Else
                    toGen = i
                End If

                For j = 0 To 5
                    schedule(toGen)(j) = 0
                Next

                For j = 0 To 23
                    schedule(toGen)(j \ 8) = schedule(toGen)(j \ 8) Or BITNUMINTR(C, CInt(key_compression(j)), CInt(7 - (j Mod 8)))
                Next

                While j < 48
                    schedule(toGen)(j \ 8) = schedule(toGen)(j \ 8) Or BITNUMINTR(D, CInt(key_compression(j)) - 27, CInt(7 - (j Mod 8)))
                    j += 1
                End While
            Next
        End Sub

        Private Shared Sub IP(state As UInteger(), input As Byte())
            state(0) = BITNUM(input, 57, 31) Or BITNUM(input, 49, 30) Or BITNUM(input, 41, 29) Or BITNUM(input, 33, 28) Or BITNUM(input, 25, 27) Or BITNUM(input, 17, 26) Or BITNUM(input, 9, 25) Or BITNUM(input, 1, 24) Or BITNUM(input, 59, 23) Or BITNUM(input, 51, 22) Or BITNUM(input, 43, 21) Or BITNUM(input, 35, 20) Or BITNUM(input, 27, 19) Or BITNUM(input, 19, 18) Or BITNUM(input, 11, 17) Or BITNUM(input, 3, 16) Or BITNUM(input, 61, 15) Or BITNUM(input, 53, 14) Or BITNUM(input, 45, 13) Or BITNUM(input, 37, 12) Or BITNUM(input, 29, 11) Or BITNUM(input, 21, 10) Or BITNUM(input, 13, 9) Or BITNUM(input, 5, 8) Or BITNUM(input, 63, 7) Or BITNUM(input, 55, 6) Or BITNUM(input, 47, 5) Or BITNUM(input, 39, 4) Or BITNUM(input, 31, 3) Or BITNUM(input, 23, 2) Or BITNUM(input, 15, 1) Or BITNUM(input, 7, 0)

            state(1) = BITNUM(input, 56, 31) Or BITNUM(input, 48, 30) Or BITNUM(input, 40, 29) Or BITNUM(input, 32, 28) Or BITNUM(input, 24, 27) Or BITNUM(input, 16, 26) Or BITNUM(input, 8, 25) Or BITNUM(input, 0, 24) Or BITNUM(input, 58, 23) Or BITNUM(input, 50, 22) Or BITNUM(input, 42, 21) Or BITNUM(input, 34, 20) Or BITNUM(input, 26, 19) Or BITNUM(input, 18, 18) Or BITNUM(input, 10, 17) Or BITNUM(input, 2, 16) Or BITNUM(input, 60, 15) Or BITNUM(input, 52, 14) Or BITNUM(input, 44, 13) Or BITNUM(input, 36, 12) Or BITNUM(input, 28, 11) Or BITNUM(input, 20, 10) Or BITNUM(input, 12, 9) Or BITNUM(input, 4, 8) Or BITNUM(input, 62, 7) Or BITNUM(input, 54, 6) Or BITNUM(input, 46, 5) Or BITNUM(input, 38, 4) Or BITNUM(input, 30, 3) Or BITNUM(input, 22, 2) Or BITNUM(input, 14, 1) Or BITNUM(input, 6, 0)
        End Sub

        Private Shared Sub InvIP(state As UInteger(), input As Byte())
            input(3) = CByte(BITNUMINTR(state(1), 7, 7) Or BITNUMINTR(state(0), 7, 6) Or BITNUMINTR(state(1), 15, 5) Or BITNUMINTR(state(0), 15, 4) Or BITNUMINTR(state(1), 23, 3) Or BITNUMINTR(state(0), 23, 2) Or BITNUMINTR(state(1), 31, 1) Or BITNUMINTR(state(0), 31, 0))

            input(2) = CByte(BITNUMINTR(state(1), 6, 7) Or BITNUMINTR(state(0), 6, 6) Or BITNUMINTR(state(1), 14, 5) Or BITNUMINTR(state(0), 14, 4) Or BITNUMINTR(state(1), 22, 3) Or BITNUMINTR(state(0), 22, 2) Or BITNUMINTR(state(1), 30, 1) Or BITNUMINTR(state(0), 30, 0))

            input(1) = CByte(BITNUMINTR(state(1), 5, 7) Or BITNUMINTR(state(0), 5, 6) Or BITNUMINTR(state(1), 13, 5) Or BITNUMINTR(state(0), 13, 4) Or BITNUMINTR(state(1), 21, 3) Or BITNUMINTR(state(0), 21, 2) Or BITNUMINTR(state(1), 29, 1) Or BITNUMINTR(state(0), 29, 0))

            input(0) = CByte(BITNUMINTR(state(1), 4, 7) Or BITNUMINTR(state(0), 4, 6) Or BITNUMINTR(state(1), 12, 5) Or BITNUMINTR(state(0), 12, 4) Or BITNUMINTR(state(1), 20, 3) Or BITNUMINTR(state(0), 20, 2) Or BITNUMINTR(state(1), 28, 1) Or BITNUMINTR(state(0), 28, 0))

            input(7) = CByte(BITNUMINTR(state(1), 3, 7) Or BITNUMINTR(state(0), 3, 6) Or BITNUMINTR(state(1), 11, 5) Or BITNUMINTR(state(0), 11, 4) Or BITNUMINTR(state(1), 19, 3) Or BITNUMINTR(state(0), 19, 2) Or BITNUMINTR(state(1), 27, 1) Or BITNUMINTR(state(0), 27, 0))

            input(6) = CByte(BITNUMINTR(state(1), 2, 7) Or BITNUMINTR(state(0), 2, 6) Or BITNUMINTR(state(1), 10, 5) Or BITNUMINTR(state(0), 10, 4) Or BITNUMINTR(state(1), 18, 3) Or BITNUMINTR(state(0), 18, 2) Or BITNUMINTR(state(1), 26, 1) Or BITNUMINTR(state(0), 26, 0))

            input(5) = CByte(BITNUMINTR(state(1), 1, 7) Or BITNUMINTR(state(0), 1, 6) Or BITNUMINTR(state(1), 9, 5) Or BITNUMINTR(state(0), 9, 4) Or BITNUMINTR(state(1), 17, 3) Or BITNUMINTR(state(0), 17, 2) Or BITNUMINTR(state(1), 25, 1) Or BITNUMINTR(state(0), 25, 0))

            input(4) = CByte(BITNUMINTR(state(1), 0, 7) Or BITNUMINTR(state(0), 0, 6) Or BITNUMINTR(state(1), 8, 5) Or BITNUMINTR(state(0), 8, 4) Or BITNUMINTR(state(1), 16, 3) Or BITNUMINTR(state(0), 16, 2) Or BITNUMINTR(state(1), 24, 1) Or BITNUMINTR(state(0), 24, 0))
        End Sub

        Private Shared Function F(state As UInteger, key As Byte()) As UInteger
            Dim lrgstate As Byte() = New Byte(5) {}
            Dim t1 As UInteger, t2 As UInteger

            t1 = BITNUMINTL(state, 31, 0) Or ((state And &HF0000000UI) >> 1) Or BITNUMINTL(state, 4, 5) Or BITNUMINTL(state, 3, 6) Or ((state And &HF000000) >> 3) Or BITNUMINTL(state, 8, 11) Or BITNUMINTL(state, 7, 12) Or ((state And &HF00000) >> 5) Or BITNUMINTL(state, 12, 17) Or BITNUMINTL(state, 11, 18) Or ((state And &HF0000) >> 7) Or BITNUMINTL(state, 16, 23)

            t2 = BITNUMINTL(state, 15, 0) Or ((state And &HF000) << 15) Or BITNUMINTL(state, 20, 5) Or BITNUMINTL(state, 19, 6) Or ((state And &HF00) << 13) Or BITNUMINTL(state, 24, 11) Or BITNUMINTL(state, 23, 12) Or ((state And &HF0) << 11) Or BITNUMINTL(state, 28, 17) Or BITNUMINTL(state, 27, 18) Or ((state And &HF) << 9) Or BITNUMINTL(state, 0, 23)

            lrgstate(0) = CByte((t1 >> 24) And &HFF)
            lrgstate(1) = CByte((t1 >> 16) And &HFF)
            lrgstate(2) = CByte((t1 >> 8) And &HFF)
            lrgstate(3) = CByte((t2 >> 24) And &HFF)
            lrgstate(4) = CByte((t2 >> 16) And &HFF)
            lrgstate(5) = CByte((t2 >> 8) And &HFF)

            lrgstate(0) = lrgstate(0) Xor key(0)
            lrgstate(1) = lrgstate(1) Xor key(1)
            lrgstate(2) = lrgstate(2) Xor key(2)
            lrgstate(3) = lrgstate(3) Xor key(3)
            lrgstate(4) = lrgstate(4) Xor key(4)
            lrgstate(5) = lrgstate(5) Xor key(5)

            state = CUInt((sbox1(SBOXBIT(CByte(lrgstate(0) >> 2))) << 28) Or (sbox2(SBOXBIT(CByte(((lrgstate(0) And &H3) << 4) Or (lrgstate(1) >> 4)))) << 24) Or (sbox3(SBOXBIT(CByte(((lrgstate(1) And &HF) << 2) Or (lrgstate(2) >> 6)))) << 20) Or (sbox4(SBOXBIT(CByte(lrgstate(2) And &H3F))) << 16) Or (sbox5(SBOXBIT(CByte(lrgstate(3) >> 2))) << 12) Or (sbox6(SBOXBIT(CByte(((lrgstate(3) And &H3) << 4) Or (lrgstate(4) >> 4)))) << 8) Or (sbox7(SBOXBIT(CByte(((lrgstate(4) And &HF) << 2) Or (lrgstate(5) >> 6)))) << 4) Or sbox8(SBOXBIT(CByte(lrgstate(5) And &H3F))))

            state = BITNUMINTL(state, 15, 0) Or BITNUMINTL(state, 6, 1) Or BITNUMINTL(state, 19, 2) Or BITNUMINTL(state, 20, 3) Or BITNUMINTL(state, 28, 4) Or BITNUMINTL(state, 11, 5) Or BITNUMINTL(state, 27, 6) Or BITNUMINTL(state, 16, 7) Or BITNUMINTL(state, 0, 8) Or BITNUMINTL(state, 14, 9) Or BITNUMINTL(state, 22, 10) Or BITNUMINTL(state, 25, 11) Or BITNUMINTL(state, 4, 12) Or BITNUMINTL(state, 17, 13) Or BITNUMINTL(state, 30, 14) Or BITNUMINTL(state, 9, 15) Or BITNUMINTL(state, 1, 16) Or BITNUMINTL(state, 7, 17) Or BITNUMINTL(state, 23, 18) Or BITNUMINTL(state, 13, 19) Or BITNUMINTL(state, 31, 20) Or BITNUMINTL(state, 26, 21) Or BITNUMINTL(state, 2, 22) Or BITNUMINTL(state, 8, 23) Or BITNUMINTL(state, 18, 24) Or BITNUMINTL(state, 12, 25) Or BITNUMINTL(state, 29, 26) Or BITNUMINTL(state, 5, 27) Or BITNUMINTL(state, 21, 28) Or BITNUMINTL(state, 10, 29) Or BITNUMINTL(state, 3, 30) Or BITNUMINTL(state, 24, 31)

            Return (state)
        End Function

        Public Shared Sub Crypt(input As Byte(), output As Byte(), key As Byte()())
            Dim state As UInteger() = New UInteger(1) {}
            Dim idx As UInteger, t As UInteger

            IP(state, input)

            For idx = 0 To 14
                t = state(1)
                state(1) = F(state(1), key(idx)) Xor state(0)
                state(0) = t
            Next

            state(0) = F(state(1), key(15)) Xor state(0)

            InvIP(state, output)
        End Sub
        Public Shared Sub TripleDESKeySetup(key As Byte(), schedule As Byte()()(), mode As UInteger)
            If mode = ENCRYPT Then
                KeySchedule(key, schedule(0), mode)
                KeySchedule(key.Skip(8).ToArray(), schedule(1), DECRYPT)
                KeySchedule(key.Skip(16).ToArray(), schedule(2), mode)

                ' KeySchedule(key[0..], schedule[0], mode);
                ' KeySchedule(key[8..], schedule[1], DECRYPT);
                ' KeySchedule(key[16..], schedule[2], mode);
            Else
                KeySchedule(key, schedule(2), mode)
                KeySchedule(key.Skip(8).ToArray(), schedule(1), ENCRYPT)
                KeySchedule(key.Skip(16).ToArray(), schedule(0), mode)

                ' KeySchedule(key[0..], schedule[2], mode);
                ' KeySchedule(key[8..], schedule[1], ENCRYPT);
                ' KeySchedule(key[16..], schedule[0], mode);
            End If
        End Sub

        Public Shared Sub TripleDESCrypt(input As Byte(), output As Byte(), key As Byte()()())
            Crypt(input, output, key(0))
            Crypt(output, output, key(1))
            Crypt(output, output, key(2))
        End Sub

    End Class


    Friend Class QQMusicProvider
        Inherits DLNALyricProvider

        Private Shared _dtFrom As New DateTime(1970, 1, 1, 8, 0, 0, 0, DateTimeKind.Local)

        Private Shared ReadOnly QQMusicNamespace As XNamespace = XNamespace.Get("http://y.qq.com/qplay/2.0/")

        <DllImport("QQMusicVerbatim.dll", EntryPoint:="?Ddes@qqmusic@@YAHPAE0H@Z", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Sub func_ddes(a As SByte(), b As String, c As Integer)
        End Sub

        <DllImport("QQMusicVerbatim.dll", EntryPoint:="?des@qqmusic@@YAHPAE0H@Z", CallingConvention:=CallingConvention.Cdecl)>
        Private Shared Sub func_des(a As SByte(), b As String, c As Integer)
        End Sub

        ' 原文
        ' 译文
        ' 罗马音
        ' 解压后的内容
        Private Shared ReadOnly VerbatimXmlMappingDict As New Dictionary(Of String, String)() From {
            {"content", "orig"},
            {"contentts", "ts"},
            {"contentroma", "roma"},
            {"Lyric_1", "lyric"}
        }

        Protected Overrides ReadOnly Property HttpRefer As String
            Get
                Return "https://c.y.qq.com/"
            End Get
        End Property

        Public Function GetNativeLyric(Id As String) As String
            Return SendPost("https://c.y.qq.com/qqmusic/fcgi-bin/lyric_download.fcg", New Dictionary(Of String, String) From
            {
                {"version", "15"},
                {"miniversion", "82"},
                {"lrctype", "4"},
                {"musicid", Id}
            })

        End Function

        Private Shared ReadOnly AmpRegex As New Regex("&(?![a-zA-Z]{2,6};|#[0-9]{2,4};)")

        Private Shared ReadOnly QuotRegex As New Regex("(\s+[\w:.-]+\s*=\s*"")(([^""]*)(("")((?!\s+[\w:.-]+\s*=\s*""|\s*(?:/?|\?)>))[^""]*)*)""")

        ''' <summary>
        ''' 创建 XML DOM
        ''' </summary>
        ''' <param name="content"></param>
        ''' <returns></returns>
        Public Shared Function XmlCreate(content As String) As XmlDocument
            content = RemoveIllegalContent(content)
            content = ReplaceAmp(content)
            content = ReplaceQuot(content)

            Dim doc As New XmlDocument()
            doc.LoadXml(content)
            Return doc
        End Function

        Private Shared Function ReplaceAmp(content As String) As String
            ' replace & symbol
            Return AmpRegex.Replace(content, "&amp;")
        End Function

        Private Shared Function ReplaceQuot(content As String) As String
            Dim sb As New StringBuilder()
            Dim currentPos As Integer = 0

            For Each match As Match In QuotRegex.Matches(content)
                sb.Append(content.Substring(currentPos, match.Index - currentPos))

                Dim f As String = match.Result(match.Groups(1).Value & match.Groups(2).Value.Replace("""", "&quot;")) & """"
                sb.Append(f)

                currentPos = match.Index + match.Length
            Next

            sb.Append(content.Substring(currentPos))
            Return sb.ToString()
        End Function

        ''' <summary>
        ''' 移除 XML 内容中无效的部分
        ''' </summary>
        ''' <param name="content">原始 XML 内容</param>
        ''' <returns>移除后的内容</returns>
        Private Shared Function RemoveIllegalContent(content As String) As String
            Dim left As Integer = 0
            Dim i As Integer = 0

            While i < content.Length
                If content(i) = "<"c Then
                    left = i
                End If

                ' 闭区间
                If i > 0 AndAlso content(i) = ">"c AndAlso content(i - 1) = "/"c Then
                    Dim part As String = content.Substring(left, i - left + 1)

                    ' 存在有且只有一个等号
                    If part.Contains("=") AndAlso part.IndexOf("=") = part.LastIndexOf("=") Then
                        ' 等号和左括号之间没有空格 <a="b" />
                        Dim part1 As String = content.Substring(left, part.IndexOf("="))
                        If Not part1.Trim().Contains(" ") Then
                            content = content.Substring(0, left) & content.Substring(i + 1)
                            i = 0
                            Continue While
                        End If
                    End If
                End If

                i += 1
            End While

            Return content.Trim()
        End Function

        ''' <summary>
        ''' 递归查找 XML DOM
        ''' </summary>
        ''' <param name="xmlNode">根节点</param>
        ''' <param name="mappingDict">节点名和结果名的映射</param>
        ''' <param name="resDict">结果集</param>
        Public Shared Sub XmlRecursionFindElement(xmlNode As XmlNode, mappingDict As Dictionary(Of String, String),
                                   resDict As Dictionary(Of String, XmlNode))
            Dim value As String = Nothing
            If mappingDict.TryGetValue(xmlNode.Name, value) Then
                resDict(value) = xmlNode
            End If

            If Not xmlNode.HasChildNodes Then
                Return
            End If

            For i As Integer = 0 To xmlNode.ChildNodes.Count - 1
                XmlRecursionFindElement(xmlNode.ChildNodes.Item(i), mappingDict, resDict)
            Next
        End Sub

        Public Function GetVerbatimLyric(songId As String)
            ' 发送POST请求到QQ音乐歌词接口
            Dim resp = SendPost("https://c.y.qq.com/qqmusic/fcgi-bin/lyric_download.fcg", New Dictionary(Of String, String) From {
        {"version", "15"},
        {"miniversion", "82"},
        {"lrctype", "4"},
        {"musicid", songId}
    })

            ' 移除QQ音乐返回内容中的注释标签
            resp = resp.Replace("<!--", "").Replace("-->", "")

            ' 存储XML节点的字典
            Dim dict = New Dictionary(Of String, XmlNode)()

            ' 递归查找XML节点
            XmlRecursionFindElement(XmlCreate(resp), VerbatimXmlMappingDict, dict)

            ' 初始化歌词结果对象
            'Dim result = New QQMusicBean.LyricResult With {
            '    .Code = 0,
            '    .Lyric = "",
            '    .Trans = ""
            '}

            ' 遍历解析每个XML节点
            For Each pair In dict
                Dim text = pair.Value.InnerText

                ' 跳过空内容
                If String.IsNullOrWhiteSpace(text) Then
                    Continue For
                End If

                ' 将加密文本转换为16进制字节数组
                Dim sbytes As SByte() = Nothing ' 对应C#的sbyte[]
                Dim sz = MathUtils.ConvertStringToHexSbytes(text, sbytes)

                ' 三重解密（调用自定义的解密函数）
                func_ddes(sbytes, "!@#)(NHLiuy*$%^&", sz)
                func_des(sbytes, "123ZXC!@#)(*$%^&", sz)
                func_ddes(sbytes, "!@#)(*$%^&abcDEF", sz)

                ' 解压解密后的字节数组并转换为字符串
                Dim decompressBytes = MathUtils.SharpZipLibDecompress(MathUtils.SbytesToBytes(sbytes, sz))
                Dim decompressText = Encoding.UTF8.GetString(decompressBytes)

                Dim s = ""
                If decompressText.Contains("<?xml") Then
                    ' 移除UTF8 BOM标识（如果存在）
                    Dim byteOrderMarkUtf8 = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble())
                    If decompressText(0) = byteOrderMarkUtf8(0) Then
                        decompressText = decompressText.Remove(0, byteOrderMarkUtf8.Length)
                    End If

                    ' 解析嵌套的XML内容
                    Dim doc = XmlCreate(decompressText)
                    Dim subDict = New Dictionary(Of String, XmlNode)()
                    XmlRecursionFindElement(doc, VerbatimXmlMappingDict, subDict)

                    ' 提取歌词内容
                    Dim d As XmlNode = Nothing
                    If subDict.TryGetValue("lyric", d) Then
                        s = d.Attributes("LyricContent").InnerText
                    End If
                Else
                    ' 非XML格式直接使用解压后的文本
                    s = decompressText
                End If

                ' 区分原文歌词和翻译歌词
                If Not String.IsNullOrWhiteSpace(s) Then
                    Select Case pair.Key
                        Case "orig"
                            Console.WriteLine(s)
                            Console.WriteLine()
                            'result.Lyric = LyricUtils.DealVerbatimLyric(s, SearchSourceEnum.QQ_MUSIC)
                        Case "ts"
                            Console.WriteLine(s)
                            Console.WriteLine()
                            'result.Trans = LyricUtils.DealVerbatimLyric(s, SearchSourceEnum.QQ_MUSIC)
                    End Select
                End If
            Next

            'Return result
            Return Nothing
        End Function


        Private Shared Function ResolveRespJson(callBackSign As String, val As String) As String
            If Not val.StartsWith(callBackSign) Then
                Return String.Empty
            End If

            Dim jsonStr = val.Replace(callBackSign & Convert.ToString("("), String.Empty)
            Return jsonStr.Remove(jsonStr.Length - 1)
        End Function

        Public Overrides Function IsMatch(Meta As String) As Boolean
            Dim Doc As XDocument = XmlUtils.SafeParseXml(Meta)
            If Doc Is Nothing Then Return False

            Dim Elements = From el In Doc.Descendants(DLNALyricProviders.MetaNamespace + "qplay")
                           Where el.Parent.Name = DLNALyricProviders.MetaNamespace + "item"
                           Select el

            Return Elements.Count > 0 AndAlso Elements(0).Attribute("version") IsNot Nothing
        End Function

        Public Overrides Function GetLyric(Meta As String) As List(Of DLNALyric)
            Dim Doc As XDocument = XmlUtils.SafeParseXml(Meta)
            If Doc Is Nothing Then Return Nothing

            Dim Elements = From el In Doc.Descendants(QQMusicNamespace + "songID")
                           Where el.Parent.Name = DLNALyricProviders.MetaNamespace + "item"
                           Select el

            For Each e In Elements
                If String.IsNullOrEmpty(e.Value) Then Continue For

            Next

            Return Nothing
        End Function

    End Class

End Namespace
