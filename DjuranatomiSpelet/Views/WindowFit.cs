// Djuranatomi Spelet
// Copyright (c) 2026 Torbjörn Nilsson och DSS Maja Blomqvist
// SPDX-License-Identifier: EUPL-1.2

using Avalonia.Controls;

namespace DjuranatomiSpelet.Views;

public static class WindowFit
{
    /// <summary>
    /// Shrinks a window that is larger than the screen's working area (small laptops, or high display
    /// scaling). Call in the constructor after InitializeComponent, before the window is shown.
    /// </summary>
    public static void ToWorkArea(Window window, double margin = 24)
    {
        try
        {
            var screen = window.Screens.Primary;
            if (screen is null) return;

            // WorkingArea is in device pixels; window sizes are in device-independent units.
            var width = screen.WorkingArea.Width / screen.Scaling;
            var height = screen.WorkingArea.Height / screen.Scaling;

            if (!double.IsNaN(window.Width) && window.Width > width - margin)
                window.Width = Math.Max(window.MinWidth, width - margin);
            if (!double.IsNaN(window.Height) && window.Height > height - margin)
                window.Height = Math.Max(window.MinHeight, height - margin);
        }
        catch
        {
            // Screen information is not available on every platform; keep the default size.
        }
    }
}
