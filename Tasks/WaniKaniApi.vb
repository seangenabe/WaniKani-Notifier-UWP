Imports Windows.Data.Json
Imports Windows.Web.Http

Friend NotInheritable Class WaniKaniApi

    Private Shared _httpclient As New HttpClient()

    Public Const WANIKANI_BASE_ENDPOINT = "https://www.wanikani.com/api/v1.4"

    Public Shared Function UserRequest(apiKey As String, endpoint As String) As IAsyncOperation(Of Result)
        Dim encKey = Uri.EscapeUriString(apiKey)
        Return Request($"user/{encKey}/{endpoint}")
    End Function

    Private Shared Function Request(endpoint As String) As IAsyncOperation(Of Result)
        Return AsyncInfo.Run(Of Result)(
            Async Function() As Task(Of Result)
                Dim _uri As New Uri($"{WANIKANI_BASE_ENDPOINT}/{endpoint}")
                Dim _data As JsonObject = Nothing
                Dim response As HttpResponseMessage = Nothing
                For retries = 3 To 1 Step -1
                    response = Await _httpclient.GetAsync(_uri)
                    If response.StatusCode = HttpStatusCode.Forbidden Then
                        ' The API will return a 403 if the rate limit is exceeded.
                        ' https://www.wanikani.com/api#getting-started
                        ' Requests are throttled at 100 hr^-1 corresponding
                        ' to a period of 36 s.
                        Await Task.Delay(TimeSpan.FromSeconds(36))
                        Continue For
                    End If
                    _data = JsonObject.Parse(Await response.Content.ReadAsStringAsync())
                    Try
                        Dim _data_error = _data.GetNamedObject("error")
                        Dim _data_error_message = _data_error.GetNamedString("message")
                        Throw New APIException(_data_error_message)
                    Catch err As Exception
                    End Try
                    Exit For
                Next
                Return New Result() With {.ResponseDate = response.Headers.Date?.Date, .Data = _data}
            End Function)
    End Function

    Friend Class Result
        Public Property ResponseDate As Nullable(Of Date)
        Public Property Data As JsonObject
    End Class

End Class
