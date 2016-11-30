Imports Windows.UI

''' <summary>
''' Provides application-specific behavior to supplement the default Application class.
''' </summary>
NotInheritable Class App
    Inherits Application

    Private Sub InitializeApp()
        ' Change title bar color
        Dim titleBar = ApplicationView.GetForCurrentView().TitleBar
        Dim mainColor = DirectCast(Resources.Item("MainColor"), Color)
        Dim white = Colors.White
        titleBar.BackgroundColor = mainColor
        titleBar.ForegroundColor = white
        titleBar.ButtonBackgroundColor = mainColor
        titleBar.ButtonForegroundColor = white

        Dim rootFrame As Frame = TryCast(Window.Current.Content, Frame)

        ' Do not repeat app initialization when the Window already has content,
        ' just ensure that the window is active

        If rootFrame Is Nothing Then
            ' Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = New Frame()

            AddHandler rootFrame.NavigationFailed, AddressOf OnNavigationFailed

            ' Place the frame in the current Window
            Window.Current.Content = rootFrame
        End If
    End Sub

    ''' <summary>
    ''' Invoked when the application is launched normally by the end user.  Other entry points
    ''' will be used when the application is launched to open a specific file, to display
    ''' search results, and so forth.
    ''' </summary>
    ''' <param name="e">Details about the launch request and process.</param>
    Protected Overrides Sub OnLaunched(e As LaunchActivatedEventArgs)
        InitializeApp()

        Dim rootFrame As Frame = TryCast(Window.Current.Content, Frame)

        If e.PrelaunchActivated = False Then
            If rootFrame.Content Is Nothing Then
                ' When the navigation stack isn't restored navigate to the first page,
                ' configuring the new page by passing required information as a navigation
                ' parameter
                rootFrame.Navigate(GetType(MainPage), e.Arguments)
            End If

            ' Ensure the current window is active
            Window.Current.Activate()
        End If
    End Sub

    Protected Overrides Sub OnActivated(args As IActivatedEventArgs)
        InitializeApp()

        Dim rootFrame = DirectCast(Window.Current.Content, Frame)

        If args.Kind = ActivationKind.ToastNotification Then
            Dim toastArgs = DirectCast(args, ToastNotificationActivatedEventArgs)
            DirectCast(Window.Current.Content, Frame).Navigate(GetType(MainPage), toastArgs.Argument)
        End If

        ' Ensure the current window is active
        Window.Current.Activate()
    End Sub

    ''' <summary>
    ''' Invoked when Navigation to a certain page fails
    ''' </summary>
    ''' <param name="sender">The Frame which failed navigation</param>
    ''' <param name="e">Details about the navigation failure</param>
    Private Sub OnNavigationFailed(sender As Object, e As NavigationFailedEventArgs)
        Throw New Exception("Failed to load Page " + e.SourcePageType.FullName)
    End Sub

End Class
