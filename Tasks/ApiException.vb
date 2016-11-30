Friend Class APIException
    Inherits Exception

    Public Sub New(serverMessage As String)
        MyBase.New($"API error occured. {serverMessage}")
    End Sub

End Class
