Imports System.Security

Namespace Commands

    Public Class CommandDLNA
        Inherits Command

        Private ReadOnly K As EasyK

        Public Sub New(K As EasyK)
            MyBase.New("dlna", "dlna [名称] - 获取/设置DLNA设备名称", CommandType.System)
            Me.K = K
        End Sub

        Protected Overrides Sub Process(Args() As String)
            If Args.Length < 2 Then
                Console.WriteLine("DLNA设备名称: {0}", K.DLNADevice)
            Else
                Dim Name As String = SecurityElement.Escape(Args(1))
                K.DLNADevice = Name
                Console.WriteLine("DLNA设备名称已设置为 {0}", Name)
            End If
        End Sub

    End Class

End Namespace
