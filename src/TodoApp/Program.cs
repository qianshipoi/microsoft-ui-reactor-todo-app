using Microsoft.UI.Reactor;

// The whole entry point. No App.xaml, no MainWindow.xaml, no code-behind:
// top-level statements boot the Reactor host and App renders the window contents.
// See https://github.com/microsoft/microsoft-ui-reactor
ReactorApp.Run<ReactorTodo.App>("Reactor Todo", width: 640, height: 820);
