// Djuranatomi Spelet
// Copyright (c) 2026 Torbjörn Nilsson och DSS Maja Blomqvist
// SPDX-License-Identifier: EUPL-1.2

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DjuranatomiSpelet.Views;

public enum DialogResult
{
    None,
    Ok,
    Yes,
    No,
    Cancel
}

/// <summary>Simple message dialogs (Avalonia has no built-in MessageBox).</summary>
public static class Dialogs
{
    public static Task Info(Window owner, string message) =>
        Show(owner, message, (DialogResult.Ok, "OK"));

    public static async Task<bool> Confirm(Window owner, string message, string yes = "Ja", string no = "Nej") =>
        await Show(owner, message, (DialogResult.Yes, yes), (DialogResult.No, no)) == DialogResult.Yes;

    /// <summary>"Spara / Spara inte / Avbryt". Closing the dialog counts as Cancel.</summary>
    public static async Task<DialogResult> AskSave(Window owner, string message = "Vill du spara ändringarna?")
    {
        var r = await Show(owner, message, (DialogResult.Yes, "Spara"), (DialogResult.No, "Spara inte"), (DialogResult.Cancel, "Avbryt"));
        return r == DialogResult.None ? DialogResult.Cancel : r;
    }

    public static async Task<DialogResult> Show(Window owner, string message, params (DialogResult Result, string Label)[] buttons)
    {
        var result = DialogResult.None;
        var dialog = new Window
        {
            Title = App.GameName,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White,
            MinWidth = 320
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        Button? first = null;
        foreach (var (r, label) in buttons)
        {
            var button = new Button { Content = label, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            button.Click += (_, _) =>
            {
                result = r;
                dialog.Close();
            };
            first ??= button;
            row.Children.Add(button);
        }

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24, 20),
            Spacing = 20,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 16, MaxWidth = 560 },
                row
            }
        };
        dialog.Opened += (_, _) => first?.Focus();

        await dialog.ShowDialog(owner);
        return result;
    }

    /// <summary>Runs an action and shows any error in a dialog instead of crashing the app.</summary>
    public static async Task Guard(Window owner, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await Info(owner, "Något gick fel:\n\n" + ex.Message);
        }
    }
}
