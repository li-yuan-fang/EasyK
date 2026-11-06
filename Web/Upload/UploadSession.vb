Imports System.Security.Cryptography
Imports System.Threading
Imports System.Windows.Forms

Namespace KWeb.Upload

    Friend Class UploadSession
        Implements IDisposable

        '会话ID
        Public ReadOnly Property Id As String

        Private ExpireTime As Long

        Private ReadOnly Total As Integer

        Private ReadOnly Uploaded As New HashSet(Of Integer)

        Private RefreshCount As Integer

        Private ReadOnly Stream As IO.FileStream

        Private ReadOnly StreamMuteX As New Mutex()

        Private ReadOnly Settings As SettingContainer

        Private Hash As String = vbNullString

        ''' <summary>
        ''' 初始化上传会话
        ''' </summary>
        ''' <param name="Size">文件大小(单位:byte)</param>
        ''' <param name="Settings">配置容器</param>
        Public Sub New(Size As Integer, Settings As SettingContainer)
            Me.Settings = Settings

            Id = Now.Ticks.ToString("x2")

            Total = Size \ Settings.Settings.Web.Upload.ChunkSize
            If Size Mod Settings.Settings.Web.Upload.ChunkSize <> 0 Then Total += 1

            ExpireTime = Now.Ticks + Settings.Settings.Web.Upload.ExpireDuration

            Stream = New IO.FileStream(IO.Path.Combine(Application.StartupPath, Settings.Settings.TempFolder, Id), IO.FileMode.Create)
            RefreshCount = 0
        End Sub

        ''' <summary>
        ''' 计算Hash
        ''' </summary>
        ''' <returns></returns>
        Public Function ComputeSHA256() As String
            If Not String.IsNullOrEmpty(Hash) Then Return Hash

            Dim Result As String = vbNullString
            Using sha256 As IncrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
                Try
                    SyncLock StreamMuteX
                        With Stream
                            Dim buffer(1023) As Byte

                            .Seek(0, IO.SeekOrigin.Begin)
                            Dim cnt As Integer = .Read(buffer, 0, buffer.Length)
                            While cnt > 0
                                sha256.AppendData(buffer, 0, cnt)
                                cnt = .Read(buffer, 0, buffer.Length)
                            End While
                        End With
                    End SyncLock
                Catch ex As Exception
                    Logger.Warn("上传会话 #{0} 计算摘要时出错 - {1}", Id, ex.Message)
                    Return vbNullString
                End Try

                Dim hashBytes As Byte() = sha256.GetHashAndReset()
                If hashBytes IsNot Nothing Then
                    For Each b As Byte In hashBytes
                        Result &= b.ToString("x2")
                    Next
                End If
            End Using

            If IsCompleted() Then Hash = Result

            Return Result
        End Function

        ''' <summary>
        ''' 上传分块
        ''' </summary>
        ''' <param name="Buffer">分块数据</param>
        ''' <param name="Index">分块序号</param>
        ''' <returns></returns>
        Public Function Upload(Buffer() As Byte, Index As Integer) As Boolean
            Dim Complete As Boolean

            If Index >= Total Then
                Logger.Warn("上传会话 #{0} 上传了非法分块 #{1}", Id, Index)
                Return False
            End If

            SyncLock Uploaded
                Uploaded.Add(Index)
                ExpireTime = Now.Ticks + Settings.Settings.Web.Upload.ExpireDuration

                Complete = Uploaded.Count >= Total
            End SyncLock

            Try
                SyncLock StreamMuteX
                    With Stream
                        .Seek(Index * Settings.Settings.Web.Upload.ChunkSize, IO.SeekOrigin.Begin)
                        .Write(Buffer, 0, Math.Min(Settings.Settings.Web.Upload.ChunkSize, Buffer.Length))
                    End With
                    RefreshCount += 1

                    If RefreshCount >= Settings.Settings.Web.Upload.RefreshThreshold OrElse Complete Then
                        RefreshCount = 0
                        Stream.Flush()
                    End If
                End SyncLock
            Catch ex As Exception
                SyncLock Uploaded
                    Uploaded.Remove(Id)
                End SyncLock

                Logger.Warn("上传会话 #{0} 上传分块 #{1} 时出错 - {2}", Id, Index, ex.Message)
            End Try

            Return Complete
        End Function

        ''' <summary>
        ''' 检测上传是否超时
        ''' </summary>
        ''' <returns></returns>
        Public Function IsExpired() As Boolean
            Return Now.Ticks > ExpireTime
        End Function

        ''' <summary>
        ''' 检测上传是否完成
        ''' </summary>
        ''' <returns></returns>
        Public Function IsCompleted() As Boolean
            SyncLock Uploaded
                Return Uploaded.Count >= Total
            End SyncLock
        End Function

        ''' <summary>
        ''' 获取未完成的分块
        ''' </summary>
        ''' <returns></returns>
        Public Function GetRequirements() As List(Of Integer)
            Dim Requirements As New List(Of Integer)
            SyncLock Uploaded
                For i = 0 To Total - 1
                    If Not Uploaded.Contains(i) Then Requirements.Add(i)
                Next
            End SyncLock

            Return Requirements
        End Function

        ''' <summary>
        ''' 销毁资源
        ''' </summary>
        Public Sub Dispose() Implements IDisposable.Dispose
            Try
                With Stream
                    .Flush()
                    .Close()
                    .Dispose()
                End With
            Catch ex As Exception
                Logger.Debug("上传会话 #{0} 销毁时出错 - {1}", Id, ex.Message)
            End Try
        End Sub

    End Class

End Namespace
