' This is a port of wanikani-emitter to WinRT.
' https://github.com/seangenabe/wanikani-emitter

Imports System.Threading
Imports Windows.Storage

Public NotInheritable Class WaniKaniEmitter

    Public Property ErrorSuspendDuration As TimeSpan = TimeSpan.FromMinutes(5)
    Public Property NotifiedSuspendDuration As TimeSpan = TimeSpan.FromMinutes(10)
    Public Property WaitingSuspendDuration As TimeSpan = TimeSpan.FromSeconds(36)
    Public Property MiniLag As TimeSpan = TimeSpan.FromSeconds(1)
    Public ReadOnly Property Key As String
    Private Shared settings As ApplicationDataContainer = ApplicationData.Current.RoamingSettings

    Private Shared Property Lessons As UInteger
        Get
            Try
                Return CUInt(settings.Values.Item("WaniKaniEmitter.Lessons"))
            Catch ex As KeyNotFoundException
                Return 0
            End Try
        End Get
        Set(value As UInteger)
            settings.Values.Item("WaniKaniEmitter.Lessons") = value
        End Set
    End Property

    Private Shared Property Reviews As UInteger
        Get
            Try
                Return CUInt(settings.Values.Item("WaniKaniEmitter.Reviews"))
            Catch ex As KeyNotFoundException
                Return 0
            End Try
        End Get
        Set(value As UInteger)
            settings.Values.Item("WaniKaniEmitter.Reviews") = value
        End Set
    End Property

    Public Sub New(key As String)
        If String.IsNullOrEmpty(key) Then
            Throw New ArgumentException("API key not specified.", NameOf(key))
        End If
        Me.Key = key
    End Sub

    Public Function Start() As IAsyncAction
        Return AsyncInfo.Run(
            Async Function(cancel As CancellationToken) As Task
                Do While True
                    cancel.ThrowIfCancellationRequested()
                    Dim delayDuration = Await Process()
                    cancel.ThrowIfCancellationRequested()
                    If delayDuration Is Nothing Then
                        Return
                    Else
                        Await Task.Delay(delayDuration.Value)
                    End If
                Loop
            End Function)
    End Function

    ''' <summary>
    ''' Checks for pending notifications.
    ''' </summary>
    ''' <returns></returns>
    Public Function Process() As IAsyncOperation(Of Nullable(Of TimeSpan))
        Return (
            Async Function() As Task(Of Nullable(Of TimeSpan))
                Try
                    ' Check for new items
                    Dim result = Await WaniKaniApi.UserRequest(Key, "study-queue")
                    Dim _data = result.Data

                    ' Assuming server is ahead.
                    Dim now = DateTimeOffset.Now
                    Dim timeDifference = If(result.ResponseDate, now) - now
                    OnLogTimeDiff(timeDifference)

                    Dim _data_ri = _data.GetNamedObject("requested_information")
                    Dim lessons = CUInt(_data_ri.GetNamedNumber("lessons_available", 0))
                    Dim reviews = CUInt(_data_ri.GetNamedNumber("reviews_available", 0))
                    If lessons <> 0 OrElse reviews <> 0 Then
                        If WaniKaniEmitter.Lessons <> lessons OrElse WaniKaniEmitter.Reviews <> reviews Then
                            WaniKaniEmitter.Lessons = lessons
                            WaniKaniEmitter.Reviews = reviews
                            OnNotify(New StudyData() With {.Lessons = lessons, .Reviews = reviews})
                        Else
                            OnLogUntouched()
                        End If
                        Return ScheduleNextCheck(NotifiedSuspendDuration)
                    End If

                    ' Get the time of the next review 
                    Dim nextReviewRaw As Double
                    Try
                        nextReviewRaw = _data_ri.GetNamedNumber("next_review_date")
                    Catch ex As Exception
                        ' If next_review_date is null, the user is on vacation mode.
                        Return Nothing
                    End Try
                    Dim nextReview = FromUnixTimestamp(CULng(nextReviewRaw))
                    ' Get the amount of time before the next review.
                    ' Correct for the observed time difference.
                    ' Add a small amount of time so we will land slightly ahead when the new items are available.
                    Dim timeBeforeNextReview = nextReview - timeDifference - now + MiniLag
                    OnLogNoPending(timeBeforeNextReview)
                    Dim nextDelay = {timeBeforeNextReview, WaitingSuspendDuration}.Max()
                    Return ScheduleNextCheck(nextDelay)
                Catch ex As Exception
                    OnUnexpectedError(ex)
                    Return ScheduleNextCheck(ErrorSuspendDuration)
                End Try
            End Function)().AsAsyncOperation()
    End Function

    Public Shared Sub Reset()
        Lessons = 0
        Reviews = 0
    End Sub

    Private Function ScheduleNextCheck(duration As TimeSpan) As TimeSpan
        OnLogScheduled(duration)
        Return duration
    End Function

    Private Shared Function FromUnixTimestamp(unixTimestamp As ULong) As Date
        Return New Date(1970, 1, 1) + TimeSpan.FromSeconds(unixTimestamp)
    End Function

    Friend Event LogTimeDiff As TypedEventHandler(Of WaniKaniEmitter, TimeSpan)
    Friend Event Notify As TypedEventHandler(Of WaniKaniEmitter, StudyData)
    Friend Event LogUntouched As TypedEventHandler(Of WaniKaniEmitter, Object)
    Friend Event LogScheduled As TypedEventHandler(Of WaniKaniEmitter, TimeSpan)
    Friend Event LogNoPending As TypedEventHandler(Of WaniKaniEmitter, TimeSpan)
    Friend Event UnexpectedError As TypedEventHandler(Of WaniKaniEmitter, Exception)

#Region "Event callers"

    Private Sub OnLogTimeDiff(ts As TimeSpan)
        RaiseEvent LogTimeDiff(Me, ts)
    End Sub

    Private Sub OnNotify(studyData As StudyData)
        RaiseEvent Notify(Me, studyData)
    End Sub

    Private Sub OnLogUntouched()
        RaiseEvent LogUntouched(Me, Nothing)
    End Sub

    Private Sub OnLogScheduled(ts As TimeSpan)
        RaiseEvent LogScheduled(Me, ts)
    End Sub

    Private Sub OnLogNoPending(ts As TimeSpan)
        RaiseEvent LogNoPending(Me, ts)
    End Sub

    Private Sub OnUnexpectedError(ex As Exception)
        RaiseEvent UnexpectedError(Me, ex)
    End Sub

#End Region

    Friend NotInheritable Class StudyData
        Public Property Lessons As UInteger
        Public Property Reviews As UInteger
    End Class

End Class
