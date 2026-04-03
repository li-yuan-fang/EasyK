Imports System.Windows.Forms
Imports CefSharp

Public Class CefStorage
    Implements ICookieVisitor

    Private _Clean As Boolean = False

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub

    Public Function Visit(cookie As Cookie, count As Integer, total As Integer, ByRef deleteCookie As Boolean) As Boolean Implements ICookieVisitor.Visit
        _Clean = True

        With cookie

            If .Name.ToUpper = "SESSDATA" AndAlso
                Not String.IsNullOrWhiteSpace(.Value) AndAlso
                .Domain.EndsWith("bilibili.com") Then

                _Clean = False

                Return False
            End If
        End With

        Return True
    End Function

    ''' <summary>
    ''' 清理
    ''' </summary>
    Public Sub Clean()
        If Not _Clean Then Return

        Try
            Array.ForEach(IO.Directory.GetFiles(IO.Path.Combine(Application.StartupPath, CefSetting.StoragePath),
                                                "*.*",
                                                IO.SearchOption.AllDirectories
                                                ), Sub(f)
                                                       Try
                                                           IO.File.Delete(f)
                                                       Catch
                                                       End Try
                                                   End Sub)
        Catch ex As Exception
            Console.WriteLine("清理CefSharp缓存时出错: {0}", ex.Message)
        End Try
    End Sub

End Class
