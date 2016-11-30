Imports Tasks
Imports Windows.ApplicationModel.Background
Imports Windows.Storage
Imports Windows.System

''' <summary>
''' An empty page that can be used on its own or navigated to within a Frame.
''' </summary>
Public NotInheritable Class MainPage
    Inherits Page

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
#If Not DEBUG Then
        ' Hide debug pivot item
        DebugPivotItem.Visibility = Visibility.Collapsed
#End If

        ' Read settings
        Dim settings = ApplicationData.Current.RoamingSettings
        apiKeyTextBox.Text = ApiKey
        Select Case ActivationTarget
            Case ActivationTargetType.DefaultBrowser
                ActivationTargetDefaultBrowserRadioButton.IsChecked = True
            Case ActivationTargetType.App
                ActivationTargetAppRadioButton.IsChecked = True
        End Select

        ' Read application arguments: switch pivot item if argument is provided.
        Select Case CStr(e.Parameter)
            Case "lessons="
                MainPivot.SelectedItem = LessonsPivotItem
            Case "reviews="
                MainPivot.SelectedItem = ReviewsPivotItem
        End Select

        ' Register background tasks
        RegisterBackgroundTasks()

        ' Run the notifier task once.
        Await NotifierTask.Notify(True)
    End Sub

    Private Sub SaveSettings(sender As Object, e As RoutedEventArgs)
        ApiKey = apiKeyTextBox.Text
        If ActivationTargetDefaultBrowserRadioButton.IsChecked Then
            ActivationTarget = ActivationTargetType.DefaultBrowser
        ElseIf ActivationTargetAppRadioButton.IsChecked Then
            ActivationTarget = ActivationTargetType.App
        End If
        RegisterBackgroundTasks()
    End Sub

    Private Async Sub MainPivot_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        Dim _activationTarget = ActivationTarget
        If MainPivot.SelectedItem Is LessonsPivotItem Then
            Select Case _activationTarget
                Case ActivationTargetType.DefaultBrowser
                    MainPivot.SelectedIndex = 1
                    Await LaunchBrowser(WaniKaniWeb.LESSONS_URI)
                Case ActivationTargetType.App
                    ReloadWebViewForPivotItem(LessonsPivotItem, WaniKaniWeb.LESSONS_URI)
            End Select
        ElseIf MainPivot.SelectedItem Is ReviewsPivotItem Then
            Select Case _activationTarget
                Case ActivationTargetType.DefaultBrowser
                    MainPivot.SelectedIndex = 1
                    Await LaunchBrowser(WaniKaniWeb.REVIEWS_URI)
                Case ActivationTargetType.App
                    ReloadWebViewForPivotItem(ReviewsPivotItem, WaniKaniWeb.REVIEWS_URI)
            End Select
        End If
    End Sub

    Private Async Function LaunchBrowser(uriString As String) As Task
        Await Launcher.LaunchUriAsync(New Uri(uriString))
    End Function

    Private Sub ReloadWebViewForPivotItem(pivotItem As PivotItem, uriString As String)
        Dim webView As WebView = DirectCast(pivotItem.Content, WebView)
        If webView Is Nothing Then
            webView = New WebView()
            pivotItem.Content = webView
        End If
        webView.Source = New Uri(uriString)
    End Sub

    Private Async Sub RegisterBackgroundTasks()
        Dim trigger As New TimeTrigger(15, False)
        Dim registration = Await NotifierTask.Register(trigger)
    End Sub

    Private Property ApiKey As String
        Get
            Return CStr(ApplicationData.Current.RoamingSettings.Values.ItemOrDefault("key"))
        End Get
        Set(value As String)
            ApplicationData.Current.RoamingSettings.Values.Item("key") = value
        End Set
    End Property

    Private Property ActivationTarget As ActivationTargetType
        Get
            Dim val = ApplicationData.Current.RoamingSettings.Values.ItemOrDefault("activationTarget")
            Return DirectCast(CUInt(val), ActivationTargetType)
        End Get
        Set(value As ActivationTargetType)
            ApplicationData.Current.RoamingSettings.Values.Item("activationTarget") = CUInt(value)
        End Set
    End Property

#Region "Debug"

    Private Sub Test()
        RegisterBackgroundTasks()
    End Sub

    Private Sub SendDummyLessons()
        NotifierTask.SendNotifications(42, 0)
    End Sub

    Private Sub SendDummyLessonsAndReviews()
        NotifierTask.SendNotifications(21, 21)
    End Sub

    Private Sub ResetNotifier()
        NotifierTask.Reset()
    End Sub

    Private Async Sub Notify()
        Await NotifierTask.Notify(True)
    End Sub

    Private Sub ResetCritical()
        ApplicationData.Current.LocalSettings.Values.Remove("WaniKaniCriticalItems.LastAccessed")
    End Sub

#End Region

End Class
