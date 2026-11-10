using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace Sakuno.Avalonia.Headless.Driver.Sample;

public sealed class LoginWindow : Window
{
    private readonly TextBox _usernameTextBox;
    private readonly TextBox _passwordTextBox;
    private readonly TextBlock _statusText;

    public LoginWindow()
    {
        Title = "Headless Driver Sample";
        Width = 480;
        Height = 720;

        _usernameTextBox = new TextBox
        {
            Name = "UsernameTextBox",
            PlaceholderText = "Username"
        };
        AutomationProperties.SetAutomationId(_usernameTextBox, "UsernameTextBox");

        _passwordTextBox = new TextBox
        {
            Name = "PasswordTextBox",
            PlaceholderText = "Password",
            PasswordChar = '*'
        };
        AutomationProperties.SetAutomationId(_passwordTextBox, "PasswordTextBox");

        _statusText = new TextBlock
        {
            Name = "StatusText",
            Text = "Not authenticated"
        };
        AutomationProperties.SetAutomationId(_statusText, "StatusText");

        var loginButton = new Button
        {
            Name = "LoginButton",
            Content = "Log in",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetAutomationId(loginButton, "LoginButton");
        loginButton.Click += (_, _) => Authenticate();

        var delayedLoginButton = new Button
        {
            Name = "DelayedLoginButton",
            Content = "Log in after a delay",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetAutomationId(delayedLoginButton, "DelayedLoginButton");
        delayedLoginButton.Click += async (_, _) =>
        {
            await Task.Delay(500);
            _statusText.Text = "Delayed authentication completed";
        };

        var openDialogButton = new Button
        {
            Name = "OpenDialogButton",
            Content = "Open dialog",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetAutomationId(openDialogButton, "OpenDialogButton");
        openDialogButton.Click += async (_, _) => await OpenDialog();

        var firstSameNameButton = new Button
        {
            Name = "SameNameButton",
            Content = "First duplicate"
        };
        var secondSameNameButton = new Button
        {
            Name = "SameNameButton",
            Content = "Second duplicate"
        };

        var disabledButton = new Button
        {
            Name = "DisabledButton",
            Content = "Disabled",
            IsEnabled = false
        };
        AutomationProperties.SetAutomationId(disabledButton, "DisabledButton");

        var hiddenButton = new Button
        {
            Name = "HiddenButton",
            Content = "Hidden",
            IsVisible = false
        };
        AutomationProperties.SetAutomationId(hiddenButton, "HiddenButton");

        var toggleSwitch = new ToggleSwitch
        {
            Name = "ToggleSwitch",
            Content = "Enable option"
        };
        AutomationProperties.SetAutomationId(toggleSwitch, "ToggleSwitch");

        var comboBox = new ComboBox
        {
            Name = "SelectionComboBox",
            ItemsSource = new[] { "First item", "Second item", "Third item" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetAutomationId(comboBox, "SelectionComboBox");

        var scrollContent = new StackPanel { Spacing = 8 };
        foreach (var index in Enumerable.Range(1, 20))
        {
            scrollContent.Children.Add(new TextBlock { Text = $"Scrollable item {index}" });
        }

        var scrollViewer = new ScrollViewer
        {
            Name = "SampleScrollViewer",
            Height = 160,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = scrollContent
        };
        AutomationProperties.SetAutomationId(scrollViewer, "SampleScrollViewer");

        var virtualizedList = new ListBox
        {
            Name = "VirtualizedList",
            Height = 100,
            ItemsSource = Enumerable.Range(1, 1000).Select(index => $"Virtual item {index}").ToArray()
        };
        AutomationProperties.SetAutomationId(virtualizedList, "VirtualizedList");

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "Sample login", FontSize = 24 },
                    _usernameTextBox,
                    _passwordTextBox,
                    loginButton,
                    delayedLoginButton,
                    openDialogButton,
                    _statusText,
                    firstSameNameButton,
                    secondSameNameButton,
                    disabledButton,
                    hiddenButton,
                    toggleSwitch,
                    comboBox,
                    scrollViewer,
                    virtualizedList
                }
            }
        };
    }

    private void Authenticate()
    {
        _statusText.Text = _usernameTextBox.Text == "agent" && _passwordTextBox.Text == "correct-password"
            ? "Authenticated"
            : "Authentication failed";
    }

    private async Task OpenDialog()
    {
        var dialogLoginButton = new Button
        {
            Name = "LoginButton",
            Content = "Dialog login",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetAutomationId(dialogLoginButton, "LoginButton");

        var dialog = new Window
        {
            Title = "Sample dialog",
            Width = 320,
            Height = 180,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "This dialog contains another LoginButton." },
                    dialogLoginButton
                }
            }
        };

        await dialog.ShowDialog(this);
    }
}
