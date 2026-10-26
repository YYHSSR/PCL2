Imports System.Windows.Threading

''' <summary>
''' Indicates that the page can be refreshed by pressing F5 or by other means.
''' </summary>
Public Interface IRefreshable
    Sub Refresh()
End Interface

''' <summary>
''' Indicates that the page can handle uncaught exceptions.
''' </summary>
Public Interface IDispatcherUnhandledException
    Sub DispatcherUnhandledException(sender As Object, e As DispatcherUnhandledExceptionEventArgs)
End Interface

''' <summary>
''' Indicates that the control is checkable.
''' </summary>
Public Interface IMyRadio
    Event Check(sender As Object, e As RouteEventArgs)
    Event Changed(sender As Object, e As RouteEventArgs)
End Interface
