Imports System.Windows.Forms

Public Class CommandClean
    Inherits Command

    Private ReadOnly K As EasyK

    Private ReadOnly Web As KWebCore

    Private ReadOnly Settings As SettingContainer

    Public Sub New(K As EasyK, Web As KWebCore, Settings As SettingContainer)
        MyBase.New("clean", "clean [all] - 清理缓存", CommandType.System)
        Me.K = K
        Me.Web = Web
        Me.Settings = Settings
    End Sub

    Protected Overrides Sub Process(Args() As String)
        Dim All As Boolean = Args.Length >= 2 AndAlso Args(1).ToLower().Equals("all")

        Dim Folder As String = IO.Path.Combine(Application.StartupPath, Settings.Settings.TempFolder)
        If All Then
            For Each File As String In IO.Directory.GetFiles(Folder)
                Try
                    IO.File.Delete(File)
                Catch ex As Exception
                    Console.WriteLine("清理文件 {0} 时失败 - {1}", File, ex.Message)
                End Try
            Next
        Else
            Dim Occupied As New HashSet(Of String)
            For Each Key As String In K.GetOccupiedFiles()
                Occupied.Add(Key)
            Next
            For Each Key As String In Web.GetOccupiedFiles()
                Occupied.Add(Key)
            Next

            For Each File As String In IO.Directory.GetFiles(Folder)
                Dim i As Integer = File.LastIndexOf("\") + 1
                If Occupied.Contains(File.Substring(i)) Then Continue For

                Try
                    IO.File.Delete(File)
                Catch ex As Exception
                    Console.WriteLine("清理文件 {0} 时失败 - {1}", File, ex.Message)
                End Try
            Next
        End If

        Console.WriteLine("缓存清理完成")
    End Sub

End Class
