Imports System.Xml
Imports MetroLog
Imports MetroLog.Targets
Imports Microsoft.Toolkit.Uwp.Notifications
Imports Windows.ApplicationModel.Background
Imports Windows.Data.Xml.Dom
Imports Windows.Foundation.Metadata
Imports Windows.Networking.Connectivity
Imports Windows.Storage
Imports Windows.UI.Notifications

Public NotInheritable Class NotifierTask
    Implements IBackgroundTask

    Friend Const TaskString = "NotifierTask"
    Friend Shared ReadOnly EntryPointString As String = GetType(NotifierTask).FullName
    Private Shared log As ILogger

    Shared Sub New()
        LogManagerFactory.DefaultConfiguration.AddTarget(LogLevel.Trace, LogLevel.Fatal, New StreamingFileTarget())
        log = LogManagerFactory.DefaultLogManager.GetLogger(Of NotifierTask)()
    End Sub

    Public Async Sub Run(taskInstance As IBackgroundTaskInstance) Implements IBackgroundTask.Run
        Dim deferral = taskInstance.GetDeferral()
        Try
            Await Notify()
        Catch ex As Exception
#If DEBUG Then
            Debugger.Break()
#End If
        Finally
            deferral.Complete()
        End Try
    End Sub

    Public Shared Function Notify() As IAsyncAction
        Return Notify(False)
    End Function

    Private Shared ReadOnly Property ApiKey() As String
        Get
            Return CStr(ApplicationData.Current.RoamingSettings.Values.ItemOrDefault("key"))
        End Get
    End Property

    Friend Shared Property CheckTime As Date
        Get
            Return New Date(CLng(If(ApplicationData.Current.LocalSettings.Values.ItemOrDefault("checkTime"), 0)))
        End Get
        Set(value As Date)
            ApplicationData.Current.LocalSettings.Values.Item("checkTime") = value.Ticks
        End Set
    End Property

    <DefaultOverload()>
    Public Shared Function Notify(force As Boolean) As IAsyncAction
        Return (
            Async Function() As Task
                log.Info($"{NameOf(Notify)} called")

                ' Check internet connectivity
                Dim connectionProfile = NetworkInformation.GetInternetConnectionProfile()
                If If(connectionProfile?.GetNetworkConnectivityLevel(), 0) <> NetworkConnectivityLevel.InternetAccess Then
                    log.Info("Returning: no internet.")
                    Return
                End If

                ' Get API key
                Dim roamingSettings = ApplicationData.Current.RoamingSettings
                Dim key = ApiKey
                If String.IsNullOrEmpty(key) Then
                    log.Info("Returning: API key is empty.")
                    TileUpdateManager.CreateTileUpdaterForApplication().Clear()
                    Return
                End If

                ' Manage delay time
                If Not force Then
                    If Date.Now < CheckTime Then
                        log.Info("Returning: check time has not passed.")
                        Return
                    End If
                End If

                Dim emitter As New WaniKaniEmitter(key)
                Dim notifierEx As Exception = Nothing
                AddHandler emitter.Notify,
                    Sub(sender, data)
                        log.Info($"Notifying: lessons={data.Lessons}, reviews={data.Reviews}")
                        SendNotifications(data)
                    End Sub
                AddHandler emitter.UnexpectedError,
                    Sub(sender, ex)
                        log.Error("Emitter unexpected error", ex)
                        notifierEx = ex
                    End Sub
                AddHandler emitter.LogTimeDiff, Sub(sender, timeDiff) log.Info($"Time diff: {XmlConvert.ToString(timeDiff)}")
                AddHandler emitter.LogUntouched, Sub() log.Info("Untouched.")
                AddHandler emitter.LogScheduled, Sub(sender, ts) log.Info($"Scheduled: {XmlConvert.ToString(ts)}")
                AddHandler emitter.LogNoPending, Sub(sender, ts) log.Info($"NoPending: {XmlConvert.ToString(ts)}")
                log.Info("Calling emitter process.")
                Dim delay = Await emitter.Process()
                log.Info("Emitter process done.")
                If delay Is Nothing Then
                    log.Info("Unregistering: vacation mode is on")
                    ' Vacation mode is on
                    ' Clear notifications.
                    TileUpdateManager.CreateTileUpdaterForApplication().Clear()
                    BadgeUpdateManager.CreateBadgeUpdaterForApplication().Clear()
                    ' End NotifierTask chain.
                    Await Unregister()
                    Return
                End If

                If notifierEx IsNot Nothing Then
                    log.Error("Notifier error occured.", notifierEx)
                    Throw notifierEx
                End If

                ' Set next check time 
                Dim newCheckTime = Date.Now + delay.Value
                log.Info("Set new check time to " + newCheckTime.ToString())
                CheckTime = newCheckTime
            End Function)().AsAsyncAction()
    End Function

    Public Shared Sub Reset()
        CheckTime = New Date(0)
        WaniKaniEmitter.Reset()
    End Sub

    Public Shared Sub SendNotifications(lessons As UInteger, reviews As UInteger)
        SendNotifications(New WaniKaniEmitter.StudyData() With {.Lessons = lessons, .Reviews = reviews})
    End Sub

    Friend Shared Async Sub SendNotifications(data As WaniKaniEmitter.StudyData)
        log.Info($"{NameOf(SendNotifications)} called")
        Dim notificationText = BuildStudyNotificationText(data)

        ' Send toast notification
        log.Info($"Sending toast notification; l={data.Lessons}, r={data.Reviews}")
        SendStudyToastNotification(data, notificationText)

        ' Send tile notification
        log.Info($"Sending tile notification; l={data.Lessons}, r={data.Reviews}")
        Dim tiles = Await BuildTileXml(notificationText)
        SendTileNotifications(tiles)

        ' Send badge notification 
        Dim total = data.Lessons + data.Reviews
        log.Info($"Sending badge notification; l={data.Lessons}, r={data.Reviews}, total={total}")
        SendBadgeNotification(total)
    End Sub

    Public Shared Function Register(delay As TimeSpan) As IAsyncOperation(Of BackgroundTaskRegistration)
        delay = {delay, TimeSpan.FromMinutes(15)}.Max()
        Return Register(New TimeTrigger(CUInt(delay.TotalMinutes), True))
    End Function

    <DefaultOverload()>
    Public Shared Function Register(trigger As IBackgroundTrigger) As IAsyncOperation(Of BackgroundTaskRegistration)
        Return (
            Async Function() As Task(Of BackgroundTaskRegistration)
                Await Unregister()
                ' Register new background task
                Dim backgroundAccessStatus = Await BackgroundExecutionManager.RequestAccessAsync()
                Select Case backgroundAccessStatus
                    Case BackgroundAccessStatus.AllowedSubjectToSystemPolicy, BackgroundAccessStatus.AlwaysAllowed
                        Dim taskBuilder As New BackgroundTaskBuilder()
                        taskBuilder.Name = TaskString
                        taskBuilder.TaskEntryPoint = EntryPointString
                        taskBuilder.SetTrigger(trigger)
                        Return taskBuilder.Register()
                End Select
                Return Nothing
            End Function
            )().AsAsyncOperation()
    End Function

    Public Shared Function Unregister() As IAsyncAction
        Return (
            Async Function() As Task
                Dim backgroundAccessStatus = Await BackgroundExecutionManager.RequestAccessAsync()
                Select Case backgroundAccessStatus
                    Case BackgroundAccessStatus.AllowedSubjectToSystemPolicy, BackgroundAccessStatus.AlwaysAllowed
                        For Each task In BackgroundTaskRegistration.AllTasks
                            If task.Value.Name = TaskString Then
                                task.Value.Unregister(True)
                            End If
                        Next
                End Select
            End Function)().AsAsyncAction()
    End Function

    Private Shared Function BuildStudyNotificationText(data As WaniKaniEmitter.StudyData) As String
        Dim lessons = data.Lessons
        Dim reviews = data.Reviews
        Dim lessonsStr = If(lessons = 1, "lesson", "lessons")
        Dim reviewsStr = If(reviews = 1, "review", "reviews")
        If reviews = 0 Then
            Return String.Format("You have {0} pending {1}.", data.Lessons, lessonsStr)
        ElseIf lessons = 0 Then
            Return String.Format("You have {0} pending {1}.", data.Reviews, reviewsStr)
        Else
            Return String.Format(
                "You have {0} pending {1} and {2} pending {3}.",
                data.Lessons,
                lessonsStr,
                data.Reviews,
                reviewsStr
                )
        End If
    End Function

#Region "Build tile notification XML"

    Private Shared Async Function BuildTileXml(notificationText As String) As Task(Of IEnumerable(Of XmlDocument))
        Try
            ' Get API key
            Dim apiKey = NotifierTask.ApiKey

            ' Get stats
            Dim stats As WaniKaniStats = Nothing
            If Not String.IsNullOrEmpty(apiKey) Then
                stats = Await WaniKaniStats.GetStats(apiKey)
            End If

            Return From tileContent In BuildTileXml_Core(notificationText, stats)
                   Select tileContent.GetXml()
        Catch ex As Exception
#If DEBUG Then
            Debugger.Break()
#End If
            Throw
        End Try
    End Function

    Private Shared Iterator Function BuildTileXml_Core(notificationText As String, stats As WaniKaniStats) As IEnumerable(Of TileContent)
        ' Main tile notification content 

        Dim mediumContent = New TileBindingContentAdaptive()
        mediumContent.Children.Add(NotifierTask.BuildTileXml_StudyData(notificationText))

        Dim wideContent = New TileBindingContentAdaptive()
        wideContent.Children.Add(NotifierTask.BuildTileXml_StudyData(notificationText))

        Dim largeContent = New TileBindingContentAdaptive()
        largeContent.Children.Add(NotifierTask.BuildTileXml_StudyData(notificationText))
        If stats IsNot Nothing Then
            largeContent.Children.AddRange(
                {
                    New AdaptiveText(),
                    BuildTileXml_LevelProgression(stats),
                    New AdaptiveText(),
                    BuildTileXml_CriticalItems(stats)
                }
            )
        End If

        Dim tiles As New List(Of TileContent)

        Yield New TileContent() With {
            .Visual = New TileVisual() With {
                .TileMedium = New TileBinding() With {.Content = mediumContent},
                .TileWide = New TileBinding() With {.Content = wideContent},
                .TileLarge = New TileBinding() With {.Content = largeContent}
            }
        }

        If stats IsNot Nothing Then
            ' Medium 2 / wide 2 tile
            Dim wide2Content = New TileBindingContentAdaptive()
            wide2Content.Children.Add(NotifierTask.BuildTileXml_LevelProgression(stats))
            Dim medium2Content = New TileBindingContentAdaptive()
            medium2Content.Children.AddRange(NotifierTask.BuildTileXml_LevelProgressionMini(stats))
            Yield New TileContent() With {
                .Visual = New TileVisual() With {
                    .TileMedium = New TileBinding() With {.Content = medium2Content},
                    .TileWide = New TileBinding() With {.Content = wide2Content}
                }
            }

            ' medium 3 / wide 3 tile
            Dim wide3Content = New TileBindingContentAdaptive()
            wide3Content.Children.Add(NotifierTask.BuildTileXml_CriticalItems(stats))
            Dim medium3Content = New TileBindingContentAdaptive()
            medium3Content.Children.Add(NotifierTask.BuildTileXml_CriticalItems(stats, 2))
            Yield New TileContent() With {
                .Visual = New TileVisual() With {
                    .TileMedium = New TileBinding() With {.Content = medium3Content},
                    .TileWide = New TileBinding() With {.Content = wide3Content}
                }
            }
        End If
    End Function

    Private Shared Function BuildTileXml_StudyData(notificationText As String) As ITileBindingContentAdaptiveChild
        Dim content = New TileBindingContentAdaptive()
        Dim plainAdaptiveText = New AdaptiveText() With {.Text = notificationText, .HintWrap = True}
        Return plainAdaptiveText
    End Function

    Private Shared Iterator Function BuildTileXml_LevelProgressionMini(stats As WaniKaniStats) As IEnumerable(Of ITileBindingContentAdaptiveChild)
        Yield New AdaptiveText() With {.Text = "Level", .HintStyle = AdaptiveTextStyle.CaptionSubtle}
        Yield New AdaptiveText() With {.Text = stats.Level.ToString()}
        Yield New AdaptiveText() With {.Text = "Progress", .HintStyle = AdaptiveTextStyle.CaptionSubtle}
        Dim progressionAverage = {stats.RadicalProgression, stats.KanjiProgression}.Average()
        Yield New AdaptiveText() With {.Text = String.Format("{0:#0%}", progressionAverage)}
    End Function

    Private Shared Function BuildTileXml_LevelProgression(stats As WaniKaniStats) As ITileBindingContentAdaptiveChild
        Dim levelProgressionGroup = New AdaptiveGroup()
        Dim levelSubgroup = New AdaptiveSubgroup() With {.HintWeight = 1}
        levelSubgroup.Children.Add(
            New AdaptiveText() With {
                .HintStyle = AdaptiveTextStyle.HeaderNumeral,
                .Text = stats.Level.ToString(),
                .HintAlign = AdaptiveTextAlign.Center
            }
        )
        levelSubgroup.Children.Add(
            New AdaptiveText() With {
                .HintStyle = AdaptiveTextStyle.BodySubtle,
                .Text = "Level",
                .HintAlign = AdaptiveTextAlign.Center
            }
        )
        Dim progressionSubgroup1 = New AdaptiveSubgroup() With {.HintWeight = 1, .HintTextStacking = AdaptiveSubgroupTextStacking.Bottom}
        progressionSubgroup1.Children.AddRange(
            {
                New AdaptiveText() With {.Text = "部首", .HintStyle = AdaptiveTextStyle.BodySubtle, .HintAlign = AdaptiveTextAlign.Center},
                New AdaptiveText() With {.Text = String.Format("{0:#0%}", stats.RadicalProgression), .HintAlign = AdaptiveTextAlign.Center}
            }
        )
        Dim progressionSubgroup2 = New AdaptiveSubgroup() With {.HintWeight = 1, .HintTextStacking = AdaptiveSubgroupTextStacking.Bottom}
        progressionSubgroup2.Children.AddRange(
            {
                New AdaptiveText() With {.Text = "漢字", .HintStyle = AdaptiveTextStyle.BodySubtle, .HintAlign = AdaptiveTextAlign.Center},
                New AdaptiveText() With {.Text = String.Format("{0:#0%}", stats.KanjiProgression), .HintAlign = AdaptiveTextAlign.Center}
            }
        )
        levelProgressionGroup.Children.AddRange({levelSubgroup, progressionSubgroup1, progressionSubgroup2})

        Return levelProgressionGroup
    End Function

    Private Shared Function BuildTileXml_CriticalItems(stats As WaniKaniStats, Optional count As Integer = 5) As ITileBindingContentAdaptiveChild
        Dim ciGroup = New AdaptiveGroup()
        For Each item In stats.CriticalItems.Take(count)
            Dim subgroup As New AdaptiveSubgroup() With {.HintWeight = 1}
            Dim subgroupItem1 As IAdaptiveSubgroupChild
            If String.IsNullOrEmpty(item.Character) Then
                subgroupItem1 = New AdaptiveImage() With {
                    .Source = item.Image,
                    .AlternateText = "No text provided by server."
                }
            Else
                Dim subgroupItem1Text = New AdaptiveText() With {
                    .HintAlign = AdaptiveTextAlign.Center,
                    .HintMaxLines = 1,
                    .Language = "ja-JP",
                    .Text = item.Character,
                    .HintStyle = AdaptiveTextStyle.Caption
                }
                Select Case item.Type
                    Case WaniKaniStats.ItemType.Radical, WaniKaniStats.ItemType.Kanji
                        subgroupItem1Text.HintStyle = AdaptiveTextStyle.Subtitle
                    Case WaniKaniStats.ItemType.Vocabulary
                        subgroupItem1Text.HintStyle =
                            If(item.Character.Length <= 3, AdaptiveTextStyle.Subtitle, AdaptiveTextStyle.Body)
                End Select
                subgroupItem1 = subgroupItem1Text
            End If
            subgroup.Children.Add(subgroupItem1)
            subgroup.Children.Add(New AdaptiveText()) ' spacing
            subgroup.Children.Add(New AdaptiveText() With {.HintAlign = AdaptiveTextAlign.Center, .Text = $"{item.Completion}%"})
            ciGroup.Children.Add(subgroup)
        Next
        Return ciGroup
    End Function

#End Region

    Private Shared Sub SendTileNotifications(xml As IEnumerable(Of XmlDocument))
        Dim updater = TileUpdateManager.CreateTileUpdaterForApplication()
        updater.Clear()
        updater.EnableNotificationQueue(True)
        For Each item In xml
            updater.Update(New TileNotification(item))
        Next
    End Sub

    Private Shared Sub SendToastNotification(xml As XmlDocument, group As String, Optional tag As String = Nothing)
        Dim notifier = ToastNotificationManager.CreateToastNotifier()
        Dim notif = New ToastNotification(xml)
        notif.Group = group
        If tag IsNot Nothing Then
            notif.Tag = tag
        End If
        notifier.Show(notif)
    End Sub

    Private Shared Sub SendBadgeNotification(num As UInteger)
        Dim updater = BadgeUpdateManager.CreateBadgeUpdaterForApplication()
        updater.Update(New BadgeNotification((New BadgeNumericContent() With {.Number = num}).GetXml()))
    End Sub

    Private Shared Sub SendStudyToastNotification(data As WaniKaniEmitter.StudyData, notificationText As String)
        Dim binding As New ToastBindingGeneric()
        binding.Children.Add(New AdaptiveText() With {.Text = notificationText})
        Dim visual As New ToastVisual() With {.BindingGeneric = binding}
        Dim content As New ToastContent() With {.Visual = visual}

        If data.Lessons = 0 Then
            SetActivationParameters(content, WaniKaniWeb.REVIEWS_URI, "reviews=")
        ElseIf data.Reviews = 0 Then
            SetActivationParameters(content, WaniKaniWeb.LESSONS_URI, "lessons=")
        Else
            SetActivationParameters(content, WaniKaniWeb.DASHBOARD_URI, "dashboard=")
            Dim actions As New ToastActionsCustom()
            content.Actions = actions
            actions.Buttons.Add(CreateToastButton("Lessons", WaniKaniWeb.LESSONS_URI, "lessons="))
            actions.Buttons.Add(CreateToastButton("Reviews", WaniKaniWeb.REVIEWS_URI, "reviews="))
        End If
        ToastNotificationManager.History.Remove("1", "notify")
        SendToastNotification(content.GetXml(), "notify", "1")
    End Sub

    Private Shared Sub SetActivationParameters(content As ToastContent, browserUriString As String, foregroundLaunchString As String)
        Dim roamingSettings = ApplicationData.Current.RoamingSettings
        Dim _activationType = DirectCast(CUInt(roamingSettings.Values.Item("activationTarget")), ActivationTargetType)
        Select Case _activationType
            Case ActivationTargetType.DefaultBrowser
                content.ActivationType = ToastActivationType.Protocol
                content.Launch = browserUriString
            Case ActivationTargetType.App
                content.ActivationType = ToastActivationType.Foreground
                content.Launch = foregroundLaunchString
        End Select
    End Sub

    Private Shared Function CreateToastButton(content As String, browserUriString As String, foregroundLaunchString As String) As ToastButton
        Dim roamingSettings = ApplicationData.Current.RoamingSettings
        Dim _activationType = DirectCast(CUInt(roamingSettings.Values.Item("activationTarget")), ActivationTargetType)
        Dim button As ToastButton = Nothing
        Select Case _activationType
            Case ActivationTargetType.DefaultBrowser
                button = New ToastButton(content, browserUriString)
                button.ActivationType = ToastActivationType.Protocol
            Case ActivationTargetType.App
                button = New ToastButton(content, foregroundLaunchString)
                button.ActivationType = ToastActivationType.Foreground
        End Select
        Return button
    End Function

End Class
